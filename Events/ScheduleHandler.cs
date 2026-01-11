using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GeistDesWaldes.Events
{
    public class ScheduleHandler : BaseHandler
    {
        public List<ScheduledEvent> EventSchedule;
        private const string SCHEDULE_FILE_NAME = "EventSchedule";

        private Task _kickOffDailysTask = null;
        private CancellationTokenSource _cancelKickOffTaskSource;


        public ScheduleHandler(Server server) : base(server)
        {
            EventSchedule = new List<ScheduledEvent>();
        }

        internal override void OnServerStart(object source, EventArgs e)
        {
            base.OnServerStart(source, e);

            InitializeScheduleHandler().SafeAsync<ScheduleHandler>(_Server.LogHandler, StartDailyKickOff);
        }
        internal override void OnServerShutdown(object source, EventArgs e)
        {
            base.OnServerShutdown(source, e);

            _cancelKickOffTaskSource?.Cancel();

            for (int i = 0; i < EventSchedule.Count; i++)
                EventSchedule[i].CancelKickOff();
        }
        internal override void OnCheckIntegrity(object source, EventArgs e)
        {
            base.OnCheckIntegrity(source, e);

            CheckIntegrity().SafeAsync<ScheduleHandler>(_Server.LogHandler);
        }

        private async Task InitializeScheduleHandler()
        {
            await GenericXmlSerializer.EnsurePathExistance(_Server.LogHandler, _Server.ServerFilesDirectoryPath, SCHEDULE_FILE_NAME, EventSchedule);
            await LoadScheduleFromFile();
        }
        private async Task CheckIntegrity()
        {
            var builder = new StringBuilder("Events ERROR:\n");
            int startLength = builder.Length;

            for (int i = 0; i < EventSchedule.Count; i++)
            {
                var subBuilder = new StringBuilder($"...[{i}]");
                int subStartLength = subBuilder.Length;

                ScheduledEvent ev = EventSchedule[i];

                if (ev != null && ev.CommandToExecute != null)
                {
                    if (!string.IsNullOrWhiteSpace(ev.CommandToExecute.Name))
                    {
                        if (ev.Repetition != ScheduledEvent.RepetitionOption.Once || DateTime.Compare(ev.ExecutionTime, DateTime.Now) > 0)
                        {
                            if (ev.CommandToExecute.CommandsToExecute != null && ev.CommandToExecute.CommandsToExecute.Length > 0)
                            {
                                var subSubBuilder = new StringBuilder($" | {ev.CommandToExecute.Name} | Commands ERROR:\n");
                                int subSubStartLength = subSubBuilder.Length;

                                foreach (var cmd in ev.CommandToExecute.CommandsToExecute)
                                {
                                    var testResult = await cmd.TestCommandExecution(_Server.CommandService, _Server.Services, _Server.CultureInfo);

                                    if (!testResult.IsSuccess)
                                        subSubBuilder.AppendLine($"......{testResult.Reason}");
                                }

                                if (subSubBuilder.Length > subSubStartLength)
                                    subBuilder.Append(subSubBuilder.ToString());
                            }
                            else
                                subBuilder.Append($" | {ev.CommandToExecute.Name} | No Commands to Execute!");
                        }
                        else
                            subBuilder.Append($" | {ev.CommandToExecute.Name} | Execution Date is in the past!");
                    }
                    else
                        subBuilder.Append(" | missing name!");
                }
                else
                    subBuilder.Append(" | NULL");

                if (subBuilder.Length > subStartLength)
                    builder.AppendLine(subBuilder.ToString());
            }


            if (builder.Length > startLength)
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
            else
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Events OK."), (int)ConsoleColor.DarkGreen);
        }


        private void StartDailyKickOff()
        {
            if (_kickOffDailysTask == null && _cancelKickOffTaskSource == null)
                _kickOffDailysTask = Task.Run(KickOffDailys);
        }
        private async Task KickOffDailys()
        {
            _cancelKickOffTaskSource = new CancellationTokenSource();
            
            await _Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(KickOffDailys), "Started."));

            try
            {
                while (!_cancelKickOffTaskSource.IsCancellationRequested)
                {
                    for (int i = 0; i < EventSchedule.Count; i++)
                        EventSchedule[i].KickOffTimer();

                    DateTime midnight = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 1).AddDays(1);
                    TimeSpan difference = midnight.Subtract(DateTime.Now);

                    await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(KickOffDailys), $"Daily Event KickOff called. Next KickOff in: {difference}"));

                    await Task.Delay(difference, _cancelKickOffTaskSource.Token);
                }
            }
            catch (TaskCanceledException)
            {

            }
            finally
            {
                _cancelKickOffTaskSource = null;
                _kickOffDailysTask = null;

                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(KickOffDailys), "Stopped."));
            }
        }

        public async Task<RuntimeResult> AddEvent(ScheduledEvent scheduledEvent)
        {
            try
            {
                if ((await GetScheduledEvent(scheduledEvent.CommandToExecute?.Name)).IsSuccess)
                {
                    scheduledEvent.CancelKickOff();
                    return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.EVENT_NAMED_X_ALREADY_EXISTS, "{X}", scheduledEvent.CommandToExecute?.Name));
                }

                EventSchedule.Add(scheduledEvent);

                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(AddEvent), $"Created: {scheduledEvent.CommandToExecute?.Name} | {scheduledEvent.ExecutionTime}"));

                await SaveScheduleToFile();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }
        public async Task<RuntimeResult> RemoveEvent(ScheduledEvent evt, IUser user)
        {
            try
            {
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(RemoveEvent), $"Removed: {evt.CommandToExecute?.Name} | {evt.ExecutionTime}"));

                evt.CancelKickOff();
                EventSchedule.Remove(evt);

                await SaveScheduleToFile();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        public Task<CustomRuntimeResult<ScheduledEvent>> GetScheduledEvent(string eventName)
        {
            return Task.Run(() =>
            {
                int hash = eventName.ToLower().GetHashCode();

                for (int i = 0; i < EventSchedule.Count; i++)
                {
                    if (EventSchedule[i].CommandToExecute?.NameHash == hash)
                        return CustomRuntimeResult<ScheduledEvent>.FromSuccess(value: EventSchedule[i]);
                }

                string message = Task.Run(() => ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_FIND_EVENT_NAMED_Y, "{x}", eventName)).GetAwaiter().GetResult();
                return CustomRuntimeResult<ScheduledEvent>.FromError(message);
            });
        }

        public async Task<CustomRuntimeResult> ListScheduledEvents(ICommandContext context)
        {
            try
            {
                StringBuilder body = new StringBuilder();
                int eventIndex = 1;
                for (int i = 0; i < EventSchedule.Count; i++)
                {
                    body.AppendLine($"{eventIndex:D2} | {EventSchedule[i].ExecutionTime:dd.MM.yyyy HH:mm} | {EventSchedule[i].CommandToExecute?.Name}");
                    eventIndex++;
                }

                ChannelMessage msg = new ChannelMessage(context)
                       .SetTemplate(ChannelMessage.MessageTemplateOption.Events)
                       .AddContent(new ChannelMessageContent()
                           .SetTitle(ReplyDictionary.EVENTS)
                           .SetDescription(body.ToString())
                       );

                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        public Task SaveScheduleToFile()
        {
            return GenericXmlSerializer.SaveAsync<List<ScheduledEvent>>(_Server.LogHandler, EventSchedule, SCHEDULE_FILE_NAME, _Server.ServerFilesDirectoryPath);
        }
        public async Task LoadScheduleFromFile()
        {
            List<ScheduledEvent> loadedSchedule = null;

            loadedSchedule = await GenericXmlSerializer.LoadAsync<List<ScheduledEvent>>(_Server.LogHandler, SCHEDULE_FILE_NAME, _Server.ServerFilesDirectoryPath);

            if (loadedSchedule == default(List<ScheduledEvent>))
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadScheduleFromFile), "Loaded Schedule == DEFAULT"));
            else
                EventSchedule = loadedSchedule;


            //Ensure Hash for externally added Events
            for (int i = 0; i < EventSchedule.Count; i++)
            {
                EventSchedule[i].CommandToExecute?.InitAfterLoadFromFile(_Server);
            }
        }
    }
}
