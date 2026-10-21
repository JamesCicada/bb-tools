using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BBTools.Memory;

namespace BBTools;

public partial class MainWindow
{
    private static bool ExplorationAvailable=>false;
    private readonly DispatcherTimer explorationTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private long explorationStamp;
    private bool resetHeld, exitHeld, photoStarting,photoHudRestorePending;
    private DateTime explorationRestoreRetry,photoHudRetry;
    public string[] CameraKeyChoices => CameraControls.Choices;
    private static readonly CameraControls defaultCameraControls = new();
    private CameraControls CurrentCameraControls => settings.Camera ?? defaultCameraControls;

    private void InitializeExploration()
    {
        LoadCameraControls();
        UpdatePhotoModeState();
        explorationStamp = Stopwatch.GetTimestamp();
        explorationTimer.Tick += ExplorationTick;
        // Photo mode and flight/noclip are temporarily unavailable in 0.6.0.
        // Keep their native implementations for later testing, without polling/input.
        if(ExplorationAvailable)explorationTimer.Start();
        Closed += (_,_) => { explorationTimer.Stop(); explorationTimer.Tick -= ExplorationTick; };
    }
    private void LoadCameraControls()
    {
        var keys=CurrentCameraControls.Keys;
        CameraBindingList.ItemsSource=CameraControls.Actions.Select((name,index)=>new CameraBinding(name,keys[index])).ToArray();
        PhotoControlsSummary.Text=CurrentCameraControls.Summary;
    }
    private void SaveCameraControls(object sender,RoutedEventArgs e)
    {
        try
        {
            var keys=CameraBindingList.Items.Cast<CameraBinding>().Select(x=>x.Key).ToArray();
            var controls=new CameraControls(keys[0],keys[1],keys[2],keys[3],keys[4],keys[5],keys[6],keys[7],keys[8],keys[9],keys[10],keys[11],keys[12],keys[13],keys[14]);
            controls.Validate();var next=settings with{Camera=controls};next.Save();settings=next;LoadCameraControls();
            var input=controls.Read(HotkeyManager.IsDown);resetHeld=input.Reset;exitHeld=input.Exit;photoCaptureHeld=input.Capture;
            Status.Text="Camera controls saved. Photo mode remains an explicit session action.";
        }
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void DefaultCameraControls(object sender,RoutedEventArgs e)
    {
        try{var next=settings with{Camera=new()};next.Save();settings=next;LoadCameraControls();Status.Text="Default WASD camera controls restored.";}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void PhotoFovChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    { if(PhotoFovLabel!=null)PhotoFovLabel.Text=$"Photo zoom · {PhotoFov.Value:0}° FOV"; }
    private void ResetPhotoCamera(object sender,RoutedEventArgs e)
    {
        try{if(freeCamera?.IsActive!=true)throw new InvalidOperationException("Enable photo mode first.");freeCamera.Reset();PhotoFov.Value=Math.Clamp(freeCamera.InitialFov,10,120);Status.Text="Photo camera reset to its starting position, rotation and FOV.";}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void TogglePhotoMode(object sender,RoutedEventArgs e)
    {
        if(FreeCameraEnabled.IsChecked!=true&&freeCamera?.RestorationPending==true)
        {
            try{freeCamera.Restore();Status.Text="Photo-mode restoration completed.";}
            catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=$"Restoration pending: {ex.Message}";}
            UpdatePhotoModeState();return;
        }
        FreeCameraEnabled.IsChecked=FreeCameraEnabled.IsChecked!=true;
    }
    private void UpdatePhotoModeState()
    {
        if(PhotoToggleButton==null)return;
        var active=FreeCameraEnabled.IsChecked==true&&freeCamera?.IsActive==true;
        var pending=!active&&freeCamera?.RestorationPending==true;
        PhotoToggleButton.Content=photoStarting?"Starting…":pending?"Retry camera restore":active?"Exit photo mode":"Start photo mode";
        PhotoToggleButton.IsEnabled=!photoStarting;
        PhotoModeState.Text=photoStarting?"Preparing camera…":pending?"Restoring normal controls…":active?"Photo mode active":"Ready to compose";
    }
    private void PhotoOptionsChanged(object sender,RoutedEventArgs e)
    {
        if(photoStarting||freeCamera?.IsActive!=true||session==null||memory==null)return;
        try
        {
            freeCamera.LockHunter(PhotoLockHunter.IsChecked==true);
            hudVisibility??=new HudVisibility(memory,session);
            if(PhotoHideHud.IsChecked==true||HideHud.IsChecked==true)hudVisibility.Apply();else hudVisibility.Show();
        }
        catch(Exception ex)when(IsExpected(ex)){FreeCameraEnabled.IsChecked=false;App.LogError(ex);Status.Text=$"Photo options could not apply: {ex.Message}";}
    }
    private async void FreeCameraChanged(object sender,RoutedEventArgs e)
    {
        if(FreeCameraEnabled==null||PhotoHideHud==null||PhotoLockHunter==null)return;
        if(FreeCameraEnabled.IsChecked==true)
        {
            if(!ExplorationAvailable){FreeCameraEnabled.IsChecked=false;Status.Text="Photo mode is disabled in this build.";return;}
            if(memory==null||session==null||actionBusy){FreeCameraEnabled.IsChecked=false;Status.Text="Connect and load a character; wait for any active game action before photo mode.";return;}
            if(photoStarting)return;
            movementSpeed??=new MovementSpeed(memory,session);
            var observed=session;var generation=targetGeneration;var camera=freeCamera??=new FreeCamera(memory,session,movementSpeed);
            photoStarting=true;UpdatePhotoModeState();
            try
            {
                Status.Text="Finding the active native camera…";
                await camera.PrepareAsync(updateLifetime.Token);
                if(closing||observed!=session||generation!=targetGeneration||FreeCameraEnabled.IsChecked!=true)return;
                FlightEnabled.IsChecked=false;NoclipEnabled.IsChecked=false;
                camera.Enable();camera.LockHunter(PhotoLockHunter.IsChecked==true);
                PhotoFov.Value=Math.Clamp(freeCamera.InitialFov,10,120);
                hudVisibility??=new HudVisibility(memory,session);if(PhotoHideHud.IsChecked==true||HideHud.IsChecked==true)hudVisibility.Apply();
                var input=CurrentCameraControls.Read(HotkeyManager.IsDown);resetHeld=input.Reset;exitHeld=input.Exit;photoCaptureHeld=input.Capture;
                explorationStamp=Stopwatch.GetTimestamp();Status.Text="Photo mode enabled. Focus the game to move the camera; Settings shows your bindings. The world keeps running.";
            }
            catch(Exception ex)when(IsExpected(ex)||ex is ObjectDisposedException){if(!closing&&observed==session&&generation==targetGeneration){FreeCameraEnabled.IsChecked=false;App.LogError(ex);Status.Text=$"Photo mode could not start: {ex.Message}";}}
            finally{photoStarting=false;UpdatePhotoModeState();}
            return;
        }
        if(freeCamera==null){UpdatePhotoModeState();return;}
        try
        {
            freeCamera.Restore();
            Status.Text="Photo mode disabled; normal camera restored.";
        }
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);explorationRestoreRetry=DateTime.UtcNow.AddSeconds(1);Status.Text=$"Photo-mode restoration pending: {ex.Message}";}
        finally{RestorePhotoHud();UpdatePhotoModeState();}
    }
    private void RestorePhotoHud()
    {
        photoHudRestorePending=true;photoHudRetry=DateTime.UtcNow.AddSeconds(1);
        try{if(HideHud.IsChecked==true)hudVisibility?.Apply();else hudVisibility?.Show();photoHudRestorePending=false;}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=$"HUD restoration pending: {ex.Message}";}
    }
    private void ShowHudNow(object sender,RoutedEventArgs e)
    {
        if(memory==null||session==null){Status.Text="Connect to the game before restoring the HUD.";return;}
        try
        {
            PhotoHideHud.IsChecked=false;HideHud.IsChecked=false;
            hudVisibility??=new HudVisibility(memory,session);hudVisibility.Show();
            photoHudRestorePending=false;
            Status.Text="HUD display restored.";
        }
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=$"HUD could not be restored: {ex.Message}";}
    }
    private void FlightChanged(object sender,RoutedEventArgs e)
    {
        if(FlightEnabled==null||NoclipEnabled==null)return;
        if(!ExplorationAvailable&&(FlightEnabled.IsChecked==true||NoclipEnabled.IsChecked==true)){FlightEnabled.IsChecked=false;NoclipEnabled.IsChecked=false;Status.Text="Flight and noclip are disabled in this build.";return;}
        if(ReferenceEquals(sender,NoclipEnabled)&&NoclipEnabled.IsChecked==true)FlightEnabled.IsChecked=true;
        if(FlightEnabled.IsChecked==true)
        {FreeCameraEnabled.IsChecked=false;explorationStamp=Stopwatch.GetTimestamp();return;}
        NoclipEnabled.IsChecked=false;
        try{flight?.Restore();}catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=$"Flight restoration pending: {ex.Message}";}
    }
    private void ExplorationTick(object? sender,EventArgs e)
    {
        var now=Stopwatch.GetTimestamp();var elapsed=Math.Clamp((float)Stopwatch.GetElapsedTime(explorationStamp,now).TotalSeconds,0,.1f);explorationStamp=now;
        if(closing||memory==null||session==null||photoStarting)return;
        var focused=HotkeyManager.ForegroundProcess()==connectedPid&&!HotkeyManager.IsDown(0x5B)&&!HotkeyManager.IsDown(0x5C);
        if(FreeCameraEnabled.IsChecked==true)
        {
            try
            {
                freeCamera!.ValidateContext();freeCamera.LockHunter(PhotoLockHunter.IsChecked==true);
                if(!focused){resetHeld=exitHeld=photoCaptureHeld=false;return;}
                var input=CurrentCameraControls.Read(HotkeyManager.IsDown);
                var reset=input.Reset&&!resetHeld;var exit=input.Exit&&!exitHeld;resetHeld=input.Reset;exitHeld=input.Exit;
                if(input.Capture&&!photoCaptureHeld)_=CapturePhotoAsync(0);photoCaptureHeld=input.Capture;
                if(exit){FreeCameraEnabled.IsChecked=false;return;}
                if(reset){ResetPhotoCamera(this,new RoutedEventArgs());return;}
                freeCamera.Step(input.Forward,input.Right,input.Up,input.Yaw,input.Pitch,(float)FreeCameraSpeed.Value*elapsed*input.Speed,
                    (float)PhotoFov.Value,input.Roll,(float)PhotoTurnSpeed.Value*MathF.PI/180*elapsed);
            }
            catch(Exception ex)when(IsExpected(ex)){FreeCameraEnabled.IsChecked=false;App.LogError(ex);Status.Text=$"Photo mode stopped: {ex.Message}";}
        }
        else if(freeCamera?.RestorationPending==true && DateTime.UtcNow>=explorationRestoreRetry)
        {explorationRestoreRetry=DateTime.UtcNow.AddSeconds(1);try{freeCamera.Restore();UpdatePhotoModeState();}catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);}}
        if(FreeCameraEnabled.IsChecked!=true&&photoHudRestorePending&&DateTime.UtcNow>=photoHudRetry)RestorePhotoHud();
        if(FlightEnabled.IsChecked==true)
        {
            try
            {
                flight??=new ExplorationFlight(memory,session);flight.Apply(NoclipEnabled.IsChecked==true);
                if(!focused||actionBusy||!HotkeyManager.IsDown(0x11))return;
                var input=CurrentCameraControls.Read(HotkeyManager.IsDown);
                if(input.Forward!=0||input.Right!=0||input.Up!=0)
                    _=MoveFlight(flight.Step(input.Forward,input.Right,input.Up,(float)FlightSpeed.Value*elapsed*input.Speed));
            }
            catch(Exception ex)when(IsExpected(ex)){FlightEnabled.IsChecked=false;NoclipEnabled.IsChecked=false;App.LogError(ex);Status.Text=$"Flight/noclip stopped: {ex.Message}";}
        }
        else if(flight?.RestorationPending==true && DateTime.UtcNow>=explorationRestoreRetry)
        {explorationRestoreRetry=DateTime.UtcNow.AddSeconds(1);try{flight.Restore();}catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);}}
    }
}
