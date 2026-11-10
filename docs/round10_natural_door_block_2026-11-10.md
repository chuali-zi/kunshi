# Round 10 — natural chair door-block route

Gameplay authority: [gameplay_logic_spec.md](gameplay_logic_spec.md) §§3–4, 7–8 and 11. Starting revision: `f8d20b6`. Read README, the Round 6/8/9 handoffs and the real-frame shot list; opened the MV frames at 6, 102, 108 and 192 seconds. The 102-second frame is lit; 108 seconds supplies the dark reference.

## Repository cleanup

Removed the 18 already-deleted tracked Python bytecode files and added `__pycache__/` to `.gitignore`. Verified the ignore rule and staged diff before committing and pushing `4643461`.

## D09 furniture door block

`KeyboardCarriesChairToBlockDoorAndNaturallyFinishesD09ThenRestoresRoom` runs in a fresh production `Room_M1`. WASD leaves the awakening chair, approaches it through the gap beside the bed, and held E carries it. WASD returns across open floor to the right doorway; short E places the chair in the real door-block socket. The test requires the scanner to select the chair and placement target, the actual socket occupancy, Carrying pose, Furniture fact and three-second breach extension.

The player returns to open floor and waits through the production entry deadline. The real breach must use its normal duration plus three seconds. Live hunt must release input, commit D09 and fire exactly one epoch-matched shot. The observer checks the impact hold, one reset of the same loaded room, fixed camera and seated awakening. The socket, block fact and delay must clear, the chair must restore its authored position and rotation, the breach duration must restore, and the next movement key must stand the player.

Movement and interaction use the production Input System; no teleport, fact/pose injection, forced phase, synthetic kill, frame-rate cap or accelerated clock is used.

## Carry movement correction

The natural route exposed a frame-rate-dependent movement failure. In the uncapped batch Editor, observed motor frame deltas were roughly `0.0005s`. At the chair's required half speed (`1.4m/s`), a planar step is only `0.0007m`, below the CharacterController's authored `0.001m` minimum. Many steps were discarded: a three-second return walk moved only about 0.6–0.8 metres. Collision tracing found only the floor; the held chair's collision pair with the player was correctly ignored.

`PlayerMotor.Awake` now sets `CharacterController.minMoveDistance` to zero so small steps survive. Walking/carrying speed, input, collision geometry and gameplay timers retain their authored values. Temporary tracing was removed. The chair approach follows the clear gap at x = -0.35; a straight northward approach at x = 0 intersects the bed collider.

## Breach reset correction

After movement was corrected, the full D09 shot/reset route reached its final duration assertion: the door reset to `0.8s` rather than its serialized `0.9s`. `RoomDoorBreachController` initialized its private base duration with the code default and only refreshed it in `Configure`, which scene deserialization does not call. Its Awake now captures the authored duration. Explicit Configure calls retain their existing behavior.

## Verification

Unity `6000.4.6f1`, `DISPLAY=:5`, graphics enabled, one Editor at a time.

- D09 plus existing door-breach regression: **2/2**, zero failed or skipped, zero C# or shader compilation errors. D09 took about **40 seconds**. Evidence: `/workspace/ref/round10_d09_verified.xml` and `.log`.
- Full EditMode: **191/191**, zero failed or skipped, zero C# or shader compilation errors. Evidence: `/workspace/ref/round10_full_editmode.xml` and `.log`. All Round 8 cases remain present.
- Full PlayMode is pending. Previous full baseline: **154/154**; Round 9 added two targeted-passing routes and Round 10 adds D09.

Reproduce the targeted route:

```bash
DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -runTests -testPlatform PlayMode \
  -testFilter 'KeyboardCarriesChairToBlockDoorAndNaturallyFinishesD09ThenRestoresRoom;RoomDoorBreachPlayModeTests' \
  -testResults /workspace/ref/round10_d09_verified.xml \
  -logFile /workspace/ref/round10_d09_verified.log
```

D08, D13, D16 and a complete natural-input D21 route remain open, alongside human discovery acceptance and detailed paired-animation/MV matching. This increment does not establish acceptance of those routes or final furniture-placement visuals.
