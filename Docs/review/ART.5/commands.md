# ART5 — команды и ограничения

Существующий checkout `/Users/alexeykravtsov/Desktop/workspace/Veilward`, ветка `codex/art5-enemies`, база `cb9101c` merge88. Один владелец записей и Unity runs; без worktree/клона/полной копии проекта. Unity6000.3.28f1/8f082d2fe476, CLI1.0.1, Blender5.3.0/02f0899d78e3, Editor Metal/Mobile. Packages/Unity version и исторические source/evidence защищены. Четыре actual rendering carry-in сохраняются точно; default delivery не требует личные TXT/pyc. Все `unity command` получают `--caller plugin --skill unity-cli --project-path "$PWD" --format json`.

```sh
# Mandatory spec/regression before implementation: Docs/ART5_ENEMIES.md
# Baseline on cb9101c before runtime changes:
unity command script run --file ArtSource/Review/CaptureArt5.cs --entry CaptureArt5.Baseline
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART5/author_enemies.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART5/check_sources.py
unity command script eval --code 'Veilward.Editor.EnemyModelBuilder.Import(); Veilward.Editor.EnemyModelBuilder.BindScene();'
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Blender/ART5/verify_reexport.py
# Focused actual suites and false empty OR filter retained separately.
# Test filter is case-insensitive partial match; OR syntax is unsupported.
# Source commit a01706e after focused60/60 and last actual2source reexport:
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Review/run_art5_gates.py
# Runs exactly Static/Edit/Play sequentially, closes task test scene BEFORE disk restoration,
# always restores only backed-up known serialized outputs and writes audit even on Editor failure,
# then Refresh/OpenSaved only after successful detach.
unity command script run --file ArtSource/Review/ReadbackArt5.cs --entry ReadbackArt5.Canonical
unity command script run --file ArtSource/Review/CaptureArt5.cs --entry CaptureArt5.After
unity command script run --file ArtSource/Review/CaptureArt5.cs --entry CaptureArt5.ConfigureView
unity command editor set_auto_tick
unity command editor.playmode set_run_in_background
unity command editor.playmode start
unity status --until-ready --project-path "$PWD" --format json
unity command job wait -- --condition '{"member":"UnityEngine.Time.frameCount","op":"changed"}' --timeout 10
unity command script run --file Docs/review/ART.5/SelectorProbe.cs --entry Art5SelectorProbe.Capture
unity command script run --file ArtSource/Review/CaptureArt5Probes.cs --entry CaptureArt5Probes.Capture
# Fresh direct Battle between synthetic pause probes and earned flow:
unity command script run --file ArtSource/Review/CaptureArt5Combat.cs --entry CaptureArt5Combat.Capture
# Inspect earned-combat-completion.json; earned explicit test clock, no resource/HP substitutions.
unity command script run --file ArtSource/Review/RecordArt5.cs --entry Art5EditorRecording.BeginRecording
# Inspect video-completion.json. Continuous composite Editor frames, production Update throughout.
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Review/encode_art5_video.py
# Instantiate only two labelled synthetic actors for loaded input audit, capture both clips/meshes/materials:
unity command script run --file ArtSource/Review/RenderInputsArt5.cs --entry RenderInputsArt5.Capture
unity command editor.playmode stop
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python Docs/review/ART.5/check_motion_bounds.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.5/verify_controls.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.5/verify_animation_controls.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.5/verify_gate_restoration_controls.py
# After adding complete packet inventory, freeze then commit evidence:
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.5/freeze_manifest.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.5/verify_delivery.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.5/verify_controls.py --delivery-entrypoint
# Final audit stdout outside checkout; no silently dirty own reports.
git diff --check
```

## Raw failures / recovery

- Initial namespace `Application` compiler error fixed explicitly as `UnityEngine.Application`. First Play test compile used EditMode-only internals, fixed by public constructors/commands and one clearly synthetic status injection. Empty filtered reports are not PASS.
- Extended Play expiry guard120renderframes completed before .4active seconds; replaced with bounded wall deadline while retaining the actual .6expiry/.85active assertion. Existing raw4/5 retained; later5/5 focused positive.
- Source forward RED found actual mask−Z. Proper180° source geometry+pivots+all location/rotation keys correction established Unity+Z; no negative scale. Rest-hover draft coordinates corrected to measured bounds, not a gameplay/root offset change.
- One readback process exited6 before exporter; wrapper had not captured child stdout, so exact reason UNKNOWN. Raw traceback retained. Later ready/compile0errors and explicit successful Before preceded one new actual reexport. Wrapper now saves child stdout/stderr before checking exit.
- Gate attempt1: Static38PASS, Edit1218/1237 with19FAIL, Play NOT RUN. Eighteen renderer-free EditMode orchestration fixtures encountered newly required per-Step prefab; narrow consumer guard retains production Start validation and old event assertions. Full-builder strict prefab localIDs drift fixed by unique four cues/plain FBX hierarchy. Focused60/60 includes unchanged strict twoBuild assertion.
- Original restoration wrote current scene bytes before Refresh, causing native `HandleOpenScenesChangeOnDisk→NSAlert.runModal`. CUA cgWindowNotFound; bounded AX read timed out−1712; Unity6000.3 lacks6000.7 native dialog reporting. Exact dialog text remained UNKNOWN. All queued task CLI clients stopped. After verified880source SHA/carry7, task-only Editor36151 exited immediately on SIGTERM, no forced kill, no disk drift. Same checkout/Library/Unity reopened as single Editor46676. No bug report/upload, no foreign process or worktree touched.
- Fail-safe runner now detaches only task post-suite scene before restore. If detach/refresh fails, exact known-byte restoration and byte audit still run with recoveryNeeded/diagnostics; failed detach does not enqueue Refresh/Open. Tiny mocked controls cover both failures without project copies.

Device Deferred to prerelease QA #53/#54/#55. Editor capture overhead/tests duration are not iPhone/performance PASS. #47VFX/#48UI/G2/release remain separate.
