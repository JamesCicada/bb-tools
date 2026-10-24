using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BBTools;

public sealed record AppLanguage(string Code, string NativeName)
{
    public string DisplayName => $"{NativeName} — {Translations.Coverage(Translations.Resolve(Code))}%";
    public override string ToString() => DisplayName;
}

// Plain .NET/JSON catalogs can also serve a future Linux desktop UI.
public static class Translations
{
    public static readonly AppLanguage[] Languages =
    [
        new("en", "English"), new("zh-Hans", "简体中文"), new("zh-Hant", "繁體中文"),
        new("ja", "日本語"), new("ko", "한국어"), new("fr", "Français"), new("de", "Deutsch"),
        new("es", "Español"), new("pt-BR", "Português (Brasil)"), new("it", "Italiano"),
        new("ru", "Русский"), new("pl", "Polski"), new("tr", "Türkçe"), new("uk", "Українська"),
        new("ar", "العربية"), new("ca", "Català"), new("cs", "Čeština"), new("da", "Dansk"),
        new("nl", "Nederlands"), new("fi", "Suomi"), new("el", "Ελληνικά"), new("hu", "Magyar"),
        new("nb", "Norsk bokmål"), new("pt-PT", "Português (Portugal)"), new("ro", "Română"),
        new("sr-Cyrl", "Српски (ћирилица)"), new("sv", "Svenska"), new("vi", "Tiếng Việt")
    ];
    private static readonly Dictionary<string, string> english = Load("en");
    private static Dictionary<string, string> current = english;
    public static string CurrentLanguage { get; private set; } = "en";
    public static IReadOnlyDictionary<string, string> English => english;
    public static bool IsSupported(string code) => code == "auto" || Languages.Any(l => l.Code == code);
    public static string Resolve(string requested, string? systemLanguage = null)
    {
        if (requested != "auto") return Languages.Any(l => l.Code == requested) ? requested : "en";
        var name = systemLanguage ?? CultureInfo.InstalledUICulture.Name;
        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return name.Contains("Hant", StringComparison.OrdinalIgnoreCase) || new[] { "zh-TW", "zh-HK", "zh-MO" }.Contains(name, StringComparer.OrdinalIgnoreCase) ? "zh-Hant" : "zh-Hans";
        var exact = Languages.FirstOrDefault(l => l.Code.Equals(name, StringComparison.OrdinalIgnoreCase));
        return exact?.Code ?? Languages.FirstOrDefault(l => l.Code.Split('-')[0].Equals(name.Split('-')[0], StringComparison.OrdinalIgnoreCase))?.Code ?? "en";
    }
    private static Dictionary<string, string> Load(string code)
    {
        using var resource = typeof(Translations).Assembly.GetManifestResourceStream($"BBTools.Locales.{code}.json");
        return resource == null ? new(StringComparer.Ordinal) : JsonSerializer.Deserialize<Dictionary<string, string>>(resource) ?? new(StringComparer.Ordinal);
    }
    internal static bool Compatible(string source, string translated)
    {
        try
        {
            _ = System.Text.CompositeFormat.Parse(translated);
            static string[] Slots(string value) => Regex.Matches(value.Replace("{{", "").Replace("}}", ""), @"\{(\d+)(?:,[^}:]+)?(?::[^}]+)?\}")
                .Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal).ToArray();
            return Slots(source).SequenceEqual(Slots(translated));
        }
        catch (FormatException) { return false; }
    }
    public static void Select(string requested)
    {
        var code = Resolve(requested);
        var next = new Dictionary<string, string>(english, StringComparer.Ordinal);
        foreach (var (key, value) in Load(code))
            if (english.TryGetValue(key, out var source) && !string.IsNullOrWhiteSpace(value) && Compatible(source, value)) next[key] = value;
        current = next;
        CurrentLanguage = code;
        // Number input/formatting culture stays unchanged.
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(code);
    }
    public static string T(string key) => current.GetValueOrDefault(key) ?? english.GetValueOrDefault(key) ?? key;
    public static string Format(string key, params object?[] arguments)
    {
        try { return string.Format(CultureInfo.CurrentCulture, T(key), arguments); }
        catch (FormatException) when (english.ContainsKey(key)) { return string.Format(CultureInfo.CurrentCulture, english[key], arguments); }
    }
    public static string Source(string source) => T(english.FirstOrDefault(p => p.Value == source).Key ?? source);
    public static int Coverage(string code)
    {
        var values = Load(code);
        return english.Count == 0 ? 0 : (int)Math.Round(100.0 * english.Count(p => values.TryGetValue(p.Key, out var value) && !string.IsNullOrWhiteSpace(value) && Compatible(p.Value, value)) / english.Count);
    }
}
