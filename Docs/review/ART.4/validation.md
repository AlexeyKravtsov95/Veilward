# ART.4 — evidence и ограничения

Baseline main/merge87 `80d453fcbf234e46f35f6c07c5018e68c371eb7c`. Media rendering inputs зафиксированы в `03af55397b08e6d18a9f5ed8a3879497906cdf42`; capture начинался с uncommitted implementation с теми же bytes. Финальный tested source `b10e8b8919b76f0b7e398558a483b4bd49eb49e9`: после media удалены ровно6unused Editor material writers, добавлены test-only byte/aspect guards и одна строка backup-списка Python wrapper. Сохранённые scene/assets/runtime consumers не менялись. Verifier проверяет exact media-input tree и точную6-line builder delta; это новый ART4 actual-input proof, не расширение старого ART3E lighting certificate.

Существующий checkout/Library, один владелец Unity/записей, без worktree/клонов/копий проекта. Четыре exact user carry-in и три untracked файла сохранены: [initial SHA](reports/carry-in.json). Они не входят PR и не названы canonical bytes. Все Unity tests/media используют фактические inputs; read-only delivery verifier не называется новым Unity run.

## VERIFIED

- Реальная production Battle из MainMenu/NewGame/Continue содержит остров/путь/81build sockets/вход/Сердце/периферийный сад. Graybox ground/grid/path/markers не видимы. Full builder создаёт ART4; legacy geometry APIs и все6старых environment materials остаются, production builder их не перезаписывает. Accepted18tower assets/binding, Domain/config/schema4/content/save/build settings и scene GUID прежние.
- Exact production geometry/mapping:12×8,origin(-6,-4),cellSize1;15blocked/81available, path14 с ordered points(-5,-2)→(0,-2)→(0,2)→(5,2). Все96cells/centers/states и world→screen→ground→cell, path samples/camera pose/projection/clip/aspect совпали: [before](reports/before-geometry.json)/[after](reports/after-geometry.json). DomainXY→UnityXZ/input planeY0/towerY.04/enemyY.56 сохранены.
- Два полных production replay FLAME/GALE до/после дают exact trace SHA/bytes и Victory8. Trace включает whole snapshots/live Grid/IDs/progression/ability/statuses и все attack/reaction/damage/ability events; сохраняется до/после как trace.gz и JSON. Новые regression tests сверяют эти goldens, не подменяют деньги/HP/позиции. Earned boss115HP ability и отдельный full safe final equality: [receipt](reports/earned-combat.json).
- Six original editable sources, shared palette/UV/normals/applied meters/pivot, Unity-generated meta/GUID/remaps/refs. [Geometry/provenance](../../../ArtSource/Blender/ART4/README.md), [manifest](../../../ArtSource/Blender/ART4/geometry-manifest.json), [read-only Blender source check](reports/geometry.txt). Реальный6/6source→FBX→Unity roundtrip: [before](reports/reexport-before.json)==[after](reports/reexport-after.json), включая geometry/UV/normals/submeshes/GUID/localIDs/remaps/prefab/scene refs. Повторные Import и full Build сохраняют результат, без дублей.
- Actual PlayMode source/visual/input/occlusion/earned evidence перечислены в [README](README.md). Independent source/visual reviewer просмотрел PNG и декодировал video1/8/16/23/29s, сверил305rawPNG/7exact picks/rollback wires; подтверждённых visual/source P1/P2 не нашёл. Итоговый delivery audit — [review](review.md).
- Video — fresh MainMenu/NewGame, вся simulation через production Update, без test clock;305frames/~31s/actual PTS/1×/без cuts. Публичные выбор/place/ability/Upgrade/Start/Next/Sell и file/menu Continue. Safe wire до/после rollback byte-identical, включая live Grid. [Proof](reports/video-proof.json), [origin](reports/video-origin.json), [safe](reports/video-safe-wire.txt)/[rollback](reports/video-rollback-wire.txt), [Console](reports/video-console.txt).
- Focused source regression13/13 и input PlayMode1/1 positive. Screen routing всех96cells, edges/boundary/blocked/occupied rejection, free preview, repeated Sell, source budgets/periphery clearance/route grout, production Update advancement. Existing full suites сохраняют all-family L1–L3/fallback/selection/range/geometry/import/earned file/menu/whole rollback/ability safeguards.

