using System.Diagnostics;
namespace BBTools;

public sealed class EncounterTimer
{
    private readonly Func<TimeSpan> clock;
    private TimeSpan previous,elapsed;
    private bool running,ready,paused,lastAlive;
    public string Name {get;private set;}="No attempt";
    public int Attempts {get;private set;}
    public int Deaths {get;private set;}
    public TimeSpan? Best {get;private set;}
    public TimeSpan Elapsed=>elapsed;
    public bool Active=>running;
    public EncounterTimer(Func<TimeSpan>? clock=null){var timer=Stopwatch.StartNew();this.clock=clock??(()=>timer.Elapsed);previous=this.clock();}
    private void Account(){var now=clock();if(running && ready && !paused)elapsed+=now-previous;previous=now;}
    public void Start(string name){Account();if(Name!=name){Best=null;Attempts=Deaths=0;}Name=name;elapsed=TimeSpan.Zero;running=true;paused=false;Attempts++;}
    public void Context(bool loaded,bool alive){Account();if(running&&loaded&&lastAlive&&!alive)Deaths++;ready=loaded&&alive;if(loaded)lastAlive=alive;}
    public void TogglePause(){Account();paused=!paused;}
    public void Finish(){Account();if(running&&elapsed>TimeSpan.Zero&&(Best==null||elapsed<Best))Best=elapsed;running=false;}
    public void Reset(){running=false;elapsed=TimeSpan.Zero;Attempts=Deaths=0;Best=null;Name="No attempt";previous=clock();}
    private static string Format(TimeSpan time)=>$"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds/100}";
    public string Summary=>$"{Name} · {Format(elapsed)} · attempts {Attempts} · deaths {Deaths} · best {(Best is {} best?Format(best):"—")}{(paused?" · paused":"")}";
}
