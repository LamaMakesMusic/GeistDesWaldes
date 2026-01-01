using Discord.Commands;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Events;
using GeistDesWaldes.UserCommands;
using System;
using System.Threading.Tasks;

namespace GeistDesWaldes.Dictionaries
{
    public class ReplyDictionary
    {
        public const string AFFIRMATIVE = "Alles klar!";
        public const string NEGATIVE = "Oje...";
        public const string ERROR = "Fehler!";
        public const string INFORMATION = "Information";

        public const string SAVED = "Gespeichert";
        public const string ACTIONS = "Aktionen";
        public const string CALENDAR = "Kalender";
        public const string CITATIONS = "Zitate";

        public const string AUDIO = "Audio";
        public const string AUDIO_FILE_X_ADDED = "Ich habe die Audio File {x} hinzugefügt!";
        public const string AUDIO_FILE_X_REMOVED = "Ich habe die Audio File {x} entfernt!";
        public const string AUDIO_FILE_NAMED_X_ALREADY_EXISTS = "Eine Audio File mit dem Namen {x} existiert bereits!";
        public const string AUDIO_FILE_NAMED_X_DOES_NOT_EXIST = "Ich konnte keine Audio File mit dem Namen {x} finden!";
        public const string AUDIO_NO_VALID_ATTACHMENT_FOUND = "Ich konnte keinen passenden Anhang finden!";
        
        public const string FILE_TYPE_NOT_SUPPORTED = "Dieser Dateityp wird nicht unterstützt!";
        public const string PATH_MUST_NOT_END_ABOVE_START_DIRECTORY = "Der Pfad darf nicht außerhalb des Startverzeichnisses liegen!";

        public const string COUNTER = "Zähler";
        public const string COUNTER_X_CREATED = "Der Zähler **{x}** wurde angelegt!";
        public const string COUNTER_X_REMOVED = "Ich habe den Zähler **{x}** entfernt!";
        public const string COUNTER_X_INCREASED = "{x} erhöht!";
        public const string COUNTER_X_DECREASED = "{x} reduziert!";
        public const string COUNTER_UNDEFINED_PARAMETER_X = "Der Parameter '{x}' ist nicht definiert!";
        public const string COUNTER_WITH_NAME_ALREADY_EXISTS = "Es gibt schon einen Zähler mit diesem Namen.";
        public const string COULD_NOT_FIND_COUNTER_NAMED = "Ich konnte keinen Zähler finden, mit diesem Namen.";
        public const string COUNTER_X_DESCRIPTION_CHANGED = "Die Beschreibung von '{x}' wurde angepasst!";

        public const string COMMAND_X_CREATED = "Der Befehl **{x}** wurde angelegt!";
        public const string COMMAND_X_REMOVED = "Ich habe den Befehl **{x}** entfernt!";
        public const string COMMAND_X_EDITED = "Der Befehl **{x}** wurde angepasst!";
        public const string COMMAND_WITH_NAME_ALREADY_EXISTS = "Es gibt schon einen Befehl mit diesem Namen.";
        public const string COULD_NOT_FIND_COMMAND_NAMED = "Ich konnte keinen Befehl finden, mit diesem Namen.";
        public const string COULD_NOT_FIND_COMMANDS_IN_GROUP = "Ich konnte keine Befehle finden, in dieser Gruppe.";

        public const string CALLBACK_X_CREATED = "Der Callback **{x}** wurde registriert!";
        public const string CALLBACK_X_CLEARED = "Der Callback **{x}** wurde entfernt!";

        public const string CHANNEL_ID_MUST_NOT_BE_EMPTY = "Die Channel-ID darf nicht leer sein!";
        public const string CAN_ONLY_SET_NAME_OF_TEXT_VOICE_CHANNELS = "Nur Text/Voice Channels können umbenannt werden.";
        public const string CAN_ONLY_SET_TOPIC_OF_TEXT_CHANNELS = "Nur bei Text Channels kann das Topic verändert werden.";
        public const string COULD_NOT_FIND_CHANNEL_WITH_ID = "Ich konnte keinen Channel mit dieser ID finden.";
        public const string CHANNEL_IS_NOT_TEXT_NOR_VOICE_CHANNEL = "Der Channel ist weder Text- noch Voice-Channel";

