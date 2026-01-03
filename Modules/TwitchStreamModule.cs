using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.TwitchIntegration;
using System;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.Chat;

namespace GeistDesWaldes.Modules
{
    [RequireTimeJoined("0", "1", Group = "Free4AllPermission")]
    [RequireIsFollower(Group = "Free4AllPermission")]
    [RequireIsBot(Group = "Free4AllPermission")]
    public class TwitchStreamModule : ModuleBase<CommandContext>, IServerModule
    {
        public Server _Server { get; set; }


        [Command("uptime")]
        [Summary("Prints time the stream has been online.")]
        public async Task<RuntimeResult> GetUptime()
        {
            try
            {
                string channelName = _Server.Config.TwitchSettings.TwitchChannelName;

                StreamObject cachedStream = null;
                
                if (TwitchIntegrationHandler.Instance.Clients.TryGetValue(channelName, out TwitchIntegrationClient client))
                {
                    cachedStream = client.StreamInfo;
                }
                
                string body;

                if (cachedStream != null && cachedStream.IsOnline)
                {
                    body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.X_HAS_BEEN_STREAMING_FOR_Y, "{x}", channelName);
                    body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{y}", (DateTime.UtcNow - cachedStream.StartedAt).ToString(@"hh\:mm\:ss", _Server.CultureInfo));
                }
                else
                {
                    if (cachedStream == null)
                    {
                        body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.X_IS_NOT_STREAMING, "{x}", channelName);
                    }
                    else
                    {
                        body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.X_LAST_STREAM_Y_AGO, "{x}", channelName);
                        body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{y}", (DateTime.UtcNow - cachedStream.LastSeenAt).ToString(@"hh\:mm\:ss", _Server.CultureInfo));
                    }
                }

