using BBTools.Memory;

namespace BBTools.LinuxCore;

public sealed record LinuxSnapshot(PlayerState Player,CharacterState Character);
public sealed class LinuxGameConnection : IDisposable
{
    private readonly LinuxProcessMemory memory;
    private readonly string identity;
    private FileStream? editLease;
    public PlayerSession Session { get; }
    public int Pid => memory.Pid;
    public bool Editing => memory.WritesEnabled;
    public bool KeepHealth { get; set; }
    public bool InfiniteStamina { get; set; }
    public HashSet<PracticeFlag> Flags { get; }=[];
    public bool FeaturesPaused { get; private set; }
    public string? FeatureError { get; private set; }
    public bool CombatFlagsAvailable { get; }
    public LinuxGameConnection(LinuxCandidate process,CancellationToken cancellationToken=default)
    {
        memory=new(process.Pid);
        identity=process.Identity;
        if(process.Identity.Length==0||LinuxProcessMemory.ParseIdentity(File.ReadAllText($"/proc/{process.Pid}/stat"))!=process.Identity)
            throw new IOException("The process changed or is restricted. Refresh and select it again.");
        var image=LinuxImageLocator.Find(memory,memory.Maps(),TimeSpan.FromSeconds(20),cancellationToken);
        Session=new(memory,image);Session.ValidateImage();
        // These options require their own unmodified instruction profile.
        try{CombatFlagsAvailable=PlayerSession.FeatureFingerprints.All(f=>memory.Read(image+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes));}
        catch(IOException){CombatFlagsAvailable=false;}
    }
    public LinuxSnapshot ReadAndUpdate()
    {
        Session.ValidateImage();var player=Session.ReadPlayer();var character=Session.ReadCharacter();
        if(Editing&&!FeaturesPaused)
        {
            try
            {
                if(KeepHealth)Session.KeepHealthFull();
                if(InfiniteStamina)Session.SetInfiniteStamina(true);else Session.RestoreStamina();
                if(CombatFlagsAvailable)foreach(var flag in Enum.GetValues<PracticeFlag>())
                    if(Flags.Contains(flag))Session.SetPracticeFlag(flag,true);else Session.RestorePracticeFlag(flag);
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {FeaturesPaused=true;FeatureError=ex.Message;}
        }
        return new(player,character);
    }
    public void SetEditing(bool enabled)
    {
        if(!enabled){Stop();memory.WritesEnabled=false;editLease?.Dispose();editLease=null;return;}
        Session.ValidateImage();Session.ReadCharacter();editLease??=LinuxEditLease.Acquire(Pid,identity);memory.WritesEnabled=true;Resume();
    }
    public void Resume(){FeaturesPaused=false;FeatureError=null;}
    public void Stop()
    {
        KeepHealth=InfiniteStamina=false;Flags.Clear();
        if(!Editing)return;
        if(memory.HasExited())return;
        // A failed restore keeps the connection available so cleanup can be
        // retried after loading. Never report success after dropping ownership.
        var errors=new List<string>();
        try{Session.RestoreStamina();}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){errors.Add(ex.Message);}
        try{Session.RestorePracticeFlags();}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){errors.Add(ex.Message);}
        if(errors.Count>0)throw new IOException("Restoration pending: "+string.Join(" ",errors.Distinct()));
        Resume();
    }
    public void SetHealth(int value){RequireEditing();Session.SetHealth(value);}
    public void SetEchoes(int value){RequireEditing();Session.SetEchoes(value);}
    public void SetAttributes(int[] attributes,CharacterState expected){RequireEditing();Session.SetAttributes(attributes,expected);}
    private void RequireEditing(){if(!Editing)throw new InvalidOperationException("Enable experimental data editing first.");}
    public void Dispose(){Stop();memory.WritesEnabled=false;editLease?.Dispose();editLease=null;}
}
