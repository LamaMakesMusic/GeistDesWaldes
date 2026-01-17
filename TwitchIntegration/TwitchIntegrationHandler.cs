using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using GeistDesWaldes.Configuration;
using GeistDesWaldes.Misc;
using Microsoft.Extensions.DependencyInjection;
using TwitchLib.Api;
using TwitchLib.Api.Core;
using TwitchLib.Api.Helix.Models.Chat;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Websockets;

namespace GeistDesWaldes.TwitchIntegration;

public class TwitchIntegrationHandler
{
    // https://dev.twitch.tv/docs/authentication/scopes/
    private static readonly string[] _scopeBot =
    [
        // Twitch API and EventSub scopes
        "moderator:manage:announcements",
        "moderator:read:banned_users",
        //"moderator:manage:banned_users",
        "moderator:read:chatters",
        "moderator:read:followers",
        "moderator:read:guest_star",
        "moderator:manage:guest_star",
        "moderator:read:moderators",
        "moderator:read:shoutouts",
        "moderator:manage:shoutouts",
        "moderator:read:vips",
        "moderator:manage:warnings",
        "user:bot",
        "user:read:blocked_users",
        "user:manage:blocked_users",
        "user:read:chat",
        "user:read:emotes",
        "user:read:moderated_channels",
        // "user:read:whispers",
        // "user:manage:whispers",
        "user:write:chat",

        // IRC Chat Scopes
        "chat:edit",
        "chat:read"


        // PubSub Scopes
        // "whispers:read"
    ];

    private static DateTime _lastAuthTokenCheck = DateTime.Now;

    public readonly Dictionary<string, TwitchIntegrationClient> Clients = new();

    private string _botTwitchId;

    private TwitchUser _botUser;

    public TwitchAPI Api;


    public TwitchIntegrationHandler()
    {
        TwitchAuthentication.OnLog += LogEventHandler;
    }

    public static TwitchIntegrationHandler Instance
    {
        get
        {
            if (Launcher.Instance != null)
            {
                return Launcher.Instance.TwitchIntegrationHandler;
            }

            return null;
        }
    }

