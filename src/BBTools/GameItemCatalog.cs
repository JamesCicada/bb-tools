using System.IO;
using System.IO.Compression;
using System.Text;
using BBTools.Memory;
namespace BBTools;

// Read item-name tables from the user's own game. No game strings or archives
// are bundled with BB Tools. This reader intentionally accepts only the verified
// little-endian BND4 / FMG v2 layout and bounded DCX DFLT streams.
public static class GameItemCatalog
{
    public static ItemChoice[] Load(string gameFolder,string language="engus")
    {
        if(language.Length is <2 or >12||language.Any(c=>!char.IsAsciiLetter(c)))throw new ArgumentException("Invalid message language.");
        var root=Directory.Exists(Path.Combine(gameFolder,"dvdroot_ps4"))?Path.Combine(gameFolder,"dvdroot_ps4"):gameFolder;
        var file=Path.Combine(root,"msg",language,"item.msgbnd.dcx");using var input=File.OpenRead(file);if(input.Length>64*1024*1024)throw new IOException("Item message archive is too large.");
        using var source=new MemoryStream();input.CopyTo(source);return ParseArchive(source.ToArray());
    }
    internal static ItemChoice[] ParseArchive(byte[] source)
    {
        var data=source;
        if(source.AsSpan().StartsWith("DCX\0"u8))
        {
            if(source.Length<0x4C||!source.AsSpan(0x28,4).SequenceEqual("DFLT"u8))throw new IOException("Only DFLT message archives are supported.");
            using var packed=new MemoryStream(source,0x4C,source.Length-0x4C);using var z=new ZLibStream(packed,CompressionMode.Decompress);using var expanded=new MemoryStream();var buffer=new byte[8192];int n;
            while((n=z.Read(buffer))>0){if(expanded.Length+n>64*1024*1024)throw new IOException("Expanded item archive exceeds the limit.");expanded.Write(buffer,0,n);}data=expanded.ToArray();
        }
        static void Range(byte[] b,long at,long size){if(at<0||size<0||at>b.Length||size>b.Length-at)throw new IOException("Item table offset is outside its file.");}
        Range(data,0,0x40);if(!data.AsSpan(0,4).SequenceEqual("BND4"u8))throw new IOException("Expected a BND4 item message archive.");
        var count=BitConverter.ToInt32(data,0xC);var stride=BitConverter.ToInt64(data,0x20);if(count is <1 or >1000||stride<0x24||stride>0x80)throw new IOException("Unsupported item binder layout.");
        Range(data,0x40,count*stride);var result=new Dictionary<(uint,int),ItemChoice>();
        for(var i=0;i<count;i++)
        {
            var header=checked((int)(0x40+i*stride));var id=BitConverter.ToInt32(data,header+0x1C);uint category=id switch{10=>0x40000000,11=>0,12=>0x10000000,13=>0x20000000,_=>uint.MaxValue};if(category==uint.MaxValue)continue;
            var size=BitConverter.ToInt64(data,header+0x10);var offset=BitConverter.ToUInt32(data,header+0x18);Range(data,offset,size);if(size>16*1024*1024)throw new IOException("Item-name table exceeds the limit.");
            var b=data.AsSpan((int)offset,(int)size).ToArray();Range(b,0,0x28);if(b[1]!=0||b[2]!=2)throw new IOException("Only little-endian FMG v2 item names are supported.");
            var groups=BitConverter.ToInt32(b,12);var strings=BitConverter.ToInt32(b,16);var pointers=BitConverter.ToInt64(b,24);
            if(groups is <0 or >20000||strings is <0 or >30000)throw new IOException("Invalid item-name counts.");Range(b,0x28,groups*16L);Range(b,pointers,strings*8L);
            for(var g=0;g<groups;g++)
            {
                var at=0x28+g*16;var index=BitConverter.ToInt32(b,at);var first=BitConverter.ToInt32(b,at+4);var last=BitConverter.ToInt32(b,at+8);
                if(first<0||last<first||last-first>30000||index<0||(long)index+last-first>=strings)throw new IOException("Invalid item-name range.");
                for(var entry=first;entry<=last;entry++)
                {
                    var text=BitConverter.ToInt64(b,checked((int)(pointers+((long)index+entry-first)*8)));if(text==0||entry==0||entry>=0x0FFFFFFF)continue;Range(b,text,2);
                    var end=text;while(end+2<=b.Length&&end-text<=512&&BitConverter.ToUInt16(b,(int)end)!=0)end+=2;
                    if(end+2>b.Length||end-text>512)throw new IOException("Invalid item-name string.");var name=Encoding.Unicode.GetString(b,(int)text,(int)(end-text)).Trim();if(string.IsNullOrWhiteSpace(name))continue;
                    result[(category,entry)]=new(name,entry,category);
                }
            }
        }
        if(result.Count==0)throw new IOException("No supported item-name tables were found.");return result.Values.OrderBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.Id).ToArray();
    }
}
