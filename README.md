# Portfolio Tracker

Ein Programm in C#, das ein Portfolio aus Aktien, ETFs und Obligationen verwaltet. Es holt die Kurse von Yahoo Finance, speichert sie lokal, berechnet Renditen und zeigt den Wertverlauf des Portfolios als Diagramm in einem Fenster. Gebaut ist es als Windows-Forms-Programm mit .NET 10, die Oberfläche ist bewusst schwarz auf weiss gehalten. Der gesamte Quelltext steht in einer einzigen Datei.

Wer das Programm nur benutzen möchte, findet die fertige Datei PortfolioTracker.exe im Release. Sie wird heruntergeladen und doppelgeklickt, es muss nichts installiert werden. Die Bedienung steht in START.md.

Der komplette Quelltext des Programms liegt in einer einzigen Datei, src/PortfolioTracker/PortfolioTracker.cs. Sie enthält die Datenhaltung für die eingegebenen Titel und die Kursverläufe, den Abruf bei Yahoo Finance, die Berechnung der Renditen und das Fenster mit dem Diagramm. Zum Bauen genügt der Befehl dotnet publish src/PortfolioTracker/PortfolioTracker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish, das Ergebnis liegt dann unter publish/PortfolioTracker.exe.

Die Daten liegen im Ordner %LOCALAPPDATA%\PortfolioTracker, dort die Datei portfolio.json mit den eingegebenen Titeln und die Datei prices.json mit den Kursverläufen. Das Programm zeigt den Ort beim Start unten in der Statusleiste an.

Als Quelle für die Kurse dient Yahoo Finance, Endpunkt https://query1.finance.yahoo.com/v8/finance/chart/{Symbol}. Yahoo Finance wurde gewählt, weil der Dienst kostenlos und ohne Schlüssel oder Registrierung nutzbar ist, Aktien, ETFs und Indizes weltweit abdeckt, auch deutsche Börsen über das Suffix .DE, und die täglichen Schlusskurse als JSON liefert. Die Antworten werden lokal gespeichert, damit das Programm bei Internetausfall mit den letzten Kursen weiterläuft. Nachteil ist, dass Dividenden und Zinsen nicht enthalten sind, die berechnete Rendite ist also eine reine Kursrendite. Anleihen müssen über ein bei Yahoo Finance gelistetes Symbol erfasst werden.