    public string BotTwitchId
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_botTwitchId) && !string.IsNullOrWhiteSpace(ConfigurationHandler.Shared.Secrets.TwitchBotUsername))
            {
                User[] users = Task.Run(() => ValidatedApiCall(Api.Helix.Users.GetUsersAsync(logins: new List<string> { ConfigurationHandler.Shared.Secrets.TwitchBotUsername }))).GetAwaiter().GetResult()?.Users;

                _botTwitchId = users?[0]?.Id;
            }

            return _botTwitchId;
        }
    }

    public TwitchUser BotUser
    {
        get
        {
            if (_botUser == null && !string.IsNullOrWhiteSpace(BotTwitchId))
            {
                _botUser = new TwitchUser(_botTwitchId, ConfigurationHandler.Shared.Secrets.TwitchBotUsername, true);
            }

            return _botUser;
        }
    }


    public static async Task<List<string>> GetChattersForChannel(string channelName)
    {
        string channelId = await ChannelNameToBroadcasterId(channelName);

        if (string.IsNullOrWhiteSpace(channelId))
        {
            throw new Exception($"Could not get channel id for channel '{channelName}'");
        }

        return (await ValidatedApiCall(Instance.Api.Helix.Chat.GetChattersAsync(channelId, Instance.BotTwitchId))).Data.Select(n => n.UserLogin).ToList();
    }

    public static async Task SendAnnouncement(string channelName, string message, AnnouncementColors color = null)
    {
        string channelId = await ChannelNameToBroadcasterId(channelName);

        if (string.IsNullOrWhiteSpace(channelId))
        {
            throw new Exception($"Could not get channel id for channel '{channelName}'");
        }

        await ValidatedApiCall(Instance.Api.Helix.Chat.SendChatAnnouncementAsync(channelId, Instance.BotTwitchId, message, color));
    }

    public static async Task SendShoutout(string channelName, string userToShoutout)
    {
        await SendAnnouncement(channelName, $"Schaut doch mal bei {userToShoutout} vorbei!");
        //await ValidatedAPICall(Instance.API.Helix.Chat.SendShoutoutAsync(channelName, userToShoutout)); -> Does not exist yet
    }

    public static async Task<string> ChannelNameToBroadcasterId(string channelName)
    {
        try
        {
            // get from cache if known broadcaster
            foreach (Server server in Launcher.Instance.Servers.Values)
            {
                if (server.Config.TwitchSettings.TwitchChannelName.Equals(channelName, StringComparison.OrdinalIgnoreCase))
                {
                    return server.RuntimeConfig.ChannelOwner.Id;
                }
            }

            return (await ValidatedApiCall(Instance.Api.Helix.Users.GetUsersAsync(logins: [channelName])))?.Users?[0]?.Id;
        }
        catch (Exception ex)
        {
            LogToMain($"[{channelName}] {nameof(ChannelNameToBroadcasterId)}", $"Could not get broadcaster id for '{channelName}'", LogSeverity.Error, exception: ex);
        }

        return null;
    }

    public static async Task<Stream> GetStream(string channelId, string debugSource)
    {
        try
        {
            GetStreamsResponse streamResponse = await ValidatedApiCall(Instance.Api.Helix.Streams.GetStreamsAsync(first: 1, userIds: [channelId]));
            return streamResponse.Streams[0];
        }
        catch (Exception ex)
        {
            LogToMain($"[{debugSource}] {nameof(GetStream)}", $"Failed getting stream for Id '{channelId}'!", LogSeverity.Error, exception: ex);
        }

        return null;
    }


    public async Task InitializeTwitchIntegration()
    {
        if (await AuthenticateAndUpdateTokens(true))
        {
            try
            {
                await SetupApi();
            }
            catch (Exception e)
            {
                LogToMain(nameof(InitializeTwitchIntegration), string.Empty, LogSeverity.Error, (int)ConsoleColor.Magenta, e);
            }
        }
    }

    public Task OnShutdownAsync()
    {
        TwitchAuthentication.OnLog -= LogEventHandler;

        foreach (TwitchIntegrationClient client in Clients.Values)
        {
            client?.Stop();
        }

        Clients.Clear();
        return Task.CompletedTask;
    }

    public async Task SetupApi()
    {
        ApiSettings settings = new()
        {
            ClientId = ConfigurationHandler.Shared.Secrets.TwitchBotClientId,
            AccessToken = ConfigurationHandler.Shared.Secrets.TwitchBotOAuth,
            Secret = ConfigurationHandler.Shared.Secrets.TwitchBotClientSecret
        };

        Api = new TwitchAPI(settings: settings);

        TwitchUser bot = BotUser;

        await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(SetupApi), $"Bot Twitch User: {(bot != null ? $"{bot.Username} ({bot.TwitchId})" : "NULL")}"));
    }

    public async Task StartListening(Server server)
    {
        User twitchChannel = server.RuntimeConfig.ChannelOwner;
        if (twitchChannel == null)
        {
            LogToMain(nameof(StartListening), $"Could not get Twitch Channel Owner for {server.GuildId}!", LogSeverity.Error);
            return;
        }

        if (!Clients.TryGetValue(server.Config.TwitchSettings.TwitchChannelName, out TwitchIntegrationClient existing))
        {
            EventSubWebsocketClient socket = server.Services.GetService<EventSubWebsocketClient>();

            if (socket == null)
            {
                LogToMain(nameof(StartListening), $"Could not get {nameof(EventSubWebsocketClient)} for {server.GuildId}", LogSeverity.Error);
                return;
            }

            existing = new TwitchIntegrationClient(socket);

            Clients.Add(server.Config.TwitchSettings.TwitchChannelName, existing);

            await existing.Start(server.Config.TwitchSettings.TwitchChannelName, server.RuntimeConfig.ChannelOwner.Id);
        }

        existing.StartListening(server);
    }

    public void StopListening(Server server)
    {
        if (!Clients.TryGetValue(server.Config.TwitchSettings.TwitchChannelName, out TwitchIntegrationClient existing))
        {
            return;
        }

        existing.StopListening(server.Config);

        if (existing.ServerCount == 0)
        {
            Clients.Remove(existing.ChannelName);
        }
    }


    private static void LogEventHandler(object o, LogEventArgs e)
    {
        LogToMain(e.Source, e.Message, (LogSeverity)e.Severity, e.Color, e.Exception);
    }

    public JoinedChannel GetChannelObject(string channelName)
    {
        foreach (KeyValuePair<string, TwitchIntegrationClient> pair in Clients)
        {
            if (pair.Value.ChannelName.Equals(channelName, StringComparison.Ordinal))
            {
                return pair.Value.Client.GetJoinedChannel(channelName);
            }
        }

        return null;
    }

    public TwitchClient GetClient(string channelName)
    {
        if (Clients.ContainsKey(channelName))
        {
            return Clients[channelName].Client;
        }

        return null;
    }

    public static async Task ValidatedApiCall(Task action)
    {
        await AutoUpdateTokens();

        try
        {
            await action;
        }
        catch (Exception e)
        {
            await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(ValidatedApiCall), $"Retrying, because of: \n{e.Message}"));

            if (!await AuthenticateAndUpdateTokens())
            {
                throw new Exception("Updating Tokens failed!");
            }

            await action;
        }
    }

    public static async Task<T> ValidatedApiCall<T>(Task<T> action)
    {
        await AutoUpdateTokens();

        try
        {
            return await action;
        }
        catch (Exception e)
        {
            await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(ValidatedApiCall), $"Retrying, because of: \n{e.Message}"));

            if (!await AuthenticateAndUpdateTokens())
            {
                throw new Exception("Updating Tokens failed!");
            }

            return await action;
        }
    }

    private static async Task AutoUpdateTokens()
    {
        if ((DateTime.Now - _lastAuthTokenCheck).TotalHours < 1)
        {
            return;
        }

        if (await AuthenticateAndUpdateTokens())
        {
            return;
        }

        throw new Exception("Updating Tokens failed!");
    }

    private static async Task<bool> AuthenticateAndUpdateTokens(bool allowTokenRequest = false)
    {
        StringBuilder scopeBuilder = new();
        for (int i = 0; i < _scopeBot.Length; i++)
        {
            scopeBuilder.Append($"{_scopeBot[i]}+");
        }

        _lastAuthTokenCheck = DateTime.Now;

        ValidationResult validationResult = await TwitchAuthentication.ValidateBotUserAuthentication(scopeBuilder.ToString().TrimEnd('+'), ConfigurationHandler.Shared.Secrets.TwitchBotOAuth, ConfigurationHandler.Shared.Secrets.TwitchBotOAuthRefresh, ConfigurationHandler.Shared.Secrets.TwitchBotClientId, ConfigurationHandler.Shared.Secrets.TwitchBotClientSecret, ConfigurationHandler.Shared.Secrets.TwitchBotOAuthRedirectURL, allowTokenRequest);

        if (validationResult.Successful && validationResult.TokensUpdated)
        {
            ConfigurationHandler.Shared.Secrets.TwitchBotOAuth = validationResult.OAuthToken;
            ConfigurationHandler.Shared.Secrets.TwitchBotOAuthRefresh = validationResult.OAuthRefreshToken;

            await ConfigurationHandler.SaveSharedConfigToFile();

            await Instance.SetupApi();
        }

        return validationResult.Successful;
    }


    public static void LogToMain(string source, string message, LogSeverity severity = LogSeverity.Info, int consoleColor = -1, Exception exception = null)
    {
        if (consoleColor is < 0 or > 15)
        {
            switch (severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                case LogSeverity.Warning:
                    consoleColor = -1;
                    break;
                case LogSeverity.Info:
                    consoleColor = (int)ConsoleColor.Magenta;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                default:
                    consoleColor = (int)ConsoleColor.DarkMagenta;
                    break;
            }
        }

        Launcher.Instance.LogHandler.Log(new LogMessage(severity, source, message, exception), consoleColor).SafeAsync<TwitchIntegrationHandler>(Launcher.Instance.LogHandler);
    }
}