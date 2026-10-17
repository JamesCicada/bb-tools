namespace BBTools.Memory;

public sealed record PracticeDestination(string Name, int EntryLamp, SavedLocation Position)
{
    public string Label => Name;
    public override string ToString() => Label;
}

// Standpoints one metre outside fog objects, checked against local 1.09 MSB placements.
// The two final bosses share the Hunter's Dream arena gate; story conditions still apply.
public static class BossDestinations
{
    public static PracticeDestination[] All =>
    [
        new("Cleric Beast", 2412951, new("Cleric Beast", 0x18010000, -155.167533f, -26.870000f, 34.383928f, -2.326873f)),
        new("Father Gascoigne", 2412951, new("Father Gascoigne", 0x18010000, -50.903740f, -40.670000f, 70.099589f, -2.346770f)),
        new("Blood-starved Beast", 2302950, new("Blood-starved Beast", 0x17000000, -86.885996f, -126.290000f, 12.871555f, 2.265874f)),
        new("Vicar Amelia", 2402950, new("Vicar Amelia", 0x18000000, 51.010000f, 35.860000f, 310.703975f, -2.617994f)),
        new("The Witch of Hemwick", 2202950, new("The Witch of Hemwick", 0x16000000, -329.750000f, 2.616000f, 697.210000f, 3.141593f)),
        new("Darkbeast Paarl", 2302950, new("Darkbeast Paarl", 0x17000000, 136.361060f, -119.680000f, -86.170749f, 1.980949f)),
        new("Shadow of Yharnam", 2702950, new("Shadow of Yharnam", 0x1B000000, -322.218626f, -185.718000f, 495.102829f, 0.781541f)),
        new("Rom, the Vacuous Spider", 3202950, new("Rom, the Vacuous Spider", 0x20000000, -451.934307f, -174.880000f, 392.992020f, 1.221730f)),
        new("The One Reborn", 2802950, new("The One Reborn", 0x1C000000, 380.459893f, -122.340000f, -214.875893f, -0.785398f)),
        new("Martyr Logarius", 2502950, new("Martyr Logarius", 0x19000000, 33.356424f, 112.470000f, -329.740848f, -0.610865f)),
        new("Amygdala", 3302950, new("Amygdala", 0x21000000, -46.255816f, 1462.003000f, -69.415607f, 1.954769f)),
        new("Celestial Emissary", 2422950, new("Celestial Emissary", 0x18020000, -6.336025f, 55.486000f, 293.910000f, -1.047198f)),
        new("Ebrietas, Daughter of the Cosmos", 2422950, new("Ebrietas, Daughter of the Cosmos", 0x18020000, 93.511242f, 4.350000f, 386.127581f, -2.630927f)),
        new("Micolash, Host of the Nightmare", 2602950, new("Micolash, Host of the Nightmare", 0x1A000000, 144.877f, 1033.097f, -14.742f, -1.570796f)),
        new("Gehrman, the First Hunter", 2102950, new("Gehrman, the First Hunter", 0x15000000, 12.167805f, -9.828f, -36.448844f, -1.483530f)),
        new("Moon Presence", 2102950, new("Moon Presence", 0x15000000, 12.167805f, -9.828f, -36.448844f, -1.483530f)),
        new("Mergo's Wet Nurse", 2602950, new("Mergo's Wet Nurse", 0x1A000000, 121.140000f, 1125.334000f, -37.839000f, -1.570796f)),
        new("[DLC] Ludwig, the Holy Blade", 3402950, new("[DLC] Ludwig, the Holy Blade", 0x22000000, -407.930000f, 1503.950000f, -715.450000f, 0.000000f)),
        new("[DLC] Living Failures", 3502950, new("[DLC] Living Failures", 0x23000000, -377.540000f, 1593.050000f, -824.550000f, 1.570796f)),
        new("[DLC] Lady Maria of the Astral Clocktower", 3502950, new("[DLC] Lady Maria of the Astral Clocktower", 0x23000000, -451.000000f, 1595.660000f, -824.410000f, 1.570796f)),
        new("[DLC] Laurence, the First Vicar", 3402950, new("[DLC] Laurence, the First Vicar", 0x22000000, -451.400000f, 1535.860000f, -293.556025f, -2.617994f)),
        new("[DLC] Orphan of Kos", 3602950, new("[DLC] Orphan of Kos", 0x24000000, -700.200000f, 1580.650000f, -905.200000f, 0.000000f)),
    ];
}
