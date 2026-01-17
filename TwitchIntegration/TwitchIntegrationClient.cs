using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Configuration;
using GeistDesWaldes.Currency;
using GeistDesWaldes.Misc;
using GeistDesWaldes.TwitchIntegration.IntervalActions;
using GeistDesWaldes.UserCommands;
using GeistDesWaldes.Users;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Games;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Core.EventArgs.Stream;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Core.SubscriptionTypes.Stream;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using Game = TwitchLib.Api.Helix.Models.Games.Game;

namespace GeistDesWaldes.TwitchIntegration;

public class TwitchIntegrationClient
{
    // see: https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types/ 
    private static readonly Dictionary<string, string> _subscriptionTypes = new()
    {
        { "channel.chat.message", "1" },
        { "channel.follow", "2" },
        { "channel.raid", "1" },
        // { "channel.subscribe", "1" }, -> requires channel:read:subscription scope
        // { "channel.subscription.gift", "1" }, -> requires channel:read:subscription scope
        { "channel.update", "2" },
        { "stream.online", "1" },
        { "stream.offline", "1" }
    };

    private readonly Dictionary<ServerConfiguration, ConfigStreamEntity> _configStreamEntities = new();
    private readonly ConnectionStatus _connectionStatus = new();

    private readonly EventSubWebsocketClient _eventSubWebsocketClient;

    public readonly StreamObject StreamInfo = new();
    private CancellationTokenSource _cancelConnectionVerificationSource;

    private Task _connectionVerificationTask;
    private Task _liveStreamUpdateLoopTask;


    public TwitchIntegrationClient(EventSubWebsocketClient eventSubWebsocketClient)
    {
        _eventSubWebsocketClient = eventSubWebsocketClient ?? throw new ArgumentNullException(nameof(eventSubWebsocketClient));

        _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
        _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
        _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;

        _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;

        _eventSubWebsocketClient.ChannelChatMessage += EventSub_OnChannelChatMessage;

        _eventSubWebsocketClient.ChannelBan += EventSub_OnChannelBan;
        _eventSubWebsocketClient.ChannelUnban += EventSub_OnChannelUnban;

        _eventSubWebsocketClient.ChannelFollow += EventSub_OnChannelFollow;
        _eventSubWebsocketClient.ChannelRaid += EventSub_OnChannelRaid;
        _eventSubWebsocketClient.ChannelSubscribe += EventSub_OnChannelSubscribe;
        _eventSubWebsocketClient.ChannelUpdate += EventSub_OnChannelUpdate;

        _eventSubWebsocketClient.StreamOnline += EventSub_OnStreamOnline;
        _eventSubWebsocketClient.StreamOffline += EventSub_OnStreamOffline;
    }

    public TwitchClient Client { get; private set; }
    public string ChannelName { get; private set; }
    public string ChannelId { get; private set; }
    public int ServerCount => _configStreamEntities.Count;


    public async Task Start(string channelName, string channelId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                throw new ArgumentNullException(nameof(channelName));
            }

            if (string.IsNullOrWhiteSpace(channelId))
            {
                throw new ArgumentNullException(nameof(channelId));
            }

            TwitchIntegrationHandler.LogToMain($"[{channelName}]{nameof(Start)}", $"Started {nameof(TwitchIntegrationClient)}");

            ChannelName = channelName;
            ChannelId = channelId;

            _connectionStatus.Reset();

            await SetupTwitchClient();

            await Task.Delay(3000);

