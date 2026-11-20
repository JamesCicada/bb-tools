namespace BBTools.Memory;

// Community report (Nexus bug, 2026-11-17, v0.7.1): weapons created directly at
// an upgrade level keep closed blood-gem slots. The same happens with other
// item-grant mods, so it is native game behavior, not a bad item request. The
// reporter confirmed that acquiring a weapon one level lower and upgrading it
// once in the workshop opens all slots for the new level. Until a native slot
// initializer is researched, offer that path instead of writing slot data.
public static class WeaponGemSlots
{
    // Weapon catalog IDs keep the reinforcement level in the last two digits
    // (see WeaponDurability: id / 100 * 100 is the base weapon row).
    public static int UpgradeLevel(int id, uint category) => category == 0 ? id % 100 : 0;
    public static bool NeedsWorkshopUpgrade(int id, uint category) => UpgradeLevel(id, category) > 0;
    public static int SpawnId(int id, uint category) => NeedsWorkshopUpgrade(id, category) ? id - 1 : id;
}
