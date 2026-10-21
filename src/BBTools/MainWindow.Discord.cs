using System.Windows;

namespace BBTools;
public partial class MainWindow
{
    private readonly DiscordPresence discordPresence=new();
    private void UpdateDiscordPresence()
    {
        var enabled=settings.DiscordRichPresence;
        var candidate=processSnapshot.FirstOrDefault(x=>x.Id==connectedPid);
        discordPresence.Update(enabled&&connectedPid>0&&session!=null&&memory?.IsAlive==true?(candidate?.Name.Contains("shadps4",StringComparison.OrdinalIgnoreCase)==true?"shadPS4":"Bloodborne PC"):null);
        DiscordPresenceStatus.Text=enabled?discordPresence.Status:"Discord Rich Presence is off.";
    }
    private void DiscordPresenceChanged(object sender,RoutedEventArgs e)
    {
        if(updating||DiscordPresenceEnabled==null||DiscordPresenceStatus==null)return;
        settings=settings with{DiscordRichPresence=DiscordPresenceEnabled.IsChecked==true};
        UpdateDiscordPresence();
        try{settings.Save();}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text="Discord preference changed for this session but could not be saved: "+ex.Message;}
    }
}
