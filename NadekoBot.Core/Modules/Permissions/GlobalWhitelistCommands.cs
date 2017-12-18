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

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GlobalWhiteList(string listName)
            {
                if (string.IsNullOrWhiteSpace(listName) || listName.Length > 20) return;
                if (!_service.CreateWhitelist(listName))
                {
                    await ReplyErrorLocalized("gwl_create_error", Format.Bold(listName)).ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalized("gwl_created", Format.Bold(listName)).ConfigureAwait(false);
                return;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ListWhitelistNames(int page=1)
            {
                if(--page < 0) return; // ensures page is 0-indexed and non-negative

                var names = _service.GetAllNames(page);
                var desc = System.String.Join("\n", names);

                if (names.Length <= 0) desc = GetText("gwl_empty");

                var embed = new EmbedBuilder()
                    .WithTitle(GetText("gwl_list", page +1))
                    .WithDescription(desc)
                    .WithOkColor();

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ListWhitelistMembers(string listName, int page=1)
            {
                if(--page < 0) return; // ensures page is 0-indexed and non-negative
                if (_service.GetGroupByName(listName, out GlobalWhitelistSet group))
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

                        if (servers.Length > 0) { serverStr = string.Join("\n",servers); }
                        if (channels.Length > 0) { channelStr = "<#"+string.Join(">\n<#",channels)+">"; }
                        if (users != null) { userStr = "<@"+string.Join(">\n<#",users)+">"; }
                            
                        var embed = new EmbedBuilder()
                            .WithOkColor()
                            .WithTitle(GetText("gwl_members", Format.Bold(listName)))
                            .AddField("Servers", serverStr, true)
                            .AddField("Channels", channelStr, true)
                            .AddField("Users", userStr, true);

                        await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    }
                    
                } else {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);    
                    return;
                }
            }

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

                var desc = string.Join("\n", names);

                if (names.Length < 0) desc = GetText("gwl_empty_member", Format.Bold(id.ToString()), id);

                var embed = new EmbedBuilder()
                    .WithTitle(GetText("gwl_listbymember", Format.Bold(id.ToString()), page +1))
                    .WithDescription(desc)
                    .WithOkColor();

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

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
                if (!_service.GetGroupByName(listName, out GlobalWhitelistSet group))
                {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
                }

                // Process Add ID to Whitelist of ListName
                if (action == AddRemove.Add) 
                {
                    if(_service.AddItemToGroup(id,type,group))
                    {
                        await ReplyConfirmLocalized("gwl_add", Format.Code(type.ToString()), Format.Code(id.ToString()), Format.Bold(listName)).ConfigureAwait(false);
                    }
                    else {
                        await ReplyErrorLocalized("gwl_add_failed", Format.Code(type.ToString()), Format.Code(id.ToString()), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                }
                // Process Remove ID from Whitelist of ListName
                else
                {
                    if(_service.RemoveItemFromGroup(id,type,group))
                    {
                        await ReplyConfirmLocalized("gwl_remove", Format.Code(type.ToString()), Format.Code(id.ToString()), Format.Bold(listName)).ConfigureAwait(false);
                    }
                    else {
                        await ReplyErrorLocalized("gwl_remove_failed", Format.Code(type.ToString()), Format.Code(id.ToString()), Format.Bold(listName)).ConfigureAwait(false);
                        return;
                    }
                }                   
            }
        }
    }
}
