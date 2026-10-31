using BBTools.Memory;

namespace BBTools.LinuxCore;

// Linux presentation uses the same game algorithms as Windows. This controller
// owns their lifetime and serializes native requests; UI code does not duplicate
// offsets, helper code or parameter layouts.
public sealed class LinuxGameFeatures
{
    private readonly LinuxGameConnection connection;
    private readonly IGameMemory memory;
    private readonly SemaphoreSlim gate=new(1,1);
    private long lastRepair;
    private bool multipliersRequested;
    private bool nativeTouched;
    private float damage=1,echoes=1;
    public GameActions Actions { get; }
    public CameraFov Fov { get; }
    public CameraTuning Camera { get; }
    public ShoulderCamera Shoulder { get; }
    public MovementSpeed Movement { get; }
    public WorldGravity Gravity { get; }
    public HudVisibility Hud { get; }
    public LootOverrides Loot { get; }
    public ProgressionCosts Progression { get; }
    public FreeCamera Photo { get; }
    public ExplorationFlight Flight { get; }
    public EnemyPracticeProtection TargetProtection { get; }
    private readonly TargetMonitor target=new();
    public EncounterTimer Timer { get; }=new();
    public PracticePreset? Desired { get; private set; }
    public string EnemyDetails { get; private set; }="Lock onto an enemy to inspect it.";
    public bool Paused {get;private set;}
    public string? Error {get;private set;}
    public void Resume(){Paused=false;Error=null;}
    public bool Busy=>gate.CurrentCount==0;
    public LinuxGameFeatures(LinuxGameConnection connection,IGameMemory memory)
    {
        this.connection=connection;this.memory=memory;var session=connection.Session;
        Actions=new(memory,session);Fov=new(memory,session);Camera=new(memory,session);Shoulder=new(memory,session);
        Movement=new(memory,session);Gravity=new(memory,session);Hud=new(memory,session);Loot=new(memory,session);Progression=new(memory,session);
        Photo=new(memory,session,Movement);Flight=new(memory,session);TargetProtection=new(session);
    }
    public async Task RunAsync(Func<GameActions,Task> operation,bool allowPhoto=false)
    {
        if(!connection.Editing)throw new InvalidOperationException("Enable game editing before using this feature.");
        if(Photo.IsActive&&!allowPhoto)throw new IOException("Exit photo mode before submitting native game actions.");
        await gate.WaitAsync();
        try{nativeTouched=true;connection.Session.ReadPlayer();await operation(Actions);}
        finally{gate.Release();}
    }
    public void PlayerUnavailable(string reason){target.Unavailable("Player unavailable");Timer.Context(false,false);EnemyDetails="Last known snapshot · "+reason+"\n"+EnemyDetails.Split('\n',2).Last();}
    public InventoryGem[] ReadInventoryGems()=>new BloodGems(memory,connection.Session).ReadInventory();
    public Task MutateAsync(Action operation)=>RunAsync(_=>{memory.Suspended(operation);return Task.CompletedTask;});
    public async Task SetMultipliersAsync(float outgoing,float rewards)
    {
        await RunAsync(a=>a.SetMultipliersAsync(outgoing,rewards));damage=outgoing;echoes=rewards;multipliersRequested=true;
    }
    public async Task ApplyPresetAsync(PracticePreset preset)
    {
        preset.Validate();if(!connection.Editing)throw new InvalidOperationException("Enable game editing first.");
        await SetMultipliersAsync(preset.Damage,preset.Echoes);
        Desired=preset;Resume();
        connection.KeepHealth=preset.KeepHealth;connection.InfiniteStamina=preset.Stamina;connection.Flags.Clear();
        foreach(var flag in preset.Flags)connection.Flags.Add(Enum.Parse<PracticeFlag>(flag));
        await TickAsync();
    }
    public async Task TickAsync()
    {
        var session=connection.Session;var player=session.ReadPlayer();Timer.Context(true,player.Health>0);
        target.Poll(session);
        if(target.Snapshot is {} enemy)
            EnemyDetails=$"{target.State}\n{enemy.Title}\nHP {enemy.Health:N0} / {enemy.Maximum:N0} · Stamina {enemy.Stamina:N0} / {enemy.MaxStamina:N0}\n"+
                (enemy.Armor?.Summary??"Super armor unavailable")+"\n"+string.Join("\n",enemy.Effects.Select(x=>x.Summary))+"\n"+
                string.Join(" · ",enemy.Gauges.Select(x=>$"{x.Name}: {x.Current}/{x.Limit}"));
        if(!connection.Editing||Paused||Photo.IsActive||!await gate.WaitAsync(0))return;
        try
        {
            if(multipliersRequested&&(damage!=1||echoes!=1)&&!Actions.MaintainMultipliers())await Actions.SetMultipliersAsync(damage,echoes);
            if(Desired is {} preset)
            {
                if(preset.OverrideFov)Fov.Apply(preset.Fov);else Fov.Restore();
                if(preset.TuneCamera)Camera.Apply(preset.CameraDistance,preset.CameraHeight);else Camera.Restore();
                Shoulder.Apply(preset.CameraHorizontal);
                var advanced=preset.Advanced??new();
                if(advanced.MovementEnabled)Movement.Apply(advanced.Movement);else Movement.Restore();
                if(advanced.GravityEnabled)Gravity.Apply(advanced.Gravity);else Gravity.Restore();
                if(advanced.HideHud)Hud.Apply();else Hud.Restore();
                if(advanced.DiscoveryEnabled||advanced.GuaranteedDrops)Loot.Apply(advanced.DiscoveryEnabled?advanced.DiscoveryBonus:0,advanced.GuaranteedDrops);else Loot.Restore();
                if(advanced.FreeLevels||advanced.FreeUpgrades)Progression.Apply(advanced.FreeLevels,advanced.FreeUpgrades);else Progression.Restore();
                if(preset.KeepStatusEmpty)session.ClearStatusBuildup();
                if((preset.KeepDurability||preset.AutoRepair)&&Environment.TickCount64-lastRepair>=2000)
                {lastRepair=Environment.TickCount64;await Actions.RepairWeaponsNativeAsync(preset.KeepDurability?100:preset.RepairThreshold);}
            }
            if(TargetProtection.IsActive&&TargetProtection.NeedsUpdate())memory.Suspended(TargetProtection.Update);
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {Paused=true;Error=ex.Message;throw;}
        finally{gate.Release();}
    }
    public void Stop()
    {
        Desired=null;multipliersRequested=false;
        if(!nativeTouched)return;
        if(!memory.IsAlive)return;
        if(!gate.Wait(0))throw new IOException("A native game request is still running. Wait for completion before disconnecting.");
        try
        {
            var errors=new List<string>();
            foreach(var restore in new Action[]{Photo.Restore,Flight.Restore,()=>{if(TargetProtection.IsActive)memory.Suspended(TargetProtection.Restore);},Actions.ResetMultipliers,Fov.Restore,Camera.Restore,Shoulder.Restore,Movement.Restore,Gravity.Restore,Hud.Restore,Loot.Restore,Progression.Restore,Actions.Dispose})
                try{restore();}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){errors.Add(ex.Message);}
            if(errors.Count>0)throw new IOException("Restoration pending: "+string.Join(" ",errors.Distinct()));
            nativeTouched=false;Resume();
        }
        finally{gate.Release();}
    }
}
