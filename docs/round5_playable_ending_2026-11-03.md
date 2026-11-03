# Round 5 — playable ending route and clock continuity

Gameplay authority: [gameplay_logic_spec.md](gameplay_logic_spec.md) §§5.3, 10–11. This round prioritizes the M3 complete-playthrough goal in [game_outline.md](game_outline.md) over further room art. Starting revision: `56ce5f2`.

The real reference JPGs at 6, 102, 108 and 192 seconds were opened and inspected. The supplied 102-second image is lit; 108 seconds shows the dark state. No scene, camera, material, lighting, navigation or reference art changes are included.

## Clock payoff

Normal `ClockTwitchBehaviour` updates previously multiplied another rotation into the hand on every twitch. The supposedly stuck hand therefore advanced throughout ordinary loops. E3 then created an independent `EndingSecondHand` instead of releasing that hand.

The normal hand now alternates between its captured mark and a 1.5-degree displacement, with no accumulated advance. E3 uses `AdvanceForEnding()` on the existing clock controller: each tick advances the original hand by six degrees from its captured mark and stops the stuck-clock animation. A reset clears ending ticks and restores the initial mark. Legacy scenes without the clock controller reuse `SecondHand` when present.

`ClockEndingContinuityTests` verifies bounded twitching, three real ending ticks that survive subsequent updates, no extra hand, and restoration after reset.

## Continuous production route

`KeyboardAndMouseReachTelevisionEndingFromSeatedStart` loads the real `Room_M1` and uses synthetic Keyboard/Mouse events through the production Input System. It walks from the awakening chair to the bat, picks it up, walks to the television and delivers two separate attacks. It then observes the production ending director through E1, E2, E3 and Finished.

Assertions include television damage, first-person takeover, an original-actor clone, actual E2 movement, E3 look-only control and immobility, two ending room presets, the original clock hand's first six-degree advance, final gunshot, unchanged loop epoch, completion saved to disk, and disabled Continue eligibility. Save files are backed up and restored by the fixture; device and room cleanup also run after failures. This route does not teleport the player, force acquisition, call hit receivers, inject phases/facts/kills, or shorten production timers.

Together with Round 4's continuous keyboard route through natural entry, live hunt, death and seated reset, this establishes automated input coverage of both the repeat loop and a route to the saved ending. It does not establish natural-input coverage of every D01–D22 branch, human discovery of the television solution, or visual acceptance of every animation.

## Verification

Unity `6000.4.6f1`, `DISPLAY=:5`, graphics enabled; one Editor process at a time. No tests removed, skipped or weakened.

Full EditMode: **191/191 passed**, **0 failed, 0 skipped**. Full PlayMode: **144/144 passed**, **0 failed, 0 skipped**. All **191 original EditMode** and **142 original PlayMode** cases remain present; the two new cases are the clock regression and continuous input route. Both final logs contain **zero C# compilation errors and zero shader compilation errors**. The PlayMode suite completed in about **400 seconds**.

Logs and NUnit reports are outside the repository under `/workspace/ref/`:

- `round5_clock.log` / `.xml`: targeted clock regression, **1/1**.
- `round5_ending2.log` / `.xml`: continuous production ending route, **1/1**, about **39 seconds** test duration.
- `round5_full_editmode.log` / `.xml`: unfiltered full EditMode suite.
- `round5_full_playmode.log` / `.xml`: unfiltered full PlayMode suite.

Reproduce the full suites (no `-testFilter`, `-nographics` or `-quit`):

```bash
DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -runTests -testPlatform EditMode \
  -testResults /workspace/ref/round5_full_editmode.xml \
  -logFile /workspace/ref/round5_full_editmode.log

DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -runTests -testPlatform PlayMode \
  -testResults /workspace/ref/round5_full_playmode.xml \
  -logFile /workspace/ref/round5_full_playmode.log
```
