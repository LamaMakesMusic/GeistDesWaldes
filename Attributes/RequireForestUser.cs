using Discord;
using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace GeistDesWaldes.Attributes
{
    public class RequireForestUser : PreconditionAttribute
    {
        public RequireForestUser()
        {

        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            Server server = (Server) services.GetService(typeof(Server));
            LogHandler logger = (LogHandler) services.GetService(typeof(LogHandler));

            var getUserResult = await server.ForestUserHandler.GetUser(context.User);

            string errorReason = string.Empty;

            if (!getUserResult.IsSuccess)
                errorReason = getUserResult.Reason;


            if (errorReason?.Length > 0)
            {
                await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Denied {nameof(RequireForestUser)}-Permission for '{command.Name}' -> {errorReason}"), (int)ConsoleColor.Red);
                return PreconditionResult.FromError(errorReason);
            }
            else
            {
                await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(RequireForestUser)}-Permission for '{command.Name}'"), (int)ConsoleColor.Green);
                return PreconditionResult.FromSuccess();
            }
        }
    }
}
