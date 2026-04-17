using Microsoft.Extensions.Logging;

namespace CSSCord.Services;

public class DiscordPollingService
{
    private string? _lastMessageId;
    private bool    _seeded;

    private readonly HashSet<string> _processedIds    = new();
    private readonly Queue<string>   _idFifo          = new();
    private const    int             MaxProcessedIds  = 512;

    private       int      _failedRequests;
    private       DateTime _nextRetryAt    = DateTime.MinValue;
    private const double   MaxRetrySeconds = 60.0;

    private readonly SemaphoreSlim _pollLock    = new(1, 1);
    private const    int           MaxBatchSize = 5;

    private readonly float   _pollingIntervalSeconds;
    private readonly ILogger _logger;

    public DiscordPollingService(float pollingIntervalSeconds, ILogger logger)
    {
        _pollingIntervalSeconds = pollingIntervalSeconds;
        _logger = logger;
    }

    public async Task PollAsync(DiscordApiService api, Action<string, string, string> onMessage)
    {
        if (!await _pollLock.WaitAsync(0))
            return;

        try
        {
            if (DateTime.UtcNow < _nextRetryAt)
                return;

            List<DiscordMessage> messages;
            try
            {
                messages = await api.FetchMessagesAsync(_lastMessageId, MaxBatchSize);
                _failedRequests = 0;
            }
            catch (Exception ex)
            {
                _failedRequests++;
                var delay = Math.Min(
                    Math.Pow(2, _failedRequests - 1) * _pollingIntervalSeconds,
                    MaxRetrySeconds);
                _nextRetryAt = DateTime.UtcNow + TimeSpan.FromSeconds(delay);
                _logger.LogWarning(ex, "Discord poll failed (attempt {N}, retry in {Delay:F1}s)",
                    _failedRequests, delay);
                return;
            }

            if (messages.Count == 0) return;

            messages.Reverse();

            if (!_seeded)
            {
                _lastMessageId = messages[^1].Id;
                _seeded = true;
                return;
            }

            int dispatched = 0;
            foreach (var msg in messages)
            {
                if (dispatched >= MaxBatchSize) break;
                if (msg.Author.Bot == true) continue;
                if (IsAlreadyProcessed(msg.Id)) continue;

                RecordId(msg.Id);
                _lastMessageId = msg.Id;

                var displayName = msg.Author.GlobalName ?? msg.Author.Username ?? "Unknown";
                onMessage(msg.Author.Id, displayName, msg.Content);
                dispatched++;
            }

            if (messages.Count > 0)
                _lastMessageId = messages[^1].Id;
        }
        finally
        {
            _pollLock.Release();
        }
    }

    private bool IsAlreadyProcessed(string id) => _processedIds.Contains(id);

    private void RecordId(string id)
    {
        if (_processedIds.Count >= MaxProcessedIds)
        {
            if (_idFifo.TryDequeue(out var oldest))
                _processedIds.Remove(oldest);
        }
        _processedIds.Add(id);
        _idFifo.Enqueue(id);
    }
}
