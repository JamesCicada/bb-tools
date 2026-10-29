using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using BBTools.Memory;
using Microsoft.Win32;

namespace BBTools;

public partial class MainWindow
{
    private GemCatalog? gemCatalog;
    private Dictionary<int, string> gemTexts = [];
    private bool gemEditorReady, changingGemChoices, customGemLoaded, pendingCustomNormal;
    private TextBox[] GemInputs => [GemEffect1, GemEffect2, GemEffect3, GemEffect4, GemEffect5, GemEffect6];
    private sealed record GemEffectChoice(int Id, string Label)
    {
        public override string ToString() => Label;
    }
    private void CenterShoulderCamera(object sender, RoutedEventArgs e) => CameraHorizontal.Value = 0;
    private void InitializeGemEditor()
    {
        gemEditorReady = true;
        foreach (var input in GemInputs.Append(GemSource)) input.TextChanged += (_, _) => UpdateGemPreview();
        GemShape.SelectionChanged += GemPreviewChanged;
        RefreshGemLanguage();
        try
        {
            gemTexts = library?.GemTexts().ToDictionary(x => x.Id, x => x.Name) ?? [];
            RefreshSavedGems();
        }
        catch (Exception ex) when (IsExpected(ex) || ex is JsonException) { App.LogError(ex); GemSummary.Text = ex.Message; }
        SearchGemEffects(this, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
        GemSummary.Text = Translations.Format("gem.catalogReady", GemGuide.Effects.Count);
    }
    private void RefreshGemLanguage()
    {
        if (!gemEditorReady) return;
        var key = (GemRecipePicker.SelectedItem as NaturalGemRecipe)?.Key;
        GemRecipePicker.ItemsSource = GemGuide.Recipes.ToArray();
        GemRecipePicker.SelectedItem = GemGuide.Recipes.FirstOrDefault(x => x.Key == key) ?? GemGuide.Recipes[0];
        GemRecipeChanged(this, new SelectionChangedEventArgs(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, Array.Empty<object>(), Array.Empty<object>()));
        SearchGemEffects(this, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
    }
    private static string GemShapeName(int shape) => Translations.T(shape switch
    {
        1 => "gems.radial", 2 => "gems.triangle", 4 => "gems.waning", 8 => "gems.circle", 63 => "gems.droplet", _ => throw new ArgumentException("Invalid gem shape.")
    });
    private void GemRecipeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!gemEditorReady || GemRecipePicker.SelectedItem is not NaturalGemRecipe recipe) return;
        pendingCustomNormal = false;
        var shape = (NormalGemShape.SelectedItem as GemEffectChoice)?.Id;
        var second = (NormalGemSecondary.SelectedItem as GemEffectChoice)?.Id;
        var curse = (NormalGemCurse.SelectedItem as GemEffectChoice)?.Id;
        changingGemChoices = true;
        void Choices(ComboBox picker, IEnumerable<int> ids, int? previous, Func<int, string> label)
        {
            var choices = ids.Select(id => new GemEffectChoice(id, label(id))).ToArray();
            picker.ItemsSource = choices; picker.SelectedItem = choices.FirstOrDefault(x => x.Id == previous) ?? choices[0];
        }
        Choices(NormalGemShape, recipe.Shapes, shape, GemShapeName);
        Choices(NormalGemSecondary, recipe.Secondaries, second, GemGuide.Describe);
        Choices(NormalGemCurse, recipe.Curses, curse ?? 41218, GemGuide.Describe);
        changingGemChoices = false; UpdateGemPreview();
    }
    private void GemModeChanged(object sender, RoutedEventArgs e)
    {
        if (!gemEditorReady) return;
        var advanced = GemAdvanced.IsChecked == true;
        if (!advanced) pendingCustomNormal = false;
        if (advanced && !customGemLoaded && GemInputs.All(x => x.Text == "-1")) PopulateRawGem(CurrentNormalGem());
        GemNormalPanel.Visibility = advanced ? Visibility.Collapsed : Visibility.Visible;
        GemAdvancedPanel.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        UpdateGemPreview();
    }
    private GemSpec CurrentNormalGem()
    {
        if (GemRecipePicker.SelectedItem is not NaturalGemRecipe recipe || NormalGemShape.SelectedItem is not GemEffectChoice shape ||
            NormalGemSecondary.SelectedItem is not GemEffectChoice second || NormalGemCurse.SelectedItem is not GemEffectChoice curse)
            throw new ArgumentException(Translations.T("gem.invalidNatural"));
        return recipe.Build(shape.Id, second.Id, curse.Id);
    }
    private GemSpec CurrentGem()
    {
        if (GemAdvanced.IsChecked != true)
        {
            if (pendingCustomNormal) throw new ArgumentException(Translations.T("gem.invalidNatural"));
            return CurrentNormalGem();
        }
        if (GemShape.SelectedItem is not ComboBoxItem shape) throw new ArgumentException(Translations.T("gems.chooseShape"));
        var spec = new GemSpec(Number(GemSource, "Gem template ID"), int.Parse((string)shape.Tag, CultureInfo.InvariantCulture), GemInputs.Select(x =>
        {
            if (!int.TryParse(x.Text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException(Translations.T("gems.integer"));
            return value;
        }).ToArray());
        spec.Validate(); return spec;
    }
    private void GemPreviewChanged(object sender, SelectionChangedEventArgs e) => UpdateGemPreview();
    private void UpdateGemPreview()
    {
        if (!gemEditorReady || changingGemChoices) return;
        try
        {
            var spec = CurrentGem();
            var header = GemAdvanced.IsChecked == true ? Translations.Format("gem.customPreview", GemShapeName(spec.Shape)) :
                Translations.Format("gem.normalPreview", GemShapeName(spec.Shape), GemGuide.RequireNatural(spec).Origin);
            GemPreview.Text = header + "\n" + string.Join("\n", spec.Effects.Where(id => id != -1).Select(GemGuide.Describe));
        }
        catch (Exception ex) when (IsExpected(ex)) { GemPreview.Text = ex.Message; }
    }
    private void RefreshGems(object sender, RoutedEventArgs e)
    {
        try
        {
            if (session == null || memory == null || actionBusy) throw new IOException(Translations.T("gems.connect"));
            var reader = new BloodGems(memory, session);
            var catalog = reader.ReadCatalog(); var inventory = reader.ReadInventory();
            gemCatalog = catalog;
            GemInventory.ItemsSource = inventory; GemInventory.SelectedIndex = inventory.Length > 0 ? 0 : -1;
            if (string.IsNullOrWhiteSpace(GemSource.Text))
                GemSource.Text = catalog.Templates.FirstOrDefault()?.Id.ToString(CultureInfo.InvariantCulture) ?? "";
            SearchGemEffects(this, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
            GemSummary.Text = Translations.Format("gems.loaded", catalog.Effects.Length, inventory.Length);
        }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); GemSummary.Text = ex.Message; }
    }
    private void SearchGemEffects(object sender, TextChangedEventArgs e)
    {
        if (!gemEditorReady || GemEffectPicker == null || GemEffectSearch == null) return;
        var terms = GemEffectSearch.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ids = gemCatalog == null ? GemGuide.Effects.Keys : gemCatalog.Effects.Select(x => x.Id);
        var ranks = gemCatalog?.Effects.ToDictionary(x => x.Id, x => x.Rank);
        GemEffectPicker.ItemsSource = ids.Select(id => new GemEffectChoice(id,
            $"{gemTexts.GetValueOrDefault(id, GemGuide.Describe(id))} · {id} · {Translations.Format("gems.rank", ranks?.GetValueOrDefault(id) ?? GemGuide.Effects.GetValueOrDefault(id)?.Rank ?? 0)}"))
            .Where(x => terms.All(term => x.Label.Contains(term, StringComparison.OrdinalIgnoreCase))).Take(500).ToArray();
        GemEffectPicker.SelectedIndex = -1;
    }
    private void SetGemEffect(object sender, RoutedEventArgs e)
    {
        if (GemEffectPicker.SelectedItem is not GemEffectChoice effect || GemEffectSlot.SelectedIndex is < 0 or > 5) return;
        GemInputs[GemEffectSlot.SelectedIndex].Text = effect.Id.ToString(CultureInfo.InvariantCulture);
    }
    private void PopulateRawGem(GemSpec spec)
    {
        GemSource.Text = spec.Source.ToString(CultureInfo.InvariantCulture);
        foreach (ComboBoxItem shape in GemShape.Items)
            if (int.Parse((string)shape.Tag, CultureInfo.InvariantCulture) == spec.Shape) GemShape.SelectedItem = shape;
        for (var i = 0; i < 6; i++) GemInputs[i].Text = spec.Effects[i].ToString(CultureInfo.InvariantCulture);
    }
    private void LoadGemSpec(GemSpec spec)
    {
        spec.Validate(); PopulateRawGem(spec); customGemLoaded = true;
        var recipe = GemGuide.Match(spec);
        pendingCustomNormal = recipe == null && GemAdvanced.IsChecked != true;
        if (recipe != null)
        {
            GemRecipePicker.SelectedItem = recipe;
            void Select(ComboBox picker, int id) => picker.SelectedItem = picker.Items.OfType<GemEffectChoice>().First(x => x.Id == id);
            Select(NormalGemShape, spec.Shape);
            Select(NormalGemSecondary, recipe.Secondaries.Contains(-1) ? -1 : spec.Effects[1]);
            Select(NormalGemCurse, recipe.Secondaries.Contains(-1) ? spec.Effects[1] : spec.Effects[2]);
        }
        GemSummary.Text = recipe == null && GemAdvanced.IsChecked != true ? Translations.T("gem.customLoaded") : Translations.T("gem.loadedStatus");
        UpdateGemPreview();
    }
    private void LoadGem(object sender, RoutedEventArgs e)
    {
        if (GemInventory.SelectedItem is InventoryGem gem) LoadGemSpec(gem.Spec);
    }
    private void ImportGemTexts(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = Translations.T("gem.archiveTitle"), Filter = "Bloodborne messages|menu.msgbnd.dcx" };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var entries = GemTextCatalog.LoadFile(picker.FileName);
            Library.SaveGemTexts(entries); gemTexts = entries.ToDictionary(x => x.Id, x => x.Name);
            SearchGemEffects(this, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
            GemSummary.Text = Translations.Format("gems.imported", entries.Length);
        }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); GemSummary.Text = ex.Message; }
    }
    private async void SpawnBloodGem(object sender, RoutedEventArgs e) => await RunGameAction(async engine =>
    {
        var natural = GemAdvanced.IsChecked != true;
        var spec = CurrentGem();
        var handle = await engine.SpawnGemAsync(spec, natural);
        App.Log?.Write("ACTION", $"Blood gem confirmed in inventory: handle {handle:X8}, template {spec.Source}, shape {spec.Shape}, effects {string.Join(",", spec.Effects)}, normal mode {natural}");
        RefreshGemsAfterSpawn();
    }, Translations.T("gems.created"));
    private void RefreshGemsAfterSpawn()
    {
        if (session == null || memory == null) return;
        GemInventory.ItemsSource = new BloodGems(memory, session).ReadInventory();
        GemSummary.Text = Translations.T("gems.created");
    }
    private void RefreshSavedGems() => SavedGemPicker.ItemsSource = library?.GemBuilds() ?? [];
    private void GemLibraryAction(Action action)
    {
        try { action(); }
        catch (Exception ex) when (IsExpected(ex) || ex is JsonException) { App.LogError(ex); GemSummary.Text = ex.Message; }
    }
    private SavedGemBuild CurrentGemBuild()
    {
        var build = new SavedGemBuild(GemBuildName.Text.Trim(), CurrentGem()); build.Validate(); return build;
    }
    private void SaveGemBuild(object sender, RoutedEventArgs e) => GemLibraryAction(() =>
    {
        Library.Save(CurrentGemBuild()); RefreshSavedGems(); GemSummary.Text = Translations.T("gem.savedStatus");
    });
    private void LoadGemBuild(object sender, RoutedEventArgs e) => GemLibraryAction(() =>
    {
        if (SavedGemPicker.SelectedItem is not SavedGemBuild build) return;
        build.Validate(); GemBuildName.Text = build.Name; LoadGemSpec(build.Gem);
    });
    private void DeleteGemBuild(object sender, RoutedEventArgs e) => GemLibraryAction(() =>
    {
        if (SavedGemPicker.SelectedItem is not SavedGemBuild build) return;
        Library.Delete("gemBuild", build.Name); RefreshSavedGems();
    });
    private void ExportGemBuild(object sender, RoutedEventArgs e) => GemLibraryAction(() =>
    {
        var build = CurrentGemBuild(); var picker = new SaveFileDialog { Filter = "Gem recipe|*.bbgem.json", FileName = "gem.bbgem.json" };
        if (picker.ShowDialog(this) == true) File.WriteAllText(picker.FileName, JsonSerializer.Serialize(build, new JsonSerializerOptions { WriteIndented = true }));
    });
    private void ImportGemBuild(object sender, RoutedEventArgs e) => GemLibraryAction(() =>
    {
        var picker = new OpenFileDialog { Filter = "Gem recipe|*.bbgem.json;*.json" };
        if (picker.ShowDialog(this) != true) return;
        using var file = File.OpenRead(picker.FileName);
        if (file.Length > 16384) throw new IOException("Gem recipes must be at most 16 KB.");
        var build = JsonSerializer.Deserialize<SavedGemBuild>(file) ?? throw new IOException("Invalid gem recipe.");
        build.Validate(); GemBuildName.Text = build.Name; LoadGemSpec(build.Gem);
    });
}