        public const string EVENTS = "Events";
        public const string EVENT_TYPE = "Event-Art";
        public const string EVENT_NAMED_X_REMOVED = "Ich habe das Event **{x}** entfernt!";
        public const string EVENT_NAMED_X_ALREADY_EXISTS = "Es existiert bereits ein Event mit dem Namen **{x}**!";
        public const string COULD_NOT_FIND_EVENT_NAMED_Y = "Ich konnte kein Event mit dem Namen **{x}** finden!";
        public const string EVENT_RESCHEDULED = "Das Event wurde verlegt!";
        public const string EVENT_CREATED = "Das Event wurde erstellt!";

        public const string COULD_NOT_PROCESS_COMMAND_WITH_NAME_X = "Ich konnte den Befehl mit dem Namen '{x}' nicht verarbeiten!";
        public const string COULD_NOT_FIND_DISCORD_COMMAND_INFO_FOR_COMMAND_NAMED_X = "Ich konnte die Discord.CommandInfo für '{x}' nicht finden.";
        public const string PARAMETER_COUNT_X_DOES_NOT_MATCH_REQUIRED_COUNT_Y = "Die gegebene Parameteranzahl {x} stimmt nicht mit der erforderlichen Anzahl überein {y}.";

        public const string COULD_NOT_PARSE_X_TO_Y = "Ich konnte den string '{x}' nicht parsen zu '{y}'";

        public const string PARAMETER_MUST_NOT_BE_EMPTY = "Der Parameter darf nicht leer sein!";

        public const string ERROR_UNSUCCESSFUL = "Da ist wohl etwas schiefgegangen.";
        public const string ERROR_UNMET_PRECONDITION = "Dir fehlen für diesen Befehl die nötigen Rechte!";
        public const string ERROR_INCOMPLETE_QUOTED_PARAMETER = "Du scheinst da ein paar Anführungszeichen vergessen zu haben.";
        public const string ERROR_TOO_MANY_PARAMETERS = "Du hast zu viele Parameter für diesen Befehl angegeben!";
        public const string ERROR_UNKNOWN_COMMAND = "Ich kenne diesen Befehl nicht!";

        public const string BLACKLIST = "Blacklist";
        public const string USER_ALREADY_ON_BLACKLIST = "Der User wurde bereits der Blacklist hinzugefügt.";
        public const string USER_NOT_ON_BLACKLIST = "Der User ist zur Zeit nicht auf der Blacklist.";
        public const string USER_X_NOW_ON_BLACKLIST = "'{x}' wurde der Blacklist hinzugefügt.";
        public const string USER_X_REMOVED_FROM_BLACKLIST = "'{x}' wurde von der Blacklist entfernt.";

        public const string COULD_NOT_SPLIT_TO_ARRAY = "Die Zeichenkette konnte nicht zu einem Array geteilt werden!";
        public const string GROUP_IS_MISSING_END_IDENTIFIER_X = "Die Gruppe besitzt keine End-Markierung '{x}'!";
        public const string GROUP_IS_MISSING_START_IDENTIFIER_X = "Die Gruppe besitzt keine Start-Markierung '{x}'!";

        public const string UPTIME = "Streamzeit";
        public const string X_HAS_BEEN_STREAMING_FOR_Y = "{x} streamt seit {y}";
        public const string X_LAST_STREAM_Y_AGO = "{x} streamte zuletzt vor {y}!";
        public const string X_IS_NOT_STREAMING = "{x} streamt gerade nicht!";

        public const string NOTHING_HAPPENED_TODAY = "Unglaublich! Heute scheint nichts passiert zu sein.";

        public const string NO_VOICE_CHANNEL_FOUND = "Du musst einen Audiokanal angeben, oder selbst einem beitreten!";
        public const string FILE_DOES_NOT_EXIST = "Diese Datei existiert nicht!";

        public const string CATEGORY = "Kategorie";
        public const string CATEGORIES = "Kategorien";
        public const string CATEGORY_ALREADY_EXISTS = "Diese Kategorie existiert bereits!";
        public const string CATEGORY_DOES_NOT_EXISTS = "Diese Kategorie existiert nicht!";
        public const string CATEGORY_X_CREATED = "Die Kategorie **{x}** wurde angelegt!";
        public const string CATEGORY_X_REMOVED = "Ich habe die Kategorie **{x}** entfernt!";
        public const string CATEGORY_X_EDITED = "Die Kategorie **{x}** wurde angepasst!";

