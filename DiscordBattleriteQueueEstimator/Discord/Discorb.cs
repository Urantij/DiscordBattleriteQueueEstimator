using System.Net;
using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Discord.Sock;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.WebSockets;
using NetCord.Rest;

namespace DiscordBattleriteQueueEstimator.Discord;

public class Discorb : IHostedService
{
    private const ulong BattleriteAppId = 352378924317147156;

    private readonly ILogger<Discorb> _logger;
    private readonly GatewayClient _client;
    private readonly ILogger<GatewayClient> _clientLogger;

    public Application? Application { get; private set; }

    public event Action<UserInfo>? UserRped;

    public Discorb(IOptions<DiscorbConfig> options, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<Discorb>();

        WebProxy? proxy = null;
        if (options.Value.Proxy != null)
        {
            _logger.LogInformation("Юзаем прокси");
            proxy = new WebProxy(options.Value.Proxy);
        }

        RestClientConfiguration? restClientConfiguration = null;
        if (proxy != null)
        {
            restClientConfiguration = new RestClientConfiguration()
            {
                RequestHandler = new RestRequestHandler(new HttpClientHandler()
                {
                    Proxy = proxy
                })
            };
        }

        IWebSocketConnectionProvider? webSocketConnectionProvider = null;
        if (proxy != null)
        {
            webSocketConnectionProvider = new MyWebSocketConnectionProvider(proxy);
        }

        _client = new GatewayClient(new BotToken(options.Value.Token), new GatewayClientConfiguration()
        {
            RestClientConfiguration = restClientConfiguration,
            WebSocketConnectionProvider = webSocketConnectionProvider,
            Intents = GatewayIntents.GuildUsers | GatewayIntents.GuildPresences | GatewayIntents.AllNonPrivileged,
        });
        _client.Log += ClientOnLog;
        _clientLogger = loggerFactory.CreateLogger<GatewayClient>();

        _client.PresenceUpdate += ClientOnPresenceUpdate;
        _client.GuildCreate += ClientOnGuildCreate;
    }

    private ValueTask ClientOnLog(LogMessage arg)
    {
        LogLevel level = arg.Severity switch
        {
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Info => LogLevel.Debug,
            _ => LogLevel.Warning
        };

        _clientLogger.Log(level, "{message} ({description})", arg.Message, arg.Description);

        return ValueTask.CompletedTask;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _client.StartAsync(new PresenceProperties(UserStatusType.Online).WithActivities(new[]
        {
            new UserActivityProperties("Watching your game", UserActivityType.Playing)
        }), cancellationToken);

        Application = await _client.Rest.GetCurrentApplicationAsync(cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _client.CloseAsync(cancellationToken: cancellationToken);
    }

    public GatewayClient GetClient()
        => _client;

    private ValueTask ClientOnPresenceUpdate(Presence arg)
    {
        // срабатывает и когда уходит в офлаин.
        
        (RpInfo? rpInfo, bool fakeRp) = Do(arg);
        
        UserRped?.Invoke(new UserInfo(arg.User.Id, fakeRp, rpInfo, DateTimeOffset.UtcNow));
        
        return ValueTask.CompletedTask;
    }

    private ValueTask ClientOnGuildCreate(GuildCreateEventArgs arg)
    {
        if (arg.Guild?.Presences is null)
            return ValueTask.CompletedTask;
        
        foreach (KeyValuePair<ulong, Presence> member in arg.Guild.Presences)
        {
            (RpInfo? rpInfo, bool fakeRp) = Do(member.Value);
    
            UserRped?.Invoke(new UserInfo(member.Value.User.Id, fakeRp, rpInfo, DateTimeOffset.UtcNow));
        }
    
        return ValueTask.CompletedTask;
    }
    
    private (RpInfo? rpInfo, bool fakeRp) Do(Presence presence)
    {
        UserActivity? activity = presence.Activities.FirstOrDefault(IsBrRp);
        
        if (activity == null)
            return (null, false);
    
        bool fakeRp;
        RpInfo? rpInfo;
    
        // Это фейковый рп, как если бы его не было.
        // Приходит, если в данный момент у игрока клиент свёрнут.
        // Возможно, стоит добавить проверку на наличие State. Он вроде всегда есть.
        if (activity.Name == "battlerite" || activity.Timestamps?.StartTime != null)
        {
            fakeRp = true;
            rpInfo = null;
        }
        else
        {
            // string.Empty нужен, чтобы модель в базе могла быть определена как отсутствующая
            // если все проперти нулл, то тада непонятно короче, был рп вообще или нет.
            fakeRp = false;
            rpInfo = new RpInfo(activity.Assets?.SmallText, activity.Details,
                activity.State ?? string.Empty, activity.Party?.CurrentSize);
        }
    
        return (rpInfo, fakeRp);
    }
    
    private bool IsBrRp(UserActivity activity)
    {
        return activity.ApplicationId == BattleriteAppId;
    }
}