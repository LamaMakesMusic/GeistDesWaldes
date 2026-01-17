using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Events;
using GeistDesWaldes.UserCommands;
using System;
using System.Threading.Tasks;
using GeistDesWaldes.CommandMeta;

namespace GeistDesWaldes.Modules
{
    [RequireUserPermission(GuildPermission.Administrator, Group = "EventPermissions")] [RequireUserPermission(GuildPermission.ManageChannels, Group = "EventPermissions")]
    [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "EventPermissions")]
    [RequireIsBot(Group = "EventPermissions")]
    [Group("event")]
    [Alias("events")]
    public class EventModule : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }

        private const int MIN_EVENT_NAME_LENGTH = 3;
        private const int MAX_EVENT_NAME_LENGTH = 30;

        [Priority(-1)]
        [Command]
        [Summary("Lists action events by name.")]
        public async Task<RuntimeResult> ListActionEvents()
        {
            return await Server.GetModule<ScheduleHandler>().ListScheduledEvents(Context);
        }

        [Command("add")]
        [Summary("Schedules a new action event.")]
        public async Task<RuntimeResult> AddActionEvent([Summary("Repetition Option")] ScheduledEvent.RepetitionOption repeat, [Summary("That date the event is being executed.")] DateTime executeAt,
            [Summary("The event name.")][RequireParameterLength(MIN_EVENT_NAME_LENGTH, MAX_EVENT_NAME_LENGTH)] string eventTitle, string[] commands, IChannel channel = null)
        {
            CustomRuntimeResult<CommandMetaInfo[]> result = await Server.GetModule<CommandInfoHandler>().ParseToSerializableCommandInfo(commands, Context);

            if (result.IsSuccess)
            {
                ScheduledEvent evt = new(executeAt, repeat, new CustomCommand(Server, eventTitle, result.ResultValue, channel?.Id ?? 0) { IsEvent = true });
                RuntimeResult addResult = await Server.GetModule<ScheduleHandler>().AddEvent(evt);

                if (addResult.IsSuccess)
                {
                    ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Events)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                            .SetDescription(ReplyDictionary.EVENT_CREATED)
                        );

                    msg.AppendContent(evt.ToMessage(Server.CultureInfo));
                    await msg.SendAsync();
                }

                return addResult;
            }

            return result;
        }

        [Command("addTimer")]
        [Summary("Schedules a new timer event.")]
        public async Task<RuntimeResult> AddTimerEvent([Summary("Duration in seconds.")]float durationInSeconds, [Summary("The timer name.")][RequireParameterLength(MIN_EVENT_NAME_LENGTH, MAX_EVENT_NAME_LENGTH)] string name, string[] commands, IChannel channel = null)
        {
            CustomRuntimeResult<CommandMetaInfo[]> result = await Server.GetModule<CommandInfoHandler>().ParseToSerializableCommandInfo(commands, Context);

            if (result.IsSuccess)
            {
                if (channel == null)
                    channel = Context.Channel;

                ScheduledEvent evt = new(DateTime.Now + TimeSpan.FromSeconds(durationInSeconds), ScheduledEvent.RepetitionOption.Once, new CustomCommand(Server, name, result.ResultValue, channel != null ? channel.Id : 0) { IsEvent = true });
                RuntimeResult addResult = await Server.GetModule<ScheduleHandler>().AddEvent(evt);

                if (addResult.IsSuccess)
                {
                    ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Events)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                            .SetDescription(ReplyDictionary.EVENT_CREATED)
                        );

                    msg.AppendContent(evt.ToMessage(Server.CultureInfo));
                    await msg.SendAsync();
                }

                return addResult;
            }

            return result;
        }

        [Command("remove")]
        [Summary("Removes an action event from the schedule.")]
        public async Task<RuntimeResult> RemoveActionEvent(string eventName)
        {
            CustomRuntimeResult<ScheduledEvent> result = await Server.GetModule<ScheduleHandler>().GetScheduledEvent(eventName);

            if (result.IsSuccess && result.ResultValue is { } evt)
            {
                RuntimeResult removeResult = await Server.GetModule<ScheduleHandler>().RemoveEvent(evt, Context.Message.Author);

                if (removeResult.IsSuccess)
                {
                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.EVENT_NAMED_X_REMOVED, "{x}", evt.CommandToExecute?.Name);
                    ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Events)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                                .SetDescription(body)
                            );

                    await msg.SendAsync();
                }

                return removeResult;
            }

            return result;
        }


        [Command("get")]
        [Summary("Gets action event info by name.")]
        public async Task<RuntimeResult> GetActionEvent(string eventName)
        {
            CustomRuntimeResult<ScheduledEvent> result = await Server.GetModule<ScheduleHandler>().GetScheduledEvent(eventName);

            if (result.IsSuccess && result.ResultValue is { } evt)
                await evt.ToMessage(Server.CultureInfo).SetChannel(Context.Channel).SendAsync();

            return result;
        }


        [Command("set")]
        [Summary("Sets action event infos by name.")]
        public async Task<RuntimeResult> SetActionEvent(string eventName, DateTime newDate)
        {
            CustomRuntimeResult<ScheduledEvent> result = await Server.GetModule<ScheduleHandler>().GetScheduledEvent(eventName);

            if (result.IsSuccess && result.ResultValue is { } evt)
            {
                RuntimeResult rescheduleResult = await evt.RescheduleEvent(newDate, Context.User);

                if (rescheduleResult.IsSuccess)
                {
                    ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Events)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                            .SetDescription(ReplyDictionary.EVENT_RESCHEDULED)
                        );

                    msg.AppendContent(evt.ToMessage(Server.CultureInfo));
                    await msg.SendAsync();
                }

                return rescheduleResult;
            }

            return result;
        }

    }
}
