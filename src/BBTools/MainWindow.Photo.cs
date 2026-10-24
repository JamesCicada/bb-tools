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
        var value=ReadPhotoAdjustments();PhotoAdjustmentsSummary.Text=Translations.Format("message.Exposure0000000EVContrast1000Saturation2000Vignette3.7105687", value.Exposure, value.Contrast, value.Saturation, value.Vignette);
        PhotoFolderLabel.Text=Translations.Format("message.PNGfolder0.f76d329", PhotoFolder);
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
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);if(!closing)PhotoCaptureSummary.Text=Translations.Format("message.Previewunavailable0.1f9a0b1", ex.Message);}
        finally{if(ReferenceEquals(photoPreviewLifetime,lifetime))photoPreviewLifetime=null;lifetime.Dispose();}
    }
    private void CapturePhotoCountdown(object sender,RoutedEventArgs e)=>_ = CapturePhotoAsync(3);
    private async Task CapturePhotoAsync(int delaySeconds)
    {
        if(saveOperationBusy){Status.Text=Translations.T("message.Waitforthecurrentphotoorsaveoperation.70f80bf");return;}
        if(session==null||connectedPid<=0){Status.Text=Translations.T("message.Connecttothegamebeforecapturingaphoto.dafaa9d");return;}
        saveOperationBusy=true;var pid=connectedPid;var generation=targetGeneration;var folder=PhotoFolder;var adjustments=ReadPhotoAdjustments();
        try
        {
            if(delaySeconds>0){Status.Text=Translations.T("message.PhotointhreesecondsFocusthegamenow.ecfa01d");await Task.Delay(TimeSpan.FromSeconds(delaySeconds));}
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
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);if(!closing){PhotoCaptureSummary.Text=ex.Message;Status.Text=Translations.Format("message.Photocapturefailed0.c203cb7", ex.Message);}}
        finally{saveOperationBusy=false;}
    }
    private async void SavePhotoEdits(object sender,RoutedEventArgs e)
    {
        if(photoOriginal is not {} original){Status.Text=Translations.T("message.Captureaphotofirst.9950f5d");return;}
        if(saveOperationBusy){Status.Text=Translations.T("message.Waitforthecurrentphotoorsaveoperation.70f80bf");return;}
        var adjustments=ReadPhotoAdjustments();var folder=PhotoFolder;saveOperationBusy=true;
        try{var path=await Task.Run(()=>PhotoProcessing.Apply(original,adjustments).SavePng(folder,"edited"));PhotoCaptureSummary.Text=Translations.Format("message.Filteredcopysaved0Originalpreserved.1d33d7e", Path.GetFileName(path));Status.Text=PhotoCaptureSummary.Text;}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=Translations.Format("message.Photoexportfailed0.2bebcbd", ex.Message);}
        finally{saveOperationBusy=false;}
    }
    private void SavePhotoDefaults(object sender,RoutedEventArgs e)
    {
        try{var next=settings with{Photos=new(photoFolder,ReadPhotoAdjustments())};next.Save();settings=next;Status.Text=Translations.T("message.Photofolderandfilterdefaultssaved.78db6bb");}
        catch(Exception ex)when(IsExpected(ex)){App.LogError(ex);Status.Text=ex.Message;}
    }
    private void ChoosePhotoFolder(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFolderDialog{Title=Translations.T("message.Choosephotooutputfolder.c02b7ff"),FolderName=Directory.Exists(PhotoFolder)?PhotoFolder:""};
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
