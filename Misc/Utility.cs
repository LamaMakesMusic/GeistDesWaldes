using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.CommandMeta;
using GeistDesWaldes.Configuration;
using GeistDesWaldes.Dictionaries;

namespace GeistDesWaldes.Misc;

public static class Utility
{
    private static HttpClient _httpClient;

    public static HttpClient HttpClient
    {
        get
        {
            if (_httpClient == null)
            {
                SocketsHttpHandler handler = new()
                {
                    // ConnectCallback = IPv4ConnectAsync,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(60) // Recreate every 15 minutes
                };

                _httpClient = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(ConfigurationHandler.Shared.WebClientTimeoutInSeconds)
                };

                _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("Geist-des-Waldes/1.0");
            }
            else if (_httpClient.Timeout.TotalSeconds != ConfigurationHandler.Shared.WebClientTimeoutInSeconds)
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(ConfigurationHandler.Shared.WebClientTimeoutInSeconds);
            }

            return _httpClient;
        }
    }

    // ASCII 'a' (97) - 'z' (122)
    public static async Task<char> IndexToLetter(int index)
    {
        char result = '?';

        if (index < 97)
        {
            index += 97;
        }

        while (index > 122)
        {
            index -= 26;
        }

        result = (char)index;

        await Task.Delay(0);

        return result;
    }


    public static string CreateCostsString(float cooldownValue, int priceValue)
    {
        return $"{EmojiDictionary.HOURGLASS} {cooldownValue}s | {EmojiDictionary.MONEY_BAG} {priceValue}";
    }

    public static string ActionsToString(CommandMetaInfo[] commandsToExecute)
    {
        StringBuilder result = new();

        if (commandsToExecute == null || commandsToExecute.Length < 1)
        {
            result.Append(" - ");
        }
        else
        {
            for (int i = 0; i < commandsToExecute?.Length; i++)
            {
                result.Append($"{commandsToExecute[i].FullName}(");

                if (commandsToExecute[i].RuntimeParameters.Length > 0)
                {
                    for (int j = 0; j < commandsToExecute[i].RuntimeParameters.Length; j++)
                    {
                        result.Append($"{commandsToExecute[i].RuntimeParameters[j].Value}, ");
                    }

                    result.Remove(result.Length - 2, 2);
                }

                result.Append("); ");
                result.AppendLine();
            }

            result.Remove(result.Length - 2, 2);
        }

        return result.ToString();
    }

    public static async Task<string> DownloadWebString(string url)
    {
        try
        {
            await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(DownloadWebString), $"#QUERY: {url}"));
            return await HttpClient.GetStringAsync(url);
        }
        catch (Exception e)
        {
            await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(DownloadWebString), "", e));
            return null;
        }
    }

    public static string GetFullCommandName(this Optional<CommandInfo> command)
    {
        return command.IsSpecified ? command.Value.Aliases?.Count > 0 ? command.Value.Aliases[0] : command.Value.Name : "null";
    }


    extension(Task task)
    {
        public void SafeAsync<TContext>(ulong guildId, Action continueWith = null)
        {
            LogHandler logger = null;

            if (Launcher.Instance?.Servers?.TryGetValue(guildId, out Server server) ?? false)
            {
                logger = server.LogHandler;
            }

            task.SafeAsync<TContext>(logger, continueWith);
        }

        public async void SafeAsync<TContext>(LogHandler logger, Action continueWith = null)
        {
            try
            {
                await task;
                continueWith?.Invoke();
            }
            catch (Exception ex)
            {
                if (logger == null)
                {
                    logger = Launcher.Instance.LogHandler;
                    await logger.Log(new LogMessage(LogSeverity.Warning, nameof(SafeAsync), "Logger is missing! Falling back to general log!"));
                }

                await logger.Log(new LogMessage(LogSeverity.Error, typeof(TContext).Name, string.Empty, ex));
            }
        }
    }
}