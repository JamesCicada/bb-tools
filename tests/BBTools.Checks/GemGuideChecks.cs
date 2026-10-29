using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using BBTools;
using BBTools.Memory;

internal static partial class Program
{
    private static void GemGuideChecks()
    {
        Check(GemGuide.Effects.Count == 12487 && GemGuide.Recipes.Count == 4, "standalone gem guide loads named numeric metadata and four documented families without game files");
        var heavy = GemGuide.Recipes.Single(x => x.Key == "heavy");
        var spec = heavy.Build(1, 62120, 41218);
        Check(spec.Source == 1053421 && spec.Effects.SequenceEqual(new[] {129520,62120,41218,-1,-1,-1}) && GemGuide.Match(spec) == heavy,
            "Heavy Abyssal recipe retains Strength +65, physical +15 and one selected native curse");
        Check(GemGuide.Effects[129520].Value == 65 && GemGuide.Effects[126619].Value == 27.2m && GemGuide.Effects[41218].Value == 3.2m,
            "built-in gem metadata distinguishes scaling, percentage damage and stamina curse values");
        Check(heavy.ToString()==heavy.Label && !heavy.ToString().Contains("System.Int32"), "styled recipe picker displays a readable gem name rather than a technical record dump");
        foreach (var recipe in GemGuide.Recipes)
        {
            Check(recipe.Shapes.All(shape => recipe.Secondaries.All(second => recipe.Curses.All(curse => GemGuide.Match(recipe.Build(shape, second, curse)) == recipe))),
                $"all offered {recipe.Key} combinations are recognized as a complete supported recipe");
        }
        Reject<ArgumentException>(() => heavy.Build(63,62120,41218), "Normal does not invent a droplet Heavy recipe from individual valid IDs");
        Reject<ArgumentException>(() => heavy.Build(1,126619,41218), "Normal rejects an individually valid but incompatible secondary property");
        Reject<ArgumentException>(() => heavy.Build(1,62120,-1), "Normal cursed gems cannot silently omit their curse");
        var custom = spec with { Effects = [129520,129520,129520,129520,129520,129520] };
        custom.Validate(); Check(GemGuide.Match(custom) == null, "six valid powerful effects remain Advanced rather than being mislabeled naturally obtainable");
        Reject<ArgumentException>(() => GemGuide.RequireNatural(custom), "creation-time Normal validation rejects custom effects independently of the UI");
        Check(GemGuide.Match(spec with { Source = 4 }) == null, "identical effects with a different generator do not bypass the complete recipe check");
        foreach (var locale in new[] { "en", "fr", "ar", "zh-Hans" })
        {
            Translations.Select(locale);
            Check(GemGuide.Effects.Values.All(x => !string.IsNullOrWhiteSpace(x.Description) && !x.Description.StartsWith("gem.property.")) &&
                GemGuide.Recipes.All(x => !x.Label.StartsWith("gem.recipe.")), $"built-in gem labels and recipes resolve in {locale}");
        }
        Translations.Select("en");
        var folder = Path.Combine(Path.GetTempPath(), "BBTools-gem-guide-" + Guid.NewGuid().ToString("N"));
        try
        {
            var lib = new UserLibrary(Path.Combine(folder,"source.db"),Path.Combine(folder,"none.json"));
            lib.Save(new SavedGemBuild("Heavy",spec)); lib.Save(new SavedGemBuild("Custom",custom));
            var path = Path.Combine(folder,"export.json"); lib.Export(path);
            var imported = new UserLibrary(Path.Combine(folder,"target.db"),Path.Combine(folder,"none.json")); imported.Import(path);
            Check(imported.GemBuilds().Length == 2 && imported.GemBuilds().Single(x=>x.Name=="Custom").Gem.Effects.SequenceEqual(custom.Effects),
                "SQLite and library export/import preserve custom gem builds without activating a mode or granting an item");
            File.WriteAllText(path,JsonSerializer.Serialize(new LibraryDocument(1,[],[],[]))); imported.Import(path);
            Check(imported.GemBuilds().Length == 2, "older library exports without gem builds still import without deleting existing builds");
            lib.Delete("gemBuild","Custom"); Check(lib.GemBuilds().Single().Name=="Heavy", "saved gem builds can be deleted independently of game inventory");
            Reject<ArgumentException>(() => lib.Save(new SavedGemBuild("Bad",spec with {Effects=[]})), "malformed saved builds are rejected before database changes");
        }
        finally { if(Directory.Exists(folder))Directory.Delete(folder,true); }
    }

