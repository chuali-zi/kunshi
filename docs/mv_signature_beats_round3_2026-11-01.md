# Round 3 — MV signature beats

Gameplay authority: `gameplay_logic_spec.md` §§4.1, 9–11. Visual authority: the actual JPGs under `art/reference/mv_frames/`, especially 6 s (right-door shot), 108 s (dark room), and 192 s (viewing-plane glass). The supplied 102 s image is lit; the shot list's broad 84–123 s dark-mode range is not a frame-by-frame claim.

`Room_M1` now has an explicitly wired `MvSignaturePresenter`. `RoomDesignGapInstaller` also installs/configures it before initial reset capture, preserving compatibility with the existing scaffold. `Resources/MvSignatureSettings.asset` contains the art, presentation durations, dark grade strength and muzzle-light settings.

- `RecoverableDark` and `PersistentBlack` select a near-monochrome blue/grey grade. Stable `Dim` light keeps the warm room palette. The grade does not write light facts, darkness XP or the ambient-intensity curve. A faint mesh makes the existing killer spotlight's cone visible; its visibility follows that light and supplies no extra detection.
- Existing right-door breach, entry navigation, aim and committed-shot timing remain authoritative. `SemanticVfxDirector.ShotPresented` adds a short point-light pulse at the same muzzle used by the particles, including live fire and D21/D22 scripted cues. Legacy paired outcomes without a live shot event receive a single terminal presentation at KillImpact.
- The original game title **困室** fades in 0.22 s into KillImpact, inside its existing 0.8 s hold. It adds no pause. Ordinary Awakening resets no longer add the old 0.35 s black interval; the initial two-second black screen remains.
- D12's camera-blind shot, an E2 viewer shot, or an explicit bullet hit on the presentation's `IHitReceiver` cracks the viewing plane. Ordinary room impacts do not. Camera glass and TV damage are separate shader inputs; a camera bullet cannot damage the TV or commit the ending. TV cracks use the same glass art while retaining the TV's own persistent/pulse/echo lifecycle.
- The first heavy TV hit in a loop shows a short, silent manga realisation cut-in. It is an expressive discovery beat, contains no hints or HUD text, and leaves movement, input, entry timing and combat running. Repeat notifications cannot restart it in the same loop.

All timers use game time. The presenter participates in `ILoopResettable`, rejects old-epoch camera hits, cancels transient presentation on ending transitions, and clears glass/title/cut-in/flash during reset. It adds no persistent-progress fields. EditMode tests cover fifty successive resets without growing the light count.

The fullscreen compositor is a URP Render Graph renderer feature, after post-processing and before final resolve. It reads `_BlitTexture`, writes a distinct graph texture using the source descriptor, and swaps the camera color. Only the bound game camera is affected. The per-room material is instantiated at runtime and disposed with the room. The rendered production test checks both ends of the manga panel, in addition to checking event/state wiring.

## Art and reproduction

The manga panel and glass fractures are original vector drawings, not copies of MV frames. Editable SVG sources and runtime PNGs are in `Assets/_Game/Art/MvSignature/`. `art/pipeline/mv_signature_art.py` regenerates them using Pillow. `TitleInk.png` rasterises the installed Noto Serif CJK Bold font (SIL Open Font License), with small dry-ink gaps and a slant; the font itself is not redistributed.

Install or refresh assets and the production scene without rebuilding the greybox:

```bash
DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -logFile /workspace/ref/mv_round3_build.log \
  -executeMethod ConGame.Editor.MvSignatureBeatBuilder.Build -quit
```

Capture the real room in lit, dark, muzzle, manga, glass, title and reset states (no `-quit`):

```bash
DISPLAY=:5 ~/Unity/Hub/Editor/6000.4.6f1/Editor/Unity \
  -batchmode -projectPath /workspace/con_game -logFile /workspace/ref/mv_round3_capture.log \
  -executeMethod ConGame.Editor.MvSignatureBeatCapture.CaptureGameplay
```

Images go to `/workspace/ref/mv_round3_<state>.png`. This acceptance utility drives existing damage/shot/phase APIs to stage the beats, then observes an actual LoopDirector reset. It does not represent a full natural-input playthrough of every counterplay route.

## Known baseline failures

Before Round 3, the full EditMode suite was 169/173. The four failures are `AimFirePresentationSettingsValidatedFillsDefaults` and the three SemanticVfxDirector event tests (`ImpactResolvedAcquiresDefaultCueFromPool`, `ReceiverMappingOverridesDefaultCue`, `BreakableItemBrokenStatePlaysRegisteredCueOnce`). They remain outside this presentation change's scope.

The broader PlayMode run also exposed `ProductionD21FlashlightTests.ProductionFlashlightChoiceRunsGrappleDropsAndRecoversTheLight`: the blanket is not acquired as the interaction focus. The identical assertion was reproduced with the original HEAD production scene and original `RoomDesignGapInstaller`, with Round 3 presentation installation absent. Evidence: `/workspace/ref/mv_round3_original_wiring_d21.xml` and `.log`. This work does not change the interaction scanner or that branch.

## Final verification

Unity `6000.4.6f1`, `DISPLAY=:5`, graphics enabled: zero C# compilation errors and zero shader compilation errors. Full EditMode: **181/185**, including **12/12 new tests**; the failure names exactly match the initial 169/173 baseline. Selected PlayMode: **11/11**, covering the two new production tests plus live-hunt flow, room integration, door/reset, practical bounce and breakable-light emission.

Final evidence:

- `/workspace/ref/mv_round3_verified_editmode.log` and `.xml`.
- `/workspace/ref/mv_round3_verified_playmode.log` and `.xml`.
- `/workspace/ref/mv_round3_compositor_final_capture.log`: seven rendered states and actual epoch advance/reset; title, camera glass and flash all clear.
- `/workspace/ref/mv_round3_lit.png`, `mv_round3_dark.png`, `mv_round3_muzzle.png`, `mv_round3_realisation.png`, `mv_round3_glass.png`, `mv_round3_title.png`, `mv_round3_reset.png`: visually inspected final composition; the final shader uses full-precision color composition and conditional layers.

Render Graph review: source binding matches `_BlitTexture`; read and write handles differ; the destination inherits the source descriptor; `AddBlitPass` supplies the render function; auxiliary art is explicitly bound; no graph handles or global textures survive across frames. This is installed in the existing production renderer, not in a separate preview-only pipeline.
