using Microsoft.Extensions.DependencyInjection;

namespace GeistDesWaldes;

public class ServiceReference<TService>
{
    public TService Value
    {
        get
        {
            field ??= _server.Services.GetService<TService>();
            return field;
        }
    }

    private readonly Server _server;
    
    
    public ServiceReference(Server server)
    {
        _server = server;
    }
}