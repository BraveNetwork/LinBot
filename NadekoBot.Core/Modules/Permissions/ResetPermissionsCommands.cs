using Discord;
using Discord.Commands;
using System.Threading.Tasks;
using NadekoBot.Common.Attributes;
using NadekoBot.Modules.Permissions.Services;

namespace NadekoBot.Modules.Permissions
{
    public partial class Permissions
    {
        [Group]
        public class ResetPermissionsCommands : NadekoSubmodule
        {
            private readonly ResetPermissionsService _service;

            public ResetPermissionsCommands(ResetPermissionsService service)
            {
                _service = service;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequireUserPermission(GuildPermission.Administrator)]
            public async Task ResetPermissions()
            {
                await _service.ResetPermissions(Context.Guild.Id).ConfigureAwait(false);
                await ReplyConfirmLocalized("perms_reset").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ResetGlobalPermissions()
            {
                await _service.ResetGlobalPermissions().ConfigureAwait(false);
                await ReplyConfirmLocalized("global_perms_reset").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ResetGlobalWhitelists(string flag="")
            {
                bool purge = false;
				string reply = "gwl_reset";
				if (flag.ToLowerInvariant() == "--purge") {
					purge = true;
					reply = "gwl_reset_purge";
				}
                bool result = await _service.ResetGlobalWhitelists(purge).ConfigureAwait(false);
				if (result) {
					await ReplyConfirmLocalized(reply).ConfigureAwait(false);
				} else {
					await ReplyErrorLocalized("gwl_reset_fail").ConfigureAwait(false);
				}
            }

            [NadekoCommand, Usage, Description, Aliases]
            [OwnerOnly]
            public async Task ResetGlobalUnblocked()
            {
                bool result = await _service.ResetGlobalUnblocked().ConfigureAwait(false);
				if (result) {
					await ReplyConfirmLocalized("gub_reset").ConfigureAwait(false);
				} else {
					await ReplyConfirmLocalized("gub_reset_fail").ConfigureAwait(false);
				}
            }
        }
    }
}
