using PortfolioTracker.UI;

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
