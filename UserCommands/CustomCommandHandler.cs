using Discord;
using Discord.Commands;
using Discord.Commands.Builders;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GeistDesWaldes.UserCommands
{
    public class CustomCommandHandler : BaseHandler
    {
        public CustomCommandDictionary CustomCommands;

        private const string COMMANDINFO_FILE_NAME = "CustomCommands";
        private ModuleInfo _moduleInfo;


        public CustomCommandHandler(Server server) : base(server)
        {
            CustomCommands = new CustomCommandDictionary();
        }
        
        internal override void OnServerStart(object source, EventArgs e)
        {
            base.OnServerStart(source, e);

            Task.Run(InitializeCustomCommandHandler).GetAwaiter().GetResult();
        }
        internal override void OnCheckIntegrity(object source, EventArgs e)
        {
            base.OnServerStart(source, e);

            Task.Run(CheckIntegrity).GetAwaiter().GetResult();
        }

        private async Task InitializeCustomCommandHandler()
        {
            await GenericXmlSerializer.EnsurePathExistance(_Server.LogHandler, _Server.ServerFilesDirectoryPath, COMMANDINFO_FILE_NAME, CustomCommands);
            await LoadCustomCommandsFromFile();

            await UpdateCommandService();
        }
        private async Task CheckIntegrity()
        {
            List<string> problematicEntries = new List<string>();

            for (int i = 0; i < CustomCommands.Commands.Count; i++)
            {
                CustomCommand command = CustomCommands.Commands[i];
                var builder = new System.Text.StringBuilder($"...[{i}]");
                int startLength = builder.Length;

                if (command == null)
                    builder.Append(" | NULL");
                else
                {
                    if (string.IsNullOrWhiteSpace(command.Name))
                        builder.Append(" | missing name");

                    var testResult = await command.TestCommandExecution(_Server.CommandService, _Server.Services);

                    if (!testResult.IsSuccess)
                        builder.Append(" | Commands ERROR:\n").AppendLine($"......{testResult.Reason}");
                }

                if (builder.Length > startLength)
                    problematicEntries.Add(builder.ToString());
            }


            if (problematicEntries.Count > 0)
            {
                var builder = new System.Text.StringBuilder("Custom Commands ERROR:\n");

                for (int i = 0; i < problematicEntries.Count; i++)
                    builder.AppendLine(problematicEntries[i]);

                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
            }
            else
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Custom Commands OK."), (int)ConsoleColor.DarkGreen);
        }

        public async Task<CustomRuntimeResult> AddCommandAsync(CustomCommand command)
        {
            if ((await GetCommandAsync(command.Name)).IsSuccess)
                return CustomRuntimeResult.FromError($"{ReplyDictionary.COMMAND_WITH_NAME_ALREADY_EXISTS}: '{command.Name}'!");

            CustomCommands.Commands.Add(command);

            await UpdateCommandService();
            await SaveCustomCommandsToFile();


            return CustomRuntimeResult.FromSuccess();
        }
        public async Task<CustomRuntimeResult> RemoveCommandAsync(string commandName)
        {
            var result = await GetCommandAsync(commandName);
            if (result.IsSuccess && result.ResultValue is CustomCommand cmd)
            {
                await cmd.SetCategory(_Server, null);

                CustomCommands.Commands.Remove(cmd);

                await UpdateCommandService();
                await SaveCustomCommandsToFile();

                return CustomRuntimeResult.FromSuccess();
            }

            return CustomRuntimeResult.FromError(result.Reason);
        }

        public Task<CustomRuntimeResult<CustomCommand>> GetCommandAsync(string commandName)
        {
            return Task.Run(() =>
            {
                int hash = commandName.ToLower().GetHashCode();

                for (int i = 0; i < _Server.CustomCommandHandler.CustomCommands.Commands.Count; i++)
                {
                    if (_Server.CustomCommandHandler.CustomCommands.Commands[i].NameHash == hash)
                        return CustomRuntimeResult<CustomCommand>.FromSuccess(value: _Server.CustomCommandHandler.CustomCommands.Commands[i]);
                }

                return CustomRuntimeResult<CustomCommand>.FromError($"{ReplyDictionary.COULD_NOT_FIND_COMMAND_NAMED} '{commandName}'.");
            });
        }


        public Task SaveCustomCommandsToFile()
        {
            return GenericXmlSerializer.SaveAsync<CustomCommandDictionary>(_Server.LogHandler, CustomCommands, COMMANDINFO_FILE_NAME, _Server.ServerFilesDirectoryPath);
        }
        public async Task LoadCustomCommandsFromFile()
        {
            CustomCommandDictionary loadedDictionary = null;

            loadedDictionary = await GenericXmlSerializer.LoadAsync<CustomCommandDictionary>(_Server.LogHandler, COMMANDINFO_FILE_NAME, _Server.ServerFilesDirectoryPath);

            if (loadedDictionary == default(CustomCommandDictionary))
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadCustomCommandsFromFile), $"Loaded {nameof(CustomCommandDictionary)} == DEFAULT"));
            else
                CustomCommands = loadedDictionary;

            //Ensure Name Hash
            for (int i = 0; i < CustomCommands.Commands.Count; i++)
            {
                CustomCommands.Commands[i]?.InitAfterLoadFromFile(_Server);
            }

            //Re-Connect Commands with Category
            if (CustomCommands.Categories != null)
            {
                foreach (var category in CustomCommands.Categories)
                {
                    for (int i = category.Commands.Count - 1; i >= 0; i--)
                    {
                        var cmdResult = await GetCommandAsync(category.Commands[i]);

                        if (cmdResult.IsSuccess)
                            cmdResult.ResultValue.Category = category;
                        else
                            category.Commands.RemoveAt(i);
                    }

                    category.Commands.Sort();
                }
            }

            // Sorting
            CustomCommands.Commands.Sort((c1, c2) => (c1?.Name ?? "").CompareTo(c2?.Name ?? ""));
            CustomCommands.Categories.Sort((c1, c2) => (c1?.Name ?? "").CompareTo(c2?.Name ?? ""));
        }


        public async Task UpdateCommandService()
        {
            try
            {
                //Sort By Name
                CustomCommands.Commands.Sort((c1, c2) => c1.CompareTo(c2));

                if (_moduleInfo != null)
                    await _Server.CommandService.RemoveModuleAsync(_moduleInfo);

                _moduleInfo = await _Server.CommandService.CreateModuleAsync("",
                    new Action<ModuleBuilder>(mb =>
                        {
                            for (int i = 0; i < CustomCommands.Commands.Count; i++)
                            {
                                CustomCommand command = CustomCommands.Commands[i];
                                Action<CommandBuilder> commandBuilderAction = new Action<CommandBuilder>(cb =>
                                {
                                    cb.WithSummary($"Category: {(command.Category == null ? "-" : command.Category.Name)}");

                                    bool hasCategory = command.Category != null;

                                    float cooldown = command.CooldownInSeconds;
                                    if (hasCategory && command.Category.CategoryCooldownInSeconds > cooldown)
                                        cooldown = command.Category.CategoryCooldownInSeconds;

                                    int priceTag = command.PriceTag;
                                    if (hasCategory && command.Category.PriceTag > priceTag)
                                        priceTag = command.Category.PriceTag;

                                    if (cooldown > 0f)
                                        cb.AddPrecondition(new CommandCooldown(cooldown));

                                    if (priceTag > 0)
                                        cb.AddPrecondition(new CommandFee(priceTag));

                                    if (hasCategory)
                                        cb.AddPrecondition(new CategoryLock(command.Category));

                                    command.RealCooldownInSeconds = cooldown;
                                    command.RealPriceTag = priceTag;
                                });

                                mb.AddCommand(command.Name, command.ExecuteCallback, commandBuilderAction);
                            }
                        }
                    )
                );

                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(UpdateCommandService), "OK!"), (int)ConsoleColor.DarkGreen);
            }
            catch (Exception e)
            {
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateCommandService), $"Command Service Update: ERROR \n{e}"));
            }
            finally
            {
                await _Server.CommandInfoHandler.CollectCustomCommands();
                await _Server.CommandInfoHandler.CreateHelpListStringsAsync();
            }
        }


        public Task<CustomRuntimeResult<CustomCommandCategory>> GetCategory(string categoryName)
        {
            return Task.Run(() =>
            {
                CustomCommandCategory category = default;

                if (!string.IsNullOrWhiteSpace(categoryName))
                    category = CustomCommands.Categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

                if (category == default)
                    return CustomRuntimeResult<CustomCommandCategory>.FromError($"{ReplyDictionary.CATEGORY_DOES_NOT_EXISTS} ('{categoryName}')");

                return CustomRuntimeResult<CustomCommandCategory>.FromSuccess(value: category);
            });
        }
        public async Task<CustomRuntimeResult<CustomCommandCategory>> CreateCategory(string categoryName)
        {
            if ((await GetCategory(categoryName)).IsSuccess)
                return CustomRuntimeResult<CustomCommandCategory>.FromError($"{ReplyDictionary.CATEGORY_ALREADY_EXISTS} ('{categoryName}')");

            var category = new CustomCommandCategory(categoryName);
            CustomCommands.Categories.Add(category);

            return CustomRuntimeResult<CustomCommandCategory>.FromSuccess(value: category);
        }
        public async Task<CustomRuntimeResult<CustomCommandCategory>> GetOrCreateCategory(string categoryName)
        {
            var result = await GetCategory(categoryName);

            if (result.IsSuccess)
                return result;

            return await CreateCategory(categoryName);
        }
        public async Task<CustomRuntimeResult> DeleteCategory(string categoryName)
        {
            var result = await GetCategory(categoryName);
            if (result.IsSuccess)
            {
                for (int i = result.ResultValue.Commands.Count - 1; i >= 0; i--)
                {
                    if ((await GetCommandAsync(result.ResultValue.Commands[i])).ResultValue is CustomCommand cmd)
                        await cmd.SetCategory(_Server, null);
                }

                CustomCommands.Categories.Remove(result.ResultValue);

                return CustomRuntimeResult.FromSuccess();
            }

            return CustomRuntimeResult.FromError(result.Reason);
        }

    }
}
