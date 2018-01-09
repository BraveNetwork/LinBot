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
        [Group("GlobalWhitelist")]
        public class GlobalUnblockCommands : NadekoSubmodule
        {
            private GlobalPermissionService _service;
            private GlobalWhitelistService _gwl;
            private readonly DbService _db;

            public GlobalUnblockCommands(GlobalPermissionService service, DbService db, GlobalWhitelistService gwl)
            {
                _service = service;
                _db = db;
                _gwl = gwl;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task Lgu(string listName)
            {
                if (listName == "") // TODO: Check if name is in whitelistgroups
                {
                    await ReplyErrorLocalized("lgu_invalidname", "MyList").ConfigureAwait(false);
                    return;
                }
                // TODO: Attempt to find related modules/commands

                if (!_service.UnblockedModules.Any() && !_service.UnblockedCommands.Any())
                {
                    await ReplyErrorLocalized("lgu_none", listName).ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithOkColor().WithTitle(GetText("gwl_title"));

                if (_service.UnblockedModules.Any())
                    embed.AddField(efb => efb.WithName(GetText("unblocked_modules")).WithValue(string.Join("\n", _service.UnblockedModules)).WithIsInline(true));

                if (_service.UnblockedCommands.Any())
                    embed.AddField(efb => efb.WithName(GetText("unblocked_commands")).WithValue(string.Join("\n", _service.UnblockedCommands)).WithIsInline(true));

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task UbMod(AddRemove action, ModuleOrCrInfo module, string listName)
            {
                var moduleName = module.Name.ToLowerInvariant();

                // If the listName doesn't exist, return an error message
                if (!_gwl.GetGroupByName(listName, out GlobalWhitelistSet group))
                {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
                }

                // Process Add module
                if (action == AddRemove.Add) 
                {
                    if (_service.UnblockedModules.Add(moduleName))
                    {
                        System.Console.WriteLine("Adding module to repo");
                    }
                    if(_gwl.AddItemToGroup(moduleName,"module",group))
                    {
                        await ReplyConfirmLocalized("ubm_add", Format.Bold(moduleName), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                        //await ReplyConfirmLocalized("gwl_add", Format.Code("module"), Format.Code(moduleName), Format.Bold(listName)).ConfigureAwait(false);
                    }
                    else {
                        await ReplyErrorLocalized("gwl_add_failed", Format.Code("module"), Format.Code(moduleName), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                }
                // Process Remove module
                else
                {
                    if(_gwl.RemoveItemFromGroup(moduleName,"module",group))
                    {
                        await ReplyConfirmLocalized("ubm_remove", Format.Bold(moduleName), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                        //await ReplyConfirmLocalized("gwl_remove", Format.Code("module"), Format.Code(moduleName), Format.Bold(listName)).ConfigureAwait(false);
                    }
                    else {
                        await ReplyErrorLocalized("gwl_remove_failed", Format.Code("module"), Format.Code(moduleName), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task UbCmd(AddRemove action, CommandOrCrInfo cmd, string listName)
            {
                var commandName = cmd.Name.ToLowerInvariant();

                // If the listName doesn't exist, return an error message
                if (!_gwl.GetGroupByName(listName, out GlobalWhitelistSet group))
                {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
                }

                // Process Add Command
                if (action == AddRemove.Add) 
                {   
                    if (_service.UnblockedCommands.Add(commandName))
                    {
                        System.Console.WriteLine("Adding command to repo");
                    }
                    if(_gwl.AddItemToGroup(commandName,"command",group))
                    {
                        await ReplyConfirmLocalized("ubc_add", Format.Bold(commandName), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                        //await ReplyConfirmLocalized("gwl_add", Format.Code("command"), Format.Code(commandName), Format.Bold(listName)).ConfigureAwait(false);
                    }
                    else {
                        await ReplyErrorLocalized("gwl_add_failed", Format.Code("command"), Format.Code(commandName), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                }
                // Process Remove Command
                else
                {
                    if(_gwl.RemoveItemFromGroup(commandName,"command",group))
                    {
                        await ReplyConfirmLocalized("ubc_remove", Format.Bold(commandName), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                        //await ReplyConfirmLocalized("gwl_remove", Format.Code("command"), Format.Code(commandName), Format.Bold(listName)).ConfigureAwait(false);
                    }
                    else {
                        await ReplyErrorLocalized("gwl_remove_failed", Format.Code("command"), Format.Code(commandName), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task UbModRm(ModuleOrCrInfo module)
            {
                var moduleName = module.Name.ToLowerInvariant();
                if (_service.UnblockedModules.TryRemove(moduleName))
                {
                    using (var uow = _db.UnitOfWork)
                    {
                        /*var bc = uow.BotConfig.GetOrCreate(set => set.Include(x => x.UnblockedModules));
                        bc.UnblockedModules.RemoveWhere(x => x.Name == moduleName);
                        uow.Complete();*/ // this only sets the BotConfigId FK to null

                        // Delete the unblockedcmd record and all relation records
                        uow._context.Set<UnblockedCmdOrMdl>().Remove( 
                            uow._context.Set<UnblockedCmdOrMdl>()
                            .Where( x => x.Name == moduleName ).FirstOrDefault()
                        );
                        uow.Complete();
                    }
                    await ReplyConfirmLocalized("ubm_remove_all", Format.Bold(moduleName)).ConfigureAwait(false);
                    return;
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task UbCmdRm(CommandOrCrInfo cmd)
            {
                var commandName = cmd.Name.ToLowerInvariant();
                if (_service.UnblockedCommands.TryRemove(commandName))
                {
                    using (var uow = _db.UnitOfWork)
                    {
                        /*var bc = uow.BotConfig.GetOrCreate(set => set.Include(x => x.UnblockedCommands));
                        bc.UnblockedCommands.RemoveWhere(x => x.Name == commandName);
                        uow.Complete();*/ // this only sets the BotConfigId FK to null

                        // Delete the unblockedcmd record and all relation records
                        uow._context.Set<UnblockedCmdOrMdl>().Remove( 
                            uow._context.Set<UnblockedCmdOrMdl>()
                            .Where( x => x.Name == commandName ).FirstOrDefault()
                        );
                        uow.Complete();
                        
                    }
                    await ReplyConfirmLocalized("ubc_remove_all", Format.Bold(commandName)).ConfigureAwait(false);
                    return;
                }
            }
        }
    }
}
