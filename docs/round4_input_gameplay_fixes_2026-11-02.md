# Round 4 — real input and gameplay path

Rules: [gameplay_logic_spec.md](gameplay_logic_spec.md), especially §§2–3, 5, 8 and 11. Starting revision: `a23c150`. Unity `6000.4.6f1`, `DISPLAY=:5`, graphics enabled throughout; one Editor process at a time. No test was removed, ignored, filtered out of final verification, or given a weaker outcome assertion.

## Root causes and fixes

| Failure | Root cause | Correction |
| --- | --- | --- |
| `AimFirePresentationSettingsValidatedFillsDefaults` | `Validated()` rejected only negative durations. The zero-initialised struct retained a zero-second AimEnter fallback. | Zero, negative and non-finite values use the existing `0.35s` default; valid positive values survive. Added boundary regressions. This affects presentation timing, not the authoritative aim/shot clock. |
| Three SemanticVfxDirector event tests | The EditMode fixture configured an inactive runtime MonoBehaviour, then expected `OnEnable` to subscribe when activated. Runtime-only MonoBehaviour lifecycle callbacks do not run there. Direct `NotifyShotFired()` worked, explaining the misleading partial pass. | Activate first, then explicitly call the public `Configure()` composition API. The original impact/default cue, receiver override and exactly-once breakage assertions remain intact. Added a reconfiguration regression proving the old event hub is detached and breakable subscriptions do not duplicate. No `ExecuteAlways` or editor-side effect playback was added. |
| Keyboard stand, floor movement, blanket pickup and desk-lamp toggle | The synthetic keyboards used the default Input System policy (`PointersAndKeyboardsRespectGameViewFocus` and `ResetAndDisableNonBackgroundDevices`). A batch Test Runner has no focused Game View, so the input never reached the gameplay actions. | The four keyboard integration fixtures share a scoped focus-policy setup: `IgnoreFocus` plus `AllDeviceInputAlwaysGoesToGameView`, restored after each test. They still inject KeyboardState events through the real Input System, PlayerInputRouter, seated presenter, motor and interaction/handoff code. The shipped focus policy and production action maps are unchanged. Floor-test teardown now removes its device and unloads its room even on assertion failure. |
| D21 pickup chooses a vase instead of floor loot | Pickup ranking used chest-to-collider-centre 3D distance after the range check. A farther tabletop vase could beat a directly faced floor prop because the prop was below chest height. | Explicit `PickUp` scans rank proximity on the floor plane, as facing already does. The chest sphere remains `1.2m`; this does not extend reach. Mixed Use/Carry/Climb scans retain their 3D ranking, so the blanket still wins over its co-located bed. Added a regression for floor-vs-tabletop selection and rejection beyond the chest sphere. |
| D21 loot has no clear floor pickup approach | The drop followed the killer root's stale entry heading. With the MV furniture, both weapon-mounted release sockets could spill into the narrow bed/nightstand gap. Their nearly coincident sockets also received identical linear velocity. | Release remains at the actual weapon sockets with dynamic rigidbodies and spin. Direction now follows the player's approach; opposite lateral impulses reuse the existing per-prop offsets to separate the choices. Added both killer-facing directions as regressions, including coincident sockets and the requirement that pickup remains manual. The passive-drop/no-attack and reset contracts still pass. |

The untouched full baseline was **181/185 EditMode** and **132/138 PlayMode**. The six PlayMode failures were the four input symptoms (with blanket acquisition in two tests), plus `ProductionPresentationRepairTests.ProductionD21DoesNotOpenLootUntilFallSemanticEvent`. The original failure at the blanket hid the later D21 targeting and physical accessibility failures; these were fixed after input started reaching the room.

## Test fixture corrections against the rules

The VFX and keyboard changes repair setup, not expected gameplay. No asserted rule was changed.

The older production D21 presentation fixture placed the killer one metre north of the player's current spawn. Round 2 moved that spawn onto the north-wall chair, putting this staged encounter and its dropped loot outside the room. It also copied the airborne pistol's Y coordinate into the player's teleport. The fixture now stages contact at the authored door trap, at floor height, consistent with §8's doorway blanket encounter and §2's seated start. It retains its fall timing, Counterplay, manual pistol choice, D22 and cancellation assertions.

