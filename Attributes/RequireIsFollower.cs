using Discord;
using Discord.Commands;
using GeistDesWaldes.TwitchIntegration;
using GeistDesWaldes.Users;
using System;
using System.Threading.Tasks;
using GeistDesWaldes.Configuration;
using TwitchLib.Api.Helix.Models.Users.GetUserFollows;

namespace GeistDesWaldes.Attributes
{
    [Obsolete("THIS WILL BE GRANTED FOR EVERYONE! The GetUsersFollowsAsync throws and will fail always, so for now it is obsolete.")]
    public class RequireIsFollower : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            LogHandler logger = (LogHandler)services.GetService(typeof(LogHandler));

            await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"OBSOLETE! Granted {nameof(RequireIsFollower)}-Permission always!"), (int)ConsoleColor.Green);
            return PreconditionResult.FromSuccess();


            string errorReason;

            IUser userInQuestion;

            if (context.User is MetaUser metaUser)
                userInQuestion = metaUser.FrontUser;
            else
                userInQuestion = context.User;

            // Check if this message is a twitch message, which is the only context where badges exist
            if (userInQuestion is TwitchUser twitchUser)
            {
                //Bot is always allowed
                if (twitchUser.IsBot && !string.IsNullOrWhiteSpace(TwitchIntegrationHandler.Instance?.BotTwitchId) && twitchUser.TwitchId.Equals(TwitchIntegrationHandler.Instance.BotTwitchId, StringComparison.Ordinal))
                    return PreconditionResult.FromSuccess();

                if (context.Channel is TwitchMessageChannel twitchChannel)
                {
                    var channelOwner = ConfigurationHandler.RuntimeConfig[twitchChannel.GuildId].ChannelOwner;

                    (Follow, Exception) isFollowerResult = await IsUserAFollower(twitchUser.TwitchId, channelOwner.Id);

                    if (isFollowerResult.Item1 != null)
                    {
                        await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(RequireIsFollower)}-Permission for '{command.Name}' (followed at: {isFollowerResult.Item1.FollowedAt})"), (int)ConsoleColor.Green);
                        return PreconditionResult.FromSuccess();
                    }

                    errorReason = isFollowerResult.Item2?.Message ?? $"User is not a Follower of '{twitchChannel.Name}'.";
                }
                else
                    errorReason = $"'{context?.Channel?.Name}' is not a {nameof(TwitchMessageChannel)}.";
            }
            else
                errorReason = $"User is not a twitch message author!";


            await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Denied {nameof(RequireIsFollower)}-Permission for '{command.Name}' -> {errorReason}"), (int)ConsoleColor.Red);
            return PreconditionResult.FromError(errorReason);
        }

        public static async Task<(Follow, Exception)> IsUserAFollower(string userId, string channelId)
        {
            Follow follower = null;

            try
            {
                var result = await TwitchIntegrationHandler.ValidatedApiCall(TwitchIntegrationHandler.Instance.Api.Helix.Users.GetUsersFollowsAsync(fromId: userId, toId: channelId));

                if (result.TotalFollows != 0)
                    follower = result.Follows[0];

                return (follower, null);
            }
            catch (Exception ex)
            {
                return (follower, ex);
            }
        }
    }

}
