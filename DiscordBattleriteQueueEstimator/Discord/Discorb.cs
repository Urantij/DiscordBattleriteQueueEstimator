using DiscordBattleriteQueueEstimator.Data.Models;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Options;

namespace DiscordBattleriteQueueEstimator.Discord;

public class Discorb : IHostedService
{
    private const ulong BattleriteAppId = 352378924317147156;

    private readonly DiscordClient _client;

    public event Action<UserInfo>? UserRped;

    public Discorb(IOptions<DiscorbConfig> options, ILoggerFactory loggerFactory)
    {
        _client = new DiscordClient(new DiscordConfiguration()
        {
            Token = options.Value.Token,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.GuildMembers | DiscordIntents.GuildPresences | DiscordIntents.AllUnprivileged,
            LoggerFactory = loggerFactory
        });

        _client.PresenceUpdated += DiscordClientOnPresenceUpdated;
        _client.GuildAvailable += DiscordClientOnGuildAvailable;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _client.ConnectAsync(activity: new DiscordActivity("Watching your game"));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _client.DisconnectAsync();
    }

    public DiscordClient GetClient()
        => _client;

    private Task DiscordClientOnPresenceUpdated(DiscordClient sender, PresenceUpdateEventArgs args)
    {
        // срабатывает и когда уходит в офлаин.

        // Иногда приходит такой прикол. При этом Activity, UserAfter, UserBefore и PresenceAfter могут быть не нулл.
        // Предположу, что это прикол библиотеки, когда информация о рп юзера приходит раньше инфы о самом юзере.
        if (args.User == null)
        {
            ulong? id = args.UserAfter?.Id ?? args.UserBefore?.Id;
            if (id != null)
            {
                UserRped?.Invoke(new UserInfo(id.Value, false, null, DateTimeOffset.UtcNow));
            }

            return Task.CompletedTask;
        }

        (RpInfo? rpInfo, bool fakeRp) = Do(args.User);

        UserRped?.Invoke(new UserInfo(args.User.Id, fakeRp, rpInfo, DateTimeOffset.UtcNow));

        return Task.CompletedTask;
    }

    private Task DiscordClientOnGuildAvailable(DiscordClient sender, GuildCreateEventArgs args)
    {
        foreach (DiscordMember member in args.Guild.Members.Values)
        {
            (RpInfo? rpInfo, bool fakeRp) = Do(member);

            UserRped?.Invoke(new UserInfo(member.Id, fakeRp, rpInfo, DateTimeOffset.UtcNow));
        }

        return Task.CompletedTask;
    }

    private (RpInfo? rpInfo, bool fakeRp) Do(DiscordUser user)
    {
        DiscordActivity? activity =
            user.Presence?.Activities?.FirstOrDefault(a =>
                a.RichPresence?.Application != null && IsBrRp(a.RichPresence));

        if (activity == null)
            return (null, false);

        bool fakeRp;
        RpInfo? rpInfo;

        // Это фейковый рп, как если бы его не было.
        // Приходит, если в данный момент у игрока клиент свёрнут.
        // Возможно, стоит добавить проверку на наличие State. Он вроде всегда есть.
        if (activity.Name == "battlerite" || activity.RichPresence.StartTimestamp != null)
        {
            fakeRp = true;
            rpInfo = null;
        }
        else
        {
            fakeRp = false;
            rpInfo = new RpInfo(activity.RichPresence.SmallImageText, activity.RichPresence.Details,
                activity.RichPresence.State, (int?)activity.RichPresence.CurrentPartySize);
        }

        return (rpInfo, fakeRp);
    }

    private bool IsBrRp(DiscordRichPresence argRichPresence)
    {
        return argRichPresence.Application.Id == BattleriteAppId;
    }
}