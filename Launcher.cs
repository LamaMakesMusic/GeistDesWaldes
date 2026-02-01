using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using GeistDesWaldes.Configuration;
using GeistDesWaldes.Misc;

namespace GeistDesWaldes;

public static class Launcher
{
    public static readonly Random Random = new();

    private static bool _requestedShutdown;
    private static bool _isRestart;

    private static int _restartDelayInSeconds = -1;

    public static Program Instance { get; private set; }

    public static string BaseDirectory { get; private set; }
    public static string ExecutingAssemblyName { get; private set; }
    public static string ApplicationFilesPath { get; private set; }
    public static string CommonFilesPath { get; private set; }


    public static async Task Main(string[] args)
    {
        BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        ExecutingAssemblyName = Assembly.GetExecutingAssembly().FullName;

        ApplicationFilesPath = Path.GetFullPath(Path.Combine(BaseDirectory, "..", "data"));
        await GenericXmlSerializer.EnsurePathExistence<object>(null, ApplicationFilesPath);

        CommonFilesPath = Path.GetFullPath(Path.Combine(ApplicationFilesPath, "common"));
        await GenericXmlSerializer.EnsurePathExistence<object>(null, CommonFilesPath);

        // outside of loop allows keeping changes on restart
        ApplyStartArguments(args);

        // Console ReadLine() Loop
        //Task inputLoop = Task.Run(LocalInputLoop);

        do
        {
            Reset();

            await ConfigurationHandler.Init();

            // Main Program
            Instance = new Program();
            await Instance.StartUp();

            // Wait for shutdown
            while (!_requestedShutdown)
            {
                if (ConfigurationHandler.Shared.DailyRestartTime == TimeSpan.Zero)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
                else
                {
                    await Task.Delay(GetNextRestartTime());
                    RequestShutdown(true, ConfigurationHandler.Shared.DailyRestartDelayInSeconds);
                }
            }

            await Instance.Shutdown();

            // Shutdown grace period
            await ScreenTimer("Waiting some more time for processes to cancel... proceeding", 10);

            if (_isRestart)
            {
                await ScreenTimer("Restarting", Math.Max(1, _restartDelayInSeconds));
            }
        } while (_isRestart);


        Environment.Exit(0);
    }


    private static void ApplyStartArguments(string[] args)
    {
        if (args == null)
        {
            return;
        }

        foreach (string a in args)
        {
            if (a.Equals(CONSOLE_OUTPUT_ONLY_ID, StringComparison.OrdinalIgnoreCase))
            {
                ConsoleOutputOnly = true;
            }
            else if (a.IndexOf(LOG_LEVEL_ID, StringComparison.OrdinalIgnoreCase) is { } idx and > -1)
            {
                if (int.TryParse(a.Remove(idx, LOG_LEVEL_ID.Length), out int logLevel))
                {
                    LogLevel = logLevel;
                }
            }
        }
    }

    private static void Reset()
    {
        Instance = null;

        _requestedShutdown = false;
        _isRestart = false;
        _restartDelayInSeconds = -1;

        GC.Collect();
    }

    private static async Task ScreenTimer(string label, int timerInSeconds, int delayInSeconds = 0)
    {
        if (delayInSeconds > 0)
        {
            await Task.Delay(delayInSeconds * 1000);
        }

        if (timerInSeconds < 0)
        {
            timerInSeconds = 0;
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"{label} in {timerInSeconds} seconds. At {DateTime.Now.AddSeconds(timerInSeconds)}");

        await Task.Delay(timerInSeconds * 1000);

        // TODO: Compatibility for Linux
        //for (int i = 0; i < timerInSeconds; i++)
        //{
        //    Console.SetCursorPosition(0, Console.CursorTop);
        //    Console.Write(new string(' ', Console.WindowWidth));
        //    Console.SetCursorPosition(0, Console.CursorTop);
        //    Console.Write(new string('#', timerInSeconds - (i + 1)));

        //    await Task.Delay(1000);
        //}
    }


    //private static async Task LocalInputLoop()
    //{
    //    while (true)
    //    {
    //        string input = Console.ReadLine();

    //        if (Instance != null && !_requestedShutdown && !string.IsNullOrWhiteSpace(input))
    //            await Instance.ExecuteMetaCommandAsync(input, new ConsoleMessageChannel());
    //    }
    //}


    private static void RequestShutdown(bool restart = false, int restartDelay = -1)
    {
        _isRestart = restart;
        _requestedShutdown = true;
        _restartDelayInSeconds = restartDelay;
    }

    public static TimeSpan GetNextRestartTime()
    {
        TimeSpan result = ConfigurationHandler.Shared.DailyRestartTime - DateTime.Now.TimeOfDay;

        if (result.TotalSeconds < 1)
        {
            result = result.Add(TimeSpan.FromDays(1));
        }

        return result;
    }

    #region Start Parameters

    public const string CONSOLE_OUTPUT_ONLY_ID = "-console";
    public const string LOG_LEVEL_ID = "-loglevel";

    public static bool ConsoleOutputOnly;
    public static int LogLevel = 3;

    #endregion
}