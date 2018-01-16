using System.Threading.Tasks;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace NadekoBot.Modules.Permissions.Services
{
    public class ResetPermissionsService : INService
    {
        private readonly PermissionService _perms;
        private readonly GlobalPermissionService _globalPerms;
        private readonly DbService _db;

        public ResetPermissionsService(PermissionService perms, GlobalPermissionService globalPerms, DbService db)
        {
            _perms = perms;
            _globalPerms = globalPerms;
            _db = db;
        }

        public async Task ResetPermissions(ulong guildId)
        {
            using (var uow = _db.UnitOfWork)
            {
                var config = uow.GuildConfigs.GcWithPermissionsv2For(guildId);
                config.Permissions = Permissionv2.GetDefaultPermlist;
                await uow.CompleteAsync().ConfigureAwait(false);
                _perms.UpdateCache(config);
            }
        }

        public async Task ResetGlobalPermissions()
        {
            using (var uow = _db.UnitOfWork)
            {
                var gc = uow.BotConfig.GetOrCreate();
                gc.BlockedCommands.Clear();
                gc.BlockedModules.Clear();

                _globalPerms.BlockedCommands.Clear();
                _globalPerms.BlockedModules.Clear();
                await uow.CompleteAsync().ConfigureAwait(false);
            }
        }

        public async Task<bool> ResetGlobalWhitelists(bool purge)
        {
			try {
				using (var uow = _db.UnitOfWork)
				{
					BotConfig gc;
					string sql;

					if (purge) { // Delete all members and unblocked
						gc = uow.BotConfig.GetOrCreate(set => set
							.Include(x => x.GlobalWhitelistGroups)
							.Include(x => x.GlobalWhitelistMembers)
							.Include(x => x.UnblockedModules)
							.Include(x => x.UnblockedCommands));
						sql = "DELETE from GlobalWhitelistSet; DELETE from UnblockedCmdOrMdl; DELETE from GlobalWhitelistItem;";
						// Unlink the members and unblocked collections
						gc.GlobalWhitelistMembers.Clear();
						gc.UnblockedCommands.Clear();
						gc.UnblockedModules.Clear();
						_globalPerms.UnblockedCommands.Clear();
						_globalPerms.UnblockedModules.Clear();

					} else { // Just delete the groups
						gc = uow.BotConfig.GetOrCreate(set => set
							.Include(x => x.GlobalWhitelistGroups));
						sql = "DELETE from GlobalWhitelistSet;";
					}

					// Unlink the whitelist groups
					gc.GlobalWhitelistGroups.Clear();
					// Finish up changes so we can do more things
					uow._context.SaveChanges();
					// Execute the sql query to delete all relevant table data
					uow._context.Database.ExecuteSqlCommand(sql);

					await uow.CompleteAsync().ConfigureAwait(false);
				}
				// Successful!
				return true;

			} catch (System.Exception e) {
				// Failure...
				// TODO: Use the Nadeko Logger (NLog?)
				System.Console.WriteLine("Exception caught while trying to use ResetGlobalWhitelists:\n{0}", e);
				return false;
			}
        }

        public async Task<bool> ResetGlobalUnblocked()
        {
			try {
				using (var uow = _db.UnitOfWork)
				{
					var gc = uow.BotConfig.GetOrCreate(set => set
						.Include(x => x.UnblockedModules)
						.Include(x => x.UnblockedCommands));

					// Unlinks the data in the UnblockedCmdOrMdl table from BotConfig
					gc.UnblockedCommands.Clear();
					gc.UnblockedModules.Clear();

					// Clear the readonly hash sets from GlobalPermissionService
					_globalPerms.UnblockedCommands.Clear();
					_globalPerms.UnblockedModules.Clear();

					//var count = await uow._context.Set<UnblockedCmdOrMdl>().CountAsync();
					//System.Console.WriteLine("Database record count {0}", count);

					// Ensure the database table is ready to be cleared
					uow._context.SaveChanges();

					// Delete all records from UnblockedCmdOrMdl table
					uow._context.Database.ExecuteSqlCommand("DELETE from UnblockedCmdOrMdl;");

					//count = await uow._context.Set<UnblockedCmdOrMdl>().CountAsync();
					//System.Console.WriteLine("Database record count after DELETE: {0}", count);

					await uow.CompleteAsync().ConfigureAwait(false);
				}

				// Successful!
				return true;
				
			} catch (System.Exception e) {
				// Failure...
				// TODO: Use the Nadeko Logger (NLog?)
				System.Console.WriteLine("Exception caught while trying to use ResetGlobalUnblocked:\n{0}", e);
				return false;
			}
        }
    }
}