        public const string COMMAND_ONLY_VALID_ON_TWITCH = "Dieser Befehl kann nur auf Twitch verwendet werden!";
        public const string COMMAND_ONLY_VALID_ON_DISCORD = "Dieser Befehl kann nur auf Discord verwendet werden!";
        public const string COMMAND_ONLY_VALID_IN_PRIVATE_CHANNEL = "Dieser Befehl kann nur per Privatnachricht verwendet werden!";

        public const string REMOVED_Y_POINTS_FROM_X = "{x} wurden {y} Punkt(e) abgezogen!";
        public const string X_RECEIVED_Y_POINTS = "{x} hat {y} Punkt(e) erhalten!";
        public const string X_HAS_GATHERED_Y_POINTS = "{x} hat {y} Punkt(e) gesammelt!";

        public const string PARAMETER_MUST_BE_GREATER_X = "Der Parameter muss größer als {x} sein!";
        public const string PARAMETER_MUST_BE_GREATER_OR_EQUAL_X = "Der Parameter muss größer oder gleich {x} sein!";
        public const string PARAMETER_MUST_BE_BETWEEN_X_AND_Y = "Der Parameter muss zwischen {x} und {y} liegen!";

        public const string YOU_ARE_LACKING_POINTS_FOR_THIS_ACTION = "Dir fehlen die nötigen Punkte für diese Aktion!";
        public const string X_TRANSFERED_Y_POINTS_TO_Z = "{x} transferierte {y} Punkte an {z}!";

        public const string X_IS_NOT_A_FOLLOWER_YET = "{x} ist noch kein Follower!";
        public const string POINTS_PER_X_MINUTES = "Punkte pro {x} Minute(n)";
        public const string BONUS_POINTS_PER_X_MINUTES = "Bonus Punkte pro {x} Minute(n)";
        public const string CURRENTLY_X_POINTS = "Zur Zeit gibt es {x} Punkt(e) pro Intervall.";
        public const string CURRENTLY_X_BONUS_POINTS = "Aktive Chatter erhalten {x} Bonuspunkt(e) pro Intervall.";

        public const string POINTS = "Punkte";

        public const string COOLDOWN_IN_SECONDS = "Cooldown in Sekunden";
        public const string COOLDOWN_IS_X_SECONDS_BETWEEN_COMMANDS = "Der Cooldown beträgt {x} Sekunden zwischen Befehlen.";
        public const string COMMAND_X_IS_STILL_ON_COOLDOWN_FOR_Y_SECONDS = "'{x}' ist noch {y} Sekunden im Cooldown!";
        public const string CATEGORY_X_IS_CURRENTLY_LOCKED = "Die Kategorie '{x}' ist zur Zeit gesperrt!";

        public const string CURRENT_LOG_LEVEL = "Aktuelles Log Level";

        public const string X_HAS_BEEN_PART_OF_Y_FOR_Z_TIME = "{x} ist schon {z} bei {y}!";
        public const string X_HAS_BEEN_FOLLOWING_Y_FOR_Z_TIME = "{x} folgt {y} schon {z}!";
        public const string X_HAS_WATCHED_FOR_Y_TIME = "{x} hat bereits {y} lang zugesehen!";

        public const string YEARS = "Jahre", YEAR = "Jahr";
        public const string MONTHS = "Monate", MONTH = "Monat";
        public const string DAYS = "Tage", DAY = "Tag";
        public const string HOURS = "Stunden", HOUR = "Stunde";
        public const string MINUTES = "Minuten", MINUTE = "Minute";
        public const string SECONDS = "Sekunden", SECOND = "Sekunde";

        public const string LEADERBOARD = "Punktetafel";

        public const string COULD_NOT_FIND_HOLIDAY_NAMED_X = "Ich konnte keinen Feiertag mit dem Namen '{x}' finden.";
        public const string THAT_DAY_IS_NOT_A_HOLIDAY = "Dieser Tag ist kein Feiertag.";
        public const string THAT_DAY_IS_A_HOLIDAY = "Dieser Tag ist ein Feiertag!";
        public const string THE_NEXT_HOLIDAY_IS_X = "Der nächste Feiertag ist {x}!";

