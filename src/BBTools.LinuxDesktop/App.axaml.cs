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
                timer.Tick+=(_,_)=>
                {
                    timer.Stop();
                    try
                    {
                        var handle=desktop.MainWindow.TryGetPlatformHandle()?.Handle??throw new IOException("Native window handle unavailable.");
                        using var x11=new X11GameWindow();var pixels=x11.CaptureOwnWindow(unchecked((ulong)handle));
                        var folder=Path.Combine(Path.GetTempPath(),"BBTools-photo-smoke-"+Guid.NewGuid().ToString("N"));string? photo=null;
                        try
                        {
                            photo=X11GameWindow.Save(PhotoProcessing.Apply(pixels,new(Filter:"Noir")),folder,"smoke");
                            var libraryPath=Path.Combine(folder,"library.db");
                            try
                            {
                                var library=new UserLibrary(libraryPath,Path.Combine(folder,"absent-legacy.json"));
                                library.Save(new PracticePreset("Smoke preset",false,false,[],1,1));
                                if(library.Presets().Single().Name!="Smoke preset")throw new IOException("Packaged SQLite library round trip failed.");
                            }
                            finally{if(File.Exists(libraryPath))File.Delete(libraryPath);}
                            using var image=new Avalonia.Media.Imaging.Bitmap(photo);
                            if(image.PixelSize.Width!=pixels.Width||image.PixelSize.Height!=pixels.Height)throw new IOException("PNG dimensions changed during capture.");
                        }
                        finally{if(photo!=null)File.Delete(photo);if(Directory.Exists(folder))Directory.Delete(folder);}
                        Console.WriteLine("PASS native X11 window ownership, key mapping, capture, filters, PNG and SQLite round trips");desktop.Shutdown(0);
                    }
                    catch(Exception ex){Console.Error.WriteLine(ex);desktop.Shutdown(1);}
                };timer.Start();
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