## Финальные Quality Gates

На tested source **b10e8b8**:

| Gate | Новый результат | Evidence |
|---|---|---|
| Static |38/38 PASS; version/packages/meta/GUID/asmdef|[raw](reports/static-command.txt)|
| Полный EditMode |1233/1233 PASS, failed/skipped/inconclusive0|[per-case JSON](reports/edit-mode.json)|
| Полный PlayMode |88/88 PASS, failed/skipped/inconclusive0|[per-case JSON](reports/play-mode.json)|
| Compile |up_to_date, errors0/warnings0, failed=false|[audit](reports/compile.json)|
| Byte restoration |714original inputs restored, unexpected/new0, exactcarry-in4|[audit](reports/gates-byte-audit.json)|
| Post-suite mesh/ref readback |совпал с real reexport-after6/6|[canonical readback](reports/reexport-canonical-readback.json)|
| Small controls |8/8 PASS, in-memory/tiny fixtures|[raw](reports/delivery-controls.txt)|

Общая Console **не объявляется нулевой**: [raw116entries](reports/console-warnings-errors.json),113expected checkpoint IO negative-path errors,1expected frozen-measurement exception(LogAssert.Expect вArtReferencePlayModeTests:159),2existingunused-mask compiler warnings. [Классификация](reports/console-classification.json):unknown0. Editor collapsed groundTruth25errors/0warnings относится к этим retained test entries. Console clear не вызывался; compile0 и own successful videoConsole0 подтверждены отдельно. Первые attempts не подменяют эти final totals.

## Фактические inputs и budgets

[Actual loaded snapshot](reports/actual-render-inputs.json): Unity6000.3.28f1/8f082d2fe476,URP17.3.0,Pipeline0.10.0-exp.1,CLI1.0.1,Blender5.3.0; macOS/AppleM4Pro/EditorMetal, targetiOS/QualityMobile/Mobile_RPAsset. Все loaded material shader/keywords/queue/pass refs, pipeline serialized fields, packages и66disk input SHA перечислены. Это Editor, не iPhone GPU/player.

| Original asset | triangles | shared slots |
|---|---:|---:|
| SanctuaryIsland |908|1|
| SanctuaryPath |124|1|
| BuildSocket |136|1|
| SpawnGate |558|1|
| SealHeart |188|2|
| RimPine |188|1|

Вся environment:119base mesh instances +22deliberate stencil masks =**141renderers/mesh instances**,8unique meshes/2312unique triangles,**20140triangle instances с masks**.3shared materials(surface/glow/mask),0colliders/0runtime scripts,0новых materials/textures/packages/shaders/renderfeatures. Instances:1island/15pavers/81sockets/2endpoints/4pines/8rocks/8shrubs; rocks/shrubs — неизменённый ART1 kit. Mesh не объединён в giant level ради draw-call числа. Реальные draw calls/GPU timings здесь не измерялись; это не ART2b/device budget PASS.

Intentional atmosphere deltas относительно [raw baseline](reports/before-render-settings.txt): camera background(.04,.05,.08)→(.105,.18,.23); ambient modeSkybox0→Flat3 и ambient(.64,.70,.74); sun explicit ref вместоnull, tint white→(1,.94,.83). Directional intensity1.2/pose(50,-30,0)/shadowsNone,fogOff и camera pose/ortho8.5/clips.3…1000 прежние. Нет новых lights/postprocess/bloom/прозрачной завесы. Saved Battle имеет автоматическую Unity Text serialization delta `m_MaxSize40→42` при `m_BestFit0`: неактивное поле без UI эффекта, отдельно замечено reviewer. Остальные gameplay/UI components/IDs прежние.

