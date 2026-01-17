using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.TwitchIntegration;
using GeistDesWaldes.Users;

namespace GeistDesWaldes.Attributes;

[Flags]
public enum BadgeTypeOption
{
    None = 1 << 0,
    Broadcaster = 1 << 1,
    Moderator = 1 << 2,
    Vip = 1 << 3,
    Founder = 1 << 4,
    Subscriber = 1 << 5
}

public class RequireTwitchBadge : PreconditionAttribute
{
    public RequireTwitchBadge(BadgeTypeOption badges)
    {
        RequiredBadges = badges;
    }

    public BadgeTypeOption RequiredBadges { get; }


    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        LogHandler logger = (LogHandler)services.GetService(typeof(LogHandler));

        string errorReason;

        IUser userInQuestion;

        if (context.User is MetaUser metaUser)
        {
            userInQuestion = metaUser.FrontUser;
        }
        else
        {
            userInQuestion = context.User;
        }

        // Check if this message is a twitch message, which is the only context where badges exist
        if (userInQuestion is TwitchUser author)
        {
            //Bot is always allowed
            if (author.IsBot && author.TwitchId.Equals(TwitchIntegrationHandler.Instance.BotTwitchId, StringComparison.Ordinal))
            {
                return PreconditionResult.FromSuccess();
            }

            if ((RequiredBadges & author.Badges) != 0)
            {
                await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(RequireTwitchBadge)}-Permission for '{command.Name}' -> {RequiredBadges} contains {author.Badges}."), (int)ConsoleColor.Green);
                return PreconditionResult.FromSuccess();
            }

            errorReason = $"User Badges '{author.Badges}' do not overlap required badges '{RequiredBadges}'.";
        }
        else
        {
            errorReason = "User is not a twitch message author!";
        }


        await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Denied {nameof(RequireTwitchBadge)}-Permission for '{command.Name}' -> {errorReason}"), (int)ConsoleColor.Red);
        return PreconditionResult.FromError(errorReason);
    }
}