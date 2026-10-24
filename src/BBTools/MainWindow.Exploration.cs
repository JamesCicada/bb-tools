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
            Status.Text=Translations.T("message.CameracontrolssavedPhotomoderemainsanexplicitsession.51477be");
        }
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void DefaultCameraControls(object sender,RoutedEventArgs e)
    {
        try{var next=settings with{Camera=new()};next.Save();settings=next;LoadCameraControls();Status.Text=Translations.T("message.DefaultWASDcameracontrolsrestored.a6851b2");}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void PhotoFovChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    { if(PhotoFovLabel!=null)PhotoFovLabel.Text=Translations.Format("message.Photozoom00FOV.45069cf", PhotoFov.Value); }
    private void ResetPhotoCamera(object sender,RoutedEventArgs e)
    {
        try{if(freeCamera?.IsActive!=true)throw new InvalidOperationException(Translations.T("message.Enablephotomodefirst.6951aa1"));freeCamera.Reset();PhotoFov.Value=Math.Clamp(freeCamera.InitialFov,10,120);Status.Text=Translations.T("message.PhotocameraresettoitsstartingpositionrotationandFOV.ba29c9e");}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void TogglePhotoMode(object sender,RoutedEventArgs e)
    {
        if(FreeCameraEnabled.IsChecked!=true&&freeCamera?.RestorationPending==true)
        {
            try{freeCamera.Restore();Status.Text=Translations.T("message.Photomoderestorationcompleted.4d15662");}
            catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=Translations.Format("message.Restorationpending0.41873b8", ex.Message);}
            UpdatePhotoModeState();return;
        }
        FreeCameraEnabled.IsChecked=FreeCameraEnabled.IsChecked!=true;
    }
    private void UpdatePhotoModeState()
    {
        if(PhotoToggleButton==null)return;
        var active=FreeCameraEnabled.IsChecked==true&&freeCamera?.IsActive==true;
        var pending=!active&&freeCamera?.RestorationPending==true;
        PhotoToggleButton.Content=photoStarting?Translations.T("message.Starting.bbe5fc3"):pending?Translations.T("message.Retrycamerarestore.c69c892"):active?Translations.T("message.Exitphotomode.ed439a9"):Translations.T("ui.Startphotomode.87d5c05");
        PhotoToggleButton.IsEnabled=!photoStarting;
        PhotoModeState.Text=photoStarting?Translations.T("message.Preparingcamera.7d31356"):pending?Translations.T("message.Restoringnormalcontrols.a5c73a6"):active?Translations.T("ui.Photomodeactive.9ec1e84"):Translations.T("ui.Readytocompose.20e5f18");
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
        catch(Exception ex)when(IsExpected(ex)){FreeCameraEnabled.IsChecked=false;App.LogError(ex);Status.Text=Translations.Format("message.Photooptionscouldnotapply0.79ff2ed", ex.Message);}
    }
    private async void FreeCameraChanged(object sender,RoutedEventArgs e)
    {
        if(FreeCameraEnabled==null||PhotoHideHud==null||PhotoLockHunter==null)return;
        if(FreeCameraEnabled.IsChecked==true)
        {
            if(!ExplorationAvailable){FreeCameraEnabled.IsChecked=false;Status.Text=Translations.T("message.Photomodeisdisabledinthisbuild.31f492f");return;}
            if(memory==null||session==null||actionBusy){FreeCameraEnabled.IsChecked=false;Status.Text=Translations.T("message.Connectandloadacharacterwaitforanyactivegameactionbe.d663675");return;}
            if(photoStarting)return;
            movementSpeed??=new MovementSpeed(memory,session);
            var observed=session;var generation=targetGeneration;var camera=freeCamera??=new FreeCamera(memory,session,movementSpeed);
            photoStarting=true;UpdatePhotoModeState();
            try
            {
                Status.Text=Translations.T("message.Findingtheactivenativecamera.ba1a9c1");
                await camera.PrepareAsync(updateLifetime.Token);
                if(closing||observed!=session||generation!=targetGeneration||FreeCameraEnabled.IsChecked!=true)return;
                FlightEnabled.IsChecked=false;NoclipEnabled.IsChecked=false;
                camera.Enable();camera.LockHunter(PhotoLockHunter.IsChecked==true);
                PhotoFov.Value=Math.Clamp(freeCamera.InitialFov,10,120);
                hudVisibility??=new HudVisibility(memory,session);if(PhotoHideHud.IsChecked==true||HideHud.IsChecked==true)hudVisibility.Apply();
                var input=CurrentCameraControls.Read(HotkeyManager.IsDown);resetHeld=input.Reset;exitHeld=input.Exit;photoCaptureHeld=input.Capture;
                explorationStamp=Stopwatch.GetTimestamp();Status.Text=Translations.T("message.PhotomodeenabledFocusthegametomovethecameraSettingss.177b0e4");
            }
            catch(Exception ex)when(IsExpected(ex)||ex is ObjectDisposedException){if(!closing&&observed==session&&generation==targetGeneration){FreeCameraEnabled.IsChecked=false;App.LogError(ex);Status.Text=Translations.Format("message.Photomodecouldnotstart0.558b62d", ex.Message);}}
            finally{photoStarting=false;UpdatePhotoModeState();}
            return;
        }
        if(freeCamera==null){UpdatePhotoModeState();return;}
        try
        {
            freeCamera.Restore();
            Status.Text=Translations.T("message.Photomodedisablednormalcamerarestored.a426ad5");
        }
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);explorationRestoreRetry=DateTime.UtcNow.AddSeconds(1);Status.Text=Translations.Format("message.Photomoderestorationpending0.4e56b1b", ex.Message);}
        finally{RestorePhotoHud();UpdatePhotoModeState();}
    }
    private void RestorePhotoHud()
    {
        photoHudRestorePending=true;photoHudRetry=DateTime.UtcNow.AddSeconds(1);
        try{if(HideHud.IsChecked==true)hudVisibility?.Apply();else hudVisibility?.Show();photoHudRestorePending=false;}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=Translations.Format("message.HUDrestorationpending0.3edc13b", ex.Message);}
    }
    private void ShowHudNow(object sender,RoutedEventArgs e)
    {
        if(memory==null||session==null){Status.Text=Translations.T("message.ConnecttothegamebeforerestoringtheHUD.835e121");return;}
        try
        {
            PhotoHideHud.IsChecked=false;HideHud.IsChecked=false;
            hudVisibility??=new HudVisibility(memory,session);hudVisibility.Show();
            photoHudRestorePending=false;
            Status.Text=Translations.T("message.HUDdisplayrestored.35f7e46");
        }
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=Translations.Format("message.HUDcouldnotberestored0.33bd0b3", ex.Message);}
    }
    private void FlightChanged(object sender,RoutedEventArgs e)
    {
        if(FlightEnabled==null||NoclipEnabled==null)return;
        if(!ExplorationAvailable&&(FlightEnabled.IsChecked==true||NoclipEnabled.IsChecked==true)){FlightEnabled.IsChecked=false;NoclipEnabled.IsChecked=false;Status.Text=Translations.T("message.Flightandnocliparedisabledinthisbuild.d7084e7");return;}
        if(ReferenceEquals(sender,NoclipEnabled)&&NoclipEnabled.IsChecked==true)FlightEnabled.IsChecked=true;
        if(FlightEnabled.IsChecked==true)
        {FreeCameraEnabled.IsChecked=false;explorationStamp=Stopwatch.GetTimestamp();return;}
        NoclipEnabled.IsChecked=false;
        try{flight?.Restore();}catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=Translations.Format("message.Flightrestorationpending0.4baa155", ex.Message);}
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
            catch(Exception ex)when(IsExpected(ex)){FreeCameraEnabled.IsChecked=false;App.LogError(ex);Status.Text=Translations.Format("message.Photomodestopped0.78b6731", ex.Message);}
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
            catch(Exception ex)when(IsExpected(ex)){FlightEnabled.IsChecked=false;NoclipEnabled.IsChecked=false;App.LogError(ex);Status.Text=Translations.Format("message.Flightnoclipstopped0.058e8a3", ex.Message);}
        }
        else if(flight?.RestorationPending==true && DateTime.UtcNow>=explorationRestoreRetry)
        {explorationRestoreRetry=DateTime.UtcNow.AddSeconds(1);try{flight.Restore();}catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);}}
    }
}
