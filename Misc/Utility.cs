using Discord;
using Discord.Commands;
using GeistDesWaldes.Configuration;
using GeistDesWaldes.Dictionaries;
using HtmlAgilityPack;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GeistDesWaldes.Misc
{
    public static class Utility
    {

        // ASCII 'a' (97) - 'z' (122)
        public static async Task<char> IndexToLetter(int index)
        {
            char result = '?';

            if (index < 97)
                index += 97;

            while (index > 122)
                index -= 26;

            result = (char)index;

            await Task.Delay(0);

            return result;
        }


        public static string CreateCostsString(float cooldownValue, int priceValue)
        {
            return $"{EmojiDictionary.HOURGLASS} {cooldownValue}s | {EmojiDictionary.MONEY_BAG} {priceValue}";
        }
        public static string ActionsToString(CommandMeta.CommandMetaInfo[] commandsToExecute)
        {
            StringBuilder result = new StringBuilder();

            if (commandsToExecute == null || commandsToExecute.Length < 1)
                result.Append(" - ");
            else
            {
                for (int i = 0; i < commandsToExecute?.Length; i++)
                {
                    result.Append($"{commandsToExecute[i].FullName}(");

                    if (commandsToExecute[i].RuntimeParameters.Length > 0)
                    {
                        for (int j = 0; j < commandsToExecute[i].RuntimeParameters.Length; j++)
                            result.Append($"{commandsToExecute[i].RuntimeParameters[j].Value}, ");

                        result.Remove(result.Length - 2, 2);
                    }
                    result.Append("); ");
                    result.AppendLine();
                }
                result.Remove(result.Length - 2, 2);
            }

            return result.ToString();
        }


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
                else if(_httpClient.Timeout.TotalSeconds != ConfigurationHandler.Shared.WebClientTimeoutInSeconds)
                {   
                    _httpClient.Timeout = TimeSpan.FromSeconds(ConfigurationHandler.Shared.WebClientTimeoutInSeconds);
                }

                return _httpClient;
            }
        }

        private static async ValueTask<Stream> IPv4ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            // By default, we create dual-mode sockets:
            // Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;

            try
            {
                await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
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

        public static async Task<HtmlDocument> DownloadWebDocument(string url)
        {
            await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(DownloadWebDocument), $"#QUERY: {url}"));

            HtmlWeb web = new();
            HtmlDocument doc = null;

            for (int i = 3; i > 0; i--)
            {
                doc = await web.LoadFromWebAsync(url);

                if (web.StatusCode == System.Net.HttpStatusCode.OK)
                    break;

                await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(DownloadWebDocument), $"{web.StatusCode}: Retrying... ({url})"));

                await Task.Delay(1000);
            }

            // doc.DisableServerSideCode = false

            return doc;
        }
    
    
        public static string GetFullCommandName(this Optional<CommandInfo> command)
        {
            return command.IsSpecified ? command.Value.Aliases?.Count > 0 ? command.Value.Aliases[0] : command.Value.Name : "null";
        }
    }
}
