using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using System.Threading.Tasks;
using System.Linq;
using NadekoBot.Extensions;
using NadekoBot.Common.Attributes;
using NadekoBot.Common.Collections;
using NadekoBot.Modules.Permissions.Services;
using NadekoBot.Common.TypeReaders;

namespace NadekoBot.Modules.Permissions
{
    public partial class Permissions
    {
        [Group]
        public class GlobalWhitelistCommands : NadekoSubmodule<GlobalWhitelistService>
        {
            public GlobalWhitelistCommands()
            {
            }

			#region Whitelist Utilities

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GlobalWhiteList(string listName="")
            {
                if (string.IsNullOrWhiteSpace(listName) || listName.Length > 20) return;

				// Ensure a similar name doesnt already exist
				bool exists = _service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group);
                if (exists) {
					await ReplyErrorLocalized("gwl_create_dupe", Format.Bold(listName), Format.Bold(group.ListName)).ConfigureAwait(false);
                	return;
				}
				// Create new list
				if (_service.CreateWhitelist(listName))
                {
                    await ReplyConfirmLocalized("gwl_created", Format.Bold(listName)).ConfigureAwait(false);
                	return;
                }
				// Failure
				await ReplyErrorLocalized("gwl_create_error", Format.Bold(listName)).ConfigureAwait(false);
                return;
            }

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task RenameGlobalWhiteList(string listName="", string newName="")
            {
				if (string.IsNullOrWhiteSpace(newName) || newName.Length > 20) return;
				if (string.IsNullOrWhiteSpace(listName) || listName.Length > 20) return;

				string listNameI = listName.ToLowerInvariant();
				string newNameI = newName.ToLowerInvariant();

				// Ensure a similar name doesnt already exist, but do allow if existing group is the one we are renaming
				if (newNameI != listNameI) {
					if (_service.GetGroupByName(newNameI, out GlobalWhitelistSet groupExists)) {
						await ReplyErrorLocalized("gwl_rename_dupe", Format.Bold(listName), Format.Bold(newName), Format.Bold(groupExists.ListName)).ConfigureAwait(false);
						return;
					}
				}

				// Create the new list if oldName is valid
				if (_service.GetGroupByName(listNameI, out GlobalWhitelistSet group)) {
					bool success = _service.RenameWhitelist(listNameI, newName);
					if (success) {
						await ReplyConfirmLocalized("gwl_rename_success", Format.Bold(group.ListName), Format.Bold(newName)).ConfigureAwait(false);
                		return;
					} else {
						await ReplyErrorLocalized("gwl_rename_failed", Format.Bold(group.ListName), Format.Bold(newName)).ConfigureAwait(false);
                    	return;
					}
				} else 
				{
					await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GlobalWhitelistStatus(string listName="", string status="")
			{
				string listNameI = listName.ToLowerInvariant();
				string statusI = status.ToLower();
				if (_service.GetGroupByName(listNameI, out GlobalWhitelistSet group)) {
					if (statusI != "true" && statusI != "false") 
					{
						string statusTxt = (group.IsEnabled) ? GetText("gwl_status_enabled_emoji") : GetText("gwl_status_disabled_emoji");
						await ReplyConfirmLocalized("gwl_status", Format.Bold(group.ListName), Format.Code(statusTxt)).ConfigureAwait(false);
						return;
					} else {
						if (statusI == "true") {
							_service.SetEnabledStatus(listNameI, true);
							await ReplyConfirmLocalized("gwl_status_enabled", Format.Bold(group.ListName), Format.Code(GetText("gwl_status_enabled_emoji"))).ConfigureAwait(false);
							return;
						} else {
							_service.SetEnabledStatus(listNameI, false);
							await ReplyConfirmLocalized("gwl_status_disabled", Format.Bold(group.ListName), Format.Code(GetText("gwl_status_disabled_emoji"))).ConfigureAwait(false);
							return;
						}
					}
				} else {
					await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ClearGwlMembers(string listName="")
			{
				if (_service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group)) 
				{
					if (_service.ClearGroupMembers(group))
					{
						await ReplyConfirmLocalized("gwl_member_remove_all", Format.Bold(group.ListName)).ConfigureAwait(false);
                    	return;
					}
					else{
						await ReplyErrorLocalized("gwl_member_remove_all_failed", Format.Bold(group.ListName)).ConfigureAwait(false);
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
            public async Task GlobalWhiteListDelete(string listName="")
            {
                if (string.IsNullOrWhiteSpace(listName) || listName.Length > 20) return;
                if (!_service.DeleteWhitelist(listName.ToLowerInvariant()))
                {
                    await ReplyErrorLocalized("gwl_delete_error", Format.Bold(listName)).ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalized("gwl_deleted", Format.Bold(listName)).ConfigureAwait(false);
                return;
            }

			#endregion Whitelist Utilities

			#region Whitelist Info

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ListWhitelistNames(int page=1)
            {
                if(--page < 0) return; // ensures page is 0-indexed and non-negative

                if (_service.GetAllNames(page, out string[] names, out int count)) {
					int lastPage = (count - 1)/_service.numPerPage;
					if (page > lastPage) page = lastPage;
                    var embed = new EmbedBuilder()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gwl_list"))
						.AddField(GetText("gwl_titlefield", count), string.Join("\n", names))
						.WithFooter($"Page {page+1}/{lastPage+1}")
						.WithOkColor();
                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);

					// await Context.Channel.SendPaginatedConfirmAsync(
					// 	(DiscordSocketClient)Context.Client, page,
					// 	(curPage) => {
					// 		_service.GetAllNames(curPage, out string[] _names, out int _count);
					// 		return new EmbedBuilder()
					// 			.WithTitle(GetText("gwl_title"))
					// 			.WithDescription(GetText("gwl_list"))
					// 			.AddField(GetText("gwl_titlefield"), string.Join("\n", _names))
					// 			.WithOkColor();
					// 	},
					// 	count, _service.numPerPage);

                    return;
                } 
				else {
					await ReplyErrorLocalized("gwl_empty").ConfigureAwait(false);
                    return;
                }
            }

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GlobalWhitelistInfo(GlobalWhitelistService.FieldType field, string listName="", int page=1)
				=> _GlobalWhitelistInfo(field, listName, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GlobalWhitelistInfo(string listName="", int page=1)
				=> _GlobalWhitelistInfo(GlobalWhitelistService.FieldType.ALL, listName, page);

			private async Task _GlobalWhitelistInfo(GlobalWhitelistService.FieldType field, string listName, int page)
			{
				if(--page < 0) return; // ensures page is 0-indexed and non-negative

                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {					
					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"));
					
					if (field != GlobalWhitelistService.FieldType.ALL) {
						// This alters embed to have the data we need for the desired fieldType
						getFieldInfo(embed, group, field, page);
					}
					else {
						// Get modules/commands
						bool hasCmds = _service.GetGroupUnblockedNames(group, UnblockedType.Command, page, out string[] cmds, out int cmdCount);
						bool hasMdls = _service.GetGroupUnblockedNames(group, UnblockedType.Module, page, out string[] mdls, out int mdlCount);

						string strCmd = (hasCmds) ? string.Join("\n", cmds) : "*no such commands*";
						string strMdl = (hasMdls) ? string.Join("\n", mdls) : "*no such modules*";
						
						// Get member lists
						bool hasServers = _service.GetGroupMembers(group, GlobalWhitelistType.Server, page, out ulong[] servers, out int serverCount);
						bool hasChannels = _service.GetGroupMembers(group, GlobalWhitelistType.Channel, page, out ulong[] channels, out int channelCount);
						bool hasUsers = _service.GetGroupMembers(group, GlobalWhitelistType.User, page, out ulong[] users, out int userCount);

						string serverStr = "*none*";
						string channelStr = "*none*";
						string userStr = "*none*";

						if (hasServers) { serverStr = string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.Server, servers)); }
						if (hasChannels) { channelStr = string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.Channel, channels)); }
						if (hasUsers) { userStr = string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.User, users)); }
						
						string statusText = (group.IsEnabled) ? GetText("gwl_status_enabled_emoji") :  GetText("gwl_status_disabled_emoji");

						// Paginated Embed
						int lastCmdPage = (cmdCount - 1)/_service.numPerPage +1;
						int lastMdlPage = (mdlCount - 1)/_service.numPerPage +1;
						int lastServerPage = (serverCount - 1)/_service.numPerPage +1;
						int lastChannelPage = (channelCount - 1)/_service.numPerPage +1;
						int lastUserPage = (userCount - 1)/_service.numPerPage +1;
						int lastPage = System.Math.Max( lastCmdPage, 
							System.Math.Max( lastMdlPage, 
								System.Math.Max( lastServerPage,
									System.Math.Max( lastChannelPage, lastUserPage ) ) ) );
						page++;
						if (page > lastPage) page = lastPage;
						if (page > 1) {
							if (hasCmds && page >= lastCmdPage) strCmd += GetText("gwl_endlist", lastCmdPage);
							if (hasMdls && page >= lastMdlPage) strMdl += GetText("gwl_endlist", lastMdlPage);
							if (hasServers && page >= lastServerPage) serverStr += GetText("gwl_endlist", lastServerPage);
							if (hasChannels && page >= lastChannelPage) channelStr += GetText("gwl_endlist", lastChannelPage);
							if (hasUsers && page >= lastUserPage) userStr += GetText("gwl_endlist", lastUserPage);
						}

						embed.WithDescription(GetText("gwl_info", Format.Bold(group.ListName)))
							.AddField(GetText("unblocked_commands", cmdCount), strCmd, true)
							.AddField(GetText("unblocked_modules", mdlCount), strMdl, true)
							.AddField("Status", statusText, true)
							.AddField(GetText("gwl_users", userCount), userStr, true)
							.AddField(GetText("gwl_channels", channelCount), channelStr, true)
							.AddField(GetText("gwl_servers", serverCount), serverStr, true)
							.WithFooter($"Page {page}/{lastPage}");
					}

					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    
                } else {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);    
                    return;
                }
			}

