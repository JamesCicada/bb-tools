using System.IO;
namespace BBTools.Memory;
public sealed partial class GameActions
{
    private ulong poisonHelper;
    public static readonly (ulong Offset,byte[] Bytes)[] PoisonFingerprints=[(0x17E1D90,Convert.FromHexString("554889E5415741564155415453504889"))];
    public async Task<int> CureActivePoisonAsync()
    {
        var dispatcher=Ready();foreach(var f in PoisonFingerprints)if(!memory.Read(session.ImageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes))throw new IOException("Effect removal differs from the supported build.");
        var actor=session.ReadPlayer().Actor;var container=memory.Pointer(actor+0x1C8);_ = EffectNodes(actor,container);
        poisonHelper=poisonHelper==0?memory.AllocateCode(BuildPoisonHelper(session.ImageBase)):poisonHelper;
        var payload=new byte[32];BitConverter.GetBytes(actor).CopyTo(payload,0);BitConverter.GetBytes(container).CopyTo(payload,8);
        await dispatcher.ExecuteAsync([new(poisonHelper,dispatcher.Payload)],payload);session.ClearStatusBuildup();return memory.Int32(dispatcher.Payload+16);
    }
    internal static byte[] BuildPoisonHelper(ulong imageBase)
    {
        using var stream=new MemoryStream();using var w=new BinaryWriter(stream);var labels=new Dictionary<string,long>();var jumps=new List<(long At,string Label)>();
        void B(string hex)=>w.Write(Convert.FromHexString(hex));void L(string n)=>labels[n]=stream.Position;void J(string op,string n){B(op);jumps.Add((stream.Position,n));w.Write(0);}
        B("554889E5534154415541564889FB4C8B6308488B034C39A0C8010000");J("0F85","end");B("4939442418");J("0F85","end");B("4D8B6C240841BE00080000");
        L("loop");B("4D85ED");J("0F84","end");B("41FFCE");J("0F88","end");B("498B45484885C0");J("0F84","next");B("0FB7885601000083F902");J("0F84","remove");B("83F905");J("0F85","next");
        L("remove");B("4C89E74C89EEBA0100000048B8");w.Write(imageBase+0x17E1D90);B("FFD04989C5FF4310");J("E9","loop");
        L("next");B("4D8B6D58");J("E9","loop");L("end");B("415E415D415C5B5DC3");
        var end=stream.Position;foreach(var j in jumps){stream.Position=j.At;w.Write((int)(labels[j.Label]-j.At-4));}stream.Position=end;return stream.ToArray();
    }
}
