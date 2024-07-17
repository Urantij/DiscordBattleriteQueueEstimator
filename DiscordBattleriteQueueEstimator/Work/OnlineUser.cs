using DiscordBattleriteQueueEstimator.Data.Models;

namespace DiscordBattleriteQueueEstimator.Work;

public class OnlineUser(DbUser user, bool lastRpFake, RpInfo lastInfo)
{
    public DbUser User { get; } = user;
    public bool LastRpFake { get; set; } = lastRpFake;
    public RpInfo LastInfo { get; set; } = lastInfo;
}