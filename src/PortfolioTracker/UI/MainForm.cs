using System.Globalization;
using PortfolioTracker.Models;
using PortfolioTracker.Services;

namespace PortfolioTracker.UI;

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
        _typeInput = CreateComboBox(new[] { "Aktie", "ETF", "Obligation", "Fonds" });
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
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        inputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 8));
        inputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        inputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        inputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

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
        AddLabel(inputPanel, "Zeitraum", 3, 2);
        inputPanel.Controls.Add(_rangeInput, 4, 2);
        inputPanel.Controls.Add(_loadButton, 5, 2);

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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
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
        _cache.Load();
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

        if (failed.Count == 0)
        {
            SetStatus("Kurse aktualisiert am " + _cache.LastUpdate.ToString("dd.MM.yyyy HH:mm", German) + ".");
        }
        else if (loaded == 0)
        {
            SetStatus("Keine Kurse geladen. Es werden die gespeicherten Daten vom "
                + _cache.LastUpdate.ToString("dd.MM.yyyy HH:mm", German) + " verwendet. Grund: " + failed[0]);
        }
        else
        {
            SetStatus("Kurse teilweise geladen. Fehlgeschlagen: " + string.Join(", ", failed));
        }

        RefreshView();
    }

    private void RefreshView()
    {
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
                    security.Symbol, security.Name, TypeName(security.Type),
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
                TypeName(security.Type),
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
        error = null;
        var symbol = _symbolInput.Text.Trim();
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

        if (_portfolio.Securities.Any(s => s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Das Symbol ist bereits im Portfolio enthalten.";
            return null;
        }

        var typeLabel = _typeInput.SelectedItem?.ToString() ?? "";
        if (typeLabel.Length == 0)
        {
            error = "Bitte eine Art auswählen.";
            return null;
        }

        if (!DateTime.TryParseExact(
                _buyDateInput.Text.Trim(),
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

        if (!TryParseDecimal(_buyPriceInput.Text, out var buyPrice))
        {
            error = "Der Kaufpreis ist keine gültige Zahl.";
            return null;
        }

        if (buyPrice <= 0)
        {
            error = "Der Kaufpreis muss größer als 0 sein.";
            return null;
        }

        if (!TryParseDecimal(_quantityInput.Text, out var quantity))
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
            Name = _nameInput.Text.Trim(),
            Type = ToSecurityType(typeLabel),
            BuyDate = buyDate,
            BuyPrice = buyPrice,
            Quantity = quantity
        };
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
        _status.Text = text;
    }

    private static bool TryParseDecimal(string text, out decimal value)
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

    private static string TypeName(SecurityType type)
    {
        return type switch
        {
            SecurityType.Stock => "Aktie",
            SecurityType.Etf => "ETF",
            SecurityType.Bond => "Obligation",
            SecurityType.Fund => "Fonds",
            _ => type.ToString()
        };
    }

    private static SecurityType ToSecurityType(string label)
    {
        return label switch
        {
            "Aktie" => SecurityType.Stock,
            "ETF" => SecurityType.Etf,
            "Obligation" => SecurityType.Bond,
            _ => SecurityType.Fund
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
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(4),
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
