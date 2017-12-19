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
    public partial class Example : NadekoTopLevelModule<ExampleService>
    {

        // Declare your private variables here, such as _db for a DBService, or _cred for IBotCredentials

        // Set up your constructor
        public Example() // Add the source(s) for your private variable(s) as arguments for the constructor
        {
            // Here is where you do any initializations, such as setting your private variables
        }
    }
}