using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BBTools;

internal static partial class Program
{
    private static void ExperimentalUiChecks(MainWindow window)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var settingsField=typeof(MainWindow).GetField("settings",flags)!;
        var previous=(AppSettings)settingsField.GetValue(window)!;
        void Apply(bool enabled)
        {
            settingsField.SetValue(window,previous with {ExperimentalExploration=enabled});
            typeof(MainWindow).GetMethod("UpdateExperimentalAvailability",flags)!.Invoke(window,null);
        }
        Check(!new AppSettings().ExperimentalExploration,"experimental camera and noclip default off");
        Apply(true);
        Check(((Button)window.FindName("NavPhoto")).Visibility==Visibility.Visible&&((FrameworkElement)window.FindName("FlightPanel")).Visibility==Visibility.Visible&&((FrameworkElement)window.FindName("PhotoPage")).IsEnabled,
            "explicit experimental opt-in reveals both camera and flight/noclip controls");
        Check(((CheckBox)window.FindName("FreeCameraEnabled")).IsChecked!=true&&((CheckBox)window.FindName("FlightEnabled")).IsChecked!=true&&((CheckBox)window.FindName("NoclipEnabled")).IsChecked!=true,
            "revealing experimental tools never starts a native mode automatically");
        Check(((ItemsControl)window.FindName("CameraBindingList")).Items.Count==15&&((FrameworkElement)window.FindName("CameraBindingsPanel")).Parent!=null,
            "all fifteen configurable camera actions are available in Settings");
        ((Button)window.FindName("NavSettings")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((TabItem)window.FindName("ExperimentalTab")).IsSelected=true;
        ((Expander)window.FindName("CameraBindingsPanel")).IsExpanded=true;
        var content=(FrameworkElement)window.Content;content.Measure(new Size(860,680));content.Arrange(new Rect(0,0,860,680));content.UpdateLayout();
        var items=(ItemsControl)window.FindName("CameraBindingList");
        ComboBox? FindKeyPicker(DependencyObject node)
        {
            if(node is ComboBox picker&&picker.DataContext is CameraBinding)return picker;
            for(var n=0;n<System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);n++)
            {var found=FindKeyPicker(System.Windows.Media.VisualTreeHelper.GetChild(node,n));if(found!=null)return found;}
            return null;
        }
        var keyPicker=FindKeyPicker(items);
        Check(keyPicker!=null&&keyPicker.Items.Count==CameraControls.Choices.Length,"camera key pickers expose the complete supported choices in the actual Settings layout");
        keyPicker!.SelectedItem="I";keyPicker.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedItemProperty)!.UpdateSource();
        Check(((CameraBinding)items.Items[0]).Key=="I","camera key selection updates the editable binding used by Save controls");
        keyPicker.SelectedItem="W";keyPicker.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedItemProperty)!.UpdateSource();
        ((Expander)window.FindName("CameraBindingsPanel")).IsExpanded=false;
        ((Button)window.FindName("NavPhoto")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(((FrameworkElement)window.FindName("PhotoPage")).Visibility==Visibility.Visible,"experimental opt-in permits photo-studio navigation");
        ((CheckBox)window.FindName("FreeCameraEnabled")).IsChecked=true;
        Check(((CheckBox)window.FindName("FreeCameraEnabled")).IsChecked==false,"experimental photo mode still requires an attached loaded character");
        Apply(false);
        Check(((FrameworkElement)window.FindName("PhotoPage")).Visibility==Visibility.Collapsed&&((Button)window.FindName("NavPhoto")).Visibility==Visibility.Collapsed&&((FrameworkElement)window.FindName("FlightPanel")).Visibility==Visibility.Collapsed,
            "opting out hides the tools and exits a currently selected photo page");
        typeof(MainWindow).GetField("photoHudRestorePending",flags)!.SetValue(window,true);Apply(false);
        var timer=(DispatcherTimer)typeof(MainWindow).GetField("explorationTimer",flags)!.GetValue(window)!;
        Check(timer.IsEnabled,"opting out retains cleanup polling while HUD restoration is pending");
        typeof(MainWindow).GetField("photoHudRestorePending",flags)!.SetValue(window,false);Apply(false);
        Check(!timer.IsEnabled,"experimental polling stops once opted out and cleanup is complete");
        var file=Path.Combine(Path.GetTempPath(),"BBTools-experimental-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            new AppSettings(ExperimentalExploration:true,Camera:new(Forward:"I")).Save(file);
            var loaded=AppSettings.Load(file);
            Check(loaded.ExperimentalExploration&&loaded.Camera?.Forward=="I","explicit experimental opt-in and camera bindings persist together");
            File.WriteAllText(file,"{\"AutoConnect\":true}");
            Check(!AppSettings.Load(file).ExperimentalExploration,"legacy settings cannot silently enable experimental camera or noclip");
        }
        finally{File.Delete(file);}
        settingsField.SetValue(window,previous);typeof(MainWindow).GetMethod("UpdateExperimentalAvailability",flags)!.Invoke(window,null);
    }
}
