using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace BBTools;

public partial class MainWindow
{
    private bool loadingLanguage;
    private void ApplyLanguageResources()
    {
        Translations.Select(settings.Language);
        foreach (var key in Translations.English.Keys) Application.Current.Resources["L." + key] = Translations.T(key);
    }
    private void LoadLanguageControls()
    {
        loadingLanguage = true;
        LanguagePicker.ItemsSource = new[] { new AppLanguage("auto", Translations.T("language.system")) }.Concat(Translations.Languages);
        LanguagePicker.SelectedValue = settings.Language;
        Language = XmlLanguage.GetLanguage(Translations.CurrentLanguage);
        FlowDirection = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        LanguageCoverage.Text = Translations.Format("language.coverage", Translations.Coverage(Translations.CurrentLanguage));
        loadingLanguage = false;
    }
    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loadingLanguage || LanguagePicker.SelectedValue is not string code || !Translations.IsSupported(code)) return;
        try
        {
            var next = settings with { Language = code }; next.Save(); settings = next;
            ApplyLanguageResources(); LoadLanguageControls();
            RefreshLocalizedLabels();
            itemSearchIndex = null;
            Status.Text = Translations.T("language.changed");
        }
        catch (Exception ex) when (IsExpected(ex)) { App.LogError(ex); Status.Text = ex.Message; LoadLanguageControls(); }
    }
    private void RefreshLocalizedLabels()
    {
        Title = Translations.Format("app.title", AppInfo.DisplayVersion);
        VersionLabel.Text = Translations.Format("app.version", AppInfo.DisplayVersion);
        AboutVersion.Text = $"BB Tools v{AppInfo.DisplayVersion}";
        ConnectionBadge.Text = Translations.Source(session == null ? "DISCONNECTED" : "CONNECTED");
        ConnectButton.Content = Translations.Source(session == null ? "Connect" : "Disconnect");
        DiscoveryValue.Text = Translations.Format("loot.bonus", DiscoveryBonus.Value);
        foreach (var (nav, page) in new[] { (NavPlayer, PlayerPage), (NavPractice, PracticePage), (NavTarget, TargetPage), (NavItems, ItemsPage),
            (NavTravel, TravelPage), (NavCamera, CameraPage), (NavWorkshop, WorkshopPage), (NavSettings, SettingsPage) })
            if (page.Visibility == Visibility.Visible) { var offset = PageScroll.VerticalOffset; Navigate(nav, new()); PageScroll.ScrollToVerticalOffset(offset); break; }
        LoadCameraControls();
        UpdateExperimentalAvailability();
        UpdatePhotoModeState();
    }
    private void HelpTranslate(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://crowdin.com/project/bb-tools") { UseShellExecute = true });
}
