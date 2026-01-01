using System;

namespace GeistDesWaldes.Configuration
{
    [Serializable]
    public class SharedConfiguration
    {
        public const string DISCORD_INVITE_URL = @"https://discord.com/api/oauth2/authorize?permissions=67069671894864&scope=applications.commands%20bot&client_id=ENTER_BOT_CLIENT_ID";

        public Secrets Secrets = new Secrets();

        public int LogFileSaveIntervalInMinutes = 15;
        public int MaxLogFileSizeInMB = 256;

        public int ServerWatchdogIntervalInMinutes = 2;

        public TimeSpan DailyRestartTime = new TimeSpan(5, 0, 0);
        public int DailyRestartDelayInSeconds = 120;

        public int TwitchForceReconnectDelayInMinutes = 10;
        public int LivestreamMonitorIntervalInMinutes = 1;

        public int WebCalSyncIntervalInMinutes = 15;

        public int UpdateUserIntervalInMinutes = 120;
        public int DownloadUserDataIntervalInMinutes = 5;

        public int AudioCommandTimeOutInSeconds = 60;
        public int VoiceChannelNoUsersExitInSeconds = 30;

        public int WebClientTimeoutInSeconds = 10;
    }

    [Serializable]
    public class Secrets
    {
        public string DiscordBotLoginToken = ""; // Bot Login Token

        public string TwitchBotUsername = ""; // Enter the account (username) of your twitch bot account!
        public string TwitchBotClientId = ""; // Client ID for your twitch bot
        public string TwitchBotClientSecret = ""; // Client Secret of your twitch application (Do not share with anyone!)

        public string TwitchBotOAuth = "";
        public string TwitchBotOAuthRefresh = "";
        public string TwitchBotOAuthRedirectURL = "http://localhost:8080/";

        public string FlickrApiKey = "";
    }
}
