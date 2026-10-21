using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace BBTools;

internal static class PhotoCapture
{
    public static PhotoImage ReadFocusedGame(int processId)
    {
        if(processId<=0)throw new IOException("Connect to the game before capturing a photo.");
        var previous=SetThreadDpiAwarenessContext(new nint(-4));
        nint desktop=0,dc=0,bitmap=0,old=0;
        try
        {
            var window=GetForegroundWindow();GetWindowThreadProcessId(window,out var owner);
            if(owner!=(uint)processId||IsIconic(window))throw new IOException("Focus the connected game before capturing. Use the capture key or the three-second countdown.");
            if(!GetClientRect(window,out var rectangle)||!ClientToScreen(window,ref rectangle.Origin))throw new Win32Exception(Marshal.GetLastWin32Error());
            var width=rectangle.Right;var height=rectangle.Bottom;var count=PhotoImage.ByteCount(width,height);
            desktop=GetDC(0);if(desktop==0)throw new Win32Exception(Marshal.GetLastWin32Error());
            dc=CreateCompatibleDC(desktop);if(dc==0)throw new Win32Exception(Marshal.GetLastWin32Error());
            var info=new BitmapInfo{Size=(uint)Marshal.SizeOf<BitmapInfo>(),Width=width,Height=-height,Planes=1,BitCount=32};
            bitmap=CreateDIBSection(desktop,ref info,0,out var pixels,0,0);if(bitmap==0||pixels==0)throw new Win32Exception(Marshal.GetLastWin32Error());
            old=SelectObject(dc,bitmap);if(old==0||old==new nint(-1))throw new Win32Exception(Marshal.GetLastWin32Error());
            // Capture the composed, visible game-client pixels at physical resolution.
            if(!BitBlt(dc,0,0,width,height,desktop,rectangle.Origin.X,rectangle.Origin.Y,0x40CC0020))throw new Win32Exception(Marshal.GetLastWin32Error());
            if(GetForegroundWindow()!=window)throw new IOException("The focused window changed during capture; no photo was saved.");
            var data=new byte[count];Marshal.Copy(pixels,data,0,count);for(var i=3;i<count;i+=4)data[i]=255;
            return new(width,height,data);
        }
        finally
        {
            if(old!=0&&old!=new nint(-1)&&dc!=0)SelectObject(dc,old);if(bitmap!=0)DeleteObject(bitmap);if(dc!=0)DeleteDC(dc);if(desktop!=0)ReleaseDC(0,desktop);
            if(previous!=0)SetThreadDpiAwarenessContext(previous);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] private struct Rect{public Point Origin;public int Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo{public uint Size;public int Width,Height;public ushort Planes,BitCount;public uint Compression,SizeImage;public int XPelsPerMeter,YPelsPerMeter;public uint ColorsUsed,ColorsImportant;}
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll")]private static extern bool IsIconic(nint window);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool GetClientRect(nint window,out Rect rectangle);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool ClientToScreen(nint window,ref Point point);
    [DllImport("user32.dll")]private static extern nint GetDC(nint window);
    [DllImport("user32.dll")]private static extern int ReleaseDC(nint window,nint dc);
    [DllImport("user32.dll")]private static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("gdi32.dll",SetLastError=true)]private static extern nint CreateCompatibleDC(nint source);
    [DllImport("gdi32.dll",SetLastError=true)]private static extern nint CreateDIBSection(nint dc,ref BitmapInfo info,uint usage,out nint pixels,nint section,uint offset);
    [DllImport("gdi32.dll",SetLastError=true)]private static extern nint SelectObject(nint dc,nint item);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(nint item);
    [DllImport("gdi32.dll")]private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll",SetLastError=true)]private static extern bool BitBlt(nint dest,int x,int y,int width,int height,nint source,int sx,int sy,uint operation);
}
