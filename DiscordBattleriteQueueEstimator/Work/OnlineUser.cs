using DiscordBattleriteQueueEstimator.Shared.Data.Models;

namespace DiscordBattleriteQueueEstimator.Work;

public class OnlineUser(DbUser user, RpInfo? info)
{
    private const int CacheLimit = 3;

    public DbUser User { get; } = user;

    // для наблюдения за матчами мне нужно иметь *несколько* последних статусов. Очень жаль.
    // Если лежит нулл, это фейкрп. не хочу делать подкласс для хранения этой чуши
    private readonly List<RpInfo?> _lastInfos = new() { info };

    public void Add(RpInfo? info)
    {
        _lastInfos.Add(info);

        if (_lastInfos.Count > CacheLimit)
            _lastInfos.RemoveAt(0);
    }

    public RpInfo? GetLast()
    {
        // кстати не уверен возможно ли такое. но я не хочу думать TODO
        if (_lastInfos.Count == 0)
            return null;

        return _lastInfos[^1];
    }

    public bool IsLastRpFake()
    {
        return GetLast() == null;
    }

    public List<RpInfo?> GetList()
    {
        return _lastInfos;
    }
}