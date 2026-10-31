using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace BBTools;

public partial class App : Application
{
    internal static SessionLog? Log { get; private set; }
    internal static string? LoggingIssue { get; private set; }
    internal static void LogError(Exception error,[CallerMemberName] string context="") => Log?.Error(error,context);
    protected override void OnStartup(StartupEventArgs e)
    {
        try { Log = new SessionLog(AppContext.BaseDirectory); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { LoggingIssue=$"Session logging unavailable: {ex.Message}"; }
        Memory.GameDiagnostics.Log = (kind, message) => Log?.Write(kind, message);
        Memory.NativeCodeCompatibility.Diagnostic = message => Log?.Write("COMPAT", message);
        DispatcherUnhandledException += (_, args) => LogError(args.Exception,"Unhandled UI exception");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if(args.ExceptionObject is Exception error) LogError(error,"Unhandled process exception");
        };
        TaskScheduler.UnobservedTaskException += (_, args) => LogError(args.Exception,"Unobserved task exception");
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        Log?.Write("INFO",$"Application exit code {e.ApplicationExitCode}");
        Log?.Dispose();
        base.OnExit(e);
    }
}
