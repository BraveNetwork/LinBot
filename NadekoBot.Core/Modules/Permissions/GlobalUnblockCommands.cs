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

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ListGUB(int page=1)
            {
				if(--page < 0) return; // ensures page is 0-indexed and non-negative

				// Error if nothing to show
				if (!_service.UnblockedModules.Any() && !_service.UnblockedCommands.Any())
                {
                    await ReplyErrorLocalized("gub_none", "WhitelistName").ConfigureAwait(false);
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
					.WithDescription(GetText("gub_list"))
					.AddField(GetText("gwl_field_commands", cmdCount), strCmd, true)
					.AddField(GetText("gwl_field_modules", mdlCount), strMdl, true)
					.WithFooter($"Page {page}/{lastPage}");

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
            }

			#region GUBFor
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GUBForMember(GlobalWhitelistService.FieldType field, ulong id, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _GUBForMember(GlobalWhitelistType.Server, id, page);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _GUBForMember(GlobalWhitelistType.Channel, id, page);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _GUBForMember(GlobalWhitelistType.User, id, page);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_member", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			#region GUBFor Member

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBForMember(IGuild server, int page=1)
				=> _GUBForMember(GlobalWhitelistType.Server, server.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBForMember(ITextChannel channel, int page=1)
				=> _GUBForMember(GlobalWhitelistType.Channel, channel.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBForMember(IUser user, int page=1)
				=> _GUBForMember(GlobalWhitelistType.User, user.Id, page);

			#endregion GUBFor Member

			private async Task _GUBForMember(GlobalWhitelistType type, ulong id, int page)
			{
				if(--page < 0) return; // ensures page is 0-indexed and non-negative

				// Error if nothing to show
				if (!_service.UnblockedModules.Any() && !_service.UnblockedCommands.Any())
                {
                    await ReplyErrorLocalized("gub_none", "WhitelistName").ConfigureAwait(false);
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
					.WithDescription(GetText("gub_list_formember", 
						Format.Code(type.ToString()),
						_gwl.GetNameOrMentionFromId(type, id)))
					.AddField(GetText("gwl_field_commands", cmdCount), strCmd, true)
					.AddField(GetText("gwl_field_modules", mdlCount), strMdl, true)
					.WithFooter($"Page {page}/{lastPage}");

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
			}

			#endregion GUBFor

			#region GUBCheck

			#region GUBCheck FieldType

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GUBCheck(GlobalWhitelistService.FieldType field, ulong id, CommandOrCrInfo cmd, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _GUBCheck(GlobalWhitelistType.Server, id, UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _GUBCheck(GlobalWhitelistType.Channel, id, UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _GUBCheck(GlobalWhitelistType.User, id, UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_member", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GUBCheck(GlobalWhitelistService.FieldType field, ulong id, ModuleOrCrInfo mdl, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _GUBCheck(GlobalWhitelistType.Server, id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _GUBCheck(GlobalWhitelistType.Channel, id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _GUBCheck(GlobalWhitelistType.User, id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_member", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			#endregion GUBCheck FieldType
			
			#region GUBCheck Command

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(IGuild server, CommandOrCrInfo cmd, int page=1)
				=> _GUBCheck(GlobalWhitelistType.Server, server.Id, UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(ITextChannel channel, CommandOrCrInfo cmd, int page=1)
				=> _GUBCheck(GlobalWhitelistType.Channel, channel.Id, UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(IUser user, CommandOrCrInfo cmd, int page=1)
				=> _GUBCheck(GlobalWhitelistType.User, user.Id, UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);

			#endregion GUBCheck Command

			#region GUBCheck Module

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(IGuild server, ModuleOrCrInfo mdl, int page=1)
				=> _GUBCheck(GlobalWhitelistType.Server, server.Id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(ITextChannel channel, ModuleOrCrInfo mdl, int page=1)
				=> _GUBCheck(GlobalWhitelistType.Channel, channel.Id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(IUser user, ModuleOrCrInfo mdl, int page=1)
				=> _GUBCheck(GlobalWhitelistType.User, user.Id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			#endregion GUBCheck Module

			private async Task _GUBCheck(GlobalWhitelistType memType, ulong memID, UnblockedType ubType, string ubName, int page=1)
			{
				if(--page < 0) return; // ensures page is 0-indexed and non-negative

				if (_gwl.CheckIfUnblockedFor(ubName, ubType, memID, memType, page, out string[] lists, out int count))
				{
					int lastPage = (count - 1)/_gwl.numPerPage;
					if (page > lastPage) page = lastPage;

					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gub_is_unblocked", 
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
					await ReplyErrorLocalized("gub_not_unblocked", 
						Format.Code(ubType.ToString()), 
						Format.Bold(ubName),
						Format.Code(memType.ToString()),
						_gwl.GetNameOrMentionFromId(memType, memID)
						)
					.ConfigureAwait(false);
                	return;
				}
			}

			#endregion GUBCheck

			
        }
    }
}
