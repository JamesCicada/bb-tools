using System.IO;
using System.IO.Compression;
namespace BBTools;

public sealed record SaveBackup(string Path,DateTime ModifiedUtc,long Size)
{
    public override string ToString()=>$"{System.IO.Path.GetFileName(Path)} · {ModifiedUtc.ToLocalTime():g} · {Size/1024:N0} KB";
}
public sealed record SaveRestoreResult(string RecoveryFolder,string RecoveryZip);
public static class SaveRestore
{
    public static string CreateBackup(string source,string destination)
    {
        source=Path.GetFullPath(source);destination=Path.GetFullPath(destination);
        var relative=Path.GetRelativePath(source,destination);
        if(relative=="." || !Path.IsPathRooted(relative) && relative!=".." && !relative.StartsWith(".."+Path.DirectorySeparatorChar))
            throw new ArgumentException(Translations.T("message.Backupdestinationmustbeoutsidethesavefolder.f824ef4"));
        ValidateFolder(new DirectoryInfo(source));Directory.CreateDirectory(destination);
        var archive=Path.Combine(destination,$"bb-tools-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");var temporary=archive+".partial";
        try{System.IO.Compression.ZipFile.CreateFromDirectory(source,temporary);File.Move(temporary,archive);return archive;}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    private const long Limit=512L*1024*1024;
    private static readonly uint[] CrcTable=Enumerable.Range(0,256).Select(x=>
    {
        var value=(uint)x;for(var i=0;i<8;i++)value=(value&1)!=0?0xEDB88320^(value>>1):value>>1;return value;
    }).ToArray();
    public static SaveBackup[] History(string folder)=>new DirectoryInfo(folder).EnumerateFiles("bb-tools-*.zip")
        .Where(x=>(x.Attributes&FileAttributes.ReparsePoint)==0).OrderByDescending(x=>x.LastWriteTimeUtc).Select(x=>new SaveBackup(x.FullName,x.LastWriteTimeUtc,x.Length)).ToArray();
    private sealed class FolderBudget { public long Bytes;public int Files; }
    private static void ValidateFolder(DirectoryInfo folder,int depth=0,FolderBudget? budget=null)
    {
        budget??=new();
        if(depth>64 || (folder.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Links, junctions and excessive folder depth are unsupported in save folders.");
        foreach(var entry in folder.EnumerateFileSystemInfos())
        {
            if((entry.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Links and junctions are unsupported in save folders.");
            if(entry is DirectoryInfo child)ValidateFolder(child,depth+1,budget);
            else if(entry is FileInfo file){budget.Bytes+=file.Length;budget.Files++;if(budget.Bytes>Limit||budget.Files>10000)throw new IOException("Selected save folder exceeds restore limits. Choose the actual save folder, not the installation.");}
        }
    }
    public static SaveRestoreResult Restore(string archive,string target,Func<bool> gameClosed)
    {
        if(!gameClosed())throw new IOException("Close Bloodborne and its launcher before restoring saves.");
        target=System.IO.Path.GetFullPath(target).TrimEnd(System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar);
        var destination=new DirectoryInfo(target);
        if(!destination.Exists || destination.Parent==null || target.Equals(System.IO.Path.GetPathRoot(target)?.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Choose an existing save folder, not a drive root.");
        var prefix=target+System.IO.Path.DirectorySeparatorChar;
        if(System.IO.Path.GetFullPath(archive).StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("The backup archive must be outside the save folder.");
        ValidateFolder(destination);
        var parent=destination.Parent.FullName;var token=Guid.NewGuid().ToString("N");var stage=System.IO.Path.Combine(parent,".bb-tools-restore-"+token);var recovery=System.IO.Path.Combine(parent,".bb-tools-recovery-"+token);
        Directory.CreateDirectory(stage);
        try
        {
            using(var zip=ZipFile.OpenRead(archive))
            {
                if(zip.Entries.Count is <1 or >10000 || zip.Entries.Any(x=>x.Length>Limit) || zip.Entries.Sum(x=>x.Length)>Limit)throw new IOException("Backup archive is empty or exceeds restore limits.");
                var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var files=0;long actualTotal=0;var buffer=new byte[81920];
                foreach(var entry in zip.Entries)
                {
                    var name=entry.FullName.Replace('\\','/');var directory=name.EndsWith('/');var parts=name.TrimEnd('/').Split('/');
                    if(name.StartsWith('/') || parts.Length>64 || parts.Any(x=>x is "" or "." or ".." || x.EndsWith('.') || x.EndsWith(' ') || x.IndexOfAny(System.IO.Path.GetInvalidFileNameChars())>=0 || System.Text.RegularExpressions.Regex.IsMatch(x.Split('.')[0],@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",System.Text.RegularExpressions.RegexOptions.IgnoreCase)) ||
                        (entry.ExternalAttributes&0x400)!=0 || ((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new IOException("Backup contains an unsafe entry path or link.");
                    var path=System.IO.Path.GetFullPath(System.IO.Path.Combine(stage,System.IO.Path.Combine(parts)));
                    if(!path.StartsWith(stage+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!names.Add(path))throw new IOException("Backup contains duplicate or escaping paths.");
                    if(directory){Directory.CreateDirectory(path);continue;}
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);files++;
                    using var input=entry.Open();using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write);long count=0;uint crc=uint.MaxValue;int read;
                    while((read=input.Read(buffer,0,buffer.Length))>0)
                    {
                        count+=read;actualTotal+=read;if(count>entry.Length || actualTotal>Limit)throw new IOException("Backup expansion exceeds its declared size.");
                        for(var i=0;i<read;i++)crc=CrcTable[(crc^buffer[i])&255]^(crc>>8);output.Write(buffer,0,read);
                    }
                    if(count!=entry.Length || ~crc!=entry.Crc32)throw new IOException("Backup checksum or size verification failed.");
                    output.Dispose();File.SetLastWriteTimeUtc(path,entry.LastWriteTime.UtcDateTime);
                }
                if(files==0)throw new IOException("Backup contains no save files.");
            }
            if(!gameClosed())throw new IOException("Bloodborne started during verification; save restoration was cancelled.");
            ValidateFolder(destination);
            var recoveryZip=CreateBackup(target,parent);
            if(!gameClosed())throw new IOException("Bloodborne started during backup; save restoration was cancelled.");
            Directory.Move(target,recovery);
            try{Directory.Move(stage,target);}
            catch(Exception error)
            {
                if(!Directory.Exists(target))
                {
                    try{Directory.Move(recovery,target);}
                    catch(Exception rollback){throw new IOException($"Restore failed; original saves remain at {recovery}. Recovery ZIP: {recoveryZip}",new AggregateException(error,rollback));}
                    throw new IOException("Restore failed; the original save folder was put back.",error);
                }
                throw new IOException($"Restore interrupted; original saves remain at {recovery}. Recovery ZIP: {recoveryZip}",error);
            }
            return new(recovery,recoveryZip);
        }
        finally
        {
            // Only this operation's uniquely named staging directory is removed.
            var resolved=System.IO.Path.GetFullPath(stage);
            if(System.IO.Path.GetDirectoryName(resolved)==parent && System.IO.Path.GetFileName(resolved)==".bb-tools-restore-"+token && Directory.Exists(resolved))Directory.Delete(resolved,true);
        }
    }
}
