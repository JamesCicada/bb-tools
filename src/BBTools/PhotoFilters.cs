namespace BBTools;
internal sealed partial record PhotoImage(int Width,int Height,byte[] Pixels)
{
    public static int ByteCount(int width,int height)
    {
        if(width<1||height<1||width>16384||height>16384||(long)width*height>100000000)throw new ArgumentException("Unsupported photo dimensions.");
        return checked(width*height*4);
    }
    public void Validate(){if(Pixels.Length!=ByteCount(Width,Height))throw new ArgumentException("Photo pixel buffer length differs from its dimensions.");}
}
internal static class PhotoProcessing
{
    private static readonly float[] Linear=Enumerable.Range(0,256).Select(i=>{var v=i/255f;return v<=.04045f?v/12.92f:MathF.Pow((v+.055f)/1.055f,2.4f);}).ToArray();
    private static byte Srgb(float value)
    {value=Math.Clamp(value,0,1);return (byte)Math.Clamp((int)MathF.Round(255*(value<=.0031308f?12.92f*value:1.055f*MathF.Pow(value,1/2.4f)-.055f)),0,255);}
    public static PhotoImage Apply(PhotoImage original,PhotoAdjustments settings,CancellationToken cancellationToken=default)
    {
        original.Validate();settings.Validate();var output=original.Pixels.ToArray();
        if(settings==new PhotoAdjustments())return new(original.Width,original.Height,output);
        var exposure=MathF.Pow(2,settings.Exposure);var saturation=settings.Filter=="Noir"?0:settings.Saturation;
        for(var y=0;y<original.Height;y++)
        {
            cancellationToken.ThrowIfCancellationRequested();var ny=(2f*y+1)/original.Height-1;
            for(var x=0;x<original.Width;x++)
            {
                var at=(y*original.Width+x)*4;var r=Linear[output[at+2]];var g=Linear[output[at+1]];var b=Linear[output[at]];
                if(settings.Filter=="Warm"){r*=1.12f;b*=.85f;}else if(settings.Filter=="Cool"){r*=.88f;b*=1.12f;}
                else if(settings.Filter=="Sepia"){var nr=.393f*r+.769f*g+.189f*b;var ng=.349f*r+.686f*g+.168f*b;b=.272f*r+.534f*g+.131f*b;r=nr;g=ng;}
                r=(r*exposure-.18f)*settings.Contrast+.18f;g=(g*exposure-.18f)*settings.Contrast+.18f;b=(b*exposure-.18f)*settings.Contrast+.18f;
                var luminance=.2126f*r+.7152f*g+.0722f*b;var nx=(2f*x+1)/original.Width-1;var shade=1-settings.Vignette*Math.Clamp((nx*nx+ny*ny)/2,0,1);
                output[at+2]=Srgb((luminance+(r-luminance)*saturation)*shade);output[at+1]=Srgb((luminance+(g-luminance)*saturation)*shade);output[at]=Srgb((luminance+(b-luminance)*saturation)*shade);
            }
        }
        return new(original.Width,original.Height,output);
    }
}
