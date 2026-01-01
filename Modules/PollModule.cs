using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using System;
using System.Threading.Tasks;

namespace GeistDesWaldes.Modules
{
    [RequireTimeJoined("0", "1", Group = "PollPermission")]
    [RequireIsFollower(Group = "PollPermission")]
    [RequireIsBot(Group = "PollPermission")]
    [Group("poll")]
    [Alias("polls")]
    public class PollModule : ModuleBase<CommandContext>, IServerModule
    {
        public Server _Server { get; set; }

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
                                .SetDescription(_Server.PollHandler.GetPollList())
                            );

                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                else
                {
                    var result = await _Server.PollHandler.GetPoll(name, channel != null ? channel.Id : Context.Channel.Id);

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
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        [Command("vote")]
        [Summary("Dummy Method for Vote Permission Check")]
        public async Task<RuntimeResult> Vote()
        {
            return CustomRuntimeResult.FromSuccess();
        }


        [RequireUserPermission(GuildPermission.Administrator, Group = "PollAdminPermission")] [RequireUserPermission(GuildPermission.ManageChannels, Group = "PollAdminPermission")]
        [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "PollAdminPermission")]
        public class PollAdminModule : ModuleBase<CommandContext>, IServerModule
        {
            public Server _Server { get; set; }

            [Command("start")]
            [Summary("Starts a new poll.")]
            public async Task<RuntimeResult> StartPoll(string name, string description, string[] voteOptions, IChannel channel = null)
            {
                try
                {
                    var result = await _Server.PollHandler.StartPoll(name, description, channel ?? Context.Channel, voteOptions);

                    if (result.IsSuccess)
                    {
                        string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.POLL_VOTE_USING_PREFIX_X, "{x}", _Server.Config.GeneralSettings.PollVotePrefix.ToString());
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
                var result = await _Server.PollHandler.StopPoll(name, channel != null ? channel.Id : Context.Channel.Id);

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
}
