using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NadekoBot.Common.Collections;
using NadekoBot.Common.ModuleBehaviors;
using NadekoBot.Core.Services;
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

        public GlobalPermissionService(IBotConfigProvider bc, GlobalWhitelistService gwl)
        {
            BlockedModules = new ConcurrentHashSet<string>(bc.BotConfig.BlockedModules.Select(x => x.Name));
            BlockedCommands = new ConcurrentHashSet<string>(bc.BotConfig.BlockedCommands.Select(x => x.Name));
            UnblockedModules = new ConcurrentHashSet<string>(bc.BotConfig.UnblockedModules.Select(x => x.Name));
            UnblockedCommands = new ConcurrentHashSet<string>(bc.BotConfig.UnblockedCommands.Select(x => x.Name));
            _gwl = gwl;
        }

        public async Task<bool> TryBlockLate(DiscordSocketClient client, IUserMessage msg, IGuild guild, IMessageChannel channel, IUser user, string moduleName, string commandName)
        {
            await Task.Yield();
            commandName = commandName.ToLowerInvariant();

            if (commandName != "resetglobalperms" &&
                (BlockedCommands.Contains(commandName) ||
                BlockedModules.Contains(moduleName.ToLowerInvariant())))
            {
                // System.Console.WriteLine("FOUND A BLOCKED CMDorMDL");

                // Check for unblocked command/module
                if (UnblockedCommands.Contains(commandName))
                {
                    // System.Console.WriteLine("Detected unblocked cmd {0}", commandName);
					// Return false if command is unblocked for user OR channel OR server
                    return !(_gwl.CheckIfUnblocked(commandName, UnblockedType.Command, 
								user.Id, GlobalWhitelistType.User)
							|| _gwl.CheckIfUnblocked(commandName, UnblockedType.Command, 
								channel.Id, GlobalWhitelistType.Channel)
							|| _gwl.CheckIfUnblocked(commandName, UnblockedType.Command, 
								guild.Id, GlobalWhitelistType.Server)); 
                }
                else if (UnblockedModules.Contains(moduleName.ToLowerInvariant())) 
                {
                    // System.Console.WriteLine("Detected unblocked mdl {0}", moduleName);
                    string modName = moduleName.ToLowerInvariant();
					// Return false if module is unblocked for user OR channel OR server
					return !(_gwl.CheckIfUnblocked(modName, UnblockedType.Module, 
								user.Id, GlobalWhitelistType.User)
							|| _gwl.CheckIfUnblocked(modName, UnblockedType.Module, 
								channel.Id, GlobalWhitelistType.Channel)
							|| _gwl.CheckIfUnblocked(modName, UnblockedType.Module, 
								guild.Id, GlobalWhitelistType.Server)); 
                }
                else { return true; }
                //return true;
                //return new ExecuteCommandResult(cmd, null, SearchResult.FromError(CommandError.Exception, $"Command or module is blocked globally by the bot owner."));
            }
            return false;
        }
    }
}
