using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace BBTools.LinuxDesktop;

// One private display connection, used only on the UI thread. Focused-window PID
// must match the selected game; no global grabs or synthetic game input.
internal sealed class X11GameWindow : IDisposable
{
    private const string X11="libX11.so.6";
    private readonly nint display;
    private readonly byte[] keys=new byte[32];
    private readonly ErrorHandler handler;
    private readonly nint previous;
    private int error;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int ErrorHandler(nint d,nint e);
    public X11GameWindow()
    {
        if(!OperatingSystem.IsLinux())throw new NotSupportedException("X11 input requires Linux.");
        display=XOpenDisplay(0);if(display==0)throw new NotSupportedException("X11/XWayland game input is unavailable on this display.");
        handler=(d,e)=>{if(d==display){error=1;return 0;}return previous==0?0:Marshal.GetDelegateForFunctionPointer<ErrorHandler>(previous)(d,e);};
        previous=XSetErrorHandler(Marshal.GetFunctionPointerForDelegate(handler));
    }
    private ulong Property(ulong window,string name,ulong expected)
    {
        var atom=XInternAtom(display,name,true);if(atom==0)return 0;
        var rc=XGetWindowProperty(display,window,atom,0,1,false,expected,out var type,out var format,out var count,out _,out var data);
        try{return rc==0&&error==0&&type==expected&&format==32&&count==1&&data!=0?unchecked((ulong)Marshal.ReadInt64(data)):0;}
        finally{if(data!=0)XFree(data);}
    }
    private ulong Focused(int pid)
    {
        error=0;XGetInputFocus(display,out var window,out _);
        for(var i=0;i<16&&window>1;i++)
        {
            if(Property(window,"_NET_WM_PID",6)==(ulong)pid)return window; // XA_CARDINAL
            if(XQueryTree(display,window,out _,out var parent,out var children,out _)==0)return 0;
            if(children!=0)XFree(children);if(parent==window)break;window=parent;
        }
        return 0;
    }
    public bool Poll(int pid){if(Focused(pid)==0)return false;XQueryKeymap(display,keys);return error==0;}
    public bool Down(int key)
    {
        ulong symbol=key switch{0x10=>0xffe1,0x11=>0xffe3,0x12=>0xffe9,0x25=>0xff51,0x26=>0xff52,0x27=>0xff53,0x28=>0xff54,0x24=>0xff50,0x23=>0xff57,0x21=>0xff55,0x22=>0xff56,0x2d=>0xff63,0x2e=>0xffff,0x1b=>0xff1b,9=>0xff09,13=>0xff0d,>=0x70 and <=0x7b=>(ulong)(0xffbe+key-0x70),>=65 and <=90=>(ulong)(key+32),_=>(ulong)key};
        bool Held(ulong sym){var code=XKeysymToKeycode(display,sym);return code>0&&(keys[code/8]&(1<<(code%8)))!=0;}
        return Held(symbol)||(key is 0x10 or 0x11 or 0x12&&Held(symbol+1));
    }
    public PhotoImage Capture(int pid)
    {
        var window=Focused(pid);if(window==0)throw new IOException("Focus the connected game window before capturing.");
        return CaptureWindow(window);
    }
    internal PhotoImage CaptureOwnWindow(ulong window)
    {
        error=0;
        if(Property(window,"_NET_WM_PID",6)!=(ulong)Environment.ProcessId)throw new IOException("The smoke-test window is not owned by this app.");
        XQueryKeymap(display,keys);_ = Down(KeyCodes.VirtualKey("W"));
        return CaptureWindow(window);
    }
    private PhotoImage CaptureWindow(ulong window)
    {
        if(XGetGeometry(display,window,out _,out _,out _,out var width,out var height,out _,out _)==0)throw new IOException("Game window size is unavailable.");
        var bytes=new byte[PhotoImage.ByteCount((int)width,(int)height)];
        var image=XGetImage(display,window,0,0,width,height,ulong.MaxValue,2);
        if(image==0||error!=0)throw new IOException("The game window cannot be captured with X11. Try windowed mode.");
        try
        {
            var info=Marshal.PtrToStructure<Image>(image);
            if(info.BitsPerPixel!=32||info.ByteOrder!=0||info.Red!=0xff0000||info.Green!=0xff00||info.Blue!=0xff||info.Stride<(int)width*4||info.Stride>65536)throw new IOException("Unsupported X11 pixel layout.");
            for(var y=0;y<height;y++)Marshal.Copy(info.Data+(int)y*info.Stride,bytes,(int)y*(int)width*4,(int)width*4);
            for(var i=3;i<bytes.Length;i+=4)bytes[i]=255;
            return new((int)width,(int)height,bytes);
        }
        finally{XDestroyImage(image);}
    }
    public static string Save(PhotoImage photo,string folder,string suffix)
    {
        photo.Validate();Directory.CreateDirectory(folder);var path=Path.Combine(folder,$"BBTools-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}-{suffix}.png");
        using var bitmap=new WriteableBitmap(new(photo.Width,photo.Height),new(96,96),PixelFormat.Bgra8888,AlphaFormat.Opaque);
        using(var buffer=bitmap.Lock())for(var y=0;y<photo.Height;y++)Marshal.Copy(photo.Pixels,y*photo.Width*4,buffer.Address+y*buffer.RowBytes,photo.Width*4);
        using(var file=new FileStream(path,FileMode.CreateNew))bitmap.Save(file,new PngBitmapEncoderOptions());
        return path;
    }
    public void Dispose(){XSync(display,false);XSetErrorHandler(previous);XCloseDisplay(display);}
    [StructLayout(LayoutKind.Sequential)]private struct Image{public int Width,Height,Offset,Format;public nint Data;public int ByteOrder,BitmapUnit,BitmapOrder,BitmapPad,Depth,Stride,BitsPerPixel;public ulong Red,Green,Blue;}
    [DllImport(X11)]private static extern nint XOpenDisplay(nint name);
    [DllImport(X11)]private static extern int XCloseDisplay(nint d);
    [DllImport(X11)]private static extern nint XSetErrorHandler(nint h);
    [DllImport(X11)]private static extern int XSync(nint d,bool discard);
    [DllImport(X11)]private static extern int XGetInputFocus(nint d,out ulong w,out int revert);
    [DllImport(X11)]private static extern ulong XInternAtom(nint d,string name,bool only);
    [DllImport(X11)]private static extern int XGetWindowProperty(nint d,ulong w,ulong atom,nint offset,nint length,bool delete,ulong request,out ulong type,out int format,out ulong count,out ulong remaining,out nint data);
    [DllImport(X11)]private static extern int XQueryTree(nint d,ulong w,out ulong root,out ulong parent,out nint children,out uint count);
    [DllImport(X11)]private static extern int XFree(nint p);
    [DllImport(X11)]private static extern int XQueryKeymap(nint d,[Out]byte[] keys);
    [DllImport(X11)]private static extern byte XKeysymToKeycode(nint d,ulong symbol);
    [DllImport(X11)]private static extern int XGetGeometry(nint d,ulong w,out ulong root,out int x,out int y,out uint width,out uint height,out uint border,out uint depth);
    [DllImport(X11)]private static extern nint XGetImage(nint d,ulong w,int x,int y,uint width,uint height,ulong planes,int format);
    [DllImport(X11)]private static extern int XDestroyImage(nint image);
}
