using Discord;
using GeistDesWaldes.TwitchIntegration;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TwitchLibUser = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace GeistDesWaldes.Configuration
{
    public class RuntimeConfiguration
    {
        public readonly ulong GuildId;
        public RuntimeConfiguration(ulong guildId)
        {
            GuildId = guildId;
        }

        private string _guildName;
        public string GuildName
        {
            get
            {
                if (_guildName == null)
                    _guildName = Launcher.Instance.DiscordClient.Guilds.FirstOrDefault(g => g.Id == GuildId)?.Name ?? null;

                return _guildName;
            }
        }

        private IMessageChannel _defaultBotTextChannel;
        public IMessageChannel DefaultBotTextChannel {
            get {
                if (_defaultBotTextChannel == null)
                    _defaultBotTextChannel = Task.Run(() => Launcher.Instance.GetChannel<IMessageChannel>(ConfigurationHandler.Configs[GuildId].DiscordSettings.DefaultBotTextChannel)).GetAwaiter().GetResult();

                return _defaultBotTextChannel;
            }
        }

        private IVoiceChannel _defaultBotVoiceChannel;
        public IVoiceChannel DefaultBotVoiceChannel {
            get {
                if (_defaultBotVoiceChannel == null)
                    _defaultBotVoiceChannel = Task.Run(() => Launcher.Instance.GetChannel<IVoiceChannel>(ConfigurationHandler.Configs[GuildId].DiscordSettings.DefaultBotVoiceChannel)).GetAwaiter().GetResult();

                return _defaultBotVoiceChannel;
            }
        }

        private ITextChannel _webCalSyncDiscordChannel;
        public ITextChannel WebCalSyncDiscordChannel {
            get {
                if (_webCalSyncDiscordChannel == null)
                    _webCalSyncDiscordChannel = Task.Run(() => Launcher.Instance.GetChannel<ITextChannel>(ConfigurationHandler.Configs[GuildId].TwitchSettings.WebCalSyncDiscordChannelId)).GetAwaiter().GetResult();

                return _webCalSyncDiscordChannel;
            }
        }

        private TwitchLibUser _channelOwner;
        public TwitchLibUser ChannelOwner {
            get {
                if (_channelOwner == null)
                {
                    string channelName = ConfigurationHandler.Configs[GuildId].TwitchSettings.TwitchChannelName;

                    if (string.IsNullOrWhiteSpace(channelName))
                        return null;

                    var response = Task.Run(() => TwitchIntegrationHandler.ValidatedApiCall(Launcher.Instance.TwitchIntegrationHandler.Api.Helix.Users.GetUsersAsync(logins: new List<string>() { channelName }))).GetAwaiter().GetResult();

                    if (response.Users.Length > 0)
                        _channelOwner = response.Users[0];
                    else
                        Launcher.Instance.Servers[GuildId].LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(ChannelOwner), $"Could not find Twitch User '{channelName}'"));
                }

                return _channelOwner;
            }
        }

        private CultureInfo _cultureInfo;
        public CultureInfo CultureInfo {
            get {
                if (_cultureInfo == null)
                {
                    _cultureInfo = CultureInfo.GetCultureInfo(ConfigurationHandler.Configs[GuildId].GeneralSettings.CultureInfoIdentifier);
                }

                return _cultureInfo;
            }
        }
    }
}
