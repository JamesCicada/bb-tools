using System.IO;
using System.Windows;
using System.Windows.Controls;
using BBTools.Memory;
using Microsoft.Win32;

namespace BBTools;

public partial class MainWindow
{
    private ItemLoadout? loadoutDraft;

    // Library edits work offline; only spawning needs a connected game.
    private void LibraryAction(Action action, string success)
    {
        try { action(); Status.Text = success; }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = ex.Message; }
    }

    private void RefreshLoadouts()
    {
        var selected = (LoadoutPicker.SelectedItem as ItemLoadout)?.Name ?? loadoutDraft?.Name;
        var all = Library.Loadouts();
        LoadoutPicker.ItemsSource = all;
        LoadoutPicker.SelectedItem = all.FirstOrDefault(x => x.Name.Equals(selected, StringComparison.OrdinalIgnoreCase));
        if (LoadoutPicker.SelectedItem == null) ShowLoadout(null);
    }
    private void ShowLoadout(ItemLoadout? loadout)
    {
        loadoutDraft = loadout;
        LoadoutItems.ItemsSource = loadout?.Items ?? [];
        if (loadout != null) LoadoutName.Text = loadout.Name;
    }
    private void LoadoutSelected(object sender, SelectionChangedEventArgs e) => ShowLoadout(LoadoutPicker.SelectedItem as ItemLoadout);

    // The item currently in the spawn fields, named from the picker when it matches.
    private LoadoutItem CurrentLoadoutItem()
    {
        if (ItemCategory.SelectedItem is not ComboBoxItem category) throw new ArgumentException(Translations.T("message.Chooseanitemcategory.47eb33d"));
        var parsed = ItemInput.Parse(ItemId.Text, uint.Parse((string)category.Tag), WikiHexId.IsChecked == true);
        var name = ItemPicker.SelectedItem is ItemChoice choice && choice.Id == parsed.Id && choice.Category == parsed.Category ? choice.Name : $"Item {parsed.Id}";
        return new LoadoutItem(name, parsed.Id, parsed.Category, Number(ItemQuantity, "Quantity"));
    }
    private void AddLoadoutItem(object sender, RoutedEventArgs e) => LibraryAction(() =>
    {
        var name = LoadoutName.Text.Trim();
        UserLibrary.ValidateName(name);
        var item = CurrentLoadoutItem();
        var existing = Library.Loadouts().FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var updated = existing?.With(item) ?? new ItemLoadout(name, [item]);
        Library.Save(updated); loadoutDraft = updated; RefreshLoadouts();
    }, Translations.T("loadouts.added"));
    private void RemoveLoadoutItem(object sender, RoutedEventArgs e) => LibraryAction(() =>
    {
        if (loadoutDraft == null) throw new ArgumentException(Translations.T("loadouts.choose"));
        var updated = loadoutDraft.Without(LoadoutItems.SelectedIndex);
        if (updated.Items.Length == 0) { Library.Delete("loadout", updated.Name); loadoutDraft = null; }
        else { Library.Save(updated); loadoutDraft = updated; }
        RefreshLoadouts();
    }, Translations.T("loadouts.removed"));
    private void DeleteLoadout(object sender, RoutedEventArgs e) => LibraryAction(() =>
    {
        if (LoadoutPicker.SelectedItem is not ItemLoadout loadout) throw new ArgumentException(Translations.T("loadouts.choose"));
        Library.Delete("loadout", loadout.Name); loadoutDraft = null; RefreshLoadouts();
    }, Translations.T("loadouts.deleted"));
    private async void SpawnLoadout(object sender, RoutedEventArgs e)
    {
        if (LoadoutPicker.SelectedItem is not ItemLoadout loadout) { Status.Text = Translations.T("loadouts.choose"); return; }
        var success = Translations.Format("loadouts.spawned", loadout.Items.Length, loadout.Name);
        var openSlots = OpenGemSlots.IsChecked == true;
        var adjusted = false;
        await RunGameAction(engine => LoadoutSpawner.SpawnAsync(loadout, item =>
        {
            var id = item.Id;
            if (openSlots && WeaponGemSlots.NeedsWorkshopUpgrade(item.Id, item.Category)) { id = WeaponGemSlots.SpawnId(item.Id, item.Category); adjusted = true; }
            App.Log?.Write("ACTION", $"Loadout {loadout.Name}: ID {id} (requested {item.Id}), category 0x{item.Category:X8}, quantity {item.Quantity}");
            return engine.SpawnAsync(id, item.Category, item.Quantity);
        }, (index, total, item) => Status.Text = Translations.Format("loadouts.progress", index + 1, total, item.Name)), success);
        if (adjusted && Status.Text == success) Status.Text = success + " " + Translations.T("loadouts.gemSlots");
    }

    private void ExportDiagnostics(object sender, RoutedEventArgs e)
    {
        var picker = new SaveFileDialog { Filter = "ZIP (*.zip)|*.zip", FileName = BugReport.DefaultFileName(DateTime.Now) };
        if (picker.ShowDialog() != true) return;
        try
        {
            var details = new Dictionary<string, string>
            {
                ["AutoConnect"] = settings.AutoConnect.ToString(),
                ["FovOverride"] = (FovEnabled.IsChecked == true).ToString(),
                ["Fov"] = FovSlider.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            var context = new BugReportContext("Windows", App.Log?.FilePath, session != null, session != null,
                session != null ? (Candidates.SelectedItem as ProcessCandidate)?.Name : null, Diagnostics.RedactPaths(Status.Text), Translations.CurrentLanguage, details);
            App.Log?.Write("INFO", "Bug report exported");
            var logs = BugReport.Create(picker.FileName, context);
            Status.Text = Translations.Format("support.bugReportSaved", logs);
        }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = ex.Message; }
    }
}
