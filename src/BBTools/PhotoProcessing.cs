using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BBTools;

internal sealed partial record PhotoImage
{
    public BitmapSource Bitmap()
    {
        Validate();var bitmap=BitmapSource.Create(Width,Height,96,96,PixelFormats.Bgra32,null,Pixels,Width*4);bitmap.Freeze();return bitmap;
    }
    public string SavePng(string folder,string suffix)
    {
        Validate();Directory.CreateDirectory(folder);var path=Path.Combine(folder,$"BBTools-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}-{suffix}.png");
        var temporary=path+".tmp";
        try
        {
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Bitmap()));
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))encoder.Save(file);
            File.Move(temporary,path);return path;
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
