# Round 11 — natural door-blind ambush

Gameplay authority: [gameplay_logic_spec.md](gameplay_logic_spec.md) §§3–4, 7–8 and 11. Starting revision: `ba2294f`. Read README, the Round 6/8/10 handoffs and real-frame shot list; opened the actual MV frames at 6, 102, 108 and 192 seconds. The 102-second frame is lit; 108 seconds supplies the dark reference.

## D08 production route

`KeyboardAmbushesFromDoorBlindAndNaturallyFinishesPairedD08ThenResets` starts in a fresh production `Room_M1`. WASD leaves the awakening chair, walks to the bat, E picks it up, and WASD reaches the DoorBlind trigger by the right doorway. The test waits for natural entry to release live-hunt input, then a real left mouse click launches D08. It requires exactly one confirmed paired launch, active root locking and a dropped bat.

With no further E presses, the actual QTE fails. The test requires the strike, paired contact, failed grapple, detach and point-blank shot semantic events, and forbids the success loot window. It checks the impact hold, one epoch increment, D08 death outcome, the same loaded room and fixed camera, released animation graph/root locks, restored authored bat position, seated awakening and movement in the next loop. No teleport, fact/pose injection, forced phase, synthetic kill or accelerated clock is used.

## Held-item ownership correction

The first natural run reached D08 but failed because the paired player always announced `Preserve`, leaving the bat in the player's hand throughout the grapple. The paired launch now explicitly applies `DropAtStart` to D08/D21, matching their existing performance declarations, and retains `Preserve` for D11. It uses the existing atomic `ForceDropHeld` path and reports the applied policy to the runtime adapter.

## Verification

Unity `6000.4.6f1`, `DISPLAY=:5`, graphics enabled, one Editor at a time.

- Corrected natural D08: **1/1**, zero failed/skipped, zero C# or shader compilation errors, approximately **45 seconds**. Evidence: `/workspace/ref/round11_d08_verified.xml` and `.log`.
- Initial failure retained in `/workspace/ref/round11_d08.xml` and `.log`.
- Full-suite baseline before this increment: EditMode **191/191**, PlayMode **157/157** (Round 10). Full-suite rerun is pending subsequent increments.

Reproduce:

```bash
DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -runTests -testPlatform PlayMode \
  -testFilter KeyboardAmbushesFromDoorBlindAndNaturallyFinishesPairedD08ThenResets \
  -testResults /workspace/ref/round11_d08_verified.xml \
  -logFile /workspace/ref/round11_d08_verified.log
```

D08 successful-QTE/pistol pickup, D13, D16 and complete natural-input D21 remain open, alongside human discovery and detailed animation/MV acceptance. Inspection also found the production rug still uses the old fact-only affordance rather than the physical trip controller; repair and natural-input verification are the next increment.
