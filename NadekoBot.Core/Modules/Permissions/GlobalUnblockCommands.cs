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
            private GlobalWhitelistService _gwl;
            private readonly DbService _db;

            public GlobalUnblockCommands(GlobalPermissionService service, DbService db, GlobalWhitelistService gwl)
            {
                _service = service;
                _db = db;
                _gwl = gwl;
            }

			#region List Info

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task Lgu(string listName="")
            {
				// Error if nothing to show
				if (!_service.UnblockedModules.Any() && !_service.UnblockedCommands.Any())
                {
                    await ReplyErrorLocalized("lgu_none", listName).ConfigureAwait(false);
                    return;
                }

				var embed = new EmbedBuilder().WithOkColor().WithTitle(GetText("gwl_title"));

				// Case when no listname provided
                if (string.IsNullOrWhiteSpace(listName))
                {
					// Send list of all unblocked modules/commands and number of lists for each
					string[] cmds = _gwl.GetUnblockedNames(UnblockedType.Command);
					string[] mdls = _gwl.GetUnblockedNames(UnblockedType.Module);

					string strCmd = (cmds.Length > 0) ? string.Join("\n", cmds) : "*no such commands*";
					string strMdl = (mdls.Length > 0) ? string.Join("\n", mdls) : "*no such modules*";

					embed.AddField(efb => 
						efb.WithName(GetText("unblocked_commands"))
						.WithValue(strCmd)
						.WithIsInline(true));
					embed.AddField(efb => 
						efb.WithName(GetText("unblocked_modules"))
						.WithValue(strMdl)
						.WithIsInline(true));
                }
				// Case when listname is provided
				else if (_gwl.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group)) 
				{
					// If valid whitelist, get its related modules/commands
					string[] cmds = _gwl.GetGroupUnblockedNames(group, UnblockedType.Command);
					string[] mdls = _gwl.GetGroupUnblockedNames(group, UnblockedType.Module);

					string strCmd = (cmds.Length > 0) ? string.Join("\n", cmds) : "*no such commands*";
					string strMdl = (mdls.Length > 0) ? string.Join("\n", mdls) : "*no such modules*";

					embed.AddField(efb => 
						efb.WithName(GetText("unblocked_commands"))
						.WithValue(strCmd)
						.WithIsInline(true));
					embed.AddField(efb => 
						efb.WithName(GetText("unblocked_modules"))
						.WithValue(strMdl)
						.WithIsInline(true));
				} 
				else {
					// Let the user know they might have typed it wrong
					await ReplyErrorLocalized("lgu_invalidname", listName).ConfigureAwait(false);
                    return;
				}

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
            }

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListGwlCmd(CommandOrCrInfo cmd)
				=>ListGwl(cmd.Name.ToLowerInvariant(), UnblockedType.Command);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListGwlMdl(ModuleOrCrInfo module)
				=>ListGwl(module.Name.ToLowerInvariant(), UnblockedType.Module);

			private async Task ListGwl(string name, UnblockedType type)
			{
				string[] listnames = _gwl.GetGroupNamesFromUbItem(name,type);
				if (listnames == null) {
					await ReplyErrorLocalized("ub_list_gwl_failed", Format.Code(type.ToString()), Format.Bold(name)).ConfigureAwait(false);
                    return;
				}
				string lists = (listnames.Length > 0) ? string.Join("\n", listnames) : "*none*";

				var embed = new EmbedBuilder()
					.WithOkColor()
					.WithTitle(GetText("gwl_title"))
					.WithDescription(GetText("ub_list_gwl", Format.Code(type.ToString()), Format.Bold(name)))
					.AddField(GetText("gwl_titlefield"), lists, true);

				await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
			}

			#region ListUnblockedForMember
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListUnblockUser(ulong userID)
				=> ListUnblockedForMember(userID, GlobalWhitelistType.User);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListUnblockUser(IUser user)
				=> ListUnblockedForMember(user.Id, GlobalWhitelistType.User);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListUnblockChannel(ulong channelID)
				=> ListUnblockedForMember(channelID, GlobalWhitelistType.Channel);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListUnblockChannel(ITextChannel channel)
				=> ListUnblockedForMember(channel.Id, GlobalWhitelistType.Channel);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListUnblockServer(ulong guildID)
				=> ListUnblockedForMember(guildID, GlobalWhitelistType.Server);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListUnblockServer(IGuild guild)
				=> ListUnblockedForMember(guild.Id, GlobalWhitelistType.Server);
			
			private async Task ListUnblockedForMember(ulong id, GlobalWhitelistType type)
			{
				var embed = new EmbedBuilder()
					.WithOkColor()
					.WithTitle(GetText("gwl_title"))
					.WithDescription(GetText("ub_list_for_member", 
						Format.Code(type.ToString()),
						_gwl.GetNameOrMentionFromId(type, id)));

				// Send list of all unblocked modules/commands and number of lists for each
				string[] cmds = _gwl.GetUnblockedNamesForMember(UnblockedType.Command, id, type);
				string[] mdls = _gwl.GetUnblockedNamesForMember(UnblockedType.Module, id, type);

				string strCmd = (cmds.Length > 0) ? string.Join("\n", cmds) : "*no such commands*";
				string strMdl = (mdls.Length > 0) ? string.Join("\n", mdls) : "*no such modules*";

				embed.AddField(efb => 
					efb.WithName(GetText("unblocked_commands"))
					.WithValue(strCmd)
					.WithIsInline(true));
				embed.AddField(efb => 
					efb.WithName(GetText("unblocked_modules"))
					.WithValue(strMdl)
					.WithIsInline(true));
				
				await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
			}

			#endregion ListUnblockedForMember

			#endregion List Info

			#region Check If Unblocked For Member

			#region User
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckCommandUser(CommandOrCrInfo command, IUser user)
				=> CheckIfUnblockedForMember(command.Name.ToLowerInvariant(), 
				UnblockedType.Command, 
				user.Id, GlobalWhitelistType.User);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckCommandUser(CommandOrCrInfo command, ulong userID)
				=> CheckIfUnblockedForMember(command.Name.ToLowerInvariant(), 
				UnblockedType.Command, 
				userID, GlobalWhitelistType.User);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckModuleUser(ModuleOrCrInfo module, IUser user)
				=> CheckIfUnblockedForMember(module.Name.ToLowerInvariant(), 
				UnblockedType.Module, 
				user.Id, GlobalWhitelistType.User);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckModuleUser(ModuleOrCrInfo module, ulong userID)
				=> CheckIfUnblockedForMember(module.Name.ToLowerInvariant(), 
				UnblockedType.Module, 
				userID, GlobalWhitelistType.User);
			#endregion User

			#region Channel

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckCommandChannel(CommandOrCrInfo command, ITextChannel channel)
				=> CheckIfUnblockedForMember(command.Name.ToLowerInvariant(), 
				UnblockedType.Command, 
				channel.Id, GlobalWhitelistType.Channel);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckCommandChannel(CommandOrCrInfo command, ulong channelID)
				=> CheckIfUnblockedForMember(command.Name.ToLowerInvariant(), 
				UnblockedType.Command, 
				channelID, GlobalWhitelistType.Channel);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckModuleChannel(ModuleOrCrInfo module, ITextChannel channel)
				=> CheckIfUnblockedForMember(module.Name.ToLowerInvariant(), 
				UnblockedType.Module, 
				channel.Id, GlobalWhitelistType.Channel);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckModuleChannel(ModuleOrCrInfo module, ulong channelID)
				=> CheckIfUnblockedForMember(module.Name.ToLowerInvariant(), 
				UnblockedType.Module, 
				channelID, GlobalWhitelistType.Channel);
			#endregion Channel

			#region Server
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckCommandServer(CommandOrCrInfo command, IGuild server)
				=> CheckIfUnblockedForMember(command.Name.ToLowerInvariant(), 
				UnblockedType.Command, 
				server.Id, GlobalWhitelistType.Server);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckCommandServer(CommandOrCrInfo command, ulong serverID)
				=> CheckIfUnblockedForMember(command.Name.ToLowerInvariant(), 
				UnblockedType.Command, 
				serverID, GlobalWhitelistType.Server);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckModuleServer(ModuleOrCrInfo module, IGuild server)
				=> CheckIfUnblockedForMember(module.Name.ToLowerInvariant(), 
				UnblockedType.Module, 
				server.Id, GlobalWhitelistType.Server);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckModuleServer(ModuleOrCrInfo module, ulong serverID)
				=> CheckIfUnblockedForMember(module.Name.ToLowerInvariant(), 
				UnblockedType.Module, 
				serverID, GlobalWhitelistType.Server);
			#endregion Server

			private async Task CheckIfUnblockedForMember(string ubName, UnblockedType ubType, ulong memID, GlobalWhitelistType memType)
			{
				if (_gwl.CheckIfUnblockedFor(ubName, ubType, memID, memType, out string[] lists))
				{
					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gwl_check_ub_yes", 
							Format.Code(ubType.ToString()), 
							Format.Bold(ubName),
							Format.Code(memType.ToString()),
							_gwl.GetNameOrMentionFromId(memType, memID),
							lists.Count(),
							string.Join("\n",lists)
							));

					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                	return;

				} else
				{
					await ReplyErrorLocalized("gwl_check_ub_no", 
						Format.Code(ubType.ToString()), 
						Format.Bold(ubName),
						Format.Code(memType.ToString()),
						_gwl.GetNameOrMentionFromId(memType, memID)
						)
					.ConfigureAwait(false);
                	return;
				}
			}

			#endregion Check If Unblocked For Member

			#region Add/Remove UnblockedCmdOrMdl

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UbMod(AddRemove action, ModuleOrCrInfo module, string listName="")
				=> UnblockAddRemove(action, UnblockedType.Module, module.Name.ToLowerInvariant(), listName);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UbCmd(AddRemove action, CommandOrCrInfo cmd, string listName="")
				=> UnblockAddRemove(action, UnblockedType.Command, cmd.Name.ToLowerInvariant(), listName);

			private async Task UnblockAddRemove(AddRemove action, UnblockedType type, string itemName, string listName)
			{
				// If the listName doesn't exist, return an error message
                if (!_gwl.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
                }

                // Process Add Command/Module
                if (action == AddRemove.Add) 
                {   
					// Add to hashset in GlobalPermissionService
					if (type == UnblockedType.Command)
                    {
                        if (_service.UnblockedCommands.Add(itemName)) System.Console.WriteLine("Adding command to GlobalPermissionService.UnblockedCommands");
                    }
					else {
						if (_service.UnblockedModules.Add(itemName)) System.Console.WriteLine("Adding module to GlobalPermissionService.UnblockedModules");
					}

					// Add to a whitelist
                    if(_gwl.AddUbItemToGroup(itemName,type,group))
                    {
                        await ReplyConfirmLocalized("gwl_add", Format.Code(type.ToString()), Format.Bold(itemName), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                    else {
                        await ReplyErrorLocalized("gwl_add_failed", Format.Code(type.ToString()), Format.Bold(itemName), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                }
                // Process Remove Command/Module
                else
                {
					// Remove from whitelist
                    if(_gwl.RemoveUbItemFromGroup(itemName,type,group))
                    {
                        await ReplyConfirmLocalized("gwl_remove", Format.Code(type.ToString()), Format.Bold(itemName), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                    else {
                        await ReplyErrorLocalized("gwl_remove_failed", Format.Code(type.ToString()), Format.Bold(itemName), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                }
			}

			#endregion Add/Remove UnblockedCmdOrMdl

			#region Bulk Add/Remove

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UbModBulk(AddRemove action, string listName="", params ModuleOrCrInfo[] mdls)
			{
				string[] names = new string[mdls.Length];
				for (int i=0; i<mdls.Length; i++) {
					names[i] = mdls[i].Name.ToLowerInvariant();
				}
				return UnblockAddRemoveBulk(action, UnblockedType.Module, listName, names);
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UbCmdBulk(AddRemove action, string listName="", params CommandOrCrInfo[] cmds)
			{
				string[] names = new string[cmds.Length];
				for (int i=0; i<cmds.Length; i++) {
					names[i] = cmds[i].Name.ToLowerInvariant();
				}
				return UnblockAddRemoveBulk(action, UnblockedType.Command, listName, names);
			}

			private async Task UnblockAddRemoveBulk(AddRemove action, UnblockedType type, string listName, params string[] itemNames)
			{
				// If the listName doesn't exist, return an error message
                if (!_gwl.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
                }

				// Get itemlist string
				string itemList = string.Join("\n",itemNames);

                // Process Add Command/Module
                if (action == AddRemove.Add) 
                {   
					// Keep track of internal changes
					int delta = 0;

					// Add to hashset in GlobalPermissionService
					if (type == UnblockedType.Command)
                    {
						int pre = _service.UnblockedCommands.Count;
						_service.UnblockedCommands.AddRange(itemNames);
						delta = _service.UnblockedCommands.Count - pre;
                    }
					else {
						int pre = _service.UnblockedModules.Count;
						_service.UnblockedModules.AddRange(itemNames);
						delta = _service.UnblockedModules.Count - pre;
					}

					System.Console.WriteLine("Added {0} items to GlobalPermissionService Unblocked HashSet", delta);

					// Add to a whitelist
                    if(_gwl.AddUbItemToGroupBulk(itemNames,type,group, out string[] successList))
                    {
                        await ReplyConfirmLocalized("gwl_add_bulk",
							successList.Count(), itemNames.Count(),
							Format.Code(type.ToString()+"s"), 
							Format.Bold(listName), 
							Format.Bold(string.Join("\n", successList)))
							.ConfigureAwait(false);
						return;
                    }
                    else {
                        await ReplyErrorLocalized("gwl_add_bulk_failed",
							successList.Count(), itemNames.Count(),
							Format.Code(type.ToString()+"s"), 
							Format.Bold(listName), 
							Format.Bold(itemList))
							.ConfigureAwait(false);
                        return;
                    }
                }
                // Process Remove Command/Module
                else
                {
					// Remove from whitelist
                    if(_gwl.RemoveUbItemFromGroupBulk(itemNames,type,group, out string[] successList))
                    {
                        await ReplyConfirmLocalized("gwl_remove_bulk",
							successList.Count(), itemNames.Count(),
							Format.Code(type.ToString()+"s"),
							Format.Bold(listName),
							Format.Bold(string.Join("\n", successList)))
							.ConfigureAwait(false);
                        return;
                    }
                    else {
                        await ReplyErrorLocalized("gwl_remove_bulk_failed",
							successList.Count(), itemNames.Count(),
							Format.Code(type.ToString()+"s"),
							Format.Bold(listName),
							Format.Bold(itemList))
							.ConfigureAwait(false);
                        return;
                    }
                }
			}

			#endregion Bulk Add/Remove

			#region Clear

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ClearGwlUb(string listName="")
			{
				if (_gwl.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group)) 
				{
					if (_gwl.ClearGroupUbItems(group))
					{
						await ReplyConfirmLocalized("gwl_ub_remove_all", Format.Bold(listName)).ConfigureAwait(false);
                    	return;
					}
					else{
						await ReplyErrorLocalized("gwl_ub_remove_all_failed", Format.Bold(listName)).ConfigureAwait(false);
                    	return;
					}
				}
				else
				{
					// Let the user know they might have typed it wrong
					await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
				}
			}

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UbModRm(ModuleOrCrInfo module)
				=> BlockForAll(UnblockedType.Module, module.Name.ToLowerInvariant());

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UbCmdRm(CommandOrCrInfo cmd)
				=> BlockForAll(UnblockedType.Command, cmd.Name.ToLowerInvariant());

			private async Task BlockForAll(UnblockedType type, string itemName)
			{
				// Try to remove from GlobalPermissionService
				bool removedFromHashset;
				if (type == UnblockedType.Command)
				{
					removedFromHashset = _service.UnblockedCommands.TryRemove(itemName);
				}
				else
				{
					removedFromHashset = _service.UnblockedModules.TryRemove(itemName);
				}

				// Try to remove from all whitelists
                if (removedFromHashset)
                {
                    using (var uow = _db.UnitOfWork)
                    {
                        /*var bc = uow.BotConfig.GetOrCreate(set => set.Include(x => x.UnblockedCommands));
                        bc.UnblockedCommands.RemoveWhere(x => x.Name == itemName);
                        uow.Complete();*/ // this only sets the BotConfigId FK to null

                        // Delete the unblockedcmd record and all relation records
                        uow._context.Set<UnblockedCmdOrMdl>().Remove( 
                            uow._context.Set<UnblockedCmdOrMdl>()
                            .Where( x => x.Name.Equals(itemName) )
							.Where( x => x.Type.Equals(type) )
							.FirstOrDefault()
                        );
                        uow.Complete();
                    }
                    await ReplyConfirmLocalized("ub_remove_all", Format.Code(type.ToString()), Format.Bold(itemName)).ConfigureAwait(false);
                    return;
                }
				else {
					await ReplyErrorLocalized("ub_remove_all_failed", Format.Code(type.ToString()), Format.Bold(itemName)).ConfigureAwait(false);
                    return;
				}
			}

			#endregion Clear
        }
    }
}
