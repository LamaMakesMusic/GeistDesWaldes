using Discord;
using System;

namespace GeistDesWaldes
{
    public abstract class BaseHandler
    {
        public readonly Server _Server;

        public BaseHandler(Server server)
        {
            _Server = server;
            _Server.OnServerStart += OnServerStart;
            _Server.OnCheckIntegrity += OnCheckIntegrity;
            _Server.OnServerShutdown += OnServerShutdown;
        }

        internal virtual void OnServerStart(object source, EventArgs e) 
        {
            _Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(OnServerStart), GetType().Name));
        }

        internal virtual void OnServerShutdown(object source, EventArgs e)
        {
            _Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(OnServerShutdown), GetType().Name));
        }

        internal virtual void OnCheckIntegrity(object source, EventArgs e)
        {
            _Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(OnCheckIntegrity), GetType().Name));
        }
    }
}
