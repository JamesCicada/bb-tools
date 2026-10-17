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
    private CharacterState? statDraft;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private bool updating, closing, connecting, discovering, autoPaused;
    private AppSettings settings = AppSettings.Load();
    private LocationBook locationBook = LocationBook.Load();
    private readonly DispatcherTimer autoTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private int connectedPid;
    private ulong lastActor, lastComponent;
    private Mutex? sessionOwner;
    private static readonly HttpClient updateClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly CancellationTokenSource updateLifetime = new();
    private Uri? updateRelease;
    public MainWindow()
    {
        InitializeComponent();
        Title = $"BB Tools v{AppInfo.DisplayVersion} · Hunter's Workshop";
        VersionLabel.Text = $"VERSION {AppInfo.DisplayVersion}";
        AboutVersion.Text = $"BB Tools v{AppInfo.DisplayVersion}";
        ItemPicker.ItemsSource = GameActions.CommonItems;
        ItemPicker.SelectedIndex = 0;
        LampPicker.ItemsSource = GameActions.Lamps;
        LampPicker.SelectedIndex = 0;
        LocationPicker.ItemsSource = locationBook.Locations;
        LocationPicker.SelectedIndex = 0;
        BossPicker.ItemsSource = BossDestinations.All; BossPicker.SelectedIndex = 0;
        timer.Tick += Poll;
        Closing += OnClosing;
        Refresh(this, new RoutedEventArgs());
        updating = true; AutoConnect.IsChecked = settings.AutoConnect; updating = false;
        autoTimer.Tick += async (_, _) => await TryAutoConnect();
        Loaded += async (_, _) => { autoTimer.Start(); await TryAutoConnect(); };
    }
    private void Refresh(object sender, RoutedEventArgs e)
    {
        var selected = (Candidates.SelectedItem as ProcessCandidate)?.Id;
        var found = ProcessDiscovery.List();
        Candidates.ItemsSource = found.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).ToArray();
        Candidates.SelectedItem = found.FirstOrDefault(x => x.Id == selected) ?? found.FirstOrDefault(x => x.Name.Contains("probe", StringComparison.OrdinalIgnoreCase)) ?? found.FirstOrDefault();
        Status.Text = found.Length == 0 ? "No matching processes. Start Bloodborne PC, load a character, then refresh." : $"Found {found.Length} matching process(es). Select the game engine and connect.";
    }
    private async void Connect(object sender, RoutedEventArgs e)
    {
        if (session != null) { autoPaused = true; Disconnect(); return; }
        if (Candidates.SelectedItem is not ProcessCandidate candidate) { Status.Text = "Select a running game process first."; return; }
        autoPaused = false;
        await ConnectCandidate(candidate);
    }
    private async Task ConnectCandidate(ProcessCandidate candidate)
    {
        if (connecting || closing || session != null) return;
        connecting = true;
        if (candidate.Name.Contains("shadps4", StringComparison.OrdinalIgnoreCase)) { connecting = false; Status.Text = "Live controls currently target Bloodborne PC 1.09. The shadPS4 adapter is not implemented yet."; return; }
        ConnectButton.IsEnabled = RefreshButton.IsEnabled = Candidates.IsEnabled = false;
        Status.Text = "Checking the loaded game image and player structures…";
        ProcessMemory? pending = null;
        try
        {
            using var process = Process.GetProcessById(candidate.Id);
            if (process.StartTime.ToUniversalTime().Ticks != candidate.Started) throw new IOException("The selected process was replaced. Refresh and select it again.");
            sessionOwner = new Mutex(false, $"Local\\BBTools.Player.{candidate.Id}.{candidate.Started}");
            var acquired = false;
            try { acquired = sessionOwner.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) { sessionOwner.Dispose(); sessionOwner = null; throw new IOException("Another BB Tools window is already connected to this process."); }
            pending = new ProcessMemory(candidate.Id);
            var opened = pending;
            var image = await Task.Run(() => PlayerSession.FindImage(opened, opened.AllocationBases()));
            if (closing) { pending.Dispose(); ReleaseOwner(); return; }
            session = new PlayerSession(opened, image);
            actions = new GameActions(opened, session);
            memory = opened; pending = null; connectedPid = candidate.Id;
            ConnectionBadge.Text = "CONNECTED";
            ConnectionHint.Text = $"{candidate.Label} · Bloodborne 1.09 instructions matched";
            ConnectButton.Content = "Disconnect";
            timer.Start();
            Status.Text = "Connected. Features start off. Player controls become available when a character is loaded.";
            Poll(this, EventArgs.Empty);
        }
        catch (Exception ex) when (IsExpected(ex)) { pending?.Dispose(); ReleaseOwner(); Status.Text = ex.Message; }
        finally
        {
            connecting = false;
            ConnectButton.IsEnabled = true;
            RefreshButton.IsEnabled = Candidates.IsEnabled = session == null;
        }
    }
    private async Task TryAutoConnect()
    {
        if (!settings.AutoConnect || autoPaused || closing || session != null || connecting || discovering) return;
        discovering = true;
        try
        {
            var candidates = ProcessDiscovery.List();
            var best = await Task.Run(() => ProcessDiscovery.FindBest(candidates, () => closing));
            if (best == null || closing || !settings.AutoConnect || autoPaused || session != null || connecting) return;
            Candidates.ItemsSource = candidates;
            Candidates.SelectedItem = best;
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
        catch (Exception ex) when (IsExpected(ex)) { Status.Text = $"Setting changed for this window, but could not be saved: {ex.Message}"; }
        await TryAutoConnect();
    }
    private static bool IsExpected(Exception ex) => ex is IOException or ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException or OperationCanceledException;
    private void Poll(object? sender, EventArgs e)
    {
        if (session == null || memory == null) return;
        if (!memory.IsAlive) { Disconnect(); Status.Text = "Game process exited. Refresh and reconnect after restarting."; return; }
        try
        {
            session.ValidateImage();
            var state = session.ReadPlayer();
            if (lastActor != 0 && (lastActor != state.Actor || lastComponent != state.Component))
            {
                statDraft = null;
                Status.Text = "Player loaded. Selected practice toggles resumed and attributes reloaded.";
            }
            lastActor = state.Actor; lastComponent = state.Component;
            HealthValue.Text = $"{state.Health:N0} / {state.MaxHealth:N0}";
            StaminaValue.Text = $"{state.Stamina:N0} / {state.MaxStamina:N0}";
            HealthBar.Maximum = state.MaxHealth; HealthBar.Value = state.Health;
            StaminaBar.Maximum = state.MaxStamina; StaminaBar.Value = state.Stamina;
            PlayerControls.IsEnabled = true;
            PracticeControls.IsEnabled = true;
            ItemControls.IsEnabled = WarpControls.IsEnabled = LocationControls.IsEnabled = BossControls.IsEnabled = !actionBusy;
            if (KeepHealth.IsChecked == true) session.KeepHealthFull();
            if (InfiniteStamina.IsChecked == true) session.SetInfiniteStamina(true);
            else session.RestoreStamina();
            foreach (var control in FlagControls)
            {
                var flag = Enum.Parse<PracticeFlag>((string)control.Tag);
                if (control.IsChecked == true) session.SetPracticeFlag(flag, true);
                else session.RestorePracticeFlag(flag);
            }
            var character = session.ReadCharacter();
            EchoValue.Text = character.Values.Echoes.ToString("N0");
            EchoControls.IsEnabled = StatControls.IsEnabled = true;
            if (statDraft == null) PopulateStats(character);
            // A different player invalidates the draft, never silently applies it.
            else if (statDraft.Address != character.Address) { statDraft = null; StatControls.IsEnabled = false; }
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            PlayerControls.IsEnabled = EchoControls.IsEnabled = StatControls.IsEnabled = false;
            PracticeControls.IsEnabled = ItemControls.IsEnabled = WarpControls.IsEnabled = LocationControls.IsEnabled = BossControls.IsEnabled = false;
            HealthValue.Text = StaminaValue.Text = "— / —"; EchoValue.Text = "—";
            HealthBar.Value = StaminaBar.Value = 0;
            statDraft = null;
            Status.Text = $"{ex.Message} Selected practice toggles will resume when the player validates.";
        }
    }
    private string? StopFeatures()
    {
        updating = true;
        KeepHealth.IsChecked = InfiniteStamina.IsChecked = false;
        foreach (var control in FlagControls) control.IsChecked = false;
        updating = false;
        var errors = new List<string>();
        try { session?.RestoreStamina(); } catch (Exception ex) when (IsExpected(ex)) { errors.Add(ex.Message); }
        try { session?.RestorePracticeFlags(); } catch (Exception ex) when (IsExpected(ex)) { errors.Add(ex.Message); }
        return errors.Count == 0 ? null : $"Practice restoration could not be verified: {string.Join(" ", errors.Distinct())}";
    }
    private void Disconnect()
    {
        timer.Stop(); var cleanupIssue = StopFeatures();
        try { actions?.Dispose(); } catch (Exception ex) when (IsExpected(ex)) { cleanupIssue = $"Request dispatcher cleanup: {ex.Message}"; }
        actions = null;
        memory?.Dispose(); memory = null; session = null; statDraft = null; connectedPid = 0;
        lastActor = lastComponent = 0;
        ReleaseOwner();
        PlayerControls.IsEnabled = EchoControls.IsEnabled = StatControls.IsEnabled = false;
        PracticeControls.IsEnabled = ItemControls.IsEnabled = WarpControls.IsEnabled = LocationControls.IsEnabled = BossControls.IsEnabled = false;
        RefreshButton.IsEnabled = Candidates.IsEnabled = true;
        ConnectButton.Content = "Connect"; ConnectionBadge.Text = "DISCONNECTED";
        ConnectionHint.Text = "Start Bloodborne PC and load a character, then connect.";
        HealthValue.Text = StaminaValue.Text = "— / —"; EchoValue.Text = "—"; LevelValue.Text = "Blood Level —";
        HealthBar.Value = StaminaBar.Value = 0;
        Status.Text = cleanupIssue ?? "Disconnected. Health, echoes and attribute changes already made remain in the game.";
    }
    private void ReleaseOwner()
    {
        if (sessionOwner == null) return;
        sessionOwner.ReleaseMutex(); sessionOwner.Dispose(); sessionOwner = null;
    }
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) { closing = true; updateLifetime.Cancel(); autoTimer.Stop(); Disconnect(); }

    private void OpenGitHub(object sender, RoutedEventArgs e) => OpenWeb(new Uri(AppInfo.RepositoryUrl));
    private void OpenUpdateRelease(object sender, RoutedEventArgs e)
    {
        if (updateRelease != null) OpenWeb(updateRelease);
    }
    private void OpenWeb(Uri uri)
    {
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (IsExpected(ex)) { Status.Text = ex.Message; }
    }
    private async void CheckForUpdates(object sender, RoutedEventArgs e)
    {
        if (!CheckUpdatesButton.IsEnabled) return;
        CheckUpdatesButton.IsEnabled = false;
        updateRelease = null; UpdateReleaseButton.Visibility = Visibility.Collapsed;
        UpdateStatus.Text = Status.Text = "Checking GitHub for updates…";
        try
        {
            var result = await UpdateChecker.CheckAsync(updateClient, AppInfo.Version, updateLifetime.Token);
            if (closing) return;
            var newer = result.IsNewerThan(AppInfo.Version);
            UpdateStatus.Text = Status.Text = newer ? $"BB Tools v{result.LatestVersion.ToString(3)} is available. Open View release to download it." :
                result.LatestVersion == AppInfo.Version ? $"You're up to date (v{AppInfo.DisplayVersion})." :
                $"You're running v{AppInfo.DisplayVersion}. The latest published release is v{result.LatestVersion.ToString(3)}.";
            if (newer) { updateRelease = result.ReleaseUrl; UpdateReleaseButton.Visibility = Visibility.Visible; }
        }
        catch (OperationCanceledException) { if (!closing) UpdateStatus.Text = Status.Text = "Update check timed out. Try again or open GitHub."; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        {
            if (!closing) UpdateStatus.Text = Status.Text = $"Couldn't check for updates: {ex.Message}";
        }
        finally { if (!closing) CheckUpdatesButton.IsEnabled = true; }
    }
    private void Run(Action action, string success)
    {
        try { if (session == null) throw new IOException("Connect to the game first."); action(); Status.Text = success; }
        catch (Exception ex) when (IsExpected(ex)) { Status.Text = ex.Message; }
    }
    private static int Number(TextBox input, string label)
    {
        if (!int.TryParse(input.Text.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException($"{label} must be a whole non-negative number.");
        return value;
    }
    private void SetHealth(object sender, RoutedEventArgs e) => Run(() => session!.SetHealth(Number(HealthInput, "Health")), "Health updated.");
    private void Heal(object sender, RoutedEventArgs e) => Run(() => session!.SetHealth(session.ReadPlayer().MaxHealth), "Health restored.");
    private void SetEchoes(object sender, RoutedEventArgs e) => Run(() => session!.SetEchoes(Number(EchoInput, "Blood Echoes")), "Blood Echoes updated.");
    private void AddEchoes(object sender, RoutedEventArgs e) => Run(() => session!.SetEchoes((int)Math.Min(999999999L, (long)session.ReadCharacter().Values.Echoes + 10000)), "Added up to 10,000 Blood Echoes.");
    private void StaminaChanged(object sender, RoutedEventArgs e)
    {
        if (updating || session == null) return;
        try { session.SetInfiniteStamina(InfiniteStamina.IsChecked == true); Status.Text = InfiniteStamina.IsChecked == true ? "Infinite stamina enabled." : "Previous stamina-consumption setting restored."; }
        catch (Exception ex) when (IsExpected(ex)) { Status.Text = $"{ex.Message} Selection retained; waiting for a valid player."; }
    }
    private CheckBox[] FlagControls => [GodMode, OneShot, Stealth, Silent, NoAi, NoFallDamage];
    private void FlagChanged(object sender, RoutedEventArgs e)
    {
        if (updating || session == null || sender is not CheckBox control) return;
        try
        {
            session.SetPracticeFlag(Enum.Parse<PracticeFlag>((string)control.Tag), control.IsChecked == true);
            Status.Text = "Practice flag updated.";
        }
        catch (Exception ex) when (IsExpected(ex)) { Status.Text = $"{ex.Message} Selection retained; waiting for a valid player."; }
    }
    private void TurnOffFeatures(object sender, RoutedEventArgs e) => Status.Text = StopFeatures() ?? "Practice toggles switched off and previous flags restored.";
    private void Navigate(object sender, RoutedEventArgs e)
    {
        var page = (string)((Button)sender).Tag;
        PlayerPage.Visibility = page == "Player" ? Visibility.Visible : Visibility.Collapsed;
        PracticePage.Visibility = page == "Practice" ? Visibility.Visible : Visibility.Collapsed;
        ItemsPage.Visibility = page == "Items" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = page switch { "Practice" => "Practice controls", "Items" => "Items & travel", "Settings" => "Settings", _ => "The hunter" };
        foreach (var nav in new[] {NavPlayer, NavPractice, NavItems, NavSettings})
            nav.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString((string)nav.Tag == page ? "#3B352A" : "#2B313B"));
    }
    private void ItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ItemPicker.SelectedItem is not ItemChoice item || ItemCategory == null) return;
        ItemId.Text = item.Id.ToString(); ItemQuantity.Text = "1";
        if (WikiHexId != null) WikiHexId.IsChecked = false;
        foreach (ComboBoxItem category in ItemCategory.Items)
            if (uint.Parse((string)category.Tag) == item.Category) ItemCategory.SelectedItem = category;
    }
    private async Task RunGameAction(Func<GameActions, Task> action, string success)
    {
        if (actions == null || actionBusy) { Status.Text = "Connect to a loaded character and wait for the active request to finish."; return; }
        actionBusy = true; ItemControls.IsEnabled = WarpControls.IsEnabled = LocationControls.IsEnabled = BossControls.IsEnabled = false;
        Status.Text = "Waiting for the game thread to process the request…";
        try { await action(actions); if (!closing) Status.Text = success; }
        catch (Exception ex) when (IsExpected(ex)) { if (!closing) Status.Text = ex.Message; }
        finally { actionBusy = false; }
    }
    private async void SpawnItem(object sender, RoutedEventArgs e) => await RunGameAction(engine =>
    {
        if (ItemCategory.SelectedItem is not ComboBoxItem category) throw new ArgumentException("Choose an item category.");
        var item = ItemInput.Parse(ItemId.Text, uint.Parse((string)category.Tag), WikiHexId.IsChecked == true);
        return engine.SpawnAsync(item.Id, item.Category, Number(ItemQuantity, "Quantity"));
    }, "Item grant completed on the game thread. Check inventory and storage.");
    private async void Warp(object sender, RoutedEventArgs e) => await RunGameAction(engine =>
    {
        if (LampPicker.SelectedItem is not LampChoice lamp) throw new ArgumentException("Choose a lamp.");
        return engine.WarpAsync(lamp);
    }, "Warp requested. Allow the game to finish loading before issuing another request.");
    private void OpenReference(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; }
        catch (Exception ex) when (IsExpected(ex)) { Status.Text = ex.Message; }
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
        var location = session!.ReadLocation(LocationName.Text);
        var next = new LocationBook([.. locationBook.Locations.Where(x => !x.Name.Equals(location.Name, StringComparison.OrdinalIgnoreCase)), location]);
        next.Save(); locationBook = next;
        LocationPicker.ItemsSource = locationBook.Locations; LocationPicker.SelectedItem = location;
    }, "Location saved. Saving with the same name replaces that bookmark.");
    private async void TravelBossArea(object sender, RoutedEventArgs e) => await RunGameAction(engine =>
    {
        if (BossPicker.SelectedItem is not PracticeDestination destination) throw new ArgumentException("Select a boss entrance first.");
        return engine.WarpAsync(GameActions.Lamps.Single(x => x.Entity == destination.EntryLamp));
    }, "Area travel requested. After loading, use Go to entrance.");
    private async void GoToBoss(object sender, RoutedEventArgs e) => await RunGameAction(engine =>
    {
        if (BossPicker.SelectedItem is not PracticeDestination destination) throw new ArgumentException("Select a boss entrance first.");
        return engine.RestoreLocationAsync(destination.Position);
    }, "Boss entrance relocation processed.");
    private async void RestoreLocation(object sender, RoutedEventArgs e) => await RunGameAction(engine =>
    {
        if (LocationPicker.SelectedItem is not SavedLocation location) throw new ArgumentException("Select a saved location first.");
        return engine.RestoreLocationAsync(location);
    }, "Location restore processed on the game thread.");
    private void DeleteLocation(object sender, RoutedEventArgs e)
    {
        try
        {
            if (LocationPicker.SelectedItem is not SavedLocation location) throw new ArgumentException("Select a saved location first.");
            var next = new LocationBook(locationBook.Locations.Where(x => x != location).ToArray());
            next.Save(); locationBook = next; LocationPicker.ItemsSource = next.Locations; LocationPicker.SelectedIndex = 0;
            Status.Text = "Location deleted.";
        }
        catch (Exception ex) when (IsExpected(ex)) { Status.Text = ex.Message; }
    }
    private TextBox[] AttributeInputs => [Vitality, Endurance, Strength, Skill, Bloodtinge, Arcane];
    private void PopulateStats(CharacterState character)
    {
        statDraft = character;
        LevelValue.Text = $"Blood Level {character.Level}";
        var inputs = AttributeInputs;
        for (var i = 0; i < inputs.Length; i++) inputs[i].Text = character.Values.Attributes[i].ToString();
    }
    private void LoadStats(object sender, RoutedEventArgs e) => Run(() => PopulateStats(session!.ReadCharacter()), "Current attributes loaded into the editor.");
    private void ApplyStats(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (statDraft == null) throw new IOException("Reload current attributes first.");
        var values = AttributeInputs.Select(x => Number(x, x.Name)).ToArray();
        session!.SetAttributes(values, statDraft);
        PopulateStats(session.ReadCharacter());
    }, "Attributes applied. Quit to title and reload your character to refresh derived stats.");
    private async void Backup(object sender, RoutedEventArgs e)
    {
        if (connectedPid != 0) { Status.Text = "Disconnect and close the game before backing up saves."; return; }
        var source = new OpenFolderDialog { Title = "Choose Bloodborne save folder (close the game first)" };
        if (source.ShowDialog(this) != true) return;
        var target = new OpenFolderDialog { Title = "Choose backup destination outside the save folder" };
        if (target.ShowDialog(this) != true) return;
        BackupButton.IsEnabled = false;
        try { var result = await Task.Run(() => CreateBackup(source.FolderName, target.FolderName)); Status.Text = $"Backup created: {result}"; }
        catch (Exception ex) when (IsExpected(ex)) { Status.Text = $"Backup failed: {ex.Message}"; }
        finally { BackupButton.IsEnabled = true; }
    }
    public static string CreateBackup(string source, string destination)
    {
        source = Path.GetFullPath(source); destination = Path.GetFullPath(destination);
        var relative = Path.GetRelativePath(source, destination);
        if (relative == "." || (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar))) throw new ArgumentException("Backup destination must be outside the save folder.");
        Validate(new DirectoryInfo(source));
        var archive = Path.Combine(destination, $"bb-tools-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
        var temporary = archive + ".partial";
        try { ZipFile.CreateFromDirectory(source, temporary); File.Move(temporary, archive); return archive; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Validate(DirectoryInfo directory)
    {
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Links and junctions are unsupported in save folders.");
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Links and junctions are unsupported in save folders.");
            if (entry is DirectoryInfo child) Validate(child);
        }
    }
}
