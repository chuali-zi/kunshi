# Round 16 — natural bookcase route

Starting revision: `1a19f6a`. Read README, the gameplay logic spec, real-frame shot list and Round 15 acceptance. Opened the actual 6s, 102s and 192s MV frames. The 102s image is lit; approximate dark timestamps in the shot list remain historical guidance, not frame evidence. No art was changed in this first step.

## Step 1: keyboard branch and reset

Added `KeyboardTipsBookcaseAndNaturallyFinishesD13ThenRestoresRoom` to the existing production input fixture. From the seated start it walks through open foreground to the bookcase's east side, faces west, releases E before two seconds to verify cancellation, then holds E for the complete push and waits for the controlled tip. It retreats to exposed floor and lets the real noise/entry clock, live hunt, committed D13 shot, death hold and same-scene reset complete. It checks the zero extra breach delay, bookcase transform/fact restoration, seated player, fixed camera and next-loop keyboard movement.

The route uses actual keyboard events without teleporting actors, injecting facts, forcing entry or requesting a kill. Earlier approaches selected loose books, the strike-wall affordance or the table lamp; the final route checks the same mixed scanner used by E, rather than relying on a verb-specific query.

Unity `6000.4.6f1`, `DISPLAY=:5`, batchmode with graphics enabled. Targeted PlayMode **1/1 passed**, zero skipped and zero C# / shader compile errors. Evidence: `/workspace/ref/round16_d13_west.log` and `.xml`.

This first step accepted the natural D13 branch, not its complete presentation. Inspection found that the saved tipping handler had no doorway reference and the killer still walked into the room and used ordinary pursuit.

## Step 2: door gap and exterior shot

The breach now captures the bookcase block and limits opening to the scene-configured 30° gap, without adding an ordinary furniture delay. The killer walks to `KillerEntryDoorGap`, outside the right wall, and holds position during the hunt. Navigation cannot warp it inside or pursue through the blocked doorway. Perception, physical chest/head rays, detection, shooting range, aim cancellation, phase confirmation, delayed hit and reset retain the ordinary tactical lifecycle. Higher-priority intents can still win before commit. Ordinary breaches retain their full opening and navigation.

An idempotent `ConGame.Editor.MvMainShotBuilder.RepairBookcaseDoorGap` entry wires the new marker and the tipping handler's actual doorway reference through Unity. The main-shot builder repeats the same wiring. The scene changes are limited to those references, the marker and the configurable gap angle.

The natural route now also checks the exterior position and actual door angle at the D13 shot, then the restored door after reset. Added regressions cover a gap followed by an ordinary breach and a stationary killer that cannot shoot beyond range or through an obstacle, but acquires and fires after sight clears. The latter allows a complete authored search sweep before requiring acquisition.

Targeted PlayMode **6/6 passed**, zero failed/skipped and zero C# / shader compile errors: `/workspace/ref/round16_d13_gap_verified.log` and `.xml`. The repair entry also compiled with zero errors: `round16_d13_gap_wiring.log`.

The current left-side bookcase's physical fall still does not visually reach the distant right doorway. The direction gate and exterior gun-gap behavior are functional, but this spatial choreography needs further design/art work before calling D13 completely accepted. No cabinet teleport, new carry verb or initial MV layout change was introduced here.

Baseline at handoff: Round 15 full EditMode **191/191** (`round15_full_editmode_verified.xml`). Round 15 full PlayMode produced no XML and is not a verified result; the last verified full PlayMode is Round 10 **157/157**. Full natural D21 and remaining MV/animation acceptance remain open. All pre-existing untracked `InitTestScene*.unity` and `.meta` files are preserved and excluded from commits.
