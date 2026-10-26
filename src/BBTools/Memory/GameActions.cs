using System.IO;

namespace BBTools.Memory;

public sealed record ItemChoice(string Name, int Id, uint Category = 0x40000000)
{
    public string Label => $"{Name} · {Id}";
    public override string ToString() => Label;
}
public sealed record LampChoice(string Name, int Entity)
{
    public string Label => Name;
    public override string ToString() => Label;
}

public enum GameActionKind { Dispatcher, Item, Warp, RestoreLocation }

public sealed partial class GameActions(ProcessMemory memory, PlayerSession session) : IDisposable
{
    private GameThreadQueue? queue;
    public static readonly (ulong Offset, byte[] Bytes)[] Fingerprints =
    [
        (0x18FD960, Convert.FromHexString("488B87C0030000C30F1F840000000000")),
        (0x18F7891, Convert.FromHexString("498B06FF90C8010000")),
        (0x17D89F0, Convert.FromHexString("554889E54157415641554154534881EC")),
        (0x17D8A7C, Convert.FromHexString("418B4708488D8DC4FEFFFF894108498B07488901")),
        (0x1A86FF0, Convert.FromHexString("554889E54157415641554154534883EC")),
        (0x1A8714B, Convert.FromHexString("89C181E1FFFFFF0081C9000000B03DFFFF7F0041BCFFFFFFB0440F42E1")),
        (0x1A8BC00, Convert.FromHexString("554889E54156534889FB8B3385F60F84")),
        (0x154B110, Convert.FromHexString("554889E54157415641554154534883EC28")),
        (0x156D110, Convert.FromHexString("554889E541574156534883EC2889FB4C")),
        (0x156D0E0, Convert.FromHexString("488B059195FE034088B824150000C390")),
        (0x132E050, Convert.FromHexString("554889E54863C64869C889B5F8144889"))
    ];
    public static ItemChoice[] CommonItems =>
    [
        new("Blood Vial",1000), new("Quicksilver Bullets",900), new("Antidote",1100),
        new("Beast Blood Pellet",1110), new("Molotov Cocktail",1200), new("Fire Paper",1300),
        new("Bolt Paper",1320), new("Bold Hunter's Mark",1400), new("Blood Stone Shard",3000),
        new("Twin Blood Stone Shards",3010), new("Blood Stone Chunk",3020), new("Blood Rock",3030),
        new("Hunter Axe",5000000,0), new("Saw Cleaver",7000000,0), new("Saw Spear",7100000,0),
        new("Ludwig's Holy Blade",8100000,0), new("Threaded Cane",22000000,0),
        new("Holy Moonlight Sword",26000000,0), new("Hunter Pistol",14000000,0)
    ];
    public static LampChoice[] Lamps => DestinationCatalog.Lamps;
    public static void ValidateActionInstructions(IMemory source, ulong imageBase, GameActionKind kind)
    {
        // Only require code used by this request. A patched warp routine must not
        // disable items, menus, repair or multipliers that never call it.
        int[] indices = kind switch
        {
            GameActionKind.Dispatcher => [0, 1],
            GameActionKind.Item => [0, 1, 2, 3, 4, 5, 6],
            GameActionKind.Warp => [0, 1, 8, 9, 10],
            GameActionKind.RestoreLocation => [0, 1, 7],
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var differences = new List<string>();
        foreach (var index in indices)
        {
            var f = Fingerprints[index];
            var actual = source.Read(imageBase + f.Offset, f.Bytes.Length);
            if (!actual.SequenceEqual(f.Bytes))
                differences.Add($"image+0x{f.Offset:X} (0x{imageBase + f.Offset:X}): expected {Convert.ToHexString(f.Bytes)}, actual {Convert.ToHexString(actual)}");
        }
        if (differences.Count != 0)
            throw new IOException($"Native {kind} instructions differ from the supported 1.09 build. " +
                string.Join("; ", differences) + ". The runtime or a mod may have changed these instructions. No request was submitted. Include this session log when reporting compatibility issues.");
    }
    private GameThreadQueue Ready(GameActionKind kind = GameActionKind.Dispatcher)
    {
        session.ValidateImage();
        ValidateActionInstructions(memory, session.ImageBase, kind);
        var player = session.ReadPlayer();
        if (player.Health == 0) throw new IOException("Load a living character before submitting a game action.");
        if (queue != null && !queue.MatchesCurrentPlayer()) { queue.Dispose(); queue = null; }
        return queue ??= new GameThreadQueue(memory, session);
    }
    public static byte[] ItemBatch(int id, uint category, int quantity)
    {
        if (id is < 1 or > 0x0FFFFFFE || category is not (0 or 0x10000000 or 0x20000000 or 0x40000000))
            throw new ArgumentException("Enter a valid base item ID and category.");
        if (quantity is < 1 or > 99 || category != 0x40000000 && quantity != 1)
            throw new ArgumentException("Quantity must be 1â€“99 for goods, or 1 for equipment.");
        var batch = new byte[20];
        BitConverter.GetBytes(1).CopyTo(batch, 0);
        // Entry: owned item handle, catalog ID, quantity, sentinel. The handle starts
        // empty and MUST be constructed by the game before awarding the batch.
        BitConverter.GetBytes(category | (uint)id).CopyTo(batch, 8);
        BitConverter.GetBytes(quantity).CopyTo(batch, 12);
        BitConverter.GetBytes(-1).CopyTo(batch, 16);
        return batch;
    }
    public Task SpawnAsync(int id, uint category, int quantity)
    {
        var payload = ItemBatch(id, category, quantity);
        var dispatcher = Ready(GameActionKind.Item);
        var manager = memory.Pointer(session.ImageBase + 0x553D6E0);
        var handles = memory.Pointer(session.ImageBase + 0x553E990);
        return dispatcher.ExecuteAsync(ItemCalls(session.ImageBase, manager, handles, dispatcher.Payload), payload);
    }
    public static NativeCall[] ItemCalls(ulong imageBase, ulong manager, ulong handles, ulong payload) =>
    [
        // The engine allocates equipment instances and encodes goods/accessory handles.
        // All three calls run in one claimed game-thread request. Award retains its
        // own references; release only the temporary batch's ownership afterward.
        new(imageBase + 0x1A86FF0, payload + 4, handles, payload + 8),
        new(imageBase + 0x17D89F0, manager, payload),
        new(imageBase + 0x1A8BC00, payload + 4)
    ];
    public Task WarpAsync(LampChoice lamp)
    {
        if (!Lamps.Any(x => x.Entity == lamp.Entity)) throw new ArgumentException("Choose a supported lamp destination.");
        var dispatcher = Ready(GameActionKind.Warp);
        return dispatcher.ExecuteAsync([
            new(session.ImageBase + 0x156D110, (ulong)lamp.Entity),
            new(session.ImageBase + 0x156D0E0, 1),
            new(session.ImageBase + 0x132E050, 0, (ulong)lamp.Entity)
        ]);
    }
    public Task RestoreLocationAsync(SavedLocation location)
    {
        var payload = location.Payload();
        var current = session.ReadLocation("Current");
        session.ValidateLocationContext(location,current);
        var dispatcher = Ready(GameActionKind.RestoreLocation);
        var world = memory.Pointer(session.ImageBase + 0x553B148);
        return dispatcher.ExecuteAsync([new(session.ImageBase + 0x154B110, world, dispatcher.Payload, dispatcher.Payload + 16, dispatcher.Payload + 32)], payload);
    }
    public void Dispose() { try { ResetMultipliers(); } finally { queue?.Dispose(); queue = null; } }
}
