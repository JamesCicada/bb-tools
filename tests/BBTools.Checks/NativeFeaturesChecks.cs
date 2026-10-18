using System.IO;
using System.Runtime.InteropServices;
using BBTools.Memory;
internal static partial class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate float FloatGetter(nint component,nint other);
    private static void NativeFeatureChecks()
    {
        var allocation=VirtualAlloc(0,0x6000000,0x3000,4);if(allocation==0)throw new Exception("Feature fixture allocation failed.");
        var root=(ulong)allocation;void Bytes(ulong at,byte[] value)=>Marshal.Copy(value,0,(nint)at,value.Length);
        void Int(ulong at,int value)=>Bytes(at,BitConverter.GetBytes(value));void Float(ulong at,float value)=>Bytes(at,BitConverter.GetBytes(value));void Ptr(ulong at,ulong value)=>Bytes(at,BitConverter.GetBytes(value));
        try
        {
            foreach(var f in PlayerSession.Fingerprints)Bytes(root+f.Offset,f.Bytes);
            var world=root+0x5700000;var actor=root+0x5710000;var modules=root+0x5720000;var stats=root+0x5730000;var motion=root+0x5740000;var argument=root+0x5750000;
            Ptr(root+PlayerSession.WorldPointer,world);Ptr(world+0x60,actor);Ptr(actor+0x3B0,modules);Ptr(modules+0x20,stats);Ptr(stats+8,actor);Int(stats+0xF8,300);Int(stats+0xFC,600);Int(stats+0x134,50);Int(stats+0x138,100);
            Ptr(motion+8,actor);Float(motion+0x374,3);Float(argument+8,1);
            Bytes(root+MovementSpeed.Offset,MovementSpeed.Original.Concat(new byte[]{0xC3}).ToArray());
            var wrapper=root+0x5760000;var code=Convert.FromHexString("5341554883EC084989CD4889D3B80000803F660F6EC848B8").Concat(BitConverter.GetBytes(root+MovementSpeed.Offset)).Concat(Convert.FromHexString("FFD04883C408415D5BC3")).ToArray();Bytes(wrapper,code);
            VirtualProtect((nint)(root+(MovementSpeed.Offset&~4095UL)),4096,0x20,out _);VirtualProtect((nint)wrapper,4096,0x20,out _);
            using var memory=new ProcessMemory(Environment.ProcessId);var session=new PlayerSession(memory,root);var movement=new MovementSpeed(memory,session);var get=Marshal.GetDelegateForFunctionPointer<FloatGetter>((nint)wrapper);
            Check(get((nint)motion,(nint)argument)==3,"unpatched movement fixture returns native movement result");
            var factor=memory.AllocateData(8);memory.Write(factor,BitConverter.GetBytes(2f));var trampoline=memory.AllocateCode(MovementSpeed.Build(root,factor));
            var patch=Convert.FromHexString("FF2500000000").Concat(BitConverter.GetBytes(trampoline)).ToArray();memory.ReplaceMovementCode(root,MovementSpeed.Original,patch);Check(get((nint)motion,(nint)argument)==6,"movement trampoline scales only the transient local player result");
            memory.Write(factor,BitConverter.GetBytes(2f));Check(get((nint)motion,(nint)argument)==6,"reapplying movement does not compound the multiplier");
            Ptr(motion+8,actor+0x1000);Check(get((nint)motion,(nint)argument)==3,"movement hook preserves foreign actor speed");Ptr(motion+8,actor);
            memory.ReplaceMovementCode(root,patch,MovementSpeed.Original);Check(get((nint)motion,(nint)argument)==3&&memory.Read(root+MovementSpeed.Offset,14).SequenceEqual(MovementSpeed.Original),"movement reset restores original executable bytes");
            Reject<ArgumentException>(()=>movement.Apply(float.NaN),"invalid movement scale rejected before patching");
            // Execute native poison filtering with a mock native unlink routine.
            var container=root+0x57A0000;var payload=root+0x57B0000;var node=root+0x57C0000;var row=root+0x57D0000;var helper=root+0x57E0000;var bridge=root+0x57F0000;
            Ptr(actor+0x1C8,container);Ptr(container+0x18,actor);Ptr(container+8,node);Ptr(payload,actor);Ptr(payload+8,container);
            for(var i=0;i<4;i++){var n=node+(ulong)i*0x100;Ptr(n+0x48,row+(ulong)i*0x200);Ptr(n+0x58,i==3?0:n+0x100);Ptr(n+0x60,i==0?0:n-0x100);Bytes(row+(ulong)i*0x200+0x156,BitConverter.GetBytes((ushort)new[]{1,2,5,3}[i]));}
            // RAX=next, RCX=previous. Preserve unrelated effect nodes on either side.
            Bytes(root+0x17E1D90,Convert.FromHexString("488B4658488B4E604885C9740648894158EB04488947084885C0740448894860C3"));VirtualProtect((nint)(root+0x17E1000),4096,0x20,out _);
            Bytes(helper,GameActions.BuildPoisonHelper(root));Bytes(bridge,Convert.FromHexString("57564883EC284889CF48B8").Concat(BitConverter.GetBytes(helper)).Concat(Convert.FromHexString("FFD04883C4285E5FC3")).ToArray());VirtualProtect((nint)helper,4096,0x20,out _);VirtualProtect((nint)bridge,4096,0x20,out _);
            Marshal.GetDelegateForFunctionPointer<Getter>((nint)bridge)((nint)payload);
            Check(memory.Int32(payload+16)==2&&memory.Pointer(container+8)==node&&memory.Pointer(node+0x58)==node+0x300,"native poison cure removes slow/rapid poison and preserves unrelated effects");
            // Read-only nearby inspection resolves modules and checks selection lifetime.
            var bank=root+0x5850000;var list=root+0x5860000;var enemy=root+0x5870000;var enemyModules=root+0x5880000;var enemyStats=root+0x5890000;var gauges=root+0x58A0000;var effects=root+0x58B0000;
            Ptr(root+0x553B098,bank);Ptr(bank+8,list);Ptr(bank+16,list+8);Ptr(list,enemy);Int(enemy+8,123);Int(enemy+0x78,5);Int(enemy+0x88,6);Ptr(enemy+0x3B0,enemyModules);Ptr(enemyModules+0x20,enemyStats);Ptr(enemyStats+8,enemy);Int(enemyStats+0xF8,70);Int(enemyStats+0xFC,100);Ptr(enemyModules+0x28,gauges);Ptr(gauges+8,enemy);Ptr(enemy+0x1C8,effects);Ptr(effects+0x18,enemy);
            var selected=session.ReadNearbyCharacters().Single();var inspection=session.InspectCharacter(selected);Check(inspection.Health==70&&inspection.Maximum==100,"nearby character inspector reads owned health and resistance modules");Int(enemy+8,124);Reject<IOException>(()=>session.InspectCharacter(selected),"replaced character handles invalidate an inspection selection");
        }
        finally{VirtualFree(allocation,0,0x8000);}
    }
    private static void GravityChecks(ProcessMemory memory,PlayerSession session,ulong root)
    {
        var old=memory.Read(root+0x553D700,8);var physics=memory.AllocateData(0x100);var wrapper=memory.AllocateData(0x100);var havok=memory.AllocateData(0x1000);
        void Ptr(ulong at,ulong value)=>memory.Write(at,BitConverter.GetBytes(value));
        foreach(var f in WorldGravity.Fingerprints)memory.Write(root+f.Offset,f.Bytes);Ptr(root+0x553D700,physics);Ptr(physics+0x28,wrapper);Ptr(wrapper+8,havok);memory.Write(havok+0x5B4,BitConverter.GetBytes(-9.8f));
        try
        {
            var gravity=new WorldGravity(memory,session);gravity.Apply(.5f);Check(Math.Abs(BitConverter.ToSingle(memory.Read(havok+0x5B4,4))+4.9f)<.001,"gravity scales the owned world vector");gravity.Apply(.5f);gravity.Restore();Check(BitConverter.ToSingle(memory.Read(havok+0x5B4,4))==-9.8f,"gravity changes do not compound and reset restores its captured value");
            gravity.Apply(.5f);memory.Write(havok+0x5B4,BitConverter.GetBytes(-3f));Reject<IOException>(gravity.Restore,"gravity restoration preserves external edits");
        }
        finally{memory.Write(root+0x553D700,old);}
    }

    private static void ExplorationChecks(ProcessMemory memory,PlayerSession session,ulong root)
    {
        var player=session.ReadPlayer();var oldController=memory.Read(player.Actor+0x58,8);var oldFlags=memory.Read(player.Component+0x200,1);var oldCamera=memory.Read(root+0x553E860,8);var manager=memory.Pointer(root+0x553B130);var oldOptions=memory.Read(manager+0x48,8);
        void Ptr(ulong at,ulong value)=>memory.Write(at,BitConverter.GetBytes(value));
        var ctrl=memory.AllocateData(0x200);var camRoot=memory.AllocateData(0x3000);var camOwner=memory.AllocateData(0x1000);var cam=memory.AllocateData(0x400);var options=memory.AllocateData(0x30);
        try
        {
            foreach(var f in ExplorationFlight.Fingerprints.Concat(HudVisibility.Fingerprints))memory.Write(root+f.Offset,f.Bytes);
            Ptr(player.Actor+0x58,ctrl);memory.Write(ctrl+0x114,[0x10]);var flight=new ExplorationFlight(memory,session);flight.Apply(true);
            Check((memory.Read(player.Component+0x200,1)[0]&0x40)!=0&&memory.Read(ctrl+0x114,1)[0]==0x12,"flight sets owned no-gravity and collision bits without changing unrelated flags");
            var start=flight.Anchor!;var next=flight.Step(1,0,1,2);Check(next.Y>start.Y&&next.Block==start.Block,"flight step stays in the loaded map and normalizes diagonal movement");
            flight.Apply(false);Check(memory.Read(ctrl+0x114,1)[0]==0x10,"disabling noclip restores only its collision bit");flight.Restore();Check(memory.Read(player.Component+0x200,1).SequenceEqual(oldFlags),"flight disable restores the original gravity flag");
            var identity=new DungeonBookmark(new string('a',32),session.ReadCharacter().Address,1,0x1D010000,new string('b',64));var spot=start with{Block=0x1D000000,Dungeon=identity};
            session.ValidateLocationContext(spot,spot);Reject<IOException>(()=>session.ValidateLocationContext(spot,spot with{Dungeon=identity with{StateHash=new string('c',64)}}),"chalice coordinate restore rejects changed instance/progress identity");Reject<IOException>(()=>session.ValidateLocationContext(spot,spot with{Dungeon=identity with{Session=new string('c',32)}}),"chalice coordinate restore rejects a different connection");
            Ptr(root+0x553E860,camRoot);Ptr(camRoot+0x2830,camOwner);Ptr(camOwner+0x60,cam);memory.SetInt32(camOwner+0x84,3);memory.Write(root+FreeCamera.Offset,FreeCamera.Prefix);
            memory.Write(cam+0x10,BitConverter.GetBytes(1f));memory.Write(cam+0x24,BitConverter.GetBytes(1f));memory.Write(cam+0x38,BitConverter.GetBytes(1f));memory.Write(cam+0x50,BitConverter.GetBytes(.75f));memory.Write(cam+0x54,BitConverter.GetBytes(1.777f));
            var camera=new FreeCamera(memory,session);camera.Enable();Check(memory.Read(root+FreeCamera.Offset,1)[0]==0xC3,"free camera freezes only the verified native follow update");camera.Step(1,0,0,0,0,2,90);
            Check(BitConverter.ToSingle(memory.Read(cam+0x48,4))==2&&Math.Abs(BitConverter.ToSingle(memory.Read(cam+0x50,4))-MathF.PI/2)<.001,"free camera writes finite translation and vertical FOV to its owned camera frame");camera.Restore();Check(memory.Read(root+FreeCamera.Offset,FreeCamera.Prefix.Length).SequenceEqual(FreeCamera.Prefix)&&memory.Read(camOwner+0x80,1)[0]==1,"free camera disable restores native instructions and requests follow reset");
            Ptr(manager+0x48,options);memory.Write(options+9,[1]);var hud=new HudVisibility(memory,session);hud.Apply();Check(memory.Read(options+9,1)[0]==0,"HUD override uses the native front-end visibility option");hud.Restore();Check(memory.Read(options+9,1)[0]==1,"HUD reset restores captured visibility");
        }
        finally{memory.Write(player.Actor+0x58,oldController);memory.Write(player.Component+0x200,oldFlags);memory.Write(root+0x553E860,oldCamera);memory.Write(manager+0x48,oldOptions);}
    }

}
