using Discord;
using Discord.Commands;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.UserCommands;
using System;
using System.Threading.Tasks;

namespace GeistDesWaldes.Attributes
{
    public class CategoryLock : PreconditionAttribute
    {
        private readonly CustomCommandCategory _category;
        public CustomCommandCategory Category { get { return _category; } }

        public CategoryLock(CustomCommandCategory category)
        {
            _category = category;
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            LogHandler logger = (LogHandler)services.GetService(typeof(LogHandler));
            string errorReason = string.Empty;

            if (Category == null)
                errorReason = "The referenced category is null!";
            else if (Category.Locked)
                errorReason = $"ERROR_CATEGORY_LOCKED{await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.CATEGORY_X_IS_CURRENTLY_LOCKED, "{x}", Category.Name)}";
            else
            {
                await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(CategoryLock)}-Permission for '{Category.Name}'"), (int)ConsoleColor.Green);
                return PreconditionResult.FromSuccess();
            }


            await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Denied {nameof(CategoryLock)}-Permission for '{Category.Name}' -> {errorReason}"), (int)ConsoleColor.Red);
            return PreconditionResult.FromError(errorReason);
        }
    }
}
