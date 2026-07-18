using CursorCycle.Application;
using CursorCycle.Infrastructure;

namespace CursorCycle;

internal static class Program
{
    private const string ApplicationId = "OshiCursour.9E20A555-0E81-4BF8-A1B8-9BB137317AE2";

    [STAThread]
    private static void Main(string[] args)
    {
        if (UpdateInstaller.TryRunUpdaterMode(args))
        {
            return;
        }

        DiagnosticLogger.Write(
            $"Process started. Version={typeof(Program).Assembly.GetName().Version}, " +
            $"Args=[{string.Join(", ", args)}]");

        try
        {
            RunApplication(args);
        }
        catch (Exception exception)
        {
            DiagnosticLogger.WriteException("Program.Main", exception);
            ShowFatalError(exception);
        }
    }

    private static void RunApplication(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            DiagnosticLogger.Write("Stopped because the operating system is not Windows.");
            return;
        }

        System.Windows.Forms.Application.ThreadException += (_, eventArgs) =>
        {
            DiagnosticLogger.WriteException(
                "Application.ThreadException",
                eventArgs.Exception);
            ShowFatalError(eventArgs.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                DiagnosticLogger.WriteException(
                    "AppDomain.UnhandledException",
                    exception);
            }
            else
            {
                DiagnosticLogger.Write(
                    $"Unhandled non-Exception object: {eventArgs.ExceptionObject}");
            }
        };

        using var singleInstance = new SingleInstanceManager(ApplicationId);
        if (!singleInstance.IsPrimaryInstance)
        {
            DiagnosticLogger.Write(
                "Another instance is already running. Requesting its settings window.");
            SingleInstanceManager.SignalPrimaryInstance(ApplicationId);
            return;
        }

        DiagnosticLogger.Write("Primary instance acquired.");

        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        DiagnosticLogger.Write("Windows Forms initialized.");

        var startedByWindows = args.Any(argument =>
            string.Equals(argument, "--startup", StringComparison.OrdinalIgnoreCase));

        using var applicationContext = new TrayApplicationContext(
            showSettingsOnStart: !startedByWindows,
            checkForUpdates: !args.Any(argument =>
                string.Equals(argument, "--update-rollback", StringComparison.OrdinalIgnoreCase)));

        UpdateInstaller.MarkApplicationHealthy(args);

        DiagnosticLogger.Write(
            $"Application context created. StartedByWindows={startedByWindows}.");
        singleInstance.StartListening(applicationContext.RequestShowSettings);
        System.Windows.Forms.Application.Run(applicationContext);
        DiagnosticLogger.Write("Application message loop exited normally.");
    }

    private static void ShowFatalError(Exception exception)
    {
        try
        {
            MessageBox.Show(
                "OshiCursour の起動中にエラーが発生しました。\n\n" +
                exception.Message + "\n\n" +
                "診断ログ:\n" + DiagnosticLogger.LogFilePath,
                "OshiCursour 起動エラー",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // メッセージ表示にも失敗した場合はログだけを残す。
        }
    }
}
