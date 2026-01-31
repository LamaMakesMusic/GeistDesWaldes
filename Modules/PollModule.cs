using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Polls;
using Poll = GeistDesWaldes.Polls.Poll;

namespace GeistDesWaldes.Modules;

[RequireTimeJoined("0", "1", Group = "PollPermission")]
[RequireIsFollower(Group = "PollPermission")]
[RequireIsBot(Group = "PollPermission")]
[Group("poll")]
[Alias("polls")]
public class PollModule : ModuleBase<CommandContext>, ICommandModule
{
    public Server Server { get; set; }

    [Priority(-1)]
    [Command]
    [Summary("Lists running polls.")]
    public async Task<RuntimeResult> ListPolls(string name = null, IChannel channel = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Polls)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle(null, EmojiDictionary.INFO)
                                                 .SetDescription(Server.GetModule<PollHandler>().GetPollList())
                                     );

                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }

            CustomRuntimeResult<Poll> result = Server.GetModule<PollHandler>().GetPoll(name, channel?.Id ?? Context.Channel.Id);

            if (result.IsSuccess)
            {
                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Polls)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle(result.ResultValue.HeaderToString(), EmojiDictionary.INFO)
                                                 .SetDescription(result.ResultValue.BodyToString())
                                     );

                await msg.SendAsync();
            }

            return result;
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }


    [Command("vote")]
    [Summary("Dummy Method for Vote Permission Check")]
    public RuntimeResult Vote()
    {
        return CustomRuntimeResult.FromSuccess();
    }


    [RequireUserPermission(GuildPermission.Administrator, Group = "PollAdminPermission")]
    [RequireUserPermission(GuildPermission.ManageChannels, Group = "PollAdminPermission")]
    [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "PollAdminPermission")]
    public class PollAdminModule : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }

        [Command("start")]
        [Summary("Starts a new poll.")]
        public async Task<RuntimeResult> StartPoll(string name, string description, string[] voteOptions, IChannel channel = null)
        {
            try
            {
                CustomRuntimeResult<Poll> result = await Server.GetModule<PollHandler>().StartPoll(name, description, channel ?? Context.Channel, voteOptions);

                if (result.IsSuccess)
                {
                    string body = ReplyDictionary.POLL_VOTE_USING_PREFIX_X.ReplaceStringInvariantCase("{x}", Server.Config.GeneralSettings.PollVotePrefix.ToString());
                    body = $"{result.ResultValue.BodyToString()}\n{body}";

                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Polls)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(ReplyDictionary.POLL_STARTED)
                                                     .SetDescription(body)
                                         );

                    await msg.SendAsync();
                }

                return result;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("stop")]
        [Summary("Stops a running poll.")]
        public async Task<RuntimeResult> StopPoll(string name, IChannel channel = null)
        {
            CustomRuntimeResult<Poll> result = await Server.GetModule<PollHandler>().StopPoll(name, channel?.Id ?? Context.Channel.Id);

            if (result.IsSuccess)
            {
                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle(ReplyDictionary.POLL_STOPPED)
                                                 .SetDescription(result.ResultValue.BodyToString())
                                     );

                await msg.SendAsync();
            }

            return result;
        }
    }
}