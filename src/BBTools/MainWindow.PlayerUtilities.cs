using System.Diagnostics;
using System.Windows;
using BBTools.Memory;

namespace BBTools;

public partial class MainWindow
{
    private NgCycleState? ngCycleDraft;
    private string? ngCycleReadError;
    private void PollNgCycle()
    {
        NgCycleControls.IsEnabled = false;
        try
        {
            var state = session!.ReadNgCycle();
            ngCycleReadError = null; NgCycleValue.ToolTip = null;
            NgCycleValue.Text = state.Value == 0 ? "NG" : $"NG+{state.Value}";
            NgCycleControls.IsEnabled = !actionBusy;
            ApplyNgCycleButton.IsEnabled = ngCycleDraft == state;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            NgCycleValue.Text = "—"; NgCycleValue.ToolTip = ex.Message;
            if (ngCycleReadError != ex.Message) { App.LogError(ex); ngCycleReadError = ex.Message; }
        }
    }
    private void LoadNgCycle(object sender, RoutedEventArgs e) => Run(() =>
    {
        ngCycleDraft = session!.ReadNgCycle();
        NgCycleInput.Text = ngCycleDraft.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        PollNgCycle();
    }, Translations.T("ng.loaded"));
    private void ApplyNgCycle(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (ngCycleDraft == null) throw new ArgumentException(Translations.T("ng.reloadFirst"));
        session!.SetNgCycle(Number(NgCycleInput, "NG cycle"), ngCycleDraft);
        ngCycleDraft = session.ReadNgCycle(); PollNgCycle();
    }, Translations.T("ng.applied"));
    private void OpenKoFi(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://ko-fi.com/jcicada") { UseShellExecute = true }); }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = ex.Message; }
    }
}
