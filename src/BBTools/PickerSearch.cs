using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BBTools;

// Filter input lives inside the dropdown. Existing catalog-specific filtering
// remains responsible for preserving IDs, categories and selected objects.
public static class PickerSearch
{
    public static readonly DependencyProperty TargetProperty=DependencyProperty.RegisterAttached("Target",typeof(string),typeof(PickerSearch),new PropertyMetadata(null,TargetChanged));
    public static readonly DependencyProperty TextProperty=DependencyProperty.RegisterAttached("Text",typeof(string),typeof(PickerSearch),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,TextChanged));
    public static string? GetTarget(DependencyObject value)=>(string?)value.GetValue(TargetProperty);
    public static void SetTarget(DependencyObject value,string? target)=>value.SetValue(TargetProperty,target);
    public static string GetText(DependencyObject value)=>(string)value.GetValue(TextProperty);
    public static void SetText(DependencyObject value,string text)=>value.SetValue(TextProperty,text);
    private static void TargetChanged(DependencyObject value,DependencyPropertyChangedEventArgs e)
    {
        if(value is not ComboBox picker)return;
        picker.DropDownOpened-=Opened;
        if(e.NewValue!=null)picker.DropDownOpened+=Opened;
    }
    private static void TextChanged(DependencyObject value,DependencyPropertyChangedEventArgs e)
    {
        if(value is ComboBox picker&&GetTarget(picker) is string name&&Window.GetWindow(picker)?.FindName(name) is TextBox target)
            target.Text=(string)e.NewValue;
    }
    private static void Opened(object? sender,EventArgs e)
    {
        if(sender is not ComboBox picker)return;
        picker.Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>
        {
            if(picker.IsDropDownOpen&&picker.Template.FindName("PickerFilter",picker) is TextBox search){search.Focus();search.SelectAll();}
        }));
    }
}
