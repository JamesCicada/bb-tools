using System.IO;
using System.Numerics;

namespace BBTools.Memory;

// Native debug-camera output; movement locking shares the tool's player movement hook.
public sealed class FreeCamera(ProcessMemory memory, PlayerSession session,MovementSpeed? movement=null)
{
    private readonly MovementSpeed movementGuard=movement??new(memory,session);
    private readonly PhotoCameraOutput output=new(memory,session);
    internal ulong FrameAddress=>frame;
    public static readonly (ulong Offset, byte[] Bytes)[] Fingerprints =
    [
        (0x193764B, Convert.FromHexString("49899C2410010000")),
        (0x19379F0, Convert.FromHexString("4989842428010000488D0D616EC003488B0948898130280000")),
        (0x19295D9, Convert.FromHexString("498985C83A0100")),
        (0x1929726, Convert.FromHexString("498B95C83A0100")),
        (0x156FEE5, Convert.FromHexString("488D05E468DB03")),
        (PhotoCameraOutput.Offset, PhotoCameraOutput.Original),
        (0x15729B6, Convert.FromHexString("4183BC24C4000000010F85D00000004983BC24D0000000000F84C10000004183BC24C8000000000F85B2000000")),
        (0x193BEE1, Convert.FromHexString("C5FA104050C4C17A114750C5FA104054C4C17A114754")),
        (0x132F786, Convert.FromHexString("488B4058889811010000")),
        (0x1337878, Convert.FromHexString("410FB6CE8B90E10100000FB6B0E5010000")),
        (0x132E4F4, Convert.FromHexString("8A881001000080E1FE4408F1888810010000")),
        (0x18D5570, Convert.FromHexString("488B87B0030000488B40200FB78000020000"))
    ];
    private ulong root, control, follow, frame, actor, cameraStep, resourceModule;
    private uint block, handle;
    private byte[]? initial;
    private ulong previousFrame;
    private int previousMode, previousHold;
    private bool active, hunterLocked;
    private byte hunterBit, originalMoveGate, originalLogicBit, originalGravityBit;
    private ulong hunterController;
    public bool IsActive => active;
    public bool RestorationPending => active || hunterLocked || movementGuard.PhotoLocked || output.RestorationPending;
    public float InitialFov => initial == null ? 43 : BitConverter.ToSingle(initial, 0x50) * 180 / MathF.PI;

