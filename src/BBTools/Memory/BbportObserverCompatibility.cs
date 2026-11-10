using System.IO;
using System.Security.Cryptography;

namespace BBTools.Memory;

// Read-only recognition of the bbport BBSC register-snapshot observer ABI.
// The callback remains in the call path; no runtime instructions are patched.
internal static class BbportObserverCompatibility
{
    private const int BodySize=562,TagOffset=298,CallbackOffset=308;
    private static readonly byte[] bodyHash=Convert.FromHexString("B424D5496F067920DEE3C5D43C4BDF3505D8E4FF966310626601147EC1BD24BC");
    public static bool Matches(IMemory memory,ulong image,ulong offset,byte[] expected,byte[] actual)
    {
        var (relocated,tag)=offset switch
        {
            0x132E050=>(7,124u), // BBSC Event.WarpNextStage_Bonfire
            0x154B110=>(6,149u), // BBSC PlayerWarp.LocalApply
            _=>(0,0u)
        };
        if(relocated==0||memory is not IExecutableMemory code||expected.Length<relocated||actual[0]!=0xE9)return false;
        if(!actual.AsSpan(5,relocated-5).SequenceEqual(Enumerable.Repeat((byte)0x90,relocated-5).ToArray())||
           !actual.AsSpan(relocated).SequenceEqual(expected.AsSpan(relocated)))return false;
        try
        {
            var entry=checked(image+offset);
            var target=RelativeTarget(checked(entry+5),BitConverter.ToInt32(actual,1));
            var size=BodySize+relocated+5;
            if(Overlaps(target,size,entry,expected.Length)||!code.IsExecutable(entry,expected.Length)||!code.IsExecutable(target,size))return false;
            var stub=memory.Read(target,size);
            if(BitConverter.ToUInt32(stub,TagOffset)!=tag)return false;
            var callback=BitConverter.ToUInt64(stub,CallbackOffset);
            if(Overlaps(callback,1,target,size)||Overlaps(callback,1,entry,expected.Length)||!code.IsExecutable(callback,1))return false;
            // All 562 body bytes are fixed except the audited site tag and
            // ASLR-dependent callback. This includes flags, MXCSR, every GPR
            // and YMM save/restore, stack alignment and the red-zone reserve.
            var body=stub.AsSpan(0,BodySize).ToArray();
            Array.Clear(body,TagOffset,4);Array.Clear(body,CallbackOffset,8);
            if(!SHA256.HashData(body).SequenceEqual(bodyHash)||
               !stub.AsSpan(BodySize,relocated).SequenceEqual(expected.AsSpan(0,relocated))||stub[BodySize+relocated]!=0xE9||
               RelativeTarget(checked(target+(ulong)size),BitConverter.ToInt32(stub,size-4))!=entry+(ulong)relocated)return false;
            if(!memory.Read(entry,expected.Length).SequenceEqual(actual)||!memory.Read(target,size).SequenceEqual(stub))return false;
            NativeCodeCompatibility.Diagnostic?.Invoke($"Accepted bbport BBSC observer at image+0x{offset:X}; tag {tag}, wrapper 0x{target:X}, callback 0x{callback:X}. Original entry and observer preserved.");
            return true;
        }
        catch(Exception ex)when(ex is IOException or OverflowException){return false;}
    }
    private static bool Overlaps(ulong first,int length,ulong second,int otherLength)=>
        first<checked(second+(ulong)otherLength)&&second<checked(first+(ulong)length);
    private static ulong RelativeTarget(ulong next,int displacement)
    {
        var value=checked((long)next+displacement);
        if(value<0x10000||value>=0x800000000000)throw new IOException("Invalid bbport observer branch target.");
        return (ulong)value;
    }
}
