using Discord;
using Discord.WebSocket;
using GeistDesWaldes.Configuration;
using GeistDesWaldes.TwitchIntegration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeistDesWaldes.Misc;
using TwitchLib.Client.Models;

namespace GeistDesWaldes
{
    public class Program
    {
        public readonly DiscordSocketClient DiscordClient;

        public readonly TwitchIntegrationHandler TwitchIntegrationHandler = new();
        public readonly Dictionary<ulong, Server> Servers = new();

        public readonly LogHandler LogHandler = new (null);
        
        private static readonly string[] _banner =
        [
            @"                                                                                                                                                        ",
            @"               ('-.             .-')    .-') _          _ .-') _     ('-.    .-')           (`\ .-') /`  ('-.              _ .-') _     ('-.    .-')    ",
            @"             _(  OO)           ( OO ). (  OO) )        ( (  OO) )  _(  OO)  ( OO ).          `.( OO ),' ( OO ).-.         ( (  OO) )  _(  OO)  ( OO ).  ",
            @"  ,----.    (,------.  ,-.-') (_)---\_)/     '._        \     .'_ (,------.(_)---\_)      ,--./  .--.   / . --. / ,--.     \     .'_ (,------.(_)---\_) ",
            @" '  .-./-')  |  .---'  |  |OO)/    _ | |'--...__)       ,`'--..._) |  .---'/    _ |       |      |  |   | \-.  \  |  |.-') ,`'--..._) |  .---'/    _ |  ",
            @" |  |_( O- ) |  |      |  |  \\  :` `. '--.  .--'       |  |  \  ' |  |    \  :` `.       |  |   |  |,.-'-'  |  | |  | OO )|  |  \  ' |  |    \  :` `.  ",
            @" |  | .--, \(|  '--.   |  |(_/ '..`''.)   |  |          |  |   ' |(|  '--.  '..`''.)      |  |.'.|  |_)\| |_.'  | |  |`-' ||  |   ' |(|  '--.  '..`''.) ",
            @"(|  | '. (_/ |  .--'  ,|  |_.'.-._)   \   |  |          |  |   / : |  .--' .-._)   \      |         |   |  .-.  |(|  '---.'|  |   / : |  .--' .-._)   \ ",
            @" |  '--'  |  |  `---.(_|  |   \       /   |  |          |  '--'  / |  `---.\       /      |   ,'.   |   |  | |  | |      | |  '--'  / |  `---.\       / ",
            @"  `------'   `------'  `--'    `-----'    `--'          `-------'  `------' `-----'       '--'   '--'   `--' `--' `------' `-------'  `------' `-----'  ",
            @"                                                                                                                                                        "
        ];
        