    internal static bool MatchesCameraStep(IMemory source,ulong imageBase,ulong step,ulong owner)
    {
        try
        {
            if(source.Pointer(step,"camera task vtable")!=imageBase+0x5330480||source.Pointer(step+0x128,"camera task output")!=owner)return false;
            var debug=source.Pointer(step+0x110,"debug-camera controller");
            return source.Pointer(debug,"debug-camera vtable")==imageBase+0x53267D0;
        }
        catch(IOException){return false;}
    }
    public Task PrepareAsync(CancellationToken cancellationToken=default)=>Task.Run(()=>
    {
        ValidateInstructions();session.ReadPlayer();
        var at=memory.Pointer(session.ImageBase+0x553E860,"lock-on manager");var owner=memory.Pointer(at+0x2830,"active camera output");
        if(cameraStep==0||!MatchesCameraStep(memory,session.ImageBase,cameraStep,owner))cameraStep=memory.FindActiveCameraStep(owner,session.ImageBase,cancellationToken);
        ValidateFrame(memory.Read(owner,0x60));
    },cancellationToken);
    private (ulong Root, ulong Control, ulong Follow) Resolve(bool restoring = false)
    {
        var at=memory.Pointer(session.ImageBase+0x553E860,"lock-on manager");
        var owner=BitConverter.ToUInt64(memory.Read(at+0x2830,8));
        if(restoring&&control!=0)
        {
            if(memory.Pointer(control,"captured debug-camera vtable")!=session.ImageBase+0x53267D0)throw new IOException("Captured native camera controller no longer validates.");
            return(at,control,owner);
        }
        if(owner<0x10000||owner%8!=0)throw new IOException("Active camera output is unavailable; wait until the view has loaded.");
        if(cameraStep==0)cameraStep=memory.FindActiveCameraStep(owner,session.ImageBase);
        else if(!MatchesCameraStep(memory,session.ImageBase,cameraStep,owner))throw new IOException("The native camera changed during loading. Enable photo mode again once the view settles.");
        return(at,memory.Pointer(cameraStep+0x110,"native debug-camera controller"),owner);
    }
    private void ValidateInstructions()
    {
        session.ValidateImage();
        foreach (var f in Fingerprints)
            if (!memory.Read(session.ImageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes))
                throw new IOException("Photo-mode instructions differ from the supported 1.09 build.");
    }
    public void Enable()
    {
        if (active) return;
        if (hunterLocked || output.RestorationPending) Restore();
        if (hunterLocked) throw new IOException("Wait for the previous hunter movement lock to restore before re-entering photo mode.");
        ValidateInstructions();
        var player = session.ReadPlayer();
        if (player.Health == 0) throw new IOException("Load a living character before photo mode.");
        var current = Resolve();
        var mode = memory.Int32(current.Control + 0xC4);
        var hold = memory.Int32(current.Control + 0xC8);
        if (mode != 0 || hold is not (0 or 1)) throw new IOException("Another debug camera is active. Disable it before photo mode.");
        var source = memory.Read(current.Follow, 0x60);
        ValidateFrame(source);
        var oldFrame = BitConverter.ToUInt64(memory.Read(current.Control + 0xD0, 8));
        frame = frame == 0 ? memory.AllocateData(0x60) : frame;
        memory.Write(frame, source);
        root = current.Root; control = current.Control; follow = current.Follow;
        actor = player.Actor; resourceModule = player.Component; handle = unchecked((uint)memory.Int32(actor + 8));
        block = unchecked((uint)memory.Int32(actor + 0x3F8));
        previousMode = mode; previousHold = hold; previousFrame = oldFrame; initial = source;
        memory.Suspended(() =>
        {
            var checkedPlayer=session.ReadPlayer();
            if (Resolve() != current || checkedPlayer.Actor != actor || checkedPlayer.Component != resourceModule || unchecked((uint)memory.Int32(actor+8))!=handle || unchecked((uint)memory.Int32(actor+0x3F8))!=block || memory.Int32(control + 0xC4) != mode ||
                memory.Int32(control + 0xC8) != hold || BitConverter.ToUInt64(memory.Read(control + 0xD0,8)) != oldFrame)
                throw new IOException("Camera changed before photo-mode activation.");
            // Borrow only for the renderer; native ownership/mode fields stay intact.
            output.Apply(frame,control,actor,follow,handle);
            active = true;
        });
    }
    public void ValidateContext()
    {
        if (!active) throw new IOException("Enable photo mode first.");
        var player = session.ReadPlayer();
        if (player.Health == 0 || player.Actor != actor || player.Component != resourceModule || unchecked((uint)memory.Int32(actor + 8)) != handle ||
            unchecked((uint)memory.Int32(actor + 0x3F8)) != block || Resolve() != (root,control,follow))
            throw new IOException("Photo mode stopped after a player, map or camera change.");
        if (!output.Matches() || memory.Int32(control + 0xC4) != previousMode || memory.Int32(control + 0xC8) != previousHold || BitConverter.ToUInt64(memory.Read(control + 0xD0,8)) != previousFrame)
            throw new IOException("Another tool or the game changed the debug camera.");
    }
    public void LockHunter(bool enabled)
    {
        ValidateContext();
        if(!enabled&&!hunterLocked&&!movementGuard.PhotoLocked)return;
        movementGuard.SetPhotoLock(enabled?actor:0);
        var currentController=memory.Pointer(actor+0x58,"hunter movement controller");
        if(hunterLocked&&currentController!=hunterController)throw new IOException("Hunter movement controller changed; photo mode must stop.");
        var value=memory.Read(actor+0x1E3,1)[0];var gate=memory.Read(currentController+0x111,1)[0];
        var logic=memory.Read(currentController+0x110,1)[0];
        if(gate>1)throw new IOException("Hunter movement gate differs from the supported layout.");
        if(enabled&&hunterLocked&&(value&0x80)!=0&&gate==1&&(logic&1)==0&&(memory.Read(resourceModule+0x200,1)[0]&0x40)!=0)return;
        memory.Suspended(() =>
        {
            ValidateContext();
            if(memory.Pointer(actor+0x58,"hunter movement controller")!=currentController)throw new IOException("Hunter movement controller changed before locking.");
            value=memory.Read(actor+0x1E3,1)[0];gate=memory.Read(currentController+0x111,1)[0];
            if(gate>1)throw new IOException("Hunter movement gate failed validation.");
            if(enabled)
            {
                if(!hunterLocked){hunterBit=(byte)(value&0x80);originalMoveGate=gate;originalLogicBit=(byte)(memory.Read(currentController+0x110,1)[0]&1);originalGravityBit=(byte)(memory.Read(resourceModule+0x200,1)[0]&0x40);hunterController=currentController;hunterLocked=true;}
                if((value&0x80)==0)memory.Write(actor+0x1E3,[(byte)(value|0x80)]);
                if(gate!=1)memory.Write(currentController+0x111,[1]);
                // The native EnableLogic gate also stops the hunter's action/root
                // motion update. A movement multiplier alone leaves actions active.
                logic=memory.Read(currentController+0x110,1)[0];
                if((logic&1)!=0)memory.Write(currentController+0x110,[(byte)(logic&~1)]);
                var gravity=memory.Read(resourceModule+0x200,1)[0];
                if((gravity&0x40)==0)memory.Write(resourceModule+0x200,[(byte)(gravity|0x40)]);
            }
            else RestoreHunterLock();
        });
    }
    private void RestoreHunterLock()
    {
        // Release the per-frame lock even while the player/controller is unavailable.
        movementGuard.SetPhotoLock(0);
        if(!hunterLocked)return;
        var player=session.ReadPlayer();
        if(player.Actor==actor&&unchecked((uint)memory.Int32(actor+8))==handle)
        {
            // Restore only the controller still belonging to the captured actor.
            var current=memory.Pointer(actor+0x58,"hunter movement controller");
            if(current==hunterController)
            {
                var gate=memory.Read(current+0x111,1)[0];
                if(gate>1)throw new IOException("Hunter movement gate changed externally; restoration is pending.");
                memory.Write(current+0x111,[originalMoveGate]);
                var logic=memory.Read(current+0x110,1)[0];
                if((logic&1)==0)memory.Write(current+0x110,[(byte)(logic|originalLogicBit)]);
            }
            var flags=memory.Read(actor+0x1E3,1)[0];memory.Write(actor+0x1E3,[(byte)((flags&0x7F)|hunterBit)]);
            if(player.Component==resourceModule)
            {
                var flags2=memory.Read(resourceModule+0x200,1)[0];
                memory.Write(resourceModule+0x200,[(byte)((flags2&~0x40)|originalGravityBit)]);
            }
        }
        hunterLocked=false;hunterController=0;
    }
    public void Step(float forward, float right, float up, float yaw, float pitch, float distance, float fov, float roll = 0, float angle = .05f)
    {
        ValidateContext();
        if (!float.IsFinite(distance) || distance is <0 or >20 || !float.IsFinite(angle) || angle is <0 or >1 ||
            !float.IsFinite(fov) || fov is <10 or >120 || new[]{forward,right,up,yaw,pitch,roll}.Any(v=>!float.IsFinite(v)||Math.Abs(v)>1))
            throw new ArgumentException("Invalid photo-mode camera input.");
        var bytes = memory.Read(frame,0x60); ValidateFrame(bytes);
        if(forward==0&&right==0&&up==0&&yaw==0&&pitch==0&&roll==0&&BitConverter.ToSingle(bytes,0x50)==fov*MathF.PI/180)return;
        Vector3 Read(int at) => new(BitConverter.ToSingle(bytes,at),BitConverter.ToSingle(bytes,at+4),BitConverter.ToSingle(bytes,at+8));
        var x = Read(0x10); var y = Read(0x20); var z = Read(0x30); var position = Read(0x40);
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw*angle);
        x = Vector3.Transform(x,turn); y = Vector3.Transform(y,turn); z = Vector3.Transform(z,turn);
        turn = Quaternion.CreateFromAxisAngle(Vector3.Normalize(x),pitch*angle);
        y = Vector3.Transform(y,turn); z = Vector3.Transform(z,turn);
        turn = Quaternion.CreateFromAxisAngle(Vector3.Normalize(z),roll*angle);
        x = Vector3.Normalize(Vector3.Transform(x,turn)); y = Vector3.Normalize(Vector3.Transform(y,turn)); z = Vector3.Normalize(z);
        var movement = -z*forward+x*right+Vector3.UnitY*up;
        if (movement.LengthSquared()>1) movement = Vector3.Normalize(movement);
        position += movement*distance;
        void Write(int at,Vector3 value) { BitConverter.GetBytes(value.X).CopyTo(bytes,at);BitConverter.GetBytes(value.Y).CopyTo(bytes,at+4);BitConverter.GetBytes(value.Z).CopyTo(bytes,at+8); }
        Write(0x10,x); Write(0x20,y); Write(0x30,z); Write(0x40,position);
        BitConverter.GetBytes(fov*MathF.PI/180).CopyTo(bytes,0x50); ValidateFrame(bytes);
        memory.Suspended(()=>{ValidateContext();memory.Write(frame+0x10,bytes[0x10..]);});
    }
    public void Reset()
    {
        ValidateContext();
        memory.Suspended(()=>{ValidateContext();memory.Write(frame+0x10,initial![0x10..]);});
    }
    private static void ValidateFrame(byte[] data)
    {
        for (var i=0x10;i<0x60;i+=4)
            if (!float.IsFinite(BitConverter.ToSingle(data,i))) throw new IOException("Camera frame contains non-finite values.");
        Vector3 Read(int at)=>new(BitConverter.ToSingle(data,at),BitConverter.ToSingle(data,at+4),BitConverter.ToSingle(data,at+8));
        var x=Read(0x10);var y=Read(0x20);var z=Read(0x30);var p=Read(0x40);
        if (Math.Abs(x.Length()-1)>.1f||Math.Abs(y.Length()-1)>.1f||Math.Abs(z.Length()-1)>.1f||
            Math.Abs(Vector3.Dot(x,y))>.1f||Math.Abs(Vector3.Dot(x,z))>.1f||Math.Abs(Vector3.Dot(y,z))>.1f||
            Math.Abs(p.X)>100000||Math.Abs(p.Y)>100000||Math.Abs(p.Z)>100000||
            BitConverter.ToSingle(data,0x50) is <.01f or >3.14f||BitConverter.ToSingle(data,0x54)<=0)
            throw new IOException("Camera frame failed basis, position or projection validation.");
    }
    public void Restore()
    {
        if (!RestorationPending) return;
        memory.Suspended(() =>
        {
            try { output.Restore(); active=false; }
            finally
            {
                if(hunterLocked||movementGuard.PhotoLocked)
                    try { RestoreHunterLock(); } catch(IOException) { /* Retry only owned hunter cleanup after loading. */ }
            }
        });
        // Trampoline and frame pages stay allocated until process exit for readers
        // already in flight. The game never owns or frees the private frame.
    }
}
