using System.Windows;

namespace BBTools;
public partial class MainWindow
{
    private readonly DiscordPresence discordPresence=new();
    private DateTime nextPresenceRead;
    private DiscordGameDetails? presenceDetails;
    private void LoadDiscordControls()
    {
        DiscordPresenceEnabled.IsChecked=settings.DiscordRichPresence;
        var detail=settings.DiscordDetails??new();
        DiscordExtra.IsChecked=detail.Enabled;DiscordArea.IsChecked=detail.Area;DiscordLevel.IsChecked=detail.Level;
        DiscordDeaths.IsChecked=detail.Deaths;DiscordName.IsChecked=detail.CharacterName;DiscordNg.IsChecked=detail.NgCycle;
        nextPresenceRead=DateTime.MinValue;presenceDetails=null;
    }
    private void UpdateDiscordPresence()
    {
        var enabled=settings.DiscordRichPresence;
        var candidate=processSnapshot.FirstOrDefault(x=>x.Id==connectedPid);
        var runtime=enabled&&connectedPid>0&&session!=null&&memory?.IsAlive==true?(candidate?.Name.Contains("shadps4",StringComparison.OrdinalIgnoreCase)==true?"shadPS4":"Bloodborne PC"):null;
        var choices=settings.DiscordDetails??new();
        if(runtime==null||!choices.Enabled)presenceDetails=null;
        else if(DateTime.UtcNow>=nextPresenceRead)
        {
            nextPresenceRead=DateTime.UtcNow.AddSeconds(5);
            try{presenceDetails=session!.ReadPresenceDetails(choices);}
            catch(Exception ex)when(IsExpected(ex)){presenceDetails=null;}
        }
        discordPresence.Update(runtime,presenceDetails);
        DiscordPresenceStatus.Text=enabled?discordPresence.Status:"Discord Rich Presence is off.";
    }
    private void DiscordPresenceChanged(object sender,RoutedEventArgs e)
    {
        if(updating||DiscordPresenceEnabled==null||DiscordPresenceStatus==null)return;
        if(DiscordExtra==null||DiscordArea==null||DiscordNg==null)return;
        settings=settings with{DiscordRichPresence=DiscordPresenceEnabled.IsChecked==true,
            DiscordDetails=new(DiscordExtra.IsChecked==true,DiscordArea.IsChecked==true,DiscordLevel.IsChecked==true,DiscordDeaths.IsChecked==true,DiscordName.IsChecked==true,DiscordNg.IsChecked==true)};
        nextPresenceRead=DateTime.MinValue;presenceDetails=null;
        UpdateDiscordPresence();
        try{settings.Save();}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text="Discord preference changed for this session but could not be saved: "+ex.Message;}
    }
}
