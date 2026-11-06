# Round 8 — exposed and sofa-rear natural-input routes

Gameplay authority: [gameplay_logic_spec.md](gameplay_logic_spec.md) §§3–4, 7–8 and 11. Starting revision: `14123d2`. Read the Round 4 and Round 6 input records before extending their production fixture. Opened the real MV frames at 6, 102 and 192 seconds; 102 seconds is lit, so also inspected the dark frame at 108 seconds.

## Production routes

Three new `PlayerSeatedPosePresenterTests` cases run in a fresh production `Room_M1`:

- **D01:** WASD leaves the wooden chair and stops empty-handed on open floor. The first meaningful input sets the real entry deadline to 30 seconds. The actual committed D01 shot preserves the exposed player position and does not start a paired execution.
- **D03:** WASD walks around the sofa to its rear zone and waits standing. Natural entry, hunt and committed shot select D03.
- **D03P:** The same walk reaches the rear; S approaches and faces the backrest, the real scanner selects the sofa, and short C enters its Crouched hide. The killer must enter Searching before discovering and shooting the player with D03P.

Each route checks the actual outcome, exactly one epoch-matched shot, the readable impact hold, one reset of the existing scene, the unchanged fixed camera, the restored chair pose and input in the next loop. D03P also checks that hiding ends on reset. Movement, stance and interactions use Input System keyboard events; entry, search, aim, shot and reset use production controllers and timers. The tests do not teleport, set facts or poses, request phases, inject kills or accelerate clocks.

## Sofa hide correction

The original natural D03P attempt reached the hide but never committed an attack within its observation window. Search tracing showed the authored `TA_SofaCheck` at `(-0.63, 0, 1.50)` samples to ground navigation at approximately `(-0.33, 0.07, 1.50)`. The old hidden-player root at `(-1.61, 0, 2.06)` was about **1.40 m** away even at that destination, outside the existing **1.35 m** reveal radius. The narrow gap behind the sofa has no baked ground route closer to the old hide point.

The hide anchor now places the crouched player behind the right side of the backrest at `(-1.15, 0, 2.10)`. Its horizontal separation from the reachable search point is **1.013 m**, leaving clearance for the killer's **0.28 m** stopping distance inside the existing reveal limit. Only this anchor's local position changes in the scene. The MV layout builder uses the same world position when rebuilding; `RepairSofaHideAnchor` applies the narrow correction through the Editor, validating ground height, doorway path completeness and reveal clearance before saving.

The corrected D03P route finishes in about **66 seconds** from scene setup. Its observer allows 75 seconds after preparation so search revisits, the shot, the full impact hold and reset are all observed; production timing remains authoritative.

## Verification

Unity `6000.4.6f1`, `DISPLAY=:5`, graphics enabled, one Editor at a time.

- D01: **1/1**, `/workspace/ref/round8_d01.xml` and `.log`.
- Standing D03: **1/1** within `/workspace/ref/round8_sofa.xml`; that initial two-case run also recorded the unresolved D03P hide approach. The later D03P result below supersedes it.
- Corrected D03P, short-hide/exit and all tactical-anchor navigation: **3/3**, zero failed or skipped, `/workspace/ref/round8_sofa_verified.xml` and `.log`.
- Narrow Editor repair: `/workspace/ref/round8_sofa_hide_repair.log`, successful path/reveal validation.
- Full EditMode: **191/191**, zero failed or skipped, `/workspace/ref/round8_full_editmode.xml` and `.log`. All 191 Round 7 cases remain present.
- Full PlayMode: running; final result pending.

The successful targeted runs, Editor repair and full EditMode suite have zero C# or shader compilation errors. Other D01–D22 natural-input routes, human discovery acceptance and detailed animation/MV matching remain open.

Reproduction (the full-suite commands have no test filter):

```bash
DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game \
  -executeMethod ConGame.Editor.MvMainShotBuilder.RepairSofaHideAnchor -quit \
  -logFile /workspace/ref/round8_sofa_hide_repair.log

DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -runTests -testPlatform EditMode \
  -testResults /workspace/ref/round8_full_editmode.xml \
  -logFile /workspace/ref/round8_full_editmode.log

DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -runTests -testPlatform PlayMode \
  -testResults /workspace/ref/round8_full_playmode.xml \
  -logFile /workspace/ref/round8_full_playmode.log
```
