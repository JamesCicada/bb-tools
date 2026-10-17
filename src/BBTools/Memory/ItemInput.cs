using System.Globalization;

namespace BBTools.Memory;

public static class ItemInput
{
    public static (int Id, uint Category) Parse(string input, uint selectedCategory, bool fullHex)
    {
        var text = input.Trim();
        var hex = fullHex || text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (!uint.TryParse(text, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException("Enter a decimal base ID, or select Wiki hexadecimal ID and paste the full Item ID.");
        var category = hex ? value & 0xF0000000 : selectedCategory;
        var id = hex ? value & 0x0FFFFFFF : value;
        if (id is < 1 or > 0x0FFFFFFE || category is not (0 or 0x10000000 or 0x20000000 or 0x40000000))
            throw new ArgumentException("The item ID has an unsupported category or base ID.");
        return ((int)id, category);
    }
}
