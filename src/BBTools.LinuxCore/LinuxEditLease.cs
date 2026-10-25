namespace BBTools.LinuxCore;

internal static class LinuxEditLease
{
    public static FileStream Acquire(int pid,string identity)
    {
        if(pid<=0||!ulong.TryParse(identity,out _))throw new ArgumentException("Invalid game identity for editing ownership.");
        var runtime=Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        var folder=runtime!=null&&Path.IsPathFullyQualified(runtime)?Path.Combine(runtime,"bb-tools"):
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"bb-tools","locks");
        Directory.CreateDirectory(folder);
        try{return new FileStream(Path.Combine(folder,$"{pid}-{identity}.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        catch(IOException ex){throw new IOException("Another BB Tools instance may already be editing this game. Close or disconnect it before enabling editing here.",ex);}
        // Leave the empty inode in place: unlinking an active flock would let a
        // second opener acquire a different inode under the same pathname.
    }
}
