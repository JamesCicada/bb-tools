using System.IO;

namespace BBTools.Memory;

public sealed record PlayerState(ulong Actor, ulong Component, int Health, int MaxHealth, int Stamina, int MaxStamina);
public sealed record CharacterState(ulong Address, int Level, CharacterValues Values);

// Bloodborne 1.09 layout facts: see docs/memory-research.md. No executable patches.
public sealed partial class PlayerSession(IMemory memory, ulong imageBase)
{
    public const ulong WorldPointer = 0x553E878;
    public static readonly (ulong Offset, byte[] Bytes)[] Fingerprints =
    [
        (0x18C2D00, Convert.FromHexString("554889E54156534889FB4C8D3567BBC703")),
        (0x18C2D3D, Convert.FromHexString("483958600F94C0")),
        (0x18F78B1, Convert.FromHexString("8B88F8000000")),
        (0x18F78DB, Convert.FromHexString("8B8034010000")),
        (0x1900278, Convert.FromHexString("498B96C00300008B8294000000")),
        (0x203A4A6, Convert.FromHexString("8B4740C38B4748C38B4750C38B4758C38B4760C38B4768C38B4770C3")),
        (0x1A571A7, Convert.FromHexString("41F6860002000080"))
    ];
    private ulong staminaOwner;
    private byte originalStaminaBit;

    public CharacterState ReadCharacter()
    {
        var player = ReadPlayer();
        var data = memory.Pointer(player.Actor + 0x3C0);
        var manager = memory.Pointer(imageBase + 0x553B130);
        if (memory.Pointer(manager + 8) != data) throw new IOException("Player data does not match the active character.");
        var block = memory.Read(data + 0x40, 0x58);
        int Get(int offset) => BitConverter.ToInt32(block, offset - 0x40);
        var values = new CharacterValues(Get(0x94), Get(0x40), Get(0x48), Get(0x58), Get(0x60), Get(0x68), Get(0x70));
        try { values.Validate(); }
        catch (ArgumentException) { throw new IOException("Character values failed validation."); }
        var level = Get(0x90);
        if (level is < 4 or > 544) throw new IOException("Blood Level failed validation.");
        return new(data, level, values);
    }
    public void SetEchoes(int value)
    {
        if (value is < 0 or > 999999999) throw new ArgumentException("Blood Echoes must be between 0 and 999,999,999.");
        ValidateImage();
        var character = ReadCharacter();
        EnsureSameCharacter(character);
        memory.SetInt32(character.Address + 0x94, value);
        if (memory.Int32(character.Address + 0x94) != value) throw new IOException("Echoes changed during the write. Check the game before retrying.");
    }
    public void SetAttributes(int[] attributes, CharacterState expected)
    {
        if (attributes.Length != 6 || attributes.Any(x => x is < 1 or > 99)) throw new ArgumentException("Every attribute must be between 1 and 99.");
        ValidateImage();
        var current = ReadCharacter();
        if (current.Address != expected.Address || current.Level != expected.Level || !current.Values.Attributes.SequenceEqual(expected.Values.Attributes))
            throw new IOException("The character changed after loading the editor. Reload current stats before applying.");
        var level = current.Level + attributes.Sum() - current.Values.Attributes.Sum();
        if (level is < 4 or > 544) throw new ArgumentException("The resulting Blood Level must be between 4 and 544.");
        // One data write preserves the intervening fields and excludes echoes.
        var block = memory.Read(current.Address + 0x40, 0x54);
        int[] offsets = [0, 8, 0x18, 0x20, 0x28, 0x30];
        for (var i = 0; i < offsets.Length; i++) BitConverter.GetBytes(attributes[i]).CopyTo(block, offsets[i]);
        BitConverter.GetBytes(level).CopyTo(block, 0x50);
        EnsureSameCharacter(current);
        memory.Write(current.Address + 0x40, block);
        var result = ReadCharacter();
        if (result.Level != level || !result.Values.Attributes.SequenceEqual(attributes)) throw new IOException("Stats changed during the write. Reload current stats and check the game.");
    }
    private void EnsureSameCharacter(CharacterState expected)
    {
        if (ReadCharacter().Address != expected.Address) throw new IOException("Active character changed. No changes applied.");
    }

