using System.IO;

namespace BBTools.Memory;

public sealed record ItemChoice(string Name, int Id, uint Category = 0x40000000)
{
    public string Label => Name;
    public override string ToString() => Label;
}
public sealed record LampChoice(string Name, int Entity)
{
    public string Label => Name;
    public override string ToString() => Label;
}

public sealed class GameActions(ProcessMemory memory, PlayerSession session) : IDisposable
{
    private GameThreadQueue? queue;
    public static readonly (ulong Offset, byte[] Bytes)[] Fingerprints =
    [
        (0x18FD960, Convert.FromHexString("488B87C0030000C30F1F840000000000")),
        (0x18F7891, Convert.FromHexString("498B06FF90C8010000")),
        (0x17D89F0, Convert.FromHexString("554889E54157415641554154534881EC")),
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
    public static LampChoice[] Lamps =>
    [
        new("Hunter's Dream",2102950), new("1st Floor Sickroom",2412950), new("Central Yharnam",2412951),
        new("Great Bridge",2412952), new("Tomb of Oedon",2412953), new("Cathedral Ward",2402950),
        new("Grand Cathedral",2402951), new("Old Yharnam",2302950), new("Church of the Good Chalice",2302951),
        new("Graveyard of the Darkbeast",2302952), new("Hemwick Charnel Lane",2202950), new("Witch's Abode",2202951),
        new("Forbidden Woods",2702950), new("Forbidden Grave",2702951), new("Byrgenwerth",3202950),
        new("Moonside Lake",3202951), new("Abandoned Old Workshop",2112950), new("Hunter's Nightmare",3402950)
    ];
    private GameThreadQueue Ready()
    {
        session.ValidateImage();
        foreach (var f in Fingerprints)
            if (!memory.Read(session.ImageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes))
                throw new IOException("Native item/warp instructions differ from the supported 1.09 build.");
        var player = session.ReadPlayer();
        if (player.Health == 0) throw new IOException("Load a living character before submitting a game action.");
        if (queue != null && queue.Actor != player.Actor) { queue.Dispose(); queue = null; }
        return queue ??= new GameThreadQueue(memory, session);
    }
    public static byte[] ItemBatch(int id, uint category, int quantity)
    {
        if (id is < 1 or > 0x0FFFFFFE || category is not (0 or 0x10000000 or 0x20000000 or 0x40000000))
            throw new ArgumentException("Enter a valid base item ID and category.");
        if (quantity is < 1 or > 99 || category != 0x40000000 && quantity != 1)
            throw new ArgumentException("Quantity must be 1–99 for goods, or 1 for equipment.");
        var batch = new byte[20];
        BitConverter.GetBytes(1).CopyTo(batch, 0);
        BitConverter.GetBytes(category | (uint)id).CopyTo(batch, 4);
        BitConverter.GetBytes(quantity).CopyTo(batch, 8);
        BitConverter.GetBytes(0).CopyTo(batch, 12);
        BitConverter.GetBytes(-1).CopyTo(batch, 16);
        return batch;
    }
    public Task SpawnAsync(int id, uint category, int quantity)
    {
        var payload = ItemBatch(id, category, quantity);
        var dispatcher = Ready();
        var manager = memory.Pointer(session.ImageBase + 0x553D6E0);
        return dispatcher.ExecuteAsync([new(session.ImageBase + 0x17D89F0, manager, dispatcher.Payload)], payload);
    }
    public Task WarpAsync(LampChoice lamp)
    {
        if (!Lamps.Any(x => x.Entity == lamp.Entity)) throw new ArgumentException("Choose a supported lamp destination.");
        var dispatcher = Ready();
        return dispatcher.ExecuteAsync([
            new(session.ImageBase + 0x156D110, (ulong)lamp.Entity),
            new(session.ImageBase + 0x156D0E0, 1),
            new(session.ImageBase + 0x132E050, 0, (ulong)lamp.Entity)
        ]);
    }
    public void Dispose() { queue?.Dispose(); queue = null; }
}
