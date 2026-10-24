using System.Windows.Threading;

namespace BBTools;

public partial class MainWindow
{
    private readonly TargetMonitor targetMonitor=new();
    private readonly DispatcherTimer targetTimer=new(){Interval=TimeSpan.FromMilliseconds(200)};
    private bool targetReading;
    private string? loggedTargetState;
    private long targetGeneration;
    private void InitializeTargetMonitor()
    {
        targetTimer.Tick+=TargetTick;targetTimer.Start();
        Closed+=(_,_)=>{targetTimer.Stop();targetTimer.Tick-=TargetTick;};
    }
    private async void TargetTick(object? sender,EventArgs e)
    {
        if(closing||targetReading)return;
        if(TrackEnemy.IsChecked!=true){targetMonitor.Unavailable("Tracking paused");RenderTarget();return;}
        if(session==null)return;
        targetReading=true;var observed=session;var generation=targetGeneration;
        try
        {
            await Task.Run(()=>targetMonitor.Poll(observed));
            if(closing||session!=observed||generation!=targetGeneration){targetMonitor.Unavailable("Connection changed");return;}
            RenderTarget();
        }
        catch(Exception ex)when(IsExpected(ex)||ex is ObjectDisposedException)
        {targetMonitor.Unavailable(ex.Message);if(!closing)RenderTarget();}
        finally{targetReading=false;}
    }
    private void RenderTarget()
    {
        EnemyTrackingState.Text=targetMonitor.State;
        if(loggedTargetState!=targetMonitor.State){loggedTargetState=targetMonitor.State;App.Log?.Write("TARGET",targetMonitor.State);}
        if(targetMonitor.Snapshot is not {} enemy)return;
        EnemyName.Text=enemy.Title;EnemyMeta.Text=Translations.Format("message.01sampled2HHmmss.4342a80", enemy.Identity, (enemy.Distance is {} distance?$"{distance:0.0} m away":"distance unavailable"), enemy.Sampled.ToLocalTime());
        EnemyHealth.Text=$"{enemy.Health:N0} / {enemy.Maximum:N0}";EnemyHealthBar.Value=100d*enemy.Health/enemy.Maximum;
        EnemyStamina.Text=enemy.MaxStamina>0?Translations.Format("message.Stamina0N01N0.61cca01", enemy.Stamina, enemy.MaxStamina):Translations.T("message.Staminaunavailableunused.2a55a31");
        EnemyArmor.Text=enemy.Armor?.Summary??Translations.T("message.Nosuperarmormoduleavailable.d5acf20");EnemyArmorBar.Value=enemy.Armor?.Percent??0;
        EnemyBreaks.Text=Translations.Format("message.Observedbreaks0.ff3e3a7", targetMonitor.ObservedBreaks);
        EnemyGauges.ItemsSource=enemy.Gauges.Where(g=>g.Name!="Legacy disease slot"||g.Current!=0||g.Limit>1).ToArray();
        EnemyEffects.ItemsSource=enemy.Effects;EnemyEffectsHeader.Text=Translations.Format("message.STATUSSPECIALEFFECTS0active1captured.71e80c8", enemy.Effects.Count(x=>x.Active), enemy.Effects.Length);
        EnemyEffectsEmpty.Text=enemy.Effects.Length==0?Translations.T("message.Noeffectsinthissnapshot.fbd9e70"):"";
    }
}
