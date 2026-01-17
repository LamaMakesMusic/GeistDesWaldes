using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.UserCommands;
using GeistDesWaldes.Users;

namespace GeistDesWaldes.Calendar;

public class BirthdayHandler : BaseHandler
{
    private const string BIRTHDAYS_FILE_NAME = "Birthdays";

    private readonly ForestUserHandler _forestUserHandler;

    private Task _birthdayWatchdog;
    private CancellationTokenSource _cancelWatchdogSource;
    public BirthdayDictionary BirthdayDictionary;


    public BirthdayHandler(Server server, ForestUserHandler userHandler) : base(server)
    {
        _forestUserHandler = userHandler;
        BirthdayDictionary = new BirthdayDictionary(server);
    }

    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();

        await InitializeBirthdayHandler();
        StartBirthdayWatchdog();
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


    private async Task InitializeBirthdayHandler()
    {
        await GenericXmlSerializer.EnsurePathExistance(Server.LogHandler, Server.ServerFilesDirectoryPath, BIRTHDAYS_FILE_NAME, BirthdayDictionary);

        await LoadBirthdaysFromFile();
    }

    private async Task CheckIntegrity()
    {
        List<string> problematicEntries = new();

        StringBuilder builder = new("...Callbacks:");
        int startLength = builder.Length;

        for (int i = 0; i < 2; i++)
        {
            CustomCommand command = i == 0 ? BirthdayDictionary.StartCallback : BirthdayDictionary.EndCallback;

            StringBuilder subBuilder = new($"......[{i} -> {command.Name}]");
            int subLength = subBuilder.Length;

            if (command.CommandsToExecute is { Length: > 0 })
            {
                if (string.IsNullOrWhiteSpace(command.Name))
                {
                    subBuilder.Append(" | missing name");
                }

                CustomRuntimeResult testResult = await command.TestCommandExecution(Server.CommandService, Server.Services);

                if (!testResult.IsSuccess)
                {
                    subBuilder.Append(" | Commands ERROR:\n").AppendLine($".........{testResult.Reason}");
                }
            }

            if (subBuilder.Length > subLength)
            {
                builder.Append(subBuilder.ToString());
            }
        }

        if (builder.Length > startLength)
        {
            problematicEntries.Add(builder.ToString());
        }


        builder.Clear();
        builder.Append("...Entries:");
        startLength = builder.Length;

        if (builder.Length > startLength)
        {
            problematicEntries.Add(builder.ToString());
        }


        if (problematicEntries.Count > 0)
        {
            StringBuilder bbuilder = new("Birthdays ERROR:\n");

            for (int i = 0; i < problematicEntries.Count; i++)
            {
                bbuilder.AppendLine(problematicEntries[i]);
            }

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
        }
        else
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Birthdays OK."), (int)ConsoleColor.DarkGreen);
        }
    }

    private void StartBirthdayWatchdog()
    {
        if (_birthdayWatchdog == null)
        {
            _birthdayWatchdog = Task.Run(BirthdayWatchdog);
        }
    }


    private async Task BirthdayWatchdog()
    {
        _cancelWatchdogSource = new CancellationTokenSource();

        await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(BirthdayWatchdog), "Started."));

        try
        {
            while (!_cancelWatchdogSource.IsCancellationRequested)
            {
                // I.a. deactivate active birthdays
                if (BirthdayDictionary.ActiveBirthdays != null && BirthdayDictionary.ActiveBirthdays.Count > 0)
                {
                    await Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(BirthdayWatchdog), $"{BirthdayDictionary.ActiveBirthdays.Count} already active birthdays!"), (int)ConsoleColor.Blue);

                    for (int i = BirthdayDictionary.ActiveBirthdays.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            Birthday bday = BirthdayDictionary.ActiveBirthdays[i];

                            // If birthday is over, end
                            if (bday.BirthDate.Day != DateTime.Today.Day || bday.BirthDate.Month != DateTime.Today.Month)
                            {
                                await InvokeBirthdayBehaviour(bday, HolidayBehaviour.BehaviourAction.EndCallback);

                                BirthdayDictionary.ActiveBirthdays.RemoveAt(i);
                            }
                        }
                        catch (Exception e)
                        {
                            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(BirthdayWatchdog), "Could not end active birthday!", e));
                        }
                    }
                }


                // I.a. activate todays birthdays
                Birthday[] birthdays = await GetBirthdays(DateTime.Today);
                if (birthdays != null)
                {
                    await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(BirthdayWatchdog), $"Daily Watchdog found {birthdays.Length} birthdays!"), (int)ConsoleColor.Blue);

                    BirthdayDictionary.ActiveBirthdays ??= [];

                    foreach (Birthday bday in birthdays)
                    {
                        try
                        {
                            if (BirthdayDictionary.ActiveBirthdays.Exists(b => b.UserId == bday.UserId))
                            {
                                await Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(BirthdayWatchdog), $"Skipping active birthday for user '{bday.UserId}'!"));
                                continue;
                            }

                            await InvokeBirthdayBehaviour(bday, HolidayBehaviour.BehaviourAction.StartCallback);

                            BirthdayDictionary.ActiveBirthdays.Add(bday);
                        }
                        catch (Exception e)
                        {
                            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(BirthdayWatchdog), "Could not start birthday!", e));
                        }
                    }
                }


                // Save current active birthdays
                await SaveBirthdaysToFile();


                DateTime morning = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 1).AddDays(1);
                TimeSpan difference = morning.Subtract(DateTime.Now);

                await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(BirthdayWatchdog), $"Daily Watchdog called. Next call in: {difference}"), (int)ConsoleColor.Blue);


                await Task.Delay(difference, _cancelWatchdogSource.Token);
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            _birthdayWatchdog = null;
            _cancelWatchdogSource = null;

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(BirthdayWatchdog), "Stopped."));
        }
    }

    private async Task InvokeBirthdayBehaviour(Birthday birthday, HolidayBehaviour.BehaviourAction actionType)
    {
        CustomRuntimeResult<ForestUser> getUserResult = await _forestUserHandler.GetUser(birthday.UserId);

        if (getUserResult.IsSuccess && getUserResult.ResultValue is { } fUser)
        {
            RestUser restUser = await Launcher.Instance.DiscordClient.Rest.GetUserAsync(fUser.DiscordUserId);
            if (restUser != null)
            {
                if (actionType == HolidayBehaviour.BehaviourAction.StartCallback)
                {
                    await BirthdayDictionary.StartCallback.Execute(null, [
                        restUser.Mention, getUserResult.ResultValue.Name
                    ]);
                }
                else
                {
                    await BirthdayDictionary.EndCallback.Execute(null, [
                        restUser.Mention, getUserResult.ResultValue.Name
                    ]);
                }
            }
            else
            {
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(InvokeBirthdayBehaviour), $"Could not find discord user for '{fUser.Name}' ({fUser.DiscordUserId})!"));
            }
        }
        else
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(InvokeBirthdayBehaviour), getUserResult.Error.ToString()));
        }
    }

    private async Task UpdateBirthdayInfo()
    {
        if (BirthdayDictionary.Birthdays == null || BirthdayDictionary.Birthdays.Count == 0)
        {
            return;
        }

        bool changed = false;

        for (int i = BirthdayDictionary.Birthdays.Count - 1; i >= 0; i--)
        {
            CustomRuntimeResult<ForestUser> getUserResult = await _forestUserHandler.GetUser(BirthdayDictionary.Birthdays[i].UserId);
            if (getUserResult.IsSuccess)
            {
                continue;
            }

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateBirthdayInfo), getUserResult.Reason));
            BirthdayDictionary.Birthdays.RemoveAt(i);

            changed = true;
        }

        if (changed)
        {
            await SaveBirthdaysToFile();
        }
    }

    public async Task<Birthday[]> GetBirthdays(DateTime date)
    {
        await UpdateBirthdayInfo();

        return BirthdayDictionary.Birthdays.FindAll(b => b.BirthDate.Day == date.Day && b.BirthDate.Month == date.Month).ToArray();
    }

    public async Task<Birthday[]> GetUpcomingBirthdays(int maxEntries)
    {
        await UpdateBirthdayInfo();

        if (maxEntries < 1)
        {
            maxEntries = 1;
        }

        if (BirthdayDictionary.Birthdays.Count < maxEntries)
        {
            maxEntries = BirthdayDictionary.Birthdays.Count;
        }

        var result = new List<Birthday>();
        DateTime startDate = DateTime.Today.AddDays(1);

        int startIndex = 0;
        for (int i = 0; i < BirthdayDictionary.Birthdays.Count; i++)
        {
            if (BirthdayDictionary.Birthdays[i].BirthDate.Month < startDate.Month)
            {
                continue;
            }

            if (BirthdayDictionary.Birthdays[i].BirthDate.Month == startDate.Month && BirthdayDictionary.Birthdays[i].BirthDate.Day < startDate.Day)
            {
                continue;
            }

            startIndex = i;
            break;
        }

        int endIndex = startIndex + maxEntries;
        int restAmount = 0;
        for (int i = startIndex; i < endIndex; i++)
        {
            if (i < BirthdayDictionary.Birthdays.Count)
            {
                result.Add(BirthdayDictionary.Birthdays[i]);
            }
            else
            {
                restAmount = startIndex + maxEntries - i;
                break;
            }
        }

        if (restAmount > 0)
        {
            if (restAmount > BirthdayDictionary.Birthdays.Count)
            {
                restAmount = BirthdayDictionary.Birthdays.Count;
            }

            if (startIndex < restAmount)
            {
                restAmount = startIndex;
            }


            for (int i = 0; i < restAmount; i++)
            {
                result.Add(BirthdayDictionary.Birthdays[i]);
            }
        }

        return result.ToArray();
    }


    public async Task<CustomRuntimeResult<Birthday>> GetBirthday(Guid userId)
    {
        await UpdateBirthdayInfo();

        Birthday result = BirthdayDictionary.Birthdays.Find(b => b.UserId == userId);

        if (result != null)
        {
            return CustomRuntimeResult<Birthday>.FromSuccess(value: result);
        }

        return CustomRuntimeResult<Birthday>.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COULD_NOT_FIND_BIRTHDAY_FOR_X, "{x}", userId.ToString()));
    }

    public async Task<CustomRuntimeResult> AddBirthday(ForestUser user, DateTime date)
    {
        if ((await GetBirthday(user.ForestUserId)).IsSuccess)
        {
            return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.BIRTHDAY_FOR_USER_X_ALREADY_EXISTS, "{x}", user.Name));
        }

        BirthdayDictionary.Birthdays.Add(new Birthday(user.ForestUserId, date));

        return CustomRuntimeResult.FromSuccess();
    }

    public async Task<CustomRuntimeResult> RemoveBirthday(ForestUser user)
    {
        CustomRuntimeResult<Birthday> result = await GetBirthday(user.ForestUserId);

        if (result.IsSuccess)
        {
            BirthdayDictionary.Birthdays.Remove(result.ResultValue);
        }

        return result;
    }


    public Task SaveBirthdaysToFile()
    {
        BirthdayDictionary.Birthdays.Sort((b1, b2) =>
        {
            DateTime d1 = new(1, b1.BirthDate.Month, b1.BirthDate.Day);
            DateTime d2 = new(1, b2.BirthDate.Month, b2.BirthDate.Day);

            return d1.CompareTo(d2);
        });

        return GenericXmlSerializer.SaveAsync<BirthdayDictionary>(Server.LogHandler, BirthdayDictionary, BIRTHDAYS_FILE_NAME, Server.ServerFilesDirectoryPath);
    }

    public async Task LoadBirthdaysFromFile()
    {
        BirthdayDictionary loadedDictionary = await GenericXmlSerializer.LoadAsync<BirthdayDictionary>(Server.LogHandler, BIRTHDAYS_FILE_NAME, Server.ServerFilesDirectoryPath);

        if (loadedDictionary == null)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadBirthdaysFromFile), $"Loaded {nameof(loadedDictionary)} == DEFAULT"));
        }
        else
        {
            BirthdayDictionary = loadedDictionary;
        }

        BirthdayDictionary.Birthdays.Sort((b1, b2) => b1.BirthDate.CompareTo(b2.BirthDate));

        // Ensure correct form
        BirthdayDictionary.StartCallback?.InitAfterLoadFromFile(Server);
        BirthdayDictionary.EndCallback?.InitAfterLoadFromFile(Server);

        await UpdateBirthdayInfo();
    }
}