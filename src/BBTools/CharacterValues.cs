namespace BBTools;

public sealed record CharacterValues(int Echoes, int Vitality, int Endurance, int Strength, int Skill, int Bloodtinge, int Arcane)
{
    public int[] Attributes => [Vitality, Endurance, Strength, Skill, Bloodtinge, Arcane];
    public void Validate()
    {
        if (Echoes is < 0 or > 999999999) throw new ArgumentException("Blood Echoes must be between 0 and 999,999,999.");
        if (Attributes.Any(x => x is < 1 or > 99)) throw new ArgumentException("Every attribute must be between 1 and 99.");
    }
}
