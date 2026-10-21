using BBTools.Memory;

namespace BBTools;

// Retention is presentation state; stale actor pointers are never read after a context loss.
internal sealed class TargetMonitor
{
    private PlayerSession? owner;
    private InspectionContext context;
    private bool retainedLive;
    private bool previouslyBroken;
    public EnemySnapshot? Snapshot { get; private set; }
    public string State { get; private set; } = "Lock onto an enemy to start tracking.";
    public int ObservedBreaks { get; private set; }
    public void Poll(PlayerSession session)
    {
        try
        {
            var now=session.ReadInspectionContext();var selected=session.ReadLockedEnemy();
            if(selected!=null)
            {
                var fresh=session.ReadEnemySnapshot(selected);
                var different=owner!=session||context!=now||Snapshot?.Character!=selected;
                if(different){ObservedBreaks=0;previouslyBroken=false;}
                owner=session;context=now;retainedLive=true;Update(fresh);
                State=fresh.Health==0?"Target defeated · final live reading":"Locked target · live";
            }
            else if(retainedLive&&owner==session&&context==now&&Snapshot!=null)
            {Update(session.ReadEnemySnapshot(Snapshot.Character));State=Snapshot.Health==0?"Enemy defeated · retained":"Unlocked · still updating the retained enemy";}
            else {retainedLive=false;State=Snapshot==null?"Lock onto an enemy to start tracking.":"Last known snapshot · target another enemy to replace it";}
        }
        catch(Exception ex)when(ex is System.IO.IOException or System.ComponentModel.Win32Exception or ObjectDisposedException)
        {Unavailable($"Last known snapshot · {ex.Message}");}
    }
    private void Update(EnemySnapshot next)
    {
        var broken=next.Armor?.Broken==true;if(broken&&!previouslyBroken)ObservedBreaks++;
        previouslyBroken=broken;Snapshot=next;
    }
    public void Unavailable(string state)
    {retainedLive=false;State=Snapshot==null?state:$"{state} · readings retained";}
}
