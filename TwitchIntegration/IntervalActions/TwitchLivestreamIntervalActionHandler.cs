using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.CommandMeta;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.UserCommands;

namespace GeistDesWaldes.TwitchIntegration.IntervalActions;

public class TwitchLivestreamIntervalActionHandler : BaseHandler
{
    public override int Priority => -4;
    
    private const string ACTIONS_FILE_NAME = "TwitchLivestreamIntervalActions";

    private readonly CommandInfoHandler _infoHandler;
    private List<CustomCommand> _actions = new();


    public TwitchLivestreamIntervalActionHandler(Server server, CommandInfoHandler infoHandler) : base(server)
    {
        _infoHandler = infoHandler;
    }

    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();
        await Initialize();
    }

    public override async Task OnCheckIntegrity()
    {
        await base.OnCheckIntegrity();
        await CheckIntegrity();
    }

    private async Task Initialize()
    {
        await GenericXmlSerializer.EnsurePathExistance(Server.LogHandler, Server.ServerFilesDirectoryPath, ACTIONS_FILE_NAME, _actions);

        await LoadActionsFromFile();
    }

    private async Task CheckIntegrity()
    {
        var problematicEntries = new List<string>();

        int idx = 0;
        foreach (CustomCommand command in _actions)
        {
            StringBuilder builder = new($"...[{idx}]");
            int startLength = builder.Length;

            if (command == null)
            {
                builder.Append(" | NULL");
            }
            else if (command.CommandsToExecute is { Length: > 0 })
            {
                if (string.IsNullOrWhiteSpace(command.Name))
                {
                    builder.Append(" | missing name");
                }

                CustomRuntimeResult testResult = await command.TestCommandExecution(Server.CommandService, Server.Services);

                if (!testResult.IsSuccess)
                {
                    builder.Append($" | {nameof(TwitchLivestreamIntervalActionHandler)} ERROR:\n").AppendLine($"......{testResult.Reason}");
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
            StringBuilder builder = new($"{nameof(TwitchLivestreamIntervalActionHandler)} ERROR:\n");

            for (int i = 0; i < problematicEntries.Count; i++)
            {
                builder.AppendLine(problematicEntries[i]);
            }

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
        }
        else
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), $"{nameof(TwitchLivestreamIntervalActionHandler)}  OK."), (int)ConsoleColor.DarkGreen);
        }
    }

    public Task SaveActionsToFile()
    {
        return GenericXmlSerializer.SaveAsync<List<CustomCommand>>(Server.LogHandler, _actions, ACTIONS_FILE_NAME, Server.ServerFilesDirectoryPath);
    }

    public async Task LoadActionsFromFile()
    {
        var loadedMessages = await GenericXmlSerializer.LoadAsync<List<CustomCommand>>(Server.LogHandler, ACTIONS_FILE_NAME, Server.ServerFilesDirectoryPath);

        if (loadedMessages == default)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadActionsFromFile), $"Loaded {nameof(loadedMessages)} == DEFAULT"));
        }
        else
        {
            _actions = loadedMessages;
        }

        foreach (CustomCommand command in _actions)
        {
            command.InitAfterLoadFromFile(Server);
        }
    }


    public async Task<RuntimeResult> TryAddAction(ICommandContext context, string name, string[] commandsToExecute, IChannel channel = null)
    {
        try
        {
            name = name.ToLower();

            if (_actions.Any(m => m != null && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                return CustomRuntimeResult.FromError(ReplyDictionary.INTERVAL_ACTION_NAMED_X_ALREADY_EXISTS.ReplaceStringInvariantCase("{x}", name));
            }

            CustomRuntimeResult<CommandMetaInfo[]> parseResult = await _infoHandler.ParseToSerializableCommandInfo(commandsToExecute, context);

            if (parseResult.IsSuccess)
            {
                CustomCommand command = new(Server, name, parseResult.ResultValue, channel != null ? channel.Id : 0);

                _actions.Add(command);

                await SaveActionsToFile();
            }

            return parseResult;
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }

    public async Task<RuntimeResult> TryRemoveAction(string name)
    {
        try
        {
            name = name.ToLower();

            for (int i = _actions.Count - 1; i >= 0; i--)
            {
                if (_actions[i] == null || !_actions[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _actions.RemoveAt(i);

                await SaveActionsToFile();

                return CustomRuntimeResult.FromSuccess();
            }

            return CustomRuntimeResult.FromError(ReplyDictionary.INTERVAL_ACTION_NAMED_X_NOT_FOUND.ReplaceStringInvariantCase("{x}", name));
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }

    public CustomRuntimeResult<CustomCommand> TryGetAction(string name)
    {
        try
        {
            name = name.ToLower();

            CustomCommand command = _actions.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (command == null)
                return CustomRuntimeResult<CustomCommand>.FromError(ReplyDictionary.INTERVAL_ACTION_NAMED_X_NOT_FOUND.ReplaceStringInvariantCase("{x}", name));

            return CustomRuntimeResult<CustomCommand>.FromSuccess(value: command);
        }
        catch (Exception e)
        {
            return CustomRuntimeResult<CustomCommand>.FromError(e.ToString());
        }
    }

    public int GetNextAction(int currIndex, out CustomCommand command)
    {
        command = null;

        if (_actions.Count == 0)
        {
            return -1;
        }

        if (currIndex < 0)
        {
            currIndex = Launcher.Random.Next(0, _actions.Count);
        }
        else
        {
            currIndex++;
        }

        if (currIndex >= _actions.Count)
        {
            currIndex = 0;
        }

        command = _actions[currIndex];

        return currIndex;
    }

    public CustomCommand[] GetAllActions()
    {
        return _actions.ToArray();
    }


    public async Task<RuntimeResult> ShuffleActions()
    {
        try
        {
            _actions = _actions.OrderBy(_ => Launcher.Random.Next()).ToList();

            await SaveActionsToFile();
            return CustomRuntimeResult.FromSuccess();
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }

    public async Task<RuntimeResult> SortActionsByName()
    {
        try
        {
            _actions.Sort((c1, c2) => string.Compare(c1.Name, c2.Name, Server.CultureInfo, CompareOptions.Ordinal));

            await SaveActionsToFile();
            return CustomRuntimeResult.FromSuccess();
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }
}