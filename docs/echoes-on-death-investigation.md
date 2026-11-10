# Blood Echoes lost on death — yharnam-pc investigation

2026-11-01. Reported with BB Tools 0.6.10 and
[Jgamrot/yharnam-pc](https://github.com/Jgamrot/yharnam-pc), a Supermedo fork.
Exact runtime build, mods/patches and first-versus-subsequent-death behavior were
not supplied. The later 0.7.0 trace confirms the loss but cannot itself confirm a
gameplay fix.

## What the submitted log establishes

- Infinite consumables selected at 16:58:33; keep Echoes selected at 16:58:35.
- A 5x Echoes multiplier applied successfully. Native repair was active.
- At 17:00:07 repair reported a non-living character. At 17:00:12 multiplier
  reapplication reported the same condition. These are action precondition errors;
  they do not show that either feature cleared Echoes protection.
- At 17:00:17 the player/world pointers became unavailable. At 17:00:18 the new
  player loaded, and at 17:00:20 the multiplier attached to its new actor.
- Another loading transition started around 17:00:36. A repair request timed out
  at 17:00:46; the process exited at 17:00:55. The log does not identify the exit's
  cause, and this is not evidence that the lost balance caused a crash.
- No selection-off entry or death-protection validation error appears. The old log
  contains no before/after balances and no readback/reset trace of the protection
  flag, so it cannot prove that protection stayed effective at the deduction.

## Code review

Existing protection originally maintained the game's script-visible death-penalty skip flag,
GameDataMan+0xA8, read by the verified IsDeathPenaltySkip getter. It is not an
interception of every native Blood Echo deduction and does not refund a remembered
balance. Infinite consumables owns a resource-module bit; reward multipliers own a
private SpEffect clone; repair uses a native equipment action. Those operations do
not intentionally share the protection field.

The fork's current public [respawn patch](https://github.com/Jgamrot/yharnam-pc/blob/windows/src/respawn_patch.h)
removes the fixed respawn countdown. That alone does not demonstrate an Echoes
conflict, and the reporter's exact build is unknown. Current source inspection did
not establish a direct overwrite of the skip flag by its cheat handling.

A real ownership race was identified in BB Tools: after resolving the manager and
reading its flag, validation checked the live actor but did not recheck the manager
before writing. Manager replacement could therefore direct a write into a retired
allocation. A synthetic regression reproduces that sequence. This is a verified
code defect, not proof it caused this user's report.

## Hardening and diagnostics in 0.7.0

- Recheck manager identity before acquiring flag ownership and immediately before
  changing its byte. Pause game threads only for an actual change; verify readback
  before resuming. Stable polling does not introduce repeated pauses or writes.
- Use the same guarded path when restoring the captured flag. Retired/replacement
  managers must never be modified during cleanup.
- Log initial protection ownership and any observed flag reset/reapplication.
- Record observed deaths and the first return after death/loading, including last
  observed living and current Echoes balances. Ordinary spending is not a death;
  loading without a sampled death is labeled separately.
- Shared code covers Windows and Linux. Automated checks verify ownership/reset
  behavior and the diagnostic state machine; **yharnam-pc gameplay remains untested**.

## Follow-up evidence

A new session log with the exact yharnam-pc build and a death/respawn reproduction
will distinguish a cleared flag, a manager transition, and loss despite a set flag.
The submitted 0.7.0 session establishes the latter: the flag was reset and
reapplied during death, then a confirmed return reported `79,540 → 0` Echoes.
The pending follow-up holds the verified `IsDeathPenaltySkip` native reader true
for the selection lifetime, rather than relying solely on the transient manager
byte. It also restores the remembered balance only after a confirmed death return
with a lower value. Ordinary spending and a warp/reload cannot qualify for that
recovery. Keep multiplier/durability interactions as hypotheses until reproduced;
live feedback is still required before calling the fix confirmed.
