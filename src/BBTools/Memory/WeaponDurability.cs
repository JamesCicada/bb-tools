using System.IO;
using System.Text;
namespace BBTools.Memory;

public sealed record WeaponDurability(uint Handle,int CatalogId,ulong Instance,int Current,int Maximum);
public sealed partial class PlayerSession
{
    public static readonly (ulong Offset,byte[] Bytes)[] DurabilityFingerprints =
    [
        (0x1A93B70,Convert.FromHexString("8B4718C3")),(0x1A93B80,Convert.FromHexString("897718C3")),
        (0x14D26F3,Convert.FromHexString("8B448724")),(0x14D2534,Convert.FromHexString("E8376B5B008B45A025FFFFFF0F3D")),
        (0x1A8911C,Convert.FromHexString("488B4CD1084885C9")),(0x1A89126,Convert.FromHexString("394108750F")),
        (0x14D270D,Convert.FromHexString("8B8F7C01000039C8")),
        (0x14D2717,Convert.FromHexString("48C1E004480387B0010000")),
        (0x14D2724,Convert.FromHexString("29C848C1E004480387A0010000"))
    ];
    private ulong weaponParamFile;
    private Dictionary<int,ulong> weaponRows=[];
    private ulong WeaponParams()
    {
        var repo=memory.Pointer(imageBase+0x5540340);
        if(memory.Int32(repo+0x70)!=1) throw new IOException("A unique weapon parameter table is not loaded.");
        var cap=memory.Pointer(repo+0x78);var file=memory.Pointer(memory.Pointer(cap+0x70)+0x70);
        if(file==weaponParamFile) return file;
        var header=memory.Read(file,0x40);
        if(!Encoding.ASCII.GetString(header,12,21).StartsWith("EQUIP_PARAM_WEAPON_ST",StringComparison.Ordinal)) throw new IOException("Weapon parameter type failed validation.");
        var count=BitConverter.ToUInt16(header,10);if(count<2 || count>30000) throw new IOException("Invalid weapon row count.");
        var wide=header[0x2D]>3 && (header[0x2E]&2)!=0;var start=header[0x2D]==2?0x34:0x40;var stride=wide?24:12;
        var records=memory.Read(file+(ulong)start,count*stride);var rows=new Dictionary<int,ulong>();
        for(var i=0;i<count;i++)
        {
            var id=BitConverter.ToInt32(records,i*stride);var offset=wide?BitConverter.ToUInt64(records,i*stride+8):BitConverter.ToUInt32(records,i*stride+4);
            if(id<0 || offset<(ulong)(start+count*stride) || offset>0x2000000 || !rows.TryAdd(id,file+offset)) throw new IOException("Invalid weapon parameter records.");
        }
        weaponRows=rows;weaponParamFile=file;return file;
    }
    public WeaponDurability[] ReadWeaponDurability()
    {
        ValidateImage();foreach(var f in DurabilityFingerprints)
            if(!memory.Read(imageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes)) throw new IOException("Weapon durability instructions differ from the supported build.");
        var character=ReadCharacter();var inventory=character.Address+0x1D0;WeaponParams();
        var manager=memory.Pointer(imageBase+0x553E990);var result=new List<WeaponDurability>();
        // The equipment reference at +0x94 is a display/cache reference. Native
        // durability reads resolve +0x24's inventory index through EquipInventoryData.
        var items=inventory+0x158;
        var first=unchecked((uint)memory.Int32(items+0x24));
        var last=unchecked((uint)memory.Int32(items+0x88));
        if(first>100000 || last>100000)throw new IOException("Invalid equipped inventory bounds.");
        for(var slot=0;slot<8;slot++)
        {
            var index=unchecked((uint)memory.Int32(inventory+0x24+(ulong)slot*4));
            if(index==uint.MaxValue)continue;
            if(index>last)throw new IOException("Equipped weapon inventory index is unavailable.");
            var entry=index<first?memory.Pointer(items+0x58)+(ulong)index*16:memory.Pointer(items+0x48)+(ulong)(index-first)*16;
            var handle=unchecked((uint)memory.Int32(entry));
            if(handle==0 || (handle&0x800000)==0 || (handle&0xFFFF)==0xFFFF) continue;
            var at=BitConverter.ToUInt64(memory.Read(manager+8+(handle&0xFFFF)*8,8));if(at==0) continue;
            if(at<0x10000 || at%8!=0 || unchecked((uint)memory.Int32(at+8))!=handle) throw new IOException("Equipped item handle ownership changed.");
            if(memory.Pointer(at)!=imageBase+0x53362D0) continue; // Armor and other item types are never modified.
            var id=memory.Int32(at+0xC);if(id<=0 || id>=0x0FFFFFFF || !weaponRows.TryGetValue(id/100*100,out var row)) throw new IOException("Equipped weapon catalog row is unavailable.");
            var maximum=BitConverter.ToUInt16(memory.Read(row+0xBE,2));var current=memory.Int32(at+0x18);
            if(maximum==0) continue;
            if(current<0 || current>maximum) throw new IOException("Weapon durability failed validation.");
            result.Add(new(handle,id,at,current,maximum));
        }
        if(ReadCharacter().Address!=character.Address) throw new IOException("Character changed while reading weapons.");
        return result.DistinctBy(x=>x.Handle).ToArray();
    }
    public int RepairWeapons(int thresholdPercent=100)
    {
        if(thresholdPercent is <1 or >100) throw new ArgumentException("Repair threshold must be 1–100 percent.");
        var repaired=0;
        void Repair()
        {
            var character=ReadCharacter();var weapons=ReadWeaponDurability();
            if(ReadCharacter().Address!=character.Address) throw new IOException("Character changed before repair.");
            foreach(var weapon in weapons)
            {
                if(weapon.Current>=weapon.Maximum || (long)weapon.Current*100>(long)weapon.Maximum*thresholdPercent) continue;
                memory.SetInt32(weapon.Instance+0x18,weapon.Maximum);repaired++;
                if(memory.Int32(weapon.Instance+0x18)!=weapon.Maximum)throw new IOException("Weapon repair did not persist. Retry after loading finishes.");
            }
        }
        if(memory is ProcessMemory process) process.Suspended(Repair);else Repair();
        return repaired;
    }
}
