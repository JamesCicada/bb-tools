
namespace BBTools;

public sealed record CameraControls(string Forward="W",string Backward="S",string Left="A",string Right="D",
    string Up="E",string Down="Q",string YawLeft="Left",string YawRight="Right",string PitchUp="Up",string PitchDown="Down",
    string RollLeft="Z",string RollRight="C",string Reset="Home",string Exit="Escape",string? Capture=null)
{
    public static string[] Actions => ["Forward","Backward","Left","Right","Up","Down","Yaw left","Yaw right","Pitch up","Pitch down","Roll left","Roll right","Reset camera","Exit photo mode","Capture PNG"];
    public static readonly string[] Choices = KeyCodes.Choices;
    private string[] MovementKeys => [Forward,Backward,Left,Right,Up,Down,YawLeft,YawRight,PitchUp,PitchDown,RollLeft,RollRight,Reset,Exit];
    public string CaptureKey=>Capture??new[]{"F12","F11","F10","F9","F8"}.Concat(Choices).First(k=>!MovementKeys.Contains(k));
    public string[] Keys=>MovementKeys.Append(CaptureKey).ToArray();
    public void Validate()
    {
        if(Keys.Any(k=>!Choices.Contains(k))||Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Keys.Length)
            throw new ArgumentException("Choose a different supported key for each camera action. Shift and Alt are reserved for speed modifiers.");
    }
    internal CameraInput Read(Func<int,bool> down)
    {
        var keys=Keys;
        bool Held(int i)=>down(KeyCodes.VirtualKey(keys[i]));
        float Axis(int plus,int minus)=>(Held(plus)?1:0)-(Held(minus)?1:0);
        return new(Axis(0,1),Axis(3,2),Axis(4,5),Axis(6,7),Axis(8,9),Axis(10,11),Held(12),Held(13),down(0x10)?4:down(0x12)?.2f:1,Held(14));
    }
    public string Summary=>Translations.Format("experimental.summary",Keys.Cast<object?>().ToArray());
}
internal readonly record struct CameraInput(float Forward,float Right,float Up,float Yaw,float Pitch,float Roll,bool Reset,bool Exit,float Speed,bool Capture=false);
public sealed class CameraBinding(string action, string key)
{
    public string Action { get; } = action;
    public string Key { get; set; } = key;
}
