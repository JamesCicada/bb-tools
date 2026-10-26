using System.Windows;
using BBTools.Memory;

namespace BBTools;

public partial class MainWindow
{
    private ProgressionCosts? progressionCosts;
    private bool progressionPaused;
    private void ProgressionChanged(object sender, RoutedEventArgs e)
    {
        if (updating) return;
        progressionPaused = false;
        App.Log?.Write("ACTION", $"Free level-ups: {FreeLevels?.IsChecked == true}; free weapon upgrades: {FreeUpgrades?.IsChecked == true}");
        PollProgression();
    }
    private void PollProgression()
    {
        if (memory == null || session == null || progressionPaused || actionBusy) return;
        try
        {
            var levels = FreeLevels.IsChecked == true; var upgrades = FreeUpgrades.IsChecked == true;
            if (levels || upgrades) { progressionCosts ??= new(memory, session); progressionCosts.Apply(levels, upgrades); }
            else progressionCosts?.Restore();
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            try { progressionCosts?.Restore(); } catch (Exception cleanup) when (IsExpected(cleanup)) { App.LogError(cleanup); }
            progressionPaused = true; App.LogError(ex); Status.Text = Translations.Format("progression.paused", ex.Message);
        }
    }
    private void PollInsight()
    {
        // An unsupported optional reader must not disable health, Echoes or stats.
        InsightControls.IsEnabled = false;
        try { InsightValue.Text = session!.ReadInsight().ToString(); InsightControls.IsEnabled = !actionBusy; }
        catch (Exception ex) when (IsExpected(ex)) { InsightValue.Text = "—"; App.LogError(ex); }
    }
    private void SetInsight(object sender, RoutedEventArgs e) => Run(() =>
    {
        session!.SetInsight(Number(InsightInput, "Insight"));
        PollInsight();
    }, Translations.T("insight.updated"));
}
