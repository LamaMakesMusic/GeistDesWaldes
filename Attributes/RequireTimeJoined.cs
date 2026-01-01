using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Users;
using System;
using System.Threading.Tasks;

namespace GeistDesWaldes.Attributes
{
    public class RequireTimeJoined : PreconditionAttribute
    {
        private readonly TimeObject _minTimeJoined = new TimeObject();
        public TimeObject TimeJoined { get { return _minTimeJoined; } }

        public RequireTimeJoined(string minutes, string hours = "0", string days = "0", string months = "0", string years = "0")
        {
            int minute = 0;
            if (int.TryParse(minutes, out minute))
                _minTimeJoined.Minutes = minute;

            int hour = 0;
            if (int.TryParse(hours, out hour))
                _minTimeJoined.Hours = hour;

            int day = 0;
            if (int.TryParse(days, out day))
                _minTimeJoined.Days = day;

            int month = 0;
            if (int.TryParse(months, out month))
                _minTimeJoined.Months = month;

            int year = 0;
            if (int.TryParse(years, out year))
                _minTimeJoined.Years = year;
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            LogHandler logger = (LogHandler) services.GetService(typeof(LogHandler));

            string errorReason;

            if (context.Channel is ConsoleMessageChannel)
            {
                await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(RequireTimeJoined)}-Permission for '{nameof(ConsoleMessageChannel)}' -> Required: {_minTimeJoined}"), (int)ConsoleColor.Green);
                return PreconditionResult.FromSuccess();
            }

            IUser userInQuestion;

            if (context.User is MetaUser metaUser)
                userInQuestion = metaUser.FrontUser;
            else
                userInQuestion = context.User;

            // Check if this user is a Guild User, which is the only context where roles exist
            if (userInQuestion is IGuildUser gUser)
            {
                DateTimeOffset? joinedAt = gUser.JoinedAt;

                if (joinedAt.HasValue)
                {
                    TimeObject joinTime = new TimeObject((DateTime.UtcNow - joinedAt.Value.UtcDateTime), joinedAt.Value.UtcDateTime);

                    if (_minTimeJoined.LessEqualOrGreaterThan(joinTime) != 1)
                    {
                        await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Granted {nameof(RequireTimeJoined)}-Permission for '{command.Name}' -> Required: {_minTimeJoined} vs. {joinTime}"), (int)ConsoleColor.Green);

                        return PreconditionResult.FromSuccess();
                    }
                    else
                        errorReason = $"User needs to be a joined member of this guild for at least '{_minTimeJoined}' to use this command.";
                }
                else
                    errorReason = "Could not get 'joinedAt.Value' for User.";
            }
            else
                errorReason = "User is not a guild user.";

            await logger.Log(new LogMessage(LogSeverity.Debug, nameof(CheckPermissionsAsync), $"Denied {nameof(RequireTimeJoined)}-Permission for '{command.Name}' -> {errorReason}"), (int)ConsoleColor.Red);
            return PreconditionResult.FromError(errorReason);
        }

        public static async Task<TimeObject> GetTimeJoinedAsync(SocketUser socketUser, LogHandler logger)
        {
            if (socketUser is SocketGuildUser guildUser)
            {
                return await GetTimeJoinedAsync(guildUser, logger);
            }
            else
            {
                await logger.Log(new LogMessage(LogSeverity.Warning, nameof(GetTimeJoinedAsync), $"Could not parse SocketGuildUser."));
                return new TimeObject();
            }
        }
        public static async Task<TimeObject> GetTimeJoinedAsync(SocketGuildUser socketGuildUser, LogHandler logger)
        {
            DateTimeOffset? joinedAt = socketGuildUser.JoinedAt;
            TimeObject timeDifference;

            if (joinedAt.HasValue)
            {
                timeDifference = new TimeObject((DateTime.UtcNow - joinedAt.Value.UtcDateTime), joinedAt.Value.UtcDateTime);
            }
            else
            {
                timeDifference = new TimeObject();
                await logger.Log(new LogMessage(LogSeverity.Warning, nameof(GetTimeJoinedAsync), $"Could not get joinedAt.Value of {nameof(socketGuildUser)}!"));
            }

            return timeDifference;
        }
    }

    public class TimeObject
    {
        public int Seconds, Minutes, Hours, Days, Months, Years;
        public TimeObject()
        {
            Seconds = 0;
            Minutes = 0;
            Hours = 0;
            Days = 0;
            Months = 0;
            Years = 0;
        }
        public TimeObject(TimeSpan span, DateTime referenceDate)
        {
            Seconds = span.Seconds;
            Minutes = span.Minutes;
            Hours = span.Hours;
            Days = span.Days;
            Months = 0;
            Years = 0;


            int lastMonth = referenceDate.Month;
            int daysInMonth = DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month);

            while (Days > daysInMonth)
            {
                int delta = daysInMonth - (referenceDate.Day - 1);

                referenceDate = referenceDate.AddDays(delta);
                Days -= delta;

                if (lastMonth != referenceDate.Month)
                {
                    Months++;

                    if (Months > 11)
                    {
                        Months = 0;
                        Years++;
                    }

                    lastMonth = referenceDate.Month;

                    daysInMonth = DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month);
                }
            }
        }

        public int LessEqualOrGreaterThan(TimeObject timeObject)
        {
            //-1 = less, 0 = equal, 1 = greater

            if (Years < timeObject.Years)
                return -1;
            if (Years == timeObject.Years)
            {
                if (Months < timeObject.Months)
                    return -1;
                if (Months == timeObject.Months)
                {
                    if (Days < timeObject.Days)
                        return -1;
                    if (Days == timeObject.Days)
                    {
                        if (Hours < timeObject.Hours)
                            return -1;
                        if (Hours == timeObject.Hours)
                        {
                            if (Minutes < timeObject.Minutes)
                                return -1;
                            if (Minutes == timeObject.Minutes)
                            {
                                if (Seconds < timeObject.Seconds)
                                    return -1;
                                if (Seconds == timeObject.Seconds)
                                {
                                    return 0;
                                }
                                else
                                    return 1;
                            }
                            else
                                return 1;
                        }
                        else
                            return 1;
                    }
                    else
                        return 1;
                }
                else
                    return 1;
            }
            else
                return 1;
        }

        public override string ToString()
        {
            return $"{Years}Y, {Months}M, {Days}D, {Hours}h, {Minutes}m, {Seconds}s";
        }

        public string ToStringMinimal()
        {
            var timeBuilder = new System.Text.StringBuilder();

            if (Years > 0)
                timeBuilder.Append($" {Years} {(Years == 1 ? ReplyDictionary.YEAR : ReplyDictionary.YEARS)}");

            if (Months > 0)
                timeBuilder.Append($" {Months} {(Months == 1 ? ReplyDictionary.MONTH : ReplyDictionary.MONTHS)}");

            if (Days > 0)
                timeBuilder.Append($" {Days} {(Days == 1 ? ReplyDictionary.DAY : ReplyDictionary.DAYS)}");

            if (Hours > 0)
                timeBuilder.Append($" {Hours} {(Hours == 1 ? ReplyDictionary.HOUR : ReplyDictionary.HOURS)}");

            if (Minutes > 0)
                timeBuilder.Append($" {Minutes} {(Minutes == 1 ? ReplyDictionary.MINUTE : ReplyDictionary.MINUTES)}");

            if (Seconds > 0)
                timeBuilder.Append($" {Seconds} {(Seconds == 1 ? ReplyDictionary.SECOND : ReplyDictionary.SECONDS)}");

            return timeBuilder.ToString();
        }
    }

}
