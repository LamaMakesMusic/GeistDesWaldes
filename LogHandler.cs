using Discord;
using GeistDesWaldes.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.EventSub.Websockets;

namespace GeistDesWaldes
{
    public class LogHandler : ILogger<EventSubWebsocketClient>
    {
        public const string LOG_FILE_NAME = "Log";

        private readonly Server _server;

        private readonly StringBuilder _logList = new ();
        private readonly object _logListLocker = new ();
        
        private static readonly object _logConsoleLocker = new ();


        public LogHandler(Server server)
        {
            _server = server;
        }

        public Task OnLog(LogMessage message) => Log(message, -1);
        public Task Log(LogMessage message, int consoleColor = -1)
        {
            return Task.Run(() =>
            {
                string logLine = $"{DateTime.Now,-19} [{message.Severity,8}] {message.Source,30}: {message.Message} {message.Exception}";


                lock (_logListLocker)
                    _logList.AppendLine(logLine);


                if ((int)message.Severity > Launcher.LogLevel)
                    return;


                lock (_logConsoleLocker)
                {
                    if (consoleColor < 0 || consoleColor > 15)
                    {
                        switch (message.Severity)
                        {
                            case LogSeverity.Critical:
                                Console.ForegroundColor = ConsoleColor.Red;
                                break;
                            case LogSeverity.Error:
                                Console.ForegroundColor = ConsoleColor.DarkRed;
                                break;
                            case LogSeverity.Warning:
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                break;
                            case LogSeverity.Info:
                                Console.ForegroundColor = ConsoleColor.White;
                                break;
                            case LogSeverity.Verbose:
                                Console.ForegroundColor = ConsoleColor.Gray;
                                break;
                            case LogSeverity.Debug:
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                break;
                        }
                    }
                    else
                        Console.ForegroundColor = (ConsoleColor)consoleColor;

                    Console.WriteLine($"[{(_server != null ? _server.Guild : "Main"),24}] {logLine}");
                    Console.ResetColor();
                }

            });
        }

        public Task SaveToLogFile(string directory)
        {
            return Task.Run(() =>
            {
                if (_logList == null || _logList.Length == 0)
                    return;

                try
                {
                    FileInfo file = new FileInfo(Path.Combine(directory, $"{LOG_FILE_NAME}.log"));

                    bool overwriteFile = (!file.Exists || (ConfigurationHandler.Shared.MaxLogFileSizeInMB < ((file.Length / 1024f) / 1024f)));

                    using (var stream = new FileStream(file.FullName, (overwriteFile ? FileMode.Create : FileMode.Append), FileAccess.Write, FileShare.Read))
                    {
                        using (StreamWriter writer = new StreamWriter(stream, Encoding.Unicode))
                        {
                            writer.Write(_logList.ToString());
                        };
                    }

                    Log(new LogMessage(LogSeverity.Verbose, nameof(SaveToLogFile), "Saved Log File!"));
                }
                catch (Exception e)
                {
                    Log(new LogMessage(LogSeverity.Error, nameof(SaveToLogFile), $"Could not Append to Log File in '{directory}'!", exception: e));
                }

                lock (_logListLocker)
                    _logList.Clear();
            });
        }


        public void LogRaw(string line)
        {
            Console.WriteLine(line);
            
            lock(_logListLocker)
                _logList.AppendLine(line);
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Log(new LogMessage(ToSeverity(logLevel), "ILogger", formatter?.Invoke(state, exception) ?? "FORMATTER == NULL"));
        }

        private static LogSeverity ToSeverity(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.None:
                case LogLevel.Debug:
                    return LogSeverity.Debug;
                
                case LogLevel.Trace:
                    return LogSeverity.Verbose;

                case LogLevel.Information:
                    return LogSeverity.Info;

                case LogLevel.Warning:
                    return LogSeverity.Warning;

                case LogLevel.Error:
                    return LogSeverity.Error;

                case LogLevel.Critical:
                    return LogSeverity.Critical;
            }

            return LogSeverity.Verbose;
        }
    }
}
