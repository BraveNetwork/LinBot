using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NadekoBot.Extensions;
using NadekoBot.Common.Collections;
using NadekoBot.Common.ModuleBehaviors;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Impl;
using NadekoBot.Core.Services.Database.Models;

namespace NadekoBot.Modules.Permissions.Services
{
    public class GlobalPermissionService : ILateBlocker, INService
    {
        public readonly ConcurrentHashSet<string> BlockedModules;
        public readonly ConcurrentHashSet<string> BlockedCommands;

        public readonly ConcurrentHashSet<string> UnblockedModules;
        public readonly ConcurrentHashSet<string> UnblockedCommands;

		// public readonly ConcurrentHashSet<Tuple<ulong,ulong>> UnblockedRoles;
        private GlobalWhitelistService _gwl;
		private NadekoStrings _strs;

        public GlobalPermissionService(IBotConfigProvider bc, GlobalWhitelistService gwl, NadekoStrings strings)
        {
            BlockedModules = new ConcurrentHashSet<string>(bc.BotConfig.BlockedModules.Select(x => x.Name));
            BlockedCommands = new ConcurrentHashSet<string>(bc.BotConfig.BlockedCommands.Select(x => x.Name));

            UnblockedModules = new ConcurrentHashSet<string>(bc.BotConfig.UnblockedModules.Select(x => x.Name));
            UnblockedCommands = new ConcurrentHashSet<string>(bc.BotConfig.UnblockedCommands.Select(x => x.Name));

			// UnblockedRoles = new ConcurrentHashSet<Tuple<ulong,ulong>>(bc.BotConfig.UnblockedRoles
			// 	.Select( x => new Tuple<ulong,ulong>(x.RoleServerId,x.ItemId) ));

			_gwl = gwl;
			_strs = strings;
        }

        public async Task<bool> TryBlockLate(DiscordSocketClient client, IUserMessage msg, IGuild guild, IMessageChannel channel, IUser user, string moduleName, string commandName)
        {
            await Task.Yield();
            commandName = commandName.ToLowerInvariant();
			moduleName = moduleName.ToLowerInvariant();

			if (commandName != "resetglobalperms") {

				if (BlockedModules.Contains(moduleName)) 
				{
					// Check if command OR module is unblocked for context
					if (IsUnblocked(moduleName, commandName, user.Id, guild.Id, channel.Id) )
					{ return false; }

					// Block it!
					await ReportBlockedCmdOrMdl(msg, channel, guild.Id, UnblockedType.Module, moduleName);
					return true;
				}

				// If command is blocked, but not the module
				else if (BlockedCommands.Contains(commandName))
				{
					// Check if command OR module is unblocked for context
					if (IsUnblocked(moduleName, commandName, user.Id, guild.Id, channel.Id) )
					{ return false; }
					
					// Block it!
					await ReportBlockedCmdOrMdl(msg, channel, guild.Id, UnblockedType.Command, commandName);
					return true;
				}
				else { return false; }
                //return true;
                //return new ExecuteCommandResult(cmd, null, SearchResult.FromError(CommandError.Exception, $"Command or module is blocked globally by the bot owner."));
            }
            return false;
        }

		private bool IsUnblocked(string mdl, string cmd, ulong uid, ulong sid, ulong cid)
		{
			return (UnblockedModules.Contains(mdl) || UnblockedCommands.Contains(cmd)) && 
				(
				_gwl.CheckIfUnblockedAll(mdl,cmd) || 
				_gwl.CheckIfUnblocked(mdl, cmd, uid, sid, cid) ||
				_gwl.IsUserRoleUnblocked(uid, cmd, mdl)
				);
		}

		private async Task ReportBlockedCmdOrMdl(IUserMessage msg, IMessageChannel channel, ulong gid, UnblockedType type, string name)
		{
			try {
				// Report blocked cmd/mdl
				IUserMessage reportMsg = await channel.SendErrorAsync(
					_strs.GetText("blocker_embed_title", gid, "permissions"),
					_strs.GetText("blocker_embed_desc", gid, "permissions", Format.Code(type.ToString()), Format.Bold(name))
				);
				// Delete the report
				reportMsg.DeleteAfter(10);
				// Delete the blocked cmd/mdl message
				msg.DeleteAfter(10);
			}
			catch (Exception ex) { System.Console.WriteLine(ex); }
		}
    }
}
