# Round 15 — natural D16 rug acceptance

Starting revision: `75d12a8`. Reviewed README, the gameplay logic spec (runtime authority), the real-frame shot list, Round 12/14 handoff and recent `/workspace/ref/rounds` logs. Opened the actual 6s, 102s and 192s MV frames, plus 84s, 96s and 114s. These supplied 84–114s samples are lit; the shot list's approximate dark-mode timestamps must not override the images. No visual assets changed this round.

## Accepted keyboard route

Completed the existing uncommitted `KeyboardArmsRugWaitsForKillerContactAndPullsNaturalD16ThenResets` test. It runs the saved production room from its seated start using actual keyboard events: approach and arm the empty rug, hide behind the armchair, wait for the normal entry clock and navigating killer to contact the rug, exit hiding, face the rug and press E again. The hide is entered from the open east side; the prior route attempted an obstructed walk around the rotated backrest after contact.

Assertions cover no pull stimulus from arming, no pull without a killer occupant, exactly one explicit pull, real D16 selection and active animation graph, no D21 paired fall, death hold, same-scene seated reset, unchanged fixed camera, cleared trap/contact/animation state, and next-loop keyboard movement. No fact injection, player teleport helper, forced entry or killer relocation is used by the test. Production hide entry/exit retains its normal anchor behavior.

## Verification

Unity `6000.4.6f1`, `DISPLAY=:5`, batchmode with graphics enabled (no `-nographics`). Natural D16 **1/1 passed**, zero skipped and zero C# / shader compile errors. Evidence: `/workspace/ref/round15_d16_east.log` and `.xml`. The initial preserved WIP failed at its backrest exit walk (`round15_d16.*`); the revised east-side route passed without weakening gameplay conditions.

Full-suite baseline before this round remains Round 10: EditMode **191/191**, PlayMode **157/157**; updated full-suite results will be appended after execution. Natural D13's door-gap behavior, full natural D21 and remaining MV/animation acceptance are still open. The two pre-existing `InitTestScene5421e05b-10e7-4001-a61c-9a31f09eb7fe.unity` files are preserved and excluded from commits.
