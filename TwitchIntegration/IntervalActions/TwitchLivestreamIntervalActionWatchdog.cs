using System;
using System.Threading.Tasks;
using GeistDesWaldes.Configuration;
using GeistDesWaldes.Misc;
using GeistDesWaldes.UserCommands;

namespace GeistDesWaldes.TwitchIntegration.IntervalActions;

public class TwitchLivestreamIntervalActionWatchdog
{
    private readonly Server _server;
    private readonly ServerConfiguration _serverConfiguration;

    private int _actionIndex = -1;

    private DateTime _lastMessageSentAt = DateTime.Now;
    private int _messageCount;
    private bool _running;


    public TwitchLivestreamIntervalActionWatchdog(Server server, ServerConfiguration config)
    {
        _server = server;
        _serverConfiguration = config;
    }


    public void Start()
    {
        if (_running)
            return;
        
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
        
        IncreaseMessageCount().SafeAsync<TwitchLivestreamIntervalActionWatchdog>(_server.LogHandler);
    }

    private async Task IncreaseMessageCount()
    {
        _messageCount++;

        if (_messageCount < _serverConfiguration.TwitchSettings.LivestreamActionIntervalMinMessages)
            return;

        if ((DateTime.Now - _lastMessageSentAt).TotalMinutes < _serverConfiguration.TwitchSettings.LivestreamActionIntervalMinMinutes)
            return;

        await ExecuteNextAction();
    }

    public async Task ExecuteNextAction()
    {
        _lastMessageSentAt = DateTime.Now;
        _messageCount = 0;

        _actionIndex = _server.GetModule<TwitchLivestreamIntervalActionHandler>().GetNextAction(_actionIndex, out CustomCommand command);

        if (command != null)
        {
            TwitchIntegrationHandler.LogToMain($"[{_serverConfiguration.TwitchSettings.TwitchChannelName}] {nameof(ExecuteNextAction)}", command.Name);
            await command.Execute(null);
        }
    }
}