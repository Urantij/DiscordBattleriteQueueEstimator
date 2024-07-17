namespace DiscordBattleriteQueueEstimator.Discord;

public class CommandInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public ulong? Id { get; set; }
}