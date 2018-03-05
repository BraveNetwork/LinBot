using Discord;
using Discord.Commands;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
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
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative

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

				int lastCmdPage = (cmdCount - 1)/_gwl.numPerPage;
				int lastMdlPage = (mdlCount - 1)/_gwl.numPerPage;
				int lastPage = (cmdCount > mdlCount) ? lastCmdPage : lastMdlPage;
				if (page > lastPage) page = lastPage;

				var embed = new EmbedBuilder()
					.WithOkColor()
					.WithTitle(GetText("gwl_title"))
					.WithDescription(GetText("gub_list"))
					.AddField(GetText("gwl_field_commands", cmdCount), strCmd, true)
					.AddField(GetText("gwl_field_modules", mdlCount), strMdl, true)
					.WithFooter($"Page {page+1}/{lastPage+1}");

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
            }

			#region GUBFor

			#region GUBFor FieldType
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GUBForMember(GlobalWhitelistService.FieldType field, IGuild server, ulong id, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.ROLE: 
						await _GUBForRole(server.Id, id, page);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_role", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

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
						await _GUBForUser(id, page);
						return;
					case GlobalWhitelistService.FieldType.ROLE: 
						await _GUBForRole(Context.Guild.Id, id, page);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_member", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			#endregion GUBFor FieldType

			#region GUBFor Member

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBForMember(IGuild server, IRole role, int page=1)
				=> _GUBForRole(server.Id, role.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBForMember(IGuild server, ulong id, int page=1)
				=> _GUBForRole(server.Id, id, page);

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
				=> _GUBForUser(user.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBForMember(IRole role, int page=1)
				=> _GUBForRole(Context.Guild.Id, role.Id, page);

			#endregion GUBFor Member

			private async Task _GUBForMember(GWLItemType type, ulong id, int page)
			{
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative

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

				int lastCmdPage = (cmdCount - 1)/_gwl.numPerPage;
				int lastMdlPage = (mdlCount - 1)/_gwl.numPerPage;
				int lastPage = (cmdCount > mdlCount) ? lastCmdPage : lastMdlPage;
				if (page > lastPage) page = lastPage;

				var embed = new EmbedBuilder()
					.WithOkColor()
					.WithTitle(GetText("gwl_title"))
					.WithDescription(GetText("gub_list_formember", 
						Format.Code(type.ToString()),
						_gwl.GetNameOrMentionFromId(type, id, true)))
					.AddField(GetText("gwl_field_commands", cmdCount), strCmd, true)
					.AddField(GetText("gwl_field_modules", mdlCount), strMdl, true)
					.WithFooter($"Page {page+1}/{lastPage+1}");

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
			}

			private async Task _GUBForUser(ulong id, int page)
			{
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative

				// Error if nothing to show
				if (!_service.UnblockedModules.Any() && !_service.UnblockedCommands.Any())
                {
                    await ReplyErrorLocalized("gub_none").ConfigureAwait(false);
                    return;
                }

				GWLItemType type = GWLItemType.User;
				
				// Get unblocked for user's roles
				bool hasCmdsR = _gwl.GetUnblockedNamesForUserRole(UnblockedType.Command, id, page, out string[] cmdsR, out int cmdRCount);
				bool hasMdlsR = _gwl.GetUnblockedNamesForUserRole(UnblockedType.Module, id, page, out string[] mdlsR, out int mdlRCount);

				// Get unblocked for user self
				bool hasCmds = _gwl.GetUnblockedNamesForMember(UnblockedType.Command, id, type, page, out string[] cmds, out int cmdCount);
				bool hasMdls = _gwl.GetUnblockedNamesForMember(UnblockedType.Module, id, type, page, out string[] mdls, out int mdlCount);

				string strCmd = (hasCmds) ? string.Join("\n", cmds) : "*no such commands*";
				string strMdl = (hasMdls) ? string.Join("\n", mdls) : "*no such modules*";
				string strCmdR = (hasCmdsR) ? string.Join("\n", cmdsR) : "*no such commands*";
				string strMdlR = (hasMdlsR) ? string.Join("\n", mdlsR) : "*no such modules*";

				int lastCmdPage = (cmdCount - 1)/_gwl.numPerPage;
				int lastMdlPage = (mdlCount - 1)/_gwl.numPerPage;
				int lastCmdRPage = (cmdRCount - 1)/_gwl.numPerPage;
				int lastMdlRPage = (mdlRCount - 1)/_gwl.numPerPage;
				
				int lastPage = System.Math.Max(
					System.Math.Max(lastCmdPage, lastCmdRPage),
					System.Math.Max(lastMdlPage,lastMdlRPage));
				if (page > lastPage) page = lastPage;

				var embed = new EmbedBuilder()
					.WithOkColor()
					.WithTitle(GetText("gwl_title"))
					.WithDescription(GetText("gub_list_formember", 
						Format.Code(type.ToString()),
						_gwl.GetNameOrMentionFromId(type, id, true)))
					.AddField(GetText("gwl_field_commands", cmdCount), strCmd, true)
					.AddField(GetText("gwl_field_modules", mdlCount), strMdl, true)
					.AddField(GetText("field_separator"), GetText("gub_role"), false)
					.AddField(GetText("gwl_field_commands", cmdRCount), strCmdR, true)
					.AddField(GetText("gwl_field_modules", mdlRCount), strMdlR, true)
					.WithFooter($"Page {page+1}/{lastPage+1}");

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
			}

			private async Task _GUBForRole(ulong sid, ulong id, int page)
			{
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative
				
				// Error if nothing to show
				if (!_service.UnblockedModules.Any() && !_service.UnblockedCommands.Any())
                {
                    await ReplyErrorLocalized("gub_none").ConfigureAwait(false);
                    return;
                }

				GWLItemType type = GWLItemType.Role;

				// Send list of all unblocked modules/commands and number of lists for each
				bool hasCmds = _gwl.GetUnblockedNamesForRole(UnblockedType.Command, id, sid, page, out string[] cmds, out int cmdCount);
				bool hasMdls = _gwl.GetUnblockedNamesForRole(UnblockedType.Module, id, sid, page, out string[] mdls, out int mdlCount);

				string strCmd = (hasCmds) ? string.Join("\n", cmds) : "*no such commands*";
				string strMdl = (hasMdls) ? string.Join("\n", mdls) : "*no such modules*";

				int lastCmdPage = (cmdCount - 1)/_gwl.numPerPage;
				int lastMdlPage = (mdlCount - 1)/_gwl.numPerPage;
				int lastPage = (cmdCount > mdlCount) ? lastCmdPage : lastMdlPage;
				if (page > lastPage) page = lastPage;

				var embed = new EmbedBuilder()
					.WithOkColor()
					.WithTitle(GetText("gwl_title"))
					.WithDescription(GetText("gub_list_formember", 
						Format.Code(type.ToString()),
						_gwl.GetRoleNameMention(id, sid, Context.Guild.Id, true)))
					.AddField(GetText("gwl_field_commands", cmdCount), strCmd, true)
					.AddField(GetText("gwl_field_modules", mdlCount), strMdl, true)
					.WithFooter($"Page {page+1}/{lastPage+1}");

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
			}

			#endregion GUBFor

			#region GUBCheck

			#region GUBCheck FieldType

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GUBCheck(CommandInfo cmd, GlobalWhitelistService.FieldType field, IGuild server, ulong id, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.ROLE: 
						await _GUBCheckRoleCommand(server.Id, id, cmd.Module.GetTopLevelModule().Name.ToLowerInvariant(), cmd.Name.ToLowerInvariant(), page);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_role", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GUBCheck(ModuleInfo mdl, GlobalWhitelistService.FieldType field, IGuild server, ulong id, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.ROLE: 
						await _GUBCheckRole(server.Id, id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_role", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GUBCheck(CommandInfo cmd, GlobalWhitelistService.FieldType field, ulong id, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _GUBCheckCommand(GWLItemType.Server, id, cmd.Module.GetTopLevelModule().Name.ToLowerInvariant(), cmd.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _GUBCheckCommand(GWLItemType.Channel, id, cmd.Module.GetTopLevelModule().Name.ToLowerInvariant(), cmd.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _GUBCheckUserCommand(id, cmd.Module.GetTopLevelModule().Name.ToLowerInvariant(), cmd.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.ROLE: 
						await _GUBCheckRoleCommand(Context.Guild.Id, id, cmd.Module.GetTopLevelModule().Name.ToLowerInvariant(), cmd.Name.ToLowerInvariant(), page);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_member", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GUBCheck(ModuleInfo mdl, GlobalWhitelistService.FieldType field, ulong id, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _GUBCheck(GWLItemType.Server, id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _GUBCheck(GWLItemType.Channel, id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _GUBCheckUser(id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);
						return;
					case GlobalWhitelistService.FieldType.ROLE: 
						await _GUBCheckRole(Context.Guild.Id, id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);
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
            public Task GUBCheck(CommandInfo cmd, IGuild server, IRole role, int page=1)
				=> _GUBCheckRoleCommand(server.Id, role.Id, cmd.Module.GetTopLevelModule().Name.ToLowerInvariant(), cmd.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(CommandInfo cmd, IGuild server, ulong id, int page=1)
				=> _GUBCheckRoleCommand(server.Id, id, cmd.Module.GetTopLevelModule().Name.ToLowerInvariant(), cmd.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(CommandInfo cmd, IGuild server, int page=1)
				=> _GUBCheckCommand(GWLItemType.Server, server.Id, cmd.Module.GetTopLevelModule().Name.ToLowerInvariant(), cmd.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(CommandInfo cmd, ITextChannel channel, int page=1)
				=> _GUBCheckCommand(GWLItemType.Channel, channel.Id, cmd.Module.GetTopLevelModule().Name.ToLowerInvariant(), cmd.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(CommandInfo cmd, IUser user, int page=1)
				=> _GUBCheckUserCommand(user.Id, cmd.Module.GetTopLevelModule().Name.ToLowerInvariant(), cmd.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(CommandInfo cmd, IRole role, int page=1)
				=> _GUBCheckRoleCommand(Context.Guild.Id, role.Id, cmd.Module.GetTopLevelModule().Name.ToLowerInvariant(), cmd.Name.ToLowerInvariant(), page);

			#endregion GUBCheck Command

			#region GUBCheck Module

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(ModuleInfo mdl, IGuild server, IRole role, int page=1)
				=> _GUBCheckRole(server.Id, role.Id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(ModuleInfo mdl, IGuild server, ulong id, int page=1)
				=> _GUBCheckRole(server.Id, id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(ModuleInfo mdl, IGuild server, int page=1)
				=> _GUBCheck(GWLItemType.Server, server.Id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(ModuleInfo mdl, ITextChannel channel, int page=1)
				=> _GUBCheck(GWLItemType.Channel, channel.Id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(ModuleInfo mdl, IUser user, int page=1)
				=> _GUBCheckUser(user.Id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GUBCheck(ModuleInfo mdl, IRole role, int page=1)
				=> _GUBCheckRole(Context.Guild.Id, role.Id, UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			#endregion GUBCheck Module

			private async Task _GUBCheck(GWLItemType memType, ulong memID, UnblockedType ubType, string ubName, int page=1)
			{
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative

				bool ubForAll = _gwl.CheckIfUnblockedForAll(ubName, ubType, page, out string[] listsA, out int countA);
				bool ubForMem = _gwl.CheckIfUnblockedForMember(ubName, ubType, memID, memType, page, out string[] listsM, out int countM);

				if (ubForAll || ubForMem)
				{
					string all = (ubForAll) ? string.Join("\n",listsA) : "*none*";
					string mem = (ubForMem) ? string.Join("\n",listsM) : "*none*";

					int lastAPage = (countA - 1)/_gwl.numPerPage;
					int lastMPage = (countM - 1)/_gwl.numPerPage;
					int lastPage = System.Math.Max(lastAPage, lastMPage);
					if (page > lastPage) page = lastPage;

					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gub_is_unblocked", 
							Format.Code(ubType.ToString()), 
							Format.Bold(ubName),
							Format.Code(memType.ToString()),
							_gwl.GetNameOrMentionFromId(memType, memID, true)
							))
						.AddField(GetText("gwl_field_title_mem", countM), mem, true)
						.AddField(GetText("gwl_field_title_all", countA), all, true)
						.WithFooter($"Page {page+1}/{lastPage+1}");

					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                	return;

				} else
				{
					await ReplyErrorLocalized("gub_not_unblocked", 
						Format.Code(ubType.ToString()), 
						Format.Bold(ubName),
						Format.Code(memType.ToString()),
						_gwl.GetNameOrMentionFromId(memType, memID, true)
						)
					.ConfigureAwait(false);
                	return;
				}
			}

			private async Task _GUBCheckCommand(GWLItemType memType, ulong memID, string mdlName, string ubName, int page=1)
			{
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative

				UnblockedType ubType = UnblockedType.Command;

				bool ubForAll = _gwl.CheckIfUnblockedForAll(mdlName, ubName, page, out string[] listsA, out int countA);
				bool ubForMem = _gwl.CheckIfUnblockedForMember(mdlName, ubName, memID, memType, page, out string[] listsM, out int countM);

				if (ubForAll || ubForMem)
				{
					string all = (ubForAll) ? string.Join("\n",listsA) : "*none*";
					string mem = (ubForMem) ? string.Join("\n",listsM) : "*none*";

					int lastAPage = (countA - 1)/_gwl.numPerPage;
					int lastMPage = (countM - 1)/_gwl.numPerPage;
					int lastPage = System.Math.Max(lastAPage, lastMPage);
					if (page > lastPage) page = lastPage;

					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gub_is_unblocked_cmd", 
							Format.Code(ubType.ToString()), 
							Format.Bold(ubName),
							Format.Code(memType.ToString()),
							_gwl.GetNameOrMentionFromId(memType, memID, true),
							Format.Bold(mdlName)
							))
						.AddField(GetText("gwl_field_title_mem", countM), mem, true)
						.AddField(GetText("gwl_field_title_all", countA), all, true)
						.WithFooter($"Page {page+1}/{lastPage+1}");

					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                	return;

				} else
				{
					await ReplyErrorLocalized("gub_not_unblocked_cmd", 
						Format.Code(ubType.ToString()), 
						Format.Bold(ubName),
						Format.Code(memType.ToString()),
						_gwl.GetNameOrMentionFromId(memType, memID, true),
						Format.Bold(mdlName)
						)
					.ConfigureAwait(false);
                	return;
				}
			}

			private async Task _GUBCheckUser(ulong id, UnblockedType ubType, string ubName, int page=1)
			{
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative

				GWLItemType memType = GWLItemType.User;

				bool ubForAll = _gwl.CheckIfUnblockedForAll(ubName, ubType, page, out string[] listsA, out int countA);
				bool ubForMem = _gwl.CheckIfUnblockedForMember(ubName, ubType, id, memType, page, out string[] listsM, out int countM);
				bool ubForRole = _gwl.CheckIfUnblockedForUserRole(ubName, ubType, id, page, out string[] listsR, out int countR);

				if (ubForAll || ubForMem || ubForRole)
				{
					string all = (ubForAll) ? string.Join("\n",listsA) : "*none*";
					string mem = (ubForMem) ? string.Join("\n",listsM) : "*none*";
					string role = (ubForRole) ? string.Join("\n",listsR) : "*none*";

					int lastAPage = (countA - 1)/_gwl.numPerPage;
					int lastMPage = (countM - 1)/_gwl.numPerPage;
					int lastRPage = (countR - 1)/_gwl.numPerPage;
					int lastPage = System.Math.Max(lastAPage, System.Math.Max(lastMPage, lastRPage));
					if (page > lastPage) page = lastPage;

					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gub_is_unblocked", 
							Format.Code(ubType.ToString()), 
							Format.Bold(ubName),
							Format.Code(memType.ToString()),
							_gwl.GetNameOrMentionFromId(memType, id, true)
							))
						.AddField(GetText("gwl_field_title_mem", countM), mem, true)
						.AddField(GetText("gwl_field_title_role", countR), role, true)
						.AddField(GetText("gwl_field_title_all", countA), all, true)
						.WithFooter($"Page {page+1}/{lastPage+1}");

					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                	return;

				} else
				{
					await ReplyErrorLocalized("gub_not_unblocked", 
						Format.Code(ubType.ToString()), 
						Format.Bold(ubName),
						Format.Code(memType.ToString()),
						_gwl.GetNameOrMentionFromId(memType, id, true)
						)
					.ConfigureAwait(false);
                	return;
				}
			}

			private async Task _GUBCheckUserCommand(ulong id, string mdlName, string ubName, int page=1)
			{
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative

				GWLItemType memType = GWLItemType.User;
				UnblockedType ubType = UnblockedType.Command;

				bool ubForAll = _gwl.CheckIfUnblockedForAll(mdlName, ubName, page, out string[] listsA, out int countA);
				bool ubForMem = _gwl.CheckIfUnblockedForMember(mdlName, ubName, id, memType, page, out string[] listsM, out int countM);
				bool ubForRole = _gwl.CheckIfUnblockedForUserRole(mdlName, ubName, id, page, out string[] listsR, out int countR);

				if (ubForAll || ubForMem || ubForRole)
				{
					string all = (ubForAll) ? string.Join("\n",listsA) : "*none*";
					string mem = (ubForMem) ? string.Join("\n",listsM) : "*none*";
					string role = (ubForRole) ? string.Join("\n",listsR) : "*none*";

					int lastAPage = (countA - 1)/_gwl.numPerPage;
					int lastMPage = (countM - 1)/_gwl.numPerPage;
					int lastRPage = (countR - 1)/_gwl.numPerPage;
					int lastPage = System.Math.Max(lastAPage, System.Math.Max(lastMPage, lastRPage));
					if (page > lastPage) page = lastPage;

					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gub_is_unblocked_cmd", 
							Format.Code(ubType.ToString()), 
							Format.Bold(ubName),
							Format.Code(memType.ToString()),
							_gwl.GetNameOrMentionFromId(memType, id, true),
							Format.Bold(mdlName)))
						.AddField(GetText("gwl_field_title_mem", countM), mem, true)
						.AddField(GetText("gwl_field_title_role", countR), role, true)
						.AddField(GetText("gwl_field_title_all", countA), all, true)
						.WithFooter($"Page {page+1}/{lastPage+1}");

					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                	return;

				} else
				{
					await ReplyErrorLocalized("gub_not_unblocked_cmd", 
						Format.Code(ubType.ToString()), 
						Format.Bold(ubName),
						Format.Code(memType.ToString()),
						_gwl.GetNameOrMentionFromId(memType, id, true),
						Format.Bold(mdlName)
						)
					.ConfigureAwait(false);
                	return;
				}
			}

			private async Task _GUBCheckRole(ulong sid, ulong id, UnblockedType ubType, string ubName, int page=1)
			{
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative

				GWLItemType memType = GWLItemType.Role;

				bool ubForAll = _gwl.CheckIfUnblockedForAll(ubName, ubType, page, out string[] listsA, out int countA);
				bool ubForRole = _gwl.CheckIfUnblockedForRole(ubName, ubType, sid, id, page, out string[] listsR, out int countR);

				if (ubForAll || ubForRole)
				{
					string all = (ubForAll) ? string.Join("\n",listsA) : "*none*";
					string role = (ubForRole) ? string.Join("\n",listsR) : "*none*";

					int lastAPage = (countA - 1)/_gwl.numPerPage;
					int lastRPage = (countR - 1)/_gwl.numPerPage;
					int lastPage = System.Math.Max(lastAPage, lastRPage);
					if (page > lastPage) page = lastPage;

					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gub_is_unblocked", 
							Format.Code(ubType.ToString()), 
							Format.Bold(ubName),
							Format.Code(memType.ToString()),
							_gwl.GetRoleNameMention(id, sid, Context.Guild.Id, true)
							))
						.AddField(GetText("gwl_field_title_role", countR), role, true)
						.AddField(GetText("gwl_field_title_all", countA), all, true)
						.WithFooter($"Page {page+1}/{lastPage+1}");

					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                	return;

				} else
				{
					await ReplyErrorLocalized("gub_not_unblocked", 
						Format.Code(ubType.ToString()), 
						Format.Bold(ubName),
						Format.Code(memType.ToString()),
						_gwl.GetRoleNameMention(id, sid, Context.Guild.Id, true)
						)
					.ConfigureAwait(false);
                	return;
				}
			}

			private async Task _GUBCheckRoleCommand(ulong sid, ulong id, string mdlName, string ubName, int page=1)
			{
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative

				GWLItemType memType = GWLItemType.Role;
				UnblockedType ubType = UnblockedType.Command;

				bool ubForAll = _gwl.CheckIfUnblockedForAll(mdlName, ubName, page, out string[] listsA, out int countA);
				bool ubForRole = _gwl.CheckIfUnblockedForRole(mdlName, ubName, sid, id, page, out string[] listsR, out int countR);

				if (ubForAll || ubForRole)
				{
					string all = (ubForAll) ? string.Join("\n",listsA) : "*none*";
					string role = (ubForRole) ? string.Join("\n",listsR) : "*none*";

					int lastAPage = (countA - 1)/_gwl.numPerPage;
					int lastRPage = (countR - 1)/_gwl.numPerPage;
					int lastPage = System.Math.Max(lastAPage, lastRPage);
					if (page > lastPage) page = lastPage;

					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gub_is_unblocked_cmd", 
							Format.Code(ubType.ToString()), 
							Format.Bold(ubName),
							Format.Code(memType.ToString()),
							_gwl.GetRoleNameMention(id, sid, Context.Guild.Id, true),
							Format.Bold(mdlName)
							))
						.AddField(GetText("gwl_field_title_role", countR), role, true)
						.AddField(GetText("gwl_field_title_all", countA), all, true)
						.WithFooter($"Page {page+1}/{lastPage+1}");

					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                	return;

				} else
				{
					await ReplyErrorLocalized("gub_not_unblocked_cmd", 
						Format.Code(ubType.ToString()), 
						Format.Bold(ubName),
						Format.Code(memType.ToString()),
						_gwl.GetRoleNameMention(id, sid, Context.Guild.Id, true),
						Format.Bold(mdlName)
						)
					.ConfigureAwait(false);
                	return;
				}
			}

			#endregion GUBCheck

			#region User-Oriented

			[NadekoCommand, Usage, Description, Aliases]
			[RequireContext(ContextType.Guild)]
            public Task ListMyGUB(int page=1)
            	=> _GUBForUser(Context.User.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
			[RequireContext(ContextType.Guild)]
            public async Task ListContextGUB(int page=1)
            {
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative

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

				bool hasCmdsA = _gwl.GetUnblockedNamesForAll(UnblockedType.Command, page, out string[] cmdsA, out int cmdACount);
				bool hasMdlsA = _gwl.GetUnblockedNamesForAll(UnblockedType.Module, page, out string[] mdlsA, out int mdlACount);

				// Send list of all unblocked modules/commands for current context
				bool hasCmds = _gwl.GetUnblockedNamesForContext(UnblockedType.Command, idC, idS, page, out string[] cmds, out int cmdCount);
				bool hasMdls = _gwl.GetUnblockedNamesForContext(UnblockedType.Module, idC, idS, page, out string[] mdls, out int mdlCount);

				string strCmd = (cmdCount>0) ? string.Join("\n", cmds) : "*no such commands*";
				string strMdl = (mdlCount>0) ? string.Join("\n", mdls) : "*no such modules*";
				string strCmdA = (cmdACount>0) ? string.Join("\n", cmdsA) : "*no such commands*";
				string strMdlA = (mdlACount>0) ? string.Join("\n", mdlsA) : "*no such modules*";

				int lastCmdPage = (cmdCount - 1)/_gwl.numPerPage;
				int lastMdlPage = (mdlCount - 1)/_gwl.numPerPage;
				int lastCmdAPage = (cmdACount - 1)/_gwl.numPerPage;
				int lastMdlAPage = (mdlACount - 1)/_gwl.numPerPage;
				
				int lastPage = System.Math.Max(
					System.Math.Max(lastCmdPage, lastCmdAPage),
					System.Math.Max(lastMdlPage,lastMdlAPage));
				if (page > lastPage) page = lastPage;

				var embed = new EmbedBuilder()
					.WithOkColor()
					.WithTitle(GetText("gwl_title"))
					.WithDescription(GetText("gub_list_formember", 
						GetText("gwl_current_ctx", 
							Format.Code(typeS.ToString()),
							Format.Bold(Context.Guild.Name),
							Format.Code(typeC.ToString()),
							MentionUtils.MentionChannel(idC)), ""
						))
					.AddField(GetText("gwl_field_commands", cmdCount), strCmd, true)
					.AddField(GetText("gwl_field_modules", mdlCount), strMdl, true)
					.AddField(GetText("field_separator"), GetText("gub_general"), false)
					.AddField(GetText("gwl_field_commands", cmdACount), strCmdA, true)
					.AddField(GetText("gwl_field_modules", mdlACount), strMdlA, true)
					.WithFooter($"Page {page+1}/{lastPage+1}");

                await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
				return;
			}

			[NadekoCommand, Usage, Description, Aliases]
			[RequireContext(ContextType.Guild)]
            public Task IsMyGUB(CommandInfo command, int page=1)
				=> _GUBCheckUserCommand(Context.User.Id, command.Module.GetTopLevelModule().Name.ToLowerInvariant(), command.Name.ToLowerInvariant(), page);
			
			[NadekoCommand, Usage, Description, Aliases]
			[RequireContext(ContextType.Guild)]
            public Task IsMyGUB(ModuleInfo module, int page=1)
				=> _GUBCheckUser(Context.User.Id, UnblockedType.Module, module.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
			[RequireContext(ContextType.Guild)]
            public Task IsContextGUB(CommandInfo command, int page=1)
				=> _IsContextGUBCommand(command.Module.GetTopLevelModule().Name.ToLowerInvariant(), command.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
			[RequireContext(ContextType.Guild)]
            public Task IsContextGUB(ModuleInfo module, int page=1)
				=> _IsContextGUB(UnblockedType.Module, module.Name.ToLowerInvariant(), page);

			private async Task _IsContextGUB(UnblockedType type, string name, int page)
			{
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative

				ulong idC = Context.Channel.Id;
				ulong idS = Context.Guild.Id;

				GWLItemType typeC = GWLItemType.Channel;
				GWLItemType typeS = GWLItemType.Server;

				bool yesC = _gwl.CheckIfUnblockedForMember(name, type, idC, typeC, page, out string[] listC, out int countC);
				bool yesS = _gwl.CheckIfUnblockedForMember(name, type, idS, typeS, page, out string[] listS, out int countS);
				bool ubForAll = _gwl.CheckIfUnblockedForAll(name, type, page, out string[] listA, out int countA);

				string serverStr = (yesS) ? string.Join("\n",listS) : "*none*";
				string channelStr = (yesC) ? string.Join("\n",listC) : "*none*";
				string allStr = (ubForAll) ? string.Join("\n",listA) : "*none*";

				int lastServerPage = (countS - 1)/_gwl.numPerPage;
				int lastChannelPage = (countC - 1)/_gwl.numPerPage;
				int lastAPage = (countA - 1)/_gwl.numPerPage;
				int lastPage = System.Math.Max( lastAPage, System.Math.Max( lastServerPage, lastChannelPage ));
				if (page > lastPage) page = lastPage;

				string desc = "";

				if (yesC || yesS || ubForAll) {
					desc = GetText("gub_is_unblocked", 
						Format.Code(type.ToString()), 
						Format.Bold(name),
						GetText("gwl_current_ctx", 
							Format.Code(typeS.ToString()),
							Format.Bold(Context.Guild.Name),
							Format.Code(typeC.ToString()),
							MentionUtils.MentionChannel(idC)), ""
						);
				} else {
					await ReplyErrorLocalized("gub_not_unblocked", 
						Format.Code(type.ToString()), 
						Format.Bold(name),
						GetText("gwl_current_ctx", 
							Format.Code(typeS.ToString()),
							Format.Bold(Context.Guild.Name),
							Format.Code(typeC.ToString()),
							MentionUtils.MentionChannel(idC)), ""
						)
						.ConfigureAwait(false);
					return;
				}

				var embed = new EmbedBuilder()
					.WithOkColor()
					.WithTitle(GetText("gwl_title"))
					.WithDescription(desc)
					.AddField(GetText("gwl_field_title_chnl", countC), channelStr, true)
					.AddField(GetText("gwl_field_title_srvr", countS), serverStr, true)
					.AddField(GetText("gwl_field_title_all", countA), allStr, true)
					.WithFooter($"Page {page+1}/{lastPage+1}");

				await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                return;
			}

			private async Task _IsContextGUBCommand(string mdlName, string name, int page)
			{
				if(--page < 0) page = 0; // ensures page is 0-indexed and non-negative

				ulong idC = Context.Channel.Id;
				ulong idS = Context.Guild.Id;

				GWLItemType typeC = GWLItemType.Channel;
				GWLItemType typeS = GWLItemType.Server;

				UnblockedType type = UnblockedType.Command;

				bool yesC = _gwl.CheckIfUnblockedForMember(mdlName, name, idC, typeC, page, out string[] listC, out int countC);
				bool yesS = _gwl.CheckIfUnblockedForMember(mdlName, name, idS, typeS, page, out string[] listS, out int countS);
				bool ubForAll = _gwl.CheckIfUnblockedForAll(mdlName, name, page, out string[] listA, out int countA);

				string serverStr = (yesS) ? string.Join("\n",listS) : "*none*";
				string channelStr = (yesC) ? string.Join("\n",listC) : "*none*";
				string allStr = (ubForAll) ? string.Join("\n",listA) : "*none*";

				int lastServerPage = (countS - 1)/_gwl.numPerPage ;
				int lastChannelPage = (countC - 1)/_gwl.numPerPage;
				int lastAPage = (countA - 1)/_gwl.numPerPage;
				int lastPage = System.Math.Max( lastAPage, System.Math.Max( lastServerPage, lastChannelPage ));
				if (page > lastPage) page = lastPage;

				string desc = "";

				if (yesC || yesS || ubForAll) {
					desc = GetText("gub_is_unblocked_cmd", 
						Format.Code(type.ToString()), 
						Format.Bold(name),
						GetText("gwl_current_ctx", 
							Format.Code(typeS.ToString()),
							Format.Bold(Context.Guild.Name),
							Format.Code(typeC.ToString()),
							MentionUtils.MentionChannel(idC)), "",
							Format.Bold(mdlName)
						);
				} else {
					await ReplyErrorLocalized("gub_not_unblocked_cmd", 
						Format.Code(type.ToString()), 
						Format.Bold(name),
						GetText("gwl_current_ctx", 
							Format.Code(typeS.ToString()),
							Format.Bold(Context.Guild.Name),
							Format.Code(typeC.ToString()),
							MentionUtils.MentionChannel(idC)), "",
						Format.Bold(mdlName)
						)
						.ConfigureAwait(false);
					return;
				}

				var embed = new EmbedBuilder()
					.WithOkColor()
					.WithTitle(GetText("gwl_title"))
					.WithDescription(desc)
					.AddField(GetText("gwl_field_title_chnl", countC), channelStr, true)
					.AddField(GetText("gwl_field_title_srvr", countS), serverStr, true)
					.AddField(GetText("gwl_field_title_all", countA), allStr, true)
					.WithFooter($"Page {page+1}/{lastPage+1}");

				await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                return;
			}

			#endregion User-Oriented
        }
    }
}
