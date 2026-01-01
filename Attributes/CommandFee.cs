using Discord;
using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace GeistDesWaldes.Attributes
{
    public class CommandFee : PreconditionAttribute
    {
        private readonly int _priceTag;
        public int PriceTag { get { return _priceTag; } }

        public CommandFee(int price)
        {
            _priceTag = price;
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var server = (Server) services.GetService(typeof(Server));
            var logger = (LogHandler) services.GetService(typeof(LogHandler));

            var getUserResult = await server.ForestUserHandler.GetUser(context.User);

            string errorReason = string.Empty;

            if (getUserResult.IsSuccess)
            {
                if (!getUserResult.ResultValue.CanAfford(_priceTag))
                    errorReason = $"The user '{context.User.Username}' is lacking funds!";
            }
            else
                errorReason = getUserResult.Reason;


            if (errorReason?.Length > 0)
            {
                await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Denied {nameof(CommandFee)}-Permission for '{command.Name}' -> {errorReason}"), (int)ConsoleColor.Red);
                return PreconditionResult.FromError(errorReason);
            }
            else
            {
                await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(CommandFee)}-Permission for '{command.Name}'"), (int)ConsoleColor.Green);
                return PreconditionResult.FromSuccess();
            }
        }
    }
}
