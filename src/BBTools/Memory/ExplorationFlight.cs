using System.IO;
namespace BBTools.Memory;
public sealed class ExplorationFlight(ProcessMemory memory,PlayerSession session)
{
    public static readonly (ulong Offset,byte[] Bytes)[] Fingerprints=
    [(0x18D5570,Convert.FromHexString("488B87B0030000488B40200FB78000020000")),(0xD35C63,Convert.FromHexString("F6871401000002"))];
    private ulong actor,component,controller;private byte gravityBit,collisionBit;private bool ownsCollision;
    public bool RestorationPending => actor != 0;
    public SavedLocation? Anchor{get;private set;}
    public void Apply(bool noclip)
    {
        session.ValidateImage();foreach(var f in Fingerprints)if(!memory.Read(session.ImageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes))throw new IOException("Exploration instructions differ from the supported build.");
        var state=session.ReadPlayer();if(state.Health==0)throw new IOException("Flight requires a living character.");
        if(actor!=0&&(actor!=state.Actor||component!=state.Component))throw new IOException("Player changed. Disable flight and enable it again after loading.");
        var control=memory.Pointer(state.Actor+0x58);if(controller!=0&&controller!=control)throw new IOException("Player collision controller changed.");
        var location=Anchor??session.ReadLocation("Flight return point");if(Anchor!=null&&Anchor.Block!=unchecked((uint)memory.Int32(state.Actor+0x3F8)))throw new IOException("Map changed. Disable flight before continuing.");
        if(actor!=0 && (memory.Read(component+0x200,1)[0]&0x40)!=0 &&
            (noclip ? ownsCollision && (memory.Read(controller+0x114,1)[0]&2)!=0 : !ownsCollision))return;
        memory.Suspended(()=>
        {
            var current=session.ReadPlayer();if(current.Actor!=state.Actor||current.Component!=state.Component||memory.Pointer(state.Actor+0x58)!=control)throw new IOException("Player changed before flight update.");
            if(actor==0){actor=state.Actor;component=state.Component;controller=control;gravityBit=(byte)(memory.Read(component+0x200,1)[0]&0x40);Anchor=location;}
            var value=memory.Read(component+0x200,1)[0];memory.Write(component+0x200,[(byte)(value|0x40)]);
            if(noclip){value=memory.Read(controller+0x114,1)[0];if(!ownsCollision){collisionBit=(byte)(value&2);ownsCollision=true;}memory.Write(controller+0x114,[(byte)(value|2)]);}
            else RestoreCollision();
        });
    }
    private void RestoreCollision(){if(!ownsCollision)return;var value=memory.Read(controller+0x114,1)[0];memory.Write(controller+0x114,[(byte)((value&~2)|collisionBit)]);ownsCollision=false;}
    public void Restore()
    {
        if(actor==0)return;
        memory.Suspended(()=>{var current=session.ReadPlayer();if(current.Actor==actor&&current.Component==component){var value=memory.Read(component+0x200,1)[0];memory.Write(component+0x200,[(byte)((value&~0x40)|gravityBit)]);if(memory.Pointer(actor+0x58)==controller)RestoreCollision();}actor=component=controller=0;ownsCollision=false;});
    }
    public SavedLocation Step(float forward,float right,float up,float distance)
    {
        if(!float.IsFinite(distance)||distance is <0 or >20||new[]{forward,right,up}.Any(v=>!float.IsFinite(v)||Math.Abs(v)>1))throw new ArgumentException("Invalid flight movement input.");
        if(actor==0)throw new IOException("Enable flight first.");var current=session.ReadLocation("Flight step");if(current.Block!=Anchor?.Block||session.ReadPlayer().Actor!=actor)throw new IOException("Flight context changed.");
        var length=MathF.Sqrt(forward*forward+right*right+up*up);if(length>1){forward/=length;right/=length;up/=length;}
        var yaw=current.Yaw;var next=current with{X=current.X+(-MathF.Sin(yaw)*forward+MathF.Cos(yaw)*right)*distance,Y=current.Y+up*distance,Z=current.Z+(-MathF.Cos(yaw)*forward-MathF.Sin(yaw)*right)*distance};next.Validate();return next;
    }
}
