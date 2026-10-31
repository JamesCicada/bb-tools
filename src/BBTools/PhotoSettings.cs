using System.IO;
namespace BBTools;

public sealed record PhotoAdjustments(string Filter="Original",float Exposure=0,float Contrast=1,float Saturation=1,float Vignette=0)
{
    public static string[] Filters=>["Original","Noir","Warm","Cool","Sepia"];
    public void Validate()
    {
        if(!Filters.Contains(Filter)||!float.IsFinite(Exposure)||Exposure is <-2 or >2||!float.IsFinite(Contrast)||Contrast is <.5f or >1.5f||
           !float.IsFinite(Saturation)||Saturation is <0 or >2||!float.IsFinite(Vignette)||Vignette is <0 or >.8f)
            throw new ArgumentException("Choose valid photo adjustments.");
    }
}
public sealed record PhotoSettings(string? Folder=null,PhotoAdjustments? Adjustments=null)
{
    public void Validate(){Adjustments?.Validate();if(Folder!=null&&(!Path.IsPathFullyQualified(Folder)||Folder.Length>1024))throw new ArgumentException("Choose a full photo folder path.");}
    public string OutputFolder=>Folder??Path.Combine(AppContext.BaseDirectory,"photos");
}
