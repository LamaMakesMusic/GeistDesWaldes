using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Commands.Builders;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.CommandMeta;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;

namespace GeistDesWaldes.UserCommands;

public class CustomCommandHandler : BaseHandler
{
    public override int Priority => -19;
    
    private const string COMMANDINFO_FILE_NAME = "CustomCommands";
    
    private CommandInfoHandler CommandInfoHandler => _commandInfoHandlerReference.Value;
    private readonly ServiceReference<CommandInfoHandler> _commandInfoHandlerReference;

    private ModuleInfo _moduleInfo;
    public CustomCommandDictionary CustomCommands = new();


    public CustomCommandHandler(Server server) : base(server)
    {
        _commandInfoHandlerReference = new ServiceReference<CommandInfoHandler>(server);
    }

    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();
        await InitializeCustomCommandHandler();
    }

    public override async Task OnCheckIntegrity()
    {
        await base.OnCheckIntegrity();
        await CheckIntegrity();
    }

    private async Task InitializeCustomCommandHandler()
    {
        await GenericXmlSerializer.EnsurePathExistence(Server.LogHandler, Server.ServerFilesDirectoryPath, COMMANDINFO_FILE_NAME, CustomCommands);
        await LoadCustomCommandsFromFile();

        await UpdateCommandService();
    }

    private async Task CheckIntegrity()
    {
        var problematicEntries = new List<string>();

        for (int i = 0; i < CustomCommands.Commands.Count; i++)
        {
            CustomCommand command = CustomCommands.Commands[i];
            StringBuilder builder = new($"...[{i}]");
            int startLength = builder.Length;

            if (command == null)
            {
                builder.Append(" | NULL");
            }
            else
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
        }


        if (problematicEntries.Count > 0)
        {
            StringBuilder builder = new("Custom Commands ERROR:\n");

            for (int i = 0; i < problematicEntries.Count; i++)
            {
                builder.AppendLine(problematicEntries[i]);
            }

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
        }
        else
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Custom Commands OK."), (int)ConsoleColor.DarkGreen);
        }
    }

    public async Task<CustomRuntimeResult> AddCommandAsync(CustomCommand command)
    {
        if ((await GetCommandAsync(command.Name)).IsSuccess)
        {
            return CustomRuntimeResult.FromError($"{ReplyDictionary.COMMAND_WITH_NAME_ALREADY_EXISTS}: '{command.Name}'!");
        }

        CustomCommands.Commands.Add(command);

        await UpdateCommandService();
        await SaveCustomCommandsToFile();


        return CustomRuntimeResult.FromSuccess();
    }

    public async Task<CustomRuntimeResult> RemoveCommandAsync(string commandName)
    {
        CustomRuntimeResult<CustomCommand> result = await GetCommandAsync(commandName);
        if (result.IsSuccess && result.ResultValue is { } cmd)
        {
            await cmd.SetCategory(Server, null);

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

            for (int i = 0; i < CustomCommands.Commands.Count; i++)
            {
                if (CustomCommands.Commands[i].NameHash == hash)
                {
                    return CustomRuntimeResult<CustomCommand>.FromSuccess(value: CustomCommands.Commands[i]);
                }
            }

            return CustomRuntimeResult<CustomCommand>.FromError($"{ReplyDictionary.COULD_NOT_FIND_COMMAND_NAMED} '{commandName}'.");
        });
    }


    public Task SaveCustomCommandsToFile()
    {
        return GenericXmlSerializer.SaveAsync<CustomCommandDictionary>(Server.LogHandler, CustomCommands, COMMANDINFO_FILE_NAME, Server.ServerFilesDirectoryPath);
    }

    public async Task LoadCustomCommandsFromFile()
    {
        CustomCommandDictionary loadedDictionary;

        loadedDictionary = await GenericXmlSerializer.LoadAsync<CustomCommandDictionary>(Server.LogHandler, COMMANDINFO_FILE_NAME, Server.ServerFilesDirectoryPath);

        if (loadedDictionary == default(CustomCommandDictionary))
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadCustomCommandsFromFile), $"Loaded {nameof(CustomCommandDictionary)} == DEFAULT"));
        }
        else
        {
            CustomCommands = loadedDictionary;
        }

        //Ensure Name Hash
        for (int i = 0; i < CustomCommands.Commands.Count; i++)
        {
            CustomCommands.Commands[i]?.InitAfterLoadFromFile(Server);
        }

        //Re-Connect Commands with Category
        if (CustomCommands.Categories != null)
        {
            foreach (CustomCommandCategory category in CustomCommands.Categories)
            {
                for (int i = category.Commands.Count - 1; i >= 0; i--)
                {
                    CustomRuntimeResult<CustomCommand> cmdResult = await GetCommandAsync(category.Commands[i]);

                    if (cmdResult.IsSuccess)
                    {
                        cmdResult.ResultValue.Category = category;
                    }
                    else
                    {
                        category.Commands.RemoveAt(i);
                    }
                }

                category.Commands.Sort();
            }
        }

        // Sorting
        CustomCommands.Commands.Sort((c1, c2) => string.Compare(c1?.Name ?? "", c2?.Name ?? "", StringComparison.Ordinal));
        CustomCommands.Categories?.Sort((c1, c2) => string.Compare(c1?.Name ?? "", c2?.Name ?? "", StringComparison.Ordinal));
    }


    public async Task UpdateCommandService()
    {
        try
        {
            //Sort By Name
            CustomCommands.Commands.Sort((c1, c2) => c1.CompareTo(c2));

            if (_moduleInfo != null)
            {
                await Server.CommandService.RemoveModuleAsync(_moduleInfo);
            }

            _moduleInfo = await Server.CommandService.CreateModuleAsync("",
                mb =>
                {
                    for (int i = 0; i < CustomCommands.Commands.Count; i++)
                    {
                        CustomCommand command = CustomCommands.Commands[i];

                        mb.AddCommand(command.Name, command.ExecuteCallback, BuildCommand);
                        continue;

                        void BuildCommand(CommandBuilder cb)
                        {
                            cb.WithSummary($"Category: {(command.Category == null ? "-" : command.Category.Name)}");

                            bool hasCategory = command.Category != null;

                            float cooldown = command.CooldownInSeconds;
                            if (hasCategory && command.Category.CategoryCooldownInSeconds > cooldown)
                            {
                                cooldown = command.Category.CategoryCooldownInSeconds;
                            }

                            int priceTag = command.PriceTag;
                            if (hasCategory && command.Category.PriceTag > priceTag)
                            {
                                priceTag = command.Category.PriceTag;
                            }

                            if (cooldown > 0f)
                            {
                                cb.AddPrecondition(new CommandCooldown(cooldown));
                            }

                            if (priceTag > 0)
                            {
                                cb.AddPrecondition(new CommandFee(priceTag));
                            }

                            if (hasCategory)
                            {
                                cb.AddPrecondition(new CategoryLock(command.Category));
                            }

                            command.RealCooldownInSeconds = cooldown;
                            command.RealPriceTag = priceTag;
                        }
                    }
                }
            );

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(UpdateCommandService), "OK!"), (int)ConsoleColor.DarkGreen);
        }
        catch (Exception e)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateCommandService), $"Command Service Update: ERROR \n{e}"));
        }
        finally
        {
            await CommandInfoHandler.CollectCustomCommands();
            await CommandInfoHandler.CreateHelpListStringsAsync();
        }
    }


    public Task<CustomRuntimeResult<CustomCommandCategory>> GetCategory(string categoryName)
    {
        return Task.Run(() =>
        {
            CustomCommandCategory category = default;

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                category = CustomCommands.Categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            }

            if (category == default)
            {
                return CustomRuntimeResult<CustomCommandCategory>.FromError($"{ReplyDictionary.CATEGORY_DOES_NOT_EXISTS} ('{categoryName}')");
            }

            return CustomRuntimeResult<CustomCommandCategory>.FromSuccess(value: category);
        });
    }

    public async Task<CustomRuntimeResult<CustomCommandCategory>> CreateCategory(string categoryName)
    {
        if ((await GetCategory(categoryName)).IsSuccess)
        {
            return CustomRuntimeResult<CustomCommandCategory>.FromError($"{ReplyDictionary.CATEGORY_ALREADY_EXISTS} ('{categoryName}')");
        }

        CustomCommandCategory category = new(categoryName);
        CustomCommands.Categories.Add(category);

        return CustomRuntimeResult<CustomCommandCategory>.FromSuccess(value: category);
    }

    public async Task<CustomRuntimeResult<CustomCommandCategory>> GetOrCreateCategory(string categoryName)
    {
        CustomRuntimeResult<CustomCommandCategory> result = await GetCategory(categoryName);

        if (result.IsSuccess)
        {
            return result;
        }

        return await CreateCategory(categoryName);
    }

    public async Task<CustomRuntimeResult> DeleteCategory(string categoryName)
    {
        CustomRuntimeResult<CustomCommandCategory> result = await GetCategory(categoryName);
        if (result.IsSuccess)
        {
            for (int i = result.ResultValue.Commands.Count - 1; i >= 0; i--)
            {
                if ((await GetCommandAsync(result.ResultValue.Commands[i])).ResultValue is { } cmd)
                {
                    await cmd.SetCategory(Server, null);
                }
            }

            CustomCommands.Categories.Remove(result.ResultValue);

            return CustomRuntimeResult.FromSuccess();
        }

        return CustomRuntimeResult.FromError(result.Reason);
    }
}