        public Program()
        {
            Launcher.OnShutdown += OnShutdown;

            #region Discord Client
            DiscordClient = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
                MessageCacheSize = 100,
                AlwaysDownloadUsers = true,
                GatewayIntents = GatewayIntents.All & ~(GatewayIntents.GuildPresences | GatewayIntents.GuildScheduledEvents | GatewayIntents.GuildInvites),
            });
            DiscordClient.Log += LogHandler.OnLog;
            DiscordClient.MessageReceived += HandleCommandAsync;
            DiscordClient.Ready += OnClientReady;
            DiscordClient.UserJoined += OnBotJoinedGuild;
            #endregion
        }


        public async Task StartUp()
        {
            LogBanner();
            LogBootInfo();

            await ConfigurationHandler.LoadSharedConfigFromFile();

            _ = Task.Run(async () =>
            {
                // Login
                await DiscordClient.LoginAsync(TokenType.Bot, ConfigurationHandler.Shared.Secrets.DiscordBotLoginToken);

                // Start Client
                await DiscordClient.StartAsync();
            });

            _ = Task.Run(TwitchIntegrationHandler.InitializeTwitchIntegration);

            // Loops
            _logTask = Task.Run(SaveLogFileLoop);
            _serverWatchdogTask = Task.Run(ServerWatchdogLoop);
        }
        
        
        private void LogBanner()
        {
            foreach (string line in _banner)
            {
                LogHandler.LogRaw(line);
            }
        }
        
        private void LogBootInfo()
        {
            LogHandler.LogRaw(Launcher.ExecutingAssemblyName);
            LogHandler.LogRaw($"{nameof(Launcher.BaseDirectory)}: {Launcher.BaseDirectory}");
            LogHandler.LogRaw("");
            LogHandler.LogRaw($"Starting Parameters: Log Level ('{Launcher.LOG_LEVEL_ID}') - {Launcher.LogLevel} | Console Only ('{Launcher.CONSOLE_OUTPUT_ONLY_ID}') - {Launcher.ConsoleOutputOnly}");
            LogHandler.LogRaw("");
            LogHandler.LogRaw("Dependencies");
            LogHandler.LogRaw($"{nameof(Launcher.FfmpegPath)}: {Launcher.FfmpegPath}");
            
            CheckDependency("opus.so");
            CheckDependency("libopus.so");
            CheckDependency("opus");
            CheckDependency("libopus");
        }

        private void CheckDependency(string filename)
        {
            bool result = File.Exists($"{Launcher.BaseDirectory}{filename}");
            
            LogHandler.LogRaw($"{filename}: {result}");
        }


        private void OnShutdown(object src, EventArgs e)
        {
            OnShutdownAsync().SafeAsync<Program>(LogHandler);
        }
        private async Task OnShutdownAsync()
        {
            await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(OnShutdownAsync), "Stopping Loops..."));
            _cancelLogLoopSource?.Cancel();
            _cancelServerWatchdogSource?.Cancel();

            await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(OnShutdownAsync), "Stopping Discord Client..."));
            if (DiscordClient != null)
            {
                await DiscordClient.StopAsync();
                await DiscordClient.LogoutAsync();
            }
            
            await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(OnShutdownAsync), "Saving Config..."));
            await ConfigurationHandler.SaveAllConfigsToFile();

            await LogHandler.SaveToLogFile(Launcher.CommonFilesPath);
        }

        private async Task OnClientReady()
        {
            await Task.Delay(10);
        }
        private async Task OnBotJoinedGuild(SocketGuildUser socketGuildUser)
        {
            await SetupGuildServer(socketGuildUser.Guild);

            // TODO: Move to appropriate callback handler
            //await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(OnUserJoinedGuild), $"A new user joined '{socketGuildUser.Guild}'"));

            //if ((await UserCallbackHandler.GetCallbackCommand(UserCallbackDictionary.DiscordCallbackTypes.OnUserJoinedGuild)).ResultValue is CustomCommand callback && callback != null)
            //    await callback.Execute(null, new string[] { socketGuildUser.Mention, socketGuildUser.Nickname });
        }
        private async Task SetupGuildServer(SocketGuild guild)
        {
            if (Servers.ContainsKey(guild.Id))
                return;

            Server server = new(guild.Id, DiscordClient);
            Servers.Add(guild.Id, server);

            server.Start().SafeAsync<Program>(LogHandler);
            
            await Task.Delay(10);
        }
        
        public async Task RestartServer(Server server)
        {
            if (server == null)
                return;

            await LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(RestartServer), $"Restarting Server {server.GuildId}!"));

            server.OnShutdown(this, EventArgs.Empty);

            Server nServer = new(server.GuildId, DiscordClient);
            Servers[nServer.GuildId] = nServer;

            nServer.Start().SafeAsync<Program>(LogHandler);
        }


        public async Task HandleCommandAsync(IMessage arg)
        {
            if (arg == null || arg.Author.IsBot || arg is not IUserMessage msg)
                return;

            if (msg.Content == null || msg.Content.Trim().Length < 2)
                return;

            IGuild guild = null;
            
            if (msg.Channel is IGuildChannel gChannel)
                guild = gChannel.Guild;

            if (guild == null)
            {
                if (msg.Author is IGuildUser gUser)
                    guild = gUser.Guild;

                if (guild == null)
                {
                    if (msg.Channel is TwitchMessageChannel tMsg)
                        guild = DiscordClient.GetGuild(tMsg.GuildId);

                    if (guild == null)
                    {
                        await LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(HandleCommandAsync), $"Could not get Guild from channel '{msg.Channel?.Name}'!"));
                        return;
                    }
                }
            }

            Servers[guild.Id].HandleCommandAsync(msg).SafeAsync<Program>(LogHandler);
        }

        public SocketGuildUser GetBotUserDiscord(Server server)
        {
            if (server == null)
                return null;

            return DiscordClient.Guilds.FirstOrDefault(g => g.Id == server.GuildId)?.CurrentUser;
        }
        public TwitchUser GetBotUserTwitch(Server server)
        {
            if (server == null)
                return null;

            return TwitchIntegrationHandler.BotUser;
        }

        public async Task<T> GetChannel<T>(ulong channelId) where T : class, IChannel
        {
            try
            {
                // Check if discord channel
                IChannel result = DiscordClient.GetChannel(channelId);

                // Check if twitch channel
                if (result == null)
                    result = GetTwitchChannel(channelId);

                return (T)result;
            }
            catch (Exception e)
            {
                await LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(GetChannel), "", e));
                return default;
            }
        }
        public async Task<T> GetChannel<T>(string channelName, ulong guildId) where T : class, IChannel
        {
            try
            {
                channelName = channelName.Trim();

                if (channelName.Equals("twitch", StringComparison.OrdinalIgnoreCase))
                    return await GetChannel<T>(ConfigurationHandler.Configs[guildId].TwitchSettings.TwitchMessageChannelId);


                SocketGuild guild = DiscordClient.Guilds.FirstOrDefault(g => g.Id == guildId);
                if (guild == default)
                    throw new Exception($"Guild Not Found! Could not find guild with id '{guildId}' in joined guilds.");

                T result = null;

                if (TryGetChannelContaining(GetChannelsByType(guild, ChannelType.Text), channelName, out result))
                    return result;

                if (TryGetChannelContaining(GetChannelsByType(guild, ChannelType.Voice), channelName, out result))
                    return result;

                if (TryGetChannelContaining(GetChannelsByType(guild, ChannelType.Forum), channelName, out result))
                    return result;

                if (TryGetChannelContaining(GetChannelsByType(guild, ChannelType.PublicThread), channelName, out result))
                    return result;

                if (TryGetChannelContaining(guild.Channels, channelName, out result))
                    return result;

                return null;
            }
            catch (Exception e)
            {
                await LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(GetChannel), "", e));
                return default;
            }
        }
        
        private static IEnumerable<SocketGuildChannel> GetChannelsByType(SocketGuild guild, ChannelType type)
        {
            return guild.Channels.Where(ch => ch != null && ch.ChannelType == type);
        }

        private static bool TryGetChannelContaining<T>(IEnumerable<SocketGuildChannel> channelList, string s, out T result) where T : class, IChannel
        {
            List<T> results = [];

            foreach (SocketGuildChannel channel in channelList)
            {
                if (channel is not T tChannel)
                    continue;

                if (channel.Name.Equals(s, StringComparison.OrdinalIgnoreCase))
                {
                    result = tChannel;
                    return true;
                }
                
                if (channel.Name.Contains(s, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(tChannel);
                }
            }

            if (results.Count == 0) 
            {
                result = null;
                return false;
            }

            result = results[0];
            return true;
        }

        public static TwitchMessageChannel GetTwitchChannel(ulong channelId) 
        {
            var configPair = ConfigurationHandler.Configs.FirstOrDefault(c => c.Value.TwitchSettings.TwitchMessageChannelId == channelId);

            if (configPair.Value == default)
                return null;

            string channelName = configPair.Value.TwitchSettings.TwitchChannelName;
            JoinedChannel channel = TwitchIntegrationHandler.Instance.GetChannelObject(channelName);

            if (channel != null)
                return new TwitchMessageChannel(configPair.Key, configPair.Value.TwitchSettings.TwitchMessageChannelId, channel.Channel);

            return null;
        }


        private Task _logTask;
        private CancellationTokenSource _cancelLogLoopSource;
        private async Task SaveLogFileLoop()
        {
            _cancelLogLoopSource = new CancellationTokenSource();

            Console.OutputEncoding = Encoding.Unicode;

            try
            {
                await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(SaveLogFileLoop), "Started."));

                while (_logTask != null)
                {
                    await LogHandler.SaveToLogFile(Launcher.CommonFilesPath);

                    foreach (var server in Servers)
                        await server.Value?.LogHandler?.SaveToLogFile(server.Value.ServerFilesDirectoryPath);

                    await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, ConfigurationHandler.Shared.LogFileSaveIntervalInMinutes)), _cancelLogLoopSource.Token);
                }
            }
            catch (TaskCanceledException)
            {

            }
            finally
            {
                _logTask = null;
                _cancelLogLoopSource = null;

                await LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(SaveLogFileLoop), "Stopped."));
            }
        }


        private Task _serverWatchdogTask;
        private CancellationTokenSource _cancelServerWatchdogSource;
        private async Task ServerWatchdogLoop()
        {
            _cancelServerWatchdogSource = new CancellationTokenSource();

            try
            {
                await Task.Delay(10000);

                await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(ServerWatchdogLoop), "Started."));

                while (_serverWatchdogTask != null)
                {
                    if (DiscordClient.ConnectionState != ConnectionState.Connected || TwitchIntegrationHandler.API == null)
                    {
                        await Task.Delay(10000, _cancelServerWatchdogSource.Token);
                        continue;
                    }

                    foreach (SocketGuild guild in Launcher.Instance.DiscordClient.Guilds)
                        await SetupGuildServer(guild);

                    await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, ConfigurationHandler.Shared.ServerWatchdogIntervalInMinutes)), _cancelServerWatchdogSource.Token);
                }
            }
            catch (TaskCanceledException)
            {

            }
            finally
            {
                _serverWatchdogTask = null;
                _cancelServerWatchdogSource = null;
                
                await LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(ServerWatchdogLoop), "Stopped."));
            }
        }
    }
}
