using Discord;
using GeistDesWaldes.Misc;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Icalendar = Ical.Net.Calendar;
using GeistDesWaldes.Configuration;
using System.Collections.Generic;
using Ical.Net.DataTypes;
using Ical.Net.CalendarComponents;
using Ical.Net;

namespace GeistDesWaldes.Calendar
{
    public class WebCalSyncHandler : BaseHandler
    {
        private Task _syncLoopTask;
        private CancellationTokenSource _cancelSyncLoopSource;

        public WebCalSyncHandler(Server server) : base(server)
        {

        }

        public override async Task OnServerStartUp()
        {
            await base.OnServerStartUp();
        
            StartSyncLoop();
        }
        
        public override async Task OnServerShutdown()
        {
            await base.OnServerShutdown();

            _cancelSyncLoopSource?.Cancel();
        }

        private void StartSyncLoop()
        {
            if (_syncLoopTask == null && _cancelSyncLoopSource == null)
                _syncLoopTask = Task.Run(SyncLoop);
        }
        
        private async Task SyncLoop()
        {
            _cancelSyncLoopSource = new CancellationTokenSource();

            await Task.Delay(1000);
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(SyncLoop), "Started."));

            while (!_cancelSyncLoopSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(Math.Max(ConfigurationHandler.Shared.WebCalSyncIntervalInMinutes, 1)), _cancelSyncLoopSource.Token);
                    await SyncCalendarToDiscordChannel();
                }
                catch (Exception e)
                {
                    if (e is TaskCanceledException)
                        break;

                    await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(SyncLoop), string.Empty, e));
                }
            }

            _syncLoopTask = null;
            _cancelSyncLoopSource = null;

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(SyncLoop), "Stopped."));
        }

        public async Task SyncCalendarToDiscordChannel()
        {
            ITextChannel syncChannel = Server.RuntimeConfig.WebCalSyncDiscordChannel;
            string webCalLink = Server.Config.TwitchSettings.WebCalLink;

            if (syncChannel == null || webCalLink == null)
                return;

            string webString = await Utility.DownloadWebString(webCalLink);

            if (webString == null)
            {
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(SyncLoop), $"Downloaded Calendar is null. Source: '{webCalLink}'"));
                return;
            }

            Icalendar calendar = Icalendar.Load(webString);

            string calendarString = CreateCalendarString(calendar);
            await UpdateChannelTopic(syncChannel, Server.GuildId, calendarString);
        }


        private string CreateCalendarString(Icalendar calendar)
        {
            DateTime start = DateTime.Today;
            DateTime end = DateTime.Today.AddDays(7);
            
            StringBuilder builder = new($"\n**{start.ToString("dd.MM.", Server.CultureInfo)} - {end.ToString("dd.MM.", Server.CultureInfo)}**\n");

            List<CalendarItem> evts = GetEvents(calendar, start, end);

            if (evts.Count == 0) 
            {
                builder.AppendLine(" - ");
            }
            else
            {
                foreach (CalendarItem ev in evts)
                {
                    string startString = ev.Start.ToString("dddd, dd.MM. HH:mm", Server.CultureInfo);
                    string description = ev.Description != null ? ev.Description.Trim(' ').Trim('.') : "??";

                    builder.AppendLine($"{startString} - {description} - {ev.Summary}");
                }
            }

            return builder.ToString();        
        }

        private static List<CalendarItem> GetEvents(Icalendar calendar, DateTime start, DateTime end)
        {
            List<CalendarItem> evts = new();

            CalDateTime calStart = new CalDateTime(start.ToUniversalTime());
            CalDateTime calEnd = new CalDateTime(end.ToUniversalTime());

            foreach (Occurrence occ in calendar.GetOccurrences(calStart).TakeWhileBefore(calEnd))
            {
                if (occ.Source is not CalendarEvent evt)
                    continue;

                evts.Add(new CalendarItem(occ.Period, evt));
            }


            evts.Sort((e1, e2) => e1.Start.CompareTo(e2.Start));

            return evts;
        }

    
        private static async Task UpdateChannelTopic(ITextChannel channel, ulong guildId, string text)
        {
            string newTopic = channel.Topic;

            int openIndex = newTopic.IndexOf(ConfigurationHandler.Configs[guildId].TwitchSettings.WebCalTagOpen, StringComparison.Ordinal);
            if (openIndex > -1)
                openIndex += ConfigurationHandler.Configs[guildId].TwitchSettings.WebCalTagOpen.Length;
            int closeIndex = newTopic.IndexOf(ConfigurationHandler.Configs[guildId].TwitchSettings.WebCalTagClose, StringComparison.Ordinal);

            if (text.Length > 0 && openIndex > -1 && closeIndex > openIndex)
            {
                newTopic = newTopic.Remove(openIndex, closeIndex - openIndex);
                newTopic = newTopic.Insert(openIndex, text);

                await channel.ModifyAsync(c =>
                {
                    c.Topic = newTopic;
                });
            }
        }
    }


    public class CalendarItem
    {
        public DateTime Start;
        public string Description;
        public string Summary;

        public CalendarItem(Period period, CalendarEvent item)
        {
            Start = period.StartTime.AsUtc.ToLocalTime();
            Description = item.Description;
            Summary = item.Summary;
        }
    }
}
