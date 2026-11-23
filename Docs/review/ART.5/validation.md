# ART.5 / #46 — validation

**VERIFIED — source/Editor/Battle scope на `a01706e`.** Независимое source/visual review и final committed delivery audit фиксируются отдельно; owner merge/ART/G2/release этим не утверждаются. Четыре carry-in rendering assets остаются вне PR и сохраняются побайтово.

## Original source и реальные animations

| Модель | Source meshes | Triangles | Уникальные palette materials | Source slots всего | Active model renderers с cues | Slots с cues |
|---|---:|---:|---:|---:|---:|---:|
| Shade |4|400|2|5|8|10|
| Страж Разрыва |4|1312|2|8|8|16|

≤2 означает два различных shared palette materials, повторяемых в articulated parts; не заявляются два total slots при нескольких renderers. Отдельный shared cyan stencil material добавляет четыре bounded cues/модель, без новых meshes/material instances. HP sources/cues и disabled legacy capsule посчитаны отдельно в prefab assertions. Без skin bones/weights/новых textures/packages/render features/lights.

Два original editable `.blend`,8rigid parts/8actions/72sourcecurves/8136keys. Unity readback:2FBX/8meshes/10legacyclips,400curvebindings/8960keys. На каждом Idle/Move≈1s, Hit≈.4s, Death/Leak≈.6s. Реальный экспорт каждого сохранённого source и повторный Unity import сохранили все geometry/UV/normals/indices, keyframes/четыре sampled poses каждого clip, GUID/localIDs/remaps/prefab+scene refs. Post-suite canonical readback равен before/after. [Данные](reports/reexport-after.json), [source audit](reports/source-audit.json), [команды экспорта](reports/source-reexport-commands.json).

Meters/+Yup/+Zforward проверены actual source/Unity bounds. Source forward RED−Z исправлен proper180° geometry+pivots+keys, без negative scale. Shade rest worldminY.058384m, boss.157247m; дополнительный read-only all-frame source audit Idle/Move показывает minima.0461/.0142 и.1359/.1011m соответственно. Root domain→world Y=.6 неизменен; только orientation child компенсирует legacy Shade scale.6 через localY−1/scale1.666667. Bossroot1/localY−.6/scale1. HP localanchor2/status2 прежние. [Motion bounds](reports/motion-bounds.json).

## Production integration и gameplay

Authoritative `EnemyDefinitionId`: rift-warden→authored boss, shade/unknown→прежний enemyPrefab; missing optional boss→legacy1.4factor. Фактический PlayMode [selector probe](reports/selector-scale-probe.json) подтверждает boss600/5 и historical1000/5root1, missingrefroot.84, unknown600HProot.6, один owner/ID, без наград/ресурсных mutations. Shade GUID прежний; visible capsule заменена authored child. Plain FBX mesh/clip references и уникальные four cue names обеспечивают прежний strict twoBuild byte contract.

Per-Step immutable damage streams/snapshots обрабатываются до clear, включая Direct/Burn/Steam/Storm/Ability, multi-hit и spawn→kill. Lethal выигрывает Hit; Domain removal/reward немедленны. Tail содержит только visual child,≤16/.6active seconds, pause-aware; stale position — captured damage point либо actual path endpoint. Нет EnemyId/HP/status в tail. Reset/production checkpoint restore/menu/unload очищают owners. Full Play regressions проверяют настоящую автоматическую expiry через production Update и реальные frozen part matrices при Pause, а не только stored clock. Cue world matrices совпадают с animated parts на всех sampled clips.

Domain/config/balance/content/schema4/packages/historical sources не изменены. Exact FLAME/GALE полные public-command replay traces before/after равны: FLAME7856entries/Victory, GALE7372entries/Victory, включая damage/reaction/ability/death/rewards/full Grid+snapshot. Production600/5 и historical1000/5 сохранены. [FLAME](reports/after-flame.json), [GALE](reports/after-gale.json). Earned media flow использует production public factory/fresh direct Battle150/20 (in-memory checkpoint disabled), explicit test clock→boss8→ability→Direct death/Victory8: HP16/Gold136/Kills64 exact control final wire, без setters ресурсов/HP/позиций. Реальный MainMenu→NewGame/file Continue подтверждены отдельно final video и полными integration suites. [Receipt](reports/earned-combat.json), [death ID68removed/captured position/tail.2333](reports/earned-boss-death.json).

## Новые обязательные gates

| Gate | Actual result |
|---|---|
| `./Scripts/run-static-checks.sh` |38/38 self-tests + static PASS|
| `./Scripts/run-edit-mode-tests.sh` |1237/1237 PASS,0failed/skipped/inconclusive|
| `./Scripts/run-play-mode-tests.sh` |93/93 PASS,0failed/skipped/inconclusive|
| Source byte audit |730/730 inputs restored, unexpected0/new0/recoveryfalse; detached+refresh true|
| Reexport/canonical |2sources/8meshes/10clips exact|
| Focused resolved regressions |60/60, including unchanged strict full-builder twoBuild|

Full testedSource `a01706e`; [gates byte/command audit](reports/gates-byte-audit.json), [Edit raw](reports/edit-mode.json), [Play raw](reports/play-mode.json). Suite durations не performance benchmark. Raw19FAIL/неполные/empty/compile/timeout attempts сохраняются и не переименованы PASS; [точные команды/классификация](commands.md).

## Реальная Play Mode media

30 synthetic CameraRT probes используют original Battle camera1600×900/ortho8.5/Metal/Mobile, прежние environment/18tower assets/освещение. Никакого foreign renderer hiding. Singles/pair/all5pose samples каждого,12behindL3sixfamilies,4gate/heart, dense12enemies+sixfamilies и два status slots. [Настройки/receipts](reports/synthetic-probes.json), [галерея](gallery.md).

Synthetic `shade|boss-death/leak.png` — **pose-only**, с отдельно стоящим корневым HP/status; они не являются lifetime proof. Production [earned boss death](playmode/earned-boss-death.png) и receipt показывают удалённый DomainID и отсутствие HP/status; [composite boss inspect](playmode/earned-boss-inspect-composite.png) показывает настоящий HUD600/600/armor5/notice/inspector. Earned flow — test clock, не real-time human playtest.

Финальное короткое continuous Editor video записано из заранее загруженного MainMenu:305frames/31.0497s, SHA ada1c6407fe97a93c114e15abcf1a0dede7c2004c1e630721059cb2eef9b9fb0; public NewGame/placement→Combat через production Update→ability/death→earned upgrade→Combat interruption→file/menu Continue/full Grid+snapshot rollback→sell. Начальный contaminated frame первого305frame attempt сохранён и отклонён отдельно: [attempt1](attempt-1/README.md). Finalvideo source/timestamps/PTS/picks/first-frame hashes проверяются verifier; без speed change/splices. Capture/encoding overhead не native display timing/performance proof.

Actual loaded inputs сохранены на двух real instantiated synthetic variants: все10clip GUID/localIDs, shared meshes/parts,8renderers/model, material slots, loaded shader/material values/pass counts, pipeline serialized values/quality/lighting/packages и exact disk SHA. Это новый audit ART5 consumers, не наследование ART3/4 certificate. [Actual inputs](reports/actual-render-inputs.json).

**NOT VERIFIED / DEFERRED:** physical iPhone/readability/touch/thermal/performance/player export/signing — Deferred to prerelease QA #53/#54/#55. Этот PR не делает TestFlight/release/self-merge; owner approval/G2/#47finalVFX/#48UI отдельно. Старые generic beams/graybox HUD остаются действующими вне ART5 scope.
