using System.IO;

namespace BBTools.Memory;

public sealed partial class PlayerSession
{
    // Native lethal-HP path calls 1A577D0, which honors bit 4 for every actor.
    // Bit 2 would suppress incoming damage entirely, defeating this drill.
    public static readonly (ulong Offset,byte[] Bytes)[] ParryPracticeFingerprints =
    [
        (0x1A577E0,Convert.FromHexString("F6830002000004")),
        (0x1A2FDCF,Convert.FromHexString("E8FC79020084C0740D498D4C240C41BD01000000"))
    ];
    internal (InspectionContext Context,PlayerState Enemy) ReadPracticeEnemy(InspectableCharacter selected,bool requireAlive=true)
    {
        ValidateTargetProfile();
        foreach(var f in ParryPracticeFingerprints)
            if(!memory.Read(imageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes))throw new IOException("Enemy practice instructions differ from the supported 1.09 build.");
        var context=ReadInspectionContext();
        if((requireAlive&&ReadPlayer().Health==0)||selected.Address==context.Player)throw new IOException("Load a living hunter and lock onto an enemy first.");
        if(ResolveInspectionHandle(selected.Handle)!=selected.Address||unchecked((uint)memory.Int32(selected.Address+8))!=selected.Handle)
            throw new IOException("Practice target changed or unloaded.");
        var stats=memory.Pointer(memory.Pointer(selected.Address+0x3B0)+0x20);
        if(memory.Pointer(stats+8)!=selected.Address)throw new IOException("Practice target resource ownership changed.");
        var hp=memory.Int32(stats+0xF8);var maximum=memory.Int32(stats+0xFC);
        if(maximum is <1 or >100000000||hp<0||(requireAlive&&hp==0)||hp>maximum)throw new IOException("Practice target must be alive with valid health.");
        if(ReadInspectionContext()!=context||ResolveInspectionHandle(selected.Handle)!=selected.Address||memory.Pointer(memory.Pointer(selected.Address+0x3B0)+0x20)!=stats)
            throw new IOException("Practice target changed during validation.");
        return (context,new(selected.Address,stats,hp,maximum,0,0));
    }
    public void RestoreLockedEnemyHealth()
    {
        var target=ReadLockedEnemy()??throw new IOException("Lock onto a living enemy before restoring its health.");
        var first=ReadPracticeEnemy(target);var second=ReadPracticeEnemy(target);
        if(first!=second||ReadLockedEnemy()!=target)throw new IOException("Target changed before health restoration.");
        memory.SetInt32(first.Enemy.Component+0xF8,first.Enemy.MaxHealth);
    }
    internal byte ReadPracticeByte(ulong component)=>memory.Read(component+0x200,1)[0];
    internal void WritePracticeByte(ulong component,byte value)=>memory.Write(component+0x200,[value]);
}

// Caller serializes mutations while game threads are paused. Only bit 4 is owned.
public sealed class EnemyPracticeProtection(PlayerSession session)
{
    private InspectableCharacter? target;
    private InspectionContext context;
    private ulong component;
    private byte original;
    public bool IsActive=>target!=null;
    public InspectableCharacter? Target=>target;
    public void ProtectLockedEnemy()
    {
        var selected=session.ReadLockedEnemy()??throw new IOException("Lock onto a living enemy to enable target protection.");
        var current=session.ReadPracticeEnemy(selected);
        if(target!=selected||context!=current.Context||component!=current.Enemy.Component)
        {
            Restore();current=session.ReadPracticeEnemy(selected);
            target=selected;context=current.Context;component=current.Enemy.Component;
            original=(byte)(session.ReadPracticeByte(component)&4);
        }
        Apply();
    }
    public bool NeedsUpdate()
    {
        if(target==null)return false;
        var selected=session.ReadLockedEnemy();if(selected!=null&&selected!=target)return true;
        var current=session.ReadPracticeEnemy(target);
        if(current.Context!=context||current.Enemy.Component!=component)throw new IOException("Practice target context changed. Re-enable protection after loading.");
        return (session.ReadPracticeByte(component)&4)==0;
    }
    public void Update()
    {
        if(target==null)return;
        var selected=session.ReadLockedEnemy();
        if(selected!=null&&selected!=target){ProtectLockedEnemy();return;}
        Apply();
    }
    private void Apply()
    {
        if(target==null)return;
        var current=session.ReadPracticeEnemy(target);
        if(current.Context!=context||current.Enemy.Component!=component)throw new IOException("Practice target context changed. Re-enable protection after loading.");
        var value=session.ReadPracticeByte(component);
        if((value&4)==0)session.WritePracticeByte(component,(byte)(value|4));
    }
    public void Restore()
    {
        if(target==null)return;
        // An identity/context loss drops ownership without touching a retired actor.
        var selected=target;var savedContext=context;var savedComponent=component;var savedOriginal=original;
        (InspectionContext Context,PlayerState Enemy) current;
        try{current=session.ReadPracticeEnemy(selected,false);}
        catch(IOException){target=null;component=0;return;}
        if(current.Context!=savedContext||current.Enemy.Component!=savedComponent){target=null;component=0;return;}
        var value=session.ReadPracticeByte(savedComponent);
        session.WritePracticeByte(savedComponent,(byte)((value&~4)|savedOriginal));
        target=null;component=0;
    }
}
