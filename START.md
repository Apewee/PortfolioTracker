# So benutzt du den Portfolio Tracker

## 1. Programm starten

`PortfolioTracker.exe` aus dem Release herunterladen und doppelklicken.
Es muss nichts installiert werden, auch nicht .NET.

## 2. Wertpapiere eingeben

Oben jedes Mal diese Angaben eintragen und auf **Hinzufügen** klicken:

| Feld | Beispiel |
|---|---|
| Symbol | `AAPL` |
| Name | `Apple` (darf leer bleiben) |
| Typ | `Aktie` |
| Kaufdatum | `2024-01-15` |
| Kaufpreis | `150,50` |
| Stückzahl | `10` |

Mindestens drei Titel hinzufügen. Erst dann macht die Darstellung Sinn.

Mögliche Symbole:

| Art | Symbol |
|---|---|
| Aktie USA | `AAPL`, `MSFT`, `AMZN` |
| Aktie Deutschland | `SAP.DE`, `SIE.DE` |
| ETF | `SXR8.DE`, `SPY` |
| Index | `^GDAXI`, `^GSPC` |

Eine Liste mit vielen Symbolen gibt es bei Yahoo Finance.

## 3. Kurse laden

Auf **Kurse laden** klicken. Das Programm holt die Kurse von Yahoo Finance
und schreibt sie in eine Datei. Der Namen des Titels wird dabei ergänzt,
wenn das Feld Name leer war.

## 4. Zeitraum wählen

Oben rechts den Zeitraum aus der Liste wählen: 1 Monat bis Max.
Das Diagramm und die Spalte „Rendite Zeitraum" folgen dieser Auswahl.

## 5. Titel entfernen

Zeile in der Tabelle anklicken und **Entfernen** drücken.

## Was zeigt die Tabelle

| Spalte | Bedeutung |
|---|---|
| Kurs | letzter Schlusskurs |
| Wert | Stückzahl mal Kurs |
| Rendite seit Kauf | Vergleich mit dem Kaufpreis |
| Rendite Zeitraum | Kurs zu Beginn und Ende des gewählten Zeitraums |

Darüber steht die Zeile mit Gesamtwert, Kaufwert und beiden Gesamtrenditen.

## Wenn das Internet weg ist

Das Programm bricht nicht ab. Es nutzt dann die zuletzt gespeicherten Kurse
und sagt unten in der Statusleiste, von wann diese sind.

## Liegen die Dateien

| Datei | Inhalt |
|---|---|
| `%LOCALAPPDATA%\PortfolioTracker\portfolio.json` | deine Eingaben |
| `%LOCALAPPDATA%\PortfolioTracker\prices.json` | die Kursverläufe |

Der genaue Ordner steht beim Start unten in der Statusleiste.

## Häufige Meldungen

| Meldung | Ursache |
|---|---|
| Bitte ein Symbol eingeben. | Das Symbol-Feld ist leer. |
| Das Kaufdatum ist ungültig. | Falsches Format, richtig ist `JJJJ-MM-TT`. |
| Der Kaufpreis ist keine gültige Zahl. | Buchstaben oder Sonderzeichen im Preis. |
| Yahoo Finance kennt das Symbol nicht. | Symbol falsch geschrieben oder nicht bei Yahoo gelistet. |
| HTTP 429 | Yahoo blockiert kurz, später noch einmal probieren. |

## Rendite ohne Dividenden

Die Berechnung nutzt die Schlusskurse. Dividenden und Zinsen sind also nicht
drin. Für ETFs und Aktien ist die echte Gesamtrendite damit etwas höher.