    public static ulong FindImage(IMemory memory, IEnumerable<ulong> bases)
    {
        var matches = new List<ulong>();
        foreach (var candidate in bases)
        {
            try
            {
                if (Fingerprints.All(f => memory.Read(candidate + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes))) matches.Add(candidate);
            }
            catch (IOException) { }
        }
        return matches.Count == 1 ? matches[0] : throw new IOException("No unique supported Bloodborne 1.09 image found. Load the game; conflicting patches or another build may be unsupported.");
    }
    public void ValidateImage()
    {
        if (Fingerprints.Any(f => !memory.Read(imageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes)))
            throw new IOException("Game instructions changed. Disconnect and close other trainers.");
    }
    public PlayerState ReadPlayer()
    {
        var world = memory.Pointer(imageBase + WorldPointer);
        var actor = memory.Pointer(world + 0x60);
        var modules = memory.Pointer(actor + 0x3B0);
        var component = memory.Pointer(modules + 0x20);
        if (memory.Pointer(component + 8) != actor) throw new IOException("Player component owner does not match.");
        var block = memory.Read(component + 0xF8, 0x44);
        var hp = BitConverter.ToInt32(block, 0);
        var maxHp = BitConverter.ToInt32(block, 4);
        var stamina = BitConverter.ToInt32(block, 0x3C);
        var maxStamina = BitConverter.ToInt32(block, 0x40);
        if (maxHp is < 1 or > 100000 || hp < 0 || hp > maxHp || maxStamina is < 1 or > 10000 || stamina < 0 || stamina > maxStamina)
            throw new IOException("Player values failed validation. Wait until your character is loaded.");
        return new(actor, component, hp, maxHp, stamina, maxStamina);
    }
    public void SetHealth(int value)
    {
        ValidateImage();
        var player = ReadPlayer();
        if (value < 1 || value > player.MaxHealth) throw new ArgumentException($"Health must be between 1 and {player.MaxHealth}.");
        EnsureSamePlayer(player);
        memory.SetInt32(player.Component + 0xF8, value);
    }
    public void KeepHealthFull()
    {
        var player = ReadPlayer();
        if (player.Health > 0 && player.Health < player.MaxHealth) SetHealth(player.MaxHealth);
    }
    public void SetInfiniteStamina(bool enabled)
    {
        ValidateImage();
        var player = ReadPlayer();
        if (!enabled) { RestoreStamina(); return; }
        if (staminaOwner != 0 && staminaOwner != player.Component)
        {
            // Do not write an old component after a character/world transition.
            staminaOwner = 0;
            throw new IOException("Player changed. Infinite stamina stopped; enable it again after loading.");
        }
        var flags = memory.Read(player.Component + 0x200, 1)[0];
        if (staminaOwner == 0) originalStaminaBit = (byte)(flags & 0x80);
        EnsureSamePlayer(player);
        memory.Write(player.Component + 0x200, [(byte)(flags | 0x80)]);
        staminaOwner = player.Component;
    }
    public void RestoreStamina()
    {
        if (staminaOwner == 0) return;
        var current = ReadPlayer();
        if (current.Component != staminaOwner) { staminaOwner = 0; return; }
        var flags = memory.Read(staminaOwner + 0x200, 1)[0];
        EnsureSamePlayer(current);
        memory.Write(staminaOwner + 0x200, [(byte)((flags & ~0x80) | originalStaminaBit)]);
        staminaOwner = 0;
    }
    private void EnsureSamePlayer(PlayerState expected)
    {
        var current = ReadPlayer();
        if (current.Actor != expected.Actor || current.Component != expected.Component)
            throw new IOException("Player changed during the operation. Try again after loading.");
    }
}
