using BBTools.Memory;

internal static class BbportCompatibilityChecks
{
    // Independent register-snapshot ABI fixture, audited against the emitted
    // bbport trampoline. Tag and callback immediates are populated per case.
    private const string BodyHex=
        "488DA424E0FCFFFF4C899C24900200009C415B4C899C24980200004989E34883E4F04889042448895C240848894C24104889542418488974242048897C242848896C24304C894424404C894C24484C895424504C896424604C896C24684C897424704C897C2478"+
        "498B83900200004889442458498B83980200004889842480000000498D832003000048894424380FAE9C2488000000"+
        "C5FE7F842490000000C5FE7F8C24B0000000C5FE7F9424D0000000C5FE7F9C24F0000000C5FE7FA42410010000C5FE7FAC2430010000C5FE7FB42450010000C5FE7FBC2470010000"+
        "C57E7F842490010000C57E7F8C24B0010000C57E7F9424D0010000C57E7F9C24F0010000C57E7FA42410020000C57E7FAC2430020000C57E7FB42450020000C57E7FBC2470020000"+
        "4989E4BF00000000498D342448B80000000000000000FFD04C89E4"+
        "C5FE6F842490000000C5FE6F8C24B0000000C5FE6F9424D0000000C5FE6F9C24F0000000C5FE6FA42410010000C5FE6FAC2430010000C5FE6FB42450010000C5FE6FBC2470010000"+
        "C57E6F842490010000C57E6F8C24B0010000C57E6F9424D0010000C57E6F9C24F0010000C57E6FA42410020000C57E6FAC2430020000C57E6FB42450020000C57E6FBC2470020000"+
        "0FAE942488000000488B5C2408488B4C2410488B542418488B742420488B7C2428488B6C24304C8B4424404C8B4C24484C8B5424504C8B5C24584C8B6424604C8B6C24684C8B7424704C8B7C2478488B842480000000509D488B0424488B642438";
    private sealed class CodeMemory : IExecutableMemory
    {
        public readonly Dictionary<ulong,byte[]> Blocks=[];
        public readonly HashSet<ulong> NonExecutable=[];
        public Action<ulong>? BeforeRead;
        public byte[] Read(ulong at,int size)
        {
            BeforeRead?.Invoke(at);
            foreach(var (start,bytes) in Blocks)
                if(at>=start&&at-start<=(ulong)bytes.Length&&(ulong)size<=(ulong)bytes.Length-(at-start))return bytes.AsSpan((int)(at-start),size).ToArray();
            throw new IOException("Unmapped fixture memory.");
        }
        public void Write(ulong at,byte[] bytes)=>throw new Exception("Observer checks must never write.");
        public bool IsExecutable(ulong at,int size)=>Blocks.Any(p=>!NonExecutable.Contains(p.Key)&&at>=p.Key&&at-p.Key<=(ulong)p.Value.Length&&(ulong)size<=(ulong)p.Value.Length-(at-p.Key));
    }
    public static void Run(Action<bool,string> check)
    {
        const ulong image=0x800000000,stub=image-0x10000,callback=0x7F1000001000;
        foreach(var (offset,relocated,tag,kind) in new[]{(0x132E050UL,7,124u,GameActionKind.Warp),(0x154B110UL,6,149u,GameActionKind.RestoreLocation)})
        {
            var memory=new CodeMemory();
            foreach(var f in GameActions.Fingerprints)memory.Blocks[image+f.Offset]=f.Bytes.ToArray();
            memory.Blocks[callback]=[0xC3];
            var expected=GameActions.Fingerprints.Single(f=>f.Offset==offset).Bytes;
            var entry=image+offset;
            var body=Convert.FromHexString(BodyHex);
            BitConverter.GetBytes(tag).CopyTo(body,298);BitConverter.GetBytes(callback).CopyTo(body,308);
            var wrapper=body.Concat(expected.Take(relocated)).Concat(new byte[]{0xE9}).Concat(BitConverter.GetBytes(checked((int)((long)(entry+(ulong)relocated)-(long)(stub+(ulong)body.Length+(ulong)relocated+5))))).ToArray();
            var patched=expected.ToArray();patched[0]=0xE9;BitConverter.GetBytes(checked((int)((long)stub-(long)(entry+5)))).CopyTo(patched,1);Array.Fill(patched,(byte)0x90,5,relocated-5);
            memory.Blocks[entry]=patched;memory.Blocks[stub]=wrapper;
            bool Matches()=>NativeCodeCompatibility.Matches(memory,image,offset,expected);
            check(Matches(),$"BBSC {kind} observer validates its exact wrapper, tag, original prologue and return address");
            GameActions.ValidateActionInstructions(memory,image,kind);
            check(true,$"BBSC {kind} passes the complete action profile without writing instructions");
            var fixedBytesRejected=true;
            for(var i=0;i<body.Length;i++)
            {
                if(i>=308&&i<316)continue;
                memory.Blocks[stub]=wrapper.ToArray();memory.Blocks[stub][i]^=1;
                fixedBytesRejected&=!Matches();
            }
            check(fixedBytesRejected,$"BBSC {kind} rejects every altered fixed register-preservation byte and site tag");
            memory.Blocks[stub]=wrapper.ToArray();memory.Blocks[stub][body.Length]^=1;
            check(!Matches(),$"BBSC {kind} rejects changed relocated guest instructions");
            memory.Blocks[stub]=wrapper.ToArray();memory.Blocks[stub][^1]^=1;
            check(!Matches(),$"BBSC {kind} rejects an incorrect resume target");
            memory.Blocks[stub]=wrapper.ToArray();memory.NonExecutable.Add(stub);
            check(!Matches(),$"BBSC {kind} rejects non-executable wrappers");
            memory.NonExecutable.Clear();memory.NonExecutable.Add(callback);
            check(!Matches(),$"BBSC {kind} rejects non-executable callbacks");
            memory.NonExecutable.Clear();memory.Blocks[entry]=patched.ToArray();memory.Blocks[entry][5]^=1;
            check(!Matches(),$"BBSC {kind} rejects unknown entry padding");
            memory.Blocks[entry]=patched.ToArray();var reads=0;
            memory.BeforeRead=at=>{if(at==entry&&++reads==2)memory.Blocks[entry][1]^=1;};
            check(!Matches(),$"BBSC {kind} rejects code changed during validation");
            memory.BeforeRead=null;memory.Blocks[entry]=patched.ToArray();
            BitConverter.GetBytes(stub).CopyTo(memory.Blocks[stub],308);
            check(!Matches(),$"BBSC {kind} rejects callbacks into its own wrapper");
        }
    }
}
