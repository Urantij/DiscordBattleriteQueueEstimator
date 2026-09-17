namespace DiscordBattleriteQueueEstimator.Routes;

public readonly struct DiscordId
{
    public ulong Value { get; }

    public DiscordId(ulong value) => Value = value;

    public static bool TryParse(string? value, IFormatProvider? provider, out DiscordId result)
    {
        if (ulong.TryParse(value, out var parsed))
        {
            result = new DiscordId(parsed);
            return true;
        }

        result = default;
        return false;
    }

    // public static implicit operator ulong(DiscordId id) => id.Value;
}