    private static void GemEditorUiChecks(MainWindow window)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var advanced=(CheckBox)window.FindName("GemAdvanced");
        var normal=(StackPanel)window.FindName("GemNormalPanel");
        var raw=(StackPanel)window.FindName("GemAdvancedPanel");
        var recipes=(ComboBox)window.FindName("GemRecipePicker");
        Check(advanced.IsChecked!=true && normal.Visibility==Visibility.Visible && raw.Visibility==Visibility.Collapsed && recipes.IsEnabled,
            "gem browsing is available while disconnected and custom editing defaults hidden");
        Check((bool)typeof(MainWindow).GetField("pendingCustomNormal",flags)!.GetValue(window)!,
            "loading an unrecognized carried gem blocks Normal creation without enabling Advanced");
        recipes.SelectedIndex=1; recipes.SelectedIndex=0;
        var preview=(TextBlock)window.FindName("GemPreview");
        Check(preview.Text.Contains("65")&&preview.Text.Contains("15")&&preview.Text.Contains("3.2"), "Normal preview lists Heavy primary, secondary and curse without raw-ID setup");
        advanced.IsChecked=true;
        Check(raw.Visibility==Visibility.Visible && normal.Visibility==Visibility.Collapsed && preview.Text.Contains("Advanced"),
            "explicit Advanced opt-in reveals the custom fields and labels the preview as custom");
        advanced.IsChecked=false;
        var spec=(GemSpec)typeof(MainWindow).GetMethod("CurrentGem",flags)!.Invoke(window,null)!;
        Check(GemGuide.Match(spec)!=null && raw.Visibility==Visibility.Collapsed, "returning to Normal uses a documented recipe instead of reclassifying the custom build");
    }

    private static void NaturalGemProfileChecks(ProcessMemory memory, BloodGems reader, ulong generators, ulong effects)
    {
        var spec=GemGuide.Recipes.Single(x=>x.Key=="heavy").Build(1,62120,41218);
        void Int(ulong at,int value)=>memory.Write(at,BitConverter.GetBytes(value));
        memory.Write(generators+10,BitConverter.GetBytes((ushort)1)); Int(generators+0x40,spec.Source);
        var generator=new byte[0x90];
        new float[]{100,100,100,10,-1,1}.SelectMany(BitConverter.GetBytes).ToArray().CopyTo(generator,16);
        generator[40]=3;generator[140]=2;
        for(var slot=0;slot<6;slot++)
        {
            BitConverter.GetBytes(slot==0?2:slot<3?4:0).CopyTo(generator,44+slot*16);
            BitConverter.GetBytes(slot switch{0=>201500,1=>1,2=>102,_=>-1}).CopyTo(generator,48+slot*16);
            BitConverter.GetBytes(slot<3?100f:0f).CopyTo(generator,52+slot*16);
        }
        memory.Write(generators+0x100,generator);
        memory.Write(effects+10,BitConverter.GetBytes((ushort)3));
        var ids=spec.Effects.Where(x=>x!=-1).ToArray();
        for(var i=0;i<ids.Length;i++)
        {
            var entry=effects+0x40+(ulong)i*24;var row=effects+0x100+(ulong)i*0x24;
            Int(entry,ids[i]);memory.Write(entry+8,BitConverter.GetBytes(0x100UL+(ulong)i*0x24));
            var fact=GemGuide.Effects[ids[i]];Int(row,fact.SpecialEffect);Int(row+4,fact.Category);Int(row+8,fact.Rank);Int(row+0x20,fact.AttackSpecialEffect);
        }
        GemGuide.ValidateNaturalLive(spec,reader,memory);
        Check(true,"Normal creation validates the complete stock generator profile and selected native effect metadata before publication");
        Int(generators+0x100+48,171500);
        Reject<IOException>(()=>GemGuide.ValidateNaturalLive(spec,reader,memory),"changed generator pools cannot retain the stock Normal-recipe label");
        memory.Write(generators+0x100,generator);
        Int(effects+0x104,999);
        Reject<IOException>(()=>GemGuide.ValidateNaturalLive(spec,reader,memory),"changed effect category is rejected even with an existing, valid numeric ID");
    }
}
