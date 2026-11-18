namespace PortfolioTracker.Services;

public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PortfolioTracker");

    public static string PortfolioFile => Path.Combine(DataDirectory, "portfolio.json");
    public static string PriceFile => Path.Combine(DataDirectory, "prices.json");
}
