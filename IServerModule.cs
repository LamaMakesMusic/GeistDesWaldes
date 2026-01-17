using System.Threading.Tasks;

namespace GeistDesWaldes;

public interface IServerModule
{
    /// <summary>
    ///     Higher Number = Lower Priority
    /// </summary>
    public int Priority { get; }

    public Task OnServerStartUp();
    public Task OnServerShutdown();
    public Task OnCheckIntegrity();
}