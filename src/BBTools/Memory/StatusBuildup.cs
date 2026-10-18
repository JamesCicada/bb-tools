using System.IO;
namespace BBTools.Memory;
public sealed partial class PlayerSession
{
    public static readonly (ulong Offset,byte[] Bytes)[] StatusFingerprints =
    [
        (0x1A5C259,Convert.FromHexString("488B7B08488B074531")),
        (0x1A5D460,Convert.FromHexString("B858000000C3"))
    ];
    public void ClearStatusBuildup()
    {
        void Clear()
        {
            ValidateImage();foreach(var f in StatusFingerprints)if(!memory.Read(imageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes))throw new IOException("Status gauge instructions differ from the supported build.");
            var player=ReadPlayer();var modules=memory.Pointer(player.Actor+0x3B0);var resist=memory.Pointer(modules+0x28);
            if(memory.Pointer(resist)!=imageBase+0x5335900 || memory.Pointer(resist+8)!=player.Actor)throw new IOException("Status module ownership failed validation.");
            var values=memory.Read(resist+0x10,40);var any=false;
            for(var i=0;i<5;i++)
            {
                var current=BitConverter.ToInt32(values,i*4);var maximum=BitConverter.ToInt32(values,20+i*4);
                if(maximum is <0 or >1000000 || current<0 || current>maximum)throw new IOException("Status buildup failed validation.");any|=current!=0;
            }
            EnsureSamePlayer(player);if(memory.Pointer(modules+0x28)!=resist)throw new IOException("Status module changed before clear.");
            if(any)memory.Write(resist+0x10,new byte[20]);
        }
        if(memory is ProcessMemory process)process.Suspended(Clear);else Clear();
    }
}
