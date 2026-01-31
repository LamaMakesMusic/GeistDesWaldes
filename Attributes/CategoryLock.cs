using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.UserCommands;

namespace GeistDesWaldes.Attributes;

public class CategoryLock : PreconditionAttribute
{
    public CategoryLock(CustomCommandCategory category)
    {
        Category = category;
    }

    public CustomCommandCategory Category { get; }

    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        LogHandler logger = (LogHandler)services.GetService(typeof(LogHandler));
        
        if (logger == null)
            Console.WriteLine("LOGGER == NULL!");
        
        string errorReason;

        if (Category == null)
        {
            errorReason = "The referenced category is null!";
        }
        else if (Category.Locked)
        {
            errorReason = $"ERROR_CATEGORY_LOCKED{ ReplyDictionary.CATEGORY_X_IS_CURRENTLY_LOCKED.ReplaceStringInvariantCase("{x}", Category.Name)}";
        }
        else
        {
            if (logger != null)
                await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(CategoryLock)}-Permission for '{Category.Name}'"), (int)ConsoleColor.Green);
            
            return PreconditionResult.FromSuccess();
        }


        if (logger != null)
            await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Denied {nameof(CategoryLock)}-Permission for '{Category?.Name}' -> {errorReason}"), (int)ConsoleColor.Red);
        
        return PreconditionResult.FromError(errorReason);
    }
}