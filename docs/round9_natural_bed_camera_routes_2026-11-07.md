# Round 9 — natural bed and camera-blind routes

Gameplay authority: [gameplay_logic_spec.md](gameplay_logic_spec.md) §§3–4, 7–8 and 11. Starting revision: `44ffddc`. Read the Round 6 and Round 8 handoffs and opened the actual MV frames at 6, 102, 108 and 192 seconds. The 102-second frame is lit; 108 seconds supplies the dark reference. Room art is unchanged.

## D04 on-bed hide

`KeyboardHidesOnBedAndNaturallyFinishesD04ThenResetsBlanket` walks from the production chair across open floor to the foot of the bed. The normal Hide scanner must select the placed blanket, rather than the bed's UnderBed hide. Short C enters Hidden + Bed; the test waits through physics updates to check that overlapping triggers preserve that semantic location.

The existing production observer verifies natural entry, live-hunt control, the committed D04 attack, exactly one epoch-matched shot, the readable impact hold, one reset of the existing scene, an unchanged fixed camera and the restored chair pose. The blanket remains placed and hide-capable, hiding clears, and S stands the player in the next loop. No teleport, fact injection, forced phase, kill request or accelerated clock is used. No runtime correction was necessary for this route.

## Verification

Unity `6000.4.6f1`, `DISPLAY=:5`, graphics enabled, one Editor at a time.

- D04: **1/1**, zero failed or skipped, zero C# or shader compilation errors. `/workspace/ref/round9_d04.xml` and `.log`.
- Full suites will be recorded after the next small route increment; the preceding Round 8 baseline is EditMode **191/191**, PlayMode **154/154**.

Reproduce the targeted run:

```bash
DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -runTests -testPlatform PlayMode \
  -testFilter KeyboardHidesOnBedAndNaturallyFinishesD04ThenResetsBlanket \
  -testResults /workspace/ref/round9_d04.xml -logFile /workspace/ref/round9_d04.log
```
