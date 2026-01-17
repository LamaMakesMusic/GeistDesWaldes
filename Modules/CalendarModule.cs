using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Calendar;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.UserCommands;
using System;
using System.Text;
using System.Threading.Tasks;
using GeistDesWaldes.CommandMeta;
using GeistDesWaldes.Users;
using PublicHoliday;

namespace GeistDesWaldes.Modules
{
    [RequireTimeJoined("0", "1", Group = "CalendarPermissions")]
    [RequireIsFollower(Group = "CalendarPermissions")]
    [RequireIsBot(Group = "CalendarPermissions")]
    [Group("holiday")]
    [Alias("holidays")]
    public class CalendarModule : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }

        [Command("is")]
        [Summary("Is the provided day a holiday?")]
        public async Task<RuntimeResult> GetHoliday([Summary("Default is today.")] string day = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(day))
                    day = DateTime.Today.ToString(Server.CultureInfo);

                if (!DateTime.TryParse(day, out DateTime parsedDay))
                    return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_PARSE_X_TO_Y, "{x}", nameof(DateTime)));

                string body;

                if (HolidayHandler.GermanHolidays.PublicHolidayNames(parsedDay.Year).TryGetValue(parsedDay, out string foundHoliday))
                    body = $"{ReplyDictionary.THAT_DAY_IS_A_HOLIDAY} => {foundHoliday}";
                else
                    body = ReplyDictionary.THAT_DAY_IS_NOT_A_HOLIDAY;


                ChannelMessage msg = new ChannelMessage(Context)
                    .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                   .AddContent(new ChannelMessageContent()
                       .SetTitle(parsedDay.ToString("dd. MMMM yyyy", Server.CultureInfo))
                       .SetDescription(body)
                   );


                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("next")]
        [Summary("Get the upcoming holiday.")]
        public async Task<RuntimeResult> GetUpcomingHoliday(int entries = 1)
        {
            try
            {
                if (entries < 1)
                    entries = 1;
                else if (entries > 10)
                    entries = 10;

                DateTime startDate = DateTime.Today.AddDays(1);
                DateTime endDate = startDate.AddMonths(12);

                StringBuilder body = new();

                Holiday[] result = await HolidayHandler.GetUpcomingHolidays(entries);
                if (result == null || result.Length == 0)
                {
                    body.Append("-");
                }
                else
                {
                    foreach (Holiday holiday in result)
                    {
                        if (!HolidayHandler.GermanHolidays.PublicHolidayNames(holiday.HolidayDate.Year).TryGetValue(holiday.HolidayDate, out string holidayName))
                            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(GetUpcomingHoliday), $"Could not find holiday name for date: {holiday.HolidayDate}"));

                        body.AppendLine($"{holiday.HolidayDate.ToString("dd. MMMM yyyy", Server.CultureInfo)} | {holidayName}");
                    }

                    endDate = result[^1].HolidayDate;
                }


                ChannelMessage msg = new ChannelMessage(Context)
                    .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                    .AddContent(new ChannelMessageContent()
                        .SetTitle($"{startDate.ToString("dd. MMMM yyyy", Server.CultureInfo)} - {endDate.ToString("dd. MMMM yyyy", Server.CultureInfo)}")
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


        [RequireUserPermission(GuildPermission.Administrator, Group = "CalendarAdminPermissions")] [RequireUserPermission(GuildPermission.ManageChannels, Group = "CalendarAdminPermissions")]
        [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "CalendarAdminPermissions")]
        [Group("behaviour")]
        [Alias("behaviours")]
        public class CalendarAdminModule : ModuleBase<CommandContext>, ICommandModule
        {
            public Server Server { get; set; }
            
            [Priority(-1)]
            [Command]
            public async Task<RuntimeResult> FindAllBehaviours(int maxEntries = 10)
            {
                try
                {
                    HolidayBehaviour[] result =  await Server.GetModule<HolidayHandler>().GetBehaviours(maxEntries);

                    ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(ReplyDictionary.HOLIDAY_BEHAVIOUR));

                    for (int i = 0; i < result?.Length; i++)
                        msg.AddContent(new ChannelMessageContent().SetDescription(result[i].HolidayName));

                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("get")]
            [Summary("Gets holiday behaviour set for provided holiday.")]
            public async Task<RuntimeResult> GetHolidayBehaviour([Summary("Name of the holiday")] string holidayName)
            {
                try
                {
                    CustomRuntimeResult<HolidayBehaviour> result = await Server.GetModule<HolidayHandler>().GetHolidayBehaviour(holidayName);

                    if (result.IsSuccess == false)
                        return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_FIND_HOLIDAY_NAMED_X, "{x}", holidayName));

                    await result.ResultValue.ToMessage().SetChannel(Context.Channel).SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("set")]
            [Summary("Sets holiday behaviour for provided holiday.")]
            public async Task<RuntimeResult> SetHolidayBehaviour([Summary("Name of the holiday")] string holidayName, HolidayBehaviour.BehaviourAction behaviourType, string[] commands, IChannel channel = null)
            {
                try
                {
                    CustomRuntimeResult<HolidayBehaviour> holidayResult = await Server.GetModule<HolidayHandler>().GetHolidayBehaviour(holidayName);

                    if (holidayResult.IsSuccess == false)
                        return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_FIND_HOLIDAY_NAMED_X, "{x}", holidayName));

                    CustomRuntimeResult<CommandMetaInfo[]> parseResult = await Server.GetModule<CommandInfoHandler>().ParseToSerializableCommandInfo(commands, Context);
                    if (parseResult.IsSuccess)
                    {
                        CustomCommand cmd;

                        if (behaviourType == HolidayBehaviour.BehaviourAction.StartCallback)
                            cmd = holidayResult.ResultValue.StartCallback;
                        else
                            cmd = holidayResult.ResultValue.EndCallback;

                        cmd.CommandsToExecute = parseResult.ResultValue;

                        if (channel != null)
                            cmd.TextChannelContextId = channel.Id;


                        CustomRuntimeResult setResult = await Server.GetModule<HolidayHandler>().SetHolidayBehaviour(holidayResult.ResultValue.HolidayName, cmd, behaviourType);
                        if (setResult.IsSuccess)
                        {
                            string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.SAVED_HOLIDAY_BEHAVIOUR_FOR_X, "{x}", holidayResult.ResultValue.HolidayName);


                            ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                .SetDescription(body)
                            );

                            await msg.SendAsync();


                            await Server.GetModule<HolidayHandler>().SaveHolidayBehavioursToFile();
                        }

                        return setResult;
                    }

                    return parseResult;
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("remove")]
            [Summary("Removes holiday behaviour of provided holiday.")]
            public async Task<RuntimeResult> RemoveHolidayBehaviour([Summary("Name of the holiday")] string holidayName)
            {
                try
                {
                    CustomRuntimeResult removeResult = await Server.GetModule<HolidayHandler>().RemoveHolidayBehaviour(holidayName);

                    if (removeResult.IsSuccess)
                    {
                        string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.REMOVED_HOLIDAY_BEHAVIOUR_FOR_X, "{x}", holidayName);


                        ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                .SetDescription(body)
                            );

                        await msg.SendAsync();


                        await Server.GetModule<HolidayHandler>().SaveHolidayBehavioursToFile();
                    }

                    return removeResult;
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

        }
    }


    [RequireTimeJoined("0", "1", Group = "CalendarPermissions")]
    [RequireIsFollower(Group = "CalendarPermissions")]
    [RequireIsBot(Group = "CalendarPermissions")]
    [Group("birthday")]
    [Alias("birthdays")]
    public class BirthdayModule : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }

        [Command("add")]
        [Summary("Adds user's birthday to the calendar.")]
        public async Task<RuntimeResult> AddBirthday(DateTime date)
        {
            try
            {
                if (Context.Channel is TwitchIntegration.TwitchMessageChannel || Context.Channel is ConsoleMessageChannel)
                    return CustomRuntimeResult.FromError(ReplyDictionary.COMMAND_ONLY_VALID_ON_DISCORD);

                CustomRuntimeResult<ForestUser> getUserResult = await Server.GetModule<ForestUserHandler>().GetUser(Context.User);

                if (getUserResult.IsSuccess)
                {
                    CustomRuntimeResult<Birthday> birthdayGetResult = await Server.GetModule<BirthdayHandler>().GetBirthday(getUserResult.ResultValue.ForestUserId);

                    if (birthdayGetResult.IsSuccess)
                        return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.BIRTHDAY_FOR_USER_X_ALREADY_EXISTS, "{x}", getUserResult.ResultValue.Name));

                    CustomRuntimeResult addResult = await Server.GetModule<BirthdayHandler>().AddBirthday(getUserResult.ResultValue, date);

                    if (addResult.IsSuccess)
                    {
                        string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.X_BIRTHDAY_IS_ON_Y, "{x}", getUserResult.ResultValue.Name);
                        body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{y}", date.ToString("dd. MMMM", Server.CultureInfo));

                        ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                            .SetDescription(body)
                        );

                        await msg.SendAsync();

                        await Server.GetModule<BirthdayHandler>().SaveBirthdaysToFile();
                    }

                    return addResult;
                }
                else
                    return getUserResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("get")]
        [Summary("Gets birthday entry for user.")]
        public async Task<RuntimeResult> GetBirthday(IUser user = null)
        {
            try
            {
                if (Context.Channel is TwitchIntegration.TwitchMessageChannel || Context.Channel is ConsoleMessageChannel)
                    return CustomRuntimeResult.FromError(ReplyDictionary.COMMAND_ONLY_VALID_ON_DISCORD);

                if (user == null)
                    user = Context.User;

                CustomRuntimeResult<ForestUser> getUserResult = await Server.GetModule<ForestUserHandler>().GetUser(user);

                if (getUserResult.IsSuccess)
                {
                    CustomRuntimeResult<Birthday> result = await Server.GetModule<BirthdayHandler>().GetBirthday(getUserResult.ResultValue.ForestUserId);

                    if (result.IsSuccess == false)
                        return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_FIND_BIRTHDAY_FOR_X, "{x}", user.Username));

                    Birthday birthday = result.ResultValue;

                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.X_BIRTHDAY_IS_ON_Y, "{x}", user.Username);
                    body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{y}", birthday.BirthDate.ToString("dd. MMMM", Server.CultureInfo));

                    ChannelMessage msg = new ChannelMessage(Context)
                    .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                    .AddContent(new ChannelMessageContent()
                        .SetTitle(user.Username, EmojiDictionary.BIRTHDAY_CAKE)
                        .SetDescription(body)
                    );

                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                else
                    return getUserResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("remove")]
        [Summary("Removes user's birthday from the calendar.")]
        public async Task<RuntimeResult> RemoveBirthday()
        {
            try
            {
                if (Context.Channel is TwitchIntegration.TwitchMessageChannel || Context.Channel is ConsoleMessageChannel)
                    return CustomRuntimeResult.FromError(ReplyDictionary.COMMAND_ONLY_VALID_ON_DISCORD);

                IUser user = Context.User;

                CustomRuntimeResult<ForestUser> getUserResult = await Server.GetModule<ForestUserHandler>().GetUser(user);
                if (getUserResult.IsSuccess)
                {
                    CustomRuntimeResult removeResult = await Server.GetModule<BirthdayHandler>().RemoveBirthday(getUserResult.ResultValue);
                    if (removeResult.IsSuccess)
                    {
                        string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.REMOVED_BIRTHDAY_ENTRY_FOR_X, "{x}", user.Username);

                        ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                            .SetDescription(body)
                        );

                        await msg.SendAsync();

                        await Server.GetModule<BirthdayHandler>().SaveBirthdaysToFile();
                    }

                    return removeResult;
                }
                else
                    return getUserResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        [Command("next")]
        [Summary("Lists upcoming birthday(s).")]
        public async Task<RuntimeResult> NextBirthday(int entries = 1)
        {
            try
            {
                if (entries < 1)
                    entries = 1;

                Birthday[] upcomingBirthdays = await Server.GetModule<BirthdayHandler>().GetUpcomingBirthdays(entries);

                StringBuilder body = new();

                if (upcomingBirthdays?.Length > 0)
                {
                    for (int i = 0; i < upcomingBirthdays.Length; i++)
                    {
                        CustomRuntimeResult<ForestUser> getUserResult = await Server.GetModule<ForestUserHandler>().GetUser(upcomingBirthdays[i].UserId);

                        if (getUserResult.IsSuccess)
                            body.AppendLine($"° {upcomingBirthdays[i].BirthDate.ToString("dd. MMMM", Server.CultureInfo)} | {getUserResult.ResultValue.Name} ");
                    }
                }

                if (body.Length == 0)
                    body.Append("-");

                ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(ReplyDictionary.BIRTHDAYS, EmojiDictionary.INFO)
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

        [RequireUserPermission(GuildPermission.Administrator, Group = "CalendarAdminPermissions")] [RequireUserPermission(GuildPermission.ManageChannels, Group = "CalendarAdminPermissions")]
        [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "CalendarAdminPermissions")]
        // [Group("admin")]
        public class BirthdayAdminModule : ModuleBase<CommandContext>, ICommandModule
        {
            public Server Server { get; set; }

            [Command("add")]
            [Summary("Adds birthday entry for provided user.")]
            public async Task<RuntimeResult> AddBirthday(IUser user, DateTime date)
            {
                try
                {
                    if (user == null)
                        throw new Exception(ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY);

                    CustomRuntimeResult<ForestUser> getUserResult = await Server.GetModule<ForestUserHandler>().GetUser(user);
                    if (getUserResult.IsSuccess)
                    {
                        CustomRuntimeResult<Birthday> birthdayGetResult = await Server.GetModule<BirthdayHandler>().GetBirthday(getUserResult.ResultValue.ForestUserId);
                        if (birthdayGetResult.IsSuccess)
                            return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.BIRTHDAY_FOR_USER_X_ALREADY_EXISTS, "{x}", user.Username));

                        CustomRuntimeResult addResult = await Server.GetModule<BirthdayHandler>().AddBirthday(getUserResult.ResultValue, date);

                        if (addResult.IsSuccess)
                        {
                            string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.X_BIRTHDAY_IS_ON_Y, "{x}", user.Username);
                            body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{y}", date.ToString("dd. MMMM", Server.CultureInfo));


                            ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                .SetDescription(body)
                            );

                            await msg.SendAsync();


                            await Server.GetModule<BirthdayHandler>().SaveBirthdaysToFile();
                        }

                        return addResult;
                    }
                    else
                        return getUserResult;
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("remove")]
            [Summary("Removes birthday entry of provided user.")]
            public async Task<RuntimeResult> RemoveBirthday(IUser user)
            {
                try
                {
                    if (user == null)
                        throw new Exception(ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY);

                    CustomRuntimeResult<ForestUser> getUserResult = await Server.GetModule<ForestUserHandler>().GetUser(user);
                    if (getUserResult.IsSuccess)
                    {
                        CustomRuntimeResult removeResult = await Server.GetModule<BirthdayHandler>().RemoveBirthday(getUserResult.ResultValue);
                        if (removeResult.IsSuccess)
                        {
                            string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.REMOVED_BIRTHDAY_ENTRY_FOR_X, "{x}", user.Username);


                            ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                .SetDescription(body)
                            );

                            await msg.SendAsync();


                            await Server.GetModule<BirthdayHandler>().SaveBirthdaysToFile();
                        }

                        return removeResult;
                    }
                    else
                        return getUserResult;
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }


            [Group("callback")]
            public class BirthdayAdminCallbackModule : ModuleBase<CommandContext>, ICommandModule
            {
                public Server Server { get; set; }

                [Command("set")]
                [Summary("Sets provided birthday callback.")]
                public async Task<RuntimeResult> SetCallback(HolidayBehaviour.BehaviourAction behaviourType, string[] commands, IChannel channel = null)
                {
                    try
                    {
                        CustomRuntimeResult<CommandMetaInfo[]> parseResult = await Server.GetModule<CommandInfoHandler>().ParseToSerializableCommandInfo(commands, Context);
                        if (parseResult.IsSuccess)
                        {
                            if (behaviourType == HolidayBehaviour.BehaviourAction.StartCallback)
                                Server.GetModule<BirthdayHandler>().BirthdayDictionary.StartCallback.CommandsToExecute = parseResult.ResultValue;
                            else
                                Server.GetModule<BirthdayHandler>().BirthdayDictionary.EndCallback.CommandsToExecute = parseResult.ResultValue;


                            if (channel != null)
                            {
                                if (behaviourType == HolidayBehaviour.BehaviourAction.StartCallback)
                                    Server.GetModule<BirthdayHandler>().BirthdayDictionary.StartCallback.TextChannelContextId = channel.Id;
                                else
                                    Server.GetModule<BirthdayHandler>().BirthdayDictionary.EndCallback.TextChannelContextId = channel.Id;
                            }


                            string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.SAVED_CALLBACK_X, "{x}", behaviourType.ToString());


                            ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                .SetDescription(body)
                            );

                            await msg.SendAsync();


                            await Server.GetModule<BirthdayHandler>().SaveBirthdaysToFile();

                            return CustomRuntimeResult.FromSuccess();
                        }

                        return parseResult;
                    }
                    catch (Exception e)
                    {
                        return CustomRuntimeResult.FromError(e.ToString());
                    }
                }

                [Command("get")]
                [Summary("Gets provided birthday callback.")]
                public async Task<RuntimeResult> GetCallback(HolidayBehaviour.BehaviourAction behaviourType)
                {
                    try
                    {
                        CustomCommand callback;

                        if (behaviourType == HolidayBehaviour.BehaviourAction.StartCallback)
                            callback = Server.GetModule<BirthdayHandler>().BirthdayDictionary.StartCallback;
                        else
                            callback = Server.GetModule<BirthdayHandler>().BirthdayDictionary.EndCallback;


                        ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(callback.Name, EmojiDictionary.INFO)
                            .SetDescription("Callback"))
                        .AddContent(callback.ActionsToMessageContent())
                        .SetFooter(callback.TargetChannelToString());

                        await msg.SendAsync();

                        return CustomRuntimeResult.FromSuccess();
                    }
                    catch (Exception e)
                    {
                        return CustomRuntimeResult.FromError(e.ToString());
                    }
                }

                [Command("test")]
                [Summary("Excecutes provided birthday callback.")]
                public async Task<RuntimeResult> TestCallback(HolidayBehaviour.BehaviourAction behaviourType, string[] additionalParameters = null)
                {
                    CustomCommand callback = behaviourType == HolidayBehaviour.BehaviourAction.StartCallback ? Server.GetModule<BirthdayHandler>().BirthdayDictionary.StartCallback : Server.GetModule<BirthdayHandler>().BirthdayDictionary.EndCallback;

                    if (callback == default)
                        return CustomRuntimeResult.FromError($"Could not find callback for behaviour '{behaviourType}'!");


                    RuntimeResult result = CustomRuntimeResult.FromSuccess();

                    ulong origChannel = callback.TextChannelContextId;

                    try
                    {
                        callback.TextChannelContextId = Context.Channel.Id;
                        await callback.Execute(Context, additionalParameters);
                    }
                    catch (Exception e)
                    {
                        await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(TestCallback), "", e));
                        result = CustomRuntimeResult.FromError(e.Message);
                    }
                    finally
                    {
                        callback.TextChannelContextId = origChannel;
                    }


                    return result;
                }
            }
        }
    }

}
