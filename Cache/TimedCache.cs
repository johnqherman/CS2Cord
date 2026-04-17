namespace CS2Cord.Cache;

public class TimedCache<TValue>
{
    private readonly record struct Entry(TValue Value, DateTime ExpiresAt);

    private readonly Dictionary<string, Entry> _store          = new();
    private readonly Queue<string>             _insertionOrder = new();
    private readonly int                       _maxCapacity;
    private readonly object                    _lock           = new();

    public TimedCache(int maxCapacity = 512) => _maxCapacity = maxCapacity;

    public bool TryGet(string key, out TValue value)
    {
        lock (_lock)
        {
            if (_store.TryGetValue(key, out var entry) && DateTime.UtcNow < entry.ExpiresAt)
            {
                value = entry.Value;
                return true;
            }
            if (_store.ContainsKey(key))
                _store.Remove(key);
            value = default!;
            return false;
        }
    }

    public void Set(string key, TValue value, TimeSpan ttl)
    {
        lock (_lock)
        {
            if (!_store.ContainsKey(key))
            {
                if (_store.Count >= _maxCapacity)
                    Evict();
                _insertionOrder.Enqueue(key);
            }
            _store[key] = new Entry(value, DateTime.UtcNow + ttl);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _store.Clear();
            _insertionOrder.Clear();
        }
    }

    private void Evict()
    {
        while (_insertionOrder.TryDequeue(out var oldest))
        {
            if (_store.Remove(oldest))
                return;
        }
    }
}
