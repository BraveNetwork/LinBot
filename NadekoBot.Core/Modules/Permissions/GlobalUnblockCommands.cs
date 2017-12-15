using Discord;
using Discord.Commands;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;
using System.Linq;
using System.Threading.Tasks;
using NadekoBot.Common.Attributes;
using NadekoBot.Common.TypeReaders;
using NadekoBot.Modules.Permissions.Services;
using NadekoBot.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace NadekoBot.Modules.Permissions
{
    public partial class Permissions
    {
        [Group]
        public class GlobalUnblockCommands : NadekoSubmodule
        {
            private GlobalPermissionService _service;
            private readonly DbService _db;

            public GlobalUnblockCommands(GlobalPermissionService service, DbService db)
            {
                _service = service;
                _db = db;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task Lgu(string listName)
            {
                if (listName == "") // TODO: Check if name is in whitelistgroups
                {
                    await ReplyErrorLocalized("lgu_invalidname").ConfigureAwait(false);
                    return;
                }
                // TODO: Attempt to find related modules/commands

                if (!_service.UnblockedModules.Any() && !_service.UnblockedCommands.Any())
                {
                    await ReplyErrorLocalized("lgu_none").ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithOkColor();

                if (_service.UnblockedModules.Any())
                    embed.AddField(efb => efb.WithName(GetText("unblocked_modules")).WithValue(string.Join("\n", _service.UnblockedModules)).WithIsInline(false));

                if (_service.UnblockedCommands.Any())
                    embed.AddField(efb => efb.WithName(GetText("unblocked_commands")).WithValue(string.Join("\n", _service.UnblockedCommands)).WithIsInline(false));

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task Ubm(ModuleOrCrInfo module)
            {
                var moduleName = module.Name.ToLowerInvariant();
                if (_service.UnblockedModules.Add(moduleName))
                {
                    using (var uow = _db.UnitOfWork)
                    {
                        var bc = uow.BotConfig.GetOrCreate(set => set.Include(x => x.UnblockedModules));
                        bc.UnblockedModules.Add(new UnblockedCmdOrMdl
                        {
                            Name = moduleName,
                        });
                        uow.Complete();
                    }
                    await ReplyConfirmLocalized("ubm_add", Format.Bold(module.Name)).ConfigureAwait(false);
                    return;
                }
                else if (_service.UnblockedModules.TryRemove(moduleName))
                {
                    using (var uow = _db.UnitOfWork)
                    {
                        var bc = uow.BotConfig.GetOrCreate(set => set.Include(x => x.UnblockedModules));
                        bc.UnblockedModules.RemoveWhere(x => x.Name == moduleName);
                        uow.Complete();
                    }
                    await ReplyConfirmLocalized("ubm_remove", Format.Bold(module.Name)).ConfigureAwait(false);
                    return;
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task Ubc(CommandOrCrInfo cmd)
            {
                var commandName = cmd.Name.ToLowerInvariant();
                if (_service.UnblockedCommands.Add(commandName))
                {
                    using (var uow = _db.UnitOfWork)
                    {
                        var bc = uow.BotConfig.GetOrCreate(set => set.Include(x => x.UnblockedCommands));
                        bc.UnblockedCommands.Add(new UnblockedCmdOrMdl
                        {
                            Name = commandName,
                        });
                        uow.Complete();
                    }
                    await ReplyConfirmLocalized("ubc_add", Format.Bold(cmd.Name)).ConfigureAwait(false);
                    return;
                }
                else if (_service.UnblockedCommands.TryRemove(commandName))
                {
                    using (var uow = _db.UnitOfWork)
                    {
                        var bc = uow.BotConfig.GetOrCreate(set => set.Include(x => x.UnblockedCommands));
                        bc.UnblockedCommands.RemoveWhere(x => x.Name == commandName);
                        uow.Complete();
                    }
                    await ReplyConfirmLocalized("ubc_remove", Format.Bold(cmd.Name)).ConfigureAwait(false);
                    return;
                }
            }
        }
    }
}
