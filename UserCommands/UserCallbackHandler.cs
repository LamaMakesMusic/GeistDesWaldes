using Discord;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Misc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static GeistDesWaldes.UserCommands.UserCallbackDictionary;

namespace GeistDesWaldes.UserCommands
{
    public class UserCallbackHandler : BaseHandler
    {
        public UserCallbackDictionary UserCallbacks;

        private const string USERCALLBACKS_FILE_NAME = "UserCallbacks";


        public UserCallbackHandler(Server server) : base(server)
        {
            UserCallbacks = new UserCallbackDictionary(server);
        }

        internal override void OnServerStart(object source, EventArgs e)
        {
            base.OnServerStart(source, e);

            InitializeUserCallbackHandler().SafeAsync<UserCallbackHandler>(_Server.LogHandler);
        }
        internal override void OnCheckIntegrity(object source, EventArgs e)
        {
            base.OnCheckIntegrity(source, e);

            CheckIntegrity().SafeAsync<UserCallbackHandler>(_Server.LogHandler);
        }

        private async Task InitializeUserCallbackHandler()
        {
            await GenericXmlSerializer.EnsurePathExistance(_Server.LogHandler, _Server.ServerFilesDirectoryPath, USERCALLBACKS_FILE_NAME, UserCallbacks);
            await LoadUserCallbacksFromFile();
        }
        private async Task CheckIntegrity()
        {
            List<string> problematicEntries = new List<string>();

            int idx = 0;
            foreach (var command in UserCallbacks.Callbacks)
            {
                var builder = new System.Text.StringBuilder($"...[{idx}]");
                int startLength = builder.Length;

                if (command == null)
                    builder.Append(" | NULL");
                else if (command.CommandsToExecute != null && command.CommandsToExecute.Length > 0)
                {
                    if (string.IsNullOrWhiteSpace(command.Name))
                        builder.Append(" | missing name");

                    var testResult = await command.TestCommandExecution(_Server.CommandService, _Server.Services);

                    if (!testResult.IsSuccess)
                        builder.Append(" | Commands ERROR:\n").AppendLine($"......{testResult.Reason}");
                }

                if (builder.Length > startLength)
                    problematicEntries.Add(builder.ToString());

                idx++;
            }


            if (problematicEntries.Count > 0)
            {
                var builder = new System.Text.StringBuilder("User Callbacks ERROR:\n");

                for (int i = 0; i < problematicEntries.Count; i++)
                    builder.AppendLine(problematicEntries[i]);

                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
            }
            else
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "User Callbacks OK."), (int)ConsoleColor.DarkGreen);
        }

        public Task SaveUserCallbacksToFile()
        {
            UserCallbacks.Callbacks.Sort((c1,c2) => c1.Name.CompareTo(c2.Name));

            return GenericXmlSerializer.SaveAsync<UserCallbackDictionary>(_Server.LogHandler, UserCallbacks, USERCALLBACKS_FILE_NAME, _Server.ServerFilesDirectoryPath);
        }
        public async Task LoadUserCallbacksFromFile()
        {
            UserCallbackDictionary loadedDictionary =  await GenericXmlSerializer.LoadAsync<UserCallbackDictionary>(_Server.LogHandler, USERCALLBACKS_FILE_NAME, _Server.ServerFilesDirectoryPath);

            if (loadedDictionary == default)
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadUserCallbacksFromFile), $"Loaded {nameof(UserCallbackDictionary)} == DEFAULT"));
            else
                UserCallbacks = loadedDictionary;

            UserCallbacks._Server = _Server;

            //Ensure Name Hash for externally added Callbacks/Commands
            foreach (var callback in UserCallbacks.Callbacks)
            {
                callback?.InitAfterLoadFromFile(_Server);
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
                    return CustomRuntimeResult<CustomCommand>.FromError($"Callback '{DiscordPrefix}{type}' does not exist!");

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
                    return CustomRuntimeResult<CustomCommand>.FromError($"Callback '{TwitchPrefix}{type}' does not exist!");

                return CustomRuntimeResult<CustomCommand>.FromSuccess(value: callbackCommand);
            });
        }

        public async Task<CustomRuntimeResult> SetCallback(DiscordCallbackTypes type, CustomCommand customCommand)
        {
            var commandResult = await GetCallbackCommand(type);

            if (commandResult.IsSuccess)
            {
                if (customCommand == null)
                {
                    commandResult.ResultValue.TextChannelContextID = default;
                    commandResult.ResultValue.CommandsToExecute = null;
                }
                else
                {
                    commandResult.ResultValue.TextChannelContextID = customCommand.TextChannelContextID;
                    commandResult.ResultValue.CommandsToExecute = customCommand.CommandsToExecute;
                }

                await SaveUserCallbacksToFile();
                return CustomRuntimeResult.FromSuccess();
            }
            else
                return CustomRuntimeResult.FromError(commandResult.Reason);
        }
        public async Task<CustomRuntimeResult> SetCallback(TwitchCallbackTypes type, CustomCommand customCommand)
        {
            var commandResult = await GetCallbackCommand(type);

            if (commandResult.IsSuccess)
            {
                if (customCommand == null)
                {
                    commandResult.ResultValue.TextChannelContextID = default;
                    commandResult.ResultValue.CommandsToExecute = null;
                }
                else
                {
                    commandResult.ResultValue.TextChannelContextID = customCommand.TextChannelContextID;
                    commandResult.ResultValue.CommandsToExecute = customCommand.CommandsToExecute;
                }

                await SaveUserCallbacksToFile();
                return CustomRuntimeResult.FromSuccess();
            }
            else
                return CustomRuntimeResult.FromError(commandResult.Reason);
        }

    }
}
