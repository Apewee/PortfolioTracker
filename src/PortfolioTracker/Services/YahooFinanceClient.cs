using System.Text.Json;

namespace PortfolioTracker.Services;

public sealed class YahooFinanceClient
{
    private const string BaseUrl = "https://query1.finance.yahoo.com/v8/finance/chart/";

    private readonly HttpClient _http;

    public YahooFinanceClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    public async Task<PriceHistory> GetHistoryAsync(string symbol, string range)
    {
        var url = BaseUrl + Uri.EscapeDataString(symbol) + "?range=" + range + "&interval=1d";
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url);
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("Zeitüberschreitung bei der Verbindung zu Yahoo Finance.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var hint = (int)response.StatusCode == 429 ? " (zu viele Anfragen, bitte spaeter noch einmal)" : "";
            throw new InvalidOperationException("Yahoo Finance antwortet mit HTTP " + (int)response.StatusCode + hint + ".");
        }

        var json = await response.Content.ReadAsStringAsync();
        try
        {
            return Parse(symbol, json);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Die Antwort von Yahoo Finance ist nicht lesbar.", exception);
        }
    }

    private static PriceHistory Parse(string symbol, string json)
    {
        using var document = JsonDocument.Parse(json);
        var chart = document.RootElement.GetProperty("chart");

        if (chart.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
        {
            throw new InvalidOperationException(error.GetString()!);
        }

        var result = chart.GetProperty("result");
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Yahoo Finance kennt das Symbol nicht.");
        }

        var item = result[0];
        var meta = item.GetProperty("meta");
        var name = meta.TryGetProperty("shortName", out var shortName) && shortName.ValueKind == JsonValueKind.String
            ? shortName.GetString()!
            : symbol;
        var currency = meta.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.String
            ? cur.GetString()!
            : "";

        var timestamps = item.TryGetProperty("timestamp", out var stamp) && stamp.ValueKind == JsonValueKind.Array
            ? stamp
            : throw new InvalidOperationException("Yahoo Finance liefert keine Kurse für dieses Symbol.");
        var indicators = item.GetProperty("indicators");
        var quote = indicators.GetProperty("quote")[0];
        var closes = quote.GetProperty("close");

        var points = new List<PricePoint>();
        for (var i = 0; i < timestamps.GetArrayLength(); i++)
        {
            var close = closes[i];
            if (close.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            points.Add(new PricePoint
            {
                Date = DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).UtcDateTime.Date,
                Close = close.GetDecimal()
            });
        }

        if (points.Count == 0)
        {
            throw new InvalidOperationException("Yahoo Finance liefert keine Kurse für dieses Symbol.");
        }

        return new PriceHistory(symbol, name, currency, points);
    }
}
