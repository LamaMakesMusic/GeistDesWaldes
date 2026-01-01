using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using System.Threading.Tasks;

namespace GeistDesWaldes.Modules
{
    [RequireUserPermission(GuildPermission.Administrator, Group = "DiscordInterfacePermissions")] [RequireUserPermission(GuildPermission.ManageChannels, Group = "DiscordInterfacePermissions")]
    [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "DiscordInterfacePermissions")]
    [RequireIsBot(Group = "DiscordInterfacePermissions")]
    public class DiscordInterfaceModule : ModuleBase<CommandContext>, IServerModule
    {
        public Server _Server { get; set; }

        [Group("channel")]
        [Alias("channels")]
        public class ChannelSubGroup : ModuleBase<CommandContext>
        {
            [Command("SetName")]
            [Summary("Sets Discord Text/Voice Channel Name.")]
            private async Task<RuntimeResult> SetChannelName([Summary("The affected channel id.")] ulong channelId, [Summary("The new channel name.")] string newName)
            {
                return await CommunicationHandler.SetChannelName(channelId, newName);
            }

            [Command("SetTopic")]
            [Summary("Sets Discord Text Channel Topic.")]
            private async Task<RuntimeResult> SetChannelTopic([Summary("The affected channel Id.")] ulong channelId, [Summary("The new channel topic.")] string newTopic)
            {
                return await CommunicationHandler.SetChannelTopic(channelId, newTopic);
            }
        }
    }
}
