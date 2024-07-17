using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace DiscordBattleriteQueueEstimator.Discord.Commands;

public class InfoCommand : BaseCommand
{
    private readonly Work.Worker _worker;

    public InfoCommand(Work.Worker worker, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _worker = worker;
    }

    public override Task DoAsync(DiscordClient sender, InteractionCreateEventArgs args)
    {
        int total = _worker.CountUsers();
        int inLeagueQueue = _worker.CountUsers(u => u.LastInfo.Details == "In Queue: League");
        int inCasualsQueue = _worker.CountUsers(u => u.LastInfo.Details == "In Queue: Casual");

        return SendReplyAsync(args,
            $"Total: {total}\nLeague Queue: {inLeagueQueue}\nCasual Queue: {inCasualsQueue}");
    }
}