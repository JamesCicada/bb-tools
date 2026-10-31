using System.IO;
namespace BBTools.Memory;
public sealed class HudVisibility(IGameMemory memory,PlayerSession session)
{
    public static readonly (ulong Offset,byte[] Bytes)[] Fingerprints=[(0x1BFBF6B,Convert.FromHexString("488D05BEF19303488B00488B40484885C0740A80780900"))];
    private bool owned;private byte original;
    public bool RestorationPending=>owned;
    private ulong Resolve()
    {
        session.ValidateImage();foreach(var f in Fingerprints)if(!memory.Read(session.ImageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes))throw new IOException("HUD visibility reader differs from the supported build.");
        var data=memory.Pointer(session.ImageBase+0x553B130);var options=memory.Pointer(data+0x48);if(memory.Read(options+9,1)[0]>1)throw new IOException("HUD visibility value failed validation.");return options;
    }
    public void Apply()
    {
        var current=Resolve();
        if(owned&&memory.Read(current+9,1)[0]==0)return;
        memory.Suspended(()=>
        {
            if(Resolve()!=current)throw new IOException("HUD options changed.");
            // Loading can clone the hidden option into a replacement object. Keep
            // the session's original visibility rather than recapturing that zero.
            if(!owned){original=memory.Read(current+9,1)[0];owned=true;}
            memory.Write(current+9,[0]);
        });
    }
    public void Restore()
    {
        if(!owned)return;
        memory.Suspended(()=>{var current=Resolve();memory.Write(current+9,[original]);owned=false;});
    }
    // Explicit recovery also repairs an option left hidden by an older build.
    public void Show()=>memory.Suspended(()=>{var current=Resolve();memory.Write(current+9,[1]);owned=false;});
}
