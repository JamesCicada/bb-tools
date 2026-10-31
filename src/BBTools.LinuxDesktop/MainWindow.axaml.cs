using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BBTools.LinuxCore;
using BBTools.Memory;

namespace BBTools.LinuxDesktop;

public partial class MainWindow : Window
{
    private LinuxCandidate[] processes=[];
    private LinuxGameConnection? connection;
    private LinuxSnapshot? snapshot;
    private CharacterState? draft;
    private readonly DispatcherTimer poll=new(){Interval=TimeSpan.FromMilliseconds(500)};
    private readonly CancellationTokenSource lifetime=new();
    private readonly bool preview;
    private SessionLog? log;
    private bool busy,closing,changing;
    private readonly string settingsPath;
    public MainWindow():this(Program.SmokeTest || !OperatingSystem.IsLinux()){}
    public MainWindow(bool preview)
    {
        this.preview=preview;
        var home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var config=Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        settingsPath=Path.Combine(config!=null&&Path.IsPathFullyQualified(config)?config:Path.Combine(home,".config"),"bb-tools","linux-settings.json");
        var locale="auto";
        if(!preview)try{locale=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(settingsPath))?.GetValueOrDefault("Language")??"auto";}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException){}
        ApplyLocale(locale);
        InitializeComponent();
        LoadLanguages(locale);
        if(!preview)
        {
            try{log=new SessionLog(AppContext.BaseDirectory);}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException)
            {
                var state=Environment.GetEnvironmentVariable("XDG_STATE_HOME");
                try{log=new SessionLog(Path.Combine(state!=null&&Path.IsPathFullyQualified(state)?state:Path.Combine(home,".local","state"),"bb-tools"));}
                catch(Exception fallback)when(fallback is IOException or UnauthorizedAccessException){Status.Text="Logging unavailable: "+fallback.Message;}
            }
        }
        LogLocation.Text=log==null?"Session logs are disabled during UI smoke tests.":"Session log: "+log.FilePath;
        PermissionInfo.Text=OperatingSystem.IsLinux()?$"Yama ptrace_scope={LinuxPermissions.Scope()}. Scope 1 may require launching the game from this tool; containers can also restrict access.":"Linux preview UI: memory access is available on native Linux x64 only.";
        InitFeatures();
        poll.Tick+=(_,_)=>Poll();poll.Start();
        Opened+=(_,_)=>{if(!preview)RefreshProcesses(this,new());};
        Closing+=(_,e)=>
        {
            try{connection?.Dispose();}
            catch(Exception ex)when(Expected(ex)){Report(ex);e.Cancel=true;return;}
            closing=true;lifetime.Cancel();poll.Stop();inputTimer.Stop();x11?.Dispose();presence?.Dispose();log?.Dispose();
        };
    }
    private T C<T>(string name)where T:Control=>this.FindControl<T>(name)!;
    private static bool Expected(Exception ex)=>ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception or JsonException or OperationCanceledException;
    private void Report(Exception ex){log?.Error(ex,"Linux desktop");Status.Text=ex.Message;}
    private void Action(Action action,string success)
    {try{action();Status.Text=success;log?.Write("ACTION",success);}catch(Exception ex)when(Expected(ex)){Report(ex);}}
    private void ApplyLocale(string code)
    {
        Translations.Select(code);
        foreach(var key in Translations.English.Keys)Application.Current!.Resources["L."+key]=Translations.T(key);
        FlowDirection=CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft?Avalonia.Media.FlowDirection.RightToLeft:Avalonia.Media.FlowDirection.LeftToRight;
    }
    private void LoadLanguages(string code)
    {
        changing=true;LanguagePicker.ItemsSource=new[]{new AppLanguage("auto",Translations.T("language.system"))}.Concat(Translations.Languages).ToArray();
        LanguagePicker.SelectedItem=((IEnumerable<AppLanguage>)LanguagePicker.ItemsSource).FirstOrDefault(x=>x.Code==code)??Translations.Languages[0];changing=false;
    }
    private void LanguageChanged(object? sender,SelectionChangedEventArgs e)
    {
        if(changing||LanguagePicker.SelectedItem is not AppLanguage language)return;
        ApplyLocale(language.Code);LoadLanguages(language.Code);
        if(!preview)Action(()=>{Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);var temp=settingsPath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(new{Language=language.Code}));File.Move(temp,settingsPath,true);},Translations.T("language.changed"));
    }
    private async void RefreshProcesses(object? sender,RoutedEventArgs e)
    {
        if(busy)return;
        try{processes=await Task.Run(LinuxProcessCatalog.List);if(!closing)UpdateProcesses();}
        catch(Exception ex)when(Expected(ex)){Report(ex);}
    }
    private void FilterChanged(object? sender,TextChangedEventArgs e){if(Candidates!=null)UpdateProcesses();}
    private void UpdateProcesses()
    {
        var old=Candidates.SelectedItem as LinuxCandidate;
        var filtered=LinuxProcessCatalog.Filter(processes,ProcessFilter.Text??"");Candidates.ItemsSource=filtered;
        Candidates.SelectedItem=filtered.FirstOrDefault(x=>x.Pid==old?.Pid&&x.Identity==old.Identity)??filtered.FirstOrDefault();
    }
    private async void Connect(object? sender,RoutedEventArgs e)
    {
        if(busy)return;
        if(connection!=null){Action(()=>{connection.Dispose();connection=null;snapshot=null;draft=null;startupApplied=false;beforePractice=null;hotkeyLocation=null;presence?.Update(null);SyncToggles();UpdateAvailability(false);},"Disconnected; owned changes restored where the same player was valid.");return;}
        if(Candidates.SelectedItem is not LinuxCandidate process){Status.Text="Select a game process first.";return;}
        busy=true;ConnectButton.IsEnabled=RefreshButton.IsEnabled=Candidates.IsEnabled=ProcessFilter.IsEnabled=false;Status.Text="Validating the native game image…";
        try
        {
            var next=await Task.Run(()=>new LinuxGameConnection(process,lifetime.Token),lifetime.Token);
            if(closing)return;
            connection=next;Status.Text=$"Connected read-only to PID {process.Pid}. Load a character.";Poll();
        }
        catch(Exception ex)when(Expected(ex)){if(!closing)Report(ex);}
        finally{busy=false;if(!closing){ConnectButton.IsEnabled=RefreshButton.IsEnabled=true;Candidates.IsEnabled=ProcessFilter.IsEnabled=connection==null;}}
    }
    private async void Poll()
    {
        if(connection==null){await AutoConnectAsync();return;}
        if(polling)return;
        polling=true;
        try
        {
            snapshot=connection.ReadAndUpdate();
            await connection.Features.TickAsync();enemyText.Text=connection.Features.EnemyDetails;
            if(connection.Editing&&!startupApplied)
            {
                startupApplied=true;
                var preset=library?.Presets().FirstOrDefault(x=>x.Name==preferences.StartupPresetName);
                if(preset!=null){await connection.Features.ApplyPresetAsync(preset);ShowPreset(preset);SyncCombat();}
                else if(preferences.StartupFovEnabled){values["fov"].Value=(decimal)preferences.StartupFov;switches["fov"].IsChecked=true;await connection.Features.ApplyPresetAsync(Capture("Startup FOV"));}
            }
            presence?.Update("Native bbport",preferences.DiscordDetails?.Enabled==true?connection.Session.ReadPresenceDetails(preferences.DiscordDetails):null);var p=snapshot.Player;var ch=snapshot.Character;
            ResourceSummary.Text=$"HP {p.Health:N0} / {p.MaxHealth:N0} · Stamina {p.Stamina:N0} / {p.MaxStamina:N0}\nBlood Level {ch.Level} · Blood Echoes {ch.Values.Echoes:N0}";
            UpdateAvailability(true);
            if(connection.FeatureError!=null)Status.Text="Features paused: "+connection.FeatureError;
        }
        catch(Exception ex)when(Expected(ex)){snapshot=null;draft=null;connection?.Features.PlayerUnavailable(ex.Message);enemyText.Text=connection?.Features.EnemyDetails;UpdateAvailability(false);Report(ex);}
        finally{polling=false;}
    }
    private void UpdateAvailability(bool ready)
    {
        changing=true;EditingEnabled.IsEnabled=connection!=null&&ready&&!busy;EditingEnabled.IsChecked=connection?.Editing==true;changing=false;
        EditorPanel.IsEnabled=ResourceFlags.IsEnabled=ready&&connection?.Editing==true;
        foreach(var control in editingControls)control.IsEnabled=EditorPanel.IsEnabled&&!busy;
        NativeFlags.IsEnabled=EditorPanel.IsEnabled&&connection?.CombatFlagsAvailable==true;
        Candidates.IsEnabled=ProcessFilter.IsEnabled=connection==null&&!busy;
        ConnectButton.Content=Translations.Source(connection==null?"Connect":"Disconnect");
        ConnectionBadge.Text=connection==null?"READ-ONLY · DISCONNECTED":$"PID {connection.Pid} · {(connection.Editing?"EDITING":"READ-ONLY")}{(ready?"":" · LOADING / UNAVAILABLE")}";
    }
    private void EditingChanged(object? sender,RoutedEventArgs e)
    {
        if(changing||connection==null)return;
        Action(()=>{connection.SetEditing(EditingEnabled.IsChecked==true);if(!connection.Editing){startupApplied=false;SyncToggles();}Poll();},"Editing mode updated for this connection.");
        UpdateAvailability(snapshot!=null);
    }
    private void CombatChanged(object? sender,RoutedEventArgs e)
    {
        if(changing||connection==null||sender is not CheckBox box)return;
        var enabled=box.IsChecked==true;
        if(box.Tag is "health")connection.KeepHealth=enabled;
        else if(box.Tag is "stamina")connection.InfiniteStamina=enabled;
        else if(Enum.TryParse<PracticeFlag>(box.Tag?.ToString(),out var flag)){if(enabled)connection.Flags.Add(flag);else connection.Flags.Remove(flag);}
        connection.Resume();Poll();
    }
    private void SyncToggles(){changing=true;foreach(var toggle in this.GetVisualDescendants().OfType<CheckBox>().Where(x=>x.Tag!=null))toggle.IsChecked=false;changing=false;}
    private void StopFeatures(object? sender,RoutedEventArgs e)=>Action(()=>{connection?.Stop();SyncToggles();},"Practice selections cleared; owned flags restored.");
    private void ResumeFeatures(object? sender,RoutedEventArgs e)=>Action(()=>{connection?.Resume();if(!preview)inputTimer.Start();Poll();},"Feature retry requested.");
    private static int Number(TextBox input)=>int.TryParse(input.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var value)?value:throw new ArgumentException("Enter a whole non-negative number.");
    private LinuxGameConnection Active()=>connection??throw new InvalidOperationException("Connect to a loaded character first.");
    private void ApplyHealth(object? sender,RoutedEventArgs e)=>Action(()=>{Active().SetHealth(Number(HealthInput));Poll();},Translations.Source("Health updated."));
    private void ApplyEchoes(object? sender,RoutedEventArgs e)=>Action(()=>{Active().SetEchoes(Number(EchoesInput));Poll();},Translations.Source("Blood Echoes updated."));
    private TextBox[] Stats()=>[VitalityInput,EnduranceInput,StrengthInput,SkillInput,BloodtingeInput,ArcaneInput];
    private void ReloadStats(object? sender,RoutedEventArgs e)=>Action(()=>{draft=Active().Session.ReadCharacter();var values=draft.Values.Attributes;for(var i=0;i<6;i++)Stats()[i].Text=values[i].ToString(CultureInfo.InvariantCulture);},Translations.Source("Current attributes loaded into the editor."));
    private void ApplyStats(object? sender,RoutedEventArgs e)=>Action(()=>{Active().SetAttributes(Stats().Select(Number).ToArray(),draft??throw new InvalidOperationException("Reload current attributes first."));draft=null;Poll();},Translations.Source("Attributes applied. Quit to title and reload your character to refresh derived stats."));
    private async void ChooseLauncher(object? sender,RoutedEventArgs e)
    {try{var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Choose your installed Bloodborne launcher",AllowMultiple=false});if(files.Count==1)LauncherPath.Text=files[0].TryGetLocalPath();}catch(Exception ex)when(Expected(ex)){Report(ex);}}
    private void LaunchGame(object? sender,RoutedEventArgs e)=>Action(()=>{using var process=LinuxProcessCatalog.StartGame(LauncherPath.Text??"",LauncherArguments.Text??"[]");RefreshProcesses(this,new());},"Launcher started. Refresh after bb-probe appears, then connect.");
    private void OpenCrowdin(object? sender,RoutedEventArgs e)=>Action(()=>{var start=new ProcessStartInfo("xdg-open"){UseShellExecute=false};start.ArgumentList.Add("https://crowdin.com/project/bb-tools");Process.Start(start)?.Dispose();},"Opening Crowdin.");
}
