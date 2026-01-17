using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Audio;
using GeistDesWaldes.Calendar;
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
        
        public IServiceProvider Services { get; }
        private readonly List<IServerModule> _modules = [];

        private readonly Dictionary<Type, IServerModule> _moduleQuickAccess = new();

        
        public readonly DiscordSocketClient DiscordClient;
        public readonly CommandService CommandService;
        public readonly LogHandler LogHandler;

        public readonly string ServerFilesDirectoryPath;
        

        public Server(ulong guildId, DiscordSocketClient client)
        {
            GuildId = guildId;
            DiscordClient = client;
            ServerFilesDirectoryPath = Path.GetFullPath(Path.Combine(ConfigurationHandler.ServerFilesDirectory, GuildId.ToString()));

            LogHandler = new LogHandler(this);

            Services = new ServiceCollection()
                .AddSingleton(this)
                .AddSingleton(LogHandler)
                .AddSingleton<ILogger<EventSubWebsocketClient>>(LogHandler)
                .AddTwitchLibEventSubWebsockets()
                .AddSingleton<ForestUserHandler>()
                .AddSingleton<CustomCommandHandler>()
                .AddSingleton<CommandCooldownHandler>()
                .AddSingleton<FlickrHandler>()
                .AddSingleton<UserCooldownHandler>()
                .AddSingleton<WebCalSyncHandler>()
                .AddSingleton<CounterHandler>()
                .AddSingleton<AudioHandler>()
                .AddSingleton<CounterHandler>()
                .AddSingleton<CurrencyHandler>()
                .AddSingleton<BirthdayHandler>()
                .AddSingleton<HolidayHandler>()
                .AddSingleton<LayoutTemplateHandler>()
                .AddSingleton<PollHandler>()
                .AddSingleton<ScheduleHandler>()
                .AddSingleton<UserCallbackHandler>()
                .AddSingleton<TwitchLivestreamIntervalActionHandler>()
                .AddSingleton<CommandInfoHandler>()
                .AddSingleton<CommandStatisticsHandler>()
            .BuildServiceProvider();
            
            _modules.AddRange(Services.GetServices<IServerModule>());
            _modules.Sort((m1, m2) => m1.Priority.CompareTo(m2));

            foreach (IServerModule m in _modules)
            {
                _moduleQuickAccess.Add(m.GetType(), m);
            }
            
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
        }

        
        public async Task Start()
        {
            try
            {
                await GenericXmlSerializer.EnsurePathExistance(LogHandler, ServerFilesDirectoryPath, ConfigurationHandler.SERVER_CONFIG_FILE_NAME, Config);
                await ConfigurationHandler.LoadServerConfigFromFile(GuildId);

                await CommandService.AddModulesAsync(Assembly.GetEntryAssembly(), Services);

                await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(Start), $"==== {nameof(IServerModule.OnServerStartUp)} ===="));
                foreach (IServerModule module in _modules)
                {
                    await module.OnServerStartUp();
                }

                await Task.Delay(3000);

                await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(Start), $"==== {nameof(IServerModule.OnCheckIntegrity)} ===="));
                foreach (IServerModule module in _modules)
                {
                    await module.OnCheckIntegrity();
                }

                await Task.Delay(3000);

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
        
        public async Task Stop()
        {
            await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(Stop), $"==== {nameof(TwitchIntegrationHandler.StopListening)} ===="));
            TwitchIntegrationHandler.Instance.StopListening(this);
            
            await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(Stop), $"==== {nameof(IServerModule.OnServerShutdown)} ===="));
            
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                try
                {
                    await _modules[i].OnServerShutdown();
                }
                catch (Exception e)
                {
                    await LogHandler.Log(new LogMessage(LogSeverity.Critical, nameof(Stop), "Exception in Module Shutdown!", e));
                }
            }

            await LogHandler.SaveToLogFile(ServerFilesDirectoryPath);
        }


        private async Task OnClientSetupComplete()
        {
            if ((await GetModule<UserCallbackHandler>().GetCallbackCommand(UserCallbackDictionary.DiscordCallbackTypes.OnClientReady)).ResultValue is { } callback)
                await callback.Execute(null, [DateTime.Now.ToString(CultureInfo)]);
        }


        public T GetModule<T>() where T : IServerModule
        {
            Type t = typeof(T);
            
            if (_moduleQuickAccess.TryGetValue(t, out IServerModule value))
                return (T)value;

            LogHandler.Log(new LogMessage(LogSeverity.Critical, nameof(GetModule), $"Could not find module of type '{t.Name}'!"));
            return default(T);
        }
        

        public async Task HandleCommandAsync(IUserMessage message)
        {
            SocketUserMessage discordMessage = message as SocketUserMessage;
            TwitchUserMessage twitchMessage = message as TwitchUserMessage;

            // Bail out if it's a System Message.
            if (discordMessage == null && twitchMessage == null)
                return;

            ForestUser forestUser = await GetModule<ForestUserHandler>().GetOrCreateUser(message.Author);

            // Ignore users that currently are on cooldown
            if (await GetModule<UserCooldownHandler>().IsOnCooldown(message.Author))
            {
                await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(HandleCommandAsync), $"Blocked '{forestUser.Name}' -> on cooldown!"));
                return;
            }

            int prefixPosition = 0;

            // Is Command
            if (message.HasCharPrefix(Config.GeneralSettings.CommandPrefix, ref prefixPosition) || message.HasMentionPrefix(DiscordClient.CurrentUser, ref prefixPosition))
            {
                await GetModule<UserCooldownHandler>().AddToCooldown(message.Author);

                // Create a Command Context.
                CommandContext context;

                if (discordMessage != null)
                    context = new CommandContext(DiscordClient, discordMessage);
                else
                    context = new CommandContext(DiscordClient, twitchMessage);


                await ExecuteCommand(context, prefixPosition);
            }
            // Is Poll Vote
            else if (await GetModule<PollHandler>().GetChannelPollCount(message.Channel.Id) > 0 && message.HasCharPrefix(Config.GeneralSettings.PollVotePrefix, ref prefixPosition))
            {
                PollHandler.VoteEvaluationResult voteResult = await GetModule<PollHandler>().TryVote(message, prefixPosition);

                await LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(HandleCommandAsync), $"Vote Result: {voteResult}"));

                await GetModule<UserCooldownHandler>().AddToCooldown(message.Author);
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

            await CommandService.ExecuteAsync(context, prefixPosition, Services);
        }

        public async Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            ForestUser user = (await GetModule<ForestUserHandler>().GetUser(context.User))?.ResultValue;
            
            await LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(OnCommandExecutedAsync), $"{user?.Name} ({user?.ForestUserId}) executed '{command.GetFullCommandName()}'."));

            await RecordCommandForStatistics(command, context);

            if (context.Message is MetaCommandMessage { BundleCallback: not null } metaMessage)
            {
                metaMessage.BundleCallback?.SetCompleted();
            }

            if (result.IsSuccess)
            {
                for (int i = 0; i < command.Value?.Preconditions.Count; i++)
                {
                    if (command.Value == null)
                        continue;

                    if (command.Value.Preconditions[i] is CommandCooldown coco)
                    {
                        await GetModule<CommandCooldownHandler>().AddToCooldown(command.Value, coco.CooldownInSeconds);
                    }
                    else if (command.Value.Preconditions[i] is CommandFee cofe)
                    {
                        CustomRuntimeResult currencyResult = await GetModule<CurrencyHandler>().AddCurrencyToUser(context.User, -cofe.PriceTag);

                        if (!currencyResult.IsSuccess)
                            await LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(OnCommandExecutedAsync), $"Could not enforce {nameof(CommandFee)} on '{user?.Name}'! \n{currencyResult.Reason}"));
                    }
                }
            }
            else
            {
                bool allowOnTwitch = false;
                string resultError = result.Error.ToString() ?? string.Empty;

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
                        resultError = GetModule<CurrencyHandler>().CustomizationData.GetToStringMessage(CurrencyCustomization.ToStringType.NotEnough);
                    }
                    else
                        resultError = ReplyDictionary.ERROR_UNMET_PRECONDITION;
                }
                else if (resultError.Equals("UnknownCommand"))
                    resultError = ReplyDictionary.ERROR_UNKNOWN_COMMAND;
                else if (resultError.StartsWith(nameof(ForestUserHandler)))
                {
                    resultError = resultError.Remove(0, nameof(ForestUserHandler).Length + 1);
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

        private async Task RecordCommandForStatistics(Optional<CommandInfo> command, ICommandContext context)
        {
            if (!command.IsSpecified || context == null || context.User.IsBot || context.Message is MetaCommandMessage)
                return;
            
            await GetModule<CommandStatisticsHandler>().RecordCommand(command.GetFullCommandName());
        }
    }
}