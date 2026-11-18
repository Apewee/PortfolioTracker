using PortfolioTracker.Models;

namespace PortfolioTracker.Services;

public sealed class PortfolioPoint
{
    public PortfolioPoint(DateTime date, decimal value)
    {
        Date = date;
        Value = value;
    }

    public DateTime Date { get; set; }
    public decimal Value { get; set; }
}

public static class ReturnCalculator
{
    public static decimal Percent(decimal start, decimal end)
    {
        return start == 0 ? 0 : (end - start) / start;
    }

    public static decimal? PriceOnOrBefore(IReadOnlyList<PricePoint> points, DateTime date)
    {
        decimal? result = null;
        foreach (var point in points)
        {
            if (point.Date <= date)
            {
                result = point.Close;
            }
            else
            {
                break;
            }
        }

        return result;
    }

    public static List<PricePoint> PointsInRange(IReadOnlyList<PricePoint> points, DateTime start)
    {
        return points.Where(p => p.Date >= start).ToList();
    }

    public static List<PortfolioPoint> BuildHistory(Portfolio portfolio, PriceCache cache, DateTime start)
    {
        var dates = new SortedSet<DateTime>();
        foreach (var security in portfolio.Securities)
        {
            foreach (var point in cache.Get(security.Symbol))
            {
                if (point.Date >= start)
                {
                    dates.Add(point.Date);
                }
            }
        }

        var history = new List<PortfolioPoint>();
        foreach (var date in dates)
        {
            decimal total = 0;
            var used = false;
            foreach (var security in portfolio.Securities)
            {
                var price = PriceOnOrBefore(cache.Get(security.Symbol), date);
                if (price == null)
                {
                    continue;
                }

                total += price.Value * security.Quantity;
                used = true;
            }

            if (used)
            {
                history.Add(new PortfolioPoint(date, total));
            }
        }

        return history;
    }
}
