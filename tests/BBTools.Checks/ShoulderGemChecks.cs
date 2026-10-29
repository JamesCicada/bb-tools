using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BBTools;
using BBTools.Memory;

internal static partial class Program
{
    private static void ShoulderHookChecks(ProcessMemory memory, PlayerSession session, ulong root, ulong world, ulong actor)
    {
        var output = memory.AllocateData(0x100); var source = memory.AllocateData(0x100);
        var task = memory.AllocateData(0x200); var debug = memory.AllocateData(0x1000);
        var camera = memory.AllocateData(0x3000); var controls = memory.AllocateData(16);
        void Ptr(ulong at, ulong value) => memory.Write(at, BitConverter.GetBytes(value));
        void Vector(ulong at, params float[] v) => memory.Write(at, v.SelectMany(BitConverter.GetBytes).ToArray());
        Ptr(task, output); Ptr(task + 0x110, debug); Ptr(task + 0x128, source);
        Ptr(root + 0x553E860, camera); Ptr(camera + 0x2830, source); Ptr(controls + 8, actor);
        Vector(output + 0x10, 1, 0, 0, 0); Vector(task + 0x20, 0, 0, 1, 0); Vector(task + 0x30, 10, 20, 30, 1);
        Vector(task + 0x60, 2, 3, 4, 5); Vector(task + 0x70, 6, 7, 8, 9);
        Marshal.Copy(ShoulderCamera.Original.Concat(new byte[] { 0xC3 }).ToArray(), 0, (nint)(root + ShoulderCamera.Offset), ShoulderCamera.Original.Length + 1);
        VirtualProtect((nint)(root + (ShoulderCamera.Offset & ~4095UL)), 4096, 0x20, out _);
        var code = memory.AllocateCode(ShoulderCamera.Build(root, controls));
        var patch = Convert.FromHexString("FF2500000000").Concat(BitConverter.GetBytes(code)).Concat(Enumerable.Repeat((byte)0x90, ShoulderCamera.Original.Length - 14)).ToArray();
        memory.ReplaceShoulderCode(root, ShoulderCamera.Original, patch);
        // Windows wrapper preserves nonvolatile registers; native site sees task
        // in R13, distinct output in R15, freshly composed XMM2/3, and known flags.
        var wrapper = Convert.FromHexString("41554157574883EC204989CD4C8B39410F105520410F105D30410F104560410F104D704839C948B8")
            .Concat(BitConverter.GetBytes(root + ShoulderCamera.Offset))
            .Concat(Convert.FromHexString("FFD09C5849898550010000410F118580000000410F118D900000004883C4205F415F415DC3")).ToArray();
        var call = Marshal.GetDelegateForFunctionPointer<Getter>((nint)memory.AllocateCode(wrapper));
        void Run(float offset) { memory.Write(controls, BitConverter.GetBytes(offset)); call((nint)task); }
        float F(int at) => BitConverter.ToSingle(memory.Read(output + (ulong)at, 4));
        Run(.75f);
        Check(F(0x40) == 10.75f && F(0x44) == 20 && F(0x48) == 30 && F(0x4C) == 1,
            "shoulder hook shifts a distinct output frame right while preserving height, depth and homogeneous position");
        Run(.75f); Check(F(0x40) == 10.75f, "shoulder hook recomposes every frame without accumulating drift");
        Vector(output + 0x10, 0, 0, -1, 1); Run(-1.5f);
        Check(F(0x40) == 10 && F(0x48) == 31.5f && F(0x4C) == 1, "negative shoulder offsets follow the camera's rotated local right vector and preserve position W");
        Check(memory.Read(task + 0x60, 32).SequenceEqual(memory.Read(task + 0x80, 32)) && (BitConverter.ToUInt64(memory.Read(task + 0x150, 8)) & 0x8D5) == 0x44,
            "shoulder hook restores both scratch vector registers and comparison flags");
        Ptr(world + 0x60, actor + 0x1000); Run(1); Check(F(0x48) == 30, "shoulder hook skips a replacement actor during loading"); Ptr(world + 0x60, actor);
        memory.SetInt32(debug + 0xC4, 1); Run(1); Check(F(0x48) == 30, "shoulder hook preserves the native debug-camera mode"); memory.SetInt32(debug + 0xC4, 0);
        Ptr(task + 0x128, source + 0x10); Run(1); Check(F(0x48) == 30, "shoulder hook ignores an unrelated camera task"); Ptr(task + 0x128, source);
        Run(0); Check(F(0x48) == 30, "pausing or centering the shoulder offset produces native camera coordinates");
        memory.ReplaceShoulderCode(root, patch, ShoulderCamera.Original);
        Reject<IOException>(() => memory.ReplaceShoulderCode(root, patch, ShoulderCamera.Original), "shoulder patch ownership prevents overwriting externally changed instructions");
        Reject<ArgumentException>(() => new ShoulderCamera(memory, session).Apply(float.NaN), "nonfinite shoulder offset is rejected before touching the process");
        Reject<ArgumentException>(() => new PracticePreset("invalid", false, false, [], 1, 1, CameraHorizontal: 3).Validate(), "presets reject unsafe shoulder offsets");
    }

