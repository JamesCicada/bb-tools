using System.IO;
namespace BBTools.Memory;
public sealed class HudVisibility(ProcessMemory memory,PlayerSession session)
{
    public static readonly (ulong Offset,byte[] Bytes)[] Fingerprints=[(0x1BFBF6B,Convert.FromHexString("488D05BEF19303488B00488B40484885C0740A80780900"))];
    private ulong owner;private byte original;
    private ulong Resolve()
    {
        session.ValidateImage();foreach(var f in Fingerprints)if(!memory.Read(session.ImageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes))throw new IOException("HUD visibility reader differs from the supported build.");
        var data=memory.Pointer(session.ImageBase+0x553B130);var options=memory.Pointer(data+0x48);if(memory.Read(options+9,1)[0]>1)throw new IOException("HUD visibility value failed validation.");return options;
    }
    public void Apply()
    {
        var current=Resolve();if(owner!=current){owner=0;original=memory.Read(current+9,1)[0];}
        if(owner==current&&memory.Read(current+9,1)[0]==0)return;
        memory.Suspended(()=>{if(Resolve()!=current)throw new IOException("HUD options changed.");memory.Write(current+9,[0]);owner=current;});
    }
    public void Restore()
    {
        if(owner==0)return;
        memory.Suspended(()=>{if(Resolve()!=owner){owner=0;return;}var value=memory.Read(owner+9,1)[0];if(value!=0&&value!=original)throw new IOException("HUD visibility changed externally.");memory.Write(owner+9,[original]);owner=0;});
    }
}