        public const string SAVED_HOLIDAY_BEHAVIOUR_FOR_X = "Ich habe das Feiertagsverhalten für '{x}' gespeichert!";
        public const string REMOVED_HOLIDAY_BEHAVIOUR_FOR_X = "Ich habe das Feiertagsverhalten für '{x}' entfernt!";


        public const string ACTIVE_TEMPLATE = "Aktives Template";
        public const string TEMPLATES = "Templates";
        public const string COULD_NOT_FIND_ACTIVE_TEMPLATE = "Ich konnte kein aktives Template finden!";

        public const string TEMPLATE_NAMED_X_ALREADY_EXISTS = "Ein Template mit dem Namen '{x}' existiert bereits!";
        public const string TEMPLATE_NAMED_X_DOES_NOT_EXISTS = "Es existiert kein Template mit dem Namen '{x}'!";

        public const string TEMPLATE_X_CREATED = "Ich habe das Template '{x}' angelegt!";
        public const string TEMPLATE_X_REMOVED = "Ich habe das Template '{x}' entfernt!";
        public const string TEMPLATE_X_APPLIED = "Ich habe das Template '{x}' aktiviert!";
        public const string TEMPLATE_X_REVERTED = "Ich habe das Template '{x}' deaktiviert!";

        public const string CHANNEL_X_ALREADY_IN_TEMPLATE = "Der Channel '{x}' ist bereits Teil dieses Templates!";
        public const string CHANNEL_X_IS_NOT_IN_TEMPLATE = "Der Channel '{x}' ist kein Teil dieses Templates!";
        public const string CHANNEL_X_ADDED_TO_TEMPLATE = "Ich habe dem Template den Channel '{x}' hinzugefügt!";
        public const string CHANNEL_X_REMOVED_FROM_TEMPLATE = "Ich habe den Channel '{x}' aus dem Template entfernt!";

        public const string MAP_OF_THIS_KIND_ALREADY_EXISTS_FOR_THIS_CONSTELLATION = "Eine solche Map existiert bereits für diese Konstellation.";
        public const string MAP_OF_THIS_KIND_DOES_NOT_EXISTS_FOR_THIS_CONSTELLATION = "Eine solche Map existiert nicht für diese Konstellation.";

        public const string MAP_ADDED_TO_CHANNEL = "Ich habe die Map dem Channel Layout hinzugefügt!";
        public const string MAP_REMOVED_FROM_CHANNEL = "Ich habe die Map aus dem Channel Layout entfernt!";

        public const string COULD_NOT_GET_GUILD_FROM_COMMANDCONTEXT = "Ich konnte keine Gilde aus dem CommandContext beziehen!";

        public const string COULD_NOT_FIND_BIRTHDAY_FOR_X = "Ich konnte keinen Eintrag für den Geburtstag von '{x}' finden!";
        public const string BIRTHDAY_FOR_USER_X_ALREADY_EXISTS = "Es existiert bereits ein Eintrag für den Geburtstag von '{x}'!";
        public const string X_BIRTHDAY_IS_ON_Y = "Der Geburtstag von '{x}' ist am {y}!";
        public const string REMOVED_BIRTHDAY_ENTRY_FOR_X = "Ich habe den Geburtstag von '{x}' wurde entfernt!";

        public const string SAVED_CALLBACK_X = "Ich habe den Callback '{x}' gespeichert!";
        public const string UPDATED_CALLBACK_X = "Ich habe den Callback '{x}' aktualisiert!";

        public const string BIRTHDAYS = "Geburtstage";

        public const string COULD_NOT_FIND_FILES = "Ich konnte die Datei(en) nicht finden!";

        public const string SUCH_PROCESS_IS_ALREADY_ACTIVE_TRY_AGAIN_LATER = "Ein solcher Prozess ist bereits aktiv! Bitte versuche es später noch einmal.";

        public const string TWITCH = "Twitch";
        public const string TWITCH_API_AUTHORIZATION_FAILED = "Die Twitch API kann nicht genutzt werden, da eine gültige Authentifizierung fehlt!";
        public const string USER_X_NOT_A_TWITCH_USER = "Twitch User mit dem Namen '{x}' konnte nicht gefunden werden!";

