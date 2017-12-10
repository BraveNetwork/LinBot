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
                    await ReplyErrorLocalized("gwl_create_error").ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalized("gwl_created", Format.Bold(listName)).ConfigureAwait(false);
                return;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ListWhitelistNames(int page=1)
            {
                if(--page < 0) return;

                var names = _service.GetAllNames(page);
                var desc = string.Join("\n", names);

                if (names.Length < 0) desc = GetText("gwl_empty");

                var embed = new EmbedBuilder()
                    .WithTitle(GetText("gwl_list", page +1))
                    .WithDescription(desc)
                    .WithOkColor();

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ListWhitelistMembers(string listName)
            {
                if (_service.GetGroupMembers(listName, 
                    out IGrouping<GlobalWhitelistType,GlobalWhitelistItem>[] members))
                {
                    var servers = members
                        .Single(x => x.Key == GlobalWhitelistType.Server)
                        .OrderBy(x => x.DateAdded)
                        .Select(x => x.ItemId)
                        .ToArray();
                    var channels = members
                        .Single(x => x.Key == GlobalWhitelistType.Channel)
                        .OrderBy(x => x.DateAdded)
                        .Select(x => x.ItemId)
                        .ToArray();
                    var users = members
                        .Single(x => x.Key == GlobalWhitelistType.User)
                        .OrderBy(x => x.DateAdded)
                        .Select(x => x.ItemId)
                        .ToArray();
                        
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle(GetText("gwl_members",listName.ToLowerInvariant()))
                        .AddField("Servers", string.Join("\n",servers), true)
                        .AddField("Channels", string.Join("\n",channels), true)
                        .AddField("Users", string.Join("\n",users), true);

                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);

                } else {
                    await ReplyErrorLocalized("gwl_not_exists").ConfigureAwait(false);    
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

                if (names.Length < 0) desc = GetText("gwl_empty_member", id);

                var embed = new EmbedBuilder()
                    .WithTitle(GetText("gwl_listbymember", page +1))
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
                    await ReplyErrorLocalized("gwl_not_exists").ConfigureAwait(false);
                    return;
                }

                // Process Add ID to Whitelist of ListName
                if (action == AddRemove.Add) 
                {
                    if(_service.AddItemToGroup(id,type,listName))
                    {
                        await ReplyConfirmLocalized("gwl_add", Format.Code(type.ToString()), Format.Code(id.ToString())).ConfigureAwait(false);
                    }
                    else {
                        await ReplyErrorLocalized("gwl_not_exists").ConfigureAwait(false);
                        return;
                    }
                }
                // Process Remove ID from Whitelist of ListName
                else
                {
                    if(_service.RemoveItemFromGroup(id,type,listName))
                    {
                        await ReplyConfirmLocalized("gwl_remove", Format.Code(type.ToString()), Format.Code(id.ToString())).ConfigureAwait(false);
                    }
                    else {
                        await ReplyErrorLocalized("gwl_not_exists").ConfigureAwait(false);
                        return;
                    }
                }                   
            }
        }
    }
}
