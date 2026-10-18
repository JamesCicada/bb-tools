using System.IO;
namespace BBTools.Memory;

public sealed record WeaponRepairReport(int Slot,int CatalogId,int Before,int After,int Maximum)
{
    public string Label=>$"Equipment slot {Slot+1} · ID {CatalogId}: {Before} → {After} / {Maximum}";
}
public sealed partial class GameActions
{
    private ulong repairHelper;
    public static readonly (ulong Offset,byte[] Bytes)[] NativeRepairFingerprints=
    [
        (0x14D24E0,Convert.FromHexString("554889E54157415641554154534883EC68")),
        (0x14D26D0,Convert.FromHexString("554889E54156534883EC20")),
        (0x1A89070,Convert.FromHexString("554889E54156534889FB8B03"))
    ];
    public async Task<WeaponRepairReport[]> RepairWeaponsNativeAsync(int threshold=100)
    {
        if(threshold is <1 or >100)throw new ArgumentException("Repair threshold must be 1–100 percent.");
        var dispatcher=Ready();
        foreach(var f in NativeRepairFingerprints.Concat(PlayerSession.DurabilityFingerprints))
            if(!memory.Read(session.ImageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes))throw new IOException("Native repair instructions differ from the supported build.");
        repairHelper=repairHelper==0?memory.AllocateCode(BuildRepairHelper(session.ImageBase)):repairHelper;
        var payload=new byte[256];BitConverter.GetBytes(session.ReadCharacter().Address).CopyTo(payload,0);BitConverter.GetBytes(threshold).CopyTo(payload,8);
        await dispatcher.ExecuteAsync([new(repairHelper,dispatcher.Payload)],payload);
        var result=memory.Read(dispatcher.Payload,256);var reports=new List<WeaponRepairReport>();
        for(var i=0;i<8;i++)
        {
            var at=32+i*24;var id=BitConverter.ToInt32(result,at);if(id<=0)continue;
            var before=BitConverter.ToInt32(result,at+4);var after=BitConverter.ToInt32(result,at+8);var max=BitConverter.ToInt32(result,at+12);
            if(before<0||after<0||after>max||max<=0)throw new IOException($"Slot {i+1} returned inconsistent durability. No further repair was queued.");
            reports.Add(new(i,id,before,after,max));
        }
        return reports.ToArray();
    }
    internal static byte[] BuildRepairHelper(ulong imageBase)
    {
        using var stream=new MemoryStream();using var w=new BinaryWriter(stream);
        var labels=new Dictionary<string,long>();var jumps=new List<(long At,string Label)>();
        void B(string hex)=>w.Write(Convert.FromHexString(hex));void L(string name)=>labels[name]=stream.Position;
        void J(string op,string label){B(op);jumps.Add((stream.Position,label));w.Write(0);}
        void Fn(ulong offset){B("48B8");w.Write(imageBase+offset);B("FFD0");}
        // SysV, aligned local item/parameter descriptors. All native mutations run
        // on the player's update thread and use the game's own inventory setter.
        B("554889E55341544155415641574883EC584889FB4C8B274981C4D00100004531ED");
        L("loop");B("438B84AC9400000089042448C744240800000000C7442410FFFFFFFF4889E7");Fn(0x1A89070);
        B("8B74241081E6FFFFFF0F81FEFFFFFF0F");J("0F84","next");B("85F6");J("0F8E","next");
        B("89742440488D7C2420");Fn(0x1F29AA0);B("488B4424284885C0");J("0F84","next");
        B("440FB7B0BE0000004585F6");J("0F84","next");
        B("4C89E74489EE");Fn(0x14D26D0);B("4189C74539F7");J("0F87","next");
        // R10 points to this slot's output record; recompute after every call.
        B("456BD5184E8D5413208B4C244041890A45897A0445897A084589720C");
        B("4539F7");J("0F84","next");B("416BC7644489F10FAF4B0839C8");J("0F8F","next");
        B("4C89E74489EE4489F24429FA");Fn(0x14D24E0);
        B("4C89E74489EE");Fn(0x14D26D0);B("456BD5184E8D54132041894208");
        L("next");B("41FFC54183FD08");J("0F8C","loop");
        B("4883C458415F415E415D415C5B5DC3");
        var end=stream.Position;foreach(var j in jumps){stream.Position=j.At;w.Write((int)(labels[j.Label]-j.At-4));}stream.Position=end;return stream.ToArray();
    }
}
