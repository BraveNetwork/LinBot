using Discord;
using Discord.Commands;
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
            private readonly IBotCredentials _creds;
            public GlobalWhitelistCommands(IBotCredentials creds)
            {
                _creds = creds;
            }

			#region Whitelist Utilities

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GlobalWhiteList(string listName)
            {
                if (string.IsNullOrWhiteSpace(listName) || listName.Length > 20) return;
                if (!_service.CreateWhitelist(listName.ToLowerInvariant()))
                {
                    await ReplyErrorLocalized("gwl_create_error", Format.Bold(listName)).ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalized("gwl_created", Format.Bold(listName)).ConfigureAwait(false);
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

				if (_service.GetGroupByName(listNameI, out GlobalWhitelistSet group)) {
					bool success = _service.RenameWhitelist(listNameI, newNameI);
					if (success) {
						await ReplyConfirmLocalized("gwl_renamed", Format.Bold(listName), Format.Bold(newName)).ConfigureAwait(false);
                		return;
					} else {
						await ReplyErrorLocalized("gwl_renamed_failed", Format.Bold(listName), Format.Bold(newName)).ConfigureAwait(false);
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
						await ReplyConfirmLocalized("gwl_status", Format.Bold(listName), Format.Code(statusTxt)).ConfigureAwait(false);
						return;
					} else {
						if (statusI == "true") {
							_service.SetEnabledStatus(listNameI, true);
							await ReplyConfirmLocalized("gwl_status_enabled", Format.Bold(listName), Format.Code(GetText("gwl_status_enabled_emoji"))).ConfigureAwait(false);
							return;
						} else {
							_service.SetEnabledStatus(listNameI, false);
							await ReplyConfirmLocalized("gwl_status_disabled", Format.Bold(listName), Format.Code(GetText("gwl_status_disabled_emoji"))).ConfigureAwait(false);
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
						await ReplyConfirmLocalized("gwl_member_remove_all", Format.Bold(listName)).ConfigureAwait(false);
                    	return;
					}
					else{
						await ReplyErrorLocalized("gwl_member_remove_all_failed", Format.Bold(listName)).ConfigureAwait(false);
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

                var names = _service.GetAllNames(page);

                if (names.Length <= 0) {
                    await ReplyErrorLocalized("gwl_empty").ConfigureAwait(false);
                    return;
                } else {
                    var embed = new EmbedBuilder()
                      .WithTitle(GetText("gwl_title"))
                      .WithDescription(GetText("gwl_list"))
                      .AddField(GetText("gwl_titlefield"), string.Join("\n", names))
                      .WithFooter("Page " + (page+1))
                      .WithOkColor();
                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    return;
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ListWhitelistMembers(string listName="", int page=1)
            {
                if(--page < 0) return; // ensures page is 0-indexed and non-negative
                if (_service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
                    if (group.GlobalWhitelistItemSets.Count() < 1) {
                        await ReplyErrorLocalized("gwl_no_members", Format.Bold(listName), listName).ConfigureAwait(false);    
                        return;
                    } else {
                        ulong[] servers = _service.GetGroupMembers(group, GlobalWhitelistType.Server, page);
                        ulong[] channels = _service.GetGroupMembers(group, GlobalWhitelistType.Channel, page);
                        ulong[] users = _service.GetGroupMembers(group, GlobalWhitelistType.User, page);

                        string serverStr = "*none*";
                        string channelStr = "*none*";
                        string userStr = "*none*";

                        if (servers.Length > 0) { serverStr = string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.Server, servers)); }
                        if (channels.Length > 0) { channelStr = string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.Channel, channels)); }
                        if (users.Length > 0) { userStr = string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.User, users)); }
                            
                        var embed = new EmbedBuilder()
                            .WithOkColor()
                            .WithTitle(GetText("gwl_title"))
                            .WithDescription(GetText("gwl_members", Format.Bold(listName)))
                            .AddField("Servers", serverStr, true)
                            .AddField("Channels", channelStr, true)
                            .AddField("Users", userStr, true)
                            .WithFooter("Page " + (page+1));

                        await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    }
                    
                } else {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);    
                    return;
                }
            }

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GlobalWhitelistInfo(string listName=null, int page=1)
            {
                if(--page < 0) return; // ensures page is 0-indexed and non-negative
                if (_service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
					// If valid whitelist, get its related modules/commands
					string[] cmds = _service.GetGroupUnblockedNames(group, UnblockedType.Command);
					string[] mdls = _service.GetGroupUnblockedNames(group, UnblockedType.Module);

					string strCmd = (cmds.Length > 0) ? string.Join("\n", cmds) : "*no such commands*";
					string strMdl = (mdls.Length > 0) ? string.Join("\n", mdls) : "*no such modules*";
					
					// Get member lists
					ulong[] servers = _service.GetGroupMembers(group, GlobalWhitelistType.Server, page);
					ulong[] channels = _service.GetGroupMembers(group, GlobalWhitelistType.Channel, page);
					ulong[] users = _service.GetGroupMembers(group, GlobalWhitelistType.User, page);

					string serverStr = "*none*";
					string channelStr = "*none*";
					string userStr = "*none*";

					if (servers.Length > 0) { serverStr = string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.Server, servers)); }
					if (channels.Length > 0) { channelStr = string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.Channel, channels)); }
					if (users.Length > 0) { userStr = string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.User, users)); }
						
					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gwl_info", Format.Bold(listName)))
						.AddField(GetText("unblocked_commands") + "("+cmds.Length+")", strCmd, true)
						.AddField(GetText("unblocked_modules") + "("+mdls.Length+")", strMdl, true)
						.AddField("Servers " + "("+servers.Length+")", serverStr, true)
						.AddField("Channels " + "("+channels.Length+")", channelStr, true)
						.AddField("Users " + "("+users.Length+")", userStr, true)
						.WithFooter("Page " + (page+1));

					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    
                } else {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);    
                    return;
                }
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
                if(_creds.OwnerIds.Contains(id) || --page < 0) return;

                var names = _service.GetNamesByMember(id, type, page);
                
                if (names.Length <= 0) {
                    await ReplyErrorLocalized("gwl_empty_member", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id)).ConfigureAwait(false);
                    return;
                } else {
                    EmbedBuilder embed = new EmbedBuilder()
                      .WithTitle(GetText("gwl_title"))
                      .WithDescription(GetText("gwl_listbymember", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id)))
                      .AddField(GetText("gwl_titlefield"), string.Join("\n", names))
                      .WithFooter("Page " + (page+1))
                      .WithOkColor();
                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    return;
                }
            }

			#endregion List Member's Whitelists

			#region IsMemberInWhitelist

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task IsUserInWhitelist(ulong id, string listName)
                => IsMemberInWhitelist(id,GlobalWhitelistType.User, listName);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task IsUserInWhitelist(IUser user, string listName)
                => IsMemberInWhitelist(user.Id,GlobalWhitelistType.User, listName);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task IsChannelInWhitelist(ulong id, string listName)
                => IsMemberInWhitelist(id,GlobalWhitelistType.Channel, listName);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task IsChannelInWhitelist(ITextChannel channel, string listName)
                => IsMemberInWhitelist(channel.Id,GlobalWhitelistType.Channel, listName);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task IsServerInWhitelist(ulong id, string listName)
                => IsMemberInWhitelist(id,GlobalWhitelistType.Server, listName);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task IsServerInWhitelist(IGuild guild, string listName)
                => IsMemberInWhitelist(guild.Id,GlobalWhitelistType.Server, listName);


            private async Task IsMemberInWhitelist(ulong id, GlobalWhitelistType type, string listName)
            {
                // Return error if whitelist doesn't exist
                if (!_service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);    
                    return;
                } else {
                    // Return result of IsMemberInList()
                    if(_creds.OwnerIds.Contains(id) || !_service.IsMemberInGroup(id,group)) {
                        string helpCmd = GetText("gwl_help_add_"+type.ToString().ToLowerInvariant(), Prefix, listName, id);
                        await ReplyErrorLocalized("gwl_not_member", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id), Format.Bold(listName), helpCmd).ConfigureAwait(false);
                        return;
                    } else {
                        await ReplyConfirmLocalized("gwl_is_member", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                }                
            }

			#endregion IsMemberInWhitelist

			#region Add/Remove Member

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UserGlobalWhitelist(AddRemove action, string listName, ulong id)
                => GlobalWhitelistAddRm(action, id, listName, GlobalWhitelistType.User);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UserGlobalWhitelist(AddRemove action, string listName, IUser usr)
                => GlobalWhitelistAddRm(action, usr.Id, listName, GlobalWhitelistType.User);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ChannelGlobalWhitelist(AddRemove action, string listName, ulong id)
                => GlobalWhitelistAddRm(action, id, listName, GlobalWhitelistType.Channel);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ChannelGlobalWhitelist(AddRemove action, string listName, ITextChannel channel)
                => GlobalWhitelistAddRm(action, channel.Id, listName, GlobalWhitelistType.Channel);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ServerGlobalWhitelist(AddRemove action, string listName, ulong id)
                => GlobalWhitelistAddRm(action, id, listName, GlobalWhitelistType.Server);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ServerGlobalWhitelist(AddRemove action, string listName, IGuild guild)
                => GlobalWhitelistAddRm(action, guild.Id, listName, GlobalWhitelistType.Server);

            private async Task GlobalWhitelistAddRm(AddRemove action, ulong id, string listName, GlobalWhitelistType type)
            {
                if(action == AddRemove.Add && _creds.OwnerIds.Contains(id))
                    return;

                // If the listName doesn't exist, return an error message
                if (!_service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
                }

                // Process Add ID to Whitelist of ListName
                if (action == AddRemove.Add) 
                {
                    if(_service.AddItemToGroup(id,type,group))
                    {
                        await ReplyConfirmLocalized("gwl_add", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id), Format.Bold(listName)).ConfigureAwait(false);
                    }
                    else {
                        await ReplyErrorLocalized("gwl_add_failed", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                }
                // Process Remove ID from Whitelist of ListName
                else
                {
                    if(_service.RemoveItemFromGroup(id,type,group))
                    {
                        await ReplyConfirmLocalized("gwl_remove", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id), Format.Bold(listName)).ConfigureAwait(false);
                    }
                    else {
                        await ReplyErrorLocalized("gwl_remove_failed", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                }                   
            }

			#endregion Add/Remove

			#region Bulk Add/Remove
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UserGlobalWhitelistBulk(AddRemove action, string listName, params ulong[] ids)
                => GlobalWhitelistAddRmBulk(action, GlobalWhitelistType.User, listName, ids);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task UserGlobalWhitelistBulk(AddRemove action, string listName, params IUser[] usrs)
            {
				ulong[] ids = new ulong[usrs.Length];
				for (int i=0; i<usrs.Length; i++) {
					ids[i] = usrs[i].Id;
				}
				return GlobalWhitelistAddRmBulk(action, GlobalWhitelistType.User, listName, ids);
			}

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ChannelGlobalWhitelistBulk(AddRemove action, string listName, params ulong[] ids)
                => GlobalWhitelistAddRmBulk(action, GlobalWhitelistType.Channel, listName, ids);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ChannelGlobalWhitelistBulk(AddRemove action, string listName, params ITextChannel[] channels)
            {
				ulong[] ids = new ulong[channels.Length];
				for (int i=0; i<channels.Length; i++) {
					ids[i] = channels[i].Id;
				}
				return GlobalWhitelistAddRmBulk(action, GlobalWhitelistType.Channel, listName, ids);
			}

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ServerGlobalWhitelistBulk(AddRemove action, string listName, params ulong[] ids)
                => GlobalWhitelistAddRmBulk(action, GlobalWhitelistType.Server, listName, ids);

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task ServerGlobalWhitelistBulk(AddRemove action, string listName, params IGuild[] guilds)
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
                if (!_service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
                }

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
							Format.Bold(listName),
							strList)
							.ConfigureAwait(false);
						return;
                    }
                    else {
                        await ReplyErrorLocalized("gwl_add_bulk_failed",
							successList.Count(), ids.Count(),
							Format.Code(type.ToString()+"s"), 
							Format.Bold(listName),
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
							Format.Bold(listName),
							strList)
							.ConfigureAwait(false);
						return;
                    }
                    else {
                        await ReplyErrorLocalized("gwl_remove_bulk_failed", 
							successList.Count(), ids.Count(),
							Format.Code(type.ToString()+"s"),
							Format.Bold(listName),
							idList)
							.ConfigureAwait(false);
                        return;
                    }
                }
			}

			#endregion Bulk Add/Remove
        }
    }
}
