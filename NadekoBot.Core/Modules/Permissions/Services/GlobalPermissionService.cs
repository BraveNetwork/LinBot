using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NadekoBot.Common.Collections;
using NadekoBot.Common.ModuleBehaviors;
using NadekoBot.Core.Services;

namespace NadekoBot.Modules.Permissions.Services
{
    public class GlobalPermissionService : ILateBlocker, INService
    {
        public readonly ConcurrentHashSet<string> BlockedModules;
        public readonly ConcurrentHashSet<string> BlockedCommands;

        public readonly ConcurrentHashSet<string> UnblockedModules;

        public readonly ConcurrentHashSet<string> UnblockedCommands;

        public GlobalPermissionService(IBotConfigProvider bc)
        {
            BlockedModules = new ConcurrentHashSet<string>(bc.BotConfig.BlockedModules.Select(x => x.Name));
            BlockedCommands = new ConcurrentHashSet<string>(bc.BotConfig.BlockedCommands.Select(x => x.Name));
            UnblockedModules = new ConcurrentHashSet<string>(bc.BotConfig.UnblockedModules.Select(x => x.Name));
            UnblockedCommands = new ConcurrentHashSet<string>(bc.BotConfig.UnblockedCommands.Select(x => x.Name));
        }

        public async Task<bool> TryBlockLate(DiscordSocketClient client, IUserMessage msg, IGuild guild, IMessageChannel channel, IUser user, string moduleName, string commandName)
        {
            await Task.Yield();
            commandName = commandName.ToLowerInvariant();

            if (commandName != "resetglobalperms" &&
                (BlockedCommands.Contains(commandName) ||
                BlockedModules.Contains(moduleName.ToLowerInvariant())))
            {
                // Check for unblocked command/module
                if (UnblockedCommands.Contains(commandName) || UnblockedModules.Contains(moduleName.ToLowerInvariant())) 
                {
                    // TODO: Check if user/channel/guild matches the associated whitelist item
                    return false; 
                }
                else { return true; }
                //return true;
                //return new ExecuteCommandResult(cmd, null, SearchResult.FromError(CommandError.Exception, $"Command or module is blocked globally by the bot owner."));
            }
            return false;
        }
    }
}
