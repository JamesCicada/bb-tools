namespace BBTools.Memory;

public enum EchoesObservationKind { Death, Returned }
public sealed record EchoesProtectionObservation(EchoesObservationKind Kind,int LastLivingEchoes,int CurrentEchoes,bool DeathObserved,bool LoadedAgain)
{
    public string Summary=>$"{Kind}: last living Echoes {LastLivingEchoes}, current {CurrentEchoes}, death observed {DeathObserved}, loading observed {LoadedAgain}. Protection selected; observation only, no balance refund.";
}

// Observe only. Spending, warp/reload and deaths are never turned into automatic
// refunds. Retained numbers are diagnostics, never addresses for later writes.
public sealed class EchoesProtectionDiagnostics
{
    private bool tracking,wasAlive,unavailable,death;
    private ulong actor,character;
    private int livingEchoes;
    public void Reset(){tracking=wasAlive=unavailable=death=false;actor=character=0;livingEchoes=0;}
    public void PlayerUnavailable(){if(tracking)unavailable=true;}
    public EchoesProtectionObservation? Observe(ulong nextActor,ulong nextCharacter,bool alive,int echoes,bool selected)
    {
        if(!selected){Reset();return null;}
        if(echoes is <0 or >999999999)throw new ArgumentOutOfRangeException(nameof(echoes));
        if(!tracking){if(!alive)return null;tracking=true;actor=nextActor;character=nextCharacter;wasAlive=alive;livingEchoes=echoes;death=!alive;return null;}
        EchoesProtectionObservation? result=null;
        var reloaded=unavailable||actor!=nextActor||character!=nextCharacter;
        if(alive&&(death||reloaded))
        {
            result=new(EchoesObservationKind.Returned,livingEchoes,echoes,death,reloaded);death=false;unavailable=false;
        }
        else if(!alive&&wasAlive&&!reloaded)
        {
            death=true;result=new(EchoesObservationKind.Death,livingEchoes,echoes,true,false);
        }
        actor=nextActor;character=nextCharacter;wasAlive=alive;
        if(alive)livingEchoes=echoes;
        return result;
    }
}
