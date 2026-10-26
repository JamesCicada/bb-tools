using System.IO;
using BBTools.Memory;

namespace BBTools;

public static class GemTextCatalog
{
    public static ItemChoice[] Load(string gameFolder, string language = "engus")
    {
        if (language.Length is < 2 or > 12 || language.Any(c => !char.IsAsciiLetter(c))) throw new ArgumentException("Invalid message language.");
        var root = Directory.Exists(Path.Combine(gameFolder, "dvdroot_ps4")) ? Path.Combine(gameFolder, "dvdroot_ps4") : gameFolder;
        var file = Path.Combine(root, "msg", language, "menu.msgbnd.dcx");
        using var input = File.OpenRead(file);
        if (input.Length > 64 * 1024 * 1024) throw new IOException("Gem message archive is too large.");
        using var data = new MemoryStream(); input.CopyTo(data);
        return GameItemCatalog.ParseTables(data.ToArray(), new Dictionary<int, uint> { [35] = 0 });
    }
}

public sealed partial class UserLibrary
{
    public ItemChoice[] GemTexts() => Read<ItemChoice[]>("gemTexts").SingleOrDefault() ?? [];
    public void SaveGemTexts(ItemChoice[] entries)
    {
        if (entries.Length is < 1 or > 30000 || entries.Any(x => x.Id < 0 || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 2048) || entries.DistinctBy(x => x.Id).Count() != entries.Length)
            throw new ArgumentException("Invalid gem effect catalog.");
        Save("gemTexts", "effects", entries);
    }
}
