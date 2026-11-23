## Результат

Closes #46. Refs #33.

Production Battle использует original Shade и самостоятельного Стража Разрыва вместо видимых капсул. По5редактируемых Blender→FBX clips: Idle/Move/Hit/Death/Leak. Boss выбирается по EnemyDefinitionId, optional missing-ref/legacy fallback сохранён; нет double scale. Shade GUID/controller/HP/status refs прежние.

## Архитектура

Presentation-only child orientation/explicit pause-aware clip sampling, без root motion/Animator damage callbacks/Domain mutation. Per-Step snapshot/damage consumers до clear обеспечивают все5damage sources/multi-hit/lethal priority. Visual-only tails≤16/.6s, captured lethal/endpoint position, immediate HP/status/Domain removal, cleanup при restore/menu/unload. Четыре shared multipart stencil cues следуют animated transforms. Source400/1312tri,2unique palette materials/model; total slots5/8 и mask draws раскрыты. Scene/builder chain сохраняет ART4 и все18tower refs.

Domain/config/balance/content/schema4/economy/spawn/ability unchanged: production600/5, historical1000/5/Continue. Exact before/after FLAME/GALE full replay traces; public file/menu Continue и whole Grid+snapshot Combat rollback.

## VERIFIED

- Static38/38, full EditMode1237/1237, full PlayMode93/93;0failed/skipped/inconclusive. Tested source `a01706e`;730originalinputs exact restored,0unexpected/new, successful detach+refresh.
-2original editable sources/2FBX/8meshes/10clips,400Unitycurvebindings/8960keys; real reexport/readback geometry/UV/normals/keys/sampled poses/GUID/remaps/refs exact, canonical after suites exact.
-30actual PlayMode synthetic original-camera probes, actual earned direct-Battle boss/ability/death+composite HUD/inspector (явный test clock, без resource/HP setters).
- Final continuous305composited frames/31.0497s MainMenu/NewGame→production Update Combat/ability/death→earned upgrade→file Continue/rollback→sell. No speed change/splices; clean exact first MainMenu frame. First contaminated-intro attempt retained separately.
- Independent source/animation/visual review: no open confirmed P1/P2; final committed read-only delivery audit и tiny negative controls приложены по завершении root review.

## NOT VERIFIED / DEFERRED

Physical iPhone/readability/touch/thermal/performance/player export/signing Deferred to prerelease QA #53/#54/#55. Это Editor evidence, не device/human G2/owner ART approval. #47finalVFX/#48UI отдельно; existing generic beams/graybox HUD сохранены. Self-merge/release/TestFlight не выполнялись. Четыре пользовательских rendering carry-in не входят в PR, exact disk inputs учтены; private TXT/pyc не требуются default delivery.

## Evidence/docs

[Spec/regressions](../../ART5_ENEMIES.md), [validation](validation.md), [commands/raw failures/recovery](commands.md), [gallery/video](gallery.md), [review](review.md), `verify_delivery.py`/`verify_controls.py`/`verify_animation_controls.py`/`verify_gate_restoration_controls.py`.
