using Discord.Commands;
using GeistDesWaldes.Dictionaries;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace GeistDesWaldes.Attributes
{
    public class CommandCooldown : PreconditionAttribute
    {
        public float CooldownInSeconds { get; }

        public CommandCooldown(float time)
        {
            CooldownInSeconds = time;
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            LogHandler logger = services.GetService<LogHandler>();
            CommandCooldownHandler cooldownHandler = services.GetService<CommandCooldownHandler>();

            double cdLeft = await cooldownHandler.IsOnCooldown(command);
            
            if (cdLeft > 0)
            {
                string errorReason = $"ERROR_COOLDOWN{ReplyDictionary.COMMAND_X_IS_STILL_ON_COOLDOWN_FOR_Y_SECONDS}";
                errorReason = await ReplyDictionary.ReplaceStringInvariantCase(errorReason, "{x}", command.Name);
                errorReason = await ReplyDictionary.ReplaceStringInvariantCase(errorReason, "{y}", cdLeft.ToString("F1"));

                await logger.Log(new Discord.LogMessage(Discord.LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Denied {nameof(CommandCooldown)}-Permission for '{command.Name}' -> {errorReason}"), (int)ConsoleColor.Red);
                return PreconditionResult.FromError(errorReason);
            }
            else
            {
                await logger.Log(new Discord.LogMessage(Discord.LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(CommandCooldown)}-Permission for '{command.Name}'"), (int)ConsoleColor.Green);
                return PreconditionResult.FromSuccess();
            }
        }
    }
}
