using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace BBTools;

public partial class MainWindow
{
    private PhotoImage? photoOriginal;
    private string? photoFolder;
    private bool photoControlsReady,photoCaptureHeld;
    private CancellationTokenSource? photoPreviewLifetime;
    private long photoPreviewGeneration;
    private void InitializePhotoCapture()
    {
        LoadPhotoControls();Closed+=(_,_)=>photoPreviewLifetime?.Cancel();
    }
    private void LoadPhotoControls()
    {
        photoControlsReady=false;
        PhotoFilter.ItemsSource=PhotoAdjustments.Filters;
        var preferences=settings.Photos??new();var adjustments=preferences.Adjustments??new();photoFolder=preferences.Folder;
        PhotoFilter.SelectedItem=adjustments.Filter;PhotoExposure.Value=adjustments.Exposure;PhotoContrast.Value=adjustments.Contrast;PhotoSaturation.Value=adjustments.Saturation;PhotoVignette.Value=adjustments.Vignette;
        photoControlsReady=true;QueuePhotoPreview();
    }
    private PhotoAdjustments ReadPhotoAdjustments()=>new(PhotoFilter.SelectedItem as string??"Original",(float)PhotoExposure.Value,(float)PhotoContrast.Value,(float)PhotoSaturation.Value,(float)PhotoVignette.Value);
    private string PhotoFolder=>photoFolder??Path.Combine(AppContext.BaseDirectory,"photos");
    private void UpdatePhotoCaption()
    {
        var value=ReadPhotoAdjustments();PhotoAdjustmentsSummary.Text=$"Exposure {value.Exposure:+0.0;-0.0;0.0} EV · Contrast {value.Contrast:0.00} · Saturation {value.Saturation:0.00} · Vignette {value.Vignette:0.00}";
        PhotoFolderLabel.Text=$"PNG folder: {PhotoFolder}";
    }
    private void PhotoAdjustmentChanged(object sender,RoutedPropertyChangedEventArgs<double> e)=>QueuePhotoPreview();
    private void PhotoFilterChanged(object sender,SelectionChangedEventArgs e)=>QueuePhotoPreview();
    private async void QueuePhotoPreview()
    {
        if(!photoControlsReady)return;UpdatePhotoCaption();
        photoPreviewLifetime?.Cancel();var lifetime=photoPreviewLifetime=new CancellationTokenSource();var token=lifetime.Token;
        var generation=++photoPreviewGeneration;var original=photoOriginal;var adjustments=ReadPhotoAdjustments();
        if(original==null){photoPreviewLifetime=null;lifetime.Dispose();return;}
        try
        {
            await Task.Delay(180,token);var bitmap=await Task.Run(()=>PhotoProcessing.Apply(original,adjustments,token).Bitmap(),token);
            if(!closing&&generation==photoPreviewGeneration)PhotoPreview.Source=bitmap;
        }
        catch(OperationCanceledException){ }
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);if(!closing)PhotoCaptureSummary.Text=$"Preview unavailable: {ex.Message}";}
        finally{if(ReferenceEquals(photoPreviewLifetime,lifetime))photoPreviewLifetime=null;lifetime.Dispose();}
    }
    private void CapturePhotoCountdown(object sender,RoutedEventArgs e)=>_ = CapturePhotoAsync(3);
    private async Task CapturePhotoAsync(int delaySeconds)
    {
        if(saveOperationBusy){Status.Text="Wait for the current photo or save operation.";return;}
        if(session==null||connectedPid<=0){Status.Text="Connect to the game before capturing a photo.";return;}
        saveOperationBusy=true;var pid=connectedPid;var generation=targetGeneration;var folder=PhotoFolder;var adjustments=ReadPhotoAdjustments();
        try
        {
            if(delaySeconds>0){Status.Text="Photo in three seconds. Focus the game now.";await Task.Delay(TimeSpan.FromSeconds(delaySeconds));}
            if(closing||pid!=connectedPid||generation!=targetGeneration)return;
            var capture=await Task.Run(()=>{var image=PhotoCapture.ReadFocusedGame(pid);var path=image.SavePng(folder,"original");return(Image:image,Path:path);});
            photoOriginal=capture.Image;SavePhotoEditsButton.IsEnabled=true;QueuePhotoPreview();
            var message=$"Saved {capture.Image.Width} × {capture.Image.Height} lossless PNG: {Path.GetFileName(capture.Path)}";
            if(adjustments!=new PhotoAdjustments())
            {
                try{await Task.Run(()=>PhotoProcessing.Apply(capture.Image,adjustments).SavePng(folder,"edited"));message+=" · filtered copy saved";}
                catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);message+=$" · original saved; filtered copy failed: {ex.Message}";}
            }
            PhotoCaptureSummary.Text=message;Status.Text=message;App.Log?.Write("PHOTO",message);
        }
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);if(!closing){PhotoCaptureSummary.Text=ex.Message;Status.Text=$"Photo capture failed: {ex.Message}";}}
        finally{saveOperationBusy=false;}
    }
    private async void SavePhotoEdits(object sender,RoutedEventArgs e)
    {
        if(photoOriginal is not {} original){Status.Text="Capture a photo first.";return;}
        if(saveOperationBusy){Status.Text="Wait for the current photo or save operation.";return;}
        var adjustments=ReadPhotoAdjustments();var folder=PhotoFolder;saveOperationBusy=true;
        try{var path=await Task.Run(()=>PhotoProcessing.Apply(original,adjustments).SavePng(folder,"edited"));PhotoCaptureSummary.Text=$"Filtered copy saved: {Path.GetFileName(path)}. Original preserved.";Status.Text=PhotoCaptureSummary.Text;}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=$"Photo export failed: {ex.Message}";}
        finally{saveOperationBusy=false;}
    }
    private void SavePhotoDefaults(object sender,RoutedEventArgs e)
    {
        try{var next=settings with{Photos=new(photoFolder,ReadPhotoAdjustments())};next.Save();settings=next;Status.Text="Photo folder and filter defaults saved.";}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void ChoosePhotoFolder(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFolderDialog{Title="Choose photo output folder",FolderName=Directory.Exists(PhotoFolder)?PhotoFolder:""};
        if(dialog.ShowDialog(this)!=true)return;photoFolder=dialog.FolderName;UpdatePhotoCaption();SavePhotoDefaults(sender,e);
    }
    private void OpenPhotoFolder(object sender,RoutedEventArgs e)
    {
        try{Directory.CreateDirectory(PhotoFolder);Process.Start(new ProcessStartInfo(PhotoFolder){UseShellExecute=true});}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void ResetPhotoAdjustments(object sender,RoutedEventArgs e)
    {
        photoControlsReady=false;PhotoFilter.SelectedItem="Original";PhotoExposure.Value=0;PhotoContrast.Value=PhotoSaturation.Value=1;PhotoVignette.Value=0;photoControlsReady=true;QueuePhotoPreview();
    }
}
