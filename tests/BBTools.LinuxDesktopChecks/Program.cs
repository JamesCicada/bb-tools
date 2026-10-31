using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using BBTools;
using BBTools.LinuxDesktop;

internal static class Program
{
    private static int count;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);count++;}
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            AppBuilder.Configure<App>().UseSkia().WithInterFont().UseHeadless(new(){UseHeadlessDrawing=false}).SetupWithoutStarting();
            var window=new MainWindow(preview:true){Width=860,Height=680};window.Show();Dispatcher.UIThread.RunJobs();
            Check(window.FindControl<CheckBox>("EditingEnabled")!.IsChecked!=true&&!window.FindControl<CheckBox>("EditingEnabled")!.IsEnabled,"Linux desktop starts read-only with editing disabled before a valid player");
            Check(!window.FindControl<StackPanel>("EditorPanel")!.IsEnabled&&!window.FindControl<StackPanel>("NativeFlags")!.IsEnabled,"data and combat controls require an opted-in valid connection");
            var languages=window.FindControl<ComboBox>("LanguagePicker")!;
            var available=(IEnumerable<AppLanguage>)languages.ItemsSource!;
            Check(available.Count()==29,"Linux desktop embeds the same 28 locale catalogs and automatic selection");
            languages.SelectedItem=available.Single(x=>x.Code=="ar");Dispatcher.UIThread.RunJobs();
            Check(window.FlowDirection==Avalonia.Media.FlowDirection.RightToLeft&&window.FindControl<TextBox>("HealthInput")!.FlowDirection==Avalonia.Media.FlowDirection.LeftToRight,"Arabic layout keeps resource and ID entry left to right");
            languages.SelectedItem=((IEnumerable<AppLanguage>)languages.ItemsSource!).Single(x=>x.Code=="fr");Dispatcher.UIThread.RunJobs();
            Check(Translations.CurrentLanguage=="fr"&&window.FlowDirection==Avalonia.Media.FlowDirection.LeftToRight,"Linux language changes apply without restarting or enabling edits");
            languages.SelectedItem=((IEnumerable<AppLanguage>)languages.ItemsSource!).Single(x=>x.Code=="en");Dispatcher.UIThread.RunJobs();
            if(args.Length>0){Directory.CreateDirectory(args[0]);using var frame=window.CaptureRenderedFrame()!;frame.Save(Path.Combine(args[0],"linux-connection.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());}
            var tabs=window.FindControl<TabControl>("Sections")!;
            Check(tabs.ItemCount==8,"Linux exposes eight primary gameplay and settings sections");
            var controls=new List<Avalonia.Controls.Control>();
            for(var i=0;i<tabs.ItemCount;i++)
            {
                tabs.SelectedIndex=i;Dispatcher.UIThread.RunJobs();
                var children=Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Control>().ToArray();controls.AddRange(children);
                if(args.Length>0){using var frame=window.CaptureRenderedFrame()!;frame.Save(Path.Combine(args[0],"linux-page-"+i+".png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());}
                foreach(var nested in children.OfType<TabControl>().Where(x=>x!=tabs))
                {
                    for(var n=0;n<nested.ItemCount;n++)
                    {
                        nested.SelectedIndex=n;Dispatcher.UIThread.RunJobs();
                        controls.AddRange(Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Control>());
                        if(args.Length>0){using var frame=window.CaptureRenderedFrame()!;frame.Save(Path.Combine(args[0],$"linux-page-{i}-{n}.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());}
                    }
                    nested.SelectedIndex=0;Dispatcher.UIThread.RunJobs();
                }
                if(i==2)Check(children.OfType<Button>().Where(x=>x.Content?.ToString()=="Apply tuning").All(x=>!x.IsEnabled),"native Linux tuning actions remain disabled before a connection");
                if(i==5)Check(children.OfType<CheckBox>().Single(x=>x.Content?.ToString()=="Enable experimental photo mode / noclip").IsChecked!=true,"experimental Linux camera and noclip remain opt-in");
            }
            Check(controls.OfType<Button>().Any(x=>x.Content?.ToString()=="Generate natural gem"),"Linux has built-in obtainable gem generation without game-file name imports");
            Check(controls.OfType<Button>().Any(x=>x.Content?.ToString()=="Revive boss"),"Linux exposes shared boss revival and first-encounter controls");
            window.Close();Console.WriteLine($"PASS: {count} Linux desktop UI checks.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
