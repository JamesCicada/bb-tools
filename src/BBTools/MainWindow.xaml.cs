using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Net.Http;
using System.Text.Json;
using BBTools.Memory;
using Microsoft.Win32;
namespace BBTools;

public partial class MainWindow : Window
{
    private ProcessMemory? memory;
    private PlayerSession? session;
    private GameActions? actions;
    private bool actionBusy;
    private ProcessCandidate[] processSnapshot = [];
    private ItemSearchIndex? itemSearchIndex;
    private float desiredDamage = 1, desiredEchoes = 1;
    private ulong multiplierFailedActor;
    private CharacterState? statDraft;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private bool updating, autoPaused;
    private volatile bool closing, connecting, discovering;
    private AppSettings settings = AppSettings.Load();
    private UserLibrary? library;
    private string? libraryIssue;
    private CameraFov? cameraFov;
    private bool fovPaused;
    private bool startupPresetPending;
    private bool durabilityPaused;
    private FreeCamera? freeCamera;
    private ExplorationFlight? flight;private HudVisibility? hudVisibility;
    private MovementSpeed? movementSpeed;private WorldGravity? worldGravity;private bool advancedPaused;
    private DateTime durabilityRetryAt;
    private bool statusPaused;
    private CameraTuning? cameraTuning;
    private bool tuningPaused;
    private PracticeDestination? retryDestination;
    private DateTime retryDeadline;
    private long retryGeneration;
    private bool retryTravelling;
    private bool retryLoadObserved;
    private ulong retryOriginActor;
    private readonly HashSet<(int Id,long Started)> knownRuntimes=[];
    private string? backupHistoryFolder;
    private volatile bool saveOperationBusy;
    private HotkeyManager? hotkeys;
    private readonly EncounterTimer encounterTimer=new();
    private readonly DispatcherTimer autoTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private int connectedPid;
    private ulong lastActor, lastComponent;
    private Mutex? sessionOwner;
    private static readonly HttpClient updateClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly CancellationTokenSource updateLifetime = new();
    private Uri? updateRelease;
    public MainWindow()
    {
        ApplyLanguageResources();
        InitializeComponent();
        LoadLanguageControls();
        InitializeExploration();
        InitializeTargetMonitor();
        InitializePhotoCapture();
        var statusProperty = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        statusProperty.AddValueChanged(Status, LogStatus);
        Closed += (_, _) => { statusProperty.RemoveValueChanged(Status, LogStatus); hotkeys?.Dispose(); };
        AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((_, args) =>
        {
            if(args.OriginalSource is Button button) App.Log?.Write("ACTION", $"Button: {button.Content}");
        }));
        Title = Translations.Format("app.title", AppInfo.DisplayVersion);
        VersionLabel.Text = Translations.Format("app.version", AppInfo.DisplayVersion);
        AboutVersion.Text = Translations.Format("message.BBToolsv0.b7fe245", AppInfo.DisplayVersion);
        ChaliceLampPicker.ItemsSource = GameActions.ChaliceLamps; ChaliceLampPicker.SelectedIndex = 0;
        ChalicePicker.ItemsSource = Enumerable.Range(1, 6).Select(bank => new ChaliceAltar(bank, 0, 0)).ToArray(); ChalicePicker.SelectedIndex = 0;
        MenuPicker.ItemsSource = GameActions.Menus; MenuPicker.SelectedIndex = 0;
        ItemPicker.ItemsSource = GameActions.CommonItems;
        ItemPicker.SelectedIndex = 0;
        LampPicker.ItemsSource = GameActions.Lamps;
        LampPicker.SelectedIndex = 0;
        try { library = new UserLibrary(); RefreshLibrary(); }
        catch (Exception ex) when (IsExpected(ex) || ex is JsonException)
        { App.LogError(ex); library = null; libraryIssue = $"Saved library unavailable: {ex.Message} Original files were retained."; Status.Text = libraryIssue; }
        BossPicker.ItemsSource = BossDestinations.All; BossPicker.SelectedIndex = 0;
        timer.Tick += Poll;
        Closing += OnClosing;
        Refresh(this, new RoutedEventArgs());
        updating = true; AutoConnect.IsChecked = settings.AutoConnect; StartupFov.IsChecked = settings.StartupFovEnabled;
        FovSlider.Value = settings.StartupFov; FovEnabled.IsChecked = settings.StartupFovEnabled;LoadDiscordControls(); updating = false;
        UpdateStartupSummary();
        LoadHotkeyControls();
        autoTimer.Tick += async (_, _) => await TryAutoConnect();
        Loaded += async (_, _) => { InitializeHotkeys(); autoTimer.Start(); await TryAutoConnect(); if (libraryIssue != null) Status.Text = libraryIssue; if (App.LoggingIssue != null) Status.Text = App.LoggingIssue; };
    }
    private void LogStatus(object? sender, EventArgs e) => App.Log?.Write("STATUS", Status.Text);
    private void Refresh(object sender, RoutedEventArgs e)
    {
        var selected = Candidates.SelectedItem as ProcessCandidate;
        processSnapshot = ProcessDiscovery.List();
        UpdateProcessPicker(selected);
        Status.Text = Translations.Format("message.Found0processesLikelyBloodborneprocessesarestarredan.688f5b3", processSnapshot.Length);
    }
    private void UpdateProcessPicker(ProcessCandidate? selected = null)
    {
        var visible = ProcessDiscovery.Filter(processSnapshot, ProcessSearch.Text);
        Candidates.ItemsSource = visible;
        Candidates.SelectedItem = visible.FirstOrDefault(x => x.Id == selected?.Id && x.Started == selected.Started) ?? visible.FirstOrDefault();
        if (visible.Length == 0) ConnectionHint.Text = Translations.T("message.NoprocessesmatchthisfilterClearitorrefreshthelist.a34ac2a");
        else if (session == null) ConnectionHint.Text = Translations.Format("message.Showing0of1processesSelectthegameenginethenconnect.6613503", visible.Length, processSnapshot.Length);
    }
    private void FilterProcesses(object sender, TextChangedEventArgs e)
    {
        if (Candidates == null) return;
        UpdateProcessPicker(Candidates.SelectedItem as ProcessCandidate);
    }
    private async void Connect(object sender, RoutedEventArgs e)
    {
        if (session != null) { if (actionBusy) { Status.Text = Translations.T("message.Waitfortheactivegamerequestbeforedisconnecting.df86824"); return; } autoPaused = true; Disconnect(); return; }
        if (Candidates.SelectedItem is not ProcessCandidate candidate) { Status.Text = Translations.T("message.Selectarunninggameprocessfirst.996f24f"); return; }
        autoPaused = false;
        await ConnectCandidate(candidate);
    }
    private async Task ConnectCandidate(ProcessCandidate candidate)
    {
        if (connecting || closing || session != null || saveOperationBusy) return;
        if (candidate.Started == 0) { Status.Text = Translations.T("message.ThisprocessidentityisrestrictedorunavailableRefresha.f5fe983"); return; }
        App.Log?.Write("INFO", $"Connecting to {candidate.Label}, creation ticks {candidate.Started}");
        connecting = true;
        ConnectButton.IsEnabled = RefreshButton.IsEnabled = Candidates.IsEnabled = ProcessSearch.IsEnabled = false;
        Status.Text = Translations.T("message.Checkingtheloadedgameimageandplayerstructures.3a71539");
        ProcessMemory? pending = null;
        try
        {
            using var process = Process.GetProcessById(candidate.Id);
            if (process.StartTime.ToUniversalTime().Ticks != candidate.Started) throw new IOException(Translations.T("message.TheselectedprocesswasreplacedRefreshandselectitagain.beb4d74"));
            sessionOwner = new Mutex(false, $"Local\\BBTools.Player.{candidate.Id}.{candidate.Started}");
            var acquired = false;
            try { acquired = sessionOwner.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) { sessionOwner.Dispose(); sessionOwner = null; throw new IOException(Translations.T("message.AnotherBBToolswindowisalreadyconnectedtothisprocess.67ccdb5")); }
            pending = new ProcessMemory(candidate.Id);
            var opened = pending;
            var image = await Task.Run(() => PlayerSession.FindImage(opened, opened.ImageCandidates(candidate.Name.Contains("shadps4",StringComparison.OrdinalIgnoreCase))));
            if (closing) { pending.Dispose(); ReleaseOwner(); return; }
            session = new PlayerSession(opened, image);
            actions = new GameActions(opened, session);
            memory = opened; pending = null; connectedPid = candidate.Id;knownRuntimes.Add((candidate.Id,candidate.Started));
            ConnectionBadge.Text = candidate.Name.Contains("shadps4",StringComparison.OrdinalIgnoreCase)?Translations.T("message.SHADPS4CONNECTED.229770f"):Translations.T("message.CONNECTED.1f914c4");
            ConnectionHint.Text = Translations.Format("message.0Bloodborne109instructionsmatched.5d2255f", candidate.Label);
            ConnectButton.Content = Translations.T("message.Disconnect.acfc5be");
            startupPresetPending = settings.StartupPresetName != null;
            if(settings.StartupFovEnabled){updating = true; FovSlider.Value = settings.StartupFov; FovEnabled.IsChecked = true; updating = false;}
            timer.Start();
            Status.Text = Translations.T("message.ConnectedFeaturesstartoffPlayercontrolsbecomeavailab.74ae0b3");
            Poll(this, EventArgs.Empty);
        }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); pending?.Dispose(); ReleaseOwner(); Status.Text = ex.Message; }
        finally
        {
            connecting = false;
            ConnectButton.IsEnabled = true;
            RefreshButton.IsEnabled = Candidates.IsEnabled = ProcessSearch.IsEnabled = session == null;
        }
    }
    private async Task TryAutoConnect()
    {
        if (!settings.AutoConnect || autoPaused || closing || session != null || connecting || discovering || saveOperationBusy) return;
        discovering = true;
        try
        {
            var (candidates, best) = await Task.Run(() =>
            {
                var found = ProcessDiscovery.List();
                return (found, ProcessDiscovery.FindBest(found, () => closing));
            });
            if (best == null || closing || !settings.AutoConnect || autoPaused || session != null || connecting) return;
            processSnapshot = candidates;
            ProcessSearch.Text = "";
            UpdateProcessPicker(best);
            await ConnectCandidate(best);
        }
        finally { discovering = false; }
    }
    private async void AutoConnectChanged(object sender, RoutedEventArgs e)
    {
        if (updating) return;
        settings = settings with { AutoConnect = AutoConnect.IsChecked == true };
        autoPaused = false;
        try { settings.Save(); }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = Translations.Format("message.Settingchangedforthiswindowbutcouldnotbesaved0.716288d", ex.Message); }
        await TryAutoConnect();
    }
    private static bool IsExpected(Exception ex) => ex is JsonException or Microsoft.Data.Sqlite.SqliteException or IOException or ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException or OperationCanceledException;
    private void Poll(object? sender, EventArgs e)
    {
        UpdateDiscordPresence();
        if (session == null || memory == null) return;
        if (!memory.IsAlive) { Disconnect(); Status.Text = Translations.T("message.GameprocessexitedRefreshandreconnectafterrestarting.a4c1e5d"); return; }
        try
        {
            session.ValidateImage();
            var state = session.ReadPlayer();
            encounterTimer.Context(true,state.Health>0);EncounterSummary.Text=encounterTimer.Summary;
            if (lastActor != 0 && (lastActor != state.Actor || lastComponent != state.Component))
            {
                statDraft = null;durabilityPaused=statusPaused=tuningPaused=fovPaused=false;
                Status.Text = Translations.T("message.PlayerloadedSelectedpracticetogglesresumedandattribu.15354a3");
            }
            lastActor = state.Actor; lastComponent = state.Component;
            HealthValue.Text = $"{state.Health:N0} / {state.MaxHealth:N0}";
            StaminaValue.Text = $"{state.Stamina:N0} / {state.MaxStamina:N0}";
            HealthBar.Maximum = state.MaxHealth; HealthBar.Value = state.Health;
            StaminaBar.Maximum = state.MaxStamina; StaminaBar.Value = state.Stamina;
            if(!advancedPaused)
            {
                try{movementSpeed??=new MovementSpeed(memory,session);worldGravity??=new WorldGravity(memory,session);if(MovementEnabled.IsChecked==true)movementSpeed.Apply((float)MovementSlider.Value);else movementSpeed.Restore();if(GravityEnabled.IsChecked==true)worldGravity.Apply((float)GravitySlider.Value);else worldGravity.Restore();}
                catch(Exception ex)when(IsExpected(ex)){advancedPaused=true;App.LogError(ex);Status.Text=Translations.Format("message.Movementphysicspaused0Togglethesettingtoretry.0de6cdf", ex.Message);}
            }
            try{hudVisibility??=new HudVisibility(memory,session);if(HideHud.IsChecked==true || FreeCameraEnabled.IsChecked==true && PhotoHideHud.IsChecked==true)hudVisibility.Apply();else hudVisibility.Restore();}
            catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);HideHud.IsChecked=false;Status.Text=Translations.Format("message.HUDoverridestopped0.f5a7c37", ex.Message);}
            cameraFov ??= new CameraFov(memory, session);
            if (!fovPaused)
            {
                try
                {
                    FovSlider.Maximum = cameraFov.Maximum;
                    if (FovEnabled.IsChecked == true) cameraFov.Apply((float)FovSlider.Value);
                    else cameraFov.Restore();
                }
                catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); fovPaused=true; Status.Text=Translations.Format("message.FOVpaused0Toggletheoverridetoretry.762beab", ex.Message); }
            }
            if(!tuningPaused)
            {
                try {cameraTuning??=new CameraTuning(memory,session);if(TuneCamera.IsChecked==true)cameraTuning.Apply((float)CameraDistance.Value,(float)CameraHeight.Value);else cameraTuning.Restore();}
                catch(Exception ex)when(IsExpected(ex)){tuningPaused=true;App.LogError(ex);Status.Text=Translations.Format("message.Cameratuningpaused0.e2403c7", ex.Message);}
            }
            if(retryDestination is {} retry && !actionBusy)
            {
                if(DateTime.UtcNow>retryDeadline){retryDestination=null;Status.Text=Translations.T("message.BossretrytimedoutwaitingfortheareaEncounterflagswere.b06282b");}
                else if((retryLoadObserved || state.Actor!=retryOriginActor) && state.Health>0 && unchecked((uint)memory.Int32(state.Actor+0x3F8))==retry.Position.Block){retryDestination=null;_ = FinishBossRetry(retry);}
            }
            if (startupPresetPending && !actionBusy && state.Health > 0)
            {
                startupPresetPending = false;
                try
                {
                    var startup = Library.Presets().FirstOrDefault(x => x.Name.Equals(settings.StartupPresetName,StringComparison.OrdinalIgnoreCase));
                    if (startup == null) Status.Text = Translations.T("message.StartuppresetismissingselectitagaininCombat.fe06ad3");
                    else _ = ApplySavedPreset(startup);
                }
                catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = Translations.Format("message.Startuppresetfailed0.4a16103", ex.Message); }
            }
            UpdateEnemyPractice();
            PlayerControls.IsEnabled = true;
            PollLoot();
            ExplorationControls.IsEnabled = PracticeControls.IsEnabled = true;
            ChaliceControls.IsEnabled = MenuControls.IsEnabled = ItemControls.IsEnabled = WarpControls.IsEnabled = LocationControls.IsEnabled = BossControls.IsEnabled = !actionBusy;
            if (!actionBusy && retryDestination==null && !retryTravelling && DateTime.UtcNow>=durabilityRetryAt && (KeepDurability.IsChecked==true || AutoRepair.IsChecked==true))
                _=RepairWeaponsOnGameThread(false);
            if(!statusPaused && KeepStatusEmpty.IsChecked==true)
            {try{session.ClearStatusBuildup();}catch(Exception ex)when(IsExpected(ex)){statusPaused=true;App.LogError(ex);Status.Text=Translations.Format("message.Statusclearpaused0UseClearnowtoretry.27704a3", ex.Message);}}
            if (KeepHealth.IsChecked == true) session.KeepHealthFull();
            if (InfiniteStamina.IsChecked == true) session.SetInfiniteStamina(true);
            else session.RestoreStamina();
            foreach (var control in FlagControls)
            {
                var flag = Enum.Parse<PracticeFlag>((string)control.Tag);
                if (control.IsChecked == true) session.SetPracticeFlag(flag, true);
                else session.RestorePracticeFlag(flag);
            }
            if (!actionBusy && actions != null && (desiredDamage != 1 || desiredEchoes != 1) &&
                multiplierFailedActor != state.Actor && !actions.MaintainMultipliers())
                _ = ResumeMultipliers(state.Actor);
            var character = session.ReadCharacter();
            EchoValue.Text = character.Values.Echoes.ToString("N0");
            EchoControls.IsEnabled = StatControls.IsEnabled = true;
            if (statDraft == null) PopulateStats(character);
            // A different player invalidates the draft, never silently applies it.
            else if (statDraft.Address != character.Address) { statDraft = null; StatControls.IsEnabled = false; }
        }
        catch (Exception ex) when (IsExpected(ex))
        { App.LogError(ex);
            try { lootOverrides?.Pause(); } catch (Exception pause) when (IsExpected(pause)) { App.LogError(pause); }
            try{StopEnemyPractice();}catch(Exception cleanup)when(IsExpected(cleanup)){App.LogError(cleanup);}
            if(retryTravelling || retryDestination!=null)retryLoadObserved=true;
            encounterTimer.Context(false,false);EncounterSummary.Text=encounterTimer.Summary;
            PlayerControls.IsEnabled = EchoControls.IsEnabled = StatControls.IsEnabled = false;
            ExplorationControls.IsEnabled = PracticeControls.IsEnabled = ChaliceControls.IsEnabled = MenuControls.IsEnabled = ItemControls.IsEnabled = WarpControls.IsEnabled = LocationControls.IsEnabled = BossControls.IsEnabled = false;
            HealthValue.Text = StaminaValue.Text = "— / —"; EchoValue.Text = "—";
            HealthBar.Value = StaminaBar.Value = 0;
            statDraft = null;
            Status.Text = Translations.Format("message.0Selectedpracticetoggleswillresumewhentheplayervalid.18721e2", ex.Message);
        }
    }
    private string? StopFeatures()
    {
        updating = true;
        KeepHealth.IsChecked = InfiniteStamina.IsChecked = KeepDurability.IsChecked = AutoRepair.IsChecked = KeepStatusEmpty.IsChecked = false;
        durabilityPaused=statusPaused=false;
        foreach (var control in FlagControls) control.IsChecked = false;
        updating = false;
        desiredDamage = desiredEchoes = 1; multiplierFailedActor = 0;
        DamageMultiplier.Text = EchoesMultiplier.Text = "1";
        FovEnabled.IsChecked = false;
        TuneCamera.IsChecked = false;
        FreeCameraEnabled.IsChecked=false;FlightEnabled.IsChecked=false;NoclipEnabled.IsChecked=false;HideHud.IsChecked=false;
        MovementEnabled.IsChecked=GravityEnabled.IsChecked=false;advancedPaused=false;
        DiscoveryEnabled.IsChecked=GuaranteedDrops.IsChecked=false;lootPaused=false;
        var errors = new List<string>();
        try { freeCamera?.Restore(); }catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);errors.Add(ex.Message);}
        try { hudVisibility?.Restore(); }catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);errors.Add(ex.Message);}
        try { flight?.Restore(); }catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);errors.Add(ex.Message);}
        try { movementSpeed?.Restore(); } catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);errors.Add(ex.Message);}
        try { lootOverrides?.Restore(); } catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);errors.Add(ex.Message);}
        try { worldGravity?.Restore(); } catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);errors.Add(ex.Message);}
        try { cameraTuning?.Restore(); } catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); errors.Add(ex.Message); }
        try { cameraFov?.Restore(); } catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); errors.Add(ex.Message); }
        beforeParryPractice=null;
        try { StopEnemyPractice(); } catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);errors.Add(ex.Message);}
        try { actions?.ResetMultipliers(); } catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); errors.Add(ex.Message); }
        try { session?.RestoreStamina(); } catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); errors.Add(ex.Message); }
        try { session?.RestorePracticeFlags(); } catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); errors.Add(ex.Message); }
        return errors.Count == 0 ? null : $"Practice restoration could not be verified: {string.Join(" ", errors.Distinct())}";
    }
    private void Disconnect()
    {
        targetGeneration++;targetMonitor.Unavailable("Disconnected");RenderTarget();
        encounterTimer.Context(false,false);EncounterSummary.Text=encounterTimer.Summary;
        retryDestination=null;retryGeneration++;
        timer.Stop(); var cleanupIssue = StopFeatures();
        try { actions?.Dispose(); } catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); cleanupIssue = $"Request dispatcher cleanup: {ex.Message}"; }
        actions = null;
        freeCamera=null;hudVisibility=null;flight=null;movementSpeed=null;worldGravity=null;advancedPaused=false;photoHudRestorePending=false;lootOverrides=null;lootPaused=false;
        cameraFov = null; fovPaused = false;cameraTuning=null;tuningPaused=false;
        memory?.Dispose(); memory = null; session = null; statDraft = null; connectedPid = 0;
        UpdateDiscordPresence();
        lastActor = lastComponent = 0;
        ReleaseOwner();
        PlayerControls.IsEnabled = EchoControls.IsEnabled = StatControls.IsEnabled = false;
        ExplorationControls.IsEnabled = PracticeControls.IsEnabled = ChaliceControls.IsEnabled = MenuControls.IsEnabled = ItemControls.IsEnabled = WarpControls.IsEnabled = LocationControls.IsEnabled = BossControls.IsEnabled = false;
        RefreshButton.IsEnabled = Candidates.IsEnabled = ProcessSearch.IsEnabled = true;
        ConnectButton.Content = Translations.T("ui.Connect.1a2303e"); ConnectionBadge.Text = Translations.T("ui.DISCONNECTED.5541de9");
        ConnectionHint.Text = Translations.T("ui.StartBloodbornePCandloadacharacterthenconnect.ccf1095");
        HealthValue.Text = StaminaValue.Text = "— / —"; EchoValue.Text = "—"; LevelValue.Text = Translations.T("ui.BloodLevel.492e51a");
        HealthBar.Value = StaminaBar.Value = 0;
        Status.Text = cleanupIssue ?? Translations.T("message.DisconnectedHealthechoesandattributechangesalreadyma.4240b3c");
    }
    private void ReleaseOwner()
    {
        if (sessionOwner == null) return;
        sessionOwner.ReleaseMutex(); sessionOwner.Dispose(); sessionOwner = null;
    }
    private bool waitingToClose,finalizingClose;
    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if(finalizingClose){e.Cancel=true;return;}
        if (actionBusy || saveOperationBusy)
        {
            e.Cancel = true;
            if (waitingToClose) return;
            waitingToClose = true; closing = true; timer.Stop(); autoTimer.Stop();
            Status.Text = Translations.T("message.Finishingtheactiveoperationbeforeclosing.466adda");
            while (actionBusy || saveOperationBusy) await Task.Delay(50);
            waitingToClose = false; Close(); return;
        }
        e.Cancel=true;finalizingClose=true;closing = true; updateLifetime.Cancel(); autoTimer.Stop(); Disconnect();
        try{await discordPresence.StopAsync();}
        finally{await Dispatcher.InvokeAsync(()=>{Closing-=OnClosing;Close();});}
    }

    private void OpenGitHub(object sender, RoutedEventArgs e) => OpenWeb(new Uri(AppInfo.RepositoryUrl));
    private void OpenUpdateRelease(object sender, RoutedEventArgs e)
    {
        if (updateRelease != null) OpenWeb(updateRelease);
    }
    private void OpenWeb(Uri uri)
    {
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = ex.Message; }
    }
    private async void CheckForUpdates(object sender, RoutedEventArgs e)
    {
        if (!CheckUpdatesButton.IsEnabled) return;
        CheckUpdatesButton.IsEnabled = false;
        updateRelease = null; UpdateReleaseButton.Visibility = Visibility.Collapsed;
        UpdateStatus.Text = Status.Text = Translations.T("message.CheckingGitHubforupdates.26293f4");
        try
        {
            var result = await UpdateChecker.CheckAsync(updateClient, AppInfo.Version, updateLifetime.Token);
            if (closing) return;
            var newer = result.IsNewerThan(AppInfo.Version);
            UpdateStatus.Text = Status.Text = newer ? Translations.Format("message.BBToolsv0isavailableOpenViewreleasetodownloadit.f15a767", result.LatestVersion.ToString(3)) :
                result.LatestVersion == AppInfo.Version ? Translations.Format("message.Youreuptodatev0.5a57995", AppInfo.DisplayVersion) :
                Translations.Format("message.Yourerunningv0Thelatestpublishedreleaseisv1.f1df4f7", AppInfo.DisplayVersion, result.LatestVersion.ToString(3));
            if (newer) { updateRelease = result.ReleaseUrl; UpdateReleaseButton.Visibility = Visibility.Visible; }
        }
        catch (OperationCanceledException) { if (!closing) UpdateStatus.Text = Status.Text = Translations.T("message.UpdatechecktimedoutTryagainoropenGitHub.58468b9"); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        { App.LogError(ex);
            if (!closing) UpdateStatus.Text = Status.Text = Translations.Format("message.Couldntcheckforupdates0.1ebee32", ex.Message);
        }
        finally { if (!closing) CheckUpdatesButton.IsEnabled = true; }
    }
    private void Run(Action action, string success, [System.Runtime.CompilerServices.CallerMemberName] string operation = "")
    {
        App.Log?.Write("ACTION", $"Start {operation}");
        try { if (session == null) throw new IOException(Translations.T("message.Connecttothegamefirst.bef6b75")); action(); Status.Text = success; }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = ex.Message; }
    }
    private static int Number(TextBox input, string label)
    {
        if (!int.TryParse(input.Text.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException(Translations.Format("message.0mustbeawholenonnegativenumber.881f3c4", label));
        return value;
    }
    private void SetHealth(object sender, RoutedEventArgs e) => Run(() => session!.SetHealth(Number(HealthInput, "Health")), Translations.T("message.Healthupdated.b0303ed"));
    private void Heal(object sender, RoutedEventArgs e) => Run(() => session!.SetHealth(session.ReadPlayer().MaxHealth), Translations.T("message.Healthrestored.d9c546e"));
    private void SetEchoes(object sender, RoutedEventArgs e) => Run(() => session!.SetEchoes(Number(EchoInput, "Blood Echoes")), Translations.T("message.BloodEchoesupdated.a23b0e1"));
    private void AddEchoes(object sender, RoutedEventArgs e) => Run(() => session!.SetEchoes((int)Math.Min(999999999L, (long)session.ReadCharacter().Values.Echoes + 10000)), Translations.T("message.Addedupto10000BloodEchoes.c7006a6"));
    private void StaminaChanged(object sender, RoutedEventArgs e)
    {
        if (updating || session == null) return;
        App.Log?.Write("ACTION", $"Infinite stamina: {InfiniteStamina.IsChecked}");
        try { session.SetInfiniteStamina(InfiniteStamina.IsChecked == true); Status.Text = InfiniteStamina.IsChecked == true ? Translations.T("message.Infinitestaminaenabled.c28be18") : Translations.T("message.Previousstaminaconsumptionsettingrestored.531a8dc"); }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = Translations.Format("message.0Selectionretainedwaitingforavalidplayer.4fa65c1", ex.Message); }
    }
    private CheckBox[] FlagControls => [GodMode, OneShot, Stealth, Silent, NoAi, NoFallDamage, InfiniteItems,IgnoreHits,KeepEchoesOnDeath];
    private void FlagChanged(object sender, RoutedEventArgs e)
    {
        if (updating || session == null || sender is not CheckBox control) return;
        App.Log?.Write("ACTION", $"Combat flag {(string)control.Tag}: {control.IsChecked}");
        try
        {
            session.SetPracticeFlag(Enum.Parse<PracticeFlag>((string)control.Tag), control.IsChecked == true);
            Status.Text = Translations.T("message.Practiceflagupdated.43a53ff");
        }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = Translations.Format("message.0Selectionretainedwaitingforavalidplayer.4fa65c1", ex.Message); }
    }
    private void TurnOffFeatures(object sender, RoutedEventArgs e) => Status.Text = actionBusy ? Translations.T("message.Waitfortheactivegamerequesttofinish.6008a4e") : StopFeatures() ?? Translations.T("message.Practicetogglesswitchedoffandpreviousflagsrestored.4364f9b");
    private void Navigate(object sender, RoutedEventArgs e)
    {
        var page = (string)((Button)sender).Tag;
        if(page=="Photo")page="Player";
        App.Log?.Write("INFO", $"Page: {page}");
        PlayerPage.Visibility = page == "Player" ? Visibility.Visible : Visibility.Collapsed;
        PracticePage.Visibility = page == "Practice" ? Visibility.Visible : Visibility.Collapsed;
        TargetPage.Visibility = page == "Target" ? Visibility.Visible : Visibility.Collapsed;
        ItemsPage.Visibility = page == "Items" ? Visibility.Visible : Visibility.Collapsed;
        TravelPage.Visibility = page == "Travel" ? Visibility.Visible : Visibility.Collapsed;
        WorkshopPage.Visibility = page == "Workshop" ? Visibility.Visible : Visibility.Collapsed;
        PhotoPage.Visibility = page == "Photo" ? Visibility.Visible : Visibility.Collapsed;
        CameraPage.Visibility = page == "Camera" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        PageSubtitle.Text=page=="Photo"?Translations.T("message.Composeashotcaptureittheneditthesavedphoto.7ddfe10"):Translations.T("ui.Shapeyournextpracticerun.33c61d1");
        PageTitle.Text = page switch { "Practice" => Translations.T("message.Combatrewards.31e6a34"),"Items" => Translations.T("message.Itemspawning.f589ae2"),"Travel" => Translations.T("message.Travellocations.b745f7d"),"Workshop" => Translations.T("message.Workshopmenus.f2a05fc"),"Target" => Translations.T("message.Enemydetails.b31585f"),"Camera" => Translations.T("message.Gameplaycamera.e7db326"),"Photo" => Translations.T("ui.Photostudio.bd45bf4"),"Settings" => Translations.T("ui.Settings.74a883a"),_ => Translations.T("ui.Thehunter.86332c5") };
        foreach (var nav in new[] {NavPlayer, NavPractice, NavTarget, NavItems, NavTravel, NavCamera, NavPhoto, NavWorkshop, NavSettings})
            nav.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString((string)nav.Tag == page ? "#3B352A" : "#2B313B"));
        PageScroll.ScrollToTop();
    }
    private void ItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ItemPicker.SelectedItem is not ItemChoice item || ItemCategory == null) return;
        ItemId.Text = item.Id.ToString(); ItemQuantity.Text = "1";
        if (WikiHexId != null) WikiHexId.IsChecked = false;
        foreach (ComboBoxItem category in ItemCategory.Items)
            if (uint.Parse((string)category.Tag) == item.Category) ItemCategory.SelectedItem = category;
    }
    private async Task RunGameAction(Func<GameActions, Task> action, string success, [System.Runtime.CompilerServices.CallerMemberName] string operation = "")
    {
        App.Log?.Write("ACTION", $"Start {operation}");
        if (actions == null || actionBusy) { Status.Text = Translations.T("message.Connecttoaloadedcharacterandwaitfortheactiverequestt.807dca7"); return; }
        if(operation is not (nameof(RetryBoss) or nameof(FinishBossRetry))){retryDestination=null;retryGeneration++;}
        actionBusy = true; ChaliceControls.IsEnabled = MenuControls.IsEnabled = ItemControls.IsEnabled = WarpControls.IsEnabled = LocationControls.IsEnabled = BossControls.IsEnabled = false;
        Status.Text = Translations.T("message.Waitingforthegamethreadtoprocesstherequest.1ac029f");
        try { await action(actions); if (!closing) Status.Text = success; }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); if (!closing) Status.Text = ex.Message; }
        finally { actionBusy = false; }
    }
    private async Task ResumeMultipliers(ulong actor)
    {
        if (actions == null || actionBusy) return;
        actionBusy = true;
        try { await actions.SetMultipliersAsync(desiredDamage, desiredEchoes); }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); multiplierFailedActor = actor; Status.Text = Translations.Format("message.Multiplierspaused0UseApplymultiplierstoretry.db026b7", ex.Message); }
        finally { actionBusy = false; }
    }
    private async void ApplyMultipliers(object sender, RoutedEventArgs e) => await RunGameAction(async engine =>
    {
        if (!float.TryParse(DamageMultiplier.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var damage) ||
            !float.TryParse(EchoesMultiplier.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var echoes))
            throw new ArgumentException(Translations.T("message.Enternumericmultipliersbetween01and100usingadecimalp.bccec26"));
        await engine.SetMultipliersAsync(damage, echoes);
        desiredDamage = damage; desiredEchoes = echoes; multiplierFailedActor = 0;
    }, Translations.T("message.PlayerdamageandenemyEchoesrewardmultipliersapplied.9ca34fc"));
    private void RefreshChalices(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (actions == null) throw new IOException(Translations.T("message.Connecttoaloadedcharacterfirst.291b82a"));
        var selected = (ChalicePicker.SelectedItem as ChaliceAltar)?.Bank ?? 1;
        var altars = actions.ReadChaliceAltars(); ChalicePicker.ItemsSource = altars;
        ChalicePicker.SelectedItem = altars.First(x => x.Bank == selected);
    }, Translations.T("message.Altardatarefreshedfromthecurrentcharacter.15a98e9"));
    private async void OpenChaliceTravel(object sender, RoutedEventArgs e) => await RunGameAction(engine =>
    {
        if (ChalicePicker.SelectedItem is not ChaliceAltar altar) throw new ArgumentException(Translations.T("message.Chooseachalicealtar.5306ccc"));
        if (ChaliceLampPicker.SelectedItem is not ChaliceLamp lamp) throw new ArgumentException(Translations.T("message.Chooseachalicelamp.f2b16b4"));
        return engine.WarpChaliceAsync(altar.Bank, lamp);
    }, Translations.T("message.Chalicelamptravelrequestedusingtheselectedaltarsgene.c46c52a"));
    private async void OpenRemoteMenu(object sender, RoutedEventArgs e) => await RunGameAction(engine =>
    {
        if (MenuPicker.SelectedItem is not MenuChoice menu) throw new ArgumentException(Translations.T("message.Chooseaworkshopmenu.c3d4087"));
        return engine.OpenMenuAsync(menu);
    }, Translations.T("message.MenuopenedCompleteorcloseitnormallyinthegame.a8d87e3"));
    private async void SpawnItem(object sender, RoutedEventArgs e) => await RunGameAction(engine =>
    {
        if (ItemCategory.SelectedItem is not ComboBoxItem category) throw new ArgumentException(Translations.T("message.Chooseanitemcategory.47eb33d"));
        var item = ItemInput.Parse(ItemId.Text, uint.Parse((string)category.Tag), WikiHexId.IsChecked == true);
        return engine.SpawnAsync(item.Id, item.Category, Number(ItemQuantity, "Quantity"));
    }, Translations.T("message.ItemgrantcompletedonthegamethreadCheckinventoryandst.be4825f"));
    private async void Warp(object sender, RoutedEventArgs e) => await RunGameAction(engine =>
    {
        if (LampPicker.SelectedItem is not LampChoice lamp) throw new ArgumentException(Translations.T("message.Choosealamp.076c529"));
        return engine.WarpAsync(lamp);
    }, Translations.T("message.WarprequestedAllowthegametofinishloadingbeforeissuin.6a3c0d2"));
    private void OpenReference(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = ex.Message; }
    }
    private void FilterLamps(object sender, TextChangedEventArgs e)
    {
        if (LampPicker == null) return;
        var selected = LampPicker.SelectedItem as LampChoice;
        var found = GameActions.Lamps.Where(x => x.Label.Contains(LampSearch.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        LampPicker.ItemsSource = found;
        LampPicker.SelectedItem = found.FirstOrDefault(x => x.Entity == selected?.Entity) ?? found.FirstOrDefault();
    }
    private void SaveLocation(object sender, RoutedEventArgs e) => Run(() =>
    {
        var location = session!.ReadLocation(LocationName.Text) with {Group=LocationGroup.Text.Trim()};
        Library.Save(location); RefreshLibrary(); LocationPicker.SelectedItem = location;
    }, Translations.T("message.LocationsavedSavingwiththesamenamereplacesthatbookma.9fef5bf"));
    private void ClearStatusBuildup(object sender,RoutedEventArgs e)=>Run(()=>{statusPaused=false;session!.ClearStatusBuildup();},Translations.T("message.StatusbuildupgaugesclearedExistingactiveeffectsareun.f67f89d"));
    private async void CurePoison(object sender,RoutedEventArgs e)=>await RunGameAction(async engine=>{var count=await engine.CureActivePoisonAsync();App.Log?.Write("ACTION",$"Removed {count} active poison effects");},Translations.T("message.Activeslowrapidpoisonremovedandbuildupcleared.3f44233"));
    private async Task MoveFlight(SavedLocation location)
    {
        if(actions==null||actionBusy)return;actionBusy=true;
        try{await actions.RestoreLocationAsync(location);}catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);FlightEnabled.IsChecked=false;Status.Text=Translations.Format("message.Flightstopped0.a86c602", ex.Message);}finally{actionBusy=false;}
    }
    private async void ReturnFromFlight(object sender,RoutedEventArgs e)
    {
        if(flight?.Anchor is not SavedLocation anchor){Status.Text=Translations.T("message.Noflightreturnpointisavailable.dc0adc5");return;}
        await RunGameAction(async engine=>{await engine.RestoreLocationAsync(anchor);FlightEnabled.IsChecked=false;},Translations.T("message.Returnedtotheflightstartanddisabledflight.cb81c9e"));
    }
    private AdvancedSettings CaptureAdvanced()=>new(MovementEnabled.IsChecked==true,(float)MovementSlider.Value,GravityEnabled.IsChecked==true,(float)GravitySlider.Value,HideHud.IsChecked==true,DiscoveryEnabled.IsChecked==true,(float)DiscoveryBonus.Value,GuaranteedDrops.IsChecked==true);
    private void AdvancedChanged(object sender,RoutedEventArgs e){advancedPaused=false;}
    private void AdvancedValueChanged(object sender,RoutedPropertyChangedEventArgs<double> e){if(MovementValue==null||GravityValue==null)return;MovementValue.Text=Translations.Format("message.Movement000.7208afb", MovementSlider.Value);GravityValue.Text=Translations.Format("message.Gravity0000.2d36093", GravitySlider.Value);advancedPaused=false;}
    private void ResetAdvanced(object sender,RoutedEventArgs e){MovementEnabled.IsChecked=GravityEnabled.IsChecked=false;MovementSlider.Value=GravitySlider.Value=1;advancedPaused=false;try{movementSpeed?.Restore();worldGravity?.Restore();Status.Text=Translations.T("message.Movementandworldgravityrestored.0142882");}catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}}
    private void DurabilityToggled(object sender,RoutedEventArgs e){durabilityPaused=false;}
    private async void RepairWeapons(object sender,RoutedEventArgs e)=>await RepairWeaponsOnGameThread(true);
    private async Task RepairWeaponsOnGameThread(bool manual)
    {
        if(actions==null||actionBusy)return;
        actionBusy=true;durabilityRetryAt=DateTime.UtcNow.AddSeconds(1);
        try
        {
            var reports=await actions.RepairWeaponsNativeAsync(manual||KeepDurability.IsChecked==true?100:Number(RepairThreshold,"Repair threshold"));
            durabilityPaused=false;DurabilitySummary.Text=reports.Length==0?Translations.T("message.Norepairableequippedweaponsdetected.ec56972"):string.Join("\n",reports.Select(x=>x.Label));
            var repaired=reports.Count(x=>x.After>x.Before);
            if(manual||repaired>0){Status.Text=Translations.Format("message.Nativerepair0repaired1equippedweaponsfound.37c48c3", repaired, reports.Length);App.Log?.Write("ACTION",Status.Text+" "+string.Join("; ",reports.Select(x=>x.Label)));}
        }
        catch(Exception ex)when(IsExpected(ex)){durabilityPaused=true;durabilityRetryAt=DateTime.UtcNow.AddSeconds(3);App.LogError(ex);DurabilitySummary.Text=ex.Message;Status.Text=Translations.Format("message.Nativerepairwaiting0.a7b0a8e", ex.Message);}
        finally{actionBusy=false;}
    }
    private async void ReviveBoss(object sender,RoutedEventArgs e)
    {
        await RunGameAction(async engine=>
        {
            if(BossPicker.SelectedItem is not PracticeDestination boss)throw new ArgumentException(Translations.T("message.Chooseabossfirst.28c03fe"));
            var journal=engine.ReviveBoss(boss.Name,firstEncounter:BossFirstEncounter.IsChecked==true);App.Log?.Write("ACTION",$"Revived {boss.Name}; first encounter: {BossFirstEncounter.IsChecked==true}; original flags: {journal}");
            await engine.WarpAsync(GameActions.Lamps.Single(x=>x.Entity==boss.EntryLamp));
        },BossRevivalNotice());
    }
    private async void RetryBoss(object sender,RoutedEventArgs e)
    {
        long generation=0;
        await RunGameAction(async engine=>
        {
            if(BossPicker.SelectedItem is not PracticeDestination boss) throw new ArgumentException(Translations.T("message.Chooseabossfirst.28c03fe"));
            generation=++retryGeneration;retryDestination=null;retryLoadObserved=false;retryOriginActor=session!.ReadPlayer().Actor;
            var journal=engine.ReviveBoss(boss.Name,firstEncounter:BossFirstEncounter.IsChecked==true);App.Log?.Write("ACTION",$"Retry {boss.Name}; first encounter: {BossFirstEncounter.IsChecked==true}; original flags: {journal}");
            retryTravelling=true;
            try{await engine.WarpAsync(GameActions.Lamps.Single(x=>x.Entity==boss.EntryLamp));}
            finally{retryTravelling=false;}
            if(generation==retryGeneration){retryDestination=boss;retryDeadline=DateTime.UtcNow.AddSeconds(90);}
        },Translations.T("message.Bossrevivaltravelrequestcompleted.dbc9f55"));
        if(generation==0)return;
        if(generation!=retryGeneration)Status.Text=Translations.T("message.PendingplacementcancelledAreatravelalreadyrequestedm.cbce444");
        else if(retryDestination!=null)Status.Text=Translations.T("message.BossrevivedWaitingforarealoadingthenentranceplacemen.a865eba");
    }
    private string BossRevivalNotice()=>"Boss revived; area reload requested. "+(BossFirstEncounter.IsChecked==true?"Enter using the original encounter trigger; its intro/fog follows first-visit rules. ":"Enter through the return-fight fog after loading. ")+(NoAi.IsChecked==true||Stealth.IsChecked==true?"Disable enemy AI/invisibility overrides if the boss remains passive. ":"")+"Flag changes can be saved by the game.";
    private Task FinishBossRetry(PracticeDestination boss)=>RunGameAction(async engine=>
    {
        await engine.RestoreLocationAsync(boss.Position);session!.RefillResources();encounterTimer.Context(true,true);encounterTimer.Start(boss.Name);EncounterSummary.Text=encounterTimer.Summary;
    },Translations.T("message.BossretryreadyattheentranceHealthstaminarefilledanda.b769021"));
    private void CancelBossRetry(object sender,RoutedEventArgs e){var pending=retryDestination!=null||retryTravelling;retryDestination=null;retryGeneration++;Status.Text=pending?Translations.T("message.PendingentranceplacementcancelledTravelalreadyreques.f8dddab"):Translations.T("message.Nopendingbossretry.2d06008");}
    private async void TravelBossArea(object sender, RoutedEventArgs e) => await RunGameAction(engine =>
    {
        if (BossPicker.SelectedItem is not PracticeDestination destination) throw new ArgumentException(Translations.T("message.Selectabossentrancefirst.db238e0"));
        return engine.WarpAsync(GameActions.Lamps.Single(x => x.Entity == destination.EntryLamp));
    }, Translations.T("message.AreatravelrequestedAfterloadinguseGotoentrance.071afef"));
    private async void GoToBoss(object sender, RoutedEventArgs e) => await RunGameAction(engine =>
    {
        if (BossPicker.SelectedItem is not PracticeDestination destination) throw new ArgumentException(Translations.T("message.Selectabossentrancefirst.db238e0"));
        return engine.RestoreLocationAsync(destination.Position);
    }, Translations.T("message.Bossentrancerelocationprocessed.29daaa1"));
    private async void RestoreLocation(object sender, RoutedEventArgs e) => await RunGameAction(engine =>
    {
        if (LocationPicker.SelectedItem is not SavedLocation location) throw new ArgumentException(Translations.T("message.Selectasavedlocationfirst.e072131"));
        return engine.RestoreLocationAsync(location);
    }, Translations.T("message.Locationrestoreprocessedonthegamethread.6c1077b"));
    private void DeleteLocation(object sender, RoutedEventArgs e)
    {
        try
        {
            if (LocationPicker.SelectedItem is not SavedLocation location) throw new ArgumentException(Translations.T("message.Selectasavedlocationfirst.e072131"));
            Library.Delete("location", location.Name); RefreshLibrary();
            Status.Text = Translations.T("message.Locationdeleted.1e3c8d7");
        }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = ex.Message; }
    }
    private void CameraTuningChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        tuningPaused=false;
        if(CameraTuningSummary!=null && CameraDistance!=null && CameraHeight!=null)CameraTuningSummary.Text=Translations.Format("message.Distance000Height100.5e95835", CameraDistance.Value, CameraHeight.Value);
    }
    private void CameraTuningToggled(object sender,RoutedEventArgs e)
    {
        tuningPaused=false;if(TuneCamera.IsChecked==true)return;
        try{cameraTuning?.Restore();}catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void FovChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FovValue != null) FovValue.Text = $"{e.NewValue:0}°";
        App.Log?.Write("ACTION", $"FOV selection: {e.NewValue:0} degrees");
        fovPaused = false;
    }
    private void FovToggled(object sender, RoutedEventArgs e)
    {
        fovPaused = false;
        App.Log?.Write("ACTION", $"FOV override: {FovEnabled.IsChecked}; degrees {FovSlider.Value}");
        if (FovEnabled.IsChecked == true) return;
        try { cameraFov?.Restore(); }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = Translations.Format("message.FOVrestorationpending0.710538a", ex.Message); }
    }
    private void ResetFov(object sender, RoutedEventArgs e)
    {
        FovEnabled.IsChecked = false;
        try { cameraFov?.Restore(); Status.Text = Translations.T("message.Originalcameraparametervaluesrestored.241f4cd"); }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = Translations.Format("message.FOVrestorationpending0.710538a", ex.Message); }
    }
    private UserLibrary Library => library ?? throw new IOException(Translations.T("message.SavedlibraryisunavailableCheckthestatusmessageandres.e2043b3"));
    private void RefreshLibrary()
    {
        itemSearchIndex = null;
        var selected = LocationPicker.SelectedItem as SavedLocation;
        LocationPicker.ItemsSource = Library.Locations().Where(x => x.Label.Contains(LocationSearch.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        LocationPicker.SelectedItem = selected;
        if (LocationPicker.SelectedIndex < 0) LocationPicker.SelectedIndex = 0;
        FavoritePicker.ItemsSource=Library.Favorites();FavoritePicker.SelectedIndex=0;
        var preset = PresetPicker.SelectedItem as PracticePreset;
        var presets=Library.Presets().Where(x=>x.ToString().Contains(PresetSearch.Text.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();
        PresetPicker.ItemsSource=presets;PresetPicker.SelectedItem=presets.FirstOrDefault(x=>x.Name.Equals(preset?.Name,StringComparison.OrdinalIgnoreCase));
        if (PresetPicker.SelectedIndex < 0) PresetPicker.SelectedIndex = 0;
    }
    private void SearchLocations(object sender, TextChangedEventArgs e)
    {
        if (library == null) return;
        try { RefreshLibrary(); } catch (Exception ex) when (IsExpected(ex) || ex is JsonException) { App.LogError(ex); Status.Text = ex.Message; }
    }
    private void SavePreset(object sender, RoutedEventArgs e)
    {
        try
        {
            var preset = new PracticePreset(PresetName.Text.Trim(), KeepHealth.IsChecked == true, InfiniteStamina.IsChecked == true,
                FlagControls.Where(x => x.IsChecked == true).Select(x => (string)x.Tag).ToArray(), desiredDamage, desiredEchoes,
                (float)FovSlider.Value, FovEnabled.IsChecked == true, settings with { StartupPresetName = null },KeepDurability.IsChecked==true,AutoRepair.IsChecked==true,Number(RepairThreshold,"Repair threshold"),TuneCamera.IsChecked==true,(float)CameraDistance.Value,(float)CameraHeight.Value,PresetGroup.Text.Trim(),KeepStatusEmpty.IsChecked==true,CaptureAdvanced());
            Library.Save(preset); RefreshLibrary(); PresetPicker.SelectedItem = PresetPicker.Items.Cast<PracticePreset>().FirstOrDefault(x=>x.Name.Equals(preset.Name,StringComparison.OrdinalIgnoreCase));
            Status.Text = Translations.T("message.CurrentactivesettingssavedAmatchingnamereplacestheex.628a437");
        }
        catch (Exception ex) when (IsExpected(ex) || ex is JsonException) { App.LogError(ex); Status.Text = ex.Message; }
    }
    private async void ApplyPreset(object sender, RoutedEventArgs e)
    {
        if (PresetPicker.SelectedItem is not PracticePreset preset) { Status.Text = Translations.T("message.Chooseasavedpreset.67169d4"); return; }
        await ApplySavedPreset(preset);
    }
    private Task ApplySavedPreset(PracticePreset preset) => RunGameAction(async engine =>
    {
        preset.Validate(); session!.ReadPlayer();
        await engine.SetMultipliersAsync(preset.Damage, preset.Echoes);
        desiredDamage = preset.Damage; desiredEchoes = preset.Echoes; multiplierFailedActor = 0;
        updating = true;
        try
        {
            if (preset.Preferences != null)
            {
                var next = preset.Preferences with { StartupPresetName = settings.StartupPresetName, Language = settings.Language };
                next.Save(); settings = next;
                AutoConnect.IsChecked = settings.AutoConnect; StartupFov.IsChecked = settings.StartupFovEnabled;
                LoadDiscordControls();UpdateDiscordPresence();
                LoadHotkeyControls();LoadCameraControls();LoadPhotoControls();hotkeys?.Apply(settings.Hotkeys??new());
            }
            CameraDistance.Value=preset.CameraDistance;CameraHeight.Value=preset.CameraHeight;TuneCamera.IsChecked=preset.TuneCamera;tuningPaused=false;
            var advanced=preset.Advanced??new();MovementSlider.Value=advanced.Movement;GravitySlider.Value=advanced.Gravity;MovementEnabled.IsChecked=advanced.MovementEnabled;GravityEnabled.IsChecked=advanced.GravityEnabled;HideHud.IsChecked=advanced.HideHud;advancedPaused=false;
            DiscoveryEnabled.IsChecked=advanced.DiscoveryEnabled;DiscoveryBonus.Value=advanced.DiscoveryBonus;GuaranteedDrops.IsChecked=advanced.GuaranteedDrops;lootPaused=false;
            FovSlider.Value = preset.Fov; FovEnabled.IsChecked = preset.OverrideFov; fovPaused = false;
            if (!preset.OverrideFov) cameraFov?.Restore();
            UpdateStartupSummary();
            KeepStatusEmpty.IsChecked=preset.KeepStatusEmpty;statusPaused=false;
            KeepDurability.IsChecked=preset.KeepDurability;AutoRepair.IsChecked=preset.AutoRepair;RepairThreshold.Text=preset.RepairThreshold.ToString();durabilityPaused=false;
            KeepHealth.IsChecked = preset.KeepHealth; InfiniteStamina.IsChecked = preset.Stamina;
            foreach (var control in FlagControls) control.IsChecked = preset.Flags.Contains((string)control.Tag);
            DamageMultiplier.Text = preset.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture);
            EchoesMultiplier.Text = preset.Echoes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        finally { updating = false; }
    }, Translations.T("message.PresetselectedCombatsettingsresumewhiletheplayerisva.f722ff9"));
    private void DeletePreset(object sender, RoutedEventArgs e)
    {
        try
        {
            if (PresetPicker.SelectedItem is not PracticePreset preset) throw new ArgumentException(Translations.T("message.Chooseasavedpreset.67169d4"));
            if (preset.Name.Equals(settings.StartupPresetName,StringComparison.OrdinalIgnoreCase)) ClearStartupPreset(sender,e);
            Library.Delete("preset",preset.Name); RefreshLibrary(); Status.Text = Translations.T("message.Presetdeletedactivecombatsettingsareunchanged.d723239");
        }
        catch (Exception ex) when (IsExpected(ex) || ex is JsonException) { App.LogError(ex); Status.Text = ex.Message; }
    }
    private void ExportDiagnostics(object sender,RoutedEventArgs e)
    {
        var picker=new SaveFileDialog{Filter="Diagnostics (*.json)|*.json",FileName=$"BBTools-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.json"};if(picker.ShowDialog()!=true)return;
        try
        {
            var status=Diagnostics.RedactPaths(Status.Text);
            var snapshot=new{AppVersion=AppInfo.DisplayVersion,CreatedUtc=DateTime.UtcNow,OS=Environment.OSVersion.ToString(),Runtime=Environment.Version.ToString(),Connected=session!=null,GameVersion="CUSA03173 1.09",AutoConnect=settings.AutoConnect,Fov=FovSlider.Value,FovOverride=FovEnabled.IsChecked==true,LastStatus=status};
            File.WriteAllText(picker.FileName,JsonSerializer.Serialize(snapshot,new JsonSerializerOptions{WriteIndented=true}));Status.Text=Translations.T("message.Diagnosticsexportedwithoutsavedataorpersonalpaths.93aae61");
        }catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private ComboBox[] HotkeyControls=>[HotkeyHeal,HotkeyGod,HotkeySave,HotkeyRestore,HotkeyRetry];
    private void LoadHotkeyControls()
    {
        var preferences=settings.Hotkeys??new();var keys=Enumerable.Range(1,12).Select(i=>$"F{i}").ToArray();HotkeysEnabled.IsChecked=preferences.Enabled;HotkeyModifiers.ItemsSource=HotkeyPreferences.ModifierChoices;HotkeyModifiers.SelectedItem=preferences.Modifiers;UpdateHotkeySummary(preferences);
        for(var i=0;i<HotkeyControls.Length;i++){HotkeyControls[i].ItemsSource=keys;HotkeyControls[i].SelectedItem=preferences.Keys[i].ToUpperInvariant();}
    }
    private void InitializeHotkeys()
    {
        try{hotkeys??=new HotkeyManager(new System.Windows.Interop.WindowInteropHelper(this).Handle,HotkeyPressed);hotkeys.Apply(settings.Hotkeys??new());}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=Translations.Format("message.Hotkeysunavailable0.088f835", ex.Message);}
    }
    private void SaveHotkeys(object sender,RoutedEventArgs e)
    {
        try
        {
            var keys=HotkeyControls.Select(x=>(string?)x.SelectedItem??"").ToArray();var prefs=new HotkeyPreferences(HotkeysEnabled.IsChecked==true,keys[0],keys[1],keys[2],keys[3],keys[4],(string?)HotkeyModifiers.SelectedItem??"Ctrl+Alt");prefs.Validate();
            hotkeys?.Apply(prefs);var next=settings with{Hotkeys=prefs};next.Save();settings=next;UpdateHotkeySummary(prefs);Status.Text=prefs.Enabled?Translations.T("message.Hotkeysactive.b7cbe6a")+HotkeySummary.Text:Translations.T("message.Hotkeysdisabled.5337d57");
        }catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void UpdateHotkeySummary(HotkeyPreferences prefs)=>HotkeySummary.Text=Translations.Format("message.0Heal1God2Save3Restore4Retry5.fc25415", (prefs.Enabled?"On":"Off"), prefs.Shortcut(prefs.Heal), prefs.Shortcut(prefs.God), prefs.Shortcut(prefs.Save), prefs.Shortcut(prefs.Restore), prefs.Shortcut(prefs.Retry));
    private async void HotkeyPressed(int action)
    {
        if(closing || connecting || actionBusy || session==null || FreeCameraEnabled.IsChecked==true)return;
        var foreground=HotkeyManager.ForegroundProcess();if(foreground!=connectedPid && foreground!=Environment.ProcessId)return;
        if(foreground==Environment.ProcessId && HotkeyManager.ForegroundWindow!=new System.Windows.Interop.WindowInteropHelper(this).Handle)return;
        if(foreground==Environment.ProcessId && (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase or ComboBox {IsDropDownOpen:true} or ComboBox {IsEditable:true}))return;
        switch(action)
        {
            case 0:Heal(this,new());break;
            case 1:GodMode.IsChecked=GodMode.IsChecked!=true;break;
            case 2:Run(()=>{Library.Save(session.ReadLocation("Quick bookmark"));RefreshLibrary();},Translations.T("message.Quickbookmarksaved.9fad55d"));break;
            case 3:await RunGameAction(engine=>engine.RestoreLocationAsync(Library.Locations().FirstOrDefault(x=>x.Name=="Quick bookmark")??throw new ArgumentException(Translations.T("message.SaveaQuickbookmarkfirst.8e0f4a8"))),Translations.T("message.Quickbookmarkrestored.7bdfba8"));break;
            case 4:RetryBoss(this,new());break;
        }
    }
    private void StartAttempt(object sender,RoutedEventArgs e)=>Run(()=>{if(session!.ReadPlayer().Health==0)throw new IOException(Translations.T("message.Loadalivingcharacterbeforestartinganattempt.e1dd61b"));encounterTimer.Start((BossPicker.SelectedItem as PracticeDestination)?.Name??"Practice");EncounterSummary.Text=encounterTimer.Summary;},Translations.T("message.Attempttimerstarted.f2ba5c7"));
    private void PauseAttempt(object sender,RoutedEventArgs e){encounterTimer.TogglePause();EncounterSummary.Text=encounterTimer.Summary;}
    private void FinishAttempt(object sender,RoutedEventArgs e){encounterTimer.Finish();EncounterSummary.Text=encounterTimer.Summary;}
    private void ResetAttempt(object sender,RoutedEventArgs e){encounterTimer.Reset();EncounterSummary.Text=encounterTimer.Summary;}
    private void SearchPresets(object sender,TextChangedEventArgs e)
    {
        if(library==null || PresetPicker==null)return;
        try{RefreshLibrary();}catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void SearchItems(object sender,TextChangedEventArgs e)
    {
        if(ItemPicker==null)return;
        var query=ItemSearch.Text.Trim();
        try
        {
            itemSearchIndex ??= new ItemSearchIndex(library == null
                ? GameActions.CommonItems
                : Library.Catalog().Concat(GameActions.CommonItems).Concat(Library.Favorites().Select(x => new ItemChoice(x.Name,x.Id,x.Category))));
            ItemPicker.ItemsSource = itemSearchIndex.Search(query);
        }catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void ImportGameCatalog(object sender,RoutedEventArgs e)
    {
        var picker=new OpenFolderDialog{Title=Translations.T("message.ChooseBloodbornegamefolderordvdrootps4.466699e")};if(picker.ShowDialog()!=true)return;
        try{var items=GameItemCatalog.Load(picker.FolderName);Library.SaveCatalog(items);itemSearchIndex=null;SearchItems(this,new TextChangedEventArgs(TextBox.TextChangedEvent,System.Windows.Controls.UndoAction.None));Status.Text=Translations.Format("message.Imported0N0itemnamesfromyourgameNoitemswerespawned.0843e70", items.Length);}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void SaveFavorite(object sender,RoutedEventArgs e)
    {
        try
        {
            if(ItemCategory.SelectedItem is not ComboBoxItem category)throw new ArgumentException(Translations.T("message.Chooseanitemcategory.47eb33d"));
            var parsed=ItemInput.Parse(ItemId.Text,uint.Parse((string)category.Tag),WikiHexId.IsChecked==true);
            Library.Save(new FavoriteItem(FavoriteName.Text.Trim(),parsed.Id,parsed.Category,Number(ItemQuantity,"Quantity")));RefreshLibrary();Status.Text=Translations.T("message.Itemfavoritesaved.f2e3fc9");
        }
        catch(Exception ex) when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void LoadFavorite(object sender,RoutedEventArgs e)
    {
        if(FavoritePicker.SelectedItem is not FavoriteItem item){Status.Text=Translations.T("message.Chooseafavoritefirst.d7249cb");return;}
        WikiHexId.IsChecked=false;ItemId.Text=item.Id.ToString();ItemQuantity.Text=item.Quantity.ToString();
        foreach(ComboBoxItem category in ItemCategory.Items)if(uint.Parse((string)category.Tag)==item.Category)ItemCategory.SelectedItem=category;
        Status.Text=Translations.T("message.FavoriteloadedUseSpawnitemtograntit.cc5be1c");
    }
    private void DeleteFavorite(object sender,RoutedEventArgs e)
    {
        try{if(FavoritePicker.SelectedItem is not FavoriteItem item)throw new ArgumentException(Translations.T("message.Chooseafavoritefirst.d7249cb"));Library.Delete("favorite",item.Name);RefreshLibrary();Status.Text=Translations.T("message.Favoritedeleted.929fd4b");}
        catch(Exception ex) when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void ExportLibrary(object sender,RoutedEventArgs e)
    {
        var picker=new SaveFileDialog{Filter="BB Tools library (*.json)|*.json",FileName="BBTools-library.json"};if(picker.ShowDialog()!=true)return;
        try{Library.Export(picker.FileName);Status.Text=Translations.T("message.Libraryexported.fcca287");}catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void ImportLibrary(object sender,RoutedEventArgs e)
    {
        var picker=new OpenFileDialog{Filter="BB Tools library (*.json)|*.json"};if(picker.ShowDialog()!=true)return;
        try{Library.Import(picker.FileName);RefreshLibrary();Status.Text=Translations.T("message.Libraryimportednogameactionswereperformed.69687f1");}catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void BackupLibrary(object sender,RoutedEventArgs e)
    {
        var picker=new SaveFileDialog{Filter="SQLite backup (*.db)|*.db",FileName=$"BBTools-library-{DateTime.Now:yyyyMMdd-HHmmss}.db"};if(picker.ShowDialog()!=true)return;
        try{Library.Backup(picker.FileName);Status.Text=Translations.T("message.Databasebackupcreated.826a284");}catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void RenamePreset(object sender,RoutedEventArgs e)
    {
        try
        {
            if(PresetPicker.SelectedItem is not PracticePreset preset)throw new ArgumentException(Translations.T("message.Chooseasavedpreset.67169d4"));
            var name=PresetName.Text.Trim();Library.RenamePreset(preset.Name,name);
            if(preset.Name.Equals(settings.StartupPresetName,StringComparison.OrdinalIgnoreCase)){var next=settings with{StartupPresetName=name};next.Save();settings=next;UpdateStartupSummary();}
            RefreshLibrary();PresetPicker.SelectedItem=PresetPicker.Items.Cast<PracticePreset>().FirstOrDefault(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase));Status.Text=Translations.T("message.Presetrenamed.eae02dd");
        }catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void DuplicatePreset(object sender,RoutedEventArgs e)
    {
        try
        {
            if(PresetPicker.SelectedItem is not PracticePreset preset)throw new ArgumentException(Translations.T("message.Chooseasavedpreset.67169d4"));
            var name=PresetName.Text.Trim();if(Library.Presets().Any(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException(Translations.T("message.Enteranewunusedpresetname.78416da"));
            Library.Save(preset with{Name=name});RefreshLibrary();PresetPicker.SelectedItem=PresetPicker.Items.Cast<PracticePreset>().FirstOrDefault(x=>x.Name.Equals(name,StringComparison.OrdinalIgnoreCase));Status.Text=Translations.T("message.Presetduplicated.e745cf4");
        }catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void UpdateStartupSummary() => StartupSummary.Text = Translations.Format("message.SavedstartupFOV001Startuppreset2.760f889", settings.StartupFov, (settings.StartupFovEnabled ? "on" : "off"), settings.StartupPresetName ?? "none");
    private void SaveStartupFov(object sender,RoutedEventArgs e)
    {
        try { var next=settings with { StartupFovEnabled=StartupFov.IsChecked==true,StartupFov=(float)FovSlider.Value }; next.Save();settings=next;UpdateStartupSummary();Status.Text=Translations.T("message.StartupFOVpreferencesaved.ca9c381"); }
        catch(Exception ex) when(IsExpected(ex)) { App.LogError(ex);Status.Text=ex.Message; }
    }
    private void SetStartupPreset(object sender,RoutedEventArgs e)
    {
        try
        {
            if(PresetPicker.SelectedItem is not PracticePreset preset) throw new ArgumentException(Translations.T("message.Selectasavedpresetfirst.628bb86"));
            var next=settings with { StartupPresetName=preset.Name };next.Save();settings=next;UpdateStartupSummary();Status.Text=Translations.T("message.Presetwillapplyafteralivingcharacterisreadyonthenext.4efe854");
        }
        catch(Exception ex) when(IsExpected(ex)) { App.LogError(ex);Status.Text=ex.Message; }
    }
    private void ClearStartupPreset(object sender,RoutedEventArgs e)
    {
        try { var next=settings with {StartupPresetName=null};next.Save();settings=next;startupPresetPending=false;UpdateStartupSummary();Status.Text=Translations.T("message.Startuppresetcleared.4f4629f"); }
        catch(Exception ex) when(IsExpected(ex)) { App.LogError(ex);Status.Text=ex.Message; }
    }
    private TextBox[] AttributeInputs => [Vitality, Endurance, Strength, Skill, Bloodtinge, Arcane];
    private void PopulateStats(CharacterState character)
    {
        statDraft = character;
        LevelValue.Text = Translations.Format("message.BloodLevel0.9907fab", character.Level);
        var inputs = AttributeInputs;
        for (var i = 0; i < inputs.Length; i++) inputs[i].Text = character.Values.Attributes[i].ToString();
    }
    private void LoadStats(object sender, RoutedEventArgs e) => Run(() => PopulateStats(session!.ReadCharacter()), Translations.T("message.Currentattributesloadedintotheeditor.2164ff1"));
    private void ApplyStats(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (statDraft == null) throw new IOException(Translations.T("message.Reloadcurrentattributesfirst.e9af61c"));
        var values = AttributeInputs.Select(x => Number(x, x.Name)).ToArray();
        session!.SetAttributes(values, statDraft);
        PopulateStats(session.ReadCharacter());
    }, Translations.T("message.AttributesappliedQuittotitleandreloadyourcharacterto.d1f5e77"));
    private async void Backup(object sender, RoutedEventArgs e)
    {
        if (!GameClosedForSaves()) { Status.Text = Translations.T("message.DisconnectandcloseBloodborneanditslauncherbeforeback.7b1c2a6"); return; }
        var source = new OpenFolderDialog { Title = Translations.T("message.ChooseBloodbornesavefolderclosethegamefirst.ac6abd7") };
        if (source.ShowDialog(this) != true) return;
        var target = new OpenFolderDialog { Title = Translations.T("message.Choosebackupdestinationoutsidethesavefolder.115324d") };
        if (target.ShowDialog(this) != true) return;
        SetSaveOperationBusy(true);
        try { var result = await Task.Run(() => {if(!GameClosedForSaves())throw new IOException(Translations.T("message.CloseBloodborneanditslauncherbeforebackup.69bd078"));return CreateBackup(source.FolderName, target.FolderName);}); backupHistoryFolder=target.FolderName;BackupHistory.ItemsSource=SaveRestore.History(backupHistoryFolder);BackupHistory.SelectedIndex=0;Status.Text = Translations.Format("message.Backupcreated0.1e5f38c", result); }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = Translations.Format("message.Backupfailed0.9e8e99a", ex.Message); }
        finally { SetSaveOperationBusy(false); }
    }
    private void SetSaveOperationBusy(bool value)
    {
        saveOperationBusy=value;BackupButton.IsEnabled=RestoreBackupButton.IsEnabled=ConnectButton.IsEnabled=RefreshButton.IsEnabled=Candidates.IsEnabled=ProcessSearch.IsEnabled=!value;
    }
    private bool GameClosedForSaves()=>!connecting && !discovering && session==null && !ProcessDiscovery.List().Any(x=>ProcessDiscovery.Matches(x.Name)||knownRuntimes.Any(known=>known.Id==x.Id&&(x.Started==0||known.Started==x.Started)));
    private void ChooseBackupFolder(object sender,RoutedEventArgs e)
    {
        var picker=new OpenFolderDialog{Title=Translations.T("message.ChooseBBToolssavebackupfolder.106bb8a")};if(picker.ShowDialog(this)!=true)return;
        try{backupHistoryFolder=picker.FolderName;BackupHistory.ItemsSource=SaveRestore.History(backupHistoryFolder);BackupHistory.SelectedIndex=0;Status.Text=Translations.T("message.Backuphistoryloaded.01477ed");}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private async void RestoreSaveBackup(object sender,RoutedEventArgs e)
    {
        if(!GameClosedForSaves()){Status.Text=Translations.T("message.DisconnectandcloseBloodborneanditslauncherbeforerest.2706ffe");return;}
        if(BackupHistory.SelectedItem is not SaveBackup backup){Status.Text=Translations.T("message.Chooseabackupfirst.d0c2076");return;}
        var picker=new OpenFolderDialog{Title=Translations.T("message.Choosetheactualsavefoldertoreplacearecoverycopywillb.fafeb12")};if(picker.ShowDialog(this)!=true)return;
        SetSaveOperationBusy(true);
        try
        {
            var result=await Task.Run(()=>SaveRestore.Restore(backup.Path,picker.FolderName,()=>GameClosedForSaves()));Status.Text=Translations.Format("message.SaverestoredPreviousfolder0RecoveryZIP1.e836063", result.RecoveryFolder, result.RecoveryZip);
        }
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
        finally{SetSaveOperationBusy(false);}
    }
    public static string CreateBackup(string source, string destination)
    {
        source = Path.GetFullPath(source); destination = Path.GetFullPath(destination);
        var relative = Path.GetRelativePath(source, destination);
        if (relative == "." || (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar))) throw new ArgumentException(Translations.T("message.Backupdestinationmustbeoutsidethesavefolder.f824ef4"));
        Validate(new DirectoryInfo(source));
        var archive = Path.Combine(destination, $"bb-tools-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
        var temporary = archive + ".partial";
        try { ZipFile.CreateFromDirectory(source, temporary); File.Move(temporary, archive); return archive; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Validate(DirectoryInfo directory)
    {
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException(Translations.T("message.Linksandjunctionsareunsupportedinsavefolders.e5b16c3"));
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException(Translations.T("message.Linksandjunctionsareunsupportedinsavefolders.e5b16c3"));
            if (entry is DirectoryInfo child) Validate(child);
        }
    }
}
