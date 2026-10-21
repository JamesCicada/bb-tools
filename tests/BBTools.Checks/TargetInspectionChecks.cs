using System.IO;
using System.Text;
using BBTools;
using BBTools.Memory;

internal static partial class Program
{
    private sealed class ReadOnlyInspectionMemory(IMemory source) : IMemory
    {
        public byte[] Read(ulong at,int size)=>source.Read(at,size);
        public void Write(ulong at,byte[] value)=>throw new Exception("Enemy inspector attempted a game-memory write.");
    }
    private static void TargetInspectionChecks(ProcessMemory memory,ulong root)
    {
        void Ptr(ulong at,ulong value)=>memory.Write(at,BitConverter.GetBytes(value));
        void Int(ulong at,int value)=>memory.SetInt32(at,value);
        void Float(ulong at,float value)=>memory.Write(at,BitConverter.GetBytes(value));
        var session=new PlayerSession(new ReadOnlyInspectionMemory(memory),root);var world=memory.Pointer(root+PlayerSession.WorldPointer);var player=session.ReadPlayer();
        var game=root+0x5B00000;var camera=root+0x5B20000;var point=root+0x5B30000;var target=root+0x5B40000;var set=root+0x5B50000;var entries=root+0x5B60000;
        foreach(var f in PlayerSession.TargetFingerprints.Concat(PlayerSession.StatusFingerprints))memory.Write(root+f.Offset,f.Bytes);
        Ptr(root+0x553E860,game);Ptr(game+0x2830,camera);Ptr(camera+0x90,0);Ptr(game+8,point);Ptr(point+0x88,target);memory.Write(point+0x90,[0x20]);memory.Write(game+0x2820,[1]);
        Ptr(world+0x850+16,set);Int(set,2);Ptr(set+8,entries);
        for(var i=0;i<2;i++)
        {
            var actor=root+0x58C0000+(ulong)i*0x10000;var modules=root+0x58E0000+(ulong)i*0x10000;var stats=root+0x5900000+(ulong)i*0x10000;
            var resist=root+0x5920000+(ulong)i*0x10000;var armor=root+0x5940000+(ulong)i*0x10000;var effects=root+0x5960000+(ulong)i*0x10000;
            var transform=root+0x5B70000+(ulong)i*0x10000;
            Ptr(entries+(ulong)i*0x38,actor);Ptr(actor+0x18,entries+(ulong)i*0x38);Int(actor+8,0x8000+i);Int(actor+0x78,1);Int(actor+0x88,5);
            Ptr(actor+0x3B0,modules);Ptr(modules+0x20,stats);Ptr(stats+8,actor);Int(stats+0xF8,400+i*100);Int(stats+0xFC,600);Int(stats+0x134,60);Int(stats+0x138,100);
            memory.Write(stats+0x158,Encoding.Unicode.GetBytes(i==0?"c1234":"c5678"));Ptr(stats+0x168,5);Ptr(stats+0x170,7);
            Ptr(modules+0x28,resist);Ptr(resist,root+0x5335900);Ptr(resist+8,actor);int[] values=[12,0,25,10,0,100,0,150,300,0];for(var j=0;j<10;j++)Int(resist+0x10+(ulong)j*4,values[j]);
            Ptr(modules+0x48,armor);Ptr(armor,root+0x5335990);Ptr(armor+8,actor);Float(armor+0x10,30);Float(armor+0x14,50);Float(armor+0x18,2);Float(armor+0x1C,3.5f);
            Ptr(actor+0x1C8,effects);Ptr(effects+0x18,actor);Ptr(modules+0x68,transform);Float(transform+0x1E0,3);Float(transform+0x1E8,4);
        }
        var actorA=root+0x58C0000;var actorB=root+0x58D0000;var statsA=root+0x5900000;var armorA=root+0x5940000;var effectsA=root+0x5960000;
        var transformPlayer=root+0x5B90000;Ptr(memory.Pointer(player.Actor+0x3B0)+0x68,transformPlayer);
        var node=root+0x5980000;var row=root+0x5BA0000;Ptr(effectsA+8,node);
        for(var i=0;i<3;i++)
        {
            var at=node+(ulong)i*0x100;var parameter=row+(ulong)i*0x200;
            Float(at,i==0?30:i==1?2.5f:-1);Int(at+0x1C,i==1?2:4);Int(at+0x40,1200+i);Ptr(at+0x48,parameter);Ptr(at+0x58,i==2?0:at+0x100);
            memory.Write(parameter+0x156,BitConverter.GetBytes((ushort)(i==0?2:i==1?6:777)));
        }
        Int(target+0x64,unchecked((int)0x10008000));var selected=session.ReadLockedEnemy()!;
        Check(selected.Address==actorA,"lock-on inspection resolves the authoritative selected point even when the camera mirror is null");
        Ptr(game+0x2830,0);Check(session.ReadLockedEnemy()?.Address==actorA,"target inspection works without a camera owner or mirrored target pointer");Ptr(game+0x2830,camera);
        Ptr(game+8,0);Check(session.ReadLockedEnemy()==null,"an empty native target list waits without claiming the player is unavailable");Ptr(game+8,point);
        Ptr(point+0x80,point);Reject<IOException>(()=>session.ReadLockedEnemy(),"cyclic native lock-on point lists are rejected");Ptr(point+0x80,0);
        var extra=point+0x200;Ptr(point+0x80,extra);memory.Write(extra+0x90,[0x20]);Reject<IOException>(()=>session.ReadLockedEnemy(),"ambiguous selected lock-on points are rejected");Ptr(point+0x80,0);
        var snapshot=session.ReadEnemySnapshot(selected);
        Check(snapshot.Health==400&&snapshot.Stamina==60&&snapshot.Model=="c1234"&&snapshot.Distance==5,"read-only target snapshot reports health, stamina, model identity and measured distance");
        Check(snapshot.Gauges[2].Name=="Rapid poison"&&snapshot.Gauges[3].Name=="Frenzy"&&snapshot.Gauges[2].Current==25,"enemy gauges map blood/curse native slots to rapid poison and frenzy");
        Check(snapshot.Armor is {Current:30,Limit:50,Broken:false,Recovery:3.5f},"enemy super-armor snapshot reports current limit, recovery and native break state");
        Check(snapshot.Effects.Length==3&&snapshot.Effects[0].Active&&!snapshot.Effects[1].Active&&snapshot.Effects[2].Kind=="Native state 777"&&snapshot.Effects[2].Remaining==-1,"enemy effects retain durations, pending flags and unknown native state IDs");
        var monitor=new TargetMonitor();monitor.Poll(session);memory.Write(game+0x2820,[0]);Int(statsA+0xF8,350);monitor.Poll(session);
        Check(monitor.Snapshot?.Health==350&&monitor.Snapshot.Character.Address==actorA&&monitor.State.StartsWith("Unlocked"),"unlocking retains the same enemy and continues live updates without selecting a nearest actor");
        memory.Write(armorA+0x20,[1]);monitor.Poll(session);monitor.Poll(session);Check(monitor.ObservedBreaks==1,"sampled break count increments only on a native unbroken-to-broken transition");
        memory.Write(armorA+0x20,[0]);monitor.Poll(session);memory.Write(armorA+0x20,[1]);monitor.Poll(session);Check(monitor.ObservedBreaks==2,"another observed super-armor break is counted for the same retained enemy");
        Int(statsA+0xF8,0);monitor.Poll(session);Check(monitor.Snapshot?.Health==0,"a defeated enemy keeps its final zero-health snapshot");
        Ptr(entries,0);var last=monitor.Snapshot;monitor.Poll(session);Check(ReferenceEquals(last,monitor.Snapshot)&&monitor.State.Contains("retained"),"unloaded enemy data remains displayed without reading through a stale character-set entry");
        Int(target+0x64,unchecked((int)0x10008001));memory.Write(game+0x2820,[1]);monitor.Poll(session);
        Check(monitor.Snapshot?.Character.Address==actorB&&monitor.Snapshot.Health==500&&monitor.ObservedBreaks==0,"targeting a different enemy replaces the retained data and resets sampled break history");
        monitor.Unavailable("Disconnected");last=monitor.Snapshot;Check(ReferenceEquals(last,monitor.Snapshot)&&monitor.State.Contains("Disconnected"),"disconnect preserves the last enemy snapshot with an explicit stale state");
        Ptr(entries,actorA);memory.Write(game+0x2820,[1]);Int(target+0x64,unchecked((int)0x20008000));Check(session.ReadLockedEnemy()==null,"non-character target tags do not become enemy selections");
        Int(target+0x64,unchecked((int)0x10008000));Int(actorA+8,0x8002);Reject<IOException>(()=>session.ReadLockedEnemy(),"reused or mismatched actor handles are rejected");Int(actorA+8,0x8000);
        Ptr(node+0x258,node);Reject<IOException>(()=>session.ReadEnemySnapshot(selected),"cyclic enemy effect lists are bounded and rejected");Ptr(node+0x258,0);
        Float(armorA+0x10,float.NaN);Reject<IOException>(()=>session.ReadEnemySnapshot(selected),"invalid super-armor values are not shown as live enemy data");Float(armorA+0x10,30);
        memory.Write(game+0x2820,[0]);monitor.Poll(session);Check(ReferenceEquals(last,monitor.Snapshot),"paused/disconnected snapshots do not silently attach to an unrelated live enemy");
        foreach(var f in PlayerSession.ParryPracticeFingerprints)memory.Write(root+f.Offset,f.Bytes);
        var writable=new PlayerSession(memory,root);var protection=new EnemyPracticeProtection(writable);
        var combat=new ParryCombatSettings(false,false,["NoAi","OneShot","Stealth","Silence","NoFallDamage"],3,4).Drill();
        Check(combat.KeepHealth&&combat.Stamina&&combat.Damage==.1f&&combat.Echoes==4&&combat.Flags.Order().SequenceEqual(new[]{"GodMode","InfiniteItems","NoFallDamage"}.Order()),"parry drill enables resources and reduced damage while preserving rewards and removing conflicting combat flags");
        Reject<IOException>(()=>protection.ProtectLockedEnemy(),"target protection requires an explicit live lock-on");
        memory.Write(game+0x2820,[1]);Int(statsA+0xF8,200);memory.Write(statsA+0x200,[0xA0]);
        protection.ProtectLockedEnemy();
        Check(memory.Read(statsA+0x200,1)[0]==0xA4&&memory.Int32(statsA+0xF8)==200,"enemy practice owns only the native cannot-die bit and leaves damage and current HP intact");
        Check(!protection.NeedsUpdate(),"stable target protection avoids redundant writes and process suspension");
        memory.Write(game+0x2820,[0]);Int(statsA+0xF8,100);protection.Update();
        Check(protection.Target?.Address==actorA&&memory.Int32(statsA+0xF8)==100,"unlocking retains target protection without repeatedly healing the enemy");
        memory.Write(game+0x2820,[1]);writable.RestoreLockedEnemyHealth();
        Check(memory.Int32(statsA+0xF8)==600,"manual enemy health restore writes the locked living enemy maximum");
        memory.Write(statsA+0x200,[0xE4]);protection.Restore();
        Check(memory.Read(statsA+0x200,1)[0]==0xE0&&!protection.IsActive,"disabling enemy protection restores its original bit while preserving unrelated changes");
        memory.Write(statsA+0x200,[4]);protection.ProtectLockedEnemy();protection.Restore();
        Check(memory.Read(statsA+0x200,1)[0]==4,"preexisting enemy cannot-die protection survives disable");
        memory.Write(statsA+0x200,[0]);protection.ProtectLockedEnemy();Int(target+0x64,unchecked((int)0x10008001));protection.Update();
        Check(memory.Read(statsA+0x200,1)[0]==0&&(memory.Read(root+0x5910200,1)[0]&4)!=0&&protection.Target?.Address==actorB,"a different explicit lock-on transfers protection and restores the previous enemy");protection.Restore();
        Int(target+0x64,unchecked((int)0x10008000));protection.ProtectLockedEnemy();Int(actorA+8,0x8002);
        Reject<IOException>(()=>protection.Update(),"actor reuse stops target-protection updates");protection.Restore();
        Check(memory.Read(statsA+0x200,1)[0]==4&&!protection.IsActive,"restoration never writes through a reused character identity");Int(actorA+8,0x8000);memory.Write(statsA+0x200,[0]);
        protection.ProtectLockedEnemy();Int(player.Component+0xF8,0);protection.Restore();
        Check(memory.Read(statsA+0x200,1)[0]==0,"hunter death still permits restoration of an independently validated target");Int(player.Component+0xF8,player.Health);
        Int(statsA+0xF8,0);Reject<IOException>(()=>writable.RestoreLockedEnemyHealth(),"manual enemy health restore never revives a dead enemy");Int(statsA+0xF8,200);
        protection.ProtectLockedEnemy();var oldMap=memory.Int32(player.Actor+0x3F8);Int(player.Actor+0x3F8,oldMap+1);
        Reject<IOException>(()=>protection.Update(),"map changes disarm target protection instead of reusing its old context");protection.Restore();
        Check(memory.Read(statsA+0x200,1)[0]==4&&!protection.IsActive,"map changes discard ownership without writing to the old context");Int(player.Actor+0x3F8,oldMap);memory.Write(statsA+0x200,[0]);
        memory.Write(root+0x1A577E0,[0x90]);Reject<IOException>(()=>protection.ProtectLockedEnemy(),"mismatched native cannot-die instructions block target mutations");memory.Write(root+0x1A577E0,PlayerSession.ParryPracticeFingerprints[0].Bytes);

    }
}