			// This alters embed to have the data we need for the desired fieldType
			private void getFieldInfo(EmbedBuilder embed, GlobalWhitelistSet group, GlobalWhitelistService.FieldType field, int page)
			{
				string fieldTitle = "";
				string fieldStr = "";
				int fieldCount = 0;
				string fieldLabel = "";

				switch(field) {
					case GlobalWhitelistService.FieldType.COMMAND:
						bool hasCmds = _service.GetGroupUnblockedNames(group, UnblockedType.Command, page, out string[] cmds, out fieldCount);
						fieldStr = (hasCmds) ? string.Join("\n", cmds) : "*no such commands*";
						fieldTitle = "unblocked_commands";
						fieldLabel = "Commands";
					break;

					case GlobalWhitelistService.FieldType.MODULE:
						bool hasMdls = _service.GetGroupUnblockedNames(group, UnblockedType.Module, page, out string[] mdls, out fieldCount);
						fieldStr = (hasMdls) ? string.Join("\n", mdls) : "*no such commands*";
						fieldTitle = "unblocked_modules";
						fieldLabel = "Modules";
					break;

					case GlobalWhitelistService.FieldType.SERVER:
						bool hasServers = _service.GetGroupMembers(group, GlobalWhitelistType.Server, page, out ulong[] servers, out fieldCount);
						fieldStr = (!hasServers) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.Server, servers));
						fieldTitle = "gwl_servers";
						fieldLabel = "Servers";
					break;

