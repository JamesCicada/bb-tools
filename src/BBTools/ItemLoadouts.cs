using System.IO;
using BBTools.Memory;

namespace BBTools;

// A named set of item grants spawned one after another with a single click.
// Each entry uses the same validated item request as a single spawn.
public sealed record LoadoutItem(string Name, int Id, uint Category, int Quantity)
{
    public string Label => $"{Name} · {Id} ×{Quantity}";
    public override string ToString() => Label;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 256) throw new ArgumentException("Each loadout item needs a name of 1–256 characters.");
        GameActions.ItemBatch(Id, Category, Quantity);
    }
}

public sealed record ItemLoadout(string Name, LoadoutItem[] Items, string Group = "General")
{
    public const int MaxItems = 50;
    public override string ToString() => $"{Group} · {Name} ({Items?.Length ?? 0})";
    public void Validate()
    {
        UserLibrary.ValidateName(Name); UserLibrary.ValidateName(Group);
        if (Items == null || Items.Length is < 1 or > MaxItems || Items.Any(x => x == null))
            throw new ArgumentException($"A loadout needs 1–{MaxItems} items.");
        foreach (var item in Items) item.Validate();
    }
    public ItemLoadout With(LoadoutItem item)
    {
        item.Validate();
        if (Items.Length >= MaxItems) throw new ArgumentException($"A loadout holds at most {MaxItems} items.");
        return this with { Items = [.. Items, item] };
    }
    public ItemLoadout Without(int index)
    {
        if (index < 0 || index >= Items.Length) throw new ArgumentException("Choose a loadout item first.");
        return this with { Items = [.. Items.Take(index), .. Items.Skip(index + 1)] };
    }
}

public sealed class LoadoutSpawnException(int spawned, int total, LoadoutItem failed, Exception inner)
    : IOException($"Loadout stopped after {spawned} of {total} items. \"{failed.Name}\" failed: {inner.Message} Items already granted were not repeated.", inner)
{
    public int Spawned { get; } = spawned;
}

public static class LoadoutSpawner
{
    // Spawns in order and stops at the first failure. It never retries, so a
    // timed-out grant that the game later completes cannot be duplicated.
    public static async Task<int> SpawnAsync(ItemLoadout loadout, Func<LoadoutItem, Task> spawn,
        Action<int, int, LoadoutItem>? progress = null, CancellationToken cancellationToken = default)
    {
        loadout.Validate();
        for (var i = 0; i < loadout.Items.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = loadout.Items[i];
            progress?.Invoke(i, loadout.Items.Length, item);
            try { await spawn(item); }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
            { throw new LoadoutSpawnException(i, loadout.Items.Length, item, ex); }
        }
        return loadout.Items.Length;
    }
}

public sealed partial class UserLibrary
{
    public ItemLoadout[] Loadouts() { var all = Read<ItemLoadout>("loadout"); foreach (var x in all) x.Validate(); return all; }
    public void Save(ItemLoadout loadout) { loadout.Validate(); Save("loadout", loadout.Name, loadout); }
}
