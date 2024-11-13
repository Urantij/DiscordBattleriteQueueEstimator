using System.Net;
using NetCord.Gateway.WebSockets;

namespace DiscordBattleriteQueueEstimator.Discord.Sock;

public class MyWebSocketConnectionProvider : IWebSocketConnectionProvider
{
    private readonly IWebProxy? _proxy;

    public MyWebSocketConnectionProvider(IWebProxy? proxy)
    {
        _proxy = proxy;
    }

    public IWebSocketConnection CreateConnection()
    {
        return new MyWebSocketConnection(_proxy);
    }
}