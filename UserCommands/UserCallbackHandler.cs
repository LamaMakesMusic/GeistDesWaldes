using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Misc;
using static GeistDesWaldes.UserCommands.UserCallbackDictionary;

namespace GeistDesWaldes.UserCommands;

public class UserCallbackHandler : BaseHandler
{
    private const string USERCALLBACKS_FILE_NAME = "UserCallbacks";
    public UserCallbackDictionary UserCallbacks;


    public UserCallbackHandler(Server server) : base(server)
    {
        UserCallbacks = new UserCallbackDictionary(server);
    }

    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();
        await InitializeUserCallbackHandler();
    }

    public override async Task OnCheckIntegrity()
    {
        await base.OnCheckIntegrity();
        await CheckIntegrity();
    }

    private async Task InitializeUserCallbackHandler()
    {
        await GenericXmlSerializer.EnsurePathExistance(Server.LogHandler, Server.ServerFilesDirectoryPath, USERCALLBACKS_FILE_NAME, UserCallbacks);
        await LoadUserCallbacksFromFile();
    }

    private async Task CheckIntegrity()
    {
        List<string> problematicEntries = new();

        int idx = 0;
        foreach (CustomCommand command in UserCallbacks.Callbacks)
        {
            StringBuilder builder = new($"...[{idx}]");
            int startLength = builder.Length;

            if (command == null)
            {
                builder.Append(" | NULL");
            }
            else if (command.CommandsToExecute != null && command.CommandsToExecute.Length > 0)
            {
                if (string.IsNullOrWhiteSpace(command.Name))
                {
                    builder.Append(" | missing name");
                }

                CustomRuntimeResult testResult = await command.TestCommandExecution(Server.CommandService, Server.Services);

                if (!testResult.IsSuccess)
                {
                    builder.Append(" | Commands ERROR:\n").AppendLine($"......{testResult.Reason}");
                }
            }

            if (builder.Length > startLength)
            {
                problematicEntries.Add(builder.ToString());
            }

            idx++;
        }


        if (problematicEntries.Count > 0)
        {
            StringBuilder builder = new("User Callbacks ERROR:\n");

            for (int i = 0; i < problematicEntries.Count; i++)
            {
                builder.AppendLine(problematicEntries[i]);
            }

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
        }
        else
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "User Callbacks OK."), (int)ConsoleColor.DarkGreen);
        }
    }

    public Task SaveUserCallbacksToFile()
    {
        UserCallbacks.Callbacks.Sort((c1, c2) => string.Compare(c1.Name, c2.Name, StringComparison.Ordinal));

        return GenericXmlSerializer.SaveAsync<UserCallbackDictionary>(Server.LogHandler, UserCallbacks, USERCALLBACKS_FILE_NAME, Server.ServerFilesDirectoryPath);
    }

    public async Task LoadUserCallbacksFromFile()
    {
        UserCallbackDictionary loadedDictionary = await GenericXmlSerializer.LoadAsync<UserCallbackDictionary>(Server.LogHandler, USERCALLBACKS_FILE_NAME, Server.ServerFilesDirectoryPath);

        if (loadedDictionary == null)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadUserCallbacksFromFile), $"Loaded {nameof(UserCallbackDictionary)} == DEFAULT"));
        }
        else
        {
            UserCallbacks = loadedDictionary;
        }

        UserCallbacks._Server = Server;

        //Ensure Name Hash for externally added Callbacks/Commands
        foreach (CustomCommand callback in UserCallbacks.Callbacks)
        {
            callback?.InitAfterLoadFromFile(Server);
        }

        UserCallbacks.AddMissingEntries();
    }


    public Task<CustomRuntimeResult<CustomCommand>> GetCallbackCommand(DiscordCallbackTypes type)
    {
        return Task.Run(() =>
        {
            int hash = $"{DiscordPrefix}{type}".ToLower().GetHashCode();

            CustomCommand callbackCommand = UserCallbacks.Callbacks.Find(c => c.NameHash == hash);
            if (callbackCommand == default)
            {
                return CustomRuntimeResult<CustomCommand>.FromError($"Callback '{DiscordPrefix}{type}' does not exist!");
            }

            return CustomRuntimeResult<CustomCommand>.FromSuccess(value: callbackCommand);
        });
    }

    public Task<CustomRuntimeResult<CustomCommand>> GetCallbackCommand(TwitchCallbackTypes type)
    {
        return Task.Run(() =>
        {
            int hash = $"{TwitchPrefix}{type}".ToLower().GetHashCode();

            CustomCommand callbackCommand = UserCallbacks.Callbacks.Find(c => c.NameHash == hash);
            if (callbackCommand == default)
            {
                return CustomRuntimeResult<CustomCommand>.FromError($"Callback '{TwitchPrefix}{type}' does not exist!");
            }

            return CustomRuntimeResult<CustomCommand>.FromSuccess(value: callbackCommand);
        });
    }

    public async Task<CustomRuntimeResult> SetCallback(DiscordCallbackTypes type, CustomCommand customCommand)
    {
        CustomRuntimeResult<CustomCommand> commandResult = await GetCallbackCommand(type);

        if (commandResult.IsSuccess)
        {
            if (customCommand == null)
            {
                commandResult.ResultValue.TextChannelContextId = 0;
                commandResult.ResultValue.CommandsToExecute = null;
            }
            else
            {
                commandResult.ResultValue.TextChannelContextId = customCommand.TextChannelContextId;
                commandResult.ResultValue.CommandsToExecute = customCommand.CommandsToExecute;
            }

            await SaveUserCallbacksToFile();
            return CustomRuntimeResult.FromSuccess();
        }

        return CustomRuntimeResult.FromError(commandResult.Reason);
    }

    public async Task<CustomRuntimeResult> SetCallback(TwitchCallbackTypes type, CustomCommand customCommand)
    {
        CustomRuntimeResult<CustomCommand> commandResult = await GetCallbackCommand(type);

        if (commandResult.IsSuccess)
        {
            if (customCommand == null)
            {
                commandResult.ResultValue.TextChannelContextId = 0;
                commandResult.ResultValue.CommandsToExecute = null;
            }
            else
            {
                commandResult.ResultValue.TextChannelContextId = customCommand.TextChannelContextId;
                commandResult.ResultValue.CommandsToExecute = customCommand.CommandsToExecute;
            }

            await SaveUserCallbacksToFile();
            return CustomRuntimeResult.FromSuccess();
        }

        return CustomRuntimeResult.FromError(commandResult.Reason);
    }
}