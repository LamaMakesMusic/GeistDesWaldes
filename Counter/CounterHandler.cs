using Discord;
using Discord.Commands;
using Discord.Commands.Builders;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GeistDesWaldes.Counters
{
    public class CounterHandler : BaseHandler
    {
        public List<Counter> Counters;

        private const string COUNTER_FILE_NAME = "Counter";
        private ModuleInfo _moduleInfo;


        public CounterHandler(Server server) : base(server)
        {
            Counters = new List<Counter>();
        }

        internal override void OnServerStart(object source, EventArgs e)
        {
            base.OnServerStart(source, e);

            InitializeCounterHandler().SafeAsync<CounterHandler>(_Server.LogHandler);
        }
        internal override void OnCheckIntegrity(object source, EventArgs e)
        {
            base.OnCheckIntegrity(source, e);

            CheckIntegrity().SafeAsync<CounterHandler>(_Server.LogHandler);
        }

        private async Task InitializeCounterHandler()
        {
            await GenericXmlSerializer.EnsurePathExistance(_Server.LogHandler, _Server.ServerFilesDirectoryPath, COUNTER_FILE_NAME, Counters);
            await LoadCounterCollectionFromFile();

            await UpdateCommandService();
        }

        private async Task CheckIntegrity()
        {
            List<int> problematicEntries = new List<int>();

            for (int i = 0; i < Counters.Count; i++)
            {
                if (Counters[i] == null || string.IsNullOrWhiteSpace(Counters[i].Name))
                    problematicEntries.Add(i);
            }

            if (problematicEntries.Count > 0)
            {
                var builder = new StringBuilder("Counters ERROR:\n");

                for (int i = 0; i < problematicEntries.Count; i++)
                {
                    var counter = Counters[problematicEntries[i]];

                    if (counter == null)
                        builder.AppendLine($"...[{problematicEntries[i]}] | null");
                    else
                        builder.AppendLine($"...[{problematicEntries[i]}] | empty name");
                }

                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), builder.ToString()));
            }
            else
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Counters OK."), (int)ConsoleColor.DarkGreen);
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
            if (GetCounter(counter.Name).ResultValue is Counter)
                return CustomRuntimeResult.FromError($"{ReplyDictionary.COUNTER_WITH_NAME_ALREADY_EXISTS}: '{counter.Name}'!");

            Counters.Add(counter);

            await UpdateCommandService();

            await SaveCounterCollectionToFile();

            return CustomRuntimeResult.FromSuccess();
        }

        public async Task<CustomRuntimeResult> RemoveCounterAsync(string name)
        {
            var runtimeResult = GetCounter(name);
            if (runtimeResult.ResultValue is Counter counter)
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
                StringBuilder sb = new StringBuilder();

                for (int i = 0; i < Counters.Count; i++)
                {
                    sb.AppendLine(Counters[i].Name);
                }

                return sb.ToString();
            });
        }

        public Task SaveCounterCollectionToFile()
        {
            return GenericXmlSerializer.SaveAsync<List<Counter>>(_Server.LogHandler, Counters, COUNTER_FILE_NAME, _Server.ServerFilesDirectoryPath);
        }
        public async Task LoadCounterCollectionFromFile()
        {
            List<Counter> loadedCounters = null;

            loadedCounters = await GenericXmlSerializer.LoadAsync<List<Counter>>(_Server.LogHandler, COUNTER_FILE_NAME, _Server.ServerFilesDirectoryPath);

            if (loadedCounters == default(List<Counter>))
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadCounterCollectionFromFile), "Loaded Counters == DEFAULT"));
            else
                Counters = loadedCounters;

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
                    await _Server.CommandService.RemoveModuleAsync(_moduleInfo);

                _moduleInfo = await _Server.CommandService.CreateModuleAsync("",
                    new Action<ModuleBuilder>(mb =>
                    {
                        for (int i = 0; i < Counters.Count; i++)
                        {
                            mb.AddCommand(Counters[i].Name, Counters[i].ExecuteCallback,
                                            new Action<CommandBuilder>(cb => cb.WithSummary("Custom Counter").AddParameter<string>("param1", p1 => p1.IsOptional = true).AddParameter<int>("param2", p2 => p2.IsOptional = true))
                                        );
                        }
                    }
                    )
                );

                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(UpdateCommandService), "Command Service Update: OK"), (int)ConsoleColor.Green);
            }
            catch (Exception e)
            {
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateCommandService), $"Command Service Update: ERROR \n{e}"));
            }
        }

    }
}
