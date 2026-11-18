namespace PortfolioTracker.Models;

public sealed class Portfolio
{
    public List<Security> Securities { get; set; } = new();
}
