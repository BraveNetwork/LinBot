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
        private GlobalWhitelistService _gwl;
		private NadekoStrings _strs;

        public GlobalPermissionService(IBotConfigProvider bc, GlobalWhitelistService gwl, NadekoStrings strings)
        {
            BlockedModules = new ConcurrentHashSet<string>(bc.BotConfig.BlockedModules.Select(x => x.Name));
            BlockedCommands = new ConcurrentHashSet<string>(bc.BotConfig.BlockedCommands.Select(x => x.Name));
            UnblockedModules = new ConcurrentHashSet<string>(bc.BotConfig.UnblockedModules.Select(x => x.Name));
            UnblockedCommands = new ConcurrentHashSet<string>(bc.BotConfig.UnblockedCommands.Select(x => x.Name));
            _gwl = gwl;
			_strs = strings;
        }

        public async Task<bool> TryBlockLate(DiscordSocketClient client, IUserMessage msg, IGuild guild, IMessageChannel channel, IUser user, string moduleName, string commandName)
        {
            await Task.Yield();
            commandName = commandName.ToLowerInvariant();
			moduleName = moduleName.ToLowerInvariant();

			if (commandName != "resetglobalperms") {

				// If module is blocked, check if either module or command is unblocked
				if (BlockedModules.Contains(moduleName)) 
				{
					bool mdlIsUB = UnblockedModules.Contains(moduleName) &&
						IsUnblocked(moduleName, UnblockedType.Module, user.Id, channel.Id, guild.Id);

					if (mdlIsUB) {
						// Pass!
						return false;
					} else {
						// Check if an unblocked command overrides blocked module!
						bool cmdIsUB = UnblockedCommands.Contains(commandName) &&
							IsUnblocked(commandName, UnblockedType.Command, user.Id, channel.Id, guild.Id);

						if (cmdIsUB) {
							// Pass!
							return false;
						} else {
							// Block it!
							await ReportBlockedCmdOrMdl(channel, guild.Id, UnblockedType.Module, moduleName);
							return true;
						}
					}
				}

				// If command is blocked, but not the module
				else if (BlockedCommands.Contains(commandName))
				{
					bool cmdIsUB = UnblockedCommands.Contains(commandName) &&
						IsUnblocked(commandName, UnblockedType.Command, user.Id, channel.Id, guild.Id);
					
					if (cmdIsUB) {
						// Pass!
						return false;
					} else {
						// Check if an unblocked module overrides blocked command!
						bool mdlIsUB = UnblockedModules.Contains(moduleName) &&
							IsUnblocked(moduleName, UnblockedType.Module, user.Id, channel.Id, guild.Id);

						if (mdlIsUB) {
							// Pass!
							return false;
						} else { 
							// Block it!
							await ReportBlockedCmdOrMdl(channel, guild.Id, UnblockedType.Command, commandName);
							return true;
						}
					}
				}
				else { return false; }
                //return true;
                //return new ExecuteCommandResult(cmd, null, SearchResult.FromError(CommandError.Exception, $"Command or module is blocked globally by the bot owner."));
            }
            return false;
        }

		private bool IsUnblocked(string name, UnblockedType type, ulong uid, ulong cid, ulong gid)
		{
			return (_gwl.CheckIfUnblocked(name, type, uid, GWLItemType.User)
					|| _gwl.CheckIfUnblocked(name, type, cid, GWLItemType.Channel)
					|| _gwl.CheckIfUnblocked(name, type, gid, GWLItemType.Server));
		}

		private async Task ReportBlockedCmdOrMdl(IMessageChannel channel, ulong gid, UnblockedType type, string name)
		{
			await channel.SendErrorAsync(
				_strs.GetText("blocker_embed_title", gid, "permissions"),
				_strs.GetText("blocker_embed_desc", gid, "permissions", Format.Code(type.ToString()), Format.Bold(name))
			);
		}
    }
}
