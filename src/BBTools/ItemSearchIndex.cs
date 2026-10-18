using System.Globalization;
using BBTools.Memory;

namespace BBTools;

// One snapshot per library refresh: typing does not reopen SQLite or deserialize the catalog.
internal sealed class ItemSearchIndex(IEnumerable<ItemChoice> items)
{
    private readonly (ItemChoice Item, string Id)[] entries = items
        .Select(item => (item, item.Id.ToString(CultureInfo.InvariantCulture))).ToArray();

    public ItemChoice[] Search(string query) => entries
        .Where(entry => entry.Item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || entry.Id.Contains(query, StringComparison.Ordinal))
        .DistinctBy(entry => (entry.Item.Id, entry.Item.Category))
        .Select(entry => entry.Item).ToArray();
}
