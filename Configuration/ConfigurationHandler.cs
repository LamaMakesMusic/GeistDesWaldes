using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Discord;
using GeistDesWaldes.Misc;

namespace GeistDesWaldes.Configuration;

public static class ConfigurationHandler
{
    public const string SHARED_CONFIG_FILE_NAME = "ConfigCommon";
    public const string SERVER_CONFIG_FILE_NAME = "Config";
    public static SharedConfiguration Shared = new();
    public static readonly ConcurrentDictionary<ulong, ServerConfiguration> Configs = new();
    public static readonly ConcurrentDictionary<ulong, RuntimeConfiguration> RuntimeConfig = new();

    public static string ServerFilesDirectory;


    public static async Task Init()
    {
        ServerFilesDirectory = Path.GetFullPath(Path.Combine(Launcher.ApplicationFilesPath, "servers"));
        await GenericXmlSerializer.EnsurePathExistence<object>(null, ServerFilesDirectory);
        await GenericXmlSerializer.EnsurePathExistence(null, Launcher.CommonFilesPath, SHARED_CONFIG_FILE_NAME, Shared);
    }

    public static ServerConfiguration EnsureServerConfig(ulong guildId)
    {
        if (!Configs.ContainsKey(guildId))
        {
            Configs.TryAdd(guildId, new ServerConfiguration(guildId));
        }

        if (!RuntimeConfig.ContainsKey(guildId))
        {
            RuntimeConfig.TryAdd(guildId, new RuntimeConfiguration(guildId));
        }

        return Configs[guildId];
    }

    public static async Task SaveAllConfigsToFile()
    {
        await SaveSharedConfigToFile();

        foreach (KeyValuePair<ulong, ServerConfiguration> pair in Configs)
        {
            await SaveConfigToFile(pair.Value);
        }
    }

    public static async Task SaveSharedConfigToFile()
    {
        await GenericXmlSerializer.SaveAsync<SharedConfiguration>(Launcher.Instance.LogHandler, Shared, SHARED_CONFIG_FILE_NAME, Launcher.CommonFilesPath);
    }

    public static async Task SaveConfigToFile(ServerConfiguration config)
    {
        Server server = Launcher.Instance.Servers[config.GuildId];
        await GenericXmlSerializer.SaveAsync<ServerConfiguration>(Launcher.Instance.LogHandler, config, SERVER_CONFIG_FILE_NAME, server.ServerFilesDirectoryPath);
    }

    public static async Task LoadSharedConfigFromFile()
    {
        SharedConfiguration loadedConfiguration = await GenericXmlSerializer.LoadAsync<SharedConfiguration>(Launcher.Instance.LogHandler, SHARED_CONFIG_FILE_NAME, Launcher.CommonFilesPath);

        if (loadedConfiguration == default)
        {
            await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadSharedConfigFromFile), "Loaded Configuration == DEFAULT"));
            return;
        }

        lock (Shared)
        {
            Shared = loadedConfiguration;

            if (Shared.Secrets.TwitchBotUsername != null)
            {
                Shared.Secrets.TwitchBotUsername = Shared.Secrets.TwitchBotUsername.Trim().ToLower();
            }
        }

        await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(LoadSharedConfigFromFile), $"Daily restart time of day: {loadedConfiguration.DailyRestartTime} => next in: {Launcher.GetNextRestartTime()}"), (int)ConsoleColor.Green);
    }

    public static async Task LoadServerConfigFromFile(ulong guildId)
    {
        ServerConfiguration loadedConfig = await GenericXmlSerializer.LoadAsync<ServerConfiguration>(Launcher.Instance.LogHandler, SERVER_CONFIG_FILE_NAME, Path.Combine(ServerFilesDirectory, guildId.ToString()));

        if (loadedConfig == null)
        {
            return;
        }

        loadedConfig.GuildId = guildId;

        await VerifyConfigContents(loadedConfig);

        Configs[guildId] = loadedConfig;
        RuntimeConfig[guildId] = new RuntimeConfiguration(guildId);
    }

    private static async Task VerifyConfigContents(ServerConfiguration config)
    {
        // Twitch channel
        if (config.TwitchSettings.TwitchChannelName != null)
        {
            config.TwitchSettings.TwitchChannelName = config.TwitchSettings.TwitchChannelName.Trim().ToLower();
        }


        #region WebCal

        string webCalLink = config.TwitchSettings.WebCalLink;

        if (!string.IsNullOrWhiteSpace(webCalLink))
        {
            webCalLink = webCalLink.Trim('"').Trim();
            webCalLink = webCalLink.Replace("http://", "");
            webCalLink = webCalLink.Replace("https://", "");
            webCalLink = webCalLink.Replace("webcal://", "");

            webCalLink = $"https://{webCalLink}";
        }
        else
        {
            webCalLink = null;
        }

        config.TwitchSettings.WebCalLink = webCalLink;

        #endregion
    }
}