Encounter fixtures let tossed props settle, then choose a floor approach that has capsule clearance and whose real scanner selects the desired prop. They do not move loot, expand range, force acquisition or substitute a capability call for the original input. This handles moving/spinning props and competing loot without assuming a fixed side is usable. The helper fails if no clear selectable approach exists; it exposed the bed/nightstand drop bug before the runtime correction.

The new `KeyboardWalksFromSeatedStartToBlanketAndLampThenRunsNaturalDeathAndReset` exercises a continuous production-scene route: first movement stands the player during Awakening and moves them, WASD reaches the bed, held E acquires the blanket, held E puts it down, WASD crosses the room, short E turns off the lamp without picking it up, and movement returns to clear floor. It then waits exposed for the real entry timer and live hunt, observes Breach → Counterplay → KillImpact → LoopReset → Awakening, checks the impact hold, same loaded room/camera, restored lamp and seated player, then stands and moves again using the same keyboard. It neither teleports nor calls private presenter methods or injects facts/phases/kills. The loose bat can physically support the player near the lamp, so floor-height verification occurs after walking back to clear floor. The original seated E-input/reset, idle grounded pose, floor collision and mixed-target regressions remain enabled.

## Verification and evidence

Full EditMode: **191/191 passed**, **0 failed, 0 skipped**. The six added cases are five aim-duration boundaries/preservation cases and one VFX subscription case.

Test inventory comparison retains all **185 original EditMode** and **138 original PlayMode** cases. PlayMode adds the continuous keyboard route, one pickup-ranking/range case and two drop-direction cases.

Final full PlayMode: **142/142 passed**, **0 failed, 0 skipped**, including the extended continuous keyboard → natural death/reset → next-loop input test (**36.48s**). The first full green run, before extending that route through natural death/reset, was also **142/142**. Final logs and the gameplay capture contain **zero C# compilation errors and zero shader compilation errors**. All originally failing cases pass; no remaining test failures.

Evidence under `/workspace/ref/`:

- `round4_initial_editmode.xml`, `.log`: untouched four-failure EditMode baseline.
- `round4_initial_playmode.xml`, `.log`: untouched full PlayMode baseline, six failures.
- `round4_input_playmode.xml`, `.log`: keyboard input reaches production gameplay; later failures become observable.
- `round4_diagnostics_playmode.xml`, `.log`, `round4_grab_playmode.xml`, `.log`: actual collider positions, competing vase/loot, invalid north-wall staging and bat support.
- `round4_reachable_playmode.xml`, `.log`: no clear pickup approach before correcting the runtime toss.
- `round4_loot_playmode.xml`, `.log`: **9/9**, including flashlight choice → three QTE windows → dislodge → manual recovery → block-light → backup fire → actual epoch reset; pistol choice/D22 shot and empty click; D22 timeout; lamp restore; passive drop physics.
- `round4_final_editmode.xml`, `.log`: full final EditMode.
- `round4_full_playmode.xml`, `.log`: first full green PlayMode, **142/142**.
- `round4_final_playmode.xml`, `.log`: final full green PlayMode, **142/142**, including the uninterrupted natural core loop and next-loop input. Also covers the existing live-hunt flow, seated reset, door/navigation, endings and Round 3 production presentation tests.
- `round4_capture.log`, `round4_progress.png`: actual `MvMainShotBuilder.CaptureGameplay` output, **1920×1080**, visually inspected against `mv_round3_lit.png` and `art/reference/mv_frames/mv_t0006.0s.jpg`. The seated pose/grounding, full left bookcase and right doorway, wallpaper spacing, warm amber light, dark ceiling, green floor, vignette and grain retain the established MV shot. No scene, camera, mesh, material, light or post-processing asset changed in this round.

All commands use the requested Editor directly. Full suite commands have no `-testFilter` and no `-nographics`:

```bash
DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -runTests -testPlatform EditMode \
  -testResults /workspace/ref/round4_final_editmode.xml \
  -logFile /workspace/ref/round4_final_editmode.log

DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -runTests -testPlatform PlayMode \
  -testResults /workspace/ref/round4_final_playmode.xml \
  -logFile /workspace/ref/round4_final_playmode.log

DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game \
  -executeMethod ConGame.Editor.MvMainShotBuilder.CaptureGameplay \
  -mvScreenshot /workspace/ref/round4_progress.png \
  -logFile /workspace/ref/round4_capture.log
```

The encounter fixtures stage their combat setup; these results do not claim an uninterrupted natural-input playthrough of every D01–D22 route or new human playtest acceptance.