Scoped visual score после fix pass: palette4/5, environment style3/5, volume/light depth3/5, route/socket/endpoint silhouette4/5, combat feedback3/5. Сначала draft выявил слишком тонкий вид острова и coplanar top z-fighting; source исправлен до final media. UI/enemy silhouettes относятся к#48/#46 и не называются finished here. Оценки субъективные и не заменяют owner approval.

## Неуспешные попытки

[Commands](commands.md) сохраняет точные failures. Initial empty discovery0tests не PASS; следующий RED6/6failed подтвердил отсутствие production environment/настроенного импорта. First video попытка failed на Upgrade вWaveComplete, хотяGold110≥105; final recorder сначала делаетNext→Preparation2. Failed input Ready поймал domain reload/EDITOR_NOT_RUNNING; зависимые кадры invalid и пересняты strict runner после frame advancement. Test-clock helper initially не скомпилировался из-за несуществующего HasPendingChoice, затем ставил ability intent на тик позднее; final coroutine dispatches guarded intent на нужном тике и проверяет exact final state. Raw reports не переименованы в PASS.

Full gate attempt1: Edit1232/1232 +Play88/88, но wrapper byte audit FAIL по двум дополнительным generated cell materials; exact pre-run hashes сверены сGit, restored only own generated bytes. Attempt2: Edit1231/1232 FAIL на строгом two-build legacy material float drift; bytes restored. Новый byte guard RED подтвердил ненужные записи и GREEN после удаления6unused writers. После task-owned restart aspect guard RED из-за размера окна, fixed16:9 внутри test+ResetAspect сделал13/13. Queued synchronous test/eval/cancel не выполнились/истекли, Editor restart только собственногоPID510; carry-in проверен7/7. Это не source/compile/test PASS и не reset/clean/stash. Исторические proofs ART1–3 не тронуты.

## Delivery и независимая передача

Committed packet6e02fd9 независимо прошёл source/artifact/protected audit:848source/3754protected/137artifacts, controls8/8; [review](review.md)/[receipt](reports/independent-review.json). [PR88](https://github.com/AlexeyKravtsov95/Veilward/pull/88), [Quality Gates success](https://github.com/AlexeyKravtsov95/Veilward/actions/runs/45135086403). OpenP1/P2=0. После review добавлены только заключение/session/CI receipts иmanifest; tested runtime/source b10 и media tree03af прежние. Текущий artifact count задаёт finalmanifest/read-only stdout; verifier не новый Unity run.

[Session restoration](reports/session-restoration.json): GameView18,captureFramerate0,sceneDirtyfalse,background overrideoff/autoTickdisabled, taskEditor4501 closed gracefully/no unsaved scenes. Чужие exact bytes сохранены. Исторические source/evidence ART1–3 защищены побайтово, новые worktrees/clone/project copies не создавались.

## NOT VERIFIED / ограничения

**Device Deferred to prerelease QA #53/#54/#55.** Physical iPhone/native recording/benchmark/player binary/iOS export/signing/TestFlight не выполнялись; touch/safe-area/background/IL2CPP/frame time/memory/GPU/thermal не объявляются PASS. Camera RT probes не показывают screen-spaceOverlay UI; полная composited UI/flow показана отдельными screen captures/видео. Synthetic positions/HP/copied Wet и earned test-clock screenshots явно отделены от production Update видео.1×sparse Editor video не native display/performance benchmark.

Окончательная художественная приёмка/owner merge отдельно. #46enemies/#47VFX/#48UI/G2/баланс/M0/release остаются отдельными gates. Собственный merge/release не выполняется; GitHub Projects card не обновлялась/не проверялась.