                ChannelMessage msg = new ChannelMessage(Context)
                    .SetTemplate(ChannelMessage.MessageTemplateOption.Twitch)
                    .AddContent(new ChannelMessageContent()
                        .SetTitle(ReplyDictionary.UPTIME, EmojiDictionary.HOURGLASS)
                        .SetDescription(body)
                    );

                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("followtime")]
        [Summary("Prints time the user has followed the channel.")]
        public async Task<RuntimeResult> GetFollowage(IUser user = null)
        {
            try
            {
                if (user == null)
                    user = Context.User;

                if (Context.Channel is not TwitchMessageChannel twitchChannel)
                    return CustomRuntimeResult.FromError($"{ReplyDictionary.COMMAND_ONLY_VALID_ON_TWITCH} -> '{Context?.Channel?.Name}' is not a {nameof(TwitchMessageChannel)}.");
                
                if (user is not TwitchUser twitchUser)
                    return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.USER_X_NOT_A_TWITCH_USER, "{x}", user?.Username ?? "null"));

                string body;
                var isFollowerResult = await RequireIsFollower.IsUserAFollower(twitchUser.TwitchId, _Server.RuntimeConfig.ChannelOwner.Id);

                if (isFollowerResult.Item1 != null)
                {
                    var followage = new TimeObject((DateTime.UtcNow - isFollowerResult.Item1.FollowedAt.ToUniversalTime()), isFollowerResult.Item1.FollowedAt.ToUniversalTime());

                    body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.X_HAS_BEEN_FOLLOWING_Y_FOR_Z_TIME, "{x}", twitchUser.Username);
                    body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{y}", _Server.Config.TwitchSettings.TwitchChannelName);
                    body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{z}", followage.ToStringMinimal());
                }
                else if (isFollowerResult.Item2 != null)
                {
                    throw isFollowerResult.Item2;
                }
                else
                {
                    body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.X_IS_NOT_A_FOLLOWER_YET, "{x}", twitchUser.Username);
                }


                ChannelMessage msg = new ChannelMessage(Context)
                .SetTemplate(ChannelMessage.MessageTemplateOption.Twitch)
                .AddContent(new ChannelMessageContent()
                    .SetDescription(body)
                );

                await msg.SendAsync();


                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("watchtime")]
        [Alias("hours", "viewtime")]
        [Summary("Prints the summed up time a user has watched.")]
        public async Task<RuntimeResult> GetWatchtime()
        {
            try
            {
                if (Context.Channel is not TwitchMessageChannel twitchChannel || Context.User is not TwitchUser twitchUser)
                    return CustomRuntimeResult.FromError($"{ReplyDictionary.COMMAND_ONLY_VALID_ON_TWITCH} -> '{Context?.Channel?.Name}' is not a {nameof(TwitchMessageChannel)}.");

                var userResult = await _Server.ForestUserHandler.GetUser(twitchUser);
                if (!userResult.IsSuccess)
                    return CustomRuntimeResult.FromError(userResult.Reason);
                
                DateTime startedWatching = DateTime.UtcNow - userResult.ResultValue.TwitchViewTime;

                string body = ReplyDictionary.X_HAS_WATCHED_FOR_Y_TIME;
                body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{x}", userResult.ResultValue.Name);
                body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{y}", $"{new TimeObject(DateTime.UtcNow - startedWatching, DateTime.Now.ToUniversalTime()).ToStringMinimal()}");

                ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Twitch)
                        .AddContent(new ChannelMessageContent().SetDescription(body));

                await msg.SendAsync();
                
                return userResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }
        
        
        
        [RequireUserPermission(GuildPermission.Administrator, Group = "Free4AllAdminPermission")]
        [RequireUserPermission(GuildPermission.ManageChannels, Group = "Free4AllAdminPermission")]
        [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "Free4AllAdminPermission")]
        [RequireIsBot(Group = "Free4AllAdminPermission")]
        public class TwitchStreamAdminModule : ModuleBase<CommandContext>, IServerModule
        {
            public Server _Server { get; set; }

            public enum TwitchAnnouncementColorOption
            {
                Primary = 0,
                Blue = 1,
                Green = 2,
                Orange = 3,
                Purple = 4
            }


            [Command("SetWatchtime")]
            [Alias("SetHours", "SetViewtime")]
            [Summary("Edits watchtime of a user.")]
            public async Task<RuntimeResult> EditWatchtime(double timeInHours, IUser targetUser = null)
            {
                try
                {
                    targetUser ??= Context.User;

                    if (Context.Channel is not TwitchMessageChannel twitchChannel)
                        return CustomRuntimeResult.FromError($"{ReplyDictionary.COMMAND_ONLY_VALID_ON_TWITCH} -> '{Context?.Channel?.Name}' is not a {nameof(TwitchMessageChannel)}.");

                    var userResult = await _Server.ForestUserHandler.GetUser(targetUser);
                    if (!userResult.IsSuccess)
                        return CustomRuntimeResult.FromError(userResult.Reason);

                    TimeSpan newTime = TimeSpan.FromHours(timeInHours);

                    string body = $"{ReplyDictionary.WATCHTIME_ADJUSTED}\n{targetUser.Username}: \"{userResult.ResultValue.TwitchViewTime}\" -> \"{newTime}\"";

                    userResult.ResultValue.TwitchViewTime = newTime;
                    userResult.ResultValue.IsDirty = true;

                    ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Twitch)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                            .SetDescription(body)
                        );

                    await msg.SendAsync();

                    return userResult;
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("Announcement")]
            [Alias("twAnnouncement")]
            [Summary("Sends a twitch announcement.")]
            public async Task<RuntimeResult> SendAnnouncement(string message, TwitchAnnouncementColorOption color = TwitchAnnouncementColorOption.Primary)
            {
                // https://dev.twitch.tv/docs/api/reference/#send-chat-announcement
                if (Context.Channel is not TwitchMessageChannel twitchChannel)
                    return CustomRuntimeResult.FromError($"{ReplyDictionary.COMMAND_ONLY_VALID_ON_TWITCH} -> '{Context?.Channel?.Name}' is not a {nameof(TwitchMessageChannel)}.");

                try
                {
                    AnnouncementColors ac = color switch
                    {
                        TwitchAnnouncementColorOption.Blue => AnnouncementColors.Blue,
                        TwitchAnnouncementColorOption.Green => AnnouncementColors.Green,
                        TwitchAnnouncementColorOption.Orange => AnnouncementColors.Orange,
                        TwitchAnnouncementColorOption.Purple => AnnouncementColors.Purple,
                        _ => AnnouncementColors.Primary
                    };
                    
                    await TwitchIntegrationHandler.SendAnnouncement(twitchChannel.Name, message, ac);
                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("Shoutout")]
            [Alias("twShoutout")]
            [Summary("Sends a twitch shoutout.")]
            public async Task<RuntimeResult> SendShoutout(IUser user)
            {
                // https://dev.twitch.tv/docs/api/reference/#send-a-shoutout
                if (Context.Channel is not TwitchMessageChannel twitchChannel)
                    return CustomRuntimeResult.FromError($"{ReplyDictionary.COMMAND_ONLY_VALID_ON_TWITCH} -> '{Context?.Channel?.Name}' is not a {nameof(TwitchMessageChannel)}.");

                if (user is not TwitchUser tUser)
                    return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.USER_X_NOT_A_TWITCH_USER, "{x}", user?.Username ?? "null"));

                try
                {
                    await TwitchIntegrationHandler.SendShoutout(twitchChannel.Name, tUser.Username);
                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("SyncStreamCalendar")]
            [Summary("Reads the stream calendar and writes it to the respective discord channel.")]
            public async Task<RuntimeResult> SyncStreamCalendar()
            {
                try
                {
                    await _Server.WebCalSyncHandler.SyncCalendarToDiscordChannel();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch(Exception ex)
                {
                    return CustomRuntimeResult.FromError(ex.Message);
                }
            }
        }
    }

}
