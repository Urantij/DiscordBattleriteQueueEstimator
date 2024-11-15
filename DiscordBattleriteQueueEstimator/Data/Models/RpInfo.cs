namespace DiscordBattleriteQueueEstimator.Data.Models;

public record RpInfo(string? Hero, string? Details, string State, int? PartySize)
{
    /// <summary>
    /// "In Menus" "In Queue: Casual" "In 3v3 Arena | 0-0 | Bo0"
    /// </summary>
    public string? Details { get; init; } = Details;

    /// <summary>
    /// "In Group" "Not in a group"
    /// </summary>
    public string State { get; init; } = State;
}
