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
                    await ReplyErrorLocalized("gub_none").ConfigureAwait(false);
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
						await _GUBForMember(GWLItemType.Server, id, page);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _GUBForMember(GWLItemType.Channel, id, page);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _GUBForMember(GWLItemType.User, id, page);
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
				=> _GUBForMember(GWLItemType.Server, server.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBForMember(ITextChannel channel, int page=1)
				=> _GUBForMember(GWLItemType.Channel, channel.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBForMember(IUser user, int page=1)
				=> _GUBForMember(GWLItemType.User, user.Id, page);

			#endregion GUBFor Member

			private async Task _GUBForMember(GWLItemType type, ulong id, int page)
			{
				if(--page < 0) return; // ensures page is 0-indexed and non-negative

				// Error if nothing to show
				if (!_service.UnblockedModules.Any() && !_service.UnblockedCommands.Any())
                {
                    await ReplyErrorLocalized("gub_none").ConfigureAwait(false);
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
            public async Task GUBCheck(GlobalWhitelistService.FieldType field, ulong id, CommandInfo cmd, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _GUBCheck(GWLItemType.Server, id, UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _GUBCheck(GWLItemType.Channel, id, UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _GUBCheck(GWLItemType.User, id, UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_member", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GUBCheck(GlobalWhitelistService.FieldType field, ulong id, ModuleInfo mdl, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _GUBCheck(GWLItemType.Server, id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _GUBCheck(GWLItemType.Channel, id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _GUBCheck(GWLItemType.User, id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);
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
            public Task GUBCheck(IGuild server, CommandInfo cmd, int page=1)
				=> _GUBCheck(GWLItemType.Server, server.Id, UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(ITextChannel channel, CommandInfo cmd, int page=1)
				=> _GUBCheck(GWLItemType.Channel, channel.Id, UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(IUser user, CommandInfo cmd, int page=1)
				=> _GUBCheck(GWLItemType.User, user.Id, UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);

			#endregion GUBCheck Command

			#region GUBCheck Module

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(IGuild server, ModuleInfo mdl, int page=1)
				=> _GUBCheck(GWLItemType.Server, server.Id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(ITextChannel channel, ModuleInfo mdl, int page=1)
				=> _GUBCheck(GWLItemType.Channel, channel.Id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(IUser user, ModuleInfo mdl, int page=1)
				=> _GUBCheck(GWLItemType.User, user.Id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			#endregion GUBCheck Module

			private async Task _GUBCheck(GWLItemType memType, ulong memID, UnblockedType ubType, string ubName, int page=1)
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
						.AddField(GetText("gwl_field_title", count), string.Join("\n", lists), true)
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

			#region User-Oriented

			[NadekoCommand, Usage, Description, Aliases]
            public Task ListMyGUB(int page=1)
            	=> _GUBForMember(GWLItemType.User, Context.User.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            public async Task ListContextGUB(int page=1)
            {
				if(--page < 0) return; // ensures page is 0-indexed and non-negative

				// Error if nothing to show
				if (!_service.UnblockedModules.Any() && !_service.UnblockedCommands.Any())
                {
                    await ReplyErrorLocalized("gub_none").ConfigureAwait(false);
                    return;
                }

				ulong idC = Context.Channel.Id;
				ulong idS = Context.Guild.Id;

				GWLItemType typeC = GWLItemType.Channel;
				GWLItemType typeS = GWLItemType.Server;

				// Send list of all unblocked modules/commands and number of lists for each
				bool hasCmdsC = _gwl.GetUnblockedNamesForMember(UnblockedType.Command, idC, typeC, page, out string[] cmdsC, out int cmdCountC);
				bool hasMdlsC = _gwl.GetUnblockedNamesForMember(UnblockedType.Module, idC, typeC, page, out string[] mdlsC, out int mdlCountC);

				bool hasCmdsS = _gwl.GetUnblockedNamesForMember(UnblockedType.Command, idS, typeS, page, out string[] cmdsS, out int cmdCountS);
				bool hasMdlsS = _gwl.GetUnblockedNamesForMember(UnblockedType.Module, idS, typeS, page, out string[] mdlsS, out int mdlCountS);

				bool hasCmds = hasCmdsC || hasCmdsS;
				bool hasMdls = hasMdlsC || hasMdlsS;

				// Combine the lists and remove dupes
				string[] cmds = (hasCmdsC && hasCmdsS) ? cmdsC.Union(cmdsS).ToArray() :
					(hasCmdsC) ? cmdsC : 
					(hasCmdsS) ? cmdsS : new string[] {};
				string[] mdls = (hasMdlsC && hasMdlsS) ? cmdsC.Union(mdlsS).ToArray() :
					(hasMdlsC) ? mdlsC : 
					(hasMdlsS) ? mdlsS : new string[] {};

				int cmdCount = cmds.Length;
				int mdlCount = mdls.Length;

				string strCmd = (cmdCount>0) ? string.Join("\n", cmds) : "*no such commands*";
				string strMdl = (mdlCount>0) ? string.Join("\n", mdls) : "*no such modules*";

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
					.WithDescription(GetText("gub_list_formember_ctx", 
						Format.Code(typeS.ToString()),
						Context.Guild.Name,
						Format.Code(typeC.ToString()),
						MentionUtils.MentionChannel(idC)))
					.AddField(GetText("gwl_field_commands", cmdCount), strCmd, true)
					.AddField(GetText("gwl_field_modules", mdlCount), strMdl, true)
					.WithFooter($"Page {page}/{lastPage}");

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
			}

			[NadekoCommand, Usage, Description, Aliases]
            public Task IsMyGUB(CommandInfo command, int page=1)
				=> _GUBCheck(GWLItemType.User, Context.User.Id, UnblockedType.Command, command.Name.ToLowerInvariant(), page);
			
			[NadekoCommand, Usage, Description, Aliases]
            public Task IsMyGUB(ModuleInfo module, int page=1)
				=> _GUBCheck(GWLItemType.User, Context.User.Id, UnblockedType.Module, module.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            public Task IsContextGUB(CommandInfo command, int page=1)
				=> _IsContextGUB(UnblockedType.Command, command.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            public Task IsContextGUB(ModuleInfo module, int page=1)
				=> _IsContextGUB(UnblockedType.Module, module.Name.ToLowerInvariant(), page);

			private async Task _IsContextGUB(UnblockedType type, string name, int page)
			{
				if(--page < 0) return; // ensures page is 0-indexed and non-negative

				ulong idC = Context.Channel.Id;
				ulong idS = Context.Guild.Id;

				GWLItemType typeC = GWLItemType.Channel;
				GWLItemType typeS = GWLItemType.Server;

				bool yesC = _gwl.CheckIfUnblockedFor(name, type, idC, typeC, page, out string[] listC, out int countC);
				bool yesS = _gwl.CheckIfUnblockedFor(name, type, idS, typeS, page, out string[] listS, out int countS);

				string serverStr = "*none*";
				string channelStr = "*none*";

				if (yesS) { serverStr = string.Join("\n",listS); }
				if (yesC) { channelStr = string.Join("\n",listC); }

				int lastServerPage = (countS - 1)/_gwl.numPerPage +1;
				int lastChannelPage = (countC - 1)/_gwl.numPerPage +1;

				int lastPage = System.Math.Max( lastServerPage, lastChannelPage );
				page++;
				if (page > lastPage) page = lastPage;
				if (page > 1) {
					if (yesS && page >= lastServerPage) serverStr += GetText("gwl_endlist", lastServerPage);
					if (yesC && page >= lastChannelPage) channelStr += GetText("gwl_endlist", lastChannelPage);
				}

				string desc = "";

				if (yesC && yesS) {
					desc = GetText("gub_is_unblocked_ctx", 
						Format.Code(type.ToString()), 
						Format.Bold(name),
						Format.Code(typeS.ToString()), 
						Context.Guild.Name, 
						Format.Code(typeC.ToString()), 
						MentionUtils.MentionChannel(idC));
				} else if (yesC) {
					desc = GetText("gub_is_unblocked", 
						Format.Code(type.ToString()), 
						Format.Bold(name),
						Format.Code(typeC.ToString()), 
						MentionUtils.MentionChannel(idC));
				} else if (yesS) {
					desc = GetText("gub_is_unblocked", 
						Format.Code(type.ToString()), 
						Format.Bold(name),
						Format.Code(typeS.ToString()), 
						Context.Guild.Name);
				} else {
					await ReplyErrorLocalized("gub_not_unblocked_ctx", 
						Format.Code(type.ToString()), 
						Format.Bold(name),
						Format.Code(typeS.ToString()), 
						Context.Guild.Name, 
						Format.Code(typeC.ToString()), 
						MentionUtils.MentionChannel(idC))
						.ConfigureAwait(false);
					return;
				}

				var embed = new EmbedBuilder()
					.WithOkColor()
					.WithTitle(GetText("gwl_title"))
					.WithDescription(desc)
					.AddField(GetText("gwl_field_channel_ctx", countC), string.Join("\n", channelStr), true)
					.AddField(GetText("gwl_field_server_ctx", countS), string.Join("\n", serverStr), true)
					.WithFooter($"Page {page+1}/{lastPage+1}");

				await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                return;
			}

			#endregion User-Oriented
        }
    }
}