    private static void GemChecks(ProcessMemory memory, PlayerSession session, ulong root)
    {
        var repoSlot = root + 0x5540340; var previous = memory.Read(repoSlot, 8);
        var repo = memory.AllocateData(0x2000); var data = session.ReadCharacter().Address;
        var inventory = data + 0x328; var oldInventory = memory.Read(inventory, 0x100);
        var handlesSlot = root + 0x553E990; var oldHandles = memory.Read(handlesSlot, 8);
        void Ptr(ulong at, ulong value) => memory.Write(at, BitConverter.GetBytes(value));
        void Int(ulong at, int value) => memory.SetInt32(at, value);
        ulong Table(bool effects)
        {
            var cap = memory.AllocateData(0x100); var holder = memory.AllocateData(0x100); var file = memory.AllocateData(0x1000);
            var name = effects ? "GemeffectParam\0" : "GemGenParam\0"; var text = memory.AllocateData(64); memory.Write(text, Encoding.Unicode.GetBytes(name));
            var slot = repo + 0x70 + (effects ? 43UL : 42UL) * 0x48; Int(slot, 1); Ptr(slot + 8, cap); Ptr(cap + 0x18, text); Ptr(cap + 0x70, holder); Ptr(holder + 0x70, file);
            var header = new byte[64]; Encoding.ASCII.GetBytes(effects ? "GEMEFFECT_PARAM_ST\0" : "GEM_GEN_PARAM_ST\0").CopyTo(header, 12);
            BitConverter.GetBytes((ushort)2).CopyTo(header, 10); header[0x2D] = 4; header[0x2E] = 2; memory.Write(file, header);
            Int(file + 0x40, effects ? 13101 : 4); Ptr(file + 0x48, 0x100); Int(file + 0x58, effects ? 17310 : 5); Ptr(file + 0x60, effects ? 0x124UL : 0x190UL);
            if (!effects) { memory.Write(file + 0x18C, [2]); memory.Write(file + 0x21C, [1]); }
            else { Int(file + 0x100, 13101); Int(file + 0x108, 1); Int(file + 0x124, 17310); Int(file + 0x12C, 10); }
            return file;
        }
        try
        {
            Ptr(repoSlot, repo); var generators = Table(false); var effects = Table(true);
            var reader = new BloodGems(memory, session); var catalog = reader.ReadCatalog();
            Check(catalog.Templates.Single().Id == 4 && catalog.Effects.Length == 2 && catalog.Effects[1].Rank == 10, "gem catalog reads live generator/effect rows and excludes Caryll rune templates");
            var spec = new GemSpec(4, 63, [13101, -1, 17310, -1, -1, -1]); var payload = reader.Prepare(spec);
            Check(BitConverter.ToInt32(payload) == 1 && BitConverter.ToUInt32(payload, 8) == 0x80000004 && BitConverter.ToUInt64(payload, 40) == generators + 0x100 && BitConverter.ToInt32(payload, 48) == 63,
                "gem spawn payload carries category 8, a validated native generator row, and the chosen shape");
            Check(BitConverter.ToUInt64(payload, 64) == effects + 0x100 && BitConverter.ToUInt64(payload, 80) == 0 && BitConverter.ToUInt64(payload, 96) == effects + 0x124,
                "gem descriptor populates six effect row references while preserving empty slots");
            using (var dispatcher = new GameThreadQueue(memory, session))
            {
                var helper = memory.AllocateCode([0xC3]); var prepared = false;
                dispatcher.ExecuteAsync([new(helper)], preparePayload: () => { prepared = true; return reader.Prepare(spec); }).GetAwaiter().GetResult();
                Check(prepared && memory.Read(dispatcher.Payload, payload.Length).SequenceEqual(payload), "gem pointer payload resolves against the checked actor immediately before game-thread publication");
                Reject<IOException>(() => dispatcher.ExecuteAsync([new(helper)], preparePayload: () => throw new IOException("simulated changed parameter table")).GetAwaiter().GetResult(),
                    "pre-publication gem validation failure cancels the request without executing native code");
                Check(memory.Read(dispatcher.Payload, payload.Length).SequenceEqual(payload), "failed gem preparation does not overwrite the last payload");
                Reject<ArgumentException>(() => dispatcher.ExecuteAsync([new(helper)], preparePayload: () => new byte[513]).GetAwaiter().GetResult(), "prepared payload cannot exceed the dispatcher workspace boundary");
            }
            Reject<ArgumentException>(() => reader.Prepare(spec with { Source = 5 }), "rune templates cannot enter the blood-gem creation path");
            Reject<ArgumentException>(() => reader.Prepare(spec with { Effects = [7100000, -1, -1, -1, -1, -1] }), "ordinary item IDs cannot be mistaken for gem effect IDs");
            Reject<ArgumentException>(() => (spec with { Effects = [-1, -1, -1, -1, -1, -1] }).Validate(), "effectless gems cannot be created");
            Reject<ArgumentException>(() => (spec with { Shape = 3 }).Validate(), "invalid gem shapes are rejected");
            Reject<ArgumentException>(() => (spec with { Effects = [0, -1, -1, -1, -1, -1] }).Validate(), "empty native effect zero cannot create a propertyless gem");
            Int(effects + 0x100, -1); Int(effects + 0x120, -1);
            Reject<ArgumentException>(() => reader.Prepare(spec), "unused gem effect rows with no native property are rejected");
            Check(reader.ReadCatalog().Effects.All(x => x.Id != 13101), "unused effect rows are hidden from searchable choices"); Int(effects + 0x100, 13101);
            var handles = memory.AllocateData(0x100); var first = memory.AllocateData(32); var second = memory.AllocateData(32); var gem = memory.AllocateData(0x100);
            Ptr(handlesSlot, handles); Int(inventory + 0x24, 1); Int(inventory + 0x88, 1); Ptr(inventory + 0x58, first); Ptr(inventory + 0x48, second);
            const uint handle = 0xC0800001; Int(first, unchecked((int)handle)); Int(second, unchecked((int)handle)); Ptr(handles + 16, gem);
            Ptr(gem, root + 0x5336180); Int(gem + 8, unchecked((int)handle)); Int(gem + 12, unchecked((int)0x80000004)); Int(gem + 0x18, 1); Int(gem + 0x1C, 63);
            memory.Write(gem + 0x20, spec.Effects.SelectMany(BitConverter.GetBytes).ToArray());
            var before = memory.Read(gem, 0x38); var result = reader.ReadInventory();
            Check(result.Length == 1 && result[0].Handle == handle && result[0].Spec.Effects.SequenceEqual(spec.Effects) && memory.Read(gem, 0x38).SequenceEqual(before),
                "inventory reader resolves both segments, deduplicates native handles and copies gem effects without writes");
            Int(gem + 8, unchecked((int)handle + 0x10000)); Check(reader.ReadInventory().Length == 0, "recycled native gem handles are excluded from the editor");
            Int(gem + 8, unchecked((int)handle)); Int(gem + 0x18, 2); Check(reader.ReadInventory().Length == 0, "inventory rune instances cannot be edited as blood gems");
            Int(inventory + 0x88, 100000); Reject<IOException>(() => reader.ReadInventory(), "unbounded inventory traversal is rejected");
            var records = new byte[48]; BitConverter.GetBytes(1).CopyTo(records, 0); BitConverter.GetBytes(0x100UL).CopyTo(records, 8); BitConverter.GetBytes(2).CopyTo(records, 24); BitConverter.GetBytes(0x104UL).CopyTo(records, 32);
            Reject<IOException>(() => BloodGems.ParseRows(records, 2, 64, 24, root, 0x24), "overlapping gem parameter records are rejected");
            NaturalGemProfileChecks(memory, reader, generators, effects);
            var folder = Path.Combine(Path.GetTempPath(), "BBTools-gems-" + Guid.NewGuid().ToString("N"));
            try
            {
                var library = new UserLibrary(Path.Combine(folder, "library.db"), Path.Combine(folder, "absent.json"));
                library.SaveGemTexts([new("Add blood ATK +0.5", 13101, 0)]);
                Check(library.GemTexts().Single().Name == "Add blood ATK +0.5", "imported gem descriptions persist independently of ordinary item names");
                library.Save(new PracticePreset("Shoulder", false, false, [], 1, 1, CameraHorizontal: -.75f));
                Check(library.Presets().Single().CameraHorizontal == -.75f, "shoulder camera offset survives SQLite preset round trips");
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
        finally { memory.Write(repoSlot, previous); memory.Write(handlesSlot, oldHandles); memory.Write(inventory, oldInventory); }
    }

    private static void GemNativeHelperChecks()
    {
        var allocation = VirtualAlloc(0, 0x6000000, 0x3000, 4); if (allocation == 0) throw new Exception("Gem helper fixture allocation failed.");
        var root = (ulong)allocation; var payload = root + 0x5700000; var manager = root + 0x5710000;
        void Bytes(ulong at, byte[] b) => Marshal.Copy(b, 0, (nint)at, b.Length);
        try
        {
            // Constructor records descriptor and handle-manager pointers; award
            // checks the complete batch before release clears temporary ownership.
            Bytes(root + 0x1A87E20, Convert.FromHexString("C707010080C04889771048895718C3"));
            Bytes(root + 0x17D89F0, Convert.FromHexString("8B0689078B46048947048B4608894708C3"));
            Bytes(root + 0x1A8BC00, Convert.FromHexString("C70700000000C3"));
            foreach (var off in new ulong[] { 0x1A87E20, 0x17D89F0, 0x1A8BC00 }) VirtualProtect((nint)(root + (off & ~4095UL)), 4096, 0x20, out _);
            var helper = root + 0x5720000; Bytes(helper, GameActions.BuildGemHelper(root)); VirtualProtect((nint)helper, 4096, 0x20, out _);
            var wrapper = Convert.FromHexString("57564883EC284889CF48BE").Concat(BitConverter.GetBytes(root + 0x5730000)).Concat(Convert.FromHexString("48BA")).Concat(BitConverter.GetBytes(manager)).Concat(Convert.FromHexString("48B8")).Concat(BitConverter.GetBytes(helper)).Concat(Convert.FromHexString("FFD04883C4285E5FC3")).ToArray();
            var bridge = root + 0x5740000; Bytes(bridge, wrapper); VirtualProtect((nint)bridge, 4096, 0x20, out _);
            Marshal.WriteInt32((nint)payload, 1); Marshal.WriteInt32((nint)(payload + 8), unchecked((int)0x80000004));
            var call = Marshal.GetDelegateForFunctionPointer<Getter>((nint)bridge); call((nint)payload);
            Check(Marshal.ReadInt32((nint)manager) == 1 && unchecked((uint)Marshal.ReadInt32((nint)(manager + 4))) == 0xC0800001 && unchecked((uint)Marshal.ReadInt32((nint)(manager + 8))) == 0x80000004,
                "native gem helper constructs the owned handle at batch+4 before awarding the intact category-8 batch");
            Check((ulong)Marshal.ReadInt64((nint)(payload + 20)) == root + 0x5730000 && (ulong)Marshal.ReadInt64((nint)(payload + 28)) == payload + 32,
                "native gem helper passes the handle manager and generated descriptor using SysV arguments");
            Check(Marshal.ReadInt32((nint)(payload + 4)) == 0 && unchecked((uint)Marshal.ReadInt32((nint)(payload + 160))) == 0xC0800001 && Marshal.ReadInt32((nint)(payload + 164)) == 1,
                "native gem helper releases temporary ownership and records the awarded handle for inventory verification");
            VirtualProtect((nint)(root + 0x1A87000), 4096, 4, out _); Bytes(root + 0x1A87E20, Convert.FromHexString("C70700000000C3")); VirtualProtect((nint)(root + 0x1A87000), 4096, 0x20, out _);
            Bytes(payload, new byte[176]); Bytes(manager, new byte[16]); call((nint)payload);
            Check(Marshal.ReadInt32((nint)(payload + 164)) == 0 && Marshal.ReadInt32((nint)manager) == 0, "failed gem allocation never awards or reports a successful gem");
        }
        finally { VirtualFree(allocation, 0, 0x8000); }
    }
}