            StartConnectionVerificationLoop();
        }
        catch (Exception e)
        {
            TwitchIntegrationHandler.LogToMain($"[{channelName}]{nameof(Start)}", string.Empty, LogSeverity.Error, exception: e);
        }
    }

    public void Stop()
    {
        _cancelConnectionVerificationSource?.Cancel();

        _connectionVerificationTask = null;
        _cancelConnectionVerificationSource = null;

        _configStreamEntities.Clear(); // TODO: proper cleanup, maybe extract in sub-class

        try
        {
            DestroyTwitchClient();
            _eventSubWebsocketClient.DisconnectAsync();
        }
        catch (Exception e)
        {
            TwitchIntegrationHandler.LogToMain($"[{ChannelName}]{nameof(Stop)}", string.Empty, LogSeverity.Error, exception: e);
        }
    }


    private async Task SetupTwitchClient()
    {
        CreateTwitchClient();

        try
        {
            if (!Client.Connect())
            {
                throw new Exception("Client failed to connect!");
            }

            if (!await _eventSubWebsocketClient.ConnectAsync())
            {
                throw new Exception("Event Sub Websocket failed to connect!");
            }
        }
        catch (Exception e)
        {
            TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(SetupTwitchClient)}", string.Empty, LogSeverity.Error, exception: e);
        }
    }

    private void CreateTwitchClient()
    {
        if (Client != null)
        {
            return;
        }

        ConnectionCredentials credentials = new(ConfigurationHandler.Shared.Secrets.TwitchBotUsername.ToLower(), ConfigurationHandler.Shared.Secrets.TwitchBotOAuth);
        ClientOptions clientOptions = new()
        {
            MessagesAllowedInPeriod = 50,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };

        WebSocketClient customClient = new(clientOptions);
        Client = new TwitchClient(customClient);

        SubscribeToEvents();

        Client.Initialize(credentials, ChannelName);
    }

    private void SubscribeToEvents()
    {
        Client.OnLog += ClientLog;
        Client.OnConnected += OnConnected;
        Client.OnDisconnected += OnDisconnected;
        Client.OnJoinedChannel += OnJoinedChannel;
    }

    private void DestroyTwitchClient()
    {
        if (Client == null)
        {
            return;
        }

        UnsubscribeFromEvents();

        Client.Disconnect();
        Client = null;
    }

    private void UnsubscribeFromEvents()
    {
        Client.OnLog -= ClientLog;
        Client.OnConnected -= OnConnected;
        Client.OnDisconnected -= OnDisconnected;
        Client.OnJoinedChannel -= OnJoinedChannel;
    }


    private void StartConnectionVerificationLoop()
    {
        _connectionVerificationTask ??= Task.Run(ConnectionVerificationLoop);
    }

    private async Task ConnectionVerificationLoop()
    {
        _cancelConnectionVerificationSource = new CancellationTokenSource();

        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(ConnectionVerificationLoop)}", "Started.");

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1));

            while (_cancelConnectionVerificationSource != null)
            {
                _cancelConnectionVerificationSource.Token.ThrowIfCancellationRequested();

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(9), _cancelConnectionVerificationSource.Token);

                    _connectionStatus.SetStatus(Client.IsConnected);

                    if (!_connectionStatus.IsConnected)
                    {
                        double offlineMinutes = _connectionStatus.TimeSinceLastChange().TotalMinutes;

                        if (offlineMinutes > ConfigurationHandler.Shared.TwitchForceReconnectDelayInMinutes)
                        {
                            TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(ConnectionVerificationLoop)}", $"Restarting Components - Connection Offline since {offlineMinutes}", LogSeverity.Warning);

                            _connectionStatus.LastChange = DateTime.Now;

                            await Start(ChannelName, ChannelId);

                            await Task.Delay(TimeSpan.FromMinutes(1), _cancelConnectionVerificationSource.Token);
                        }
                    }
                    else if (!_connectionStatus.EventSubConnected && !_connectionStatus.IsEventSubRateLimited())
                    {
                        _connectionStatus.WillAttemptEventSubConnection();

                        await _eventSubWebsocketClient.ReconnectAsync();
                    }
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(ConnectionVerificationLoop)}", string.Empty, LogSeverity.Error, exception: e);
                }
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception e)
        {
            TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(ConnectionVerificationLoop)}", string.Empty, LogSeverity.Error, exception: e);
        }
        finally
        {
            _connectionVerificationTask = null;
            _cancelConnectionVerificationSource = null;

            TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(ConnectionVerificationLoop)}", "Stopped.", LogSeverity.Warning);
        }
    }


    private void ClientLog(object sender, OnLogArgs e)
    {
        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(ClientLog)}", $"{e.DateTime}: {e.BotUsername} - {e.Data}", LogSeverity.Debug);
    }

    private void OnConnected(object sender, OnConnectedArgs e)
    {
        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(OnConnected)}", "Connected.");
    }

    private void OnDisconnected(object sender, OnDisconnectedEventArgs e)
    {
        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(OnDisconnected)}", "Disconnected.");
    }

    private void OnJoinedChannel(object sender, OnJoinedChannelArgs e)
    {
        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(OnJoinedChannel)}", $"Joined channel {e.Channel}");
    }


    // EVENT SUBS
    private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
    {
        _connectionStatus.EventSubConnected = true;
        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(OnWebsocketConnected)}", $"Websocket '{_eventSubWebsocketClient.SessionId}' connected!");

        if (e.IsRequestedReconnect)
        {
            return;
        }

        // Create and send EventSubscription
        await CreateEventSubscriptionsAsync();
    }

    private async Task CreateEventSubscriptionsAsync()
    {
        // subscribe to topics
        // create condition Dictionary
        // You need BOTH broadcaster and moderator values or EventSub returns an Error!
        var condition = new Dictionary<string, string>
        {
            { "broadcaster_user_id", ChannelId },
            { "moderator_user_id", TwitchIntegrationHandler.Instance.BotTwitchId },
            { "user_id", TwitchIntegrationHandler.Instance.BotTwitchId },
            { "to_broadcaster_user_id", ChannelId }
        };

        foreach ((string subType, string version) in _subscriptionTypes)
        {
            try
            {
                TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(CreateEventSubscriptionsAsync)}", $"Requesting '{subType}' v. {version}", LogSeverity.Debug);

                await TwitchIntegrationHandler.ValidatedApiCall(TwitchIntegrationHandler.Instance.Api.Helix.EventSub.CreateEventSubSubscriptionAsync(subType, version, condition, EventSubTransportMethod.Websocket, _eventSubWebsocketClient.SessionId));
            }
            catch (Exception ex)
            {
                TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(OnWebsocketConnected)}", $"Sending Topics '{subType} v.{version}' Failed!", LogSeverity.Error, exception: ex);
            }
        }

        // If you want to get Events for special Events you need to additionally add the AccessToken of the ChannelOwner to the request.
        // https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types/
    }


    private Task OnWebsocketDisconnected(object sender, EventArgs e)
    {
        _connectionStatus.EventSubConnected = false;

        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(OnWebsocketDisconnected)}", $"Websocket '{_eventSubWebsocketClient.SessionId}' disconnected!");

        return Task.CompletedTask;
    }

    private Task OnWebsocketReconnected(object sender, EventArgs e)
    {
        _connectionStatus.EventSubConnected = true;
        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(OnWebsocketReconnected)}", $"Websocket '{_eventSubWebsocketClient.SessionId}' reconnected!");

        return Task.CompletedTask;
    }

    private async Task OnErrorOccurred(object sender, ErrorOccuredArgs e)
    {
        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(OnErrorOccurred)}", $"Websocket '{_eventSubWebsocketClient.SessionId}' -> {e.Exception.Source}: {e.Exception.Message} | {e.Exception.StackTrace}", LogSeverity.Error);

        _connectionStatus.EventSubConnected = false;
        await _eventSubWebsocketClient.DisconnectAsync();
    }


    private Task EventSub_OnChannelChatMessage(object sender, ChannelChatMessageArgs e)
    {
        ChannelChatMessage evt = e.Payload.Event;

        if (evt.ChatterUserId == TwitchIntegrationHandler.Instance.BotTwitchId)
        {
            return Task.CompletedTask;
        }

        string srcChannelId = evt.SourceBroadcasterUserId;

        // Only listen to commands from our chat
        if (srcChannelId != null && srcChannelId != ChannelId)
        {
            return Task.CompletedTask;
        }

        string fromUser = evt.ChatterUserName;
        string fromUserId = evt.ChatterUserId;
        string message = evt.Message.Text.Trim();

        bool userIntro = evt.MessageType == "user_intro";

        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(EventSub_OnChannelChatMessage)}", $"Source: {evt.SourceBroadcasterUserName} | First: {userIntro} | {fromUser}: {message}", LogSeverity.Verbose);

        foreach ((ServerConfiguration config, ConfigStreamEntity entity) in _configStreamEntities)
        {
            entity.IntervalActionWatchdog.OnChatMessageReceived();

            if (userIntro)
            {
                HandleUserIntro(entity, fromUser).SafeAsync<TwitchIntegrationClient>(entity.Server.LogHandler);
            }

            // Active Chatters Update
            entity.ChatMessageReceived(fromUserId, fromUser).ConfigureAwait(false);

            // Parse for Commands
            bool isCommand = message[0] == config.GeneralSettings.CommandPrefix;
            bool isPoll = message[0] == config.GeneralSettings.PollVotePrefix;

            if (!isCommand && !isPoll)
            {
                continue;
            }

            TwitchMessageChannel channelMessage = new(Client, config.GuildId, config.TwitchSettings.TwitchMessageChannelId, ChannelName);
            TwitchUser author = new(evt);
            TwitchUserMessage twitchMessage = new(channelMessage, author, message);

            entity.Server.HandleCommandAsync(twitchMessage).SafeAsync<TwitchIntegrationClient>(entity.Server.LogHandler);
        }

        return Task.CompletedTask;
    }

    private async Task HandleUserIntro(ConfigStreamEntity entity, string userName)
    {
        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(HandleUserIntro)}", $"User: {userName}");

        CustomRuntimeResult<CustomCommand> callback = await entity.Server.GetModule<UserCallbackHandler>().GetCallbackCommand(UserCallbackDictionary.TwitchCallbackTypes.OnUserIntro);

        if (callback.IsSuccess && callback.ResultValue is { } command)
        {
            await command.Execute(null, [userName]);
        }
    }


    private Task EventSub_OnChannelBan(object sender, ChannelBanArgs e)
    {
        string channel = e.Payload.Event.BroadcasterUserName;
        string user = e.Payload.Event.UserName;
        string mod = e.Payload.Event.ModeratorUserName;
        string action = e.Payload.Event.IsPermanent ? "banned" : "timeout";
        string reason = e.Payload.Event.Reason;

        TwitchIntegrationHandler.LogToMain($"[{channel}] {nameof(EventSub_OnChannelBan)}", $"User '{user}' {action} by '{mod}' for '{reason}'.");
        return Task.CompletedTask;
    }

    private Task EventSub_OnChannelUnban(object sender, ChannelUnbanArgs e)
    {
        string channel = e.Payload.Event.BroadcasterUserName;
        string user = e.Payload.Event.UserName;
        string mod = e.Payload.Event.ModeratorUserName;

        TwitchIntegrationHandler.LogToMain($"[{channel}] {nameof(EventSub_OnChannelBan)}", $"User '{user}' unbanned by '{mod}'.");
        return Task.CompletedTask;
    }

    private Task EventSub_OnChannelFollow(object sender, ChannelFollowArgs e)
    {
        ChannelFollow followPayload = e?.Payload?.Event;

        string userName = followPayload?.UserName ?? "NULL";
        string userId = followPayload?.UserId;

        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(EventSub_OnChannelFollow)}", $"'{userName}' just followed!");

        if (userId == null)
        {
            return Task.CompletedTask;
        }

        foreach ((ServerConfiguration _, ConfigStreamEntity entity) in _configStreamEntities)
        {
            NotifyChannelFollow(entity, userName, userId).SafeAsync<TwitchIntegrationClient>(entity.Server.LogHandler);
        }

        return Task.CompletedTask;
    }

    private async Task NotifyChannelFollow(ConfigStreamEntity entity, string userName, string userId)
    {
        entity.UpdateUserCooldownList();

        bool onCooldown = entity.IsOnCooldown(userId);
        entity.AddCooldown(userId);

        if (onCooldown)
        {
            TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(NotifyChannelFollow)}", "User already triggered callback.");
        }
        else
        {
            CustomRuntimeResult<CustomCommand> callback = await entity.Server.GetModule<UserCallbackHandler>().GetCallbackCommand(UserCallbackDictionary.TwitchCallbackTypes.OnFollow);

            if (callback.IsSuccess && callback.ResultValue is { } command)
            {
                await command.Execute(null, [userName]);
            }
        }
    }


    private Task EventSub_OnChannelRaid(object sender, ChannelRaidArgs e)
    {
        ChannelRaid raidPayload = e?.Payload?.Event;

        string fromUserName = raidPayload?.FromBroadcasterUserName ?? "NULL";
        string toUserName = raidPayload?.ToBroadcasterUserName ?? "NULL";
        string viewerCount = raidPayload?.Viewers.ToString() ?? "-1";

        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(EventSub_OnChannelRaid)}", $"{fromUserName} -> {toUserName} with '{viewerCount}' viewers.");

        if (raidPayload == null || raidPayload.FromBroadcasterUserId == ChannelId)
        {
            return Task.CompletedTask;
        }

        foreach ((ServerConfiguration _, ConfigStreamEntity entity) in _configStreamEntities)
        {
            NotifyChannelRaid(entity, fromUserName, viewerCount).SafeAsync<TwitchIntegrationClient>(entity.Server.LogHandler);
        }

        return Task.CompletedTask;
    }

    private async Task NotifyChannelRaid(ConfigStreamEntity entity, string fromUserName, string viewerCount)
    {
        CustomRuntimeResult<CustomCommand> callback = await entity.Server.GetModule<UserCallbackHandler>().GetCallbackCommand(UserCallbackDictionary.TwitchCallbackTypes.OnRaid);

        if (callback.IsSuccess && callback.ResultValue is { } command)
        {
            await command.Execute(null, [fromUserName, viewerCount]);
        }
    }


    private Task EventSub_OnChannelSubscribe(object sender, ChannelSubscribeArgs e)
    {
        ChannelSubscribe subPayload = e?.Payload?.Event;

        string userName = subPayload?.UserName ?? "NULL";
        string tier = subPayload?.Tier ?? "NULL";
        bool isGift = subPayload?.IsGift ?? false;

        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(EventSub_OnChannelSubscribe)}", $"User '{userName}' just {(isGift ? "subscribed" : "was gifted")} via '{tier}'!");

        return Task.CompletedTask;
    }


    private async Task EventSub_OnChannelUpdate(object sender, ChannelUpdateArgs e)
    {
        ChannelUpdate updatePayload = e?.Payload?.Event;

        string title = updatePayload?.Title ?? "NULL";
        string category = updatePayload?.CategoryName ?? "NULL";

        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(EventSub_OnChannelUpdate)}", $"Channel Updated! Now streaming '{title}' in category '{category}'!", LogSeverity.Debug);

        StreamInfo.Title = title;
        StreamInfo.Category = category;

        EnsureLiveStreamUpdateLoop();

        await NotifyStreamUpdate();
    }

    private Task NotifyStreamUpdate()
    {
        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(NotifyStreamUpdate)}", $"[{ChannelName}] Stream Updated! {StreamInfo}");

        foreach ((ServerConfiguration _, ConfigStreamEntity entity) in _configStreamEntities)
        {
            NotifyStreamUpdate(entity).SafeAsync<TwitchIntegrationClient>(entity.Server.LogHandler);
        }

        return Task.CompletedTask;
    }

    private async Task NotifyStreamUpdate(ConfigStreamEntity entity)
    {
        if ((await entity.Server.GetModule<UserCallbackHandler>().GetCallbackCommand(UserCallbackDictionary.TwitchCallbackTypes.OnStreamUpdate)).ResultValue is { } callback)
        {
            await callback.Execute(null, [StreamInfo.Title, StreamInfo.Category, StreamInfo.LastChange.ToString(entity.Server.RuntimeConfig.CultureInfo)]);
        }
    }


    private async Task EventSub_OnStreamOnline(object sender, StreamOnlineArgs e)
    {
        if (StreamInfo.IsOnline)
        {
            return;
        }

        StreamOnline evt = e?.Payload?.Event;
        DateTimeOffset startedAt = evt?.StartedAt ?? DateTimeOffset.Now;

        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(EventSub_OnStreamOnline)}", $"Channel went live at {startedAt}!", LogSeverity.Debug);

        StreamInfo.SetOnline(startedAt);

        if (StreamInfo.HasInvalidEntries)
        {
            StreamInfo.Update(await TwitchIntegrationHandler.GetStream(ChannelId, ChannelName));
        }

        EnsureLiveStreamUpdateLoop();

        await NotifyStreamOnline();
    }

    private async Task NotifyStreamOnline()
    {
        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(NotifyStreamOnline)}", $"[{ChannelName}] Stream is Online! {StreamInfo}");

        Game game = null;

        if (!string.IsNullOrWhiteSpace(StreamInfo.Category))
        {
            try
            {
                GetGamesResponse gameMatches = await TwitchIntegrationHandler.ValidatedApiCall(TwitchIntegrationHandler.Instance.Api.Helix.Games.GetGamesAsync([StreamInfo.Category]));

                if (gameMatches.Games?.Length > 0)
                {
                    game = gameMatches.Games[0];
                }
            }
            catch (Exception ex)
            {
                TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(NotifyStreamOnline)}", "Failed retrieving game.", exception: ex);
            }
        }

        string titleName = StreamInfo.Title ?? "???";
        string gameName = game?.Name ?? "???";
        double minutesSinceLastChange = StreamInfo.TimeSinceLastChange.TotalMinutes;

        foreach ((ServerConfiguration config, ConfigStreamEntity entity) in _configStreamEntities)
        {
            NotifyStreamOnline(config, entity, titleName, gameName, minutesSinceLastChange).SafeAsync<TwitchIntegrationClient>(entity.Server.LogHandler);
        }
    }

    private async Task NotifyStreamOnline(ServerConfiguration config, ConfigStreamEntity entity, string titleName, string gameName, double minutesSinceLastChange)
    {
        entity.IntervalActionWatchdog.Start();

        string lastChangeString = StreamInfo.LastChange.ToString(entity.Server.RuntimeConfig.CultureInfo);

        CustomRuntimeResult<CustomCommand> callbackResult = await entity.Server.GetModule<UserCallbackHandler>().GetCallbackCommand(UserCallbackDictionary.TwitchCallbackTypes.OnStreamStart);
        if (callbackResult.IsSuccess)
        {
            if (callbackResult.ResultValue is { } callback)
            {
                await callback.Execute(null, [titleName, gameName, lastChangeString]);
            }
        }

        if (minutesSinceLastChange < config.TwitchSettings.LivestreamOneShotWindowInMinutes)
        {
            callbackResult = await entity.Server.GetModule<UserCallbackHandler>().GetCallbackCommand(UserCallbackDictionary.TwitchCallbackTypes.OnStreamStartOneShot);

            if (callbackResult.IsSuccess && callbackResult.ResultValue is { } callback)
            {
                await callback.Execute(null, [titleName, gameName, lastChangeString]);
            }
        }
    }


    private async Task EventSub_OnStreamOffline(object sender, StreamOfflineArgs e)
    {
        if (!StreamInfo.IsOnline)
        {
            return;
        }

        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(EventSub_OnStreamOffline)}", "Channel went offline!", LogSeverity.Debug);

        StreamInfo.SetOffline();

        await NotifyStreamOffline();
    }

    private Task NotifyStreamOffline()
    {
        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(NotifyStreamOffline)}", $"Stream is Offline! {StreamInfo}");

        foreach ((ServerConfiguration config, ConfigStreamEntity entity) in _configStreamEntities)
        {
            NotifyStreamOffline(config, entity).SafeAsync<TwitchIntegrationClient>(entity.Server.LogHandler);
        }

        return Task.CompletedTask;
    }

    private async Task NotifyStreamOffline(ServerConfiguration config, ConfigStreamEntity entity)
    {
        entity.ClearActiveChatters();
        entity.IntervalActionWatchdog.Stop();

        CustomRuntimeResult<CustomCommand> callbackResult = await entity.Server.GetModule<UserCallbackHandler>().GetCallbackCommand(UserCallbackDictionary.TwitchCallbackTypes.OnStreamEnd);

        if (callbackResult.IsSuccess)
        {
            if (callbackResult.ResultValue is { } callback)
            {
                await callback.Execute(null, [StreamInfo.Title, StreamInfo.Category, StreamInfo.LastChange.ToString(entity.Server.RuntimeConfig.CultureInfo)]);
            }
        }

        if (StreamInfo.TimeSinceLastChange.TotalMinutes < config.TwitchSettings.LivestreamOneShotWindowInMinutes)
        {
            callbackResult = await entity.Server.GetModule<UserCallbackHandler>().GetCallbackCommand(UserCallbackDictionary.TwitchCallbackTypes.OnStreamEndOneShot);

            if (callbackResult.IsSuccess && callbackResult.ResultValue is { } callback)
            {
                await callback.Execute(null, [StreamInfo.Title, StreamInfo.Category, StreamInfo.LastChange.ToString(entity.Server.RuntimeConfig.CultureInfo)]);
            }
        }
    }


    private void EnsureLiveStreamUpdateLoop()
    {
        if (_liveStreamUpdateLoopTask != null || !StreamInfo.IsOnline)
        {
            return;
        }

        _liveStreamUpdateLoopTask = Task.Run(LiveStreamUpdateLoop);
    }

    private async Task LiveStreamUpdateLoop()
    {
        TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(LiveStreamUpdateLoop)}", "Started!", LogSeverity.Debug);

        try
        {
            do
            {
                await Task.Delay(TimeSpan.FromMinutes(ConfigurationHandler.Shared.LivestreamMonitorIntervalInMinutes));

                foreach ((ServerConfiguration _, ConfigStreamEntity entity) in _configStreamEntities)
                {
                    await entity.UpdateViewers();
                }
            } while (_liveStreamUpdateLoopTask != null && StreamInfo.IsOnline);
        }
        catch (Exception ex)
        {
            TwitchIntegrationHandler.LogToMain(nameof(LiveStreamUpdateLoop), string.Empty, LogSeverity.Error, exception: ex);
        }
        finally
        {
            _liveStreamUpdateLoopTask = null;

            TwitchIntegrationHandler.LogToMain($"[{ChannelName}] {nameof(LiveStreamUpdateLoop)}", "Stopped!", LogSeverity.Debug);
        }
    }


    public void StartListening(Server server)
    {
        if (_configStreamEntities.ContainsKey(server.Config))
        {
            return;
        }

        _configStreamEntities.Add(server.Config, new ConfigStreamEntity(server, server.Config));
    }

    public void StopListening(ServerConfiguration config)
    {
        _configStreamEntities.Remove(config);
    }

    private class ConnectionStatus
    {
        private bool _eventSubConnected;
        private int _eventSubRateLimitInSeconds = 120;

        private DateTime _lastEventSubConnectionAttempt = DateTime.Now;
        public bool IsConnected;
        public DateTime LastChange = DateTime.Now;

        public bool EventSubConnected
        {
            get => _eventSubConnected;
            set
            {
                if (_eventSubConnected == value)
                {
                    return;
                }

                _eventSubConnected = value;

                if (_eventSubConnected)
                {
                    _eventSubRateLimitInSeconds = 0;
                }
            }
        }


        public void SetStatus(bool connected)
        {
            if (IsConnected != connected)
            {
                LastChange = DateTime.Now;
            }

            IsConnected = connected;
        }


        public TimeSpan TimeSinceLastChange()
        {
            return DateTime.Now - LastChange;
        }


        public bool IsEventSubRateLimited()
        {
            return (DateTime.Now - _lastEventSubConnectionAttempt).TotalSeconds < _eventSubRateLimitInSeconds;
        }

        public void WillAttemptEventSubConnection()
        {
            _lastEventSubConnectionAttempt = DateTime.Now;

            if (_eventSubRateLimitInSeconds < 600)
            {
                _eventSubRateLimitInSeconds += 60;
            }
        }

        public void Reset()
        {
            IsConnected = false;
            EventSubConnected = false;
            LastChange = DateTime.Now;
            _lastEventSubConnectionAttempt = DateTime.Now;
            _eventSubRateLimitInSeconds = 0;
        }
    }


    private class ConfigStreamEntity
    {
        private readonly List<ActiveChatterInfo> _activeChatters = new();
        private readonly Lock _activeChattersLocker = new();
        private readonly ServerConfiguration _config;

        private readonly Dictionary<string, long> _userCooldownList = new();
        private readonly Lock _userCooldownLocker = new();

        public readonly TwitchLivestreamIntervalActionWatchdog IntervalActionWatchdog;
        public readonly Server Server;


        public ConfigStreamEntity(Server server, ServerConfiguration config)
        {
            Server = server;
            _config = config;

            IntervalActionWatchdog = new TwitchLivestreamIntervalActionWatchdog(server, config);
        }


        public void UpdateUserCooldownList()
        {
            try
            {
                List<string> toRemove = [];
                long timespan = TimeSpan.FromMinutes(Math.Max(1, _config.TwitchSettings.TwitchFollowAlertCooldownInMinutes)).Ticks;

                lock (_userCooldownLocker)
                {
                    foreach ((string id, long ticks) in _userCooldownList)
                    {
                        if (DateTime.UtcNow.Ticks - ticks < timespan)
                        {
                            continue;
                        }

                        toRemove.Add(id);
                    }

                    foreach (string entry in toRemove)
                    {
                        _userCooldownList.Remove(entry);
                    }
                }
            }
            catch (Exception e)
            {
                Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateUserCooldownList), "", e));
            }
        }

        public bool IsOnCooldown(string userId)
        {
            lock (_userCooldownLocker)
            {
                return _userCooldownList.ContainsKey(userId);
            }
        }

        public void AddCooldown(string userId)
        {
            lock (_userCooldownLocker)
            {
                _userCooldownList[userId] = DateTime.UtcNow.Ticks;
            }
        }


        public async Task ChatMessageReceived(string userId, string userName)
        {
            try
            {
                await Server.GetModule<ForestUserHandler>().GetOrCreateUser(userId);

                lock (_activeChattersLocker)
                {
                    ActiveChatterInfo chatter = _activeChatters.FirstOrDefault(c => c.UserId == userId);

                    if (chatter == null)
                    {
                        _activeChatters.Add(new ActiveChatterInfo(userId, userName, DateTime.Now));
                    }
                    else
                    {
                        chatter.LatestMessage = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(ChatMessageReceived), "", ex));
            }
        }

        public async Task UpdateViewers()
        {
            await RemoveInactiveChatters();
            await UpdateTwitchPoints();
        }

        private async Task RemoveInactiveChatters()
        {
            lock (_activeChattersLocker)
            {
                for (int i = _activeChatters.Count - 1; i >= 0; i--)
                {
                    if ((DateTime.Now - _activeChatters[i].LatestMessage).TotalMinutes > _config.TwitchSettings.ActiveChatterWindowInMinutes)
                    {
                        _activeChatters.RemoveAt(i);
                    }
                }
            }

            await Task.Delay(10);
        }

        private async Task UpdateTwitchPoints()
        {
            GetUsersResponse response = await TwitchIntegrationHandler.ValidatedApiCall(TwitchIntegrationHandler.Instance.Api.Helix.Users.GetUsersAsync(logins: await TwitchIntegrationHandler.GetChattersForChannel(_config.TwitchSettings.TwitchChannelName)));

            // Points for broadcaster channel
            User broadcaster = ConfigurationHandler.RuntimeConfig[_config.GuildId].ChannelOwner;
            if (broadcaster != null)
            {
                CustomRuntimeResult<ForestUser> getChannelUserResult = await Server.GetModule<ForestUserHandler>().GetUser(twitchId: broadcaster.Id);

                if (getChannelUserResult.IsSuccess)
                {
                    await AddPointsToUser(getChannelUserResult.ResultValue);
                }
                else
                {
                    await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(UpdateTwitchPoints), $"Could not get {nameof(ForestUser)} for '{broadcaster.DisplayName}'!"));
                }
            }

            // Points for viewers
            ForestUser[] existingUsers = await Server.GetModule<ForestUserHandler>().GetUsers(response.Users.Select(u => u.Id).ToArray());
            foreach (ForestUser forestUser in existingUsers)
            {
                try
                {
                    forestUser.UpdateUserData();
                    AddViewTimeToUser(forestUser);
                    await AddPointsToUser(forestUser);
                }
                catch (Exception e)
                {
                    await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(UpdateTwitchPoints), "", e));
                }
            }
        }

        private static void AddViewTimeToUser(ForestUser user)
        {
            user.TwitchViewTime = TimeSpan.FromTicks(user.TwitchViewTime.Ticks + TimeSpan.FromMinutes(ConfigurationHandler.Shared.LivestreamMonitorIntervalInMinutes).Ticks);
            user.IsDirty = true;
        }

        private async Task AddPointsToUser(ForestUser user)
        {
            int totalPoints = _config.TwitchSettings.TwitchPointsPerMonitorInterval;

            lock (_activeChatters)
            {
                if (_activeChatters.Any(c => c.UserId == user.TwitchUserId))
                {
                    totalPoints += _config.TwitchSettings.TwitchPointsBonusForActiveChatters;
                }
            }

            await Server.GetModule<CurrencyHandler>().AddCurrencyToUser(user, totalPoints);
        }

        public void ClearActiveChatters()
        {
            lock (_activeChattersLocker)
            {
                _activeChatters.Clear();
            }
        }


        private class ActiveChatterInfo(string id, string name, DateTime latestMessage)
        {
            public readonly string UserId = id;
            public DateTime LatestMessage = latestMessage;
            public string UserName = name;
        }
    }
}