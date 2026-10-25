using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace BBTools.LinuxDesktop;

public sealed partial class App : Application
{
    public override void Initialize()=>AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow=new MainWindow();
            if(Program.SmokeTest)
            {
                var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
                timer.Tick+=(_,_)=>{timer.Stop();desktop.Shutdown(0);};timer.Start();
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
