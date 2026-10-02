using System.Security.Principal;

namespace ScannerDisabler;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var instanceMutex = new Mutex(true, @"Global\PenguinAntiScan_SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "Penguin Anti-Scan is already running in the system tray.",
                "Penguin Anti-Scan",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            MessageBox.Show(
                "Penguin Anti-Scan requires administrator privileges.\n\nReopen the app and accept the Windows UAC prompt.",
                "Administrator privileges required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Application.Run(new MainForm());
        }
        finally
        {
            WirelessService.Shutdown();
            instanceMutex.ReleaseMutex();
        }
    }
}
