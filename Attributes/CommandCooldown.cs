using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.Dictionaries;
using Microsoft.Extensions.DependencyInjection;

namespace GeistDesWaldes.Attributes;

public class CommandCooldown : PreconditionAttribute
{
    public CommandCooldown(float time)
    {
        CooldownInSeconds = time;
    }

    public float CooldownInSeconds { get; }

    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        LogHandler logger = services.GetService<LogHandler>();
        CommandCooldownHandler cooldownHandler = services.GetService<CommandCooldownHandler>();

        double cdLeft = await cooldownHandler.IsOnCooldown(command);

        if (cdLeft > 0)
        {
            string errorReason = $"ERROR_COOLDOWN{ReplyDictionary.COMMAND_X_IS_STILL_ON_COOLDOWN_FOR_Y_SECONDS}"
                .ReplaceStringInvariantCase("{x}", command.Name)
                .ReplaceStringInvariantCase("{y}", cdLeft.ToString("F1"));

            await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Denied {nameof(CommandCooldown)}-Permission for '{command.Name}' -> {errorReason}"), (int)ConsoleColor.Red);
            return PreconditionResult.FromError(errorReason);
        }

        await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(CommandCooldown)}-Permission for '{command.Name}'"), (int)ConsoleColor.Green);
        return PreconditionResult.FromSuccess();
    }
}