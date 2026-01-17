using GeistDesWaldes.Configuration;
using GeistDesWaldes.UserCommands;
using System;
using System.Threading.Tasks;
using GeistDesWaldes.Misc;

namespace GeistDesWaldes.TwitchIntegration.IntervalActions
{
    public class TwitchLivestreamIntervalActionWatchdog
    {
        private readonly Server _server;
        private readonly ServerConfiguration _serverConfiguration;

        private DateTime _lastMessageSentAt = DateTime.Now;
        private int _messageCount;
        private bool _running;

        private int _actionIndex = -1;


        public TwitchLivestreamIntervalActionWatchdog(Server server, ServerConfiguration config)
        {
            _server = server;
            _serverConfiguration = config;
        }


        public void Start()
        {
            _lastMessageSentAt = DateTime.Now;
            _messageCount = 0;
            _running = true;
        }

        public void Stop()
        {
            _running = false;
        }

        public void OnChatMessageReceived()
        {
            if (!_running)
                return;

            _messageCount++;

            TriggerAction().SafeAsync<TwitchLivestreamIntervalActionWatchdog>(_server.GuildId);
        }

        private async Task TriggerAction()
        {
            try
            {
                if (_messageCount < _serverConfiguration.TwitchSettings.LivestreamActionIntervalMinMessages)
                    return;

                if ((DateTime.Now - _lastMessageSentAt).TotalMinutes < _serverConfiguration.TwitchSettings.LivestreamActionIntervalMinMinutes)
                    return;

                _lastMessageSentAt = DateTime.Now;
                _messageCount = 0;
                _actionIndex = _server.GetModule<TwitchLivestreamIntervalActionHandler>().GetNextAction(_actionIndex, out CustomCommand command);

                if (command != null)
                {
                    TwitchIntegrationHandler.LogToMain($"[{_serverConfiguration.TwitchSettings.TwitchChannelName}] {nameof(TriggerAction)}", command.Name, Discord.LogSeverity.Info);
                    await command.Execute(null);
                }
            }
            catch (Exception ex)
            {
                TwitchIntegrationHandler.LogToMain($"[{_serverConfiguration.TwitchSettings.TwitchChannelName}] {nameof(TriggerAction)}", string.Empty, Discord.LogSeverity.Error, exception: ex);
            }
        }
    }
}
