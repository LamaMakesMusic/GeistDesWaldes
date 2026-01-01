using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.UserCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GeistDesWaldes.CommandMeta
{
    public class CommandInfoHandler : BaseHandler
    {
        public List<CommandMetaInfo> FactoryCommands;
        public List<CommandMetaInfo> CustomCommands;

        public ChannelMessage CommandPageLinkMessage;
        public ChannelMessage CustomCommandHelpMessage;
        public ChannelMessage FactoryCommandHelpMessage;

        private const string FACTORY_COMMAND_MANUAL_FILE_NAME = "commandsBuiltIn.md";
        private const string CUSTOM_COMMAND_MANUAL_FILE_NAME = "commandsCustom.md";
        public const string COMMAND_ALIAS_DIVIDER = "::";


        public CommandInfoHandler(Server server) : base(server)
        {
            FactoryCommands = new List<CommandMetaInfo>();
            CustomCommands = new List<CommandMetaInfo>();
        }


        internal override void OnServerStart(object source, EventArgs e)
        {
            base.OnServerStart(source, e);

            Task.Run(InitializeCommandInfoHandler).GetAwaiter().GetResult();
        }
        internal override void OnCheckIntegrity(object source, EventArgs e)
        {
            base.OnCheckIntegrity(source, e);

            Task.Run(CheckIntegrity).GetAwaiter().GetResult();
        }

        private async Task InitializeCommandInfoHandler()
        {
            await CollectCommandInfo();
            await CollectCustomCommands();
            await CreateHelpListStringsAsync();
            
            await ExportCustomCommandInfoPage();
            await ExportFactoryCommandInfoPage();

            CommandPageLinkMessage = new ChannelMessage(null).SetTemplate(ChannelMessage.MessageTemplateOption.Information).AddContent(new ChannelMessageContent().SetTitle("All Commands").SetDescription(_Server.Config.GeneralSettings.CommandPageLink));
        }

        private async Task CheckIntegrity()
        {
            // Is there even anything to check here?
            await _Server.LogHandler.Log(new Discord.LogMessage(Discord.LogSeverity.Info, nameof(CheckIntegrity), "Command Info OK."), (int)ConsoleColor.DarkGreen);
        }


        private Task CollectCommandInfo()
        {
            return Task.Run(() =>
            {
                var types = Assembly.GetExecutingAssembly().GetTypes();

                Dictionary<Type, List<PreconditionAttribute>> classPreconditions = new Dictionary<Type, List<PreconditionAttribute>>();
                Dictionary<Type, List<string>> classGroups = new Dictionary<Type, List<string>>();

                //Get Precondition Attributes on classes
                foreach (Type type in types)
                {
                    if (type.BaseType != (typeof(ModuleBase<ICommandContext>)) && type.BaseType != (typeof(ModuleBase<CommandContext>)) && type.BaseType != (typeof(ModuleBase<SocketCommandContext>)))
                        continue;

                    foreach (Attribute attribute in type.GetCustomAttributes())
                    {
                        if (attribute is PreconditionAttribute pAttr)
                        {
                            if (classPreconditions.ContainsKey(type))
                                classPreconditions[type].Add(pAttr);
                            else
                                classPreconditions[type] = new List<PreconditionAttribute>() { pAttr };
                        }
                        else if (attribute is GroupAttribute gAttr)
                        {
                            if (classGroups.ContainsKey(type))
                                classGroups[type][0] += $"{COMMAND_ALIAS_DIVIDER}{gAttr.Prefix}"; // Add as alias
                            else
                                classGroups[type] = new List<string>() { gAttr.Prefix };
                        }
                        else if (attribute is AliasAttribute aAttr)
                        {
                            if (classGroups.ContainsKey(type))
                            {
                                // Add alias for command
                                foreach (string a in aAttr.Aliases)
                                    classGroups[type][0] += $"{COMMAND_ALIAS_DIVIDER}{a}";
                            }
                        }
                    }
                }

                //Get Precondition Attributes on methods
                foreach (Type type in types)
                {
                    List<CommandMetaInfo> classCommands = new List<CommandMetaInfo>();

                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        CommandMetaInfo entry = null;

                        foreach (var attribute in method.GetCustomAttributes())
                        {
                            if (attribute is CommandAttribute cAttr)
                            {
                                if (entry == null)
                                    entry = new CommandMetaInfo() { Name = cAttr.Text };
                                else
                                    entry.Name = cAttr.Text;
                            }
                            else if (attribute is SummaryAttribute sAttr)
                            {
                                if (entry == null)
                                    entry = new CommandMetaInfo() { Summary = sAttr.Text };
                                else
                                    entry.Summary = sAttr.Text;
                            }
                            else if (attribute is PreconditionAttribute pAttr)
                            {
                                if (entry == null)
                                    entry = new CommandMetaInfo() { Preconditions = new List<PreconditionAttribute>() { pAttr } };
                                else
                                    entry.Preconditions.Add(pAttr);
                            }
                        }

                        if (entry != null)
                        {
                            if (classPreconditions.ContainsKey(type))
                                entry.Preconditions.AddRange(classPreconditions[type]);

                            if (classGroups.ContainsKey(type))
                                entry.Groups.AddRange(classGroups[type]);

                            if (type.IsNestedPublic)
                            {
                                Type tempType = type;

                                // Check nesting until certain height, or top is reached
                                for (int i = 0; i < 5; i++)
                                {
                                    Type enclosingType = null;

                                    //Find enclosing type
                                    foreach (var t in types)
                                    {
                                        foreach (var nt in t.GetNestedTypes())
                                        {
                                            if (nt == tempType)
                                            {
                                                enclosingType = t;
                                                break;
                                            }
                                        }

                                        if (enclosingType != null)
                                            break;
                                    }

                                    if (enclosingType == null)
                                        break;

                                    //Get Preconditions from enclosing class
                                    if (classPreconditions.ContainsKey(enclosingType))
                                        entry.Preconditions.InsertRange(0, classPreconditions[enclosingType]);

                                    //Get Groups from enclosing class
                                    if (classGroups.ContainsKey(enclosingType))
                                        entry.Groups.InsertRange(0, classGroups[enclosingType]);

                                    tempType = enclosingType;
                                }
                            }

                            entry.Parameters.AddRange(method.GetParameters());
                            entry.CreateRuntimeParameters();

                            classCommands.Add(entry);
                        }
                    }


                    FactoryCommands.AddRange(classCommands);
                }


                //Sort By Name
                FactoryCommands.Sort((c1, c2) => c1.FullName.CompareTo(c2.FullName));
            });
        }


        private static string PreconditionAttributeToString(PreconditionAttribute precondition)
        {
            string attrDescription = precondition.ToString();

            if (precondition is RequireBotPermissionAttribute rbpa)
                attrDescription = "Bot Permission";
            else if (precondition is RequireContextAttribute rca)
                attrDescription = $"Context: {rca.Contexts}";
            else if (precondition is RequireNsfwAttribute nsfwa)
                attrDescription = $"NSFW Channel";
            else if (precondition is RequireOwnerAttribute roa)
                attrDescription = $"Owner Role";
            else if (precondition is RequireUserPermissionAttribute rra)
                attrDescription = $"User Permission: '{rra.GuildPermission}'";
            else if (precondition is RequireTimeJoined rtj)
                attrDescription = $"Time Joined: '{rtj.TimeJoined}'";
            else if (precondition is RequireUserPermissionAttribute upa)
                attrDescription = $"Permission: '{upa.ChannelPermission.Value}'";
            else if (precondition is RequireTwitchBadge rtb)
                attrDescription = $"Twitch Badge: '{rtb.RequiredBadges}'";
            else if (precondition is RequireIsBot isBot)
                attrDescription = "Bot";
            else if (precondition is RequireIsFollower isFollower)
                attrDescription = "Twitch Follower";

            return attrDescription;
        }

        public Task CollectCustomCommands()
        {
            return Task.Run(() =>
            {
                CustomCommands.Clear();

                for (int i = 0; i < _Server.CustomCommandHandler.CustomCommands.Commands.Count; i++)
                    CustomCommands.Add(new CommandMetaInfo() { Name = _Server.CustomCommandHandler.CustomCommands.Commands[i].Name, IsCustomCommand = true });
            });
        }

        public Task<CustomRuntimeResult<CommandMetaInfo>> GetCommandInfoAsync(string commandName, bool isCustomCommand = false)
        {
            return Task.Run(() =>
            {
                commandName = commandName.TrimStart().TrimEnd();

                if (!isCustomCommand)
                {
                    string[] splitName = commandName.Split(' ');

                    for (int i = 0; i < FactoryCommands.Count; i++)
                    {
                        if (splitName.Length != FactoryCommands[i].Groups.Count + (FactoryCommands[i].Name != null ? 1 : 0))
                            continue;

                        bool failed = false;

                        for (int j = 0; j < FactoryCommands[i].Groups.Count; j++)
                        {
                            string[] splitGroup = FactoryCommands[i].Groups[j].Split(COMMAND_ALIAS_DIVIDER);

                            if (splitGroup.Contains(splitName[j], StringComparer.OrdinalIgnoreCase))
                                continue;

                            failed = true;
                            break;
                        }

                        if (failed)
                            continue;

                        if (FactoryCommands[i].Name != null && !FactoryCommands[i].Name.Equals(splitName[^1], StringComparison.OrdinalIgnoreCase))
                            continue;

                        return CustomRuntimeResult<CommandMetaInfo>.FromSuccess(value: FactoryCommands[i]);
                    }
                }

                for (int i = 0; i < CustomCommands.Count; i++)
                {
                    if (commandName.Equals(CustomCommands[i].Name, StringComparison.OrdinalIgnoreCase))
                        return CustomRuntimeResult<CommandMetaInfo>.FromSuccess(value: CustomCommands[i]);
                }

                return CustomRuntimeResult<CommandMetaInfo>.FromError($"{ReplyDictionary.COULD_NOT_FIND_COMMAND_NAMED} '{commandName}'");
            });
        }
        public Task<CustomRuntimeResult<CommandMetaInfo[]>> GetCommandsInGroupAsync(string[] groupQuery)
        {
            return Task.Run(() =>
            {
                List<CommandMetaInfo> commands = new List<CommandMetaInfo>();

                for (int i = 0; i < FactoryCommands.Count; i++)
                {
                    if (FactoryCommands[i].Groups.Count < groupQuery.Length)
                        continue;

                    bool failed = false;
                    for (int j = 0; j < groupQuery.Length; j++)
                    {
                        string[] aliases = FactoryCommands[i].Groups[j].Split(COMMAND_ALIAS_DIVIDER);

                        if (aliases.Contains(groupQuery[j], StringComparer.OrdinalIgnoreCase))
                            continue;

                        failed = true;
                        break;
                    }

                    if (failed)
                        continue;

                    commands.Add(FactoryCommands[i]);
                }

                if (commands.Count > 0)
                    return CustomRuntimeResult<CommandMetaInfo[]>.FromSuccess(value: commands.ToArray());


                var groupName = new StringBuilder();

                if (groupQuery != null)
                {
                    for (int i = 0; i < groupQuery.Length; i++)
                        groupName.Append($"{groupQuery[i]} ");
                }

                return CustomRuntimeResult<CommandMetaInfo[]>.FromError($"{ReplyDictionary.COULD_NOT_FIND_COMMANDS_IN_GROUP} '{groupName}'");
            });
        }

        public async Task<string> GetCommandInfoStringAsync(CommandMetaInfo command, int infoLevel = 0)
        {
            StringBuilder bodyBuilder = new StringBuilder();
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
                            bodyBuilder.Append($"{command.Groups[i]} ");
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
                            if ((await _Server.CustomCommandHandler.GetCommandAsync(command.Name)).ResultValue is CustomCommand customCommand)
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
                                StringBuilder extendedBuilder = new StringBuilder($" | Parameters: ");

                                ParameterInfoCollection[] parameterInfos = await GetParameterInfosAsync(command);

                                if (parameterInfos?.Length > 0)
                                {
                                    foreach (var parameter in parameterInfos)
                                    {
                                        extendedBuilder.Append($"{(parameter.IsOptional ? "(Optional)" : "")}");
                                        extendedBuilder.Append($"{parameter.Type} {parameter.Name}");
                                        if (!string.IsNullOrWhiteSpace(parameter.Summary))
                                            extendedBuilder.Append($" ({parameter.Summary})");
                                        extendedBuilder.Append(", ");
                                    }

                                    extendedBuilder.Remove(extendedBuilder.Length - 2, 2);
                                }
                                else
                                    extendedBuilder.Append("-");

                                bodyBuilder.Append(extendedBuilder.ToString());


                                //Preconditions
                                extendedBuilder = new StringBuilder(" | Preconditions: ");

                                if (command.Preconditions.Count > 0)
                                {
                                    foreach (var precondition in command.Preconditions)
                                    {
                                        string pString = PreconditionAttributeToString(precondition);
                                        extendedBuilder.Append($"[{pString}], ");
                                    }

                                    extendedBuilder.Remove(extendedBuilder.Length - 2, 2);
                                }
                                else
                                    extendedBuilder.Append("-");


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
            var bundleResult = await BundleGroups(commands, '\0', ')', ArrayReader.DEFAULT_ELEMENT_SEPERATOR);
            if (bundleResult.IsSuccess)
                commands = bundleResult.ResultValue;
            else
                return CustomRuntimeResult<CommandMetaInfo[]>.FromError(bundleResult.Reason);

            List<CommandMetaInfo> cmdInfos = new List<CommandMetaInfo>();

            for (int i = 0; i < commands.Length; i++)
            {
                var paramUnpackResult = await ArrayReader.SplitToArray(commands[i], new char[] { '(', ')' });
                string[] splitCommand = (string[])paramUnpackResult.BestMatch;

                if (!paramUnpackResult.IsSuccess || splitCommand == null || splitCommand.Length < 1)
                    return CustomRuntimeResult<CommandMetaInfo[]>.FromError($"{await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_PROCESS_COMMAND_WITH_NAME_X, "{x}", commands[i])}\n{paramUnpackResult.ErrorReason}");


                var taskResult = await GetCommandInfoAsync(splitCommand[0]);

                if (!taskResult.IsSuccess)
                    return CustomRuntimeResult<CommandMetaInfo[]>.FromError(taskResult.Reason);

                //Since it is no struct, create copy manually
                CommandMetaInfo command = new CommandMetaInfo(taskResult.ResultValue);

                if (!command.IsCustomCommand)
                {
                    Discord.Commands.CommandInfo commandInfo = null;

                    // Find matching commandservice commandinfo
                    foreach (var ci in _Server.CommandService.Commands)
                    {
                        if (ci.Parameters.Count != command.Parameters.Count)
                            continue;

                        bool match = false;

                        if (command.Name == null || command.Name.Equals(ci.Name, StringComparison.OrdinalIgnoreCase) == false)
                        {
                            for (int a = 0; a < ci.Aliases?.Count; a++)
                            {
                                if (command.Name == null)
                                {
                                    if (!command.FullName.Contains(ci.Aliases[a], StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    match = true;
                                    break;
                                }
                                else if (command.FullName.Equals(ci.Aliases[a], StringComparison.OrdinalIgnoreCase))
                                {
                                    match = true;
                                    break;
                                }
                            }

                            if (match == false)
                                continue;
                        }

                        if (ci.Parameters.Count > 0)
                        {
                            match = true;
                            for (int p = 0; p < ci.Parameters.Count; p++)
                            {
                                if (ci.Parameters[p].Type == command.Parameters[p].ParameterType)
                                    continue;

                                match = false;
                                break;
                            }

                            if (match == false)
                                continue;
                        }

                        commandInfo = ci;
                        break;
                    }

                    if (commandInfo == null)
                        return CustomRuntimeResult<CommandMetaInfo[]>.FromError($"{await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_FIND_DISCORD_COMMAND_INFO_FOR_COMMAND_NAMED_X, "{x}", command.FullName)}");

                    var preconditionCheckResult = await commandInfo.CheckPreconditionsAsync(context, _Server.Services);
                    if (!preconditionCheckResult.IsSuccess)
                        return CustomRuntimeResult<CommandMetaInfo[]>.FromError(preconditionCheckResult.ErrorReason);

                    int minRequiredParameters = 0;
                    command.Parameters.ForEach(p => minRequiredParameters += p.IsOptional ? 0 : 1);

                    if (splitCommand.Length - 1 < minRequiredParameters)
                        return CustomRuntimeResult<CommandMetaInfo[]>.FromError($"{command.FullName} => {await ReplyDictionary.ReplaceStringInvariantCase(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.PARAMETER_COUNT_X_DOES_NOT_MATCH_REQUIRED_COUNT_Y, "{x}", (splitCommand.Length - 1).ToString()), "{y}", $">={minRequiredParameters}")}");
                    else if (splitCommand.Length - 1 > command.Parameters.Count)
                        return CustomRuntimeResult<CommandMetaInfo[]>.FromError($"{command.FullName} => {await ReplyDictionary.ReplaceStringInvariantCase(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.PARAMETER_COUNT_X_DOES_NOT_MATCH_REQUIRED_COUNT_Y, "{x}", (splitCommand.Length - 1).ToString()), "{y}", command.Parameters.Count.ToString())}");

                    for (int j = 0; j < command.RuntimeParameters.Length; j++)
                    {
                        var parameterInfo = command.RuntimeParameters[j];

                        try
                        {
                            if (parameterInfo.Type == typeof(string[]))
                                parameterInfo.Value = splitCommand[j + 1];
                            else
                                parameterInfo.Value = System.ComponentModel.TypeDescriptor.GetConverter(parameterInfo.Type).ConvertFromString(splitCommand[j + 1]);
                        }
                        catch
                        {
                            if (!parameterInfo.IsOptional)
                                return CustomRuntimeResult<CommandMetaInfo[]>.FromError($"{await ReplyDictionary.ReplaceStringInvariantCase(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_PARSE_X_TO_Y, "{x}", splitCommand[j + 1]), "{y}", parameterInfo.Type.Name)}");
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
                List<string> merged = new List<string>();
                StringBuilder arrayStringBuilder = new StringBuilder();

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
                                groupStartIndex = j;
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
                            arrayStringBuilder.Append($"{input[l]}{(l < groupEndIndex ? groupElementDivider.ToString() : "")}");

                        merged.Add(arrayStringBuilder.ToString());

                        groupStartIndex = -1;
                        groupEndIndex = -1;
                        arrayStringBuilder.Clear();
                    }
                }

                if (groupStartIndex > -1 && groupEndIndex < 0)
                    return CustomRuntimeResult<string[]>.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.GROUP_IS_MISSING_END_IDENTIFIER_X, "{x}", endIdentifier.ToString()));
                if (groupStartIndex < 0 && groupEndIndex > -1)
                    return CustomRuntimeResult<string[]>.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.GROUP_IS_MISSING_START_IDENTIFIER_X, "{x}", startIdentifier.ToString()));

                return CustomRuntimeResult<string[]>.FromSuccess(value: merged.ToArray());
            }

            return CustomRuntimeResult<string[]>.FromSuccess(value: null);
        }


        public async Task CreateHelpListStringsAsync()
        {
            FactoryCommandHelpMessage = new ChannelMessage(null).SetTemplate(ChannelMessage.MessageTemplateOption.Information);

            // BUILT IN
            List<string> lines = new List<string>();
            StringBuilder bodyBuilder = new StringBuilder();

            foreach (CommandMetaInfo command in FactoryCommands)
            {
                string s = await GetCommandInfoStringAsync(command);
                if (lines.Contains(s))
                    continue;

                lines.Add(s);
                bodyBuilder.Append($"{s} | ");
            }
            if (bodyBuilder.Length > 2)
                bodyBuilder.Remove(bodyBuilder.Length - 2, 2);

            ChannelMessageContent cmdMsgContent = new ChannelMessageContent()
                .SetTitle("Factory Commands")
                .SetDescription(bodyBuilder.ToString());

            FactoryCommandHelpMessage.Contents.Add(cmdMsgContent);
            await _Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(CreateHelpListStringsAsync), cmdMsgContent.ToString()));


            //CUSTOM            
            bodyBuilder.Clear();
            CustomCommandHelpMessage = new ChannelMessage(null).SetTemplate(ChannelMessage.MessageTemplateOption.Information);
            cmdMsgContent = new ChannelMessageContent().SetTitle("Custom Commands");

            if (CustomCommands.Count == 0)
            {
                cmdMsgContent = cmdMsgContent.SetDescription("-");
                CustomCommandHelpMessage.AddContent(cmdMsgContent);
                return;
            }

            string currCategory = "undefined";

            for (int i = 0; i < CustomCommands.Count; i++)
            {
                if (!((await _Server.CustomCommandHandler.GetCommandAsync(CustomCommands[i].Name)).ResultValue is CustomCommand cuco))
                    continue;

                string catName = (cuco.Category != null ? cuco.Category.Name : "undefined");

                if (catName.Equals(currCategory, StringComparison.OrdinalIgnoreCase))
                {
                    bodyBuilder.Append($"{await GetCommandInfoStringAsync(CustomCommands[i])} | ");
                    continue;
                }

                if (bodyBuilder.Length > 3)
                    cmdMsgContent = cmdMsgContent.SetDescription(bodyBuilder.ToString().TrimStart('\n').TrimEnd().TrimEnd('|'));
                else
                    cmdMsgContent = cmdMsgContent.SetDescription("-");

                CustomCommandHelpMessage.Contents.Add(cmdMsgContent);
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(CreateHelpListStringsAsync), cmdMsgContent.ToString()));

                currCategory = catName;
                cmdMsgContent = new ChannelMessageContent().SetTitle(currCategory);

                bodyBuilder.Clear();
                i--;
            }

            // Flush rest
            if (bodyBuilder.Length > 3)
                cmdMsgContent = cmdMsgContent.SetDescription(bodyBuilder.ToString().TrimStart('\n').TrimEnd().TrimEnd('|'));
            else
                cmdMsgContent = cmdMsgContent.SetDescription("-");

            CustomCommandHelpMessage.Contents.Add(cmdMsgContent);
            await _Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(CreateHelpListStringsAsync), cmdMsgContent.ToString()));
        }

        private static Task<ParameterInfoCollection[]> GetParameterInfosAsync(CommandMetaInfo command)
        {
            return Task.Run(() =>
            {
                List<ParameterInfoCollection> parameterInfos = new List<ParameterInfoCollection>();

                if (command.Parameters.Count > 0)
                {
                    foreach (var parameter in command.Parameters)
                    {
                        var summaryBuilder = new StringBuilder();
                        foreach (var pAttr in parameter.GetCustomAttributes())
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

                            var names = Enum.GetNames(parameter.ParameterType);
                            for (int i = 0; i < names.Length; i++)
                            {
                                var parsed = Enum.Parse(parameter.ParameterType, names[i]);

                                summaryBuilder.Append($"{names[i]}{(parsed != null ? $"={(int)parsed}" : "")}");

                                if (i < names.Length - 1)
                                    summaryBuilder.Append(", ");
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
            builder.AppendLine($"Dynamic commands for the '{_Server.Config.TwitchSettings.TwitchChannelName}'/'{_Server.RuntimeConfig.GuildName}' twitch/discord channels.".AsMarkdown(MarkdownOption.Italic));
            builder.AppendLine();

            string currentCategory = string.Empty;

            for (int i = 0; i < CustomCommands.Count; i++)
            {
                CustomRuntimeResult<CustomCommand> getCommandResult = await _Server.CustomCommandHandler.GetCommandAsync(CustomCommands[i].Name);

                if (getCommandResult.IsSuccess && getCommandResult.ResultValue is CustomCommand customCommand)
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
                            cooldownInSeconds = customCommand.Category.CategoryCooldownInSeconds;

                        if (customCommand.Category.PriceTag > priceTag)
                            priceTag = customCommand.Category.PriceTag;
                    }

                    AppendCommandInfo(builder, true, CustomCommands[i], cooldownInSeconds, priceTag, customCommand.ActionsToArray(), null, null);

                    builder.AppendLine();
                }
                else
                {
                    await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(ExportCustomCommandInfoPage), $"Could not get Custom Command: {getCommandResult.Reason}"));
                }
            }

            await HTMLSerializer.SaveTextToFile(builder, _Server.ServerFilesDirectoryPath, CUSTOM_COMMAND_MANUAL_FILE_NAME);
        }

        private async Task ExportFactoryCommandInfoPage()
        {
            StringBuilder builder = new();

            builder.AppendLine("Factory Commands".AsMarkdown(MarkdownOption.H1));
            builder.AppendLine($"Built-in commands of the 'Geist des Waldes' Disord/Twitch Bot.".AsMarkdown(MarkdownOption.Italic));
            builder.AppendLine();

            string currentGroup = string.Empty;

            for (int i = 0; i < FactoryCommands.Count; i++)
            {
                string cmdGroup = FactoryCommands[i].Groups.Count > 0 ? FactoryCommands[i].Groups[0] : FactoryCommands[i].Name;

                if (cmdGroup != currentGroup)
                {
                    currentGroup = cmdGroup;
                    builder.AppendLine(currentGroup.AsMarkdown(MarkdownOption.H2));
                    builder.AppendLine();
                }

                float cooldownDuration = 0f;
                int priceTagAmount = 0;

                var cooldownPrecondition = (CommandCooldown)FactoryCommands[i].Preconditions.FirstOrDefault(p => p.GetType() == typeof(CommandCooldown));
                if (cooldownPrecondition != default)
                    cooldownDuration = cooldownPrecondition.CooldownInSeconds;

                var feePrecondition = (CommandFee)FactoryCommands[i].Preconditions.FirstOrDefault(c => c.GetType() == typeof(CommandFee));
                if (feePrecondition != default)
                    priceTagAmount = feePrecondition.PriceTag;

                ParameterInfoCollection[] parameterInfos = await GetParameterInfosAsync(FactoryCommands[i]);
                PreconditionAttribute[] preconditions = FactoryCommands[i].Preconditions.ToArray();

                AppendCommandInfo(builder, false, FactoryCommands[i], cooldownDuration, priceTagAmount, null, parameterInfos, preconditions);

                builder.AppendLine();
            }

            await HTMLSerializer.SaveTextToFile(builder, _Server.ServerFilesDirectoryPath, FACTORY_COMMAND_MANUAL_FILE_NAME);
        }


        private void AppendCommandInfo(StringBuilder builder, bool isCustomCommand, CommandMetaInfo command, float cooldownDuration, int priceTagAmount, string[] actions, ParameterInfoCollection[] parameterInfos, PreconditionAttribute[] preconditions)
        {
            builder.AppendLine($"{_Server.Config.GeneralSettings.CommandPrefix}{command.FullName} | {Utility.CreateCostsString(cooldownDuration, priceTagAmount)}".AsMarkdown(MarkdownOption.H3));
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
                        builder.Append("[Optional] ");

                    builder.Append($"{pInfo.Type.AsMarkdown(MarkdownOption.Bold)} {pInfo.Name}");

                    if (!string.IsNullOrWhiteSpace(pInfo.Summary))
                        builder.Append($" ({pInfo.Summary.AsMarkdown(MarkdownOption.Italic)})");

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
                            builder.Append(" OR ");

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

    class ParameterInfoCollection
    {
        public string Name;
        public string Type;
        public bool IsOptional;
        public string Summary;

        public ParameterInfoCollection(string name, string type, bool optional, string summary)
        {
            Name = name;
            Type = type;
            IsOptional = optional;
            Summary = summary;
        }
    }
}