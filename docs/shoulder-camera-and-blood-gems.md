# Shoulder camera and blood gems — v0.6.7

Implemented after the community requests selected on 26 October 2026. Both
features need in-game confirmation on Bloodborne PC and shadPS4. Both controls
show a persistent **Not tested in-game** label in version 0.6.7.

## Shoulder camera

Open **Camera → Position & distance**, enable the camera override, then adjust
**Shoulder offset**. Negative values move left; positive values move right.
The range is −2 to +2 metres in 0.05-metre steps. **Centre shoulder camera**
returns the horizontal setting to zero. Distance and height remain independent.
Saved combat presets, including a selected startup preset, retain the offset.
Older presets default to zero.

The normal camera's freshly composed frame is shifted along its local right
vector. The offset does not accumulate and does not move the hunter. Zero,
disabling the override, stopping features and normal disconnect restore the
original instructions. The existing camera-instability warning still applies;
large offsets can clip scenery. Hidden freecam and photo tools remain disabled.

Test left/right at several angles, lock-on, stairs, narrow rooms, cutscenes,
loading, death and warp. Verify centre, disable, disconnect, reconnect, saved
presets and startup presets. Confirm that other camera mods are rejected when
they replace the same instructions.

## Blood-gem editor and spawner

The unreleased overhaul replaces the setup below with a built-in catalog,
Normal/Advanced modes and saved recipes. See [the current development guide](gem-catalog.md).
The following instructions describe the released 0.6.7/0.6.8 interface.

1. Load a living character, connect, then open **Items → Blood gems**.
2. Click **Refresh gems & effects**. A blood-gem template ID is selected
   automatically from the loaded game. Leave it as selected unless loading an
   existing gem or deliberately choosing another valid GemGenParam ID.
3. Optionally **Import effect names from game files** and select the Bloodborne
   game folder or `dvdroot_ps4`. Descriptions are read from the local English
   `menu.msgbnd.dcx` and saved in the local library. No game text is bundled.
4. Choose Radial, Triangle, Waning, Circle or Droplet. Search the effect picker
   by name or decimal **GemeffectParam ID**, select a slot number, then click
   **Set selected slot**. Six effect slots are available; `-1` means empty.
   At least one effect is required. Ordinary item IDs and SpEffectParam IDs are
   different ID spaces and must not be substituted for gem effect IDs.
5. Click **Create blood gem**. Success is reported only when the newly allocated
   handle, source, shape and all six effects match a carried inventory gem.
   Equip it using the game's workshop.

Select a carried gem and **Load gem into editor** to use its current shape and
effects as a starting point. Creating an edited copy preserves the original;
this version does not overwrite equipped gems or edit storage in place.
Caryll runes are excluded. Custom gems persist in the game save.

If inventory readback cannot confirm the grant, the tool reports the uncertainty
and never repeats the request automatically. Check inventory, storage and
capacity before retrying. A timeout also does not automatically repeat a grant.

For the first test, use one ordinary positive effect. Verify its displayed
property and actual weapon behavior, then test multiple effects, an empty slot
between two populated slots, a curse/negative property, all five shapes, loading
an existing chalice gem, and save/reload. Check inventory-full handling and
character changes during a request. Report the runtime and session log for any
failure. Automated native fixtures do not establish these in-game results.

## Implementation evidence

The camera hook replays the 19-byte instruction span at image+`0x193BEAD`, shifts
the renderer output at frame+`0x40` along frame+`0x10`, and checks the current
actor, active source frame and native debug-camera mode. Scratch registers and
flags are restored. Hook restoration preserves foreign replacements.

Gem generation uses the native constructor at image+`0x1A87E20`, award at
`0x17D89F0` and temporary-reference release at `0x1A8BC00`, through the existing
game-thread queue. GemGenParam and GemeffectParam repository holders are 42 and
43; resource names, types, row bounds and instruction signatures are checked.
Pointer-bearing payloads resolve while threads are paused immediately before
publication. Runtime gem instances use shape+`0x1C` and six effects+`0x20`;
serialized save offsets are not used for live memory.

Regression checks execute the generated helpers in synthetic Windows processes,
including rotated camera basis, no drift, register/flag preservation, ownership
changes, failed allocation, native argument/ownership handling, parameter
validation and inventory readback. Private 1.09 instruction and message-file
checks are read-only; neither game assets nor third-party editor code are shipped.
