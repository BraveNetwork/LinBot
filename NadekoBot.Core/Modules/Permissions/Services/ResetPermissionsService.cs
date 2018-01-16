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

        public async Task ResetGlobalWhitelists(bool purge)
        {
            using (var uow = _db.UnitOfWork)
            {
                // TODO: delete all records for GlobalWhitelistSet
                // TODO: if purge == true, delete all records for GlobalWhitelistItem and UnblockedCmdOrMdl
                var gc = uow.BotConfig.GetOrCreate();
                gc.UnblockedCommands.Clear();
                gc.UnblockedModules.Clear();

                _globalPerms.UnblockedCommands.Clear();
                _globalPerms.UnblockedModules.Clear();
                await uow.CompleteAsync().ConfigureAwait(false);
            }
        }

        public async Task ResetGlobalUnblocked()
        {
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
        }
    }
}
