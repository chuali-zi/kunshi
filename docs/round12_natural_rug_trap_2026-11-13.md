# Round 12 continuation / Round 14 — natural rug trap

Gameplay authority: [gameplay_logic_spec.md](gameplay_logic_spec.md) §§3–5, 7–8 and 11. Starting revision: `5bac49c`. Reviewed README, the real-frame shot list and the actual 6s, 102s and 192s reference images. The 102s file is lit; use a genuinely dark frame for subsequent dark-mode visual work.

## Production wiring

The production rug still used `GameplayAffordanceHandler`, which only set a fact. `MvMainShotLayout` now installs the existing `TripTrapController`, wires its facts and event hub, and routes the rug's Use capability to it. Its authored footprint becomes a trigger with a kinematic, gravity-free Rigidbody, allowing the navigating killer's real collider to register contact. The legacy handler is removed. The saved production scene and layout rebuild use the same configuration; `RepairRugTrap` can apply just this repair.

The first E press arms an empty rug. Pulling requires actual killer contact, the existing 1.6m horizontal range and facing checks, and another player input. D16 remains the stumble route, separate from D21's paired fall and loot.

## Verification and handoff

Unity `6000.4.6f1`, `DISPLAY=:5`, batchmode with graphics enabled. Trap occupancy/explicit pull and production navigation regressions passed **3/3**, zero C# or shader compilation errors (`/workspace/ref/round14_d16_route.log` and `.xml`). The same run's new natural D16 test failed at the approach to the armchair hide; its keyboard path is being corrected before acceptance. No original tests were removed or weakened.

Full-suite baseline remains Round 10: EditMode **191/191**, PlayMode **157/157**. Natural D16 acceptance, D13, complete natural D21 and further MV visual acceptance remain pending until subsequent verification is recorded below.
