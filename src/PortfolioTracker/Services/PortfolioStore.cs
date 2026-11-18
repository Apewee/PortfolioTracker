using System.Text.Json;
using System.Text.Json.Serialization;
using PortfolioTracker.Models;

namespace PortfolioTracker.Services;

public sealed class PortfolioStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public Portfolio Load()
    {
        if (!File.Exists(AppPaths.PortfolioFile))
        {
            return new Portfolio();
        }

        var raw = JsonSerializer.Deserialize<Portfolio>(File.ReadAllText(AppPaths.PortfolioFile), Options);
        return raw ?? new Portfolio();
    }

    public void Save(Portfolio portfolio)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(AppPaths.PortfolioFile, JsonSerializer.Serialize(portfolio, Options));
    }
}
