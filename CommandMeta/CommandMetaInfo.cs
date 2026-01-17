using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Discord.Commands;
using Discord.WebSocket;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using ParameterInfo = System.Reflection.ParameterInfo;

namespace GeistDesWaldes.CommandMeta;

[Serializable]
public class CommandMetaInfo
{
    private string _commandLineReadyName;
    private string _fullName;

    public List<string> Groups;

    [XmlIgnore] public bool IsCustomCommand;
    public string Name;

    [XmlIgnore] public List<ParameterInfo> Parameters;
    [XmlIgnore] public List<PreconditionAttribute> Preconditions;
    public RuntimeParameterInfo[] RuntimeParameters;

    [XmlIgnore] public string Summary;

    public CommandMetaInfo()
    {
        Groups = new List<string>();
        Parameters = new List<ParameterInfo>();
        Preconditions = new List<PreconditionAttribute>();
    }

    public CommandMetaInfo(CommandMetaInfo commandInfo)
    {
        Name = commandInfo.Name;
        Groups = commandInfo.Groups;
        Summary = commandInfo.Summary;
        Parameters = commandInfo.Parameters;
        Preconditions = commandInfo.Preconditions;
        IsCustomCommand = commandInfo.IsCustomCommand;

        CreateRuntimeParameters();
    }

    public string FullName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_fullName))
            {
                _fullName = GetFullName();
            }

            return _fullName;
        }
    }

    public string CommandLineReadyName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_commandLineReadyName))
            {
                _commandLineReadyName = GetCommandLineReadyName();
            }

            return _commandLineReadyName;
        }
    }

    public override string ToString()
    {
        return $"!{GroupsToString()} {Name} | {Summary} | Requires: {PreconditionsToString()}";
    }


    public string GroupsToString()
    {
        StringBuilder builder = new("");

        for (int i = 0; i < Groups?.Count; i++)
        {
            builder.Append($"{Groups[i]} ");
        }

        return builder.ToString().TrimEnd();
    }

    private string PreconditionsToString()
    {
        StringBuilder builder = new();

        for (int i = 0; i < Preconditions.Count; i++)
        {
            builder.Append($"[{Preconditions[i]}]");

            if (i < Preconditions.Count - 1)
            {
                builder.Append(", ");
            }
        }

        return builder.ToString();
    }

    public string GetFullName()
    {
        return $"{GroupsToString()} {Name}".TrimStart().TrimEnd();
    }

    public string GetCommandLineReadyName()
    {
        StringBuilder builder = new();

        for (int i = 0; i < Groups?.Count; i++)
        {
            builder.Append($"{Groups[i].Split(CommandInfoHandler.COMMAND_ALIAS_DIVIDER)[0]} ");
        }

        if (Name != null)
        {
            builder.Append(Name);
        }

        return builder.ToString().TrimEnd();
    }


    public void CreateRuntimeParameters()
    {
        RuntimeParameters = new RuntimeParameterInfo[Parameters.Count];

        for (int i = 0; i < Parameters.Count; i++)
        {
            RuntimeParameters[i] = new RuntimeParameterInfo(Parameters[i].Name, Parameters[i].ParameterType, Parameters[i].DefaultValue, Parameters[i].IsOptional);
        }
    }

    public string GetCommandLineString(CultureInfo culture, params string[] additionalParameters)
    {
        string command = CommandLineReadyName;

        for (int j = 0; j < RuntimeParameters.Length; j++)
        {
            string parameter = RuntimeParameters[j].Value?.ToString();

            if (additionalParameters != null)
            {
                for (int k = 0; k < additionalParameters.Length; k++)
                {
                    parameter = parameter.Replace($"[{k}]", additionalParameters[k]);
                }
            }

            parameter = ConstantsDictionary.InjectConstants(parameter, culture);

            if (!parameter.TrimStart().StartsWith('"'))
            {
                parameter = $"\"{parameter}";
            }

            if (!parameter.TrimEnd().EndsWith('"'))
            {
                parameter = $"{parameter}\"";
            }

            command += $" {parameter}";
        }

        return command;
    }


    public async Task<CustomRuntimeResult> TestCommandExecution(CommandService commandService, IServiceProvider services, CultureInfo culture)
    {
        StringBuilder builder = new();
        bool issueOccured = false;

        string commandLine = GetCommandLineString(culture);

        if (!string.IsNullOrWhiteSpace(commandLine))
        {
            builder.Append(commandLine);

            SocketUser botUser = Launcher.Instance.DiscordClient.GetUser(Launcher.Instance.DiscordClient.CurrentUser.Id);
            MetaCommandMessage commandMessage = new(commandLine, new ConsoleMessageChannel(), botUser);
            CommandContext context = new(Launcher.Instance.DiscordClient, commandMessage);
            SearchResult searchResult = commandService.Search(commandLine);

            if (searchResult.IsSuccess && searchResult.Commands?.Count > 0)
            {
                CommandMatch matchingCmd = searchResult.Commands[0];

                if (searchResult.Commands.Count > 1)
                {
                    builder.Append(" | CommandService found multiple matches!");

                    foreach (CommandMatch c in searchResult.Commands)
                    {
                        builder.Append($" / {c.Command.Name}");

                        foreach (string a in c.Command.Aliases)
                        {
                            if (a.Equals(FullName, StringComparison.OrdinalIgnoreCase))
                            {
                                matchingCmd = c;
                                builder.Append(" (Matched)");
                                break;
                            }
                        }
                    }
                }

                PreconditionResult preconditionResult = await matchingCmd.CheckPreconditionsAsync(context, services);

                if (!preconditionResult.IsSuccess)
                {
                    builder.Append($" | Precondition Check Failed! {preconditionResult.ErrorReason}");
                    issueOccured = true;
                }

                ParseResult parseResult = await matchingCmd.ParseAsync(context, searchResult, preconditionResult);

                if (!parseResult.IsSuccess)
                {
                    builder.Append($" | Parsing Failed! {parseResult.ErrorReason}");
                    issueOccured = true;
                }
            }
            else
            {
                builder.Append(" | CommandService Command not found!");
                issueOccured = true;
            }
        }
        else
        {
            builder.Append("missing command line");
            issueOccured = true;
        }


        if (issueOccured)
        {
            return CustomRuntimeResult.FromError(builder.ToString());
        }

        return CustomRuntimeResult.FromSuccess();
    }
}

[Serializable]
public class RuntimeParameterInfo
{
    public readonly bool IsOptional;
    public readonly string Name;
    public readonly Type Type;
    public object Value;

    public RuntimeParameterInfo()
    {
    }

    public RuntimeParameterInfo(string name, Type type, object value, bool optional)
    {
        Name = name;
        Type = type;
        Value = value;
        IsOptional = optional;
    }
}