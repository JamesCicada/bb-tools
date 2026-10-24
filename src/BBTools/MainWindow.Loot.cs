using System.Windows;
using BBTools.Memory;

namespace BBTools;

public partial class MainWindow
{
    private LootOverrides? lootOverrides;
    private bool lootPaused;
    private void LootChanged(object sender, RoutedEventArgs e) => lootPaused = false;
    private void DiscoveryChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DiscoveryValue != null) DiscoveryValue.Text = Translations.Format("loot.bonus", DiscoveryBonus.Value);
        lootPaused = false;
    }
    private void PollLoot()
    {
        if (memory == null || session == null || lootPaused || actionBusy) return;
        try
        {
            var bonus = DiscoveryEnabled.IsChecked == true ? (float)DiscoveryBonus.Value : 0;
            var guaranteed = GuaranteedDrops.IsChecked == true;
            if (bonus != 0 || guaranteed) { lootOverrides ??= new(memory, session); lootOverrides.Apply(bonus, guaranteed); }
            else lootOverrides?.Restore();
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            try { lootOverrides?.Pause(); } catch (Exception pause) when (IsExpected(pause)) { App.LogError(pause); }
            lootPaused = true; App.LogError(ex); Status.Text = Translations.Format("loot.paused", ex.Message);
        }
    }
}
