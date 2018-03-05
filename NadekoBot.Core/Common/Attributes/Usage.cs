using System.Runtime.CompilerServices;
using Discord.Commands;
using NadekoBot.Core.Services.Impl;
using System.Linq;
using Discord;

namespace NadekoBot.Common.Attributes
{
    public class Usage : RemarksAttribute
    {
        public Usage([CallerMemberName] string memberName="") : base(Usage.GetUsage(memberName))
        {

        }

        public static string GetUsage(string memberName)
        {
            var usage = Localization.LoadCommand(memberName.ToLowerInvariant()).Usage;

			if (usage.Count() > 2) {
				return "```\n" + string.Join("\n", usage) + "\n```";
			}
			else {
				return string.Join(" or ", usage
					.Select(x => Format.Code(x)));
			}
				
        }
    }
}
