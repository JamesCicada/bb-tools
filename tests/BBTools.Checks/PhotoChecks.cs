using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BBTools;

internal static partial class Program
{
    private static void PhotoChecks()
    {
        var pixels=Enumerable.Range(0,25).SelectMany(_=>new byte[]{60,100,180,255}).ToArray();var original=new PhotoImage(5,5,pixels);
        var neutral=PhotoProcessing.Apply(original,new());
        Check(neutral.Pixels.SequenceEqual(pixels)&&!ReferenceEquals(neutral.Pixels,pixels),"neutral photo processing preserves every original pixel without sharing its mutable buffer");
        var noir=PhotoProcessing.Apply(original,new(Filter:"Noir"));
        Check(noir.Pixels[0]==noir.Pixels[1]&&noir.Pixels[1]==noir.Pixels[2]&&noir.Pixels[3]==255,"Noir photo filter produces grayscale while preserving opacity");
        var brighter=PhotoProcessing.Apply(original,new(Exposure:1));var darker=PhotoProcessing.Apply(original,new(Exposure:-1));
        Check(brighter.Pixels[1]>pixels[1]&&darker.Pixels[1]<pixels[1],"photo exposure changes brightness in the expected direction without changing dimensions");
        var flat=new PhotoImage(5,5,Enumerable.Range(0,25).SelectMany(_=>new byte[]{100,100,100,255}).ToArray());var vignette=PhotoProcessing.Apply(flat,new(Vignette:.8f));
        Check(vignette.Pixels[0]<vignette.Pixels[48]&&vignette.Pixels[48]==100,"photo vignette darkens edges while retaining the image center");
        foreach(var filter in PhotoAdjustments.Filters){var result=PhotoProcessing.Apply(original,new(Filter:filter));Check(result.Width==5&&result.Height==5&&original.Pixels.SequenceEqual(pixels),$"{filter} photo filter preserves source pixels and resolution");}
        Reject<ArgumentException>(()=>PhotoProcessing.Apply(original,new(Exposure:float.NaN)),"photo filters reject non-finite adjustment values");
        Reject<ArgumentException>(()=>PhotoProcessing.Apply(original,new(Filter:"Unknown")),"unknown photo filters are rejected");
        Reject<ArgumentException>(()=>PhotoImage.ByteCount(16384,16384),"oversized photo buffers are rejected before allocation");
        Reject<ArgumentException>(()=>new PhotoImage(5,5,[1,2]).Validate(),"photo dimensions and pixel buffer must agree");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();Reject<OperationCanceledException>(()=>PhotoProcessing.Apply(original,new(Filter:"Noir"),cancellation.Token),"superseded photo previews can cancel processing");}
        var folder=Path.Combine(Path.GetTempPath(),"BBTools-photos-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            var path=Task.Run(()=>original.SavePng(folder,"original")).GetAwaiter().GetResult();
            using var file=File.OpenRead(path);var decoder=new PngBitmapDecoder(file,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var image=new FormatConvertedBitmap(decoder.Frames[0],PixelFormats.Bgra32,null,0);var roundtrip=new byte[pixels.Length];image.CopyPixels(roundtrip,20,0);
            Check(image.PixelWidth==5&&image.PixelHeight==5&&roundtrip.SequenceEqual(pixels),"lossless PNG export preserves exact dimensions and decoded pixels from a worker thread");
            var edited=PhotoProcessing.Apply(original,new(Filter:"Noir")).SavePng(folder,"edited");
            Check(edited!=path&&File.Exists(path)&&Directory.GetFiles(folder,"*.tmp").Length==0,"filtered export creates a separate file and leaves the original intact without temporary sidecars");
            var config=Path.Combine(folder,"settings.json");new AppSettings(Photos:new(folder,new(Filter:"Warm",Exposure:.5f))).Save(config);
            Check(AppSettings.Load(config).Photos is {Folder:var output,Adjustments.Filter:"Warm"}&&output==folder,"photo folder and filter defaults survive settings restart");
        }
        finally{Directory.Delete(folder,true);}
        Reject<IOException>(()=>PhotoCapture.ReadFocusedGame(int.MaxValue),"photo capture rejects another foreground process before copying any screen pixels");
        var legacy=new CameraControls(Forward:"F12");legacy.Validate();Check(legacy.CaptureKey=="F11","legacy camera bindings keep an occupied F12 and choose an unused capture default");
        var binding=new CameraControls(Capture:"F8");binding.Validate();Check(binding.Read(k=>k==0x77).Capture,"custom photo capture binding is read independently of movement");
    }
}
