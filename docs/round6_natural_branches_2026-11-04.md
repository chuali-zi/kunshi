# Round 6 — natural-input counterplay routes

Gameplay authority: [gameplay_logic_spec.md](gameplay_logic_spec.md) §§2–4, 7–8 and 11. This round continues M2 branch acceptance from the production-input foundation in Rounds 4–5. Starting revision: `affb815`.

The actual reference frames at 6, 102, 108 and 192 seconds were opened and inspected. The 102-second frame is lit; 108 seconds supplies the dark reference. This round focuses on playable branch verification and retains the established room art.

## Production routes

`PlayerSeatedPosePresenterTests` now covers three more routes through `Room_M1`:

- **D07, armed confrontation:** WASD leaves the awakening chair, reaches the bat, E picks it up, and WASD returns to open floor. Natural entry and live hunt commit and fire D07. Death restores the held bat and the seated player; the next movement key stands the player again.
- **D06, surrender:** WASD leaves the chair and faces the right doorway. After natural entry releases input, holding C empty-handed produces Kneeling. Continuous arbitration commits D06, followed by its actual shot, death and reset.
- **D14, playing dead:** WASD reaches open floor, short C crouches and held C produces Prone before entry. Entry arbitration retains D14 through live hunt, shot, death and reset.

The shared observer checks live-hunt input availability, the confirmed attack outcome, exactly one shot with the current epoch, the recorded death outcome, the readable impact hold, exactly one epoch increment, the existing loaded room, fixed camera position/rotation and the seated reset. Both stance routes accept movement and return to Standing in the next loop. Save files, synthetic devices and loaded scenes use the existing fixture cleanup.

All movement, pickup and stance actions go through the production Input System. Entry, detection, aim, shot and reset use the production timers and controllers. These routes do not teleport, set poses/facts, force acquisition, request phases or inject kills.

The first D07 run reached the correct committed shot and reset, but its new bat-position assertion compared against a loose prop after approach physics. The fixture now captures the authored position in `sceneLoaded`, before Start/physics, and observes the restored position at `LoopResetCompleted`, before the next physics step. This preserves the requirement to restore the original position rather than an incidental settled position.

## Playable stance correction

The first combined run passed D07 and D14 but failed D06: holding C toward the entered killer produced Hidden instead of Kneeling. At this position the real scanner also exposes the bed's hide action. `PlayerInteractor.OnStance()` immediately executed Hide on key-down, consuming the input before its 0.6-second surrender hold could start.

Hide now executes when the short stance input is released. Long holds continue through the existing surrender/playing-dead gate; pressing C while already hiding still exits. The D06 regression explicitly requires the competing Hide target to remain scanner-visible, preserving the failing scenario. A fourth new input test checks that the player remains standing while the tap/hold decision is pending, short release enters the bed's UnderBed hide, and another C press exits and returns to the approach position.

This adds automated route acceptance for D06/D07/D14. Natural-input acceptance of the rest of D01–D22, human discovery of the television ending and detailed animation acceptance remain separate work.

## Verification

Unity `6000.4.6f1`, `DISPLAY=:5`, graphics enabled, one Editor process at a time. Targeted D07: **1/1** (`/workspace/ref/round6_d07_verified.xml` / `.log`). The first combined stance run: **2/3**, exposing the D06 bug (`round6_stance.xml` / `.log`). After the runtime correction, D06, D14 and the short-hide/exit regression: **3/3**, **0 failed, 0 skipped**, zero C# and shader compilation errors (`round6_stance_fix.xml` / `.log`).
