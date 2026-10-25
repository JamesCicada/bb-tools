using Avalonia;

namespace BBTools.LinuxDesktop;

public static class Program
{
    public static bool SmokeTest { get; private set; }
    [STAThread]
    public static void Main(string[] args)
    {
        SmokeTest=args.Contains("--smoke-test");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    public static AppBuilder BuildAvaloniaApp()=>AppBuilder.Configure<App>().UsePlatformDetect()
        .With(new X11PlatformOptions{RenderingMode=[X11RenderingMode.Software]})
        .WithInterFont().LogToTrace();
}
