using System.Text.Json;

namespace PortfolioTracker.Services;

public sealed class PriceCache
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly Dictionary<string, PriceHistory> _entries = new(StringComparer.OrdinalIgnoreCase);

    public DateTime LastUpdate { get; private set; }

    public List<PricePoint> Get(string symbol)
    {
        return _entries.TryGetValue(symbol, out var history) ? history.Points : new List<PricePoint>();
    }

    public string GetName(string symbol)
    {
        return _entries.TryGetValue(symbol, out var history) ? history.Name : "";
    }

    public void Update(PriceHistory history)
    {
        _entries[history.Symbol] = history;
        LastUpdate = DateTime.Now;
    }

    public void Load()
    {
        _entries.Clear();
        if (!File.Exists(AppPaths.PriceFile))
        {
            return;
        }

        var raw = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(AppPaths.PriceFile), Options);
        if (raw == null)
        {
            return;
        }

        LastUpdate = raw.LastUpdate;
        foreach (var entry in raw.Symbols)
        {
            _entries[entry.Key] = entry.Value;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        var raw = new CacheFile { LastUpdate = LastUpdate, Symbols = _entries };
        File.WriteAllText(AppPaths.PriceFile, JsonSerializer.Serialize(raw, Options));
    }

    private sealed class CacheFile
    {
        public DateTime LastUpdate { get; set; }
        public Dictionary<string, PriceHistory> Symbols { get; set; } = new();
    }
}
