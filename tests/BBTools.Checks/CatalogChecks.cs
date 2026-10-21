using System.IO;
using System.Text;
using BBTools;
internal static partial class Program
{
    private static void CatalogChecks()
    {
        var fmg=new byte[0x80];fmg[2]=2;BitConverter.GetBytes(1).CopyTo(fmg,12);BitConverter.GetBytes(1).CopyTo(fmg,16);BitConverter.GetBytes(0x38L).CopyTo(fmg,24);BitConverter.GetBytes(7000000).CopyTo(fmg,44);BitConverter.GetBytes(7000000).CopyTo(fmg,48);BitConverter.GetBytes(0x40L).CopyTo(fmg,0x38);Encoding.Unicode.GetBytes("Test weapon\0").CopyTo(fmg,0x40);
        var binder=new byte[0x100];Encoding.ASCII.GetBytes("BND4").CopyTo(binder,0);BitConverter.GetBytes(1).CopyTo(binder,12);BitConverter.GetBytes(0x24L).CopyTo(binder,0x20);BitConverter.GetBytes((long)fmg.Length).CopyTo(binder,0x50);BitConverter.GetBytes(0x80u).CopyTo(binder,0x58);BitConverter.GetBytes(11).CopyTo(binder,0x5C);fmg.CopyTo(binder,0x80);
        var catalog=GameItemCatalog.ParseArchive(binder);Check(catalog.Single().Id==7000000&&catalog[0].Category==0&&catalog[0].Name=="Test weapon","game catalog parses bounded FMG name ranges and category IDs");
        var broken=binder.ToArray();BitConverter.GetBytes(uint.MaxValue).CopyTo(broken,0x58);Reject<IOException>(()=>GameItemCatalog.ParseArchive(broken),"catalog import rejects out-of-file binder offsets");
        broken=binder.ToArray();BitConverter.GetBytes(long.MaxValue).CopyTo(broken,0x80+24);Reject<IOException>(()=>GameItemCatalog.ParseArchive(broken),"catalog import rejects out-of-file FMG pointer tables");
        var folder=Path.Combine(Path.GetTempPath(),"BBTools-catalog-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try{var library=new UserLibrary(Path.Combine(folder,"library.db"),Path.Combine(folder,"absent.json"));library.SaveCatalog(catalog);Check(library.Catalog().Single().Name=="Test weapon","imported game catalog persists in the local SQLite library");}
        finally{Directory.Delete(folder,true);}
        var search = new ItemSearchIndex(new[] {
            new BBTools.Memory.ItemChoice("Saw Cleaver",7000000,0),
            new BBTools.Memory.ItemChoice("Cleaver alias",7000000,0),
            new BBTools.Memory.ItemChoice("Blood Vial",1000,0x40000000),
            new BBTools.Memory.ItemChoice("Shared number",1000,0) });
        Check(search.Search("CLEAVER").Length==1 && search.Search("alias").Single().Name=="Cleaver alias", "cached item search keeps case-insensitive aliases and removes duplicate identities after matching");
        Check(search.Search("1000").Length==2 && search.Search("").Length==3, "cached item search preserves matching IDs in separate categories and empty-query results");
        var controls = new CameraControls();controls.Validate();
        var pressed=new HashSet<int>{0x57,0x44,0x45};var input=controls.Read(pressed.Contains);
        Check(input.Forward==1&&input.Right==1&&input.Up==1&&input.Yaw==0,"default photo controls read WASD and E/Q without a modifier");
        pressed.Add(0x10);Check(controls.Read(pressed.Contains).Speed==4,"photo controls provide a held Shift speed boost");pressed.Remove(0x10);pressed.Add(0x12);Check(controls.Read(pressed.Contains).Speed==.2f,"photo controls provide held Alt precision movement");
        Reject<ArgumentException>(()=>(controls with{Forward="S"}).Validate(),"duplicate photo bindings are rejected");
        Reject<ArgumentException>(()=>(controls with{Forward="LeftShift"}).Validate(),"camera speed modifiers cannot be rebound as movement actions");
        var config=Path.Combine(Path.GetTempPath(),"BBTools-camera-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            var custom=controls with{Forward="I"};new AppSettings(Camera:custom).Save(config);
            Check(AppSettings.Load(config).Camera==custom,"custom camera bindings survive settings save and restart");
            File.WriteAllText(config,"{\"AutoConnect\":false}");Check(AppSettings.Load(config).Camera==null,"legacy settings retain safe default camera controls without enabling photo mode");
        }
        finally{File.Delete(config);}
        var advanced=new AdvancedSettings(true,2,true,.5f,true);advanced.Validate();Reject<ArgumentException>(()=>new AdvancedSettings(Movement:float.NaN).Validate(),"advanced preset rejects invalid movement values");
    }
}
