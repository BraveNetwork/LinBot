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
using NadekoBot.Common.TypeReaders.Models;

namespace NadekoBot.Modules.Permissions
{
    public partial class Permissions
    {
        [Group]
        public class GlobalWhitelistCommands : NadekoSubmodule<GlobalWhitelistService>
        {
			private GlobalPermissionService _perm;

			public enum SyncMethod {
				OR,
				AND
			};

			public const int MaxNumInput = 30;
			public const int MaxNameLength = 20;

            public GlobalWhitelistCommands(GlobalPermissionService perm)
            {
				_perm = perm;
            }

			#region Whitelist Utilities

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GWLCreate(string listName="")
            {
                if (string.IsNullOrWhiteSpace(listName) || listName.Length > MaxNameLength) {
					await ReplyErrorLocalized("gwl_name_error", Format.Bold(listName), MaxNameLength).ConfigureAwait(false);
                	return;
				}

				// Ensure a similar name doesnt already exist
				bool exists = _service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group);
                if (exists) {
					await ReplyErrorLocalized("gwl_create_dupe", Format.Bold(listName), Format.Bold(group.ListName)).ConfigureAwait(false);
                	return;
				}
				// Create new list
				if (_service.CreateWhitelist(listName))
                {
                    await ReplyConfirmLocalized("gwl_create_success", Format.Bold(listName)).ConfigureAwait(false);
                	return;
                }
				// Failure
				await ReplyErrorLocalized("gwl_create_fail", Format.Bold(listName)).ConfigureAwait(false);
                return;
            }

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GWLRename(string listName="", string newName="")
            {
				if (string.IsNullOrWhiteSpace(newName) || newName.Length > MaxNameLength) {
					await ReplyErrorLocalized("gwl_name_error", Format.Bold(listName), MaxNameLength).ConfigureAwait(false);
                	return;
				}

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
						await ReplyErrorLocalized("gwl_rename_fail", Format.Bold(group.ListName), Format.Bold(newName)).ConfigureAwait(false);
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
			public async Task GWLEnable(PermissionAction doEnable, string listName="")
			{
				string listNameI = listName.ToLowerInvariant();
				if (_service.GetGroupByName(listNameI, out GlobalWhitelistSet group)) {
					if (doEnable.Value) {
						_service.SetEnabledStatus(listNameI, true);
						await ReplyConfirmLocalized("gwl_status_enabled", Format.Bold(group.ListName), Format.Code(GetText("gwl_status_enabled_emoji"))).ConfigureAwait(false);
						return;
					} else {
						_service.SetEnabledStatus(listNameI, false);
						await ReplyConfirmLocalized("gwl_status_disabled", Format.Bold(group.ListName), Format.Code(GetText("gwl_status_disabled_emoji"))).ConfigureAwait(false);
						return;
					}
				} else {
					await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public async Task GWLEnable(string listName="")
			{
				string listNameI = listName.ToLowerInvariant();
				if (_service.GetGroupByName(listNameI, out GlobalWhitelistSet group)) {
					string statusTxt = (group.IsEnabled) ? GetText("gwl_status_enabled_emoji") : GetText("gwl_status_disabled_emoji");
					await ReplyConfirmLocalized("gwl_status", Format.Bold(group.ListName), Format.Code(statusTxt)).ConfigureAwait(false);
					return;
				} else {
					await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);
                    return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GWLDelete(string listName="")
            {
                if (!_service.DeleteWhitelist(listName.ToLowerInvariant()))
                {
                    await ReplyErrorLocalized("gwl_delete_fail", Format.Bold(listName)).ConfigureAwait(false);
                    return;
                }
                await ReplyConfirmLocalized("gwl_delete_success", Format.Bold(listName)).ConfigureAwait(false);
                return;
            }

			#endregion Whitelist Utilities

			#region Add/Remove

			#region Add/Remove FieldType

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public async Task GWLAddRemove(AddRemove action, GlobalWhitelistService.FieldType field, string listName="", params ulong[] ids)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _AddRemove(action, GlobalWhitelistType.Server, listName, ids);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _AddRemove(action, GlobalWhitelistType.Channel, listName, ids);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _AddRemove(action, GlobalWhitelistType.User, listName, ids);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_member", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public async Task GWLAddRemove(AddRemove action, GlobalWhitelistService.FieldType field, string listName="", params string[] names)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.COMMAND: 
						await _AddRemove(action, UnblockedType.Command, listName, names);
						return;
					case GlobalWhitelistService.FieldType.MODULE: 
						await _AddRemove(action, UnblockedType.Module, listName, names);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_unblock", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}
			
			#endregion Add/Remove FieldType

			#region Add/Rem Members

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public async Task GWLAddRemove(AddRemove action, string listName="", params IGuild[] servers)
			{
				if (servers.Length > MaxNumInput) {
					await ReplyErrorLocalized("gwl_toomany_params", Format.Code("Server"), MaxNumInput).ConfigureAwait(false);
                    return;
				}
				ulong[] ids = new ulong[servers.Length];
				for (int i=0; i<servers.Length; i++) {
					ids[i] = servers[i].Id;
				}
				await _AddRemove(action, GlobalWhitelistType.Server, listName, ids);
				return;
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public async Task GWLAddRemove(AddRemove action, string listName="", params ITextChannel[] channels)
			{
				if (channels.Length > MaxNumInput) {
					await ReplyErrorLocalized("gwl_toomany_params", Format.Code("Channel"), MaxNumInput).ConfigureAwait(false);
                    return;
				}
				ulong[] ids = new ulong[channels.Length];
				for (int i=0; i<channels.Length; i++) {
					ids[i] = channels[i].Id;
				}
				await _AddRemove(action, GlobalWhitelistType.Channel, listName, ids);
				return;
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public async Task GWLAddRemove(AddRemove action, string listName="", params IUser[] users)
			{
				if (users.Length > MaxNumInput) {
					await ReplyErrorLocalized("gwl_toomany_params", Format.Code("User"), MaxNumInput).ConfigureAwait(false);
                    return;
				}
				ulong[] ids = new ulong[users.Length];
				for (int i=0; i<users.Length; i++) {
					ids[i] = users[i].Id;
				}
				await _AddRemove(action, GlobalWhitelistType.User, listName, ids);
				return;
			}

			#endregion Add/Rem Members

			#region Add/Rem Unblocked

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public async Task GWLAddRemove(AddRemove action, string listName="", params CommandOrCrInfo[] cmds)
			{
				if (cmds.Length > MaxNumInput) {
					await ReplyErrorLocalized("gwl_toomany_params", Format.Code("Command"), MaxNumInput).ConfigureAwait(false);
                    return;
				}
				string[] names = new string[cmds.Length];
				for (int i=0; i<cmds.Length; i++) {
					names[i] = cmds[i].Name.ToLowerInvariant();
				}
				await _AddRemove(action, UnblockedType.Command, listName, names);
				return;
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public async Task GWLAddRemove(AddRemove action, string listName="", params ModuleOrCrInfo[] mdls)
			{
				if (mdls.Length > MaxNumInput) {
					await ReplyErrorLocalized("gwl_toomany_params", Format.Code("Module"), MaxNumInput).ConfigureAwait(false);
                    return;
				}
				string[] names = new string[mdls.Length];
				for (int i=0; i<mdls.Length; i++) {
					names[i] = mdls[i].Name.ToLowerInvariant();
				}
				await _AddRemove(action, UnblockedType.Module, listName, names);
				return;
			}

			#endregion Add/Rem Unblocked

			private async Task _AddRemove(AddRemove action, GlobalWhitelistType type, string listName="", params ulong[] ids)
			{
				// If params is empty, report error
				if (ids.Length < 1) {
					await ReplyErrorLocalized("gwl_missing_params", Format.Code(type.ToString())).ConfigureAwait(false);
                    return;
				}
				// If params is too long, report error
				if (ids.Length > MaxNumInput) {
					await ReplyErrorLocalized("gwl_toomany_params", Format.Code(type.ToString()), MaxNumInput).ConfigureAwait(false);
                    return;
				}
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
							await ReplyConfirmLocalized("gwl_add_success",
								successList.Count(), ids.Count(),
								Format.Code(type.ToString()+"s"),
								Format.Bold(group.ListName),
								strList)
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_add_fail",
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
							await ReplyConfirmLocalized("gwl_remove_success", 
								successList.Count(), ids.Count(),
								Format.Code(type.ToString()+"s"),
								Format.Bold(group.ListName),
								strList)
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_remove_fail", 
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

			private async Task _AddRemove(AddRemove action, UnblockedType type, string listName="", params string[] names)
			{
				// If params is empty, report error
				if (names.Length < 1) {
					await ReplyErrorLocalized("gwl_missing_params", Format.Code(type.ToString())).ConfigureAwait(false);
                    return;
				}
				// If params is too long, report error
				if (names.Length > MaxNumInput) {
					await ReplyErrorLocalized("gwl_toomany_params", Format.Code(type.ToString()), MaxNumInput).ConfigureAwait(false);
                    return;
				}
				// If the listName doesn't exist, return an error message
                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
                    // Get itemlist string
					string itemList = string.Join("\n",names);

					// Process Add Command/Module
					if (action == AddRemove.Add) 
					{   
						// Keep track of internal changes
						int delta = 0;

						// Add to hashset in GlobalPermissionService
						if (type == UnblockedType.Command)
						{
							int pre = _perm.UnblockedCommands.Count;
							_perm.UnblockedCommands.AddRange(names);
							delta = _perm.UnblockedCommands.Count - pre;
						}
						else {
							int pre = _perm.UnblockedModules.Count;
							_perm.UnblockedModules.AddRange(names);
							delta = _perm.UnblockedModules.Count - pre;
						}

						System.Console.WriteLine("Added {0} items to GlobalPermissionService Unblocked HashSet", delta);

						// Add to a whitelist
						if(_service.AddUbItemToGroupBulk(names,type,group, out string[] successList))
						{
							await ReplyConfirmLocalized("gwl_add_success",
								successList.Count(), names.Count(),
								Format.Code(type.ToString()+"s"), 
								Format.Bold(group.ListName), 
								Format.Bold(string.Join("\n", successList)))
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_add_fail",
								successList.Count(), names.Count(),
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
						if(_service.RemoveUbItemFromGroupBulk(names,type,group, out string[] successList))
						{
							await ReplyConfirmLocalized("gwl_remove_success",
								successList.Count(), names.Count(),
								Format.Code(type.ToString()+"s"),
								Format.Bold(group.ListName),
								Format.Bold(string.Join("\n", successList)))
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_remove_fail",
								successList.Count(), names.Count(),
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

			#endregion Add/Remove

			#region RoleSync
			
			#region RoleSync Bulk

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GWLRoleSync(SyncMethod method, string listName="", params IRole[] roles)
			{
				if (roles.Length > MaxNumInput) {
					await ReplyErrorLocalized("gwl_toomany_params", Format.Code("Role"), MaxNumInput).ConfigureAwait(false);
                    return;
				}
				ulong[] ids = new ulong[roles.Length];
				for (int i=0; i<roles.Length; i++) {
					ids[i] = roles[i].Id;
				}
				await _RoleSync(method, listName, ids);
				return;
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public Task GWLRoleSync(SyncMethod method, string listName="", params ulong[] ids)
				=> _RoleSync(method, listName, ids);

			private async Task _RoleSync(SyncMethod method, string listName, params ulong[] ids)
			{
				// If params is empty, report error
				if (ids.Length < 1) {
					await ReplyErrorLocalized("gwl_missing_params", Format.Code("Role")).ConfigureAwait(false);
                    return;
				}
				// If params is too long, report error
				if (ids.Length > MaxNumInput) {
					await ReplyErrorLocalized("gwl_toomany_params", Format.Code("Role"), MaxNumInput).ConfigureAwait(false);
                    return;
				}

				await ReplyConfirmLocalized("You used a bulk RoleSync command!").ConfigureAwait(false);
                return;
			}

			#endregion RoleSync Bulk

			#region RoleSync Single

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLRoleSync(IRole role, string listName="")
				=> _RoleSync(role.Id, listName);
			
			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLRoleSync(ulong id, string listName="")
				=> _RoleSync(id, listName);
			
			private async Task _RoleSync(ulong id, string listName)
			{
				await ReplyConfirmLocalized("You used a RoleSync command!").ConfigureAwait(false);
                return;
			}

			#endregion RoleSync Single

			#endregion RoleSync

			#region Clear

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLClear(GlobalWhitelistService.FieldType field, string listName="")
				=> _Clear(field, listName);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLClear(string listName="")
				=> _Clear(GlobalWhitelistService.FieldType.ALL, listName);

			private async Task _Clear(GlobalWhitelistService.FieldType field, string listName)
			{
				if (_service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group)) 
				{
					bool result;
					string typeName;

					switch(field) {
						case GlobalWhitelistService.FieldType.COMMAND:
							result = _service.ClearUnblocked(group, UnblockedType.Command);
							typeName = "Command";
							break;

						case GlobalWhitelistService.FieldType.MODULE:
							result = _service.ClearUnblocked(group, UnblockedType.Module);
							typeName = "Module";
							break;

						case GlobalWhitelistService.FieldType.UNBLOCKED:
							result = _service.ClearUnblocked(group);
							typeName = "All Unblocked";
							break;

						case GlobalWhitelistService.FieldType.MEMBER:
							result = _service.ClearMembers(group);
							typeName = "All Members";
							break;

						case GlobalWhitelistService.FieldType.SERVER:
							result = _service.ClearMembers(group, GlobalWhitelistType.Server);
							typeName = "Server";
							break;

						case GlobalWhitelistService.FieldType.CHANNEL:
							result = _service.ClearMembers(group, GlobalWhitelistType.Channel);
							typeName = "Channel";
							break;

						case GlobalWhitelistService.FieldType.USER:
							result = _service.ClearMembers(group, GlobalWhitelistType.User);
							typeName = "User";
							break;

						default:
							result = _service.ClearAll(group);
							typeName = "Everything";
							break;
					}

					if (result)
					{
						await ReplyConfirmLocalized("gwl_clear_success", Format.Code(typeName), Format.Bold(group.ListName)).ConfigureAwait(false);
                    	return;
					}
					else{
						await ReplyErrorLocalized("gwl_clear_fail", Format.Code(typeName), Format.Bold(group.ListName)).ConfigureAwait(false);
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

			#endregion Clear

			#region Purge
			
			#region Purge FieldType

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GWLPurge(GlobalWhitelistService.FieldType field, ulong id) 
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _Purge(GlobalWhitelistType.Server, id);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _Purge(GlobalWhitelistType.Channel, id);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _Purge(GlobalWhitelistType.User, id);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_member", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GWLPurge(GlobalWhitelistService.FieldType field, string name) 
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.COMMAND: 
						await _Purge(UnblockedType.Command, name);
						return;
					case GlobalWhitelistService.FieldType.MODULE: 
						await _Purge(UnblockedType.Module, name);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_unblock", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			#endregion Purge FieldType

			#region Purge Members

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLPurge(IGuild server)
				=> _Purge(GlobalWhitelistType.Server, server.Id);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLPurge(ITextChannel channel)
				=> _Purge(GlobalWhitelistType.Server, channel.Id);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLPurge(IUser user)
				=> _Purge(GlobalWhitelistType.Server, user.Id);

			#endregion Purge Members

			#region Purge Unblock

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLPurge(CommandOrCrInfo cmd)
				=> _Purge(UnblockedType.Command, cmd.Name.ToLowerInvariant());

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLPurge(ModuleOrCrInfo mdl)
				=> _Purge(UnblockedType.Module, mdl.Name.ToLowerInvariant());

			#endregion PurgeUnblock

			private async Task _Purge(GlobalWhitelistType type, ulong id) 
			{
				// Try to remove from all whitelists
                if (_service.PurgeMember(type, id))
                {
                    await ReplyConfirmLocalized("gwl_purge_success", 
						Format.Code(type.ToString()), 
						_service.GetNameOrMentionFromId(type, id))
						.ConfigureAwait(false);
                    return;
                }
				else {
					await ReplyErrorLocalized("gwl_purge_fail", 
						Format.Code(type.ToString()), 
						_service.GetNameOrMentionFromId(type, id))
						.ConfigureAwait(false);
                    return;
				}
			}
			
			private async Task _Purge(UnblockedType type, string name)
			{
				// Try to remove from GlobalPermissionService
				bool removedFromHashset;
				if (type == UnblockedType.Command)
				{
					removedFromHashset = _perm.UnblockedCommands.TryRemove(name);
				}
				else
				{
					removedFromHashset = _perm.UnblockedModules.TryRemove(name);
				}

				// Try to remove from all whitelists
                if (removedFromHashset)
                {
                    _service.PurgeUnblocked(type, name);
                    await ReplyConfirmLocalized("gwl_purge_success", 
						Format.Code(type.ToString()), 
						Format.Bold(name))
						.ConfigureAwait(false);
                    return;
                }
				else {
					await ReplyErrorLocalized("gwl_purge_fail", 
						Format.Code(type.ToString()), 
						Format.Bold(name))
						.ConfigureAwait(false);
                    return;
				}
			}

			#endregion Purge

			#region Whitelist Queries

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ListGWL(int page=1)
            {
                if(--page < 0) return; // ensures page is 0-indexed and non-negative

                if (_service.GetAllNames(page, out string[] names, out int count)) {
					int lastPage = (count - 1)/_service.numPerPage;
					if (page > lastPage) page = lastPage;
                    var embed = new EmbedBuilder()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gwl_list"))
						.AddField(GetText("gwl_field_title", count), string.Join("\n", names))
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

			#region Info

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
							.AddField(GetText("gwl_field_commands", cmdCount), strCmd, true)
							.AddField(GetText("gwl_field_modules", mdlCount), strMdl, true)
							.AddField("gwl_field_status", statusText, true)
							.AddField(GetText("gwl_field_users", userCount), userStr, true)
							.AddField(GetText("gwl_field_channels", channelCount), channelStr, true)
							.AddField(GetText("gwl_field_servers", serverCount), serverStr, true)
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
						fieldTitle = "gwl_field_commands";
						fieldLabel = "Commands";
					break;

					case GlobalWhitelistService.FieldType.MODULE:
						bool hasMdls = _service.GetGroupUnblockedNames(group, UnblockedType.Module, page, out string[] mdls, out fieldCount);
						fieldStr = (hasMdls) ? string.Join("\n", mdls) : "*no such commands*";
						fieldTitle = "gwl_field_modules";
						fieldLabel = "Modules";
					break;

					case GlobalWhitelistService.FieldType.SERVER:
						bool hasServers = _service.GetGroupMembers(group, GlobalWhitelistType.Server, page, out ulong[] servers, out fieldCount);
						fieldStr = (!hasServers) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.Server, servers));
						fieldTitle = "gwl_field_servers";
						fieldLabel = "Servers";
					break;

					case GlobalWhitelistService.FieldType.CHANNEL:
						bool hasChannels = _service.GetGroupMembers(group, GlobalWhitelistType.Channel, page, out ulong[] channels, out fieldCount);
						fieldStr = (!hasChannels) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.Channel, channels));
						fieldTitle = "gwl_field_channels";
						fieldLabel = "Channels";
					break;

					case GlobalWhitelistService.FieldType.USER:
						bool hasUsers = _service.GetGroupMembers(group, GlobalWhitelistType.User, page, out ulong[] users, out fieldCount);
						fieldStr = (!hasUsers) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GlobalWhitelistType.User, users));
						fieldTitle = "gwl_field_users";
						fieldLabel = "Users";
					break;

					default:
						fieldStr = "*none*";
						fieldTitle = "gwl_field_unknown";
						fieldLabel = "Unknown";
					break;
				}
				
				int lastPage = (fieldCount - 1)/_service.numPerPage;
				if (page > lastPage) page = lastPage;

				// Alter the object stored in memory, pointed to by the provided embed argument
				embed.WithDescription(GetText("gwl_info_field", Format.Code(fieldLabel), Format.Bold(group.ListName)))
					.AddField(GetText(fieldTitle, fieldCount), fieldStr, false)
					.WithFooter($"Page {page+1}/{lastPage+1}");
			}

			#endregion Info

			#region Check If Has

			#region Has FieldType

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GWLHasMember(GlobalWhitelistService.FieldType field, ulong id, string listName="")
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _HasMember(GlobalWhitelistType.Server, id, listName);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _HasMember(GlobalWhitelistType.Channel, id, listName);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _HasMember(GlobalWhitelistType.User, id, listName);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_member", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GWLHasMember(GlobalWhitelistService.FieldType field, string name, string listName="")
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.COMMAND: 
						await _HasMember(UnblockedType.Command, name, listName);
						return;
					case GlobalWhitelistService.FieldType.MODULE: 
						await _HasMember(UnblockedType.Module, name, listName);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_unblock", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			#endregion Has FieldType

			#region Has Member

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLHasMember(IGuild server, string listName="")
				=> _HasMember(GlobalWhitelistType.Server, server.Id, listName);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLHasMember(ITextChannel channel, string listName="")
				=> _HasMember(GlobalWhitelistType.Channel, channel.Id, listName);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLHasMember(IUser user, string listName="")
				=> _HasMember(GlobalWhitelistType.User, user.Id, listName);

			#endregion Has Member

			#region Has UB

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLHasMember(CommandOrCrInfo cmd, string listName="")
				=> _HasMember(UnblockedType.Command, cmd.Name.ToLowerInvariant(), listName);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLHasMember(ModuleOrCrInfo mdl, string listName="")
				=> _HasMember(UnblockedType.Module, mdl.Name.ToLowerInvariant(), listName);

			#endregion Has UB

			private async Task _HasMember(GlobalWhitelistType type, ulong id, string listName)
			{
				// Return error if whitelist doesn't exist
                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
                    // Return result of IsMemberInList()
                    if(!_service.IsMemberInGroup(id,type,group)) {
						await ReplyErrorLocalized("gwl_not_member", 
							Format.Code(type.ToString()), 
							_service.GetNameOrMentionFromId(type,id), 
							Format.Bold(group.ListName))
							.ConfigureAwait(false);
                        return;
                    } else {
                        await ReplyConfirmLocalized("gwl_is_member", 
							Format.Code(type.ToString()), 
							_service.GetNameOrMentionFromId(type,id), 
							Format.Bold(group.ListName))
							.ConfigureAwait(false);
                        return;
                    }
                } else {
					await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);    
                    return;
				}
			}

			private async Task _HasMember(UnblockedType type, string name, string listName)
			{
				// Return error if whitelist doesn't exist
                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GlobalWhitelistSet group))
                {
                    // Return result of IsMemberInList()
                    if(!_service.IsUnblockedInGroup(name,type,group)) {
						await ReplyErrorLocalized("gwl_not_member", 
							Format.Code(type.ToString()), 
							name, 
							Format.Bold(group.ListName))
							.ConfigureAwait(false);
                        return;
                    } else {
                        await ReplyConfirmLocalized("gwl_is_member", 
							Format.Code(type.ToString()), 
							name, 
							Format.Bold(group.ListName))
							.ConfigureAwait(false);
                        return;
                    }
                } else {
					await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);    
                    return;
				}
			}

			#endregion Check If Has

			#region ListGWLFor

			#region ListGWLFor FieldType

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GWLForMember(GlobalWhitelistService.FieldType field, ulong id, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _ListForMember(GlobalWhitelistType.Server, id, page);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _ListForMember(GlobalWhitelistType.Channel, id, page);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _ListForMember(GlobalWhitelistType.User, id, page);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_member", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GWLForMember(GlobalWhitelistService.FieldType field, string name, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.COMMAND: 
						await _ListForMember(UnblockedType.Command, name, page);
						return;
					case GlobalWhitelistService.FieldType.MODULE: 
						await _ListForMember(UnblockedType.Module, name, page);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_unblock", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			#endregion ListGWLFor FieldType

			#region ListGWLFor Member

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLForMember(IGuild server, int page=1)
				=> _ListForMember(GlobalWhitelistType.Server, server.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLForMember(ITextChannel channel, int page=1)
				=> _ListForMember(GlobalWhitelistType.Channel, channel.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLForMember(IUser user, int page=1)
				=> _ListForMember(GlobalWhitelistType.User, user.Id, page);

			#endregion ListGWLFor Member

			#region ListGWLFor Unblock

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLForMember(CommandOrCrInfo cmd, int page=1)
				=> _ListForMember(UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLForMember(ModuleOrCrInfo mdl, int page=1)
				=> _ListForMember(UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			#endregion ListGWLFor Unblock

            private async Task _ListForMember(GlobalWhitelistType type, ulong id, int page)
            {
                if(--page < 0) return;
                
                if (_service.GetGroupNamesByMember(id, type, page, out string[] names, out int count)) {
					int lastPage = (count - 1)/_service.numPerPage;
					if (page > lastPage) page = lastPage;
                    EmbedBuilder embed = new EmbedBuilder()
                      .WithTitle(GetText("gwl_title"))
                      .WithDescription(GetText("gwl_list_bymember", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id)))
                      .AddField(GetText("gwl_field_title", count), string.Join("\n", names))
                      .WithFooter($"Page {page+1}/{lastPage+1}")
                      .WithOkColor();
                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    return;
                } else {
					await ReplyErrorLocalized("gwl_empty_member", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id)).ConfigureAwait(false);
                    return;
                }
            }

			private async Task _ListForMember(UnblockedType type, string name, int page)
            {
                if(--page < 0) return; // ensures page is 0-indexed and non-negative

				if (_service.GetGroupNamesByUnblocked(name,type,page, out string[] names, out int count)) {
					int lastPage = (count - 1)/_service.numPerPage;
					if (page > lastPage) page = lastPage;
					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gwl_list_bymember", Format.Code(type.ToString()), Format.Bold(name)))
						.AddField(GetText("gwl_field_title", count), string.Join("\n", names), true)
						.WithFooter($"Page {page+1}/{lastPage+1}");

					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
					return;
				}
				else {
					await ReplyErrorLocalized("gwl_empty_member", Format.Code(type.ToString()), Format.Bold(name)).ConfigureAwait(false);
                    return;
				}
            }

			#endregion ListGWLFor

			#endregion Whitelist Queries

        }
    }
}
