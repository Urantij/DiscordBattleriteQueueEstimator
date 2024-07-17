using DiscordBattleriteQueueEstimator.Data.Models;

namespace DiscordBattleriteQueueEstimator.Discord;

public class UserInfo(ulong id, bool fakeRp, RpInfo? info, DateTimeOffset date)
{
    public ulong Id { get; } = id;
    public bool FakeRp { get; } = fakeRp;
    public RpInfo? Info { get; } = info;
    
    public DateTimeOffset Date { get; } = date;
}