namespace DiscordBattleriteQueueEstimator.Utils;

public class SoloLooper
{
    private readonly Lock _loopLocker = new();
    private readonly Queue<Func<Task>> _queue = new();
    private bool _looping = false;
    private bool _working = true;

    private readonly ILogger _logger;

    public SoloLooper(ILogger logger)
    {
        _logger = logger;
    }

    public void Add(Func<Task> obj)
    {
        lock (_loopLocker)
        {
            _queue.Enqueue(obj);
            if (_looping)
                return;

            _looping = true;
            Task.Run(async () =>
            {
                try
                {
                    await LoopAsync();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "loop сломався");
                }
            });
        }
    }

    public void Stop()
    {
        _working = false;
    }

    private async Task LoopAsync()
    {
        while (_working)
        {
            Func<Task>? obj;
            lock (_loopLocker)
            {
                if (!_queue.TryDequeue(out obj))
                {
                    _looping = false;
                    return;
                }
            }

            await obj();
        }
    }
}