using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.TwitchIntegration;
using GeistDesWaldes.Users;

namespace GeistDesWaldes.Attributes;

public class RequireIsBot : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        LogHandler logger = (LogHandler)services.GetService(typeof(LogHandler));

        IUser user;

        if (context.User is MetaUser metaUser)
        {
            user = metaUser.FrontUser;
        }
        else
        {
            user = context.User;
        }

        string errorReason = null;

        if (user == default)
        {
            errorReason = "User is null!";
        }

        if (errorReason == null && !user.IsBot)
        {
            errorReason = $"User {user.Username} is not a bot!";
        }

        if (errorReason == null)
        {
            if (user is TwitchUser twitchUser)
            {
                string botId = TwitchIntegrationHandler.Instance?.BotTwitchId;

                if (!string.IsNullOrWhiteSpace(botId) && twitchUser.TwitchId.Equals(botId))
                {
                    await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(RequireIsBot)}-Permission for '{command.Name}'"), (int)ConsoleColor.Green);
                    return PreconditionResult.FromSuccess();
                }

                errorReason = $"User Id {twitchUser.TwitchId} does not match bot id {botId}!";
            }
            else
            {
                ulong botId = Launcher.Instance.DiscordClient.CurrentUser.Id;

                if (botId != default && user.Id == botId)
                {
                    await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(RequireIsBot)}-Permission for '{command.Name}'"), (int)ConsoleColor.Green);
                    return PreconditionResult.FromSuccess();
                }

                errorReason = $"User Id {user.Id} does not match bot id {botId}!";
            }
        }

        await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Denied {nameof(RequireIsBot)}-Permission for '{command.Name}' -> {errorReason}"), (int)ConsoleColor.Red);
        return PreconditionResult.FromError(errorReason);
    }
}