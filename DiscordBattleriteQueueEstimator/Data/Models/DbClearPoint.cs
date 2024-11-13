using System.ComponentModel.DataAnnotations;

namespace DiscordBattleriteQueueEstimator.Data;

public class DbClearPoint
{
    [Key] public int Id { get; set; }

    public int StatusId { get; set; }

    public DateTimeOffset Date { get; set; }
}