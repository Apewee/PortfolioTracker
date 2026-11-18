using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace PortfolioTracker.UI;

public readonly record struct ChartPoint(DateTime Date, decimal Value);

public sealed class ChartSeries
{
    public ChartSeries(string name, IReadOnlyList<ChartPoint> points, Color color, float width)
    {
        Name = name;
        Points = points;
        Color = color;
        Width = width;
    }

    public string Name { get; }
    public IReadOnlyList<ChartPoint> Points { get; }
    public Color Color { get; }
    public float Width { get; }
}

public sealed class ChartControl : Control
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private IReadOnlyList<ChartSeries> _series = Array.Empty<ChartSeries>();

    public ChartControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 8f);
    }

    public void SetSeries(IReadOnlyList<ChartSeries> series)
    {
        _series = series;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.White);

        var allPoints = _series.SelectMany(s => s.Points).ToList();
        if (allPoints.Count == 0)
        {
            DrawCentered(graphics, "Keine Daten");
            return;
        }

        var left = 70;
        var right = 14;
        var top = 16;
        var bottom = 32;
        var plot = new Rectangle(left, top, Math.Max(20, ClientSize.Width - left - right), Math.Max(20, ClientSize.Height - top - bottom));

        var minValue = allPoints.Min(p => p.Value);
        var maxValue = allPoints.Max(p => p.Value);
        if (maxValue - minValue < 0.0001m)
        {
            maxValue += 1;
            minValue -= 1;
        }

        var padding = (maxValue - minValue) * 0.06m;
        minValue -= padding;
        maxValue += padding;

        var minDate = allPoints.Min(p => p.Date);
        var maxDate = allPoints.Max(p => p.Date);
        var days = Math.Max(1.0, (maxDate - minDate).TotalDays);

        DrawHorizontalGrid(graphics, plot, minValue, maxValue);
        DrawVerticalGrid(graphics, plot, minDate, maxDate, days);

        foreach (var series in _series)
        {
            DrawSeries(graphics, series, plot, minValue, maxValue, minDate, days);
        }

        if (_series.Count > 1)
        {
            DrawLegend(graphics, plot);
        }
    }

    private void DrawHorizontalGrid(Graphics graphics, Rectangle plot, decimal minValue, decimal maxValue)
    {
        for (var i = 0; i <= 4; i++)
        {
            var value = minValue + (maxValue - minValue) * i / 4;
            var y = ToY(value, plot, minValue, maxValue);
            graphics.DrawLine(Pens.LightGray, plot.Left, y, plot.Right, y);
            var text = FormatValue(value);
            var size = TextRenderer.MeasureText(text, Font);
            TextRenderer.DrawText(graphics, text, Font, new Point(plot.Left - size.Width - 6, y - size.Height / 2), Color.Black);
        }
    }

    private void DrawVerticalGrid(Graphics graphics, Rectangle plot, DateTime minDate, DateTime maxDate, double days)
    {
        var ticks = Math.Clamp(plot.Width / 130, 2, 7);
        var format = days > 730 ? "MMM yy" : days > 90 ? "MM.yy" : "dd.MM.";

        for (var i = 0; i < ticks; i++)
        {
            var fraction = (double)i / (ticks - 1);
            var x = plot.Left + (int)(plot.Width * fraction);
            graphics.DrawLine(Pens.LightGray, x, plot.Top, x, plot.Bottom);

            var date = minDate.AddDays(days * fraction);
            var text = date.ToString(format, German);
            var size = TextRenderer.MeasureText(text, Font);
            TextRenderer.DrawText(graphics, text, Font, new Point(x - size.Width / 2, plot.Bottom + 8), Color.Black);
        }
    }

    private void DrawSeries(Graphics graphics, ChartSeries series, Rectangle plot, decimal minValue, decimal maxValue, DateTime minDate, double days)
    {
        if (series.Points.Count < 2)
        {
            return;
        }

        using var pen = new Pen(series.Color, series.Width);
        var points = series.Points
            .Select(p => new PointF(ToX(p.Date, plot, minDate, days), ToY(p.Value, plot, minValue, maxValue)))
            .ToArray();
        graphics.DrawLines(pen, points);
    }

    private void DrawLegend(Graphics graphics, Rectangle plot)
    {
        var lineLength = 18;
        var x = plot.Right - 8;
        var y = plot.Top + 6;

        foreach (var series in _series)
        {
            var text = series.Name;
            var size = TextRenderer.MeasureText(text, Font);
            var textX = x - size.Width;
            TextRenderer.DrawText(graphics, text, Font, new Point(textX, y), Color.Black);
            using var pen = new Pen(series.Color, series.Width);
            graphics.DrawLine(pen, textX - lineLength - 4, y + size.Height / 2, textX - 4, y + size.Height / 2);
            y += size.Height + 6;
        }
    }

    private void DrawCentered(Graphics graphics, string text)
    {
        TextRenderer.DrawText(
            graphics,
            text,
            Font,
            ClientRectangle,
            Color.Black,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static int ToY(decimal value, Rectangle plot, decimal minValue, decimal maxValue)
    {
        var fraction = (double)(value - minValue) / (double)(maxValue - minValue);
        return plot.Bottom - (int)(plot.Height * fraction);
    }

    private static int ToX(DateTime date, Rectangle plot, DateTime minDate, double days)
    {
        var fraction = (date - minDate).TotalDays / days;
        return plot.Left + (int)(plot.Width * fraction);
    }

    private static string FormatValue(decimal value)
    {
        var format = Math.Abs(value) >= 1000 ? "#,##0" : "#,##0.00";
        return value.ToString(format, German);
    }
}
