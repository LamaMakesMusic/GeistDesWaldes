using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;

namespace GeistDesWaldes.Counters;

public class CounterHandler : BaseHandler
{
    public override int Priority => -14;
    
    private const string COUNTER_FILE_NAME = "Counter";
    private ModuleInfo _moduleInfo;
    public List<Counter> Counters;


    public CounterHandler(Server server) : base(server)
    {
        Counters = new List<Counter>();
    }

    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();
        await InitializeCounterHandler();
    }

    private async Task InitializeCounterHandler()
    {
        await GenericXmlSerializer.EnsurePathExistance(Server.LogHandler, Server.ServerFilesDirectoryPath, COUNTER_FILE_NAME, Counters);
        await LoadCounterCollectionFromFile();

        await UpdateCommandService();
    }

    public override async Task OnCheckIntegrity()
    {
        await base.OnCheckIntegrity();
        await CheckIntegrity();
    }

    private async Task CheckIntegrity()
    {
        List<int> problematicEntries = [];

        for (int i = 0; i < Counters.Count; i++)
        {
            if (Counters[i] == null || string.IsNullOrWhiteSpace(Counters[i].Name))
            {
                problematicEntries.Add(i);
            }
        }

        if (problematicEntries.Count > 0)
        {
            StringBuilder builder = new("Counters ERROR:\n");

            for (int i = 0; i < problematicEntries.Count; i++)
            {
                Counter counter = Counters[problematicEntries[i]];

                if (counter == null)
                {
                    builder.AppendLine($"...[{problematicEntries[i]}] | null");
                }
                else
                {
                    builder.AppendLine($"...[{problematicEntries[i]}] | empty name");
                }
            }

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), builder.ToString()));
        }
        else
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Counters OK."), (int)ConsoleColor.DarkGreen);
        }
    }


    public CustomRuntimeResult<Counter> GetCounter(string counterName)
    {
        int hash = counterName.ToLower().GetHashCode();
        for (int i = 0; i < Counters.Count; i++)
        {
            if (Counters[i].NameHash == hash)
            {
                return CustomRuntimeResult<Counter>.FromSuccess(value: Counters[i]);
            }
        }

        return CustomRuntimeResult<Counter>.FromError($"{ReplyDictionary.COULD_NOT_FIND_COUNTER_NAMED} '{counterName}'");
    }

    public async Task<CustomRuntimeResult> AddCounterAsync(Counter counter)
    {
        if (GetCounter(counter.Name).ResultValue != null)
        {
            return CustomRuntimeResult.FromError($"{ReplyDictionary.COUNTER_WITH_NAME_ALREADY_EXISTS}: '{counter.Name}'!");
        }

        Counters.Add(counter);

        await UpdateCommandService();

        await SaveCounterCollectionToFile();

        return CustomRuntimeResult.FromSuccess();
    }

    public async Task<CustomRuntimeResult> RemoveCounterAsync(string name)
    {
        CustomRuntimeResult<Counter> runtimeResult = GetCounter(name);

        if (runtimeResult.ResultValue is { } counter)
        {
            Counters.Remove(counter);

            await UpdateCommandService();

            await SaveCounterCollectionToFile();

            return CustomRuntimeResult.FromSuccess();
        }

        return CustomRuntimeResult.FromError(runtimeResult.Error.HasValue ? runtimeResult.Error.Value.ToString() : "");
    }

    public Task<string> ListCounters()
    {
        return Task.Run(() =>
        {
            StringBuilder sb = new();

            for (int i = 0; i < Counters.Count; i++)
            {
                sb.AppendLine(Counters[i].Name);
            }

            return sb.ToString();
        });
    }

    public Task SaveCounterCollectionToFile()
    {
        return GenericXmlSerializer.SaveAsync<List<Counter>>(Server.LogHandler, Counters, COUNTER_FILE_NAME, Server.ServerFilesDirectoryPath);
    }

    public async Task LoadCounterCollectionFromFile()
    {
        List<Counter> loadedCounters;

        loadedCounters = await GenericXmlSerializer.LoadAsync<List<Counter>>(Server.LogHandler, COUNTER_FILE_NAME, Server.ServerFilesDirectoryPath);

        if (loadedCounters == default(List<Counter>))
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadCounterCollectionFromFile), "Loaded Counters == DEFAULT"));
        }
        else
        {
            Counters = loadedCounters;
        }

        //Ensure Name Hash for externally added Counters
        for (int i = 0; i < Counters.Count; i++)
        {
            Counters[i].SetName(Counters[i].Name);
        }
    }

    public async Task UpdateCommandService()
    {
        try
        {
            if (_moduleInfo != null)
            {
                await Server.CommandService.RemoveModuleAsync(_moduleInfo);
            }

            _moduleInfo = await Server.CommandService.CreateModuleAsync("",
                mb =>
                {
                    for (int i = 0; i < Counters.Count; i++)
                    {
                        mb.AddCommand(Counters[i].Name, Counters[i].ExecuteCallback,
                            cb => cb.WithSummary("Custom Counter").AddParameter<string>("param1", p1 => p1.IsOptional = true).AddParameter<int>("param2", p2 => p2.IsOptional = true)
                        );
                    }
                }
            );

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(UpdateCommandService), "Command Service Update: OK"), (int)ConsoleColor.Green);
        }
        catch (Exception e)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateCommandService), $"Command Service Update: ERROR \n{e}"));
        }
    }
}