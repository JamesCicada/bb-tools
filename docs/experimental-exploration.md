# Experimental photo mode and flight/noclip — v0.6.8

These tools are **not tested in-game** on Bloodborne PC or shadPS4. They are
hidden by default. Automated checks do not establish that the previous hunter
movement, HUD or collision reports are resolved in real gameplay.

## Enable and use

1. Open **Settings → Experimental** and enable the camera/noclip option.
   This reveals controls; it does not start a mode. No external patches or
   add-ons are needed. Other camera tools can conflict with the same native state.
2. **Camera → Photo studio:** start photo mode with a living character loaded.
   Hunter locking and HUD hiding default on. Move with WASD, rise/descend with E/Q,
   turn/tilt with arrows, roll with Z/C; Shift accelerates and Alt slows movement.
   Home resets the view, Escape exits, and the configured capture key saves PNG.
   Input is read only while the connected game is foreground.
3. Rebind any of the fifteen actions under **Settings → Experimental → Camera
   keyboard controls** and save. Photo speed, turn rate, zoom, capture folder
   and saved-image filters remain in Photo studio. Filters affect saved copies,
   not the live renderer; capture uses the game's displayed resolution.
4. **Camera → Movement:** enable flight or noclip; noclip includes flight.
   Hold Ctrl plus the configured movement keys while the game is focused.
   Return to the activation point with the return button. Return points are
   restricted to the same character/actor handle and area.
5. Exit photo mode explicitly or navigate away to restore normal controls.
   Turning off the Experimental preference stops active modes and hides them.
   Cleanup retries continue if loading temporarily prevents restoration.

Photo mode and flight are mutually exclusive. Native item/menu/warp/multiplier
requests require exiting photo mode before submission, because freezing hunter logic can
prevent the existing player-thread dispatcher from running. Photo mode does not
freeze the whole world or enemies. Do not assume the hunter is invulnerable.

## What changed

- The renderer borrows the private photo frame through an owned, reversible read
  hook at `0x193BEC5`. The native controller’s owning `+0xD0` pointer is never
  replaced: its destructor frees that pointer via the game allocator. This avoids
  handing tool-allocated memory to the game’s destructor during loading. Native
  mode/hold fields remain unchanged; actor/source checks select the photo frame.
- Hunter lock supplements the existing owned movement hook with native controller
  `EnableLogic` bit zero at `ChrCtrl+0x110`, the movement gate at `+0x111`, the
  existing actor event flag and the owned no-gravity bit. Capture and restoration
  preserve unrelated bits and do not write through replacement player modules.
- The native EnableLogic writer is independently fingerprinted at `0x132E4F4`
  in the private 1.09 image; no instructions from another game's build are used.
- Photo HUD cleanup restores its captured baseline, including an originally
  hidden HUD. The explicit Restore HUD button remains a separate recovery action.
- Camera preparation is cancellable on exit/opt-out. A camera, actor, resource
  module or map change stops movement through the captured context.
- Flight checks actor handles as well as addresses, captures a fresh anchor per
  activation, validates returns and accounts for dispatcher latency in movement
  timing. Actual wall traversal still needs live confirmation.

## Reference review

The review used primary project sources for patterns rather than copying their
game offsets or implementations:

- [FreecamMod camera lifecycle](https://github.com/Logersnamed/FreecamMod/blob/master/src/core/free_camera.cpp)
  and [game-state manager](https://github.com/Logersnamed/FreecamMod/blob/master/src/core/features/game_state_manager.cpp):
  separate player freezing, camera movement and restoration of graphics options.
- [ERTool release history](https://github.com/kh0nsu/EldenRingTool/releases):
  its 1.5.8.2 notes identify keyboard/mouse cleanup after leaving the game world
  as a real freecam/noclip failure mode.
- [bbhost character-controller layout](https://github.com/droogie/bbhost/blob/main/include/bbhost/engine/sprj/chr_ins.hpp):
  cross-check of native movement/logic/collision flags, independently inspected
  in the local Bloodborne 1.09 image.

## Live test checklist

Check opt-in persistence with all modes still off at restart; keyboard and gamepad
movement while hunter lock is on; unlocking and relocking; HUD initially visible
and hidden; exit, page navigation, opt-out, death, loading, area travel and normal
app close. Verify that restored movement speed and unrelated combat flags keep
their prior values. Test noclip against walls/floors, fresh return points, map
changes and character reloads. Include runtime/build, action sequence and session
log when reporting a failure. Keep the testing status pending until confirmed.
