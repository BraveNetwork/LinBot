using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
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
			private CommandService _cmds;
			public const int MaxNumInput = 30;
			public const int MaxNameLength = 20;

            public GlobalWhitelistCommands(GlobalPermissionService perm, CommandService cmds)
            {
				_perm = perm;
				_cmds = cmds;
            }

			#region Type Compatibility
			private bool IsCompatible(GWLType type, GlobalWhitelistService.FieldType field)
			{
				int val = (int)field;

				bool isCmdOrMdl = field.Equals(GlobalWhitelistService.FieldType.COMMAND) 
					|| field.Equals(GlobalWhitelistService.FieldType.MODULE);

				switch(type) {
					case GWLType.All:
						return isCmdOrMdl;

					case GWLType.Member:
						return isCmdOrMdl || (val >= (int)GlobalWhitelistService.FieldType.SERVER 
							&& val <= (int)GlobalWhitelistService.FieldType.USER);

					case GWLType.Role:
						return isCmdOrMdl || val == (int)GlobalWhitelistService.FieldType.ROLE;

					default:
						return false;
				}
			}

			private bool IsCompatible(GWLType type, GWLItemType item)
			{
				bool isRole = item.Equals(GWLItemType.Role);
				return (type.Equals(GWLType.Member) && !isRole) 
					|| (type.Equals(GWLType.Role) && isRole);
			}

			private bool IsCompatible(GWLType type, UnblockedType ub)
			{
				return true;
			}
			#endregion Type Compatibility

			#region Whitelist Utilities

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GWLCreate(string listName="", GlobalWhitelistService.FieldType field=0)
            {
                if (string.IsNullOrWhiteSpace(listName) || listName.Length > MaxNameLength) {
					await ReplyErrorLocalized("gwl_name_error", Format.Bold(listName), MaxNameLength).ConfigureAwait(false);
                	return;
				}

				// Ensure a similar name doesnt already exist
				bool exists = _service.GetGroupByName(listName.ToLowerInvariant(), out GWLSet group);

				// Get the type
				GWLType type; 
				switch(field) {
					case GlobalWhitelistService.FieldType.ALL:
						type = GWLType.All;
						break;
					case GlobalWhitelistService.FieldType.MEMBER:
						type = GWLType.Member;
						break;
					case GlobalWhitelistService.FieldType.ROLE:
						type = GWLType.Role;
						break;
					default:
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_gwltype", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}

                if (exists) {
					await ReplyErrorLocalized("gwl_create_dupe", Format.Bold(listName), Format.Code(type.ToString()), Format.Bold(group.ListName), Format.Code(group.Type.ToString())).ConfigureAwait(false);
                	return;
				}
				// Create new list
				if (_service.CreateWhitelist(listName, type))
                {
                    await ReplyConfirmLocalized("gwl_create_success", Format.Bold(listName), Format.Code(type.ToString())).ConfigureAwait(false);
                	return;
                }
				// Failure
				await ReplyErrorLocalized("gwl_create_fail", Format.Bold(listName), Format.Code(type.ToString())).ConfigureAwait(false);
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
					if (_service.GetGroupByName(newNameI, out GWLSet groupExists)) {
						await ReplyErrorLocalized("gwl_rename_dupe", Format.Bold(listName), Format.Bold(newName), Format.Bold(groupExists.ListName)).ConfigureAwait(false);
						return;
					}
				}

				// Create the new list if oldName is valid
				if (_service.GetGroupByName(listNameI, out GWLSet group)) {
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
				if (_service.GetGroupByName(listNameI, out GWLSet group)) {
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
				if (_service.GetGroupByName(listNameI, out GWLSet group)) {
					string statusTxt = (group.IsEnabled) ? GetText("gwl_status_enabled_emoji") : GetText("gwl_status_disabled_emoji");
					await ReplyConfirmLocalized("gwl_status", Format.Bold(group.ListName), Format.Code(statusTxt), Format.Code(group.Type.ToString())).ConfigureAwait(false);
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
			public async Task GWLAddRemove(AddRemove action, string listName="", GlobalWhitelistService.FieldType field=0, params ulong[] ids)
			{
				// If params is empty, report error
				if (ids.Length < 1) {
					await ReplyErrorLocalized("gwl_missing_params", Format.Code(field.ToString())).ConfigureAwait(false);
                    return;
				}
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _AddRemove(action, GWLItemType.Server, listName, ids);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _AddRemove(action, GWLItemType.Channel, listName, ids);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _AddRemove(action, GWLItemType.User, listName, ids);
						return;
					case GlobalWhitelistService.FieldType.ROLE: 
						await _AddRemoveRoles(action, listName, ids);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_member", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public async Task GWLAddRemove(AddRemove action, string listName="", GlobalWhitelistService.FieldType field=0, params string[] names)
			{
				// If params is empty, report error
				if (names.Length < 1) {
					await ReplyErrorLocalized("gwl_missing_params", Format.Code(field.ToString())).ConfigureAwait(false);
                    return;
				}
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

			#region Add/Rem Unblocked

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public async Task GWLAddRemove(AddRemove action, string listName="", params CommandInfo[] cmds)
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
			public async Task GWLAddRemove(AddRemove action, string listName="", params ModuleInfo[] mdls)
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
				await _AddRemove(action, GWLItemType.Server, listName, ids);
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
				await _AddRemove(action, GWLItemType.Channel, listName, ids);
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
				await _AddRemove(action, GWLItemType.User, listName, ids);
				return;
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
			public async Task GWLAddRemove(AddRemove action, string listName="", params IRole[] roles)
			{
				if (roles.Length > MaxNumInput) {
					await ReplyErrorLocalized("gwl_toomany_params", Format.Code("Role"), MaxNumInput).ConfigureAwait(false);
                    return;
				}

				// Ensure that the current serverID is included
				ulong[] ids = new ulong[roles.Length+1];
				ids[0] = Context.Guild.Id;
				for (int i=0; i<roles.Length; i++) {
					ids[i+1] = roles[i].Id;
				}

				await _AddRemoveRoles(action, listName, ids);
				return;
			}

			#endregion Add/Rem Members

			private async Task _AddRemove(AddRemove action, GWLItemType type, string listName="", params ulong[] ids)
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
                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GWLSet group))
                {
					// Ensure the group type is compatible!
					if (!IsCompatible(group.Type, type)) {
						await ReplyErrorLocalized("gwl_incompat_type", Format.Code(type.ToString()), Format.Bold(group.ListName), Format.Code(group.Type.ToString())).ConfigureAwait(false);
                    	return;
					}
					// Get string list of name/mention from ids
					string idList = string.Join("\n",_service.GetNameOrMentionFromId(type,ids));

					// Process Add ID to Whitelist of ListName
					if (action == AddRemove.Add) 
					{
						if(_service.AddMemberToGroup(ids,type,group, out ulong[] successList))
						{
							string strList = string.Join("\n",_service.GetNameOrMentionFromId(type,successList));
							await ReplyConfirmLocalized("gwl_add_success",
								successList.Count(), ids.Count(),
								Format.Code(type.ToString()),
								Format.Bold(group.ListName),
								strList)
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_add_fail",
								successList.Count(), ids.Count(),
								Format.Code(type.ToString()), 
								Format.Bold(group.ListName),
								idList)
								.ConfigureAwait(false);
							return;
						}
					}
					// Process Remove ID from Whitelist of ListName
					else
					{
						if(_service.RemoveMemberFromGroup(ids,type,group, out ulong[] successList))
						{
							string strList = string.Join("\n",_service.GetNameOrMentionFromId(type,successList));
							await ReplyConfirmLocalized("gwl_remove_success", 
								successList.Count(), ids.Count(),
								Format.Code(type.ToString()),
								Format.Bold(group.ListName),
								strList)
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_remove_fail", 
								successList.Count(), ids.Count(),
								Format.Code(type.ToString()),
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

			private async Task _AddRemoveRoles(AddRemove action, string listName="", params ulong[] ids)
			{
				GWLItemType type = GWLItemType.Role;

				// If params is empty, report error
				if (ids.Length < 1) {
					await ReplyErrorLocalized("gwl_missing_params", Format.Code(type.ToString())).ConfigureAwait(false);
                    return;
				}

				// Get the server ID
				var serverID = ids.FirstOrDefault();
				ids = ids.Skip(1).ToArray();

				// If params is too long, report error
				if (ids.Length > MaxNumInput) {
					await ReplyErrorLocalized("gwl_toomany_params", Format.Code(type.ToString()), MaxNumInput).ConfigureAwait(false);
                    return;
				}
				// If the listName doesn't exist, return an error message
                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GWLSet group))
                {
					// Ensure the group type is compatible!
					if (!group.Type.Equals(GWLType.Role)) {
						await ReplyErrorLocalized("gwl_incompat_type", Format.Code(type.ToString()), Format.Bold(group.ListName), Format.Code(group.Type.ToString())).ConfigureAwait(false);
                    	return;
					}
					// Get string list of name/mention from ids
					string idList = string.Join("\n",_service.GetNameOrMentionFromId(type,ids));

					// Process Add ID to Whitelist of ListName
					if (action == AddRemove.Add) 
					{
						if(_service.AddRoleToGroup(serverID, ids, group, out ulong[] successList))
						{
							string strList = string.Join("\n",_service.GetNameOrMentionFromId(type,successList));
							await ReplyConfirmLocalized("gwl_add_role_success",
								successList.Count(), ids.Count(),
								Format.Bold(group.ListName),
								_service.GetNameOrMentionFromId(GWLItemType.Server, serverID),
								strList)
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_add_role_fail",
								successList.Count(), ids.Count(),
								Format.Bold(group.ListName),
								_service.GetNameOrMentionFromId(GWLItemType.Server, serverID),
								idList)
								.ConfigureAwait(false);
							return;
						}
					}
					// Process Remove ID from Whitelist of ListName
					else
					{
						if(_service.RemoveRoleFromGroup(serverID, ids, group, out ulong[] successList))
						{
							string strList = string.Join("\n",_service.GetNameOrMentionFromId(type,successList));
							await ReplyConfirmLocalized("gwl_remove_role_success", 
								successList.Count(), ids.Count(),
								Format.Bold(group.ListName),
								_service.GetNameOrMentionFromId(GWLItemType.Server, serverID),
								strList)
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_remove_role_fail", 
								successList.Count(), ids.Count(),
								Format.Bold(group.ListName),
								_service.GetNameOrMentionFromId(GWLItemType.Server, serverID),
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

				// Verify type
				string[] truenames = new string[names.Length];
				if (type.Equals(UnblockedType.Command)) {
					for (int i=0; i<names.Length; i++) {
						string name = names[i].ToLowerInvariant();
						if (names[i].StartsWith(Prefix)) name = name.Substring(Prefix.Length);
						var cmd = _cmds.Commands.FirstOrDefault(c => 
							c.Aliases.Select(a => a.ToLowerInvariant()).Contains(name));
						if (cmd != null) truenames[i] = cmd.Name.ToLowerInvariant();
					}
				} else {
					for (int i=0; i<names.Length; i++) {
						string name = names[i].ToLowerInvariant();
						var mdl = _cmds.Modules.GroupBy(m => m.GetTopLevelModule()).FirstOrDefault(m => m.Key.Name.ToLowerInvariant() == name)?.Key;
						if (mdl != null) truenames[i] = mdl.Name.ToLowerInvariant();
					}
				}

				// Remove empty in truenames (faster than remaking truenames each iteration)
				truenames = truenames.Where(x=>!string.IsNullOrEmpty(x)).ToArray();

				if (truenames.Length < 1) {
					await ReplyErrorLocalized("gwl_missing_params", Format.Code(type.ToString())).ConfigureAwait(false);
                    return;
				}				

				// If the listName doesn't exist, return an error message
                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GWLSet group))
                {
					// Ensure the group type is compatible!
					if (!IsCompatible(group.Type, type)) {
						await ReplyErrorLocalized("gwl_incompat_type", Format.Code(type.ToString()), Format.Bold(group.ListName), Format.Code(group.Type.ToString())).ConfigureAwait(false);
                    	return;
					}

                    // Get itemlist string
					string itemList = string.Join("\n",truenames);

					// Process Add Command/Module
					if (action == AddRemove.Add) 
					{   
						// Keep track of internal changes
						int delta = 0;

						// Add to hashset in GlobalPermissionService
						if (type == UnblockedType.Command)
						{
							int pre = _perm.UnblockedCommands.Count;
							_perm.UnblockedCommands.AddRange(truenames);
							delta = _perm.UnblockedCommands.Count - pre;
						}
						else {
							int pre = _perm.UnblockedModules.Count;
							_perm.UnblockedModules.AddRange(truenames);
							delta = _perm.UnblockedModules.Count - pre;
						}

						System.Console.WriteLine("Added {0} items to GlobalPermissionService Unblocked HashSet", delta);

						// Add to a whitelist
						if(_service.AddUnblockedToGroup(truenames,type,group, out string[] successList))
						{
							await ReplyConfirmLocalized("gwl_add_success",
								successList.Count(), truenames.Count(),
								Format.Code(type.ToString()+"s"), 
								Format.Bold(group.ListName), 
								Format.Bold(string.Join("\n", successList)))
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_add_fail",
								successList.Count(), truenames.Count(),
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
						if(_service.RemoveUnblockedFromGroup(truenames,type,group, out string[] successList))
						{
							await ReplyConfirmLocalized("gwl_remove_success",
								successList.Count(), truenames.Count(),
								Format.Code(type.ToString()+"s"),
								Format.Bold(group.ListName),
								Format.Bold(string.Join("\n", successList)))
								.ConfigureAwait(false);
							return;
						}
						else {
							await ReplyErrorLocalized("gwl_remove_fail",
								successList.Count(), truenames.Count(),
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
				if (_service.GetGroupByName(listName.ToLowerInvariant(), out GWLSet group)) 
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
							result = _service.ClearMembers(group, GWLItemType.Server);
							typeName = "Server";
							break;

						case GlobalWhitelistService.FieldType.CHANNEL:
							result = _service.ClearMembers(group, GWLItemType.Channel);
							typeName = "Channel";
							break;

						case GlobalWhitelistService.FieldType.USER:
							result = _service.ClearMembers(group, GWLItemType.User);
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
						await _Purge(GWLItemType.Server, id);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _Purge(GWLItemType.Channel, id);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _Purge(GWLItemType.User, id);
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
				=> _Purge(GWLItemType.Server, server.Id);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLPurge(ITextChannel channel)
				=> _Purge(GWLItemType.Server, channel.Id);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLPurge(IUser user)
				=> _Purge(GWLItemType.Server, user.Id);

			#endregion Purge Members

			#region Purge Unblock

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLPurge(CommandInfo cmd)
				=> _Purge(UnblockedType.Command, cmd.Name.ToLowerInvariant());

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLPurge(ModuleInfo mdl)
				=> _Purge(UnblockedType.Module, mdl.Name.ToLowerInvariant());

			#endregion PurgeUnblock

			private async Task _Purge(GWLItemType type, ulong id) 
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

				bool hasTypeAll = _service.GetGroupNames(GWLType.All, page, out string[] namesA, out int countA);
				bool hasTypeMem = _service.GetGroupNames(GWLType.Member, page, out string[] namesM, out int countM);
				bool hasTypeRole = _service.GetGroupNames(GWLType.Role, page, out string[] namesR, out int countR);

				string strA = (hasTypeAll) ? string.Join("\n", namesA) : "*none*";
				string strM = (hasTypeMem) ? string.Join("\n", namesM) : "*none*";
				string strR = (hasTypeRole) ? string.Join("\n", namesR) : "*none*";

                if (hasTypeAll || hasTypeMem || hasTypeRole) {
					int lastAPage = (countA - 1)/_service.numPerPage;
					int lastMPage = (countM - 1)/_service.numPerPage;
					int lastRPage = (countR - 1)/_service.numPerPage;
					int lastPage = System.Math.Max(lastAPage, System.Math.Max(lastMPage,lastRPage));
					if (page > lastPage) page = lastPage;
                    var embed = new EmbedBuilder()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gwl_list"))
						.AddField(GetText("gwl_field_title_all", countA), strA)
						.AddField(GetText("gwl_field_title_mem", countM), strM)
						.AddField(GetText("gwl_field_title_role", countR), strR)
						.WithFooter($"Page {page+1}/{lastPage+1}")
						.WithOkColor();
                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);

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
            public Task GWLInfo(string listName="", GlobalWhitelistService.FieldType field=0, int page=1)
				=> _Info(field, listName, page);

			private async Task _Info(GlobalWhitelistService.FieldType field, string listName, int page)
			{
				if(--page < 0) return; // ensures page is 0-indexed and non-negative

                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GWLSet group))
                {
					// Ensure the group type is compatible!
					if (!field.Equals(GlobalWhitelistService.FieldType.ALL) 
						&& !IsCompatible(group.Type, field)) {
						await ReplyErrorLocalized("gwl_incompat_type", Format.Code(field.ToString()), Format.Bold(group.ListName), Format.Code(group.Type.ToString())).ConfigureAwait(false);
                    	return;
					}

					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"));
					
					if (!field.Equals(GlobalWhitelistService.FieldType.ALL)) {
						// This alters embed to have the data we need for the desired fieldType
						getFieldInfo(embed, group, field, page);
					}
					else {

						// Get modules/commands
						int numCmd = getPartialFieldInfo(embed, group, GlobalWhitelistService.FieldType.COMMAND, page);
						int numMdl = getPartialFieldInfo(embed, group, GlobalWhitelistService.FieldType.MODULE, page);

						int lastCmdPage = (numCmd - 1)/_service.numPerPage +1;
						int lastMdlPage = (numMdl - 1)/_service.numPerPage +1;

						int lastPage = System.Math.Max(lastCmdPage,lastMdlPage);

						// Some important embed info
						string statusText = (group.IsEnabled) ? GetText("gwl_status_enabled_emoji") :  GetText("gwl_status_disabled_emoji");
						embed.WithDescription(GetText("gwl_info", Format.Bold(group.ListName), Format.Code(group.Type.ToString())))
							.AddField(GetText("gwl_field_status"), statusText, true);

						switch (group.Type) {
							case GWLType.Member:
								// Get Member lists
								int numU = getPartialFieldInfo(embed, group, GlobalWhitelistService.FieldType.USER, page);
								int numC = getPartialFieldInfo(embed, group, GlobalWhitelistService.FieldType.CHANNEL, page);
								int numS = getPartialFieldInfo(embed, group, GlobalWhitelistService.FieldType.SERVER, page);
								// Do Page Calcs
								int lastUPage = (numU - 1)/_service.numPerPage +1;
								int lastCPage = (numC - 1)/_service.numPerPage +1;
								int lastSPage = (numS - 1)/_service.numPerPage +1;
								lastPage = System.Math.Max(lastPage,
									System.Math.Max(lastUPage,
									System.Math.Max(lastCPage,lastSPage)));
								break;
							case GWLType.Role:
								// Get Role lists
								int numRole = getPartialFieldInfo(embed, group, GlobalWhitelistService.FieldType.ROLE, page);
								// Do Page Calcs
								int lastRolePage = (numRole - 1)/_service.numPerPage +1;
								lastPage = System.Math.Max(lastPage,lastRolePage);
								break;
							case GWLType.All:
							default:
								break;
						}
						// Paginated Embed
						page++;
						if (page > lastPage) page = lastPage;						
						embed.WithFooter($"Page {page}/{lastPage}");
					}

					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    
                } else {
                    await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);    
                    return;
                }
			}

			// This alters embed to have the data we need for the desired fieldType
			private void getFieldInfo(EmbedBuilder embed, GWLSet group, GlobalWhitelistService.FieldType field, int page)
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
						bool hasServers = _service.GetGroupMembers(group, GWLItemType.Server, page, out ulong[] servers, out fieldCount);
						fieldStr = (!hasServers) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GWLItemType.Server, servers));
						fieldTitle = "gwl_field_servers";
						fieldLabel = "Servers";
					break;

					case GlobalWhitelistService.FieldType.CHANNEL:
						bool hasChannels = _service.GetGroupMembers(group, GWLItemType.Channel, page, out ulong[] channels, out fieldCount);
						fieldStr = (!hasChannels) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GWLItemType.Channel, channels));
						fieldTitle = "gwl_field_channels";
						fieldLabel = "Channels";
					break;

					case GlobalWhitelistService.FieldType.USER:
						bool hasUsers = _service.GetGroupMembers(group, GWLItemType.User, page, out ulong[] users, out fieldCount);
						fieldStr = (!hasUsers) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GWLItemType.User, users));
						fieldTitle = "gwl_field_users";
						fieldLabel = "Users";
					break;

					case GlobalWhitelistService.FieldType.ROLE:
						bool hasRoles = _service.GetGroupMembers(group, GWLItemType.Role, page, out ulong[] roles, out fieldCount);
						fieldStr = (!hasRoles) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GWLItemType.Role, roles));
						fieldTitle = "gwl_field_roles";
						fieldLabel = "Roles";
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

			private int getPartialFieldInfo(EmbedBuilder embed, GWLSet group, GlobalWhitelistService.FieldType field, int page)
			{
				string fieldTitle = "";
				string fieldStr = "";
				int fieldCount = 0;

				switch(field) {
					case GlobalWhitelistService.FieldType.COMMAND:
						bool hasCmds = _service.GetGroupUnblockedNames(group, UnblockedType.Command, page, out string[] cmds, out fieldCount);
						fieldStr = (hasCmds) ? string.Join("\n", cmds) : "*no such commands*";
						fieldTitle = "gwl_field_commands";
					break;

					case GlobalWhitelistService.FieldType.MODULE:
						bool hasMdls = _service.GetGroupUnblockedNames(group, UnblockedType.Module, page, out string[] mdls, out fieldCount);
						fieldStr = (hasMdls) ? string.Join("\n", mdls) : "*no such commands*";
						fieldTitle = "gwl_field_modules";
					break;

					case GlobalWhitelistService.FieldType.SERVER:
						bool hasServers = _service.GetGroupMembers(group, GWLItemType.Server, page, out ulong[] servers, out fieldCount);
						fieldStr = (!hasServers) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GWLItemType.Server, servers));
						fieldTitle = "gwl_field_servers";
					break;

					case GlobalWhitelistService.FieldType.CHANNEL:
						bool hasChannels = _service.GetGroupMembers(group, GWLItemType.Channel, page, out ulong[] channels, out fieldCount);
						fieldStr = (!hasChannels) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GWLItemType.Channel, channels));
						fieldTitle = "gwl_field_channels";
					break;

					case GlobalWhitelistService.FieldType.USER:
						bool hasUsers = _service.GetGroupMembers(group, GWLItemType.User, page, out ulong[] users, out fieldCount);
						fieldStr = (!hasUsers) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GWLItemType.User, users));
						fieldTitle = "gwl_field_users";
					break;

					case GlobalWhitelistService.FieldType.ROLE:
						bool hasRoles = _service.GetGroupMembers(group, GWLItemType.Role, page, out ulong[] roles, out fieldCount);
						fieldStr = (!hasRoles) ? "*none*" : string.Join("\n",_service.GetNameOrMentionFromId(GWLItemType.Role, roles));
						fieldTitle = "gwl_field_roles";
					break;

					default:
						fieldStr = "*none*";
						fieldTitle = "gwl_field_unknown";
					break;
				}

				// Alter the object stored in memory, pointed to by the provided embed argument
				embed.AddField(GetText(fieldTitle, fieldCount), fieldStr, true);
				return fieldCount;
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
						await _HasMember(GWLItemType.Server, id, listName);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _HasMember(GWLItemType.Channel, id, listName);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _HasMember(GWLItemType.User, id, listName);
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
				=> _HasMember(GWLItemType.Server, server.Id, listName);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLHasMember(ITextChannel channel, string listName="")
				=> _HasMember(GWLItemType.Channel, channel.Id, listName);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLHasMember(IUser user, string listName="")
				=> _HasMember(GWLItemType.User, user.Id, listName);

			#endregion Has Member

			#region Has UB

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLHasMember(CommandInfo cmd, string listName="")
				=> _HasMember(UnblockedType.Command, cmd.Name.ToLowerInvariant(), listName);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLHasMember(ModuleInfo mdl, string listName="")
				=> _HasMember(UnblockedType.Module, mdl.Name.ToLowerInvariant(), listName);

			#endregion Has UB

			private async Task _HasMember(GWLItemType type, ulong id, string listName)
			{
				// Return error if whitelist doesn't exist
                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GWLSet group))
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
                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GWLSet group))
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
            public async Task GWLForMember(GlobalWhitelistService.FieldType field, ulong sid, ulong rid, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.ROLE: 
						await _ListForMemberRole(sid, rid, page);
						return;
					default: 
						// Not valid
						await ReplyErrorLocalized("gwl_field_invalid_member", Format.Bold(field.ToString())).ConfigureAwait(false);
						return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task GWLForMember(GlobalWhitelistService.FieldType field, ulong id, int page=1)
			{
				switch(field) {
					case GlobalWhitelistService.FieldType.SERVER: 
						await _ListForMember(GWLItemType.Server, id, page);
						return;
					case GlobalWhitelistService.FieldType.CHANNEL: 
						await _ListForMember(GWLItemType.Channel, id, page);
						return;
					case GlobalWhitelistService.FieldType.USER: 
						await _ListForMember(GWLItemType.User, id, page);
						return;
					case GlobalWhitelistService.FieldType.ROLE: 
						await _ListForMemberRole(Context.Guild.Id, id, page);
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
				=> _ListForMember(GWLItemType.Server, server.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLForMember(ITextChannel channel, int page=1)
				=> _ListForMember(GWLItemType.Channel, channel.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLForMember(IUser user, int page=1)
				=> _ListForMember(GWLItemType.User, user.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLForMember(IRole role, int page=1)
				=> _ListForMemberRole(role.Guild.Id, role.Id, page);

			#endregion ListGWLFor Member

			#region ListGWLFor Unblock

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLForMember(CommandInfo cmd, int page=1)
				=> _ListForMember(UnblockedType.Command, cmd.Name.ToLowerInvariant(), page);

			[NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public Task GWLForMember(ModuleInfo mdl, int page=1)
				=> _ListForMember(UnblockedType.Module, mdl.Name.ToLowerInvariant(), page);

			#endregion ListGWLFor Unblock

            private async Task _ListForMember(GWLItemType type, ulong id, int page)
            {
                if(--page < 0) return;

				bool hasTypeAll = _service.GetGroupNames(GWLType.All, page, out string[] namesA, out int countA);
				bool hasTypeMem = _service.GetGroupNamesByMemberType(id, type, page, out string[] namesM, out int countM);
				bool hasTypeRole = false; string[] namesR = null; int countR = 0;

				// Get Role/ServerID stuff
				switch(type) {
					case GWLItemType.User:
						// Get Dictionary<ServerID,RoleID[]>
						Dictionary<ulong,ulong[]> servRoles = _service.GetRoleIDs(type, id, Context.Guild.Id);
						if (servRoles != null) hasTypeRole = _service.GetGroupNamesByMemberRoles(servRoles, page, out namesR, out countR);
						break;

					case GWLItemType.Channel:
						ulong sID =_service.GetServerID(id);
						if (sID > 0) hasTypeRole = _service.GetGroupNamesByServer(sID, page, out namesR, out countR);
						break;

					case GWLItemType.Server:
						hasTypeRole = _service.GetGroupNamesByServer(id, page, out namesR, out countR);
						break;

					default:
						break;
				}

				string strA = (hasTypeAll) ? string.Join("\n", namesA) : "*none*";
				string strM = (hasTypeMem) ? string.Join("\n", namesM) : "*none*";
				string strR = (hasTypeRole) ? string.Join("\n", namesR) : "*none*";
                
                if (hasTypeAll || hasTypeMem || hasTypeRole) {
					int lastAPage = (countA - 1)/_service.numPerPage;
					int lastMPage = (countM - 1)/_service.numPerPage;
					int lastRPage = (countR - 1)/_service.numPerPage;
					int lastPage = System.Math.Max(lastAPage, System.Math.Max(lastMPage,lastRPage));
					if (page > lastPage) page = lastPage;
                    EmbedBuilder embed = new EmbedBuilder()
                      	.WithTitle(GetText("gwl_title"))
                      	.WithDescription(GetText("gwl_list_bymember", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id)))
                      	.AddField(GetText("gwl_field_title_all", countA), strA, true)
						.AddField(GetText("gwl_field_title_mem", countM), strM, true)
						.AddField(GetText("gwl_field_title_role", countR), strR, true)
                      	.WithFooter($"Page {page+1}/{lastPage+1}")
                      	.WithOkColor();
                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    return;
                } else {
					await ReplyErrorLocalized("gwl_empty_member", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,id)).ConfigureAwait(false);
                    return;
                }
            }

			private async Task _ListForMemberRole(ulong sID, ulong rID, int page)
            {
                if(--page < 0) return;

				GWLItemType type = GWLItemType.Role;

				bool hasTypeAll = _service.GetGroupNames(GWLType.All, page, out string[] namesA, out int countA);
				bool hasTypeRole = _service.GetGroupNamesByServerRole(sID, rID, page, out string[] namesR, out int countR);

				string strA = (hasTypeAll) ? string.Join("\n", namesA) : "*none*";
				string strR = (hasTypeRole) ? string.Join("\n", namesR) : "*none*";
                
                if (hasTypeAll || hasTypeRole) {
					int lastAPage = (countA - 1)/_service.numPerPage;
					int lastRPage = (countR - 1)/_service.numPerPage;
					int lastPage = System.Math.Max(lastAPage,lastRPage);
					if (page > lastPage) page = lastPage;
                    EmbedBuilder embed = new EmbedBuilder()
                      	.WithTitle(GetText("gwl_title"))
                      	.WithDescription(GetText("gwl_list_bymember", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,rID)))
                      	.AddField(GetText("gwl_field_title_all", countA), strA, true)
						.AddField(GetText("gwl_field_title_mem", 0), "*none*", true)
						.AddField(GetText("gwl_field_title_role", countR), strR, true)
                      	.WithFooter($"Page {page+1}/{lastPage+1}")
                      	.WithOkColor();
                    await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
                    return;
                } else {
					await ReplyErrorLocalized("gwl_empty_member", Format.Code(type.ToString()), _service.GetNameOrMentionFromId(type,rID)).ConfigureAwait(false);
                    return;
                }
            }

			private async Task _ListForMember(UnblockedType type, string name, int page)
            {
                if(--page < 0) return; // ensures page is 0-indexed and non-negative

				bool hasTypeAll = _service.GetGroupNamesByUnblocked(name, type, GWLType.All, page, out string[] namesA, out int countA);
				bool hasTypeMem = _service.GetGroupNamesByUnblocked(name, type, GWLType.Member, page, out string[] namesM, out int countM);
				bool hasTypeRole = _service.GetGroupNamesByUnblocked(name, type, GWLType.Role, page, out string[] namesR, out int countR);

				string strA = (hasTypeAll) ? string.Join("\n", namesA) : "*none*";
				string strM = (hasTypeMem) ? string.Join("\n", namesM) : "*none*";
				string strR = (hasTypeRole) ? string.Join("\n", namesR) : "*none*";

				if (hasTypeAll || hasTypeMem || hasTypeRole) {
					int lastAPage = (countA - 1)/_service.numPerPage;
					int lastMPage = (countM - 1)/_service.numPerPage;
					int lastRPage = (countR - 1)/_service.numPerPage;
					int lastPage = System.Math.Max(lastAPage, System.Math.Max(lastMPage,lastRPage));
					if (page > lastPage) page = lastPage;
					var embed = new EmbedBuilder()
						.WithOkColor()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gwl_list_bymember", Format.Code(type.ToString()), Format.Bold(name)))
						.AddField(GetText("gwl_field_title_all", countA), strA, true)
						.AddField(GetText("gwl_field_title_mem", countM), strM, true)
						.AddField(GetText("gwl_field_title_role", countR), strR, true)
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

			#region User-Oriented

			[NadekoCommand, Usage, Description, Aliases]
            public Task ListMyGWL(int page=1)
            	=> _ListForMember(GWLItemType.User, Context.User.Id, page);

			[NadekoCommand, Usage, Description, Aliases]
            public async Task ListContextGWL(int page=1)
            {
				if(--page < 0) return;

				ulong idC = Context.Channel.Id;
				ulong idS = Context.Guild.Id;

				GWLItemType typeC = GWLItemType.Channel;
				GWLItemType typeS = GWLItemType.Server;

				bool hasTypeAll = _service.GetGroupNames(GWLType.All, page, out string[] namesA, out int countA);
				bool hasC = _service.GetGroupNamesByMemberType(idC, typeC, page, out string[] namesC, out int countC);
				bool hasS = _service.GetGroupNamesByMemberType(idS, typeS, page, out string[] namesS, out int countS);
				bool hasTypeRole = _service.GetGroupNamesByServer(idS, page, out string[] namesR, out int countR);

				string strA = (hasTypeAll) ? string.Join("\n", namesA) : "*none*";
				string serverStr = (hasS) ? string.Join("\n",namesS) : "*none*";
				string channelStr = (hasC) ? string.Join("\n",namesC) : "*none*";
				string strR = (hasTypeRole) ? string.Join("\n", namesR) : "*none*";
                
                if (hasTypeAll || hasS || hasC || hasTypeRole) {
					int lastAPage = (countA - 1)/_service.numPerPage;
					int lastServerPage = (countS - 1)/_service.numPerPage +1;
					int lastChannelPage = (countC - 1)/_service.numPerPage +1;
					int lastRPage = (countR - 1)/_service.numPerPage;
					int lastPage = System.Math.Max(lastAPage, 
						System.Math.Max(lastRPage,
							System.Math.Max( lastServerPage, lastChannelPage )));
					if (page > lastPage) page = lastPage;

					EmbedBuilder embed = new EmbedBuilder()
						.WithTitle(GetText("gwl_title"))
						.WithDescription(GetText("gwl_list_bymember",
							GetText("gwl_current_ctx", 
								Format.Code(typeS.ToString()),
								Context.Guild.Name,
								Format.Code(typeC.ToString()),
								MentionUtils.MentionChannel(idC)), ""
							))
						.AddField(GetText("gwl_field_title_all", countA), strA, true)
						.AddField(GetText("gwl_field_server_ctx", countS), 
							serverStr, true)
						.AddField(GetText("gwl_field_channel_ctx", countC), 
							channelStr, true)
						.AddField(GetText("gwl_field_title_role", countR), strR, true)
                      	.WithFooter($"Page {page+1}/{lastPage+1}")
                      	.WithOkColor();
					await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
					return;

				} else {
					await ReplyErrorLocalized("gwl_empty_member", 
							GetText("gwl_current_ctx", 
								Format.Code(typeS.ToString()),
								Context.Guild.Name,
								Format.Code(typeC.ToString()),
								MentionUtils.MentionChannel(idC)), ""
							)
						.ConfigureAwait(false);
                    return;
				}
			}

			[NadekoCommand, Usage, Description, Aliases]
            public Task IsMyGWL(string listName="")
				=> _HasMember(GWLItemType.User, Context.User.Id, listName);

			[NadekoCommand, Usage, Description, Aliases]
            public async Task IsContextGWL(string listName="")
            {
				// Return error if whitelist doesn't exist
                if (!string.IsNullOrWhiteSpace(listName) && _service.GetGroupByName(listName.ToLowerInvariant(), out GWLSet group))
                {
					ulong idC = Context.Channel.Id;
					ulong idS = Context.Guild.Id;

					GWLItemType typeC = GWLItemType.Channel;
					GWLItemType typeS = GWLItemType.Server;
					
                    bool hasC = _service.IsMemberInGroup(idC, typeC, group);
					bool hasS = _service.IsMemberInGroup(idS, typeS, group);

					if (hasC && hasS) {
						await ReplyConfirmLocalized("gwl_is_member", 
							GetText("gwl_current_ctx", 
								Format.Code(typeS.ToString()),
								Context.Guild.Name,
								Format.Code(typeC.ToString()),
								MentionUtils.MentionChannel(idC)), "",
							Format.Bold(group.ListName))
							.ConfigureAwait(false);
                        return;
					} else if (hasC) {
						await ReplyConfirmLocalized("gwl_is_member", 
							Format.Code(typeC.ToString()), 
							MentionUtils.MentionChannel(idC), 
							Format.Bold(group.ListName))
							.ConfigureAwait(false);
                        return;
					} else if (hasS) {
						await ReplyConfirmLocalized("gwl_is_member", 
							Format.Code(typeS.ToString()), 
							Context.Guild.Name, 
							Format.Bold(group.ListName))
							.ConfigureAwait(false);
                        return;
					} else {
						await ReplyErrorLocalized("gwl_not_member", 
							GetText("gwl_current_ctx", 
								Format.Code(typeS.ToString()),
								Context.Guild.Name,
								Format.Code(typeC.ToString()),
								MentionUtils.MentionChannel(idC)), "", 
							Format.Bold(group.ListName))
							.ConfigureAwait(false);
                        return;
					}

                } else {
					await ReplyErrorLocalized("gwl_not_exists", Format.Bold(listName)).ConfigureAwait(false);    
                    return;
				}
			}

			#endregion User-Oriented
        }
    }
}
