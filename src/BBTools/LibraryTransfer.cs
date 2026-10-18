using System.IO;
using System.Text.Json;
using BBTools.Memory;
using Microsoft.Data.Sqlite;
namespace BBTools;

public sealed record FavoriteItem(string Name,int Id,uint Category,int Quantity,string Group="General")
{
    public override string ToString()=>$"{Group} · {Name}";
    public void Validate(){UserLibrary.ValidateName(Name);UserLibrary.ValidateName(Group);GameActions.ItemBatch(Id,Category,Quantity);}
}
public sealed record LibraryDocument(int Version,PracticePreset[] Presets,SavedLocation[] Locations,FavoriteItem[] Favorites);
public sealed partial class UserLibrary
{
    public FavoriteItem[] Favorites(){var all=Read<FavoriteItem>("favorite");foreach(var x in all)x.Validate();return all;}
    public void Save(FavoriteItem item){item.Validate();Save("favorite",item.Name,item);}
    public void Export(string destination)
    {
        if(Path.GetFullPath(destination).Equals(Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Do not overwrite the active database with an export.");
        var text=JsonSerializer.Serialize(new LibraryDocument(1,Presets(),Locations(),Favorites()),new JsonSerializerOptions{WriteIndented=true});
        var temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporary,text);File.Move(temporary,destination,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public void Import(string source)
    {
        using var file=File.OpenRead(source);using var reader=new BinaryReader(file);
        var bytes=reader.ReadBytes(4*1024*1024+1);if(bytes.Length>4*1024*1024)throw new ArgumentException("Library imports must be at most 4 MB.");
        var document=JsonSerializer.Deserialize<LibraryDocument>(bytes)??throw new ArgumentException("Invalid library export.");
        if(document.Version!=1 || document.Presets==null || document.Locations==null || document.Favorites==null ||
            document.Presets.Length>1000 || document.Locations.Length>1000 || document.Favorites.Length>1000 ||
            document.Presets.Any(x=>x==null) || document.Locations.Any(x=>x==null) || document.Favorites.Any(x=>x==null))throw new ArgumentException("Unsupported library export version or size.");
        foreach(var x in document.Presets)x.Validate();foreach(var x in document.Locations){x.Validate();ValidateName(x.Name);if(x.Block>>24==29)throw new ArgumentException("Generated chalice coordinate imports require instance identity and are not supported.");}foreach(var x in document.Favorites)x.Validate();
        using var db=Open();using var tx=db.BeginTransaction();
        void Insert<T>(string kind,IEnumerable<T> items,Func<T,string> name)
        {
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in items)
            {
                if(!names.Add(name(item)))throw new ArgumentException("Import contains duplicate names.");
                using var cmd=db.CreateCommand();cmd.Transaction=tx;
                cmd.CommandText="INSERT INTO entries VALUES($kind,$name,$data) ON CONFLICT(kind,name) DO UPDATE SET name=excluded.name,data=excluded.data";
                cmd.Parameters.AddWithValue("$kind",kind);cmd.Parameters.AddWithValue("$name",name(item));cmd.Parameters.AddWithValue("$data",JsonSerializer.Serialize(item));cmd.ExecuteNonQuery();
            }
        }
        Insert("preset",document.Presets,x=>x.Name);Insert("location",document.Locations,x=>x.Name);Insert("favorite",document.Favorites,x=>x.Name);tx.Commit();
    }
    public void Backup(string destination)
    {
        if(Path.GetFullPath(destination).Equals(Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Choose a different backup file.");
        using var source=Open();using var target=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=destination,Pooling=false}.ToString());target.Open();source.BackupDatabase(target);
    }
    public void RenamePreset(string oldName,string newName)
    {
        ValidateName(newName);var preset=Presets().Single(x=>x.Name.Equals(oldName,StringComparison.OrdinalIgnoreCase));
        if(Presets().Any(x=>!x.Name.Equals(oldName,StringComparison.OrdinalIgnoreCase)&&x.Name.Equals(newName,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("A preset already has this name.");
        using var db=Open();using var cmd=db.CreateCommand();
        cmd.CommandText="UPDATE entries SET name=$new,data=$data WHERE kind='preset' AND name=$old";
        cmd.Parameters.AddWithValue("$new",newName);cmd.Parameters.AddWithValue("$old",oldName);cmd.Parameters.AddWithValue("$data",JsonSerializer.Serialize(preset with{Name=newName}));cmd.ExecuteNonQuery();
    }
}
