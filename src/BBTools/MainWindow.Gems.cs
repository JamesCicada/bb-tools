using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using BBTools.Memory;
using Microsoft.Win32;

namespace BBTools;

public partial class MainWindow
{
    private GemCatalog? gemCatalog;
    private Dictionary<int, string> gemTexts = [];
    private TextBox[] GemInputs => [GemEffect1, GemEffect2, GemEffect3, GemEffect4, GemEffect5, GemEffect6];
    private sealed record GemEffectChoice(int Id, string Label);
    private void CenterShoulderCamera(object sender, RoutedEventArgs e) => CameraHorizontal.Value = 0;
    private void RefreshGems(object sender, RoutedEventArgs e)
    {
        try
        {
            if (session == null || memory == null || actionBusy) throw new IOException(Translations.T("gems.connect"));
            var reader = new BloodGems(memory, session);
            var catalog = reader.ReadCatalog(); var inventory = reader.ReadInventory();
            gemCatalog = catalog; gemTexts = Library.GemTexts().ToDictionary(x => x.Id, x => x.Name);
            GemInventory.ItemsSource = inventory; GemInventory.SelectedIndex = inventory.Length > 0 ? 0 : -1;
            if (!int.TryParse(GemSource.Text, out var source) || !catalog.Templates.Any(x => x.Id == source))
                GemSource.Text = catalog.Templates.FirstOrDefault()?.Id.ToString(CultureInfo.InvariantCulture) ?? "";
            SearchGemEffects(this, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
            GemSummary.Text = Translations.Format("gems.loaded", catalog.Effects.Length, inventory.Length);
        }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); GemSummary.Text = ex.Message; }
    }
    private void SearchGemEffects(object sender, TextChangedEventArgs e)
    {
        if (GemEffectPicker == null || GemEffectSearch == null || gemCatalog == null) return;
        var query = GemEffectSearch.Text.Trim();
        GemEffectPicker.ItemsSource = gemCatalog.Effects.Select(x => new GemEffectChoice(x.Id,
            $"{gemTexts.GetValueOrDefault(x.Id, Translations.T("gems.unnamed"))} · {x.Id} · {Translations.Format("gems.rank", x.Rank)}"))
            .Where(x => x.Label.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(500).ToArray();
        GemEffectPicker.SelectedIndex = -1;
    }
    private void SetGemEffect(object sender, RoutedEventArgs e)
    {
        if (GemEffectPicker.SelectedItem is not GemEffectChoice effect || GemEffectSlot.SelectedIndex is < 0 or > 5) return;
        GemInputs[GemEffectSlot.SelectedIndex].Text = effect.Id.ToString(CultureInfo.InvariantCulture);
    }
    private void LoadGem(object sender, RoutedEventArgs e)
    {
        if (GemInventory.SelectedItem is not InventoryGem gem) return;
        GemSource.Text = gem.Spec.Source.ToString(CultureInfo.InvariantCulture);
        foreach (ComboBoxItem shape in GemShape.Items)
            if (int.Parse((string)shape.Tag, CultureInfo.InvariantCulture) == gem.Spec.Shape) GemShape.SelectedItem = shape;
        for (var i = 0; i < 6; i++) GemInputs[i].Text = gem.Spec.Effects[i].ToString(CultureInfo.InvariantCulture);
        GemSummary.Text = Translations.T("gems.copyLoaded");
    }
    private void ImportGemTexts(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = Translations.T("message.ChooseBloodbornegamefolderordvdrootps4.466699e") };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var entries = GemTextCatalog.Load(picker.FolderName);
            Library.SaveGemTexts(entries); gemTexts = entries.ToDictionary(x => x.Id, x => x.Name);
            SearchGemEffects(this, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
            GemSummary.Text = Translations.Format("gems.imported", entries.Length);
        }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); GemSummary.Text = ex.Message; }
    }
    private async void SpawnBloodGem(object sender, RoutedEventArgs e) => await RunGameAction(async engine =>
    {
        if (GemShape.SelectedItem is not ComboBoxItem shape) throw new ArgumentException(Translations.T("gems.chooseShape"));
        var spec = new GemSpec(Number(GemSource, "Gem template ID"), int.Parse((string)shape.Tag, CultureInfo.InvariantCulture), GemInputs.Select(x =>
        {
            if (!int.TryParse(x.Text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException(Translations.T("gems.integer"));
            return value;
        }).ToArray());
        var handle = await engine.SpawnGemAsync(spec);
        App.Log?.Write("ACTION", $"Blood gem confirmed in inventory: handle {handle:X8}, template {spec.Source}, shape {spec.Shape}, effects {string.Join(",", spec.Effects)}");
        RefreshGemsAfterSpawn();
    }, Translations.T("gems.created"));
    private void RefreshGemsAfterSpawn()
    {
        if (session == null || memory == null) return;
        GemInventory.ItemsSource = new BloodGems(memory, session).ReadInventory();
        GemSummary.Text = Translations.T("gems.created");
    }
}
