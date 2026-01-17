using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.Users;
using Microsoft.Extensions.DependencyInjection;

namespace GeistDesWaldes.Attributes;

public class CommandFee : PreconditionAttribute
{
    public CommandFee(int price)
    {
        PriceTag = price;
    }

    public int PriceTag { get; }

    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        LogHandler logger = services.GetService<LogHandler>();
        ForestUserHandler userHandler = services.GetService<ForestUserHandler>();

        CustomRuntimeResult<ForestUser> getUserResult = await userHandler.GetUser(context.User);

        string errorReason = string.Empty;

        if (getUserResult.IsSuccess)
        {
            if (!getUserResult.ResultValue.CanAfford(PriceTag))
            {
                errorReason = $"The user '{context.User.Username}' is lacking funds!";
            }
        }
        else
        {
            errorReason = getUserResult.Reason;
        }


        if (errorReason?.Length > 0)
        {
            await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Denied {nameof(CommandFee)}-Permission for '{command.Name}' -> {errorReason}"), (int)ConsoleColor.Red);
            return PreconditionResult.FromError(errorReason);
        }

        await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(CommandFee)}-Permission for '{command.Name}'"), (int)ConsoleColor.Green);
        return PreconditionResult.FromSuccess();
    }
}