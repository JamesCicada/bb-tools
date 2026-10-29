using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using BBTools.Memory;

namespace BBTools;

public sealed record NamedGemEffect(int Id, string Kind, decimal Value, int Rank, int Category, int SpecialEffect, int AttackSpecialEffect)
{
    public string Description => Translations.Format($"gem.property.{Kind}", Value);
    public string Label => $"{Description} · {Id} · {Translations.Format("gems.rank", Rank)}";
}

public sealed record NaturalGemRecipe(string Key, int Source, int Primary, int[] Secondaries, int[] Shapes,
    int[] Curses, string GeneratorHash, string Origin)
{
    public string Label => Translations.T($"gem.recipe.{Key}");
    public override string ToString() => Label;
    public GemSpec Build(int shape, int secondary, int curse)
    {
        if (!Shapes.Contains(shape) || !Secondaries.Contains(secondary) || !Curses.Contains(curse))
            throw new ArgumentException(Translations.T("gem.invalidNatural"));
        return new(Source, shape, secondary == -1 ? [Primary, curse, -1, -1, -1, -1] : [Primary, secondary, curse, -1, -1, -1]);
    }
    public bool Matches(GemSpec spec) => spec.Source == Source && Shapes.Contains(spec.Shape) &&
        Secondaries.Any(second => Curses.Any(curse => Build(spec.Shape, second, curse).Effects.SequenceEqual(spec.Effects)));
}

// Authored labels + numeric metadata; no FMG text or game archives are bundled.
// Natural recipes are a deliberately bounded audited set, not a validity heuristic.
public static class GemGuide
{
    private sealed record Document(int Version, string Profile, JsonElement[] Effects, NaturalGemRecipe[] Recipes);
    private static readonly Lazy<(Dictionary<int, NamedGemEffect> Effects, NaturalGemRecipe[] Recipes)> data = new(Load);
    public static IReadOnlyDictionary<int, NamedGemEffect> Effects => data.Value.Effects;
    public static IReadOnlyList<NaturalGemRecipe> Recipes => data.Value.Recipes;
    private static (Dictionary<int, NamedGemEffect>, NaturalGemRecipe[]) Load()
    {
        using var resource = typeof(GemGuide).Assembly.GetManifestResourceStream("BBTools.GemGuide.json") ?? throw new IOException("Gem guide is missing.");
        var doc = JsonSerializer.Deserialize<Document>(resource) ?? throw new IOException("Invalid gem guide.");
        if (doc.Version != 1 || doc.Profile != "CUSA03173 1.09" || doc.Effects.Length is < 1 or > 30000 || doc.Recipes.Length is < 1 or > 1000)
            throw new IOException("Unsupported gem guide.");
        var effects = new Dictionary<int, NamedGemEffect>();
        foreach (var row in doc.Effects)
        {
            if (row.GetArrayLength() != 7) throw new IOException("Invalid gem guide row.");
            var effect = new NamedGemEffect(row[0].GetInt32(), row[1].GetString()!, row[2].GetDecimal(), row[3].GetInt32(),
                row[4].GetInt32(), row[5].GetInt32(), row[6].GetInt32());
            if (effect.Id <= 0 || effect.Rank is < 0 or > 100 || !Translations.English.ContainsKey("gem.property." + effect.Kind) ||
                !effects.TryAdd(effect.Id, effect)) throw new IOException("Invalid or duplicate gem guide effect.");
        }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipe in doc.Recipes)
        {
            if (!keys.Add(recipe.Key) || !Translations.English.ContainsKey("gem.recipe." + recipe.Key) || recipe.GeneratorHash.Length != 64 ||
                recipe.Shapes.Length == 0 || recipe.Secondaries.Length == 0 || recipe.Curses.Length == 0 || string.IsNullOrWhiteSpace(recipe.Origin) ||
                recipe.Secondaries.Concat(recipe.Curses).Append(recipe.Primary).Any(id => id != -1 && !effects.ContainsKey(id)))
                throw new IOException("Invalid natural gem recipe.");
            foreach (var shape in recipe.Shapes) foreach (var second in recipe.Secondaries) foreach (var curse in recipe.Curses)
                recipe.Build(shape, second, curse).Validate();
        }
        return (effects, doc.Recipes);
    }
    public static NaturalGemRecipe? Match(GemSpec spec)
    {
        spec.Validate();
        return Recipes.FirstOrDefault(recipe => recipe.Matches(spec));
    }
    public static NaturalGemRecipe RequireNatural(GemSpec spec) => Match(spec) ?? throw new ArgumentException(Translations.T("gem.invalidNatural"));
    public static string Describe(int id) => id == -1 ? Translations.T("gem.none") :
        Effects.TryGetValue(id, out var effect) ? effect.Description : Translations.Format("gem.unknown", id);

    // Recheck on the game thread immediately before publication. A catalog match
    // alone is not proof that the currently loaded parameter profile still agrees.
    internal static void ValidateNaturalLive(GemSpec spec, BloodGems reader, IMemory memory)
    {
        var recipe = RequireNatural(spec);
        var generators = reader.Resolve(false); var effects = reader.Resolve(true);
        if (!generators.Rows.TryGetValue(spec.Source, out var generator) ||
            !Convert.ToHexString(SHA256.HashData(memory.Read(generator, 0x90))).Equals(recipe.GeneratorHash, StringComparison.OrdinalIgnoreCase))
            throw new IOException(Translations.T("gem.profileMismatch"));
        foreach (var id in spec.Effects.Where(id => id != -1))
        {
            var expected = Effects[id];
            if (!effects.Rows.TryGetValue(id, out var row)) throw new IOException(Translations.T("gem.profileMismatch"));
            var bytes = memory.Read(row, 0x24);
            if (BitConverter.ToInt32(bytes) != expected.SpecialEffect || BitConverter.ToInt32(bytes, 4) != expected.Category ||
                BitConverter.ToInt32(bytes, 8) != expected.Rank || BitConverter.ToInt32(bytes, 0x20) != expected.AttackSpecialEffect)
                throw new IOException(Translations.T("gem.profileMismatch"));
        }
    }
}

public sealed record SavedGemBuild(string Name, GemSpec Gem)
{
    public override string ToString() => Name;
    public void Validate() { UserLibrary.ValidateName(Name); if (Gem == null) throw new ArgumentException("Missing gem build."); Gem.Validate(); }
}

public sealed partial class UserLibrary
{
    public SavedGemBuild[] GemBuilds() { var builds = Read<SavedGemBuild>("gemBuild"); foreach (var build in builds) build.Validate(); return builds; }
    public void Save(SavedGemBuild build) { build.Validate(); Save("gemBuild", build.Name, build); }
}
