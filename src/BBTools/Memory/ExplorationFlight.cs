using System.IO;
namespace BBTools.Memory;
public sealed class ExplorationFlight(IGameMemory memory,PlayerSession session)
{
    public static readonly (ulong Offset,byte[] Bytes)[] Fingerprints=
    [(0x18D5570,Convert.FromHexString("488B87B0030000488B40200FB78000020000")),(0xD35C63,Convert.FromHexString("F6871401000002"))];
    private ulong actor,component,controller;private uint handle;private byte gravityBit,collisionBit;private bool ownsCollision;
    private ulong anchorActor,anchorComponent;private uint anchorHandle;
    public bool RestorationPending => actor != 0;
    public SavedLocation? Anchor{get;private set;}
    public void Apply(bool noclip)
    {
        session.ValidateImage();foreach(var f in Fingerprints)if(!memory.Read(session.ImageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes))throw new IOException("Exploration instructions differ from the supported build.");
        var state=session.ReadPlayer();if(state.Health==0)throw new IOException("Flight requires a living character.");
        var observedHandle=unchecked((uint)memory.Int32(state.Actor+8));
        if(actor!=0&&(actor!=state.Actor||component!=state.Component||unchecked((uint)memory.Int32(state.Actor+8))!=handle))throw new IOException("Player changed. Disable flight and enable it again after loading.");
        var control=memory.Pointer(state.Actor+0x58);if(controller!=0&&controller!=control)throw new IOException("Player collision controller changed.");
        var location=actor==0?session.ReadLocation("Flight return point"):Anchor??throw new IOException("Flight anchor is unavailable.");if(actor!=0&&location.Block!=unchecked((uint)memory.Int32(state.Actor+0x3F8)))throw new IOException("Map changed. Disable flight before continuing.");
        if(actor!=0 && (memory.Read(component+0x200,1)[0]&0x40)!=0 &&
            (noclip ? ownsCollision && (memory.Read(controller+0x114,1)[0]&2)!=0 : !ownsCollision))return;
        memory.Suspended(()=>
        {
            var current=session.ReadPlayer();if(current.Health==0||current.Actor!=state.Actor||current.Component!=state.Component||unchecked((uint)memory.Int32(current.Actor+8))!=observedHandle||unchecked((uint)memory.Int32(current.Actor+0x3F8))!=location.Block||memory.Pointer(state.Actor+0x58)!=control)throw new IOException("Player changed before flight update.");
            if(actor==0){actor=state.Actor;component=state.Component;controller=control;handle=unchecked((uint)memory.Int32(actor+8));gravityBit=(byte)(memory.Read(component+0x200,1)[0]&0x40);Anchor=location;anchorActor=actor;anchorComponent=component;anchorHandle=handle;}
            var value=memory.Read(component+0x200,1)[0];memory.Write(component+0x200,[(byte)(value|0x40)]);
            if(noclip){value=memory.Read(controller+0x114,1)[0];if(!ownsCollision){collisionBit=(byte)(value&2);ownsCollision=true;}memory.Write(controller+0x114,[(byte)(value|2)]);}
            else RestoreCollision();
        });
    }
    public SavedLocation ReadReturnPoint()
    {
        var player=session.ReadPlayer();
        if(Anchor==null||player.Actor!=anchorActor||player.Component!=anchorComponent||unchecked((uint)memory.Int32(player.Actor+8))!=anchorHandle||unchecked((uint)memory.Int32(player.Actor+0x3F8))!=Anchor.Block)
            throw new IOException("The flight return point belongs to another character or area. Enable flight again to capture a new start point.");
        return Anchor;
    }
    private void RestoreCollision(){if(!ownsCollision)return;var value=memory.Read(controller+0x114,1)[0];memory.Write(controller+0x114,[(byte)((value&~2)|collisionBit)]);ownsCollision=false;}
    public void Restore()
    {
        if(actor==0)return;
        memory.Suspended(()=>{var current=session.ReadPlayer();if(current.Actor==actor&&current.Component==component&&unchecked((uint)memory.Int32(actor+8))==handle){var value=memory.Read(component+0x200,1)[0];memory.Write(component+0x200,[(byte)((value&~0x40)|gravityBit)]);if(memory.Pointer(actor+0x58)==controller)RestoreCollision();}actor=component=controller=0;handle=0;ownsCollision=false;});
    }
    public SavedLocation Step(float forward,float right,float up,float distance)
    {
        if(!float.IsFinite(distance)||distance is <0 or >20||new[]{forward,right,up}.Any(v=>!float.IsFinite(v)||Math.Abs(v)>1))throw new ArgumentException("Invalid flight movement input.");
        if(actor==0)throw new IOException("Enable flight first.");var current=session.ReadLocation("Flight step");var player=session.ReadPlayer();if(current.Block!=Anchor?.Block||player.Actor!=actor||player.Component!=component||unchecked((uint)memory.Int32(actor+8))!=handle||memory.Pointer(actor+0x58)!=controller)throw new IOException("Flight context changed.");
        var length=MathF.Sqrt(forward*forward+right*right+up*up);if(length>1){forward/=length;right/=length;up/=length;}
        var yaw=current.Yaw;var next=current with{X=current.X+(-MathF.Sin(yaw)*forward+MathF.Cos(yaw)*right)*distance,Y=current.Y+up*distance,Z=current.Z+(-MathF.Cos(yaw)*forward-MathF.Sin(yaw)*right)*distance};next.Validate();return next;
    }
}
