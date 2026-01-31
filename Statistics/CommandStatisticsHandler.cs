using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;

namespace GeistDesWaldes.Statistics;

public class CommandStatisticsHandler : BaseHandler
{
    public override int Priority => -2;
    
    private const string COMMANDS_FILE_NAME = "Statistics_Commands";
    private readonly Dictionary<string, CommandStatistic> _runtimeDictionary = new();

    private List<CommandStatistic> _commandStatistics = new();


    public CommandStatisticsHandler(Server server) : base(server)
    {
    }


    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();
        await InitializeCommandStatisticsHandler();
    }

    private async Task InitializeCommandStatisticsHandler()
    {
        await GenericXmlSerializer.EnsurePathExistance(Server.LogHandler, Server.ServerFilesDirectoryPath, COMMANDS_FILE_NAME, _commandStatistics);
        await LoadCommandStatisticsFromFile();
    }

    public override async Task OnCheckIntegrity()
    {
        await base.OnCheckIntegrity();
        await CheckIntegrity();
    }

    private async Task CheckIntegrity()
    {
        HashSet<string> ids = new();

        StringBuilder builder = new("Command Statistics ERROR:\n");
        bool errorsFound = false;

        foreach (CommandStatistic stat in _commandStatistics)
        {
            if (ids.Add(stat.Id))
            {
                continue;
            }

            builder.AppendLine($"...Duplicate Id ({stat.Id})");
            errorsFound = true;
        }

        if (errorsFound)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
        }
        else
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Command Statistics OK."), (int)ConsoleColor.DarkGreen);
        }
    }

    public override async Task OnServerShutdown()
    {
        await base.OnServerShutdown();
        await SaveCommandStatisticsToFile();
    }


    private Task SaveCommandStatisticsToFile()
    {
        _commandStatistics.Sort((s1, s2) => s1.Id.CompareTo(s2.Id, StringComparison.Ordinal));

        return GenericXmlSerializer.SaveAsync<List<CommandStatistic>>(Server.LogHandler, _commandStatistics, COMMANDS_FILE_NAME, Server.ServerFilesDirectoryPath);
    }

    public async Task LoadCommandStatisticsFromFile()
    {
        var loadedStatistics = await GenericXmlSerializer.LoadAsync<List<CommandStatistic>>(Server.LogHandler, COMMANDS_FILE_NAME, Server.ServerFilesDirectoryPath);

        if (loadedStatistics == null)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadCommandStatisticsFromFile), $"Loaded {nameof(loadedStatistics)} == DEFAULT"));
            return;
        }

        _commandStatistics = loadedStatistics;

        foreach (CommandStatistic cs in _commandStatistics)
        {
            _runtimeDictionary.Add(cs.Id, cs);

            cs.CreateRuntimeDictionary();
        }
    }


    public async Task RecordCommand(string command)
    {
        try
        {
            bool dirty = false;

            foreach (CommandStatistic stat in _commandStatistics)
            {
                if (!stat.Active)
                {
                    continue;
                }

                stat.Add(command);
                dirty = true;
            }

            if (dirty)
            {
                await SaveCommandStatisticsToFile();
            }
        }
        catch (Exception e)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(RecordCommand), string.Empty, e));
        }
    }


    public async Task<CustomRuntimeResult<CommandStatistic>> CreateStatistic(string name, DateTime start, DateTime end)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CustomRuntimeResult.FromError($"{ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY}: {nameof(name)}");
        }

        if (_runtimeDictionary.ContainsKey(name))
        {
            return CustomRuntimeResult.FromError(ReplyDictionary.STATISTICS_NAME_ALREADY_EXISTS);
        }

        if (end <= start)
        {
            return CustomRuntimeResult.FromError(ReplyDictionary.STATISTICS_END_SMALLER_START);
        }

        CommandStatistic stat = new(name, start, end);

        _commandStatistics.Add(stat);
        _runtimeDictionary.Add(stat.Id, stat);

        await SaveCommandStatisticsToFile();

        return CustomRuntimeResult<CommandStatistic>.FromSuccess(value: stat);
    }

    public async Task<CustomRuntimeResult<CommandStatistic>> DeleteStatistic(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CustomRuntimeResult.FromError($"{ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY}: {nameof(name)}");
        }

        if (!_runtimeDictionary.TryGetValue(name, out CommandStatistic stat))
        {
            return CustomRuntimeResult.FromError(ReplyDictionary.STATISTICS_NAME_NOT_FOUND);
        }

        _runtimeDictionary.Remove(name);
        _commandStatistics.Remove(stat);

        await SaveCommandStatisticsToFile();

        return CustomRuntimeResult<CommandStatistic>.FromSuccess(value: stat);
    }


    public async Task<CustomRuntimeResult<CommandStatistic>> StartRecordingStatistic(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CustomRuntimeResult.FromError($"{ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY}: {nameof(name)}");
        }

        if (_runtimeDictionary.ContainsKey(name))
        {
            return CustomRuntimeResult.FromError(ReplyDictionary.STATISTICS_NAME_ALREADY_EXISTS);
        }

        CommandStatistic stat = new(name, DateTime.Now, DateTime.Now.AddYears(100));

        _commandStatistics.Add(stat);
        _runtimeDictionary.Add(stat.Id, stat);

        await SaveCommandStatisticsToFile();

        return CustomRuntimeResult<CommandStatistic>.FromSuccess(value: stat);
    }

    public async Task<CustomRuntimeResult<CommandStatistic>> StopRecordingStatistic(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CustomRuntimeResult.FromError($"{ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY}: {nameof(name)}");
        }

        if (!_runtimeDictionary.TryGetValue(name, out CommandStatistic stat))
        {
            return CustomRuntimeResult.FromError(ReplyDictionary.STATISTICS_NAME_NOT_FOUND);
        }

        stat.End = DateTime.Now;

        await SaveCommandStatisticsToFile();

        return CustomRuntimeResult<CommandStatistic>.FromSuccess(value: stat);
    }


    public CustomRuntimeResult<CommandStatistic> GetStatistic(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CustomRuntimeResult.FromError($"{ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY}: {nameof(name)}");
        }

        if (!_runtimeDictionary.TryGetValue(name, out CommandStatistic stat))
        {
            return CustomRuntimeResult.FromError(ReplyDictionary.STATISTICS_NAME_NOT_FOUND);
        }

        return CustomRuntimeResult<CommandStatistic>.FromSuccess(value: stat);
    }


    public IEnumerable<CommandStatistic> GetStatistics(bool includeInactive)
    {
        foreach (CommandStatistic stat in _commandStatistics)
        {
            if (includeInactive || stat.Active)
            {
                yield return stat;
            }
        }
    }
}