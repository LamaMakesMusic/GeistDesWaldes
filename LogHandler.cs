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
            string logLine = $"{DateTime.Now,-19} [{message.Severity,8}] {message.Source,30}: {message.Message} {message.Exception}";

            lock (_logListLocker)
                _logList.AppendLine(logLine);

            if ((int)message.Severity > Launcher.LogLevel)
                return Task.CompletedTask;

            lock (_logConsoleLocker)
            {
                if (consoleColor is < 0 or > 15)
                {
                    Console.ForegroundColor = message.Severity switch
                    {
                        LogSeverity.Critical => ConsoleColor.Red,
                        LogSeverity.Error => ConsoleColor.DarkRed,
                        LogSeverity.Warning => ConsoleColor.Yellow,
                        LogSeverity.Info => ConsoleColor.White,
                        LogSeverity.Verbose => ConsoleColor.Gray,
                        LogSeverity.Debug => ConsoleColor.DarkGray,
                        _ => Console.ForegroundColor
                    };
                }
                else
                    Console.ForegroundColor = (ConsoleColor)consoleColor;

                Console.WriteLine($"[{(_server != null ? _server.Guild : "Main"),24}] {logLine}");
                Console.ResetColor();
            }

            return Task.CompletedTask;
        }

        public Task SaveToLogFile(string directory)
        {
            lock (_logListLocker)
            {
                if (_logList == null || _logList.Length == 0)
                    return Task.CompletedTask;
            }

            try
            {
                FileInfo file = new(Path.Combine(directory, $"{LOG_FILE_NAME}.log"));

                bool overwriteFile = (!file.Exists || (ConfigurationHandler.Shared.MaxLogFileSizeInMB < ((file.Length / 1024f) / 1024f)));

                using (FileStream stream = new(file.FullName, (overwriteFile ? FileMode.Create : FileMode.Append), FileAccess.Write, FileShare.Read))
                {
                    using (StreamWriter writer = new(stream, Encoding.Unicode))
                    {
                        lock (_logListLocker)
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
            
            return Task.CompletedTask;
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
