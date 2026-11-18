// PortfolioTracker, das komplette Programm in einer Datei.
// Zum Bauen diese Datei in ein Windows-Forms-Projekt mit net10.0-windows legen,
// oder einfach src/PortfolioTracker/PortfolioTracker.csproj bauen.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PortfolioTracker;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowError(e.ExceptionObject as Exception);
        Application.Run(new MainForm());
    }

    private static void ShowError(Exception? exception)
    {
        MessageBox.Show(
            "Unerwarteter Fehler: " + (exception?.Message ?? "unbekannt"),
            "Portfolio Tracker",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}

public enum SecurityType
{
    Stock,
    Etf,
    Bond,
    Fund
}

public sealed class Security
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public SecurityType Type { get; set; }
    public DateTime BuyDate { get; set; }
    public decimal BuyPrice { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class Portfolio
{
    public List<Security> Securities { get; set; } = new();
}

public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PortfolioTracker");

    public static string PortfolioFile => Path.Combine(DataDirectory, "portfolio.json");
    public static string PriceFile => Path.Combine(DataDirectory, "prices.json");
}

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

public static class InputValidator
{
    public const string StockLabel = "Aktie";
    public const string EtfLabel = "ETF";
    public const string BondLabel = "Obligation";
    public const string FundLabel = "Fonds";

    public static readonly string[] TypeLabels = { StockLabel, EtfLabel, BondLabel, FundLabel };

    public static Security? Validate(
        string symbol,
        string name,
        string typeLabel,
        string buyDateText,
        string buyPriceText,
        string quantityText,
        IReadOnlyList<Security> existing,
        out string? error)
    {
        error = null;
        symbol = symbol.Trim();

        if (symbol.Length == 0)
        {
            error = "Bitte ein Symbol eingeben.";
            return null;
        }

        if (symbol.Any(char.IsWhiteSpace))
        {
            error = "Das Symbol darf keine Leerzeichen enthalten.";
            return null;
        }

        if (!symbol.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '^'))
        {
            error = "Das Symbol enthält ungültige Zeichen (erlaubt sind Buchstaben, Ziffern, Punkt, Bindestrich).";
            return null;
        }

        if (existing.Any(s => s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Das Symbol ist bereits im Portfolio enthalten.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(typeLabel))
        {
            error = "Bitte eine Art auswählen.";
            return null;
        }

        if (!DateTime.TryParseExact(
                buyDateText.Trim(),
                new[] { "yyyy-MM-dd", "dd.MM.yyyy" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var buyDate))
        {
            error = "Das Kaufdatum ist ungültig. Erwartet wird JJJJ-MM-TT oder TT.MM.JJJJ.";
            return null;
        }

        if (buyDate > DateTime.Now.Date)
        {
            error = "Das Kaufdatum darf nicht in der Zukunft liegen.";
            return null;
        }

        if (!TryParseDecimal(buyPriceText, out var buyPrice))
        {
            error = "Der Kaufpreis ist keine gültige Zahl.";
            return null;
        }

        if (buyPrice <= 0)
        {
            error = "Der Kaufpreis muss größer als 0 sein.";
            return null;
        }

        if (!TryParseDecimal(quantityText, out var quantity))
        {
            error = "Die Stückzahl ist keine gültige Zahl.";
            return null;
        }

        if (quantity <= 0)
        {
            error = "Die Stückzahl muss größer als 0 sein.";
            return null;
        }

        return new Security
        {
            Symbol = symbol.ToUpperInvariant(),
            Name = name.Trim(),
            Type = ToSecurityType(typeLabel),
            BuyDate = buyDate,
            BuyPrice = buyPrice,
            Quantity = quantity
        };
    }

    public static bool TryParseDecimal(string text, out decimal value)
    {
        text = text.Trim().Replace(" ", "");
        if (text.Contains(',') && text.Contains('.'))
        {
            text = text.Replace(".", "").Replace(",", ".");
        }
        else if (text.Contains(','))
        {
            text = text.Replace(",", ".");
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    public static string TypeName(SecurityType type)
    {
        return type switch
        {
            SecurityType.Stock => StockLabel,
            SecurityType.Etf => EtfLabel,
            SecurityType.Bond => BondLabel,
            SecurityType.Fund => FundLabel,
            _ => type.ToString()
        };
    }

    private static SecurityType ToSecurityType(string label)
    {
        return label switch
        {
            StockLabel => SecurityType.Stock,
            EtfLabel => SecurityType.Etf,
            BondLabel => SecurityType.Bond,
            _ => SecurityType.Fund
        };
    }
}

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

public sealed class MainForm : Form
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private readonly PortfolioStore _store = new();
    private readonly PriceCache _cache = new();
    private readonly YahooFinanceClient _yahoo = new();
    private Portfolio _portfolio = new();

    private readonly TextBox _symbolInput;
    private readonly TextBox _nameInput;
    private readonly ComboBox _typeInput;
    private readonly TextBox _buyDateInput;
    private readonly TextBox _buyPriceInput;
    private readonly TextBox _quantityInput;
    private readonly Button _addButton;
    private readonly Button _removeButton;
    private readonly CheckBox _showSingle;
    private readonly ComboBox _rangeInput;
    private readonly Button _loadButton;
    private readonly DataGridView _grid;
    private readonly ChartControl _chart;
    private readonly Label _summary;
    private readonly Label _status;

    public MainForm()
    {
        Text = "Portfolio Tracker";
        MinimumSize = new Size(1120, 720);
        ClientSize = new Size(1160, 780);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        _symbolInput = CreateInput("Symbol, z.B. AAPL");
        _nameInput = CreateInput("Name (optional)");
        _typeInput = CreateComboBox(InputValidator.TypeLabels);
        _buyDateInput = CreateInput("JJJJ-MM-TT");
        _buyPriceInput = CreateInput("Kaufpreis");
        _quantityInput = CreateInput("Stückzahl");
        _addButton = CreateButton("Hinzufügen");
        _removeButton = CreateButton("Entfernen");
        _showSingle = new CheckBox
        {
            Text = "Einzelne Titel anzeigen",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ForeColor = Color.Black,
            Margin = new Padding(4),
        };
        _rangeInput = CreateComboBox(new[] { "1 Monat", "3 Monate", "6 Monate", "1 Jahr", "2 Jahre", "5 Jahre", "Max" });
        _rangeInput.SelectedItem = "1 Jahr";
        _loadButton = CreateButton("Kurse laden");
        _grid = CreateGrid();
        _chart = new ChartControl { Dock = DockStyle.Fill };
        _summary = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0),
            ForeColor = Color.Black,
        };
        _status = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0),
            ForeColor = Color.Black,
        };

        var inputPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 3,
            BackColor = Color.White,
            Padding = new Padding(6),
        };
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        inputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        inputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        inputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        AddLabel(inputPanel, "Symbol", 0, 0);
        AddLabel(inputPanel, "Name", 1, 0);
        AddLabel(inputPanel, "Typ", 2, 0);
        AddLabel(inputPanel, "Kaufdatum", 3, 0);
        AddLabel(inputPanel, "Kaufpreis", 4, 0);
        AddLabel(inputPanel, "Stückzahl", 5, 0);

        inputPanel.Controls.Add(_symbolInput, 0, 1);
        inputPanel.Controls.Add(_nameInput, 1, 1);
        inputPanel.Controls.Add(_typeInput, 2, 1);
        inputPanel.Controls.Add(_buyDateInput, 3, 1);
        inputPanel.Controls.Add(_buyPriceInput, 4, 1);
        inputPanel.Controls.Add(_quantityInput, 5, 1);

        inputPanel.Controls.Add(_addButton, 0, 2);
        inputPanel.Controls.Add(_removeButton, 1, 2);
        inputPanel.Controls.Add(_showSingle, 2, 2);
        inputPanel.SetColumnSpan(_showSingle, 2);
        AddLabel(inputPanel, "Zeitraum", 4, 2);
        inputPanel.Controls.Add(_rangeInput, 5, 2);
        inputPanel.Controls.Add(_loadButton, 6, 2);

        var chartPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White,
        };
        chartPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        chartPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        chartPanel.Controls.Add(_summary, 0, 0);
        chartPanel.Controls.Add(_chart, 0, 1);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.White,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.Controls.Add(inputPanel, 0, 0);
        root.Controls.Add(chartPanel, 0, 1);
        root.Controls.Add(_grid, 0, 2);
        root.Controls.Add(_status, 0, 3);
        Controls.Add(root);

        _addButton.Click += AddButton_Click;
        _removeButton.Click += RemoveButton_Click;
        _loadButton.Click += async (_, _) => await LoadPricesAsync();
        _rangeInput.SelectedIndexChanged += async (_, _) => await LoadPricesAsync();
        _showSingle.CheckedChanged += (_, _) => RefreshView();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            _cache.Load();
        }
        catch (Exception exception)
        {
            SetStatus("Fehler: Kursdatei konnte nicht gelesen werden: " + exception.Message);
        }

        try
        {
            _portfolio = _store.Load();
        }
        catch (Exception exception)
        {
            _portfolio = new Portfolio();
            SetStatus("Fehler: Portfolio-Datei konnte nicht gelesen werden: " + exception.Message);
        }

        SetStatus("Datenordner: " + AppPaths.DataDirectory);
        RefreshView();
        _ = LoadPricesAsync();
    }

    private void AddButton_Click(object? sender, EventArgs e)
    {
        var security = ValidateInput(out var error);
        if (error != null)
        {
            SetStatus("Fehler: " + error);
            return;
        }

        _portfolio.Securities.Add(security!);
        SavePortfolio();
        ClearInputs();
        SetStatus(security!.Symbol + " hinzugefügt.");
        RefreshView();
        _ = LoadPricesAsync();
    }

    private void RemoveButton_Click(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0)
        {
            SetStatus("Fehler: Bitte zuerst eine Zeile in der Tabelle auswählen.");
            return;
        }

        var symbol = _grid.SelectedRows[0].Cells["Symbol"].Value?.ToString() ?? "";
        var security = _portfolio.Securities.FirstOrDefault(s => s.Symbol == symbol);
        if (security == null)
        {
            return;
        }

        _portfolio.Securities.Remove(security);
        SavePortfolio();
        SetStatus(symbol + " entfernt.");
        RefreshView();
    }

    private async Task LoadPricesAsync()
    {
        if (_portfolio.Securities.Count == 0)
        {
            SetStatus("Keine Wertpapiere im Portfolio. Bitte zuerst Titel hinzufügen.");
            return;
        }

        var range = ToYahooRange(_rangeInput.SelectedItem?.ToString() ?? "1 Jahr");
        SetStatus("Kurse werden geladen...");

        var loaded = 0;
        var failed = new List<string>();
        foreach (var security in _portfolio.Securities)
        {
            try
            {
                var history = await _yahoo.GetHistoryAsync(security.Symbol, range);
                if (string.IsNullOrWhiteSpace(security.Name))
                {
                    security.Name = history.Name;
                }

                _cache.Update(history);
                loaded++;
            }
            catch (Exception exception)
            {
                failed.Add(security.Symbol + " (" + exception.Message + ")");
            }

            await Task.Delay(150);
        }

        if (loaded > 0)
        {
            _cache.Save();
            SavePortfolio();
        }

        if (IsDisposed)
        {
            return;
        }

        if (failed.Count == 0)
        {
            SetStatus("Kurse aktualisiert am " + _cache.LastUpdate.ToString("dd.MM.yyyy HH:mm", German) + ".");
        }
        else if (loaded == 0)
        {
            var cached = _cache.LastUpdate == default
                ? "Keine Kurse geladen und noch keine Kurse gespeichert."
                : "Keine Kurse geladen. Es werden die gespeicherten Daten vom "
                    + _cache.LastUpdate.ToString("dd.MM.yyyy HH:mm", German) + " verwendet.";
            SetStatus(cached + " Grund: " + failed[0]);
        }
        else
        {
            SetStatus("Kurse teilweise geladen. Fehlgeschlagen: " + string.Join(", ", failed));
        }

        RefreshView();
    }

    private void RefreshView()
    {
        if (IsDisposed)
        {
            return;
        }

        var rangeLabel = _rangeInput.SelectedItem?.ToString() ?? "1 Jahr";
        var start = PeriodStart(rangeLabel);

        _grid.Rows.Clear();
        var totalCost = 0m;
        var totalValue = 0m;
        var singleSeries = new List<ChartSeries>();

        foreach (var security in _portfolio.Securities)
        {
            var points = _cache.Get(security.Symbol);
            if (points.Count == 0)
            {
                _grid.Rows.Add(
                    security.Symbol, security.Name, InputValidator.TypeName(security.Type),
                    security.BuyDate.ToString("dd.MM.yyyy", German),
                    FormatNumber(security.BuyPrice), FormatQuantity(security.Quantity), "-", "-", "-", "-");
                continue;
            }

            var last = points[^1];
            var value = last.Close * security.Quantity;
            totalCost += security.BuyPrice * security.Quantity;
            totalValue += value;

            var inRange = ReturnCalculator.PointsInRange(points, start);
            var returnPeriod = inRange.Count >= 2
                ? ReturnCalculator.Percent(inRange[0].Close, inRange[^1].Close)
                : (decimal?)null;

            _grid.Rows.Add(
                security.Symbol,
                string.IsNullOrWhiteSpace(security.Name) ? _cache.GetName(security.Symbol) : security.Name,
                InputValidator.TypeName(security.Type),
                security.BuyDate.ToString("dd.MM.yyyy", German),
                FormatNumber(security.BuyPrice),
                FormatQuantity(security.Quantity),
                FormatNumber(last.Close),
                FormatNumber(value),
                FormatPercent(ReturnCalculator.Percent(security.BuyPrice, last.Close)),
                FormatPercent(returnPeriod));

            if (_showSingle.Checked)
            {
                singleSeries.Add(new ChartSeries(
                    security.Symbol,
                    inRange.Where(p => p.Close * security.Quantity > 0)
                        .Select(p => new ChartPoint(p.Date, p.Close * security.Quantity))
                        .ToList(),
                    Color.Gray,
                    1f));
            }
        }

        var history = ReturnCalculator.BuildHistory(_portfolio, _cache, start);
        var series = new List<ChartSeries> { new("Portfolio", history.Select(p => new ChartPoint(p.Date, p.Value)).ToList(), Color.Black, 2f) };
        series.AddRange(singleSeries);
        _chart.SetSeries(series);

        var returnPeriodTotal = history.Count >= 2
            ? ReturnCalculator.Percent(history[0].Value, history[^1].Value)
            : (decimal?)null;

        _summary.Text = "Gesamtwert: " + FormatMoney(totalValue)
            + "   Kaufwert: " + FormatMoney(totalCost)
            + "   Rendite seit Kauf: " + FormatPercent(ReturnCalculator.Percent(totalCost, totalValue))
            + "   Rendite im Zeitraum: " + FormatPercent(returnPeriodTotal);
    }

    private Security? ValidateInput(out string? error)
    {
        return InputValidator.Validate(
            _symbolInput.Text,
            _nameInput.Text,
            _typeInput.SelectedItem?.ToString() ?? "",
            _buyDateInput.Text,
            _buyPriceInput.Text,
            _quantityInput.Text,
            _portfolio.Securities,
            out error);
    }

    private void SavePortfolio()
    {
        try
        {
            _store.Save(_portfolio);
        }
        catch (Exception exception)
        {
            SetStatus("Fehler: Portfolio konnte nicht gespeichert werden: " + exception.Message);
        }
    }

    private void ClearInputs()
    {
        _symbolInput.Clear();
        _nameInput.Clear();
        _buyDateInput.Clear();
        _buyPriceInput.Clear();
        _quantityInput.Clear();
        _symbolInput.Focus();
    }

    private void SetStatus(string text)
    {
        if (!IsDisposed)
        {
            _status.Text = text;
        }
    }

    private static DateTime PeriodStart(string rangeLabel)
    {
        var now = DateTime.Now.Date;
        return rangeLabel switch
        {
            "1 Monat" => now.AddMonths(-1),
            "3 Monate" => now.AddMonths(-3),
            "6 Monate" => now.AddMonths(-6),
            "1 Jahr" => now.AddYears(-1),
            "2 Jahre" => now.AddYears(-2),
            "5 Jahre" => now.AddYears(-5),
            _ => DateTime.MinValue
        };
    }

    private static string ToYahooRange(string rangeLabel)
    {
        return rangeLabel switch
        {
            "1 Monat" => "1mo",
            "3 Monate" => "3mo",
            "6 Monate" => "6mo",
            "1 Jahr" => "1y",
            "2 Jahre" => "2y",
            "5 Jahre" => "5y",
            _ => "max"
        };
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("#,##0.00", German) + " EUR";
    }

    private static string FormatNumber(decimal value)
    {
        return value.ToString("#,##0.00", German);
    }

    private static string FormatQuantity(decimal value)
    {
        return value.ToString("#,##0.##", German);
    }

    private static string FormatPercent(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("P2", German) : "-";
    }

    private static TextBox CreateInput(string placeholder)
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            BorderStyle = BorderStyle.FixedSingle,
            ForeColor = Color.Black,
            BackColor = Color.White,
        };
    }

    private static ComboBox CreateComboBox(string[] items)
    {
        var combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.Black,
        };
        combo.Items.AddRange(items);
        combo.SelectedIndex = 0;
        return combo;
    }

    private static Button CreateButton(string text)
    {
        return new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.Black,
        };
    }

    private static void AddLabel(TableLayoutPanel panel, string text, int column, int row)
    {
        panel.Controls.Add(new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 0, 4, 0),
            ForeColor = Color.Black,
        }, column, row);
    }

    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            GridColor = Color.LightGray,
        };
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.Black,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.Black,
            SelectionBackColor = Color.LightGray,
            SelectionForeColor = Color.Black,
        };
        grid.ColumnHeadersHeight = 26;

        AddColumn(grid, "Symbol", "Symbol", 90, false);
        AddColumn(grid, "Name", "Name", 170, false);
        AddColumn(grid, "Type", "Typ", 75, false);
        AddColumn(grid, "BuyDate", "Kaufdatum", 90, false);
        AddColumn(grid, "BuyPrice", "Kaufpreis", 90, true);
        AddColumn(grid, "Quantity", "Stück", 65, true);
        AddColumn(grid, "Price", "Kurs", 90, true);
        AddColumn(grid, "Value", "Wert", 100, true);
        AddColumn(grid, "ReturnBuy", "Rendite seit Kauf", 115, true);
        AddColumn(grid, "ReturnPeriod", "Rendite Zeitraum", 115, true);

        return grid;
    }

    private static void AddColumn(DataGridView grid, string name, string header, int weight, bool rightAligned)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            FillWeight = weight,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = rightAligned ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft,
            },
        });
    }
}