        public const string UNKNOWN_AUTHOR = "Unbekannter Waldbewohner";
        public const string QUOTE_CONTENT_IS_EMPTY = "Das Zitat darf nicht leer sein!";
        public const string QUOTE_WITH_ID_X_NOT_FOUND = "Es existiert kein Zitat mit der ID '{x}'";
        public const string NO_QUOTES_CREATED = "In einem Wald ohne Bäume, ist der Holzfäller arbeitslos.";
        public const string QUOTE_SAVED = "Zitat gespeichert!";
        public const string QUOTE_MODIFIED = "Zitat geändert!";
        public const string QUOTE_DELETED = "Zitat gelöscht!";

        public const string RESULTS = "Ergebnis(se)";

        public const string POLLS = "Abstimmungen";
        public const string POLL_MAX_POLLS_X_PER_CHANNEL_REACHED = "Es dürfen maximal {X} Umfragen pro Channel existieren!";
        public const string POLL_NOT_FOUND = "Ich konnte diese Abstimmung nicht finden!";
        public const string POLL_STARTED = "Abstimmung gestartet!";
        public const string POLL_STOPPED = "Abstimmung beendet!";
        public const string POLL_WITH_NAME_ALREADY_EXISTS = "Eine Abstimmung mit diesem Namen existiert bereits!";
        public const string POLL_HAS_NO_OPTIONS = "Die Abstimmung hat keine Optionen!";
        public const string POLL_VOTE_USING_PREFIX_X = "Nutze das Präfix '{x}' um abzustimmen!";

        public const string VOTES = "Stimmen";
        public const string ID = "ID";
        public const string DESCRIPTION = "Beschreibung";

        public const string USER_CAN_NOT_BE_YOURSELF = "Du kannst nicht selbst das Ziel dieser Aktion sein!";

        public const string USER_NOT_FOUND = "Dieser Nutzer wurde nicht gefunden!";

        public const string DATA_OF_X = "Daten von {x}";
        public const string BIRTHDAY = "Geburtstag";

        public const string HOLIDAY_BEHAVIOUR = "Feiertagsverhalten";

        public const string VALUE_X_IS_SET_TO_Y = "Der Wert von '{x}' beträgt aktuell '{y}'!";
        public const string VALUE_X_MODIFIED_Y_TO_Z = "Wert '{x}' erfolgreich geändert! '{y}' -> '{z}'";

        public const string WATCHTIME_ADJUSTED = "Twitch Watchtime angepasst!";


        public const string INTERVAL_ACTION_X_CREATED = "Die Interval Aktion {x} wurde angelegt!";
        public const string INTERVAL_ACTION_X_REMOVED = "Die Interval Aktion {x} wurde entfernt!";
        public const string INTERVAL_ACTION_NAMED_X_ALREADY_EXISTS = "Es existiert bereits eine Interval Aktion mit dem Namen {x}.";
        public const string INTERVAL_ACTION_NAMED_X_NOT_FOUND = "Ich konnte keine Interval Aktion mit dem Namen {x} finden.";
        public const string INTERVAL_ACTIONS_SORTED = "Die Interval Aktionen wurden neu sortiert!";

        public const string STATISTICS = "Statistiken";
        public const string STATISTICS_NAME_ALREADY_EXISTS = "Es existiert bereits eine Statistik mit diesem Namen!";
        public const string STATISTICS_END_SMALLER_START = "Die Startzeit muss vor der Endzeit liegen!";
        public const string STATISTICS_CREATED_SUCCESSFULLY = "Die Statistik wurde erfolgreich angelegt!";
        public const string STATISTICS_NAME_NOT_FOUND = "Eine Statistik mit diesem Namen konnte nicht gefunden werden!";
        public const string STATISTICS_REMOVED_SUCCESSFULLY = "Die Statistik wurde erfolgreich gelöscht!";
        public const string STATISTICS_STARTED_RECORDING = "Aufzeichnung erfolgreich gestartet!";
        public const string STATISTICS_STOPPED_RECORDING = "Aufzeichnung erfolgreich beendet!";

