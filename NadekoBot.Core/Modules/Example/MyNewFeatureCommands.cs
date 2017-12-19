using Discord;
using Discord.Commands;
using System.Threading.Tasks;
using NadekoBot.Extensions;
using NadekoBot.Common.Attributes;
using NadekoBot.Common.Collections;
using NadekoBot.Common.TypeReaders;
using NadekoBot.Modules.Example.Services;

namespace NadekoBot.Modules.Example
{
    public partial class Example 
    {
        [Group]
        public class MyNewFeatureCommands : NadekoSubmodule
        { 
            // Declare your private variables here, such as _db for a DBService, or _cred for IBotCredentials
            //private readonly IBotCredentials _cred; // for accessing bot credentials (like checking if someone is a bot owner)

            // Set up your constructor
            //public MyNewFratureCommands(IBotCredentials cred)
            public MyNewFeatureCommands() // Add the source(s) for your private variable(s) as arguments for the constructor
            {
                // Here is where you do any initializations, such as setting your private variables
                //_cred = cred;
            }

            // Set up your first new command
            [NadekoCommand, Usage, Description, Aliases] // Required. Tells Nadeko to get information about this command from `src/NadekoBot/_strings/cmd/command_strings.json`
            [OwnerOnly] // Optional. Specifies that this command can only be used by the bot owner
            [RequireContext(ContextType.Guild)] // Optional. Specifies that this command can only be used in a Guild (not in a DM)
            public async Task MyNewCommand( string text, int optionalNumber=1 ) // These arguments determine what parameters your command will accept
            {
                // This is a simple example, so let's just repeat back to the user, using data from `src/NadekoBot/_strings/ResponseStrings.en-US.json`

                if ( string.IsNullOrWhiteSpace(text) ) {
                    // This will use the string labeled 'example_mynewfeature_emptystring'
                    await ReplyErrorLocalized("mynewfeature_emptystring").ConfigureAwait(false);
                    return;
                } else {
                    // This will use the string labeled 'example_mynewfeature_success'
                    await ReplyConfirmLocalized("mynewfeature_success", Format.Bold(text), Format.Bold(optionalNumber.ToString())).ConfigureAwait(false);
                    return;
                }
            }

            // Here's an example of some proxy commands
            // These are commands that use alternate method signatures, but function almost identically
            // So they simply point to a private helper method that does all the work

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public Task MentionUser(ulong userId) => SendText("<@"+userId+">");

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public Task MentionUser(IUser user) => SendText("<@"+user.Id+">");

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public Task MentionChannel(ulong channelId) => SendText("<#"+channelId+">");

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public Task MentionChannel(IChannel channel) => SendText("<#"+channel.Id+">");

            private async Task SendText(string text)
            {
                // Send the text via the same channel
                await Context.Channel.SendMessageAsync(text).ConfigureAwait(false);
                return;
            }
        }
    }
}