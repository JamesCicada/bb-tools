using System.IO;
using System.Windows;
using BBTools.Memory;

namespace BBTools;

internal sealed record ParryCombatSettings(bool KeepHealth,bool Stamina,string[] Flags,float Damage,float Echoes)
{
    public ParryCombatSettings Drill()=>this with {KeepHealth=true,Stamina=true,Damage=.1f,
        Flags=Flags.Where(x=>x is "NoFallDamage" or "NoEchoesLoss").Concat(new[]{"GodMode","InfiniteItems"}).ToArray()};
}

public partial class MainWindow
{
    private ParryCombatSettings? beforeParryPractice;
    private EnemyPracticeProtection? enemyPractice;
    private ParryCombatSettings CaptureParryCombat()=>new(KeepHealth.IsChecked==true,InfiniteStamina.IsChecked==true,
        FlagControls.Where(x=>x.IsChecked==true).Select(x=>(string)x.Tag).ToArray(),desiredDamage,desiredEchoes);
    private async void ApplyParrySetup(object sender,RoutedEventArgs e)
    {
        var previous=CaptureParryCombat();
        await RunGameAction(async engine=>
        {
            beforeParryPractice??=previous;
            await SetParryCombat(engine,previous.Drill());
            TrackEnemy.IsChecked=true;
            ParrySummary.Text=Translations.T("message.Practicesetupappliedprotectioninfinitebulletsstamina.2090fea");
        },Translations.T("message.ParrypracticesetupappliedEnemyAIisactiveoneshotinvis.3e72427"));
    }
    private async void RestoreParrySetup(object sender,RoutedEventArgs e)
    {
        if(beforeParryPractice is not {} previous){Status.Text=Translations.T("message.Nopreviousparrypracticesetuptorestore.bb266fc");return;}
        await RunGameAction(async engine=>
        {
            StopEnemyPractice();await SetParryCombat(engine,previous);beforeParryPractice=null;
            ParrySummary.Text=Translations.T("message.Previouscombatselectionsrestored.c11fd35");
        },Translations.T("message.PreviouscombatselectionsrestoredHealthchangesalready.a0acb47"));
    }
    private async Task SetParryCombat(GameActions engine,ParryCombatSettings next)
    {
        await engine.SetMultipliersAsync(next.Damage,next.Echoes);
        desiredDamage=next.Damage;desiredEchoes=next.Echoes;multiplierFailedActor=0;
        updating=true;
        try
        {
            KeepHealth.IsChecked=next.KeepHealth;InfiniteStamina.IsChecked=next.Stamina;
            foreach(var control in FlagControls)control.IsChecked=next.Flags.Contains((string)control.Tag);
            DamageMultiplier.Text=next.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture);
            EchoesMultiplier.Text=next.Echoes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        finally{updating=false;}
        // The regular player poll also reapplies selections after loading.
        session!.SetInfiniteStamina(next.Stamina);
        foreach(var control in FlagControls)session.SetPracticeFlag(Enum.Parse<PracticeFlag>((string)control.Tag),control.IsChecked==true);
    }
    private void EnemyPracticeChanged(object sender,RoutedEventArgs e)
    {
        if(updating)return;
        try
        {
            if(ProtectPracticeEnemy.IsChecked!=true){StopEnemyPractice();ParrySummary.Text=Translations.T("message.Targetprotectiondisabledoriginalflagrestoredwherethe.4e9fc0b");return;}
            if(session==null||memory==null||actionBusy)throw new IOException(Translations.T("message.Connecttoaloadedcharacterandfinishthecurrentrequestf.c4b5d82"));
            enemyPractice??=new(session);memory.Suspended(()=>enemyPractice.ProtectLockedEnemy());
            App.Log?.Write("ACTION","Parry practice target protection enabled");
            TrackEnemy.IsChecked=true;ParrySummary.Text=Translations.T("message.LockedenemyprotectedfromordinarylethalHPdamageUnlock.e137988");
        }
        catch(Exception ex)when(IsExpected(ex))
        {
            updating=true;ProtectPracticeEnemy.IsChecked=false;updating=false;
            App.LogError(ex);Status.Text=ex.Message;
            try{StopEnemyPractice();}catch(Exception cleanup)when(IsExpected(cleanup)){App.LogError(cleanup);Status.Text+=Translations.Format("message.Restorationpending0.b9d681a", cleanup.Message);}
        }
    }
    private void UpdateEnemyPractice()
    {
        if(enemyPractice==null||memory==null)return;
        if(ProtectPracticeEnemy.IsChecked!=true){StopEnemyPractice();return;}
        try{if(enemyPractice.NeedsUpdate())memory.Suspended(()=>enemyPractice.Update());}
        catch(Exception ex)when(IsExpected(ex))
        {
            StopEnemyPractice();ParrySummary.Text=Translations.Format("message.Targetprotectionstopped0Lockontoalivingenemyandreena.6b018ad", ex.Message);
            App.LogError(ex);
        }
    }
    private void StopEnemyPractice()
    {
        updating=true;ProtectPracticeEnemy.IsChecked=false;updating=false;
        if(enemyPractice==null)return;
        if(memory?.IsAlive==true)memory.Suspended(()=>enemyPractice.Restore());
        enemyPractice=null;
    }
    private void RestorePracticeEnemyHealth(object sender,RoutedEventArgs e)
    {
        Run(()=>
        {
            if(actionBusy||memory==null)throw new IOException(Translations.T("message.Finishthecurrentgamerequestbeforerestoringenemyhealt.f7d7389"));
            memory.Suspended(()=>session!.RestoreLockedEnemyHealth());
            ParrySummary.Text=Translations.T("message.LockedenemyrestoredtofullHPThisdoesnotreviveadeadene.06d484e");
        },Translations.T("message.Lockedenemyhealthrestored.2a881cf"));
    }
}
