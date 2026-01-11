using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Audio;
using GeistDesWaldes.Calendar;
using GeistDesWaldes.Citations;
using GeistDesWaldes.CommandMeta;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Configuration;
using GeistDesWaldes.Counters;
using GeistDesWaldes.Currency;
using GeistDesWaldes.Decoration;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Events;
using GeistDesWaldes.Misc;
using GeistDesWaldes.Polls;
using GeistDesWaldes.Statistics;
using GeistDesWaldes.TwitchIntegration;
using GeistDesWaldes.TwitchIntegration.IntervalActions;
using GeistDesWaldes.UserCommands;
using GeistDesWaldes.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Extensions;

namespace GeistDesWaldes
{
    public class Server
    {
        public readonly ulong GuildId;
        public SocketGuild Guild => Launcher.Instance.DiscordClient.GetGuild(GuildId);

        public ServerConfiguration Config => ConfigurationHandler.Configs[GuildId];
        public RuntimeConfiguration RuntimeConfig => ConfigurationHandler.RuntimeConfig[GuildId];

        public CultureInfo CultureInfo => ConfigurationHandler.RuntimeConfig[GuildId].CultureInfo;

        public readonly DiscordSocketClient DiscordClient;
        public readonly CommandService CommandService;
        public readonly LogHandler LogHandler;

        public readonly AudioHandler AudioHandler;
        public readonly BirthdayHandler BirthdayHandler;
        public readonly CitationsHandler CitationsHandler;
        public readonly CommandCooldownHandler CommandCooldownHandler;
        public readonly CommandInfoHandler CommandInfoHandler;
        public readonly CommandStatisticsHandler CommandStatisticsHandler;
        public readonly CounterHandler CounterHandler;
        public readonly CurrencyHandler CurrencyHandler;
        public readonly CustomCommandHandler CustomCommandHandler;
        public readonly ForestUserHandler ForestUserHandler;
        public readonly FlickrHandler FlickrHandler;
        public readonly HolidayHandler HolidayHandler;
        public readonly LayoutTemplateHandler LayoutTemplateHandler;
        public readonly PollHandler PollHandler;
        public readonly ScheduleHandler ScheduleHandler;
        public readonly TwitchLivestreamIntervalActionHandler TwitchLivestreamIntervalActionHandler;
        public readonly UserCallbackHandler UserCallbackHandler;
        public readonly UserCooldownHandler UserCooldownHandler;
        public readonly WebCalSyncHandler WebCalSyncHandler;

        public readonly string ServerFilesDirectoryPath;
        
        public event EventHandler OnServerStart;
        public event EventHandler OnCheckIntegrity;
        public event EventHandler OnServerShutdown;

        private readonly IServiceProvider _services;
        public IServiceProvider Services => _services;

        public Server(ulong guildId, DiscordSocketClient client)
        {
            GuildId = guildId;
            DiscordClient = client;
            ServerFilesDirectoryPath = Path.GetFullPath(Path.Combine(ConfigurationHandler.ServerFilesDirectory, GuildId.ToString()));

            LogHandler = new LogHandler(this);
            Launcher.OnShutdown += OnShutdown;

            _services = new ServiceCollection().AddSingleton<ILogger<EventSubWebsocketClient>>(LogHandler).AddSingleton(this).AddSingleton(LogHandler).AddTwitchLibEventSubWebsockets().BuildServiceProvider();

            ConfigurationHandler.EnsureServerConfig(GuildId);

            // Command Service
            CommandService = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Verbose,
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Async
            });
            CommandService.CommandExecuted += OnCommandExecutedAsync;
            CommandService.Log += LogHandler.OnLog;

            // Type Readers
            CommandService.AddTypeReader<IUser>(new MixedUserReader<IUser>(), true);

            CommandService.AddTypeReader<IChannel>(new MixedChannelReader<IChannel>());
            CommandService.AddTypeReader<IChannel[]>(new MixedChannelArrayReader<IChannel>());

            CommandService.AddTypeReader<ITextChannel>(new MixedChannelReader<ITextChannel>());
            CommandService.AddTypeReader<ITextChannel[]>(new MixedChannelArrayReader<ITextChannel>());

            CommandService.AddTypeReader<IVoiceChannel>(new MixedChannelReader<IVoiceChannel>());
            CommandService.AddTypeReader<IVoiceChannel[]>(new MixedChannelArrayReader<IVoiceChannel>());

            CommandService.AddTypeReader<string[]>(new ArrayReader());
            CommandService.AddTypeReader<IndexValuePair>(new IndexValuePairReader());
            CommandService.AddTypeReader<IndexValuePair[]>(new IndexValuePairArrayReader());

            ForestUserHandler = new(this);
            
            CustomCommandHandler = new(this);
            CommandCooldownHandler = new(this);
            FlickrHandler = new(this);
            UserCooldownHandler = new(this);
            WebCalSyncHandler = new(this);
            CounterHandler = new(this);
            AudioHandler = new(this);
            CitationsHandler = new(this);
            CurrencyHandler = new(this);
            
