# Round 16 — natural bookcase route

Starting revision: `1a19f6a`. Read README, the gameplay logic spec, real-frame shot list and Round 15 acceptance. Opened the actual 6s, 102s and 192s MV frames. The 102s image is lit; approximate dark timestamps in the shot list remain historical guidance, not frame evidence. No art was changed in this first step.

## Step 1: keyboard branch and reset

Added `KeyboardTipsBookcaseAndNaturallyFinishesD13ThenRestoresRoom` to the existing production input fixture. From the seated start it walks through open foreground to the bookcase's east side, faces west, releases E before two seconds to verify cancellation, then holds E for the complete push and waits for the controlled tip. It retreats to exposed floor and lets the real noise/entry clock, live hunt, committed D13 shot, death hold and same-scene reset complete. It checks the zero extra breach delay, bookcase transform/fact restoration, seated player, fixed camera and next-loop keyboard movement.

The route uses actual keyboard events without teleporting actors, injecting facts, forcing entry or requesting a kill. Earlier approaches selected loose books, the strike-wall affordance or the table lamp; the final route checks the same mixed scanner used by E, rather than relying on a verb-specific query.

Unity `6000.4.6f1`, `DISPLAY=:5`, batchmode with graphics enabled. Targeted PlayMode **1/1 passed**, zero skipped and zero C# / shader compile errors. Evidence: `/workspace/ref/round16_d13_west.log` and `.xml`.

This accepts the natural D13 branch, not its complete presentation. Inspection found that the saved tipping handler has no doorway reference and the killer still walks into the room and uses ordinary pursuit. A dedicated door-gap behavior and production wiring are the next D13 increment. The current bookcase's physical location/fall also needs visual review against the distant right doorway before full spatial acceptance.

Baseline at handoff: Round 15 full EditMode **191/191** (`round15_full_editmode_verified.xml`). Round 15 full PlayMode produced no XML and is not a verified result; the last verified full PlayMode is Round 10 **157/157**. Full natural D21 and remaining MV/animation acceptance remain open. All pre-existing untracked `InitTestScene*.unity` and `.meta` files are preserved and excluded from commits.
