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
                System.Console.WriteLine("FOUND A BLOCKED CMDorMDL");

                // Check for unblocked command/module
                if (UnblockedCommands.Contains(commandName))
                {
                    System.Console.WriteLine("Detected unblocked cmd {0}", commandName);
                    // TODO: Check if user/channel/guild exists in any whitelist that has this command unblocked
                    // 1. get whitelists for which command is unblocked
                    // New Method: list WLNAME containing unblocked command

                    // 2A. check if server is in whitelist's servers
                    // 3. check if channel is in whitelist's channels
                    // 4. check if user is in whitelist's users

                    // 2B. check if whitelist members includes serverID, channelID or userID (doesn't matter which)
                    // New Method: check if ID is member in whitelist
                    string[] lists = _gwl.GetNamesByUnblocked(commandName, UnblockedType.Command);
                    GlobalWhitelistSet[] groups = _gwl.GetGroupsByUnblocked(commandName, UnblockedType.Command);
                    bool result = false;
                    for (int i = 0; i<groups.Length; i++) {
                        //System.Console.WriteLine(groups[i].ListName);
                        result = result || _gwl.IsMemberInGroup(guild.Id, groups[i])
                            || _gwl.IsMemberInGroup(channel.Id, groups[i])
                            || _gwl.IsMemberInGroup(user.Id, groups[i]);
                    }
                    System.Console.WriteLine("List Count {0}, Group count {1}", lists.Length, groups.Length);
                    // C. join 3 tables via 2 relation tables to do one database call with a complex query
                    return !result; 
                }
                else if (UnblockedModules.Contains(moduleName.ToLowerInvariant())) 
                {
                    System.Console.WriteLine("Detected unblocked mdl {0}", moduleName);
                    // TODO: Check if user/channel/guild exists in any whitelist that has this module unblocked
                    string[] lists = _gwl.GetNamesByUnblocked(moduleName.ToLowerInvariant(), UnblockedType.Module);
                    GlobalWhitelistSet[] groups = _gwl.GetGroupsByUnblocked(moduleName.ToLowerInvariant(), UnblockedType.Module);
                    bool result = false;
                    for (int i = 0; i<groups.Length; i++) {
                        //System.Console.WriteLine(groups[i].ListName);
                        result = result || _gwl.IsMemberInGroup(guild.Id, groups[i])
                            || _gwl.IsMemberInGroup(channel.Id, groups[i])
                            || _gwl.IsMemberInGroup(user.Id, groups[i]);
                    }
                    System.Console.WriteLine("List Count {0}, Group count {1}", lists.Length);
                    return !result; 
                }
                else { return true; }
                //return true;
                //return new ExecuteCommandResult(cmd, null, SearchResult.FromError(CommandError.Exception, $"Command or module is blocked globally by the bot owner."));
            }
            return false;
        }
    }
}
