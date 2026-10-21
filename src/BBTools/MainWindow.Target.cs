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
        EnemyName.Text=enemy.Title;EnemyMeta.Text=$"{enemy.Identity} · {(enemy.Distance is {} distance?$"{distance:0.0} m away":"distance unavailable")} · sampled {enemy.Sampled.ToLocalTime():HH:mm:ss}";
        EnemyHealth.Text=$"{enemy.Health:N0} / {enemy.Maximum:N0}";EnemyHealthBar.Value=100d*enemy.Health/enemy.Maximum;
        EnemyStamina.Text=enemy.MaxStamina>0?$"Stamina {enemy.Stamina:N0} / {enemy.MaxStamina:N0}":"Stamina unavailable / unused";
        EnemyArmor.Text=enemy.Armor?.Summary??"No super-armor module available";EnemyArmorBar.Value=enemy.Armor?.Percent??0;
        EnemyBreaks.Text=$"Observed breaks: {targetMonitor.ObservedBreaks}";
        EnemyGauges.ItemsSource=enemy.Gauges.Where(g=>g.Name!="Legacy disease slot"||g.Current!=0||g.Limit>1).ToArray();
        EnemyEffects.ItemsSource=enemy.Effects;EnemyEffectsHeader.Text=$"STATUS / SPECIAL EFFECTS · {enemy.Effects.Count(x=>x.Active)} active / {enemy.Effects.Length} captured";
        EnemyEffectsEmpty.Text=enemy.Effects.Length==0?"No effects in this snapshot.":"";
    }
}
