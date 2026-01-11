using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.UserCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeistDesWaldes.TwitchIntegration.IntervalActions
{
    public class TwitchLivestreamIntervalActionHandler : BaseHandler
    {
        private List<CustomCommand> _actions = new List<CustomCommand>();

        private const string ACTIONS_FILE_NAME = "TwitchLivestreamIntervalActions";


        public TwitchLivestreamIntervalActionHandler(Server server) : base(server)
        {
        }


        internal override void OnServerStart(object source, EventArgs e)
        {
            base.OnServerStart(source, e);

            Initialize().SafeAsync<TwitchLivestreamIntervalActionHandler>(_Server.LogHandler);
        }
        internal override void OnCheckIntegrity(object source, EventArgs e)
        {
            base.OnCheckIntegrity(source, e);

            CheckIntegrity().SafeAsync<TwitchLivestreamIntervalActionHandler>(_Server.LogHandler);
        }

        private async Task Initialize()
        {
            await GenericXmlSerializer.EnsurePathExistance(_Server.LogHandler, _Server.ServerFilesDirectoryPath, ACTIONS_FILE_NAME, _actions);

            await LoadActionsFromFile();
        }

        private async Task CheckIntegrity()
        {
            List<string> problematicEntries = new List<string>();

            int idx = 0;
            foreach (CustomCommand command in _actions)
            {
                var builder = new StringBuilder($"...[{idx}]");
                int startLength = builder.Length;

                if (command == null)
                {
                    builder.Append(" | NULL");
                }
                else if (command != null && command.CommandsToExecute != null && command.CommandsToExecute.Length > 0)
                {
                    if (string.IsNullOrWhiteSpace(command.Name))
                        builder.Append(" | missing name");

                    var testResult = await command.TestCommandExecution(_Server.CommandService, _Server.Services);

                    if (!testResult.IsSuccess)
                        builder.Append($" | {nameof(TwitchLivestreamIntervalActionHandler)} ERROR:\n").AppendLine($"......{testResult.Reason}");
                }

                if (builder.Length > startLength)
                    problematicEntries.Add(builder.ToString());

                idx++;
            }


            if (problematicEntries.Count > 0)
            {
                var builder = new StringBuilder($"{nameof(TwitchLivestreamIntervalActionHandler)} ERROR:\n");

                for (int i = 0; i < problematicEntries.Count; i++)
                    builder.AppendLine(problematicEntries[i]);

                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
            }
            else
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), $"{nameof(TwitchLivestreamIntervalActionHandler)}  OK."), (int)ConsoleColor.DarkGreen);
        }
        
        public Task SaveActionsToFile()
        {
            return GenericXmlSerializer.SaveAsync<List<CustomCommand>>(_Server.LogHandler, _actions, ACTIONS_FILE_NAME, _Server.ServerFilesDirectoryPath);
        }
        public async Task LoadActionsFromFile()
        {
            List<CustomCommand> loadedMessages = await GenericXmlSerializer.LoadAsync<List<CustomCommand>>(_Server.LogHandler, ACTIONS_FILE_NAME, _Server.ServerFilesDirectoryPath);

            if (loadedMessages == default)
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadActionsFromFile), $"Loaded {nameof(loadedMessages)} == DEFAULT"));
            else
                _actions = loadedMessages;

            foreach (CustomCommand command in _actions)
            {
                command.InitAfterLoadFromFile(_Server);
            }
        }


        public async Task<RuntimeResult> TryAddAction(ICommandContext context, string name, string[] commandsToExecute, IChannel channel = null)
        {
            try
            {
                name = name.ToLower();

                if (_actions.Any(m => m != null && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.INTERVAL_ACTION_NAMED_X_ALREADY_EXISTS, "{x}", name));

                CustomRuntimeResult<CommandMeta.CommandMetaInfo[]> parseResult = await _Server.CommandInfoHandler.ParseToSerializableCommandInfo(commandsToExecute, context);

                if (parseResult.IsSuccess)
                {
                    CustomCommand command = new CustomCommand(_Server, name, parseResult.ResultValue, channel != null ? channel.Id : 0);

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
                        continue;

                    _actions.RemoveAt(i);

                    await SaveActionsToFile();

                    return CustomRuntimeResult.FromSuccess();
                }

                return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.INTERVAL_ACTION_NAMED_X_NOT_FOUND, "{x}", name));
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        public async Task<CustomRuntimeResult<CustomCommand>> TryGetAction(string name)
        {
            try
            {
                name = name.ToLower();

                CustomCommand command = _actions.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (command == default)
                    return CustomRuntimeResult<CustomCommand>.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.INTERVAL_ACTION_NAMED_X_NOT_FOUND, "{x}", name));

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
                return -1;

            if (currIndex < 0)
                currIndex = Launcher.Random.Next(0, _actions.Count);
            else
                currIndex++;

            if (currIndex >= _actions.Count)
                currIndex = 0;

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
                _actions = _actions.OrderBy(c => Launcher.Random.Next()).ToList();

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
                _actions.Sort((c1,c2) => c1.Name.CompareTo(c2.Name));

                await SaveActionsToFile();
                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }
    }
}
