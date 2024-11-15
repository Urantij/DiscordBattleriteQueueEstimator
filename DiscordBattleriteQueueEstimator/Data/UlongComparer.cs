using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DiscordBattleriteQueueEstimator.Data;

public class UlongComparer : ValueComparer<ulong>
{
    public UlongComparer()
        : base((arg1, arg2) => arg1 == arg2,
            value => value.GetHashCode(), value => value)
    {
    }
}