using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.UserCommands;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GeistDesWaldes.Misc;
using Microsoft.Extensions.DependencyInjection;

namespace GeistDesWaldes.Events
{
    [Serializable]
    public class ScheduledEvent
    {
        public enum RepetitionOption
        {
            Once = 0,
            Daily = 1,
            Weekly = 2,
            Monthly = 3,
            Yearly = 4,
            Minutely = 5,
            Hourly = 6
        }

        public DateTime ExecutionTime;
        public RepetitionOption Repetition;

        public CustomCommand CommandToExecute;

        [XmlIgnore] [NonSerialized] private Task _awaitExecutionTask;
        [XmlIgnore] [NonSerialized] private CancellationTokenSource _cancellationTokenSource;

        public ScheduledEvent()
        {

        }
        public ScheduledEvent(DateTime executionTime, RepetitionOption repetition, CustomCommand command)
        {
            ExecutionTime = executionTime;
            Repetition = repetition;
            CommandToExecute = command;
            
            KickOffTimer();
        }
                
        public void KickOffTimer()
        {
            if (_awaitExecutionTask != null)
                return;

            if (DateTime.Compare(ExecutionTime, DateTime.Now) < 0)
            {
                RescheduleToNextRepetition().SafeAsync<ScheduledEvent>(CommandToExecute?.Server?.LogHandler);
                return;
            }

            TimeSpan timeDifference = ExecutionTime.Subtract(DateTime.Now);
            if (timeDifference.TotalDays > 1)
                return;

            if (timeDifference.TotalMinutes < 1)
                timeDifference = TimeSpan.FromMinutes(1);

            _awaitExecutionTask = WaitAndExecute(timeDifference);
        }
        public void CancelKickOff()
        {
            if (_cancellationTokenSource != null)
                _cancellationTokenSource.Cancel();
        }
        

        public async Task WaitAndExecute(TimeSpan delay)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await Task.Run(async () =>
                {
                    await CommandToExecute.Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(WaitAndExecute), $"Started kick off '{CommandToExecute.Name}'"));

                    await Task.Delay(delay, _cancellationTokenSource.Token);

                    if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
                        throw new TaskCanceledException();

                    await ExecuteEventAsync();
                }, 
                _cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                if (e is TaskCanceledException)
                    await CommandToExecute.Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(WaitAndExecute), $"Cancelled kick off '{CommandToExecute.Name}'"));
                else
                    await CommandToExecute.Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(WaitAndExecute), string.Empty, e));
            }
            finally
            {
                _awaitExecutionTask = null;
                _cancellationTokenSource = null;
            }
        }

        private async Task ExecuteEventAsync()
        {
            await CommandToExecute.Execute(null);

            await RescheduleToNextRepetition();
        }

        private async Task RescheduleToNextRepetition()
        {
            DateTime nextTime = DateTime.Now;

            switch (Repetition)
            {
                default:
                case RepetitionOption.Once:
                    await CommandToExecute.Server.Services.GetService<ScheduleHandler>().RemoveEvent(this, Launcher.Instance.DiscordClient.CurrentUser);
                    return;

                case RepetitionOption.Minutely:
                    nextTime = ExecutionTime + TimeSpan.FromMinutes(1);
                    break;

                case RepetitionOption.Hourly:
                    nextTime = ExecutionTime + TimeSpan.FromHours(1);
                    break;

                case RepetitionOption.Daily:
                    nextTime = ExecutionTime + TimeSpan.FromDays(1);
                    break;

                case RepetitionOption.Weekly:
                    nextTime = ExecutionTime + TimeSpan.FromDays(7);
                    break;

                case RepetitionOption.Monthly:
                    nextTime = new(nextTime.Year, nextTime.Month == 12 ? 1 : nextTime.Month + 1, ExecutionTime.Day, ExecutionTime.Hour, ExecutionTime.Minute, ExecutionTime.Second);
                    break;

                case RepetitionOption.Yearly:
                    nextTime = new(ExecutionTime.Year + 1, ExecutionTime.Month, ExecutionTime.Day, ExecutionTime.Hour, ExecutionTime.Minute, ExecutionTime.Second);
                    break;
            }

            await RescheduleEvent(nextTime, Launcher.Instance.DiscordClient.CurrentUser);
        }
        
        public async Task<RuntimeResult> RescheduleEvent(DateTime changedDate, IUser user)
        {
            Server server = CommandToExecute.Server;

            try
            {
                await server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(RescheduleEvent), $"Rescheduled: {CommandToExecute?.Name} | before: {ExecutionTime} - after: {changedDate}"));

                CancelKickOff();

                ExecutionTime = changedDate;

                await server.Services.GetService<ScheduleHandler>().SaveScheduleToFile();

                KickOffTimer();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.Message);
            }

        }


        public ChannelMessage ToMessage(CultureInfo culture, ChannelMessage.MessageTemplateOption template = ChannelMessage.MessageTemplateOption.Events)
        {
            ChannelMessage msg = new ChannelMessage(null)
                .SetTemplate(template)
                .AddContent(
                    new ChannelMessageContent()
                    .SetTitle($"{CommandToExecute?.Name} ({ReplyDictionary.GetOutputTextForEnum(Repetition)})")
                    .SetDescription(ExecutionTime.ToString(culture))
                ).AddContent(
                    new ChannelMessageContent()
                    .SetTitle(ReplyDictionary.ACTIONS)
                    .SetDescription(CommandToExecute?.ActionsToString(), (int)ChannelMessageContent.DescriptionStyleOption.CodeBlock)
                ).SetFooter(CommandToExecute?.TargetChannelToString());

            return msg;
        }
    }

}