					case GlobalWhitelistService.FieldType.CHANNEL:
						bool hasChannels = _service.GetGroupMembers(group, GlobalWhitelistType.Channel, page, out ulong[] channels, out fieldCount);
						fieldStr = (!hasChannels) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.Channel, channels));
						fieldTitle = "gwl_channels";
						fieldLabel = "Channels";
					break;

					case GlobalWhitelistService.FieldType.USER:
						bool hasUsers = _service.GetGroupMembers(group, GlobalWhitelistType.User, page, out ulong[] users, out fieldCount);
						fieldStr = (!hasUsers) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.User, users));
						fieldTitle = "gwl_users";
						fieldLabel = "Users";
					break;
				}
				
				int lastPage = (fieldCount - 1)/_service.numPerPage;
				if (page > lastPage) page = lastPage;

				// Alter the object stored in memory, pointed to by the provided embed argument
				embed.WithDescription(GetText("gwl_info_field", Format.Code(fieldLabel), Format.Bold(group.ListName)))
					.AddField(GetText(fieldTitle, fieldCount), fieldStr, false)
					.WithFooter($"Page {page+1}/{lastPage+1}");
			}

			#endregion Whitelist Info

			#region List Member's Whitelists

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UserCheckMemberWhitelist(ulong id, int page=1)
                => CheckMemberWhitelist(id,GlobalWhitelistType.User,page);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UserCheckMemberWhitelist(IUser user, int page=1)
                => CheckMemberWhitelist(user.Id,GlobalWhitelistType.User,page);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ChannelCheckMemberWhitelist(ulong id, int page=1)
                => CheckMemberWhitelist(id,GlobalWhitelistType.Channel,page);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ChannelCheckMemberWhitelist(ITextChannel channel, int page=1)
                => CheckMemberWhitelist(channel.Id,GlobalWhitelistType.Channel,page);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ServerCheckMemberWhitelist(ulong id, int page=1)
                => CheckMemberWhitelist(id,GlobalWhitelistType.Server,page);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ServerCheckMemberWhitelist(IGuild guild, int page=1)
                => CheckMemberWhitelist(guild.Id,GlobalWhitelistType.Server,page);


            private async Task CheckMemberWhitelist(ulong id, GlobalWhitelistType type, int page=1)
            {
                if(--page < 0) return;
                
                if (_service.GetNamesByMember(id, type, page, out string[] names, out int count)) {
					int lastPage = (count - 1)/_service.numPerPage;
					if (page > lastPage) page = lastPage;
                    EmbedBuilder embed = new EmbedBuilder()
                      .WithTitle(GetText("gwl_title"))
                      .WithDescription(GetText("gwl_listbymember", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id)))
                      .AddField(GetText("gwl_titlefield", count), string.Join("\n", names))
                      .WithFooter($"Page {page+1}/{lastPage+1}")
                      .WithOkColor();
                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    return;
                } else {
					await ReplyErrorLocalized("gwl_empty_member", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id)).ConfigureAwait(false);
                    return;
                }
            }

			#endregion List Member's Whitelists

			#region IsMemberInWhitelist

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task IsUserInWhitelist(ulong id, string listName="")
                => IsMemberInWhitelist(id,GlobalWhitelistType.User, listName);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task IsUserInWhitelist(IUser user, string listName="")
                => IsMemberInWhitelist(user.Id,GlobalWhitelistType.User, listName);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task IsChannelInWhitelist(ulong id, string listName="")
                => IsMemberInWhitelist(id,GlobalWhitelistType.Channel, listName);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task IsChannelInWhitelist(ITextChannel channel, string listName="")
                => IsMemberInWhitelist(channel.Id,GlobalWhitelistType.Channel, listName);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task IsServerInWhitelist(ulong id, string listName="")
                => IsMemberInWhitelist(id,GlobalWhitelistType.Server, listName);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task IsServerInWhitelist(IGuild guild, string listName="")
                => IsMemberInWhitelist(guild.Id,GlobalWhitelistType.Server, listName);


            private async Task IsMemberInWhitelist(ulong id, GlobalWhitelistType type, string listName)
            {
                // Return error if whitelist doesn't exist
                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
                    // Return result of IsMemberInList()
                    if(!_service.IsMemberInGroup(id,group)) {
                        string helpCmd = GetText("gwl_help_add_"+type.ToString().ToLowerInvariant(), Prefix, group.ListName, id);
                        await ReplyErrorLocalized("gwl_not_member", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id), Format.Bold(group.ListName), helpCmd).ConfigureAwait(false);
                        return;
                    } else {
                        await ReplyConfirmLocalized("gwl_is_member", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id), Format.Bold(group.ListName)).ConfigureAwait(false);
                        return;
                    }
                } else {
					await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);    
                    return;
				}           
            }

			#endregion IsMemberInWhitelist

			#region Bulk Add/Remove
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UserGlobalWhitelistBulk(AddRemove action, string listName="", params ulong[] ids)
                => GlobalWhitelistAddRmBulk(action, GlobalWhitelistType.User, listName, ids);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UserGlobalWhitelistBulk(AddRemove action, string listName="", params IUser[] usrs)
            {
				ulong[] ids = new ulong[usrs.Length];
				for (int i=0; i<usrs.Length; i++) {
					ids[i] = usrs[i].Id;
				}
				return GlobalWhitelistAddRmBulk(action, GlobalWhitelistType.User, listName, ids);
			}

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ChannelGlobalWhitelistBulk(AddRemove action, string listName="", params ulong[] ids)
                => GlobalWhitelistAddRmBulk(action, GlobalWhitelistType.Channel, listName, ids);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ChannelGlobalWhitelistBulk(AddRemove action, string listName="", params ITextChannel[] channels)
            {
				ulong[] ids = new ulong[channels.Length];
				for (int i=0; i<channels.Length; i++) {
					ids[i] = channels[i].Id;
				}
				return GlobalWhitelistAddRmBulk(action, GlobalWhitelistType.Channel, listName, ids);
			}

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ServerGlobalWhitelistBulk(AddRemove action, string listName="", params ulong[] ids)
                => GlobalWhitelistAddRmBulk(action, GlobalWhitelistType.Server, listName, ids);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ServerGlobalWhitelistBulk(AddRemove action, string listName="", params IGuild[] guilds)
			{
				ulong[] ids = new ulong[guilds.Length];
				for (int i=0; i<guilds.Length; i++) {
					ids[i] = guilds[i].Id;
				}
				return GlobalWhitelistAddRmBulk(action, GlobalWhitelistType.Server, listName, ids);
			}
			
			private async Task GlobalWhitelistAddRmBulk(AddRemove action, GlobalWhitelistType type, string listName, params ulong[] ids)
			{
                // If the listName doesn't exist, return an error message
                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
					// Get string list of name/mention from ids
					string idList = string.Join("\n",_service.GetNameOrMentionFromId(type,ids));

					// Process Add ID to Whitelist of ListName
					if (action == AddRemove.Add) 
					{
						if(_service.AddItemToGroupBulk(ids,type,group, out ulong[] successList))
						{
							string strList = string.Join("\n",_service.GetNameOrMentionFromId(type,successList));
							await ReplyConfirmLocalized("gwl_add_bulk",
								successList.Count(), ids.Count(),
								Format.Code(type.ToString()+"s"),
								Format.Bold(group.ListName),
								strList)
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_add_bulk_failed",
								successList.Count(), ids.Count(),
								Format.Code(type.ToString()+"s"), 
								Format.Bold(group.ListName),
								idList)
								.ConfigureAwait(false);
							return;
						}
					}
					// Process Remove ID from Whitelist of ListName
					else
					{
						if(_service.RemoveItemFromGroupBulk(ids,type,group, out ulong[] successList))
						{
							string strList = string.Join("\n",_service.GetNameOrMentionFromId(type,successList));
							await ReplyConfirmLocalized("gwl_remove_bulk", 
								successList.Count(), ids.Count(),
								Format.Code(type.ToString()+"s"),
								Format.Bold(group.ListName),
								strList)
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_remove_bulk_failed", 
								successList.Count(), ids.Count(),
								Format.Code(type.ToString()+"s"),
								Format.Bold(group.ListName),
								idList)
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
        }
    }
}
