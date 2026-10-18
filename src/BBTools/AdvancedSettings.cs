namespace BBTools;
public sealed record AdvancedSettings(bool MovementEnabled=false,float Movement=1,bool GravityEnabled=false,float Gravity=1,bool HideHud=false)
{
    public void Validate(){if(!float.IsFinite(Movement)||Movement is <.1f or >5||!float.IsFinite(Gravity)||Gravity is <.05f or >2)throw new ArgumentException("Invalid movement/gravity preset.");}
}
