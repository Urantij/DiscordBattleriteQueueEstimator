using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordBattleriteQueueEstimator.Data;

public class DbUserStatus
{
    [Key]
    public int Id { get; set; }
    
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }
    public DbUser User { get; set; }

    /// <summary>
    /// Если игра свёрнута, настоящтеи RP события не приходят.
    /// Приходит мусор, мол, он просто играет в battlerite столько то времени.
    /// Это типа fake rp
    /// </summary>
    public bool FakeRp { get; set; }
    /// <summary>
    /// Если нулл, рп нет. Если <see cref="FakeRp"/> false, то юзер офлаин.
    /// </summary>
    public RpInfo? RpInfo { get; set; }
    
    public DateTimeOffset Date { get; set; }
}