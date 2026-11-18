using System.Globalization;
using PortfolioTracker.Models;

namespace PortfolioTracker.Services;

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
