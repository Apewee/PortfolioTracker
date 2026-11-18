# So benutzt du den Portfolio Tracker

Die Datei PortfolioTracker.exe liegt im Release. Sie herunterladen und doppelklicken, es muss nichts installiert werden.

Beim Start ist das Fenster zunächst leer. Oben trägst du einen Titel ein. Zentral ist dabei das Symbol. Damit ist der Schlüssel gemeint, unter dem der Titel bei Yahoo Finance gelistet ist, also das Kürzel an der Börse und nicht die Wertpapiernummer. Bekannte Beispiele sind AAPL für Apple, MSFT für Microsoft, SAP.DE für SAP, SXR8.DE für den iShares Core S&P 500 ETF und ^GDAXI für den DAX. Gesucht wird auf finance.yahoo.com, dort findest du das passende Kürzel für jeden Titel. Das Symbol wird beim Abspeichern gross geschrieben.

Neben dem Symbol kannst du einen Namen eingeben, er wird aber automatisch ergänzt, wenn du das Feld leer lässt. Dazu gehören der Typ, also Aktie, ETF, Obligation oder Fonds, das Kaufdatum im Format JJJJ-MM-TT, der Kaufpreis und die Stückzahl. Bei Preis und Stückzahl sind Komma und Punkt als Dezimalzeichen erlaubt. Mit Hinzufügen landet der Titel in der Tabelle. So baust du dein Portfolio auf, mindestens drei Titel sollten es sein, sonst ergibt die Gesamtdarstellung wenig Sinn.

Mit Kurse laden holt das Programm die Kursverläufe von Yahoo Finance und schreibt sie in eine Datei. Der Zeitraum oben rechts, wählbar von einem Monat bis Max, bestimmt, welcher Abschnitt im Diagramm und in der Spalte Rendite Zeitraum betrachtet wird. Nach jeder Änderung des Zeitraums werden die Kurse neu geladen.

Die Tabelle zeigt pro Titel den letzten Kurs, den aktuellen Wert als Stückzahl mal Kurs, die Rendite seit Kauf im Vergleich zum Kaufpreis und die Rendite im gewählten Zeitraum. Darüber steht eine Zeile mit Gesamtwert, Kaufwert und beiden Renditen für das ganze Portfolio. Das Diagramm darunter zeichnet den Wertverlauf des Portfolios als schwarze Linie, mit der Option Einzelne Titel anzeigen kommen die Linien der einzelnen Titel in Grau dazu.

Eine Zeile entfernst du, indem du sie anklickst und Entfernen drückst.

Wenn das Internet kurz weg ist, stürzt das Programm nicht ab. Es nimmt dann die zuletzt gespeicherten Kurse und sagt unten in der Statusleiste, von wann diese sind. Liegen noch keine Kurse vor, steht das ebenfalls dort.

Die Dateien liegen unter %LOCALAPPDATA%\PortfolioTracker, einmal portfolio.json mit deinen Eingaben und einmal prices.json mit den Kursverläufen. Der genaue Pfad steht beim Start unten in der Statusleiste.

Fehlermeldungen sind bewusst einfach gehalten. Meldet das Programm Bitte ein Symbol eingeben, ist das Symbol-Feld leer. Meldet es Das Kaufdatum ist ungültig, wurde das Datum nicht im Format JJJJ-MM-TT geschrieben. Meldet es Der Kaufpreis ist keine gültige Zahl, stecken Buchstaben oder Sonderzeichen im Preis. Meldet es Yahoo Finance kennt das Symbol nicht, ist das Kürzel falsch geschrieben oder der Titel nicht bei Yahoo gelistet. Meldet es HTTP 429, hat Yahoo kurz zu viele Anfragen blockiert, dann später noch einmal probieren.

Zum Schluss noch ein Hinweis zur Berechnung. Die Rendite wird aus den Schlusskursen gerechnet, Dividenden und Zinsen sind nicht enthalten. Bei ETFs und Aktien ist die echte Gesamtrendite daher etwas höher, als das Programm anzeigt.

Die Datei PortfolioTracker.cs im Hauptordner des Repositories enthält denselben Quelltext in einer einzigen Datei, falls du den Code lieber auf einen Schlag sehen oder abgeben willst.
