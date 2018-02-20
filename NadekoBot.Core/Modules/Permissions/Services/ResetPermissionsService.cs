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
					string sql;

					if (purge) { // Delete all members and unblocked
						BotConfig gc = uow.BotConfig.GetOrCreate(set => set
							.Include(x => x.UnblockedModules)
							.Include(x => x.UnblockedCommands));
						sql = "DELETE from GWLSet; DELETE from UnblockedCmdOrMdl; DELETE from GWLItem;";
						// Unlink the members and unblocked collections
						gc.UnblockedCommands.Clear();
						gc.UnblockedModules.Clear();
						_globalPerms.UnblockedCommands.Clear();
						_globalPerms.UnblockedModules.Clear();
						// Finish up changes so we can do more things
						uow._context.SaveChanges();

					} else { // Just delete the groups
						sql = "DELETE from GWLSet;";
					}

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
