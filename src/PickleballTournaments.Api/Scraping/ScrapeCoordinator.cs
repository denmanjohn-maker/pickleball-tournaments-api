using System.Threading.Channels;
using PickleballTournaments.Api.Domain;

namespace PickleballTournaments.Api.Scraping;

/// <summary>Singleton gate ensuring only one scrape runs at a time, with a queue for on-demand triggers.</summary>
public class ScrapeCoordinator
{
    private readonly Lock _gate = new();
    private readonly Channel<(ScrapeTrigger Trigger, TaskCompletionSource<int> RunIdSource)> _channel =
        Channel.CreateUnbounded<(ScrapeTrigger, TaskCompletionSource<int>)>();
    private bool _isRunning;

    public bool IsRunning { get { lock (_gate) return _isRunning; } }

    public ChannelReader<(ScrapeTrigger Trigger, TaskCompletionSource<int> RunIdSource)> Reader => _channel.Reader;

    /// <summary>Returns false (without queuing) if a scrape is already in progress.</summary>
    public bool TryEnqueue(ScrapeTrigger trigger, out TaskCompletionSource<int> runIdSource)
    {
        runIdSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_isRunning)
                return false;
            _isRunning = true;
        }

        _channel.Writer.TryWrite((trigger, runIdSource));
        return true;
    }

    public void MarkCompleted()
    {
        lock (_gate) _isRunning = false;
    }
}
