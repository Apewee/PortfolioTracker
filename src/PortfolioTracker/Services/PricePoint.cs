namespace PortfolioTracker.Services;

public sealed class PricePoint
{
    public DateTime Date { get; set; }
    public decimal Close { get; set; }
}

public sealed class PriceHistory
{
    public PriceHistory(string symbol, string name, string currency, List<PricePoint> points)
    {
        Symbol = symbol;
        Name = name;
        Currency = currency;
        Points = points;
    }

    public string Symbol { get; set; }
    public string Name { get; set; }
    public string Currency { get; set; }
    public List<PricePoint> Points { get; set; }
}
