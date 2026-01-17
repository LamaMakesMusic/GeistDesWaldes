using System.Threading.Tasks;
using Discord;

namespace GeistDesWaldes;

public abstract class BaseHandler : IServerModule
{
    protected readonly Server Server;


    protected BaseHandler(Server server)
    {
        Server = server;
    }

    public virtual int Priority { get; } = 0;

    public virtual Task OnServerStartUp()
    {
        Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(OnServerStartUp), GetType().Name));
        return Task.CompletedTask;
    }

    public virtual Task OnServerShutdown()
    {
        Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(OnServerShutdown), GetType().Name));
        return Task.CompletedTask;
    }

    public virtual Task OnCheckIntegrity()
    {
        Server.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(OnCheckIntegrity), GetType().Name));
        return Task.CompletedTask;
    }
}