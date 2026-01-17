using System;
using System.Xml.Serialization;

namespace GeistDesWaldes.Configuration;

[Serializable]
public class ServerConfiguration
{
    public DiscordSettingsEntry DiscordSettings;

    public GeneralSettingsEntry GeneralSettings;
    [XmlIgnore] public ulong GuildId;
    public TwitchSettingsEntry TwitchSettings;
    public UserSettingsEntry UserSettings;

    public ServerConfiguration()
    {
        GeneralSettings = new GeneralSettingsEntry();
        DiscordSettings = new DiscordSettingsEntry();
        TwitchSettings = new TwitchSettingsEntry();
        UserSettings = new UserSettingsEntry();
    }

    public ServerConfiguration(ulong guildId) : this()
    {
        GuildId = guildId;
    }
}

[Serializable]
public class GeneralSettingsEntry
{
    public string CommandPageLink = ""; // If existing, enter the website-link listing all of your commands
    public char CommandPrefix = '!';

    public string CultureInfoIdentifier = "de-DE";

    public int MaxPollsPerChannel = 3;
    public char PollVotePrefix = '?';
}

[Serializable]
public class DiscordSettingsEntry
{
    public ulong DefaultBotTextChannel = 0; // Enter the ChannelId for the Bot's Default Text Channel!
    public ulong DefaultBotVoiceChannel = 0; // Enter the ChannelId for the Bot's Default Voice Channel!
}

[Serializable]
public class TwitchSettingsEntry
{
    public int ActiveChatterWindowInMinutes = 5;
    public int LivestreamActionIntervalMinMessages = 15;

    public int LivestreamActionIntervalMinMinutes = 15;

    public int LivestreamOneShotWindowInMinutes = 10;
    public string TwitchChannelName = ""; // Enter the twitch account (channel), the bot is going to join!

    public int TwitchFollowAlertCooldownInMinutes = 360;

    public ulong TwitchMessageChannelId = GenerateTwitchMessageChannelId();
    public int TwitchPointsBonusForActiveChatters = 1;

    public int TwitchPointsPerMonitorInterval = 1;

    public string WebCalLink = ""; // Twitch stream plan webcal link
    public ulong WebCalSyncDiscordChannelId = 0; // Id of text channel the stream schedule gets synced to 
    public string WebCalTagClose = "{/cal}";
    public string WebCalTagOpen = "{cal}"; // Pattern used to recognize where the webcal texts should be placed;


    public static ulong GenerateTwitchMessageChannelId()
    {
        return (ulong)(DateTime.Now - new DateTime(2021, 09, 11, 0, 1, 1)).TotalMilliseconds;
    }
}

[Serializable]
public class UserSettingsEntry
{
    public double UserCooldownInSeconds = 1.5f;
}