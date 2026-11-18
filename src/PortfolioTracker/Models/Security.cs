namespace PortfolioTracker.Models;

public sealed class Security
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public SecurityType Type { get; set; }
    public DateTime BuyDate { get; set; }
    public decimal BuyPrice { get; set; }
    public decimal Quantity { get; set; }
}
