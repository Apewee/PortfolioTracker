# Portfolio Tracker

Kleines C#-Programm (Windows Forms, .NET 10) zur Verwaltung eines Wertpapierportfolios.
Es holt Kursdaten von Yahoo Finance, speichert sie lokal, berechnet Renditen und zeigt den
Wertverlauf des Portfolios als Diagramm an.

## Start

`PortfolioTracker.exe` aus dem Release starten (liegt im Release unter Assets zum Download).
Die .exe ist eine einzige Datei und benötigt keine Installation von .NET.

## Bedienung

1. Mindestens drei Wertpapiere eingeben: Symbol, Name (optional), Typ, Kaufdatum, Kaufpreis, Stückzahl.
2. Auf "Hinzufügen" klicken. Die Eingaben werden geprüft, das Portfolio wird gespeichert.
3. "Kurse laden" ruft die Kurse ab. Der Zeitraum (1 Monat bis Max) ist wählbar.
4. Die Tabelle zeigt Kurs, Wert und Renditen, das Diagramm den Wertverlauf des Portfolios.

Erlaubte Symbole sind die Yahoo-Finance-Symbole, z.B. `AAPL`, `MSFT`, `SXR8.DE`, `^GDAXI`.
Bei Fehler im Internet werden die zuletzt gespeicherten Kurse verwendet und ein Hinweis unten angezeigt.

## Daten

| Datei | Inhalt |
|---|---|
| `portfolio.json` | Eingebene Wertpapiere |
| `prices.json` | Gespeicherte Kursverläufe |

Ort: `%LOCALAPPDATA%\PortfolioTracker\` (im Programm unten in der Statusleiste angezeigt).

## Kursdatenquelle

Yahoo Finance, Endpunkt `https://query1.finance.yahoo.com/v8/finance/chart/{Symbol}`.

Begründung:
- Kostenlos und ohne API-Schlüssel nutzbar, damit das Programm ohne Registrierung läuft.
- Deckt Aktien, ETFs, Fonds und Indizes weltweit ab, inklusive deutscher Börsen (Suffix `.DE`).
- Liefert tägliche Schlusskurse als JSON mit Zeitstempel.
- Die Antworten werden lokal gespeichert, damit das Programm offline mit den letzten Kursen weiterläuft.

Einschränkung: Dividenden und Zinsen sind im Schlusskurs nicht enthalten, die Rendite ist daher
eine reine Kursrendite. Anleihen müssen über ein bei Yahoo Finance gelistetes Symbol erfasst werden.

## Struktur

```
src/PortfolioTracker/
  Program.cs                  Einstiegspunkt, Fehlerbehandlung
  Models/                     Security, Portfolio, SecurityType
  Services/
    AppPaths.cs               Pfade der Datendateien
    PortfolioStore.cs         Speichern/Laden des Portfolios als JSON
    YahooFinanceClient.cs     Abruf und Auswertung der Kursdaten
    PriceCache.cs             Speichern/Laden der Kursverläufe als JSON
    ReturnCalculator.cs       Rendite- und Verlaufsberechnung
  UI/
    MainForm.cs               Eingaben, Tabelle, Zusammenfassung
    ChartControl.cs           Diagramm (schwarz auf weiß)
```

## Bauen

```bash
dotnet publish src/PortfolioTracker/PortfolioTracker.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableWindowsTargeting=true \
  -o publish
```

Ergebnis: `publish/PortfolioTracker.exe`.
