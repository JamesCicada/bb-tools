using System.IO;
using System.Numerics;
namespace BBTools.Memory;
public sealed class FreeCamera(ProcessMemory memory,PlayerSession session)
{
    public const ulong Offset=0x143AC60;
    public static readonly byte[] Prefix=Convert.FromHexString("554889E54157415641554154534881EC");
    private ulong camera,owner,actor;private uint block;private byte[]? original;private bool patched;
    private (ulong Owner,ulong Camera) Resolve()
    {
        var root=memory.Pointer(session.ImageBase+0x553E860);var control=memory.Pointer(root+0x2830);
        if(memory.Int32(control+0x84)!=3)throw new IOException("Free camera requires the extended follow camera (mode 3).");return(control,memory.Pointer(control+0x60));
    }
    public void Enable()
    {
        if(patched)return;session.ValidateImage();var player=session.ReadPlayer();if(player.Health==0)throw new IOException("Load a living character before free camera.");var current=Resolve();
        var values=memory.Read(current.Camera+0x10,0x48);for(var i=0;i<values.Length;i+=4)if(!float.IsFinite(BitConverter.ToSingle(values,i)))throw new IOException("Camera frame failed validation.");
        var fov=BitConverter.ToSingle(values,0x40);if(fov is <.01f or >3.14f)throw new IOException("Camera FOV failed validation.");
        memory.Suspended(()=>{if(Resolve()!=current||session.ReadPlayer().Actor!=player.Actor)throw new IOException("Camera owner changed.");memory.FreezeFollowCamera(session.ImageBase,true);});
        camera=current.Camera;owner=current.Owner;actor=player.Actor;block=unchecked((uint)memory.Int32(actor+0x3F8));original=values;patched=true;
    }
    public void ValidateContext()
    {
        if(!patched)throw new IOException("Enable free camera first.");session.ValidateImage();var current=session.ReadPlayer();if(current.Actor!=actor||unchecked((uint)memory.Int32(actor+0x3F8))!=block||Resolve()!=(owner,camera))throw new IOException("Free camera context changed; disable before continuing.");
        var expected=Prefix.ToArray();expected[0]=0xC3;if(!memory.Read(session.ImageBase+Offset,expected.Length).SequenceEqual(expected))throw new IOException("Free camera instructions changed externally.");
    }
    public void Step(float forward,float right,float up,float yaw,float pitch,float distance,float fov)
    {
        ValidateContext();if(!float.IsFinite(distance)||distance is <0 or >20||!float.IsFinite(fov)||fov is <38 or >120||new[]{forward,right,up,yaw,pitch}.Any(v=>!float.IsFinite(v)||Math.Abs(v)>1))throw new ArgumentException("Invalid free-camera step.");
        var bytes=memory.Read(camera+0x10,0x48);Vector3 Read(int at)=>new(BitConverter.ToSingle(bytes,at),BitConverter.ToSingle(bytes,at+4),BitConverter.ToSingle(bytes,at+8));
        var x=Read(0);var y=Read(16);var z=Read(32);var position=Read(48);
        if(Math.Abs(x.Length()-1)>.1f||Math.Abs(y.Length()-1)>.1f||Math.Abs(z.Length()-1)>.1f)throw new IOException("Free-camera basis failed validation.");
        var turn=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw*.05f);x=Vector3.Transform(x,turn);y=Vector3.Transform(y,turn);z=Vector3.Transform(z,turn);
        turn=Quaternion.CreateFromAxisAngle(Vector3.Normalize(x),pitch*.05f);y=Vector3.Transform(y,turn);z=Vector3.Transform(z,turn);
        var move=z*forward+x*right+Vector3.UnitY*up;if(move.Length()>1)move=Vector3.Normalize(move);position+=move*distance;
        if(new[]{position.X,position.Y,position.Z}.Any(v=>!float.IsFinite(v)||Math.Abs(v)>100000))throw new IOException("Free-camera position is out of range.");
        void Write(int at,Vector3 v){BitConverter.GetBytes(v.X).CopyTo(bytes,at);BitConverter.GetBytes(v.Y).CopyTo(bytes,at+4);BitConverter.GetBytes(v.Z).CopyTo(bytes,at+8);}
        Write(0,x);Write(16,y);Write(32,z);Write(48,position);BitConverter.GetBytes(fov*MathF.PI/180).CopyTo(bytes,0x40);
        memory.Suspended(()=>{ValidateContext();memory.Write(camera+0x10,bytes);});
    }
    public void Restore()
    {
        if(!patched)return;
        memory.Suspended(()=>
        {
            memory.FreezeFollowCamera(session.ImageBase,false);patched=false;
            // Resume the native update even if the old camera has unloaded.
            try{if(Resolve()==(owner,camera)&&original!=null){memory.Write(camera+0x10,original);memory.Write(owner+0x80,[1]);}}catch(IOException){/* Old owner is unavailable; native update remains restored. */}
        });
    }
}
