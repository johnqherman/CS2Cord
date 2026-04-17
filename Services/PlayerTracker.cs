namespace CS2Cord.Services;

public class PlayerTracker
{
    private readonly bool[]    _connected = new bool[65];
    private readonly string?[] _names     = new string?[65];
    private readonly string?[] _steamIds  = new string?[65];

    public int HumanPlayerCount { get; private set; }

    public void Add(int slot, string name, string? steamId)
    {
        _connected[slot] = true;
        _names[slot]     = name;
        _steamIds[slot]  = steamId;
        HumanPlayerCount++;
    }

    public (string name, string? steamId) Remove(int slot, string fallbackName)
    {
        var name    = _names[slot] ?? fallbackName;
        var steamId = _steamIds[slot];

        _connected[slot] = false;
        _names[slot]     = null;
        _steamIds[slot]  = null;

        if (HumanPlayerCount > 0) HumanPlayerCount--;

        return (name, steamId);
    }

    public bool IsConnected(int slot) => _connected[slot];
}
