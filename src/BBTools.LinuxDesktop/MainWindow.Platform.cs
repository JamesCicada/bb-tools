using Avalonia.Controls;
using Avalonia.Threading;
using BBTools.Memory;

namespace BBTools.LinuxDesktop;

public partial class MainWindow
{
    private X11GameWindow? x11;
    private readonly DispatcherTimer inputTimer=new(){Interval=TimeSpan.FromMilliseconds(33)};
    private StackPanel explorationPanel=new();
    private readonly HashSet<string> pressed=[];
    private bool inputBusy;
    private long inputTime;
    private SavedLocation? hotkeyLocation;
    private CameraControls controls=new();
    private HotkeyPreferences hotkeys=new();
    private void InitPlatform()
    {
        controls=preferences.Camera??new();hotkeys=preferences.Hotkeys??new();
        inputTimer.Tick+=(_,_)=>InputTick();if(!preview)inputTimer.Start();
    }
    private void RequireX11(){x11??=new();}
    private void BuildExploration(StackPanel panel)
    {
        var enable=Switch(panel,"experimental","Enable experimental photo mode / noclip",preferences.ExperimentalExploration);
        explorationPanel=Column();explorationPanel.IsVisible=enable.IsChecked==true;
        enable.IsCheckedChanged+=async(_,_)=>
        {
            try
            {
                if(enable.IsChecked!=true&&connection?.Editing==true)await Native(f=>f.RunAsync(_=>{f.Photo.Restore();f.Flight.Restore();return Task.CompletedTask;},true));
                preferences=preferences with{ExperimentalExploration=enable.IsChecked==true};SavePreferences();explorationPanel.IsVisible=enable.IsChecked==true;
            }
            catch(Exception ex)when(Expected(ex)){enable.IsChecked=true;Report(ex);}
        };
        Add(explorationPanel,Label("Experimental · not tested in-game on Linux. X11/XWayland input and window capture are required. Changes restore on exit while the original player remains valid."));
        var lockHunter=Switch(explorationPanel,"lockHunter","Lock player movement during photo mode",true);
        var hide=Switch(explorationPanel,"photoHud","Hide HUD during photo mode",true);
        Value(explorationPanel,"photoSpeed","Camera movement speed",1,.05m,10,.1m);
        Add(explorationPanel,Button("Enter photo mode",async()=>
        {
            RequireX11();await Native(f=>f.RunAsync(async _=>{if(f.Flight.RestorationPending)throw new IOException("Disable flight first.");await f.Photo.PrepareAsync(lifetime.Token);f.Photo.Enable();f.Photo.LockHunter(lockHunter.IsChecked==true);if(hide.IsChecked==true)f.Hud.Apply();}));
        }),Button("Exit photo mode and restore HUD",()=>Native(f=>f.RunAsync(_=>{f.Photo.Restore();if(!On("hud"))f.Hud.Restore();return Task.CompletedTask;},true))));
        var noClip=Switch(explorationPanel,"noclip","Disable world collision while flying");
        Add(explorationPanel,Button("Enable flight",()=>{RequireX11();return NativeAction(f=>{if(f.Photo.IsActive)throw new IOException("Exit photo mode first.");f.Flight.Apply(noClip.IsChecked==true);});}),Button("Disable flight",()=>NativeAction(f=>f.Flight.Restore())),Button("Return to flight start",()=>Native(f=>{var at=f.Flight.ReadReturnPoint();return f.RunAsync(async a=>{f.Flight.Restore();await a.RestoreLocationAsync(at);});})));
        var filter=Picker(PhotoAdjustments.Filters);filter.SelectedItem=preferences.Photos?.Adjustments?.Filter??"Original";
        Add(explorationPanel,Label("PNG capture · native window resolution"),filter);Value(explorationPanel,"exposure","Exposure",0,-2,2,.1m);Value(explorationPanel,"contrast","Contrast",1,.5m,1.5m,.05m);Value(explorationPanel,"saturation","Saturation",1,0,2,.05m);Value(explorationPanel,"vignette","Vignette",0,0,.8m,.05m);
        var folder=new TextBox{Text=preferences.Photos?.OutputFolder??new PhotoSettings().OutputFolder};Add(explorationPanel,Label("Photo folder"),folder,Button("Save photo settings",()=>{preferences=preferences with{Photos=new(folder.Text,new(Selected<string>(filter),V("exposure"),V("contrast"),V("saturation"),V("vignette")))};preferences.Validate();SavePreferences();return Task.CompletedTask;},false),Label("Focus the game and press the configured capture key. The unmodified original and filtered PNG are saved together; filters affect the saved photo."));
        var bindings=new List<ComboBox>();
        for(var i=0;i<CameraControls.Actions.Length;i++){var box=Picker(CameraControls.Choices);box.SelectedItem=(preferences.Camera??new()).Keys[i];Add(explorationPanel,Label(CameraControls.Actions[i]),box);bindings.Add(box);}
        Add(explorationPanel,Button("Save camera controls",()=>{var k=bindings.Select(Selected<string>).ToArray();var next=new CameraControls(k[0],k[1],k[2],k[3],k[4],k[5],k[6],k[7],k[8],k[9],k[10],k[11],k[12],k[13],k[14]);next.Validate();controls=next;preferences=preferences with{Camera=next};SavePreferences();return Task.CompletedTask;},false));
        var enabled=Switch(panel,"hotkeys","Enable game hotkeys (X11/XWayland)",preferences.Hotkeys?.Enabled==true);var modifiers=Picker(HotkeyPreferences.ModifierChoices);modifiers.SelectedItem=(preferences.Hotkeys??new()).Modifiers;
        var choices=new List<ComboBox>();foreach(var (name,index) in new[]{"Heal","Toggle God mode","Save location","Restore location","Retry boss"}.Select((x,i)=>(x,i))){var key=Picker(KeyCodes.FunctionKeys);key.SelectedItem=(preferences.Hotkeys??new()).Keys[index];Add(panel,Label(name),key);choices.Add(key);}
        Add(panel,modifiers,Button("Save hotkeys",()=>{var k=choices.Select(Selected<string>).ToArray();var next=new HotkeyPreferences(enabled.IsChecked==true,k[0],k[1],k[2],k[3],k[4],Selected<string>(modifiers));next.Validate();if(next.Enabled)RequireX11();hotkeys=next;preferences=preferences with{Hotkeys=next};SavePreferences();return Task.CompletedTask;},false),explorationPanel);
    }
    private async void InputTick()
    {
        if(inputBusy||busy||connection?.Editing!=true)return;
        var features=connection.Features;
        if(!hotkeys.Enabled&&!features.Photo.IsActive&&!features.Flight.RestorationPending)return;
        inputBusy=true;
        try
        {
            RequireX11();var focused=x11!.Poll(connection.Pid);var now=Environment.TickCount64;var dt=Math.Clamp((now-inputTime)/1000f,0,.1f);inputTime=now;
            if(!focused){pressed.Clear();return;}
            bool Edge(string name,bool held){if(!held){pressed.Remove(name);return false;}return pressed.Add(name);}
            if(features.Photo.IsActive||features.Flight.RestorationPending)
            {
                var input=controls.Read(x11.Down);var photo=features.Photo.IsActive;
                if(input.Exit){await features.RunAsync(_=>{features.Photo.Restore();features.Flight.Restore();if(!On("hud"))features.Hud.Restore();return Task.CompletedTask;},true);return;}
                if(photo)
                {
                    if(Edge("reset",input.Reset))features.Photo.Reset();
                    features.Photo.Step(input.Forward,input.Right,input.Up,input.Yaw,input.Pitch,dt*V("photoSpeed")*input.Speed,features.Photo.InitialFov,input.Roll,dt);
                    if(Edge("capture",input.Capture))
                    {
                        var original=x11.Capture(connection.Pid);var settings=preferences.Photos??new();
                        var processed=await Task.Run(()=>PhotoProcessing.Apply(original,settings.Adjustments??new(),lifetime.Token));
                        var path=X11GameWindow.Save(original,settings.OutputFolder,"original");X11GameWindow.Save(processed,settings.OutputFolder,"filtered");Status.Text="Saved "+path;
                    }
                }
                else features.Flight.Step(input.Forward,input.Right,input.Up,dt*V("photoSpeed")*input.Speed);
                return;
            }
            var modifiers=(!hotkeys.Modifiers.Contains("Ctrl")||x11.Down(0x11))&&(!hotkeys.Modifiers.Contains("Alt")||x11.Down(0x12));
            for(var i=0;i<hotkeys.Keys.Length;i++)if(Edge("hotkey"+i,modifiers&&x11.Down(KeyCodes.VirtualKey(hotkeys.Keys[i]))))
            {
                switch(i)
                {
                    case 0:connection.SetHealth(connection.Session.ReadPlayer().MaxHealth);break;
                    case 1:if(!connection.Flags.Remove(PracticeFlag.GodMode))connection.Flags.Add(PracticeFlag.GodMode);break;
                    case 2:hotkeyLocation=connection.Session.ReadLocation("Hotkey return point");break;
                    case 3:await features.RunAsync(a=>a.RestoreLocationAsync(hotkeyLocation??throw new IOException("Save a location first.")));break;
                    case 4:throw new IOException("Select a boss in Travel and use Revive boss.");
                }
            }
        }
        catch(Exception ex)when(Expected(ex)){Report(ex);}
        finally{inputBusy=false;}
    }
}
