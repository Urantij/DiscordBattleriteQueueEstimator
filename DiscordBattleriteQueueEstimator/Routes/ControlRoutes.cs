using DiscordBattleriteQueueEstimator.Data;
using DiscordBattleriteQueueEstimator.Work;

namespace DiscordBattleriteQueueEstimator.Routes;

public class HealthWebResult(int observeMatches, int users)
{
    public int ObserveMatches { get; } = observeMatches;
    public int Users { get; } = users;
}

public static class ControlRoutes
{
    public static async Task<IResult> GenerateMatchesAsync(Database database)
    {
        int generated = 0;

        await StatusReprocessor.DoAsync(database);

        return TypedResults.Ok(generated);
    }

    public static async Task<IResult> GetHealthAsync(MatchObserver matchObserver, Worker worker)
    {
        int m = await matchObserver.GetMatchesCountAsync();

        int u = worker.CountUsers();

        HealthWebResult result = new(m, u);

        return TypedResults.Ok(result);
    }
}