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
            public async Task Lgu(int page=1)
            {
				if(--page < 0) return; // ensures page is 0-indexed and non-negative

				// Error if nothing to show
				if (!_service.UnblockedModules.Any() && !_service.UnblockedCommands.Any())
                {
                    await ReplyErrorLocalized("lgu_none", "WhitelistName").ConfigureAwait(false);
                    return;
                }

				// Send list of all unblocked modules/commands and number of lists for each
				bool hasCmds = _gwl.GetUnblockedNames(UnblockedType.Command, page, out string[] cmds, out int cmdCount);
				bool hasMdls = _gwl.GetUnblockedNames(UnblockedType.Module, page, out string[] mdls, out int mdlCount);

				string strCmd = (hasCmds) ? string.Join("\n", cmds) : "*no such commands*";
				string strMdl = (hasMdls) ? string.Join("\n", mdls) : "*no such modules*";

				int lastCmdPage = (cmdCount - 1)/_gwl.numPerPage +1;
				int lastMdlPage = (mdlCount - 1)/_gwl.numPerPage +1;
				int lastPage = (cmdCount > mdlCount) ? lastCmdPage : lastMdlPage;
				page++;
				if (page > lastPage) page = lastPage;
				if (page > 1) {
					if (hasCmds && page >= lastCmdPage) strCmd += GetText("gwl_endlist", lastCmdPage);
					if (hasMdls && page >= lastMdlPage) strMdl += GetText("gwl_endlist", lastMdlPage);
				}

				var embed = new EmbedBuilder()
					.WithOkColor()
					.WithTitle(GetText("gwl_title"))
					.WithDescription(GetText("gwl_lgu_desc"))
					.AddField(GetText("unblocked_commands", cmdCount), strCmd, true)
					.AddField(GetText("unblocked_modules", mdlCount), strMdl, true)
					.WithFooter($"Page {page}/{lastPage}");

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
            }

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListGwlCmd(CommandOrCrInfo command, int page=1)
				=>ListGwl(command.Name.ToLowerInvariant(), UnblockedType.Command, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListGwlMdl(ModuleOrCrInfo module, int page=1)
				=>ListGwl(module.Name.ToLowerInvariant(), UnblockedType.Module, page);

			private async Task ListGwl(string name, UnblockedType type, int page)
			{
				if(--page < 0) return; // ensures page is 0-indexed and non-negative

				if (_gwl.GetGroupNamesFromUbItem(name,type,page, out string[] names, out int count)) {
					int lastPage = (count - 1)/_gwl.numPerPage;
					if (page > lastPage) page = lastPage;
					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("ub_list_gwl", Format.Code(type.ToString()), Format.Bold(name)))
						.AddField(GetText("gwl_titlefield", count), string.Join("\n", names), true)
						.WithFooter($"Page {page+1}/{lastPage+1}");

					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
					return;
				}
				else {
					await ReplyErrorLocalized("ub_list_gwl_failed", Format.Code(type.ToString()), Format.Bold(name)).ConfigureAwait(false);
                    return;
				}
				
			}

			#region ListUnblockedForMember
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListUnblockUser(ulong userID, int page=1)
				=> ListUnblockedForMember(userID, GlobalWhitelistType.User, page);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListUnblockUser(IUser user, int page=1)
				=> ListUnblockedForMember(user.Id, GlobalWhitelistType.User, page);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListUnblockChannel(ulong channelID, int page=1)
				=> ListUnblockedForMember(channelID, GlobalWhitelistType.Channel, page);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListUnblockChannel(ITextChannel channel, int page=1)
				=> ListUnblockedForMember(channel.Id, GlobalWhitelistType.Channel, page);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListUnblockServer(ulong guildID, int page=1)
				=> ListUnblockedForMember(guildID, GlobalWhitelistType.Server, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task ListUnblockServer(IGuild guild, int page=1)
				=> ListUnblockedForMember(guild.Id, GlobalWhitelistType.Server, page);
			
			private async Task ListUnblockedForMember(ulong id, GlobalWhitelistType type, int page)
			{
				if(--page < 0) return; // ensures page is 0-indexed and non-negative

				// Error if nothing to show
				if (!_service.UnblockedModules.Any() && !_service.UnblockedCommands.Any())
                {
                    await ReplyErrorLocalized("lgu_none", "WhitelistName").ConfigureAwait(false);
                    return;
                }

				// Send list of all unblocked modules/commands and number of lists for each
				bool hasCmds = _gwl.GetUnblockedNamesForMember(UnblockedType.Command, id, type, page, out string[] cmds, out int cmdCount);
				bool hasMdls = _gwl.GetUnblockedNamesForMember(UnblockedType.Module, id, type, page, out string[] mdls, out int mdlCount);

				string strCmd = (hasCmds) ? string.Join("\n", cmds) : "*no such commands*";
				string strMdl = (hasMdls) ? string.Join("\n", mdls) : "*no such modules*";

				int lastCmdPage = (cmdCount - 1)/_gwl.numPerPage +1;
				int lastMdlPage = (mdlCount - 1)/_gwl.numPerPage +1;
				int lastPage = (cmdCount > mdlCount) ? lastCmdPage : lastMdlPage;
				page++;
				if (page > lastPage) page = lastPage;
				if (page > 1) {
					if (hasCmds && page >= lastCmdPage) strCmd += GetText("gwl_endlist", lastCmdPage);
					if (hasMdls && page >= lastMdlPage) strMdl += GetText("gwl_endlist", lastMdlPage);
				}

				var embed = new EmbedBuilder()
					.WithOkColor()
					.WithTitle(GetText("gwl_title"))
					.WithDescription(GetText("ub_list_for_member", 
						Format.Code(type.ToString()),
						_gwl.GetNameOrMentionFromId(type, id)))
					.AddField(GetText("unblocked_commands", cmdCount), strCmd, true)
					.AddField(GetText("unblocked_modules", mdlCount), strMdl, true)
					.WithFooter($"Page {page}/{lastPage}");

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
			}

			#endregion ListUnblockedForMember

			#endregion List Info

			#region Check If Unblocked For Member

			#region User
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckCommandUser(CommandOrCrInfo command, IUser user, int page=1)
				=> CheckIfUnblockedForMember(command.Name.ToLowerInvariant(), 
				UnblockedType.Command, 
				user.Id, GlobalWhitelistType.User, page);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckCommandUser(CommandOrCrInfo command, ulong userID, int page=1)
				=> CheckIfUnblockedForMember(command.Name.ToLowerInvariant(), 
				UnblockedType.Command, 
				userID, GlobalWhitelistType.User, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckModuleUser(ModuleOrCrInfo module, IUser user, int page=1)
				=> CheckIfUnblockedForMember(module.Name.ToLowerInvariant(), 
				UnblockedType.Module, 
				user.Id, GlobalWhitelistType.User, page);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckModuleUser(ModuleOrCrInfo module, ulong userID, int page=1)
				=> CheckIfUnblockedForMember(module.Name.ToLowerInvariant(), 
				UnblockedType.Module, 
				userID, GlobalWhitelistType.User, page);
			#endregion User

			#region Channel

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckCommandChannel(CommandOrCrInfo command, ITextChannel channel, int page=1)
				=> CheckIfUnblockedForMember(command.Name.ToLowerInvariant(), 
				UnblockedType.Command, 
				channel.Id, GlobalWhitelistType.Channel, page);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckCommandChannel(CommandOrCrInfo command, ulong channelID, int page=1)
				=> CheckIfUnblockedForMember(command.Name.ToLowerInvariant(), 
				UnblockedType.Command, 
				channelID, GlobalWhitelistType.Channel, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckModuleChannel(ModuleOrCrInfo module, ITextChannel channel, int page=1)
				=> CheckIfUnblockedForMember(module.Name.ToLowerInvariant(), 
				UnblockedType.Module, 
				channel.Id, GlobalWhitelistType.Channel, page);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckModuleChannel(ModuleOrCrInfo module, ulong channelID, int page=1)
				=> CheckIfUnblockedForMember(module.Name.ToLowerInvariant(), 
				UnblockedType.Module, 
				channelID, GlobalWhitelistType.Channel, page);
			#endregion Channel

			#region Server
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckCommandServer(CommandOrCrInfo command, IGuild server, int page=1)
				=> CheckIfUnblockedForMember(command.Name.ToLowerInvariant(), 
				UnblockedType.Command, 
				server.Id, GlobalWhitelistType.Server, page);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckCommandServer(CommandOrCrInfo command, ulong serverID, int page=1)
				=> CheckIfUnblockedForMember(command.Name.ToLowerInvariant(), 
				UnblockedType.Command, 
				serverID, GlobalWhitelistType.Server, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckModuleServer(ModuleOrCrInfo module, IGuild server, int page=1)
				=> CheckIfUnblockedForMember(module.Name.ToLowerInvariant(), 
				UnblockedType.Module, 
				server.Id, GlobalWhitelistType.Server, page);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task CheckModuleServer(ModuleOrCrInfo module, ulong serverID, int page=1)
				=> CheckIfUnblockedForMember(module.Name.ToLowerInvariant(), 
				UnblockedType.Module, 
				serverID, GlobalWhitelistType.Server, page);
			#endregion Server

			private async Task CheckIfUnblockedForMember(string ubName, UnblockedType ubType, ulong memID, GlobalWhitelistType memType, int page=1)
			{
				if(--page < 0) return; // ensures page is 0-indexed and non-negative

				if (_gwl.CheckIfUnblockedFor(ubName, ubType, memID, memType, page, out string[] lists, out int count))
				{
					int lastPage = (count - 1)/_gwl.numPerPage;
					if (page > lastPage) page = lastPage;

					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gwl_check_ub_yes", 
							Format.Code(ubType.ToString()), 
							Format.Bold(ubName),
							Format.Code(memType.ToString()),
							_gwl.GetNameOrMentionFromId(memType, memID)
							))
						.AddField(GetText("gwl_titlefield", count), string.Join("\n", lists), true)
						.WithFooter($"Page {page+1}/{lastPage+1}");

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
                if (!string.IsNullOrWhiteSpace(listName) && _gwl.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
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
								Format.Bold(group.ListName), 
								Format.Bold(string.Join("\n", successList)))
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_add_bulk_failed",
								successList.Count(), itemNames.Count(),
								Format.Code(type.ToString()+"s"), 
								Format.Bold(group.ListName), 
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
								Format.Bold(group.ListName),
								Format.Bold(string.Join("\n", successList)))
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_remove_bulk_failed",
								successList.Count(), itemNames.Count(),
								Format.Code(type.ToString()+"s"),
								Format.Bold(group.ListName),
								Format.Bold(itemList))
								.ConfigureAwait(false);
							return;
						}
					}
                } else {
					await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
				}
			}

			#endregion Bulk Add/Remove

			#region Clear

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ClearGwlUb(string listName="")
			{
				if (!string.IsNullOrWhiteSpace(listName) && _gwl.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group)) 
				{
					if (_gwl.ClearGroupUbItems(group))
					{
						await ReplyConfirmLocalized("gwl_ub_remove_all", Format.Bold(group.ListName)).ConfigureAwait(false);
                    	return;
					}
					else{
						await ReplyErrorLocalized("gwl_ub_remove_all_failed", Format.Bold(group.ListName)).ConfigureAwait(false);
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
