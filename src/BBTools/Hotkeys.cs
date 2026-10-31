using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;
namespace BBTools;

// Poll the physical key state: SDL/fullscreen input need not deliver WM_HOTKEY.
// Never swallow the game's keyboard input. MainWindow checks focus before acting.
internal sealed class HotkeyManager : IDisposable
{
    private readonly Action<int> pressed;
    private readonly Func<int,bool> down;
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(25)};
    private HotkeyPreferences preferences=new();
    private readonly bool[] held=new bool[5];
    private bool disposed;
    public HotkeyManager(nint window,Action<int> pressed,Func<int,bool>? keyState=null)
    {
        this.pressed=pressed;down=keyState??(key=>(GetAsyncKeyState(key)&0x8000)!=0);
        timer.Tick+=Tick;
    }
    public void Apply(HotkeyPreferences preferences)
    {
        preferences.Validate();timer.Stop();this.preferences=preferences;
        // Applying a binding while it is held must not execute an action.
        for(var i=0;i<held.Length;i++)held[i]=down(VirtualKey(i));
        if(preferences.Enabled&&!disposed)timer.Start();
    }
    private int VirtualKey(int index)=>KeyInterop.VirtualKeyFromKey(Enum.Parse<Key>(preferences.Keys[index],true));
    private void Tick(object? sender,EventArgs e)=>Poll();
    internal void Poll()
    {
        if(disposed||!preferences.Enabled)return;
        var ctrl=down(0x11);var alt=down(0x12);
        var modifiers=ctrl==preferences.Modifiers.Contains("Ctrl")&&alt==preferences.Modifiers.Contains("Alt")&&!down(0x10)&&!down(0x5B)&&!down(0x5C);
        for(var i=0;i<held.Length;i++)
        {
            var now=down(VirtualKey(i));var trigger=now&&!held[i]&&modifiers;held[i]=now;
            if(trigger)pressed(i);
        }
    }
    public void Dispose(){disposed=true;timer.Stop();timer.Tick-=Tick;}
    internal static bool IsDown(int key)=>(GetAsyncKeyState(key)&0x8000)!=0;
    public static nint ForegroundWindow => GetForegroundWindow();
    public static int ForegroundProcess(){GetWindowThreadProcessId(GetForegroundWindow(),out var pid);return (int)pid;}
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
}