            BirthdayHandler = new(this);
            HolidayHandler = new(this);
            LayoutTemplateHandler = new(this);
            PollHandler = new(this);
            ScheduleHandler = new(this);
            UserCallbackHandler = new(this);

            TwitchLivestreamIntervalActionHandler = new(this);
            
            CommandInfoHandler = new(this);

            CommandStatisticsHandler = new(this);
        }

        public async Task Start()
        {
            try
            {
                await GenericXmlSerializer.EnsurePathExistance(LogHandler, ServerFilesDirectoryPath, ConfigurationHandler.SERVER_CONFIG_FILE_NAME, Config);
                await ConfigurationHandler.LoadServerConfigFromFile(GuildId);

                await CommandService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

                OnServerStart?.Invoke(this, EventArgs.Empty);

                await Task.Delay(3000);

                await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(Start), $"==== {nameof(OnCheckIntegrity)} ===="));
                OnCheckIntegrity?.Invoke(this, EventArgs.Empty);

                await Task.Delay(4000);

                await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(Start), $"==== {nameof(TwitchIntegrationHandler.StartListening)} ===="));
                await TwitchIntegrationHandler.Instance.StartListening(this);
                await Task.Delay(3000);
                
                await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(Start), $"==== {nameof(OnClientSetupComplete)} ===="));
                await OnClientSetupComplete();

                await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(Start), "==== Server Start Completed ===="));
            }
            catch (Exception ex)
            {
                await LogHandler.Log(new LogMessage(LogSeverity.Critical, nameof(Start), "Failed!", ex));
            }
        }
        public void OnShutdown(object sender, EventArgs args)
        {
            TwitchIntegrationHandler.Instance.StopListening(this);

            OnServerShutdown?.Invoke(this, EventArgs.Empty);

            LogHandler.SaveToLogFile(ServerFilesDirectoryPath);
        }


        private async Task OnClientSetupComplete()
        {
            if ((await UserCallbackHandler.GetCallbackCommand(UserCallbackDictionary.DiscordCallbackTypes.OnClientReady)).ResultValue is { } callback)
                await callback.Execute(null, [DateTime.Now.ToString(CultureInfo)]);
        }


        public async Task HandleCommandAsync(IUserMessage message)
        {
            SocketUserMessage discordMessage = message as SocketUserMessage;
            TwitchUserMessage twitchMessage = message as TwitchUserMessage;

            // Bail out if it's a System Message.
            if (discordMessage == null && twitchMessage == null)
                return;

            ForestUser forestUser = await ForestUserHandler.GetOrCreateUser(message.Author);

            // Ignore users that currently are on cooldown
            if (await UserCooldownHandler.IsOnCooldown(message.Author))
            {
                await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(HandleCommandAsync), $"Blocked '{forestUser.Name}' -> on cooldown!"));
                return;
            }

            int prefixPosition = 0;

            // Is Command
            if (message.HasCharPrefix(Config.GeneralSettings.CommandPrefix, ref prefixPosition) || message.HasMentionPrefix(DiscordClient.CurrentUser, ref prefixPosition))
            {
                await UserCooldownHandler.AddToCooldown(message.Author);

                // Create a Command Context.
                CommandContext context = null;

                if (discordMessage != null)
                    context = new CommandContext(DiscordClient, discordMessage);
                else
                    context = new CommandContext(DiscordClient, twitchMessage);


                await ExecuteCommand(context, prefixPosition);
            }
            // Is Poll Vote
            else if (await PollHandler.GetChannelPollCount(message.Channel.Id) > 0 && message.HasCharPrefix(Config.GeneralSettings.PollVotePrefix, ref prefixPosition))
            {
                var voteResult = await PollHandler.TryVote(message, prefixPosition);

                await LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(HandleCommandAsync), $"Vote Result: {voteResult}"));

                await UserCooldownHandler.AddToCooldown(message.Author);
            }
        }
        public async Task ExecuteMetaCommandAsync(string command, IMessageChannel contextChannel, IUser originalUser = null, CommandBundleEntry bundleCallback = null)
        {
            IUser frontUser;

            if (contextChannel is TwitchMessageChannel)
                frontUser = TwitchIntegrationHandler.Instance.BotUser;
            //TODO: Console User?
            //else if (contextChannel is ConsoleMessageChannel)
            //    contextUser = 
            else
                frontUser = await contextChannel.GetUserAsync(DiscordClient.CurrentUser.Id);


            if (originalUser != null)
                frontUser = new MetaUser(frontUser, originalUser);


            MetaCommandMessage commandMessage = new MetaCommandMessage(command, contextChannel, frontUser, bundleCallback);

            await ExecuteCommand(new CommandContext(DiscordClient, commandMessage), 0);
        }

        private async Task ExecuteCommand(CommandContext context, int prefixPosition)
        {
            await LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(ExecuteCommand), $"Executing Command: {context.Message.Content.Substring(prefixPosition)}"));

            await CommandService.ExecuteAsync(context, prefixPosition, _services);
        }

        public async Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            ForestUser user = (await ForestUserHandler.GetUser(context.User))?.ResultValue;
            
            await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(OnCommandExecutedAsync), $"{user?.Name} ({user?.ForestUserId}) executed '{command.GetFullCommandName()}'."));

            await RecordCommandForStatistics(command, context, result);

            if (context?.Message is MetaCommandMessage metaMessage && metaMessage.BundleCallback != null)
            {
                metaMessage.BundleCallback?.SetCompleted();
            }

            if (result.IsSuccess)
            {
                for (int i = 0; i < command.Value?.Preconditions.Count; i++)
                {
                    if (command.Value == null)
                        continue;

                    if (command.Value.Preconditions[i] is CommandCooldown coco && coco != null)
                        await CommandCooldownHandler.AddToCooldown(command.Value, coco.CooldownInSeconds);
                    else if (command.Value.Preconditions[i] is CommandFee cofe && cofe != null)
                    {
                        var currencyResult = await CurrencyHandler.AddCurrencyToUser(context.User, -cofe.PriceTag);

                        if (!currencyResult.IsSuccess)
                            await LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(OnCommandExecutedAsync), $"Could not enforce {nameof(CommandFee)} on '{user?.Name}'! \n{currencyResult.Reason}"));
                    }
                }
            }
            else
            {
                bool allowOnTwitch = false;
                string resultError = result.Error.ToString();

                if (resultError.Equals("UnmetPrecondition"))
                {
                    if (result.ErrorReason.StartsWith("ERROR_COOLDOWN"))
                    {
                        allowOnTwitch = true;
                        resultError = result.ErrorReason.Substring("ERROR_COOLDOWN".Length);
                    }
                    else if (result.ErrorReason.StartsWith("ERROR_CATEGORY_LOCKED"))
                    {
                        allowOnTwitch = true;
                        resultError = result.ErrorReason.Substring("ERROR_CATEGORY_LOCKED".Length);
                    }
                    else if (result.ErrorReason.IndexOf("lacking funds", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        allowOnTwitch = true;
                        resultError = CurrencyHandler.CustomizationData.GetToStringMessage(CurrencyCustomization.ToStringType.NotEnough);
                    }
                    else
                        resultError = ReplyDictionary.ERROR_UNMET_PRECONDITION;
                }
                else if (resultError.Equals("UnknownCommand"))
                    resultError = ReplyDictionary.ERROR_UNKNOWN_COMMAND;
                else if (resultError.StartsWith(nameof(Users.ForestUserHandler)))
                {
                    resultError = resultError.Remove(0, nameof(Users.ForestUserHandler).Length + 1);
                    allowOnTwitch = true;
                }
                else if (resultError.Equals("A quoted parameter is incomplete."))
                    resultError = ReplyDictionary.ERROR_INCOMPLETE_QUOTED_PARAMETER;
                else if (resultError.Equals("The input text has too many parameters."))
                    resultError = ReplyDictionary.ERROR_TOO_MANY_PARAMETERS;
                else if (resultError.StartsWith(ReplyDictionary.COMMAND_ONLY_VALID_ON_DISCORD))
                    allowOnTwitch = true;
                else if (resultError.StartsWith(ReplyDictionary.COMMAND_ONLY_VALID_ON_TWITCH))
                    allowOnTwitch = true;
                else if (resultError.Equals("Unsuccessful"))
                {
                    if (result.ErrorReason.Equals(ReplyDictionary.COMMAND_ONLY_VALID_IN_PRIVATE_CHANNEL))
                    {
                        allowOnTwitch = true;
                        resultError = result.ErrorReason;
                    }
                    else
                        resultError = $"{ReplyDictionary.ERROR_UNSUCCESSFUL}\n{result.ErrorReason.Substring(0, Math.Min(result.ErrorReason.Length, 160))}";
                }
                else if (result.ErrorReason.Length < 160)
                    resultError = result.ErrorReason;

                if (allowOnTwitch || !(context.Channel is TwitchMessageChannel))
                {
                    ChannelMessage msg = new ChannelMessage(context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Error)
                        .AddContent(new ChannelMessageContent()
                            .SetDescription(resultError)
                        );

                    await msg.SendAsync();
                }

                await LogHandler.Log(new LogMessage(LogSeverity.Error, result.Error.ToString(), result.ErrorReason));
            }
        }

        private async Task RecordCommandForStatistics(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (!command.IsSpecified || context == null || context.User.IsBot || context.Message is MetaCommandMessage)
                return;
            
            await CommandStatisticsHandler.RecordCommand(command.GetFullCommandName());
        }
    }
}
