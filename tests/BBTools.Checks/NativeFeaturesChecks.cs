using System.IO;
using System.Runtime.InteropServices;
using BBTools.Memory;
internal static partial class Program
{
    private sealed class CountingWritesMemory(IMemory source):IMemory
    {
        public int Writes{get;private set;}
        public byte[] Read(ulong address,int size)=>source.Read(address,size);
        public void Write(ulong address,byte[] value){source.Write(address,value);Writes++;}
    }
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
            memory.Write(factor+8,BitConverter.GetBytes(actor));memory.Write(factor,BitConverter.GetBytes(0f));
            Check(get((nint)motion,(nint)argument)==0,"photo movement hook suppresses the native hunter result on every execution");
            Ptr(world+0x60,actor+0x1000);Ptr(motion+8,actor+0x1000);
            Check(get((nint)motion,(nint)argument)==3,"photo movement hook cannot freeze a replacement player after loading");
            Ptr(world+0x60,actor);Ptr(motion+8,actor);memory.Write(factor+8,BitConverter.GetBytes(0UL));memory.Write(factor,BitConverter.GetBytes(2f));
            Ptr(motion+8,actor+0x1000);Check(get((nint)motion,(nint)argument)==3,"movement hook preserves foreign actor speed");Ptr(motion+8,actor);
            memory.ReplaceMovementCode(root,patch,MovementSpeed.Original);Check(get((nint)motion,(nint)argument)==3&&memory.Read(root+MovementSpeed.Offset,14).SequenceEqual(MovementSpeed.Original),"movement reset restores original executable bytes");
            Reject<ArgumentException>(()=>movement.Apply(float.NaN),"invalid movement scale rejected before patching");
            foreach(var f in PlayerSession.FeatureFingerprints)Bytes(root+f.Offset,f.Bytes);
            var actionFlags=memory.AllocateData(0x300);Ptr(modules+8,actionFlags);
            var nativeHitGate=memory.AllocateCode(Convert.FromHexString("8B8FE1010000F7C100400004751D488B8FB0030000488B49088B89BC010000F6C101750783E102D1E988C8C3"));
            var hitWrapper=memory.AllocateCode(Convert.FromHexString("574889CFB80100000049BB").Concat(BitConverter.GetBytes(nativeHitGate)).Concat(Convert.FromHexString("41FFD35FC3")).ToArray());
            var hitGate=Marshal.GetDelegateForFunctionPointer<Getter>((nint)hitWrapper);
            Check(hitGate((nint)actor)==0,"native invincibility reader accepts a hunter with no animation or event invincibility");
            session.SetPracticeFlag(PracticeFlag.NoHitReaction,true);
            Check(hitGate((nint)actor)==1&&memory.Read(actionFlags+0x1BC,4).All(x=>x==0),"Ignore hits uses persistent native event invincibility independently of animation flags");
            session.RestorePracticeFlag(PracticeFlag.NoHitReaction);
            Check(hitGate((nint)actor)==0,"disabling Ignore hits restores the original native hit eligibility");
            var counted=new CountingWritesMemory(memory);var combined=new PlayerSession(counted,root);
            memory.Write(stats+0x200,[0x08]);memory.Write(actor+0x1E2,[0x02]);
            combined.SetPracticeFlag(PracticeFlag.GodMode,true);combined.SetPracticeFlag(PracticeFlag.NoHitReaction,true);combined.SetInfiniteStamina(true);
            var initialWrites=counted.Writes;
            for(var i=0;i<30;i++){combined.SetPracticeFlag(PracticeFlag.GodMode,true);combined.SetPracticeFlag(PracticeFlag.NoHitReaction,true);combined.SetInfiniteStamina(true);}
            Check(initialWrites==3&&counted.Writes==initialWrites&&hitGate((nint)actor)==1,"God mode, Ignore hits and stamina together perform no repeated writes while native flags remain set");
            memory.Write(actor+0x1E2,[0x06]);combined.SetPracticeFlag(PracticeFlag.NoHitReaction,true);
            Check(counted.Writes==initialWrites+1&&memory.Read(actor+0x1E2,1)[0]==0x46,"Ignore hits reapplies a cleared flag while preserving new unrelated actor bits");
            combined.RestorePracticeFlag(PracticeFlag.GodMode);
            Check(memory.Read(stats+0x200,1)[0]==0x88&&hitGate((nint)actor)==1,"turning God mode off preserves Ignore hits and infinite stamina");
            combined.RestorePracticeFlag(PracticeFlag.NoHitReaction);combined.RestoreStamina();
            Check(memory.Read(stats+0x200,1)[0]==8&&memory.Read(actor+0x1E2,1)[0]==6&&hitGate((nint)actor)==0,"combined toggle cleanup restores original bits and preserves unrelated engine changes");
            memory.Write(actor+0x1E2,[0x40]);combined.SetPracticeFlag(PracticeFlag.NoHitReaction,true);var beforeRestore=counted.Writes;combined.RestorePracticeFlag(PracticeFlag.NoHitReaction);
            Check(counted.Writes==beforeRestore&&hitGate((nint)actor)==1,"preexisting hit invincibility is owned and restored without unnecessary writes");
            memory.Write(actor+0x1E2,[0]);
            var gameData=memory.AllocateData(0x200);Ptr(root+0x553B130,gameData);
            VirtualProtect((nint)(root+0x1338000),4096,0x20,out _);
            var penaltySkip=Marshal.GetDelegateForFunctionPointer<Getter>((nint)(root+0x13389B0));
            Check((byte)penaltySkip(0)==0,"native IsDeathPenaltySkip initially reports ordinary death penalties");
            session.SetPracticeFlag(PracticeFlag.NoEchoesLoss,true);Check((byte)penaltySkip(0)==1&&memory.Read(gameData+0xA8,1)[0]==1,"Echoes protection activates the native death-penalty gate without balance/refund writes");
            session.RestorePracticeFlag(PracticeFlag.NoEchoesLoss);Check((byte)penaltySkip(0)==0,"turning Echoes protection off restores its original native death-penalty flag");
            memory.Write(gameData+0xA8,[1]);session.SetPracticeFlag(PracticeFlag.NoEchoesLoss,true);session.RestorePracticeFlag(PracticeFlag.NoEchoesLoss);
            Check((byte)penaltySkip(0)==1,"Echoes protection preserves a preexisting death-penalty skip flag");
            memory.Write(gameData+0xA8,[2]);Reject<IOException>(()=>session.SetPracticeFlag(PracticeFlag.NoEchoesLoss,true),"Echoes protection rejects an unsupported native flag value");
            memory.Write(gameData+0xA8,[0]);session.SetPracticeFlag(PracticeFlag.NoEchoesLoss,true);
            var replacementData=memory.AllocateData(0x200);Ptr(root+0x553B130,replacementData);session.RestorePracticeFlag(PracticeFlag.NoEchoesLoss);
            Check(memory.Read(gameData+0xA8,1)[0]==1&&memory.Read(replacementData+0xA8,1)[0]==0,"Echoes cleanup never writes a retired or replacement game-data manager");
            session.SetPracticeFlag(PracticeFlag.NoEchoesLoss,true);session.RestorePracticeFlag(PracticeFlag.NoEchoesLoss);
            Check((byte)penaltySkip(0)==0,"Echoes protection captures and restores a newly loaded manager independently");
            // Execute native poison filtering with a mock native unlink routine.
            var container=root+0x57A0000;var payload=root+0x57B0000;var node=root+0x57C0000;var row=root+0x57D0000;var helper=root+0x57E0000;var bridge=root+0x57F0000;
            Ptr(actor+0x1C8,container);Ptr(container+0x18,actor);Ptr(container+8,node);Ptr(payload,actor);Ptr(payload+8,container);
            for(var i=0;i<4;i++){var n=node+(ulong)i*0x100;Ptr(n+0x48,row+(ulong)i*0x200);Ptr(n+0x58,i==3?0:n+0x100);Ptr(n+0x60,i==0?0:n-0x100);Bytes(row+(ulong)i*0x200+0x156,BitConverter.GetBytes((ushort)new[]{1,2,6,5}[i]));}
            // RAX=next, RCX=previous. Preserve unrelated effect nodes on either side.
            Bytes(root+0x17E1D90,Convert.FromHexString("488B4658488B4E604885C9740648894158EB04488947084885C0740448894860C3"));VirtualProtect((nint)(root+0x17E1000),4096,0x20,out _);
            Bytes(helper,GameActions.BuildPoisonHelper(root));Bytes(bridge,Convert.FromHexString("57564883EC284889CF48B8").Concat(BitConverter.GetBytes(helper)).Concat(Convert.FromHexString("FFD04883C4285E5FC3")).ToArray());VirtualProtect((nint)helper,4096,0x20,out _);VirtualProtect((nint)bridge,4096,0x20,out _);
            Marshal.GetDelegateForFunctionPointer<Getter>((nint)bridge)((nint)payload);
            Check(memory.Int32(payload+16)==2&&memory.Pointer(container+8)==node&&memory.Pointer(node+0x58)==node+0x300,"native poison cure removes slow/rapid poison and preserves unrelated effects");
            // Read-only nearby inspection resolves modules and checks selection lifetime.
            var bank=root+0x5850000;var list=root+0x5860000;var enemy=root+0x5870000;var enemyModules=root+0x5880000;var enemyStats=root+0x5890000;var gauges=root+0x58A0000;var effects=root+0x58B0000;
            Ptr(root+0x553B098,bank);Ptr(bank+8,list);Ptr(bank+16,list+8);Ptr(list,enemy);Int(enemy+8,123);Int(enemy+0x78,5);Int(enemy+0x88,6);Ptr(enemy+0x3B0,enemyModules);Ptr(enemyModules+0x20,enemyStats);Ptr(enemyStats+8,enemy);Int(enemyStats+0xF8,70);Int(enemyStats+0xFC,100);Ptr(enemyModules+0x28,gauges);Ptr(gauges+8,enemy);Ptr(enemy+0x1C8,effects);Ptr(effects+0x18,enemy);
            var selected=session.ReadNearbyCharacters().Single();var inspection=session.InspectCharacter(selected);Check(inspection.Health==70&&inspection.Maximum==100,"nearby character inspector reads owned health and resistance modules");Int(enemy+8,124);Reject<IOException>(()=>session.InspectCharacter(selected),"replaced character handles invalidate an inspection selection");
            TargetInspectionChecks(memory,root);
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
        var ctrl=memory.AllocateData(0x200);var camRoot=memory.AllocateData(0x14000);var camOwner=memory.AllocateData(0x1000);var cam=memory.AllocateData(0x400);var options=memory.AllocateData(0x30);
        try
        {
            foreach(var f in ExplorationFlight.Fingerprints.Concat(HudVisibility.Fingerprints))memory.Write(root+f.Offset,f.Bytes);
            Ptr(player.Actor+0x58,ctrl);memory.Write(ctrl+0x114,[0x10]);var flight=new ExplorationFlight(memory,session);flight.Apply(true);
            Check((memory.Read(player.Component+0x200,1)[0]&0x40)!=0&&memory.Read(ctrl+0x114,1)[0]==0x12,"flight sets owned no-gravity and collision bits without changing unrelated flags");
            var start=flight.Anchor!;var next=flight.Step(1,0,1,2);Check(next.Y>start.Y&&next.Block==start.Block,"flight step stays in the loaded map and normalizes diagonal movement");
            flight.Apply(false);Check(memory.Read(ctrl+0x114,1)[0]==0x10,"disabling noclip restores only its collision bit");flight.Restore();Check(memory.Read(player.Component+0x200,1).SequenceEqual(oldFlags),"flight disable restores the original gravity flag");
            flight.Apply(true);Reject<ArgumentException>(()=>flight.Step(float.NaN,0,0,1),"noclip rejects invalid movement before native relocation");flight.Restore();
            var identity=new DungeonBookmark(new string('a',32),session.ReadCharacter().Address,1,0x1D010000,new string('b',64));var spot=start with{Block=0x1D000000,Dungeon=identity};
            session.ValidateLocationContext(spot,spot);Reject<IOException>(()=>session.ValidateLocationContext(spot,spot with{Dungeon=identity with{StateHash=new string('c',64)}}),"chalice coordinate restore rejects changed instance/progress identity");Reject<IOException>(()=>session.ValidateLocationContext(spot,spot with{Dungeon=identity with{Session=new string('c',32)}}),"chalice coordinate restore rejects a different connection");
            Ptr(root+0x553E860,camRoot);Ptr(camRoot+0x2830,camOwner);Ptr(camRoot+0x13AC8,0);var step=camOwner+0x600;Ptr(step,root+0x5330480);Ptr(step+0x110,cam);Ptr(step+0x128,camOwner);
            Check(!FreeCamera.MatchesCameraStep(memory,root,step,camOwner+8),"camera task resolution rejects an output owner mismatch");
            Ptr(cam,root+0x53267D0);memory.SetInt32(camOwner+0x84,2); // Legacy mode: no extended-camera dependency.
            foreach(var f in FreeCamera.Fingerprints)memory.Write(root+f.Offset,f.Bytes);
            memory.Write(root+MovementSpeed.Offset,MovementSpeed.Original);
            memory.Write(camOwner+0x10,BitConverter.GetBytes(1f));memory.Write(camOwner+0x24,BitConverter.GetBytes(1f));memory.Write(camOwner+0x38,BitConverter.GetBytes(1f));memory.Write(camOwner+0x50,BitConverter.GetBytes(.75f));memory.Write(camOwner+0x54,BitConverter.GetBytes(1.777f));
            var originalCode=memory.Read(root+0x143AC60,32);var hunterFlags=memory.Read(player.Actor+0x1E3,1);
            var photoMovement=new MovementSpeed(memory,session);var camera=new FreeCamera(memory,session,photoMovement);camera.Enable();var frame=memory.Pointer(cam+0xD0);
            Check(memory.Int32(cam+0xC4)==1&&memory.Int32(cam+0xC8)==1&&frame!=camOwner,"photo mode selects an owned native debug frame without an external patch or extended-follow mode");
            camera.LockHunter(true);Check((memory.Read(player.Actor+0x1E3,1)[0]&0x80)!=0&&memory.Read(ctrl+0x111,1)[0]==1,"photo mode locks hunter movement through both the event flag and native controller movement gate");
            Check(photoMovement.PhotoLocked&&!memory.Read(root+MovementSpeed.Offset,14).SequenceEqual(MovementSpeed.Original),"photo lock installs the shared per-frame player movement guard");
            photoMovement.Apply(2);photoMovement.Restore();Check(photoMovement.PhotoLocked,"ordinary movement controls cannot release an active photo lock");
            memory.Write(player.Actor+0x1E3,[(byte)(memory.Read(player.Actor+0x1E3,1)[0]&0x7F)]);memory.Write(ctrl+0x111,[0]);camera.LockHunter(true);
            Check((memory.Read(player.Actor+0x1E3,1)[0]&0x80)!=0&&memory.Read(ctrl+0x111,1)[0]==1,"photo mode reasserts the movement lock when game updates clear its fields");
            camera.LockHunter(false);Check(memory.Read(ctrl+0x111,1)[0]==0&&memory.Read(player.Actor+0x1E3,1).SequenceEqual(hunterFlags),"unchecking hunter lock immediately restores both original movement gates");camera.LockHunter(true);
            camera.Step(1,0,0,0,0,2,90);
            Check(BitConverter.ToSingle(memory.Read(frame+0x48,4))==-2&&Math.Abs(BitConverter.ToSingle(memory.Read(frame+0x50,4))-MathF.PI/2)<.001,"photo mode moves its private camera and sets FOV without changing the follow-camera frame");
            Check(BitConverter.ToSingle(memory.Read(camOwner+0x48,4))==0&&memory.Read(root+0x143AC60,32).SequenceEqual(originalCode),"photo camera leaves the follow output and former camera hook site unchanged");
            camera.Step(0,0,0,.5f,.5f,0,20,1,.1f);camera.Reset();
            Check(memory.Read(frame+0x10,0x50).SequenceEqual(memory.Read(camOwner+0x10,0x50)),"photo reset restores entry position, basis, roll and FOV");
            Reject<ArgumentException>(()=>camera.Step(float.NaN,0,0,0,0,1,90),"photo mode rejects invalid movement before modifying the frame");
            memory.SetInt32(player.Component+0xF8,0);Reject<IOException>(camera.ValidateContext,"photo mode stops on death");camera.Restore();memory.SetInt32(player.Component+0xF8,player.Health);
            Check(memory.Int32(cam+0xC4)==0&&memory.Int32(cam+0xC8)==0&&BitConverter.ToUInt64(memory.Read(cam+0xD0,8))==0&&memory.Read(player.Actor+0x1E3,1).SequenceEqual(hunterFlags)&&memory.Read(ctrl+0x111,1)[0]==0,"photo disable restores camera selection and hunter movement even after death");
            camera.Enable();var map=memory.Int32(player.Actor+0x3F8);memory.SetInt32(player.Actor+0x3F8,map+1);Reject<IOException>(camera.ValidateContext,"photo mode refuses movement after a map change");camera.Restore();memory.SetInt32(player.Actor+0x3F8,map);
            memory.SetInt32(cam+0xC4,1);Reject<IOException>(camera.Enable,"photo mode preserves an already active external debug camera");memory.SetInt32(cam+0xC4,0);
            camera.Enable();camera.LockHunter(true);Ptr(cam+0xD0,camOwner);Reject<IOException>(camera.Restore,"photo restoration preserves a foreign replacement camera");
            Check(memory.Read(player.Actor+0x1E3,1).SequenceEqual(hunterFlags),"hunter movement restores independently when a foreign camera blocks restoration");Ptr(cam+0xD0,frame);camera.Restore();
            camera.Enable();camera.LockHunter(true);var world=memory.Pointer(root+PlayerSession.WorldPointer);Ptr(world+0x60,0);Ptr(camRoot+0x2830,0);
            Reject<IOException>(camera.ValidateContext,"photo mode rejects a null-player loading transition");camera.Restore();
            Check(memory.Int32(cam+0xC4)==0,"photo output restores during loading even if the follow camera and player are unavailable");
            Ptr(world+0x60,player.Actor);Ptr(camRoot+0x2830,camOwner);camera.Restore();Check(!camera.RestorationPending&&memory.Read(player.Actor+0x1E3,1).SequenceEqual(hunterFlags),"pending hunter movement restoration completes after loading");
            Check(!photoMovement.PhotoLocked&&memory.Read(root+MovementSpeed.Offset,14).SequenceEqual(MovementSpeed.Original),"photo exit removes its per-frame lock and restores native movement instructions");
            photoMovement.Apply(2);camera.Enable();camera.LockHunter(true);camera.Restore();Check(!photoMovement.PhotoLocked&&!memory.Read(root+MovementSpeed.Offset,14).SequenceEqual(MovementSpeed.Original),"photo exit preserves a configured movement speed override");photoMovement.Restore();
            memory.Write(ctrl+0x111,[1]);camera.Enable();camera.LockHunter(true);camera.Restore();
            Check(memory.Read(ctrl+0x111,1)[0]==1,"photo mode preserves a preexisting native movement lock on exit");memory.Write(ctrl+0x111,[0]);
            camera.Enable();camera.LockHunter(true);var replacement=memory.AllocateData(0x200);Ptr(player.Actor+0x58,replacement);
            Reject<IOException>(()=>camera.LockHunter(true),"photo mode stops reapplying movement flags after the hunter controller changes");camera.Restore();
            Check(memory.Read(replacement+0x111,1)[0]==0,"photo cleanup never writes the replacement movement controller");Ptr(player.Actor+0x58,ctrl);memory.Write(ctrl+0x111,[0]);
            Ptr(manager+0x48,options);memory.Write(options+9,[1]);var hud=new HudVisibility(memory,session);hud.Apply();Check(memory.Read(options+9,1)[0]==0,"HUD override uses the native front-end visibility option");hud.Restore();Check(memory.Read(options+9,1)[0]==1,"HUD reset restores captured visibility");
            hud.Apply();var clonedOptions=memory.AllocateData(0x30);Ptr(manager+0x48,clonedOptions);hud.Apply();hud.Restore();
            Check(memory.Read(clonedOptions+9,1)[0]==1&&!hud.RestorationPending,"HUD baseline survives a replacement options object cloned while hidden");
            hud.Apply();Ptr(manager+0x48,0);Reject<IOException>(hud.Restore,"HUD cleanup retains ownership when its options are temporarily unavailable");Ptr(manager+0x48,clonedOptions);hud.Restore();
            Check(memory.Read(clonedOptions+9,1)[0]==1&&!hud.RestorationPending,"HUD restoration retries successfully after its options become available");
            memory.Write(clonedOptions+9,[0]);hud.Show();Check(memory.Read(clonedOptions+9,1)[0]==1,"explicit HUD recovery repairs visibility left hidden by an earlier session");
        }
        finally{memory.Write(player.Actor+0x58,oldController);memory.Write(player.Component+0x200,oldFlags);memory.Write(root+0x553E860,oldCamera);memory.Write(manager+0x48,oldOptions);}
    }

}
