using Discord;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.UserCommands;
using PublicHoliday;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GeistDesWaldes.Calendar
{
    public class HolidayHandler : BaseHandler
    {
        private HolidayBehaviourDictionary _behaviourDictionary;

        private static GermanPublicHoliday _germanHolidays;
        public static GermanPublicHoliday GermanHolidays {
            get {
                if (_germanHolidays == null)
                {
                    _germanHolidays = new GermanPublicHoliday();
                    _germanHolidays.State = GermanPublicHoliday.States.ALL;
                }

                return _germanHolidays;
            }
        }

        private Task _holidayWatchdog;
        private CancellationTokenSource _cancelWatchdogSource;

        private const string HOLIDAYBEHAVIOURS_FILE_NAME = "HolidayBehaviours";


        public HolidayHandler(Server server) : base(server)
        {
            _behaviourDictionary = new HolidayBehaviourDictionary();
        }

        public override async Task OnServerStartUp()
        {
            await base.OnServerStartUp();
            await InitializeHolidayHandler();
            StartHolidayWatchdog();
        }
        
        public override async Task OnServerShutdown()
        {
            await base.OnServerShutdown();
            _cancelWatchdogSource?.Cancel();
        }
        
        public override async Task OnCheckIntegrity()
        {
            await base.OnCheckIntegrity();
            await CheckIntegrity();
        }

        private async Task InitializeHolidayHandler()
        {
            await GenericXmlSerializer.EnsurePathExistance(Server.LogHandler, Server.ServerFilesDirectoryPath, HOLIDAYBEHAVIOURS_FILE_NAME, _behaviourDictionary);

            await LoadHolidayBehavioursFromFile();
        }        
        private async Task CheckIntegrity()
        {
            List<string> problematicEntries = new List<string>();

            var allHolidays = GermanHolidays.PublicHolidayNames(DateTime.Now.Year);

            foreach (HolidayBehaviour behaviour in _behaviourDictionary.Behaviours)
            {
                var builder = new System.Text.StringBuilder($"...[{behaviour.HolidayName}] ");
                int startLength = builder.Length;

                var correspondingDay = allHolidays.FirstOrDefault(h => h.Value.Equals(behaviour.HolidayName, StringComparison.OrdinalIgnoreCase));

                if (correspondingDay.Value == default)
                    builder.Append($"Could not find corresponding holiday in {DateTime.Today.Year}!");
                else if (correspondingDay.Key != behaviour.HolidayDate)
                {
                    builder.Append($"Forced Update of Holiday Date {behaviour.HolidayDate} -> {correspondingDay.Key}");
                    behaviour.HolidayDate = correspondingDay.Key;
                }

                for (int i = 0; i < 2; i++)
                {
                    CustomCommand command = (i == 0 ? behaviour.StartCallback : behaviour.EndCallback);

                    var subBuilder = new System.Text.StringBuilder($"......[{i}]");
                    int subLength = subBuilder.Length;

                    if (command == null)
                        subBuilder.Append(" | NULL");
                    else if (command.CommandsToExecute != null && command.CommandsToExecute.Length > 0)
                    {
                        if (string.IsNullOrWhiteSpace(command.Name))
                            subBuilder.Append(" | missing name");

                        var testResult = await command.TestCommandExecution(Server.CommandService, Server.Services);

                        if (!testResult.IsSuccess)
                            subBuilder.Append(" | Commands ERROR:\n").AppendLine($"..........{testResult.Reason}");
                    }

                    if (subBuilder.Length > subLength)
                        builder.Append(subBuilder.ToString());
                }

                if (builder.Length > startLength)
                    problematicEntries.Add(builder.ToString());
            }

            if (problematicEntries.Count > 0)
            {
                var builder = new System.Text.StringBuilder("Holiday Behaviours ERROR:\n");

                for (int i = 0; i < problematicEntries.Count; i++)
                    builder.AppendLine(problematicEntries[i]);

                await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
            }
            else
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Holiday Behaviours OK."), (int)ConsoleColor.DarkGreen);
        }
        
        private void StartHolidayWatchdog()
        {
            if (_holidayWatchdog == null && _cancelWatchdogSource == null)
                _holidayWatchdog = Task.Run(HolidayWatchdog);
        }


        private async Task HolidayWatchdog()
        {
            _cancelWatchdogSource = new CancellationTokenSource();

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(HolidayWatchdog), "Started."));

            try
            {
                while (!_cancelWatchdogSource.IsCancellationRequested)
                {
                    // End running holiday behaviours
                    if (!string.IsNullOrWhiteSpace(_behaviourDictionary.ActiveHoliday))
                    {
                        await InvokeHolidayBehaviour(_behaviourDictionary.ActiveHoliday, HolidayBehaviour.BehaviourAction.EndCallback);
                        _behaviourDictionary.ActiveHoliday = null;
                    }

                    if (GermanHolidays.IsPublicHoliday(DateTime.Now) && GermanHolidays.PublicHolidayNames(DateTime.Now.Year).TryGetValue(DateTime.Today, out _behaviourDictionary.ActiveHoliday))
                    {
                        await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(HolidayWatchdog), $"Daily Watchdog found holiday: {_behaviourDictionary.ActiveHoliday}"), (int)ConsoleColor.Cyan);

                        await InvokeHolidayBehaviour(_behaviourDictionary.ActiveHoliday, HolidayBehaviour.BehaviourAction.StartCallback);
                    }

                    await SaveHolidayBehavioursToFile();

                    DateTime midnight = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 1).AddDays(1);
                    TimeSpan difference = midnight.Subtract(DateTime.Now);

                    await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(HolidayWatchdog), $"Daily Watchdog called. Next call in: {difference}"), (int)ConsoleColor.Cyan);

                    await Task.Delay(difference, _cancelWatchdogSource.Token);
                }
            }
            catch (TaskCanceledException)
            {

            }
            finally
            {
                _holidayWatchdog = null;
                _cancelWatchdogSource = null;

                await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(HolidayWatchdog), "Stopped."));
            }
        }
        private async Task InvokeHolidayBehaviour(string holidayName, HolidayBehaviour.BehaviourAction actionType)
        {
            var result = await GetHolidayBehaviour(holidayName);

            if (result.IsSuccess)
            {
                if (actionType == HolidayBehaviour.BehaviourAction.StartCallback)
                    await result.ResultValue.StartCallback.Execute(null);
                else
                    await result.ResultValue.EndCallback.Execute(null);
            }
        }


        public static Task<Holiday[]> GetUpcomingHolidays(int maxEntries)
        {
            return Task.Run(() =>
            {
                var holidays = GermanHolidays.GetHolidaysInDateRange(DateTime.Today, DateTime.Today.AddMonths(12));

                if (holidays == null || holidays.Count == 0)
                    return null;

                if (maxEntries < 1)
                    maxEntries = 1;

                List<Holiday> result = new List<Holiday>();

                for (int i = 0; i < holidays.Count; i++)
                {
                    if (i < maxEntries)
                        result.Add(holidays[i]);
                    else
                        break;
                }

                return result.ToArray();
            });
        }

        public Task<HolidayBehaviour[]> GetBehaviours(int maxEntries)
        {
            return Task.Run(() => 
            {
                List<HolidayBehaviour> result = new List<HolidayBehaviour>();

                if (_behaviourDictionary.Behaviours.Count == 0)
                    return result.ToArray();

                maxEntries = Math.Clamp(maxEntries, 1, _behaviourDictionary.Behaviours.Count);
                for (int i = 0; i < maxEntries; i++)
                    result.Add(_behaviourDictionary.Behaviours[i]);

                return result.ToArray();
            });
        }

        public async Task<CustomRuntimeResult<HolidayBehaviour>> GetHolidayBehaviour(string holidayName)
        {
            int nameHash = holidayName.ToLower().GetHashCode();

            HolidayBehaviour result = _behaviourDictionary.Behaviours.Find(b => b.HolidayNameHash == nameHash);

            if (result == default)
            {
                var allHolidays = GermanHolidays.PublicHolidayNames(DateTime.Now.Year);
                var holidayDate = allHolidays.FirstOrDefault(h => h.Value.Equals(holidayName, StringComparison.OrdinalIgnoreCase));

                if (holidayDate.Key != default && holidayDate.Value != default)
                    result = new HolidayBehaviour(Server, holidayDate.Value, holidayDate.Key);
            }

            if (result != default)
                return CustomRuntimeResult<HolidayBehaviour>.FromSuccess(value: result);

            return CustomRuntimeResult<HolidayBehaviour>.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_FIND_HOLIDAY_NAMED_X, "{x}", holidayName));
        }
        public async Task<CustomRuntimeResult> SetHolidayBehaviour(string holidayName, CustomCommand callback, HolidayBehaviour.BehaviourAction actionType)
        {
            var result = await GetHolidayBehaviour(holidayName);

            if (result.IsSuccess)
            {
                var newBehaviour = new HolidayBehaviour(result.ResultValue);

                if (actionType == HolidayBehaviour.BehaviourAction.StartCallback)
                {
                    if (callback == null)
                        newBehaviour.StartCallback = new CustomCommand(Server, nameof(HolidayBehaviour.BehaviourAction.StartCallback), null, 0);
                    else
                        newBehaviour.StartCallback = callback;
                }
                else
                {
                    if (callback == null)
                        newBehaviour.EndCallback = new CustomCommand(Server, nameof(HolidayBehaviour.BehaviourAction.EndCallback), null, 0);
                    else
                        newBehaviour.EndCallback = callback;
                }

                _behaviourDictionary.Behaviours.Remove(result.ResultValue);
                _behaviourDictionary.Behaviours.Add(newBehaviour);
            }

            return result;
        }
        public async Task<CustomRuntimeResult> RemoveHolidayBehaviour(string holidayName)
        {
            var result = await GetHolidayBehaviour(holidayName);

            if (result.IsSuccess)
                _behaviourDictionary.Behaviours.Remove(result.ResultValue);

            return result;
        }


        public Task SaveHolidayBehavioursToFile()
        {
            _behaviourDictionary.Behaviours.Sort((b1, b2) => b1.HolidayDate.CompareTo(b2.HolidayDate));

            return GenericXmlSerializer.SaveAsync<HolidayBehaviourDictionary>(Server.LogHandler, _behaviourDictionary, HOLIDAYBEHAVIOURS_FILE_NAME, Server.ServerFilesDirectoryPath);
        }
        public async Task LoadHolidayBehavioursFromFile()
        {
            HolidayBehaviourDictionary loadedDictionary = await GenericXmlSerializer.LoadAsync<HolidayBehaviourDictionary>(Server.LogHandler, HOLIDAYBEHAVIOURS_FILE_NAME, Server.ServerFilesDirectoryPath);

            if (loadedDictionary == null)
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadHolidayBehavioursFromFile), $"Loaded {nameof(loadedDictionary)} == DEFAULT"));
            else
                _behaviourDictionary = loadedDictionary;


            // Assure correct name hash and parameter quotes
            foreach (var behaviour in _behaviourDictionary.Behaviours)
            {
                behaviour.SetHolidayName(behaviour.HolidayName);

                behaviour.StartCallback?.InitAfterLoadFromFile(Server);
                behaviour.EndCallback?.InitAfterLoadFromFile(Server);
            }
        }
    }
}