        public static string GetOutputTextForEnum(Enum enumeration)
        {
            if (enumeration is ScheduledEvent.RepetitionOption repetitionOption)
            {
                switch (repetitionOption)
                {
                    case ScheduledEvent.RepetitionOption.Once:
                        return "einmalig";
                    case ScheduledEvent.RepetitionOption.Minutely:
                        return "minütlich";
                    case ScheduledEvent.RepetitionOption.Hourly:
                        return "stündlich";
                    case ScheduledEvent.RepetitionOption.Daily:
                        return "täglich";
                    case ScheduledEvent.RepetitionOption.Weekly:
                        return "wöchentlich";
                    case ScheduledEvent.RepetitionOption.Monthly:
                        return "monatlich";
                    case ScheduledEvent.RepetitionOption.Yearly:
                        return "jährlich";
                    default:
                        return repetitionOption.ToString();
                }
            }
            else if (enumeration is Currency.CurrencyCustomization.ToStringType toStringType)
            {
                switch (toStringType)
                {
                    case Currency.CurrencyCustomization.ToStringType.Points:
                        return "Punkte Übersicht";
                    case Currency.CurrencyCustomization.ToStringType.NotEnough:
                        return "Nicht genug Punkte";
                    case Currency.CurrencyCustomization.ToStringType.Transfer:
                        return "Übertragen von Punkten";
                    default:
                        return toStringType.ToString();
                }
            }
            else if (enumeration is UserCallbackDictionary.DiscordCallbackTypes discordCallbacks)
            {
                switch (discordCallbacks)
                {
                    case UserCallbackDictionary.DiscordCallbackTypes.OnClientReady:
                        return "Client Bereit";
                    case UserCallbackDictionary.DiscordCallbackTypes.OnUserJoinedGuild:
                        return "Gildenbeitritt";
                    default:
                        return discordCallbacks.ToString();
                }
            }
            else if (enumeration is UserCallbackDictionary.TwitchCallbackTypes twitchCallbacks)
            {
                switch (twitchCallbacks)
                {
                    case UserCallbackDictionary.TwitchCallbackTypes.OnStreamStart:
                        return "Stream Beginn";
                    case UserCallbackDictionary.TwitchCallbackTypes.OnStreamUpdate:
                        return "Stream Update";
                    case UserCallbackDictionary.TwitchCallbackTypes.OnStreamEnd:
                        return "Stream Ende";
                    case UserCallbackDictionary.TwitchCallbackTypes.OnFollow:
                        return "Follow";
                    case UserCallbackDictionary.TwitchCallbackTypes.OnUserIntro:
                        return "User Intro";
                    case UserCallbackDictionary.TwitchCallbackTypes.OnStreamStartOneShot:
                        return "Stream Start (einmalig)";
                    case UserCallbackDictionary.TwitchCallbackTypes.OnStreamEndOneShot:
                        return "Stream Ende (einmalig)";
                    default:
                        return twitchCallbacks.ToString();
                }
            }


            return "UNDEFINED";
        }

        public static Task<string> ReplaceStringInvariantCase(string input, string replacePattern, string replaceValue)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrEmpty(input))
                    input = "";
                if (string.IsNullOrEmpty(replaceValue))
                    replaceValue = "";

                int idx = input.IndexOf(replacePattern, StringComparison.OrdinalIgnoreCase);

                if (idx < 0)
                    return input;
                else
                    return input.Insert(idx, replaceValue).Remove(idx + replaceValue.Length, replacePattern.Length);
            });
        }

        public static async Task<ChannelMessage> GetValueModifiedMessage(ICommandContext context, string valueName, string oldValue, string newValue)
        {
            string description = await ReplaceStringInvariantCase(VALUE_X_MODIFIED_Y_TO_Z, "{x}", valueName);
            description = await ReplaceStringInvariantCase(description, "{y}", oldValue);
            description = await ReplaceStringInvariantCase(description, "{z}", newValue);

            ChannelMessage msg = new ChannelMessage(context)
                .SetTemplate(ChannelMessage.MessageTemplateOption.Modified)
                .AddContent(new ChannelMessageContent()
                    .SetDescription(description)
                );

            return msg;
        }

        public static async Task<ChannelMessage> GetValueMessage(ICommandContext context, string valueName, string value)
        {
            string description = await ReplaceStringInvariantCase(VALUE_X_IS_SET_TO_Y, "{x}", valueName);
            description = await ReplaceStringInvariantCase(description, "{y}", value);

            ChannelMessage msg = new ChannelMessage(context)
                .SetTemplate(ChannelMessage.MessageTemplateOption.Information)
                .AddContent(new ChannelMessageContent()
                    .SetDescription(description)
                );

            return msg;
        }
    }
}
