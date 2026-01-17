using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.UserCommands;
using ParameterInfo = System.Reflection.ParameterInfo;

namespace GeistDesWaldes.CommandMeta;

public class CommandInfoHandler : BaseHandler
{
    private const string FACTORY_COMMAND_MANUAL_FILE_NAME = "commandsBuiltIn.md";
    private const string CUSTOM_COMMAND_MANUAL_FILE_NAME = "commandsCustom.md";
    public const string COMMAND_ALIAS_DIVIDER = "::";

    private readonly CustomCommandHandler _customCommandHandler;
    private readonly List<CommandMetaInfo> _customCommands = [];
    private readonly List<CommandMetaInfo> _factoryCommands = [];

    public ChannelMessage CommandPageLinkMessage;
    public ChannelMessage CustomCommandHelpMessage;
    public ChannelMessage FactoryCommandHelpMessage;


    public CommandInfoHandler(Server server, CustomCommandHandler commandHandler) : base(server)
    {
        _customCommandHandler = commandHandler;
    }

    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();
        await InitializeCommandInfoHandler();
    }

    public override async Task OnCheckIntegrity()
    {
        await base.OnCheckIntegrity();
        await CheckIntegrity();
    }

    private async Task InitializeCommandInfoHandler()
    {
        await CollectCommandInfo();
        await CollectCustomCommands();
        await CreateHelpListStringsAsync();

        await ExportCustomCommandInfoPage();
        await ExportFactoryCommandInfoPage();

        CommandPageLinkMessage = new ChannelMessage(null).SetTemplate(ChannelMessage.MessageTemplateOption.Information).AddContent(new ChannelMessageContent().SetTitle("All Commands").SetDescription(Server.Config.GeneralSettings.CommandPageLink));
    }

    private async Task CheckIntegrity()
    {
        // Is there even anything to check here?
        await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Command Info OK."), (int)ConsoleColor.DarkGreen);
    }


    private Task CollectCommandInfo()
    {
        Type[] types = Assembly.GetExecutingAssembly().GetTypes();

        Dictionary<Type, List<PreconditionAttribute>> classPreconditions = new();
        Dictionary<Type, List<string>> classGroups = new();

        //Get Precondition Attributes on classes
        foreach (Type type in types)
        {
            if (type.BaseType != typeof(ModuleBase<ICommandContext>) && type.BaseType != typeof(ModuleBase<CommandContext>) && type.BaseType != typeof(ModuleBase<SocketCommandContext>))
            {
                continue;
            }

            foreach (Attribute attribute in type.GetCustomAttributes())
            {
                switch (attribute)
                {
                    case PreconditionAttribute pAttr:
                        if (classPreconditions.ContainsKey(type))
                        {
                            classPreconditions[type].Add(pAttr);
                        }
                        else
                        {
                            classPreconditions[type] = [pAttr];
                        }

                        break;

                    case GroupAttribute gAttr:
                        if (classGroups.TryGetValue(type, out List<string> group))
                        {
                            group[0] += $"{COMMAND_ALIAS_DIVIDER}{gAttr.Prefix}"; // Add as alias
                        }
                        else
                        {
                            classGroups[type] = [gAttr.Prefix];
                        }

                        break;

                    case AliasAttribute aAttr:
                        if (classGroups.TryGetValue(type, out List<string> classGroup))
                        {
                            // Add alias for command
                            foreach (string a in aAttr.Aliases)
                            {
                                classGroup[0] += $"{COMMAND_ALIAS_DIVIDER}{a}";
                            }
                        }

                        break;
                }
            }
        }

        //Get Precondition Attributes on methods
        foreach (Type type in types)
        {
            List<CommandMetaInfo> classCommands = new();

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                CommandMetaInfo entry = null;

                foreach (Attribute attribute in method.GetCustomAttributes())
                {
                    switch (attribute)
                    {
                        case CommandAttribute cAttr:
                            if (entry == null)
                            {
                                entry = new CommandMetaInfo { Name = cAttr.Text };
                            }
                            else
                            {
                                entry.Name = cAttr.Text;
                            }

                            break;

                        case SummaryAttribute sAttr:
                            if (entry == null)
                            {
                                entry = new CommandMetaInfo { Summary = sAttr.Text };
                            }
                            else
                            {
                                entry.Summary = sAttr.Text;
                            }

                            break;

                        case PreconditionAttribute pAttr:
                            if (entry == null)
                            {
                                entry = new CommandMetaInfo { Preconditions = [pAttr] };
                            }
                            else
                            {
                                entry.Preconditions.Add(pAttr);
                            }

                            break;
                    }
                }

                if (entry == null)
                {
                    continue;
                }

                if (classPreconditions.TryGetValue(type, out List<PreconditionAttribute> preconditions))
                {
                    entry.Preconditions.AddRange(preconditions);
                }

                if (classGroups.TryGetValue(type, out List<string> groups))
                {
                    entry.Groups.AddRange(groups);
                }

                if (type.IsNestedPublic)
                {
                    Type tempType = type;

                    // Check nesting until certain height, or top is reached
                    for (int i = 0; i < 5; i++)
                    {
                        Type enclosingType = null;

                        //Find enclosing type
                        foreach (Type t in types)
                        {
                            foreach (Type nt in t.GetNestedTypes())
                            {
                                if (nt != tempType)
                                {
                                    continue;
                                }

                                enclosingType = t;
                                break;
                            }

                            if (enclosingType != null)
                            {
                                break;
                            }
                        }

                        if (enclosingType == null)
                        {
                            break;
                        }

                        //Get Preconditions from enclosing class
                        if (classPreconditions.TryGetValue(enclosingType, out List<PreconditionAttribute> nestedPreconditions))
                        {
                            entry.Preconditions.InsertRange(0, nestedPreconditions);
                        }

                        //Get Groups from enclosing class
                        if (classGroups.TryGetValue(enclosingType, out List<string> nestedGroups))
                        {
                            entry.Groups.InsertRange(0, nestedGroups);
                        }

                        tempType = enclosingType;
                    }
                }

                entry.Parameters.AddRange(method.GetParameters());
                entry.CreateRuntimeParameters();

                classCommands.Add(entry);
            }

            _factoryCommands.AddRange(classCommands);
        }


        //Sort By Name
        _factoryCommands.Sort((c1, c2) => c1.FullName.CompareTo(c2.FullName));
        return Task.CompletedTask;
    }


    private static string PreconditionAttributeToString(PreconditionAttribute precondition)
    {
        string attrDescription = precondition.ToString();

        if (precondition is RequireBotPermissionAttribute rbpa)
        {
            attrDescription = "Bot Permission";
        }
        else if (precondition is RequireContextAttribute rca)
        {
            attrDescription = $"Context: {rca.Contexts}";
        }
        else if (precondition is RequireNsfwAttribute nsfwa)
        {
            attrDescription = "NSFW Channel";
        }
        else if (precondition is RequireOwnerAttribute roa)
        {
            attrDescription = "Owner Role";
        }
        else if (precondition is RequireUserPermissionAttribute rra)
        {
            attrDescription = $"User Permission: '{rra.GuildPermission}'";
        }
        else if (precondition is RequireTimeJoined rtj)
        {
            attrDescription = $"Time Joined: '{rtj.TimeJoined}'";
        }
        else if (precondition is RequireUserPermissionAttribute upa)
        {
            attrDescription = $"Permission: '{upa.ChannelPermission.Value}'";
        }
        else if (precondition is RequireTwitchBadge rtb)
        {
            attrDescription = $"Twitch Badge: '{rtb.RequiredBadges}'";
        }
        else if (precondition is RequireIsBot isBot)
        {
            attrDescription = "Bot";
        }
        else if (precondition is RequireIsFollower isFollower)
        {
            attrDescription = "Twitch Follower";
        }

        return attrDescription;
    }

    public Task CollectCustomCommands()
    {
        _customCommands.Clear();

        foreach (CustomCommand cmd in _customCommandHandler.CustomCommands.Commands)
        {
            _customCommands.Add(new CommandMetaInfo { Name = cmd.Name, IsCustomCommand = true });
        }

        return Task.CompletedTask;
    }

    public Task<CustomRuntimeResult<CommandMetaInfo>> GetCommandInfoAsync(string commandName, bool isCustomCommand = false)
    {
        return Task.Run(() =>
        {
            commandName = commandName.TrimStart().TrimEnd();

            if (!isCustomCommand)
            {
                string[] splitName = commandName.Split(' ');

                for (int i = 0; i < _factoryCommands.Count; i++)
                {
                    if (splitName.Length != _factoryCommands[i].Groups.Count + (_factoryCommands[i].Name != null ? 1 : 0))
                    {
                        continue;
                    }

                    bool failed = false;

                    for (int j = 0; j < _factoryCommands[i].Groups.Count; j++)
                    {
                        string[] splitGroup = _factoryCommands[i].Groups[j].Split(COMMAND_ALIAS_DIVIDER);

                        if (splitGroup.Contains(splitName[j], StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        failed = true;
                        break;
                    }

                    if (failed)
                    {
                        continue;
                    }

                    if (_factoryCommands[i].Name != null && !_factoryCommands[i].Name.Equals(splitName[^1], StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return CustomRuntimeResult<CommandMetaInfo>.FromSuccess(value: _factoryCommands[i]);
                }
            }

            for (int i = 0; i < _customCommands.Count; i++)
            {
                if (commandName.Equals(_customCommands[i].Name, StringComparison.OrdinalIgnoreCase))
                {
                    return CustomRuntimeResult<CommandMetaInfo>.FromSuccess(value: _customCommands[i]);
                }
            }

            return CustomRuntimeResult<CommandMetaInfo>.FromError($"{ReplyDictionary.COULD_NOT_FIND_COMMAND_NAMED} '{commandName}'");
        });
    }

    public Task<CustomRuntimeResult<CommandMetaInfo[]>> GetCommandsInGroupAsync(string[] groupQuery)
    {
        return Task.Run(() =>
        {
            var commands = new List<CommandMetaInfo>();

            for (int i = 0; i < _factoryCommands.Count; i++)
            {
                if (_factoryCommands[i].Groups.Count < groupQuery.Length)
                {
                    continue;
                }

                bool failed = false;
                for (int j = 0; j < groupQuery.Length; j++)
                {
                    string[] aliases = _factoryCommands[i].Groups[j].Split(COMMAND_ALIAS_DIVIDER);

                    if (aliases.Contains(groupQuery[j], StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    failed = true;
                    break;
                }

                if (failed)
                {
                    continue;
                }

                commands.Add(_factoryCommands[i]);
            }

            if (commands.Count > 0)
            {
                return CustomRuntimeResult<CommandMetaInfo[]>.FromSuccess(value: commands.ToArray());
            }


            StringBuilder groupName = new();

            if (groupQuery != null)
            {
                for (int i = 0; i < groupQuery.Length; i++)
                {
                    groupName.Append($"{groupQuery[i]} ");
                }
            }

            return CustomRuntimeResult<CommandMetaInfo[]>.FromError($"{ReplyDictionary.COULD_NOT_FIND_COMMANDS_IN_GROUP} '{groupName}'");
        });
    }

    public async Task<string> GetCommandInfoStringAsync(CommandMetaInfo command, int infoLevel = 0)
    {
        StringBuilder bodyBuilder = new();
        bodyBuilder.Append("!");
        {
            if (infoLevel < 1)
            {
                //Groups or Name
                bodyBuilder.Append($"{(command.Groups?.Count > 0 ? command.Groups[0] : command.Name)}");
            }
            else
            {
                for (int i = 0; i < infoLevel; i++)
                {
                    if (i < command.Groups.Count)
                    {
                        bodyBuilder.Append($"{command.Groups[i]} ");
                    }
                    else
                    {
                        bodyBuilder.Append(command.Name);
                        break;
                    }
                }

                int remainingLevel = infoLevel - (command.Groups.Count + 1); // + 1 for command name
                if (remainingLevel > 0)
                {
                    if (command.IsCustomCommand)
                    {
                        if ((await _customCommandHandler.GetCommandAsync(command.Name)).ResultValue is { } customCommand)
                        {
                            bodyBuilder.Append($" | {customCommand.CostsToString()}");

                            if (remainingLevel > 1)
                            {
                                bodyBuilder.Append($" | {ReplyDictionary.CATEGORY}: {(customCommand.Category == null ? "-" : customCommand.Category.ToString())}");

                                if (remainingLevel > 2)
                                {
                                    bodyBuilder.AppendLine($"\n{ReplyDictionary.ACTIONS} [{(customCommand.Embed ? "embed" : "plain")} | {(customCommand.ShowCommandInfo ? "" : "no")} info]:");
                                    bodyBuilder.AppendLine(customCommand.ActionsToString());
                                }
                            }
                        }
                    }
                    else
                    {
                        // Summary
                        bodyBuilder.Append($" ({command.Summary})");

                        if (remainingLevel > 1)
                        {
                            //Parameters
                            StringBuilder extendedBuilder = new(" | Parameters: ");

                            ParameterInfoCollection[] parameterInfos = await GetParameterInfosAsync(command);

                            if (parameterInfos?.Length > 0)
                            {
                                foreach (ParameterInfoCollection parameter in parameterInfos)
                                {
                                    extendedBuilder.Append($"{(parameter.IsOptional ? "(Optional)" : "")}");
                                    extendedBuilder.Append($"{parameter.Type} {parameter.Name}");
                                    if (!string.IsNullOrWhiteSpace(parameter.Summary))
                                    {
                                        extendedBuilder.Append($" ({parameter.Summary})");
                                    }

                                    extendedBuilder.Append(", ");
                                }

                                extendedBuilder.Remove(extendedBuilder.Length - 2, 2);
                            }
                            else
                            {
                                extendedBuilder.Append("-");
                            }

                            bodyBuilder.Append(extendedBuilder.ToString());


                            //Preconditions
                            extendedBuilder = new StringBuilder(" | Preconditions: ");

                            if (command.Preconditions.Count > 0)
                            {
                                foreach (PreconditionAttribute precondition in command.Preconditions)
                                {
                                    string pString = PreconditionAttributeToString(precondition);
                                    extendedBuilder.Append($"[{pString}], ");
                                }

                                extendedBuilder.Remove(extendedBuilder.Length - 2, 2);
                            }
                            else
                            {
                                extendedBuilder.Append("-");
                            }


                            bodyBuilder.Append(extendedBuilder.ToString());
                        }
                    }
                }
            }
        }


        return bodyBuilder.ToString();
    }

    public async Task<CustomRuntimeResult<CommandMetaInfo[]>> ParseToSerializableCommandInfo(string[] commands, ICommandContext context)
    {
        // Merge commands that contain arrays as parameter and thus got split by the ArrayReader on accident
        CustomRuntimeResult<string[]> bundleResult = await BundleGroups(commands, '\0', ')', ArrayReader.DEFAULT_ELEMENT_SEPERATOR);
        if (bundleResult.IsSuccess)
        {
            commands = bundleResult.ResultValue;
        }
        else
        {
            return CustomRuntimeResult<CommandMetaInfo[]>.FromError(bundleResult.Reason);
        }

        var cmdInfos = new List<CommandMetaInfo>();

        for (int i = 0; i < commands.Length; i++)
        {
            TypeReaderResult paramUnpackResult = await ArrayReader.SplitToArray(commands[i], new[] { '(', ')' });
            string[] splitCommand = (string[])paramUnpackResult.BestMatch;

            if (!paramUnpackResult.IsSuccess || splitCommand == null || splitCommand.Length < 1)
            {
                return CustomRuntimeResult<CommandMetaInfo[]>.FromError($"{await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_PROCESS_COMMAND_WITH_NAME_X, "{x}", commands[i])}\n{paramUnpackResult.ErrorReason}");
            }


            CustomRuntimeResult<CommandMetaInfo> taskResult = await GetCommandInfoAsync(splitCommand[0]);

            if (!taskResult.IsSuccess)
            {
                return CustomRuntimeResult<CommandMetaInfo[]>.FromError(taskResult.Reason);
            }

            //Since it is no struct, create copy manually
            CommandMetaInfo command = new(taskResult.ResultValue);

            if (!command.IsCustomCommand)
            {
                CommandInfo commandInfo = null;

                // Find matching commandservice commandinfo
                foreach (CommandInfo ci in Server.CommandService.Commands)
                {
                    if (ci.Parameters.Count != command.Parameters.Count)
                    {
                        continue;
                    }

                    bool match = false;

                    if (command.Name == null || !command.Name.Equals(ci.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        for (int a = 0; a < ci.Aliases?.Count; a++)
                        {
                            if (command.Name == null)
                            {
                                if (!command.FullName.Contains(ci.Aliases[a], StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                match = true;
                                break;
                            }

                            if (command.FullName.Equals(ci.Aliases[a], StringComparison.OrdinalIgnoreCase))
                            {
                                match = true;
                                break;
                            }
                        }

                        if (!match)
                        {
                            continue;
                        }
                    }

                    if (ci.Parameters.Count > 0)
                    {
                        match = true;
                        for (int p = 0; p < ci.Parameters.Count; p++)
                        {
                            if (ci.Parameters[p].Type == command.Parameters[p].ParameterType)
                            {
                                continue;
                            }

                            match = false;
                            break;
                        }

                        if (!match)
                        {
                            continue;
                        }
                    }

                    commandInfo = ci;
                    break;
                }

                if (commandInfo == null)
                {
                    return CustomRuntimeResult<CommandMetaInfo[]>.FromError($"{await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_FIND_DISCORD_COMMAND_INFO_FOR_COMMAND_NAMED_X, "{x}", command.FullName)}");
                }

                PreconditionResult preconditionCheckResult = await commandInfo.CheckPreconditionsAsync(context, Server.Services);
                if (!preconditionCheckResult.IsSuccess)
                {
                    return CustomRuntimeResult<CommandMetaInfo[]>.FromError(preconditionCheckResult.ErrorReason);
                }

                int minRequiredParameters = 0;
                command.Parameters.ForEach(p => minRequiredParameters += p.IsOptional ? 0 : 1);

                if (splitCommand.Length - 1 < minRequiredParameters)
                {
                    return CustomRuntimeResult<CommandMetaInfo[]>.FromError($"{command.FullName} => {await ReplyDictionary.ReplaceStringInvariantCase(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.PARAMETER_COUNT_X_DOES_NOT_MATCH_REQUIRED_COUNT_Y, "{x}", (splitCommand.Length - 1).ToString()), "{y}", $">={minRequiredParameters}")}");
                }

                if (splitCommand.Length - 1 > command.Parameters.Count)
                {
                    return CustomRuntimeResult<CommandMetaInfo[]>.FromError($"{command.FullName} => {await ReplyDictionary.ReplaceStringInvariantCase(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.PARAMETER_COUNT_X_DOES_NOT_MATCH_REQUIRED_COUNT_Y, "{x}", (splitCommand.Length - 1).ToString()), "{y}", command.Parameters.Count.ToString())}");
                }

                for (int j = 0; j < command.RuntimeParameters.Length; j++)
                {
                    RuntimeParameterInfo parameterInfo = command.RuntimeParameters[j];

                    try
                    {
                        if (parameterInfo.Type == typeof(string[]))
                        {
                            parameterInfo.Value = splitCommand[j + 1];
                        }
                        else
                        {
                            parameterInfo.Value = TypeDescriptor.GetConverter(parameterInfo.Type).ConvertFromString(splitCommand[j + 1]);
                        }
                    }
                    catch
                    {
                        if (!parameterInfo.IsOptional)
                        {
                            return CustomRuntimeResult<CommandMetaInfo[]>.FromError($"{await ReplyDictionary.ReplaceStringInvariantCase(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_PARSE_X_TO_Y, "{x}", splitCommand[j + 1]), "{y}", parameterInfo.Type.Name)}");
                        }
                    }
                }
            }

            cmdInfos.Add(command);
        }

        return CustomRuntimeResult<CommandMetaInfo[]>.FromSuccess(value: cmdInfos.ToArray());
    }

    private static async Task<CustomRuntimeResult<string[]>> BundleGroups(string[] input, char startIdentifier, char endIdentifier, char groupElementDivider = '\0', char wildcard = '\\', bool removeIdentifiersFromResult = true)
    {
        // e.g. startIdentifier = '(' , endIdentifier = ')' , endOfGroupAddition = '*'
        // input: string[] { p1 , (p2.0 , p2.1 , p2.2) , p3 , (p4.0 , p4.1) , p5 }
        // result: string[] { p1 , p2.0 * p.2.1 * p2.2 , p3 , p4.0 * p4.1 , p5 };

        if (input?.Length > 0)
        {
            int groupStartIndex = -1;
            int groupEndIndex = -1;
            var merged = new List<string>();
            StringBuilder arrayStringBuilder = new();

            for (int j = 0; j < input.Length; j++)
            {
                for (int k = 0; k < input[j].Length; k++)
                {
                    // Skip Wildcards
                    if (input[j][k] == wildcard)
                    {
                        k++;
                        continue;
                    }

                    if (groupStartIndex < 0)
                    {
                        if (startIdentifier == '\0' || input[j][k] == startIdentifier)
                        {
                            groupStartIndex = j;
                        }
                    }
                    else
                    {
                        if (input[j][k] == endIdentifier)
                        {
                            groupEndIndex = j;
                        }
                    }
                }

                if (groupStartIndex < 0 && groupEndIndex < 0)
                {
                    merged.Add(input[j]);
                    continue;
                }

                if (groupStartIndex > -1 && groupEndIndex > -1)
                {
                    if (removeIdentifiersFromResult)
                    {
                        input[groupStartIndex] = input[groupStartIndex].Replace(startIdentifier.ToString(), "");
                        input[groupEndIndex] = input[groupEndIndex].Replace(endIdentifier.ToString(), "");
                    }

                    for (int l = groupStartIndex; l <= groupEndIndex; l++)
                    {
                        arrayStringBuilder.Append($"{input[l]}{(l < groupEndIndex ? groupElementDivider.ToString() : "")}");
                    }

                    merged.Add(arrayStringBuilder.ToString());

                    groupStartIndex = -1;
                    groupEndIndex = -1;
                    arrayStringBuilder.Clear();
                }
            }

            if (groupStartIndex > -1 && groupEndIndex < 0)
            {
                return CustomRuntimeResult<string[]>.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.GROUP_IS_MISSING_END_IDENTIFIER_X, "{x}", endIdentifier.ToString()));
            }

            if (groupStartIndex < 0 && groupEndIndex > -1)
            {
                return CustomRuntimeResult<string[]>.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.GROUP_IS_MISSING_START_IDENTIFIER_X, "{x}", startIdentifier.ToString()));
            }

            return CustomRuntimeResult<string[]>.FromSuccess(value: merged.ToArray());
        }

        return CustomRuntimeResult<string[]>.FromSuccess(value: null);
    }


    public async Task CreateHelpListStringsAsync()
    {
        FactoryCommandHelpMessage = new ChannelMessage(null).SetTemplate(ChannelMessage.MessageTemplateOption.Information);

        // BUILT IN
        var lines = new List<string>();
        StringBuilder bodyBuilder = new();

        foreach (CommandMetaInfo command in _factoryCommands)
        {
            string s = await GetCommandInfoStringAsync(command);
            if (lines.Contains(s))
            {
                continue;
            }

            lines.Add(s);
            bodyBuilder.Append($"{s} | ");
        }

        if (bodyBuilder.Length > 2)
        {
            bodyBuilder.Remove(bodyBuilder.Length - 2, 2);
        }

        ChannelMessageContent cmdMsgContent = new ChannelMessageContent()
                                              .SetTitle("Factory Commands")
                                              .SetDescription(bodyBuilder.ToString());

        FactoryCommandHelpMessage.Contents.Add(cmdMsgContent);
        await Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(CreateHelpListStringsAsync), cmdMsgContent.ToString()));


        //CUSTOM            
        bodyBuilder.Clear();
        CustomCommandHelpMessage = new ChannelMessage(null).SetTemplate(ChannelMessage.MessageTemplateOption.Information);
        cmdMsgContent = new ChannelMessageContent().SetTitle("Custom Commands");

        if (_customCommands.Count == 0)
        {
            cmdMsgContent = cmdMsgContent.SetDescription("-");
            CustomCommandHelpMessage.AddContent(cmdMsgContent);
            return;
        }

        string currCategory = "undefined";

        for (int i = 0; i < _customCommands.Count; i++)
        {
            if (!((await _customCommandHandler.GetCommandAsync(_customCommands[i].Name)).ResultValue is { } cuco))
            {
                continue;
            }

            string catName = cuco.Category != null ? cuco.Category.Name : "undefined";

            if (catName.Equals(currCategory, StringComparison.OrdinalIgnoreCase))
            {
                bodyBuilder.Append($"{await GetCommandInfoStringAsync(_customCommands[i])} | ");
                continue;
            }

            if (bodyBuilder.Length > 3)
            {
                cmdMsgContent = cmdMsgContent.SetDescription(bodyBuilder.ToString().TrimStart('\n').TrimEnd().TrimEnd('|'));
            }
            else
            {
                cmdMsgContent = cmdMsgContent.SetDescription("-");
            }

            CustomCommandHelpMessage.Contents.Add(cmdMsgContent);
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(CreateHelpListStringsAsync), cmdMsgContent.ToString()));

            currCategory = catName;
            cmdMsgContent = new ChannelMessageContent().SetTitle(currCategory);

            bodyBuilder.Clear();
            i--;
        }

        // Flush rest
        if (bodyBuilder.Length > 3)
        {
            cmdMsgContent = cmdMsgContent.SetDescription(bodyBuilder.ToString().TrimStart('\n').TrimEnd().TrimEnd('|'));
        }
        else
        {
            cmdMsgContent = cmdMsgContent.SetDescription("-");
        }

        CustomCommandHelpMessage.Contents.Add(cmdMsgContent);
        await Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(CreateHelpListStringsAsync), cmdMsgContent.ToString()));
    }

    private static Task<ParameterInfoCollection[]> GetParameterInfosAsync(CommandMetaInfo command)
    {
        return Task.Run(() =>
        {
            var parameterInfos = new List<ParameterInfoCollection>();

            if (command.Parameters.Count > 0)
            {
                foreach (ParameterInfo parameter in command.Parameters)
                {
                    StringBuilder summaryBuilder = new();
                    foreach (Attribute pAttr in parameter.GetCustomAttributes())
                    {
                        if (pAttr is SummaryAttribute pInfo)
                        {
                            summaryBuilder.Append(pInfo.Text);
                            break;
                        }
                    }

                    if (parameter.ParameterType.IsEnum)
                    {
                        summaryBuilder.Append("(");

                        string[] names = Enum.GetNames(parameter.ParameterType);
                        for (int i = 0; i < names.Length; i++)
                        {
                            object parsed = Enum.Parse(parameter.ParameterType, names[i]);

                            summaryBuilder.Append($"{names[i]}{(parsed != null ? $"={(int)parsed}" : "")}");

                            if (i < names.Length - 1)
                            {
                                summaryBuilder.Append(", ");
                            }
                        }

                        summaryBuilder.Append(")");
                    }
                    else if (parameter.ParameterType.IsArray)
                    {
                        char seperator = ArrayReader.DEFAULT_ELEMENT_SEPERATOR;
                        summaryBuilder.Append($"(e.g. \"a{seperator}b{seperator}c{seperator}d\")");
                    }

                    parameterInfos.Add(new ParameterInfoCollection(parameter.Name, parameter.ParameterType.Name, parameter.IsOptional, summaryBuilder.ToString()));
                }
            }

            return parameterInfos.ToArray();
        });
    }


    private async Task ExportCustomCommandInfoPage()
    {
        StringBuilder builder = new();

        builder.AppendLine("Custom Commands".AsMarkdown(MarkdownOption.H1));
        builder.AppendLine($"Dynamic commands for the '{Server.Config.TwitchSettings.TwitchChannelName}'/'{Server.RuntimeConfig.GuildName}' twitch/discord channels.".AsMarkdown(MarkdownOption.Italic));
        builder.AppendLine();

        string currentCategory = string.Empty;

        for (int i = 0; i < _customCommands.Count; i++)
        {
            CustomRuntimeResult<CustomCommand> getCommandResult = await _customCommandHandler.GetCommandAsync(_customCommands[i].Name);

            if (getCommandResult.IsSuccess && getCommandResult.ResultValue is { } customCommand)
            {
                string cmdCat = customCommand.Category?.Name ?? "No Category";

                if (cmdCat != currentCategory)
                {
                    currentCategory = cmdCat;
                    builder.AppendLine(currentCategory.AsMarkdown(MarkdownOption.H2));
                    builder.AppendLine();
                }

                float cooldownInSeconds = customCommand.CooldownInSeconds;
                int priceTag = customCommand.PriceTag;

                if (customCommand.Category != null)
                {
                    if (customCommand.Category.CategoryCooldownInSeconds > cooldownInSeconds)
                    {
                        cooldownInSeconds = customCommand.Category.CategoryCooldownInSeconds;
                    }

                    if (customCommand.Category.PriceTag > priceTag)
                    {
                        priceTag = customCommand.Category.PriceTag;
                    }
                }

                AppendCommandInfo(builder, true, _customCommands[i], cooldownInSeconds, priceTag, customCommand.ActionsToArray(), null, null);

                builder.AppendLine();
            }
            else
            {
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(ExportCustomCommandInfoPage), $"Could not get Custom Command: {getCommandResult.Reason}"));
            }
        }

        await HTMLSerializer.SaveTextToFile(builder, Server.ServerFilesDirectoryPath, CUSTOM_COMMAND_MANUAL_FILE_NAME);
    }

    private async Task ExportFactoryCommandInfoPage()
    {
        StringBuilder builder = new();

        builder.AppendLine("Factory Commands".AsMarkdown(MarkdownOption.H1));
        builder.AppendLine("Built-in commands of the 'Geist des Waldes' Disord/Twitch Bot.".AsMarkdown(MarkdownOption.Italic));
        builder.AppendLine();

        string currentGroup = string.Empty;

        for (int i = 0; i < _factoryCommands.Count; i++)
        {
            string cmdGroup = _factoryCommands[i].Groups.Count > 0 ? _factoryCommands[i].Groups[0] : _factoryCommands[i].Name;

            if (cmdGroup != currentGroup)
            {
                currentGroup = cmdGroup;
                builder.AppendLine(currentGroup.AsMarkdown(MarkdownOption.H2));
                builder.AppendLine();
            }

            float cooldownDuration = 0f;
            int priceTagAmount = 0;

            CommandCooldown cooldownPrecondition = (CommandCooldown)_factoryCommands[i].Preconditions.FirstOrDefault(p => p.GetType() == typeof(CommandCooldown));
            if (cooldownPrecondition != default)
            {
                cooldownDuration = cooldownPrecondition.CooldownInSeconds;
            }

            CommandFee feePrecondition = (CommandFee)_factoryCommands[i].Preconditions.FirstOrDefault(c => c.GetType() == typeof(CommandFee));
            if (feePrecondition != default)
            {
                priceTagAmount = feePrecondition.PriceTag;
            }

            ParameterInfoCollection[] parameterInfos = await GetParameterInfosAsync(_factoryCommands[i]);
            PreconditionAttribute[] preconditions = _factoryCommands[i].Preconditions.ToArray();

            AppendCommandInfo(builder, false, _factoryCommands[i], cooldownDuration, priceTagAmount, null, parameterInfos, preconditions);

            builder.AppendLine();
        }

        await HTMLSerializer.SaveTextToFile(builder, Server.ServerFilesDirectoryPath, FACTORY_COMMAND_MANUAL_FILE_NAME);
    }


    private void AppendCommandInfo(StringBuilder builder, bool isCustomCommand, CommandMetaInfo command, float cooldownDuration, int priceTagAmount, string[] actions, ParameterInfoCollection[] parameterInfos, PreconditionAttribute[] preconditions)
    {
        builder.AppendLine($"{Server.Config.GeneralSettings.CommandPrefix}{command.FullName} | {Utility.CreateCostsString(cooldownDuration, priceTagAmount)}".AsMarkdown(MarkdownOption.H3));
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(command.Summary))
        {
            builder.AppendLine(command.Summary.AsMarkdown(MarkdownOption.Italic));
            builder.AppendLine();
        }

        if (isCustomCommand)
        {
            AppendActionString(builder, actions);
        }
        else
        {
            AppendParameterInfoString(builder, parameterInfos);

            builder.AppendLine();

            AppendPreconditionString(builder, preconditions);
        }

        builder.AppendLine();
    }

    private static void AppendActionString(StringBuilder builder, string[] actions)
    {
        builder.AppendLine("Actions: ");

        if (actions?.Length > 0)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                builder.AppendLine(actions[i].AsMarkdown(MarkdownOption.ListSorted));
            }
        }
        else
        {
            builder.Append(@" \- ");
        }

        builder.AppendLine();
    }

    private static void AppendParameterInfoString(StringBuilder builder, ParameterInfoCollection[] parameterInfos)
    {
        builder.AppendLine("Parameters: ");

        if (parameterInfos?.Length > 0)
        {
            for (int i = 0; i < parameterInfos.Length; i++)
            {
                builder.Append(string.Empty.AsMarkdown(MarkdownOption.ListSorted));

                ParameterInfoCollection pInfo = parameterInfos[i];

                if (pInfo.IsOptional)
                {
                    builder.Append("[Optional] ");
                }

                builder.Append($"{pInfo.Type.AsMarkdown(MarkdownOption.Bold)} {pInfo.Name}");

                if (!string.IsNullOrWhiteSpace(pInfo.Summary))
                {
                    builder.Append($" ({pInfo.Summary.AsMarkdown(MarkdownOption.Italic)})");
                }

                builder.AppendLine();
            }
        }
        else
        {
            builder.Append(@" \- ");
        }

        builder.AppendLine();
    }

    private static void AppendPreconditionString(StringBuilder builder, PreconditionAttribute[] attributes)
    {
        builder.AppendLine("Preconditions: ");

        if (attributes?.Length > 0)
        {
            Dictionary<string, List<PreconditionAttribute>> groupAttributeMap = new();

            for (int i = 0; i < attributes.Length; i++)
            {
                string grp = attributes[i].Group ?? "None";

                if (groupAttributeMap.TryGetValue(grp, out List<PreconditionAttribute> atts))
                {
                    atts.Add(attributes[i]);
                }
                else
                {
                    groupAttributeMap.Add(grp, new List<PreconditionAttribute> { attributes[i] });
                }
            }

            foreach ((string _, List<PreconditionAttribute> preconditions) in groupAttributeMap)
            {
                builder.Append(string.Empty.AsMarkdown(MarkdownOption.ListSorted));

                for (int i = 0; i < preconditions.Count; i++)
                {
                    if (i != 0)
                    {
                        builder.Append(" OR ");
                    }

                    builder.Append(PreconditionAttributeToString(preconditions[i]));
                }

                builder.AppendLine();
            }
        }
        else
        {
            builder.Append(@" \- ");
            builder.AppendLine();
        }
    }
}

internal class ParameterInfoCollection
{
    public bool IsOptional;
    public string Name;
    public string Summary;
    public string Type;

    public ParameterInfoCollection(string name, string type, bool optional, string summary)
    {
        Name = name;
        Type = type;
        IsOptional = optional;
        Summary = summary;
    }
}