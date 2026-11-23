# Проверки ART.3f / #86

Baseline main `d7895fe0927f6d5890a2439fcdae4168dd224e1a` (merged85). Media/model source `81a510a`; final tested source `e9277df` отличается только обновлением obsolete STORM tint expectation в RegBattle157. Точные полные heads/source inventory/artifact hashes — `manifest.json`. Текущий существующий checkout/Library, без новых worktrees/клонов/копий Unity project; чужой исторический Editor/worktree не менялся.

## VERIFIED

- Три editable originals, FBX и production prefabs STORM: **952/1168/1412tri**,2shared palette slots, footprint.90m, ground0, identity/units/UV/normals/semantic groups. [Geometry](../../../ArtSource/Blender/ART3/STORM/geometry-manifest.json), [provenance](../../../ArtSource/Blender/ART3/STORM/README.md), [Blender check](reports/geometry.txt).
- Реальный reexport3/3: before/after Unity geometry/UV/normals/submeshes/remaps/GUID/localIDs/material/prefab/scene refs идентичны. [Команда](reports/roundtrip-final.txt), [before](reports/reexport-before.json), [after](reports/reexport-after.json).
- Минимальный единый binder с optional stormLevels, все6семейств, type+level cache, single fallback owner/deferred inactive/no TypeTint; legacy/missing/unsupported fallback. Новых packages/materials/lights/render features нет; Domain/config/content/schema4/баланс не менялись.
- Fresh production earned lifecycle/public commands: GALE→TIDE→FLAME; L1 Preparation4/tick1233/Gold10; L2 Preparation7/tick2682/Gold35; L3 Preparation8/tick3207/Gold2.51kills/15HP, бюджет497, investment495; две законные продажи GALE по56. Полный file/menu Continue дважды на уровне, whole snapshot/live Grid Combat rollback, repeated/invalid/MAX/Sell/restart. [Команды](inputs/earned.commands.json), [trace](reports/natural-trace.txt), natural-L1/2/3-checkpoint.json.
- Intrinsic STORM в actual earned Combat: source Tower3, primary58, tick3375, reaction sequence1 после direct0, соответствующий AttackEvent; additional59/60 получили **18HP каждый**, hitIndex1/2, damage sequence2/3, primary повторно не поражена. [Same-event receipt](reports/intrinsic-positive.json). Отдельный controlled fixture с production definition проверяет exact обе secondary, live HP1000→994, primary1000→991 direct-only, cooldown/no-neighbor negatives; fixture не выдаётся за earned lifecycle.
- Реальные Editor PlayMode PNG singles/lineup/all6families/front-side-behind/dense/preview/earned flow. [Обзор](README.md), [исходная camera/settings](playmode/settings.json). HP/status anchors не менялись; occlusion не обходится отключением чужих renderer.
- Непрерывное Editor видео:254frames/35.49s,1600×900, реальные PTS,0splices/speed1. Earned L1 byte-identical replay, public UI upgrades, prefix test clock×4/fixed30Hz, финальный production Update/positive intrinsic chain/Inspect, whole rollback/Sell. [Видео](editor-flow.mp4), [proof/picks](reports/video-proof.json), [origin/readback](reports/video-checkpoint-origin.json), [safe](reports/video-safe-wire.txt)/[rollback](reports/video-rollback-wire.txt). Console recording без ошибок/предупреждений.
- Rendering inputs: actual loaded qualityMobile/Mobile_RPAsset/Metal/palette/materials/keywords и66disk SHA +packages. [Actual snapshot](reports/actual-render-inputs.json). Exact user4 совпадают с85certificate;142dependency/19settings проверены read-only. Две точные model-wiring source deltas и exact Storm shared-palette builder adaptation проверены отдельно. [Applicability](reports/render-input-applicability.json), [verifier](verify_render_inputs.py). Исторические95include proof/21controls не названы новыми runs.
- Малые current delivery controls **8/8 PASS**: source/protected/artifact tamper, committed foreign inventory, exact carry-in, strict positive suite totals. [Результат](reports/delivery-controls.txt), [код](verify_controls.py). Это маленький stdlib Git fixture, не копия Unity project, не полный historical tamper matrix.

## Полные Quality Gates

На final source `e9277dfca7ad6b142bc1cf49a9418ab9868d8111`:

| Gate | Новый результат | Evidence |
|---|---|---|
| Static |38/38 PASS|[raw](reports/static-command.txt)|
| Полный EditMode |1220/1220 PASS, failed/skipped/inconclusive0|[per-case JSON](reports/edit-mode.json), [command](reports/edit-mode-command.txt)|
| Полный PlayMode |87/87 PASS, failed/skipped/inconclusive0|[per-case JSON](reports/play-mode.json), [command](reports/play-mode-command.txt)|
| Compile |completed, errors0, failed=false|[audit](reports/compile.json)|
| Input restoration |все исходные input SHA совпали; unexpected/new0; exact carry-in4|[byte audit](reports/gates-byte-audit.json)|
| Canonical geometry/ref readback после suites |совпал с after реального reexport|[readback](reports/reexport-canonical-readback.json)|
| Small delivery controls |8/8 PASS|[report](reports/delivery-controls.txt)|

Общая Console **не объявляется нулевой**. [Raw236 entries](reports/console-warnings-errors.json):226expected checkpoint errors и2expected frozen-measurement guards из двух full-suite attempts;2ранние compile errors Newtonsoft исправлены. Четыре существующих BossGreedy unused-mask warnings и2Editor memoryless-depth warnings сохранены. [Классификация](reports/console-classification.json): unknown0, Console не очищалась. Final compile0 и собственная video Console0 подтверждены отдельно; агрегированная Editor groundTruth содержит25collapsed errors, не подменяет per-case gates.

Первая попытка сохранена как история: Edit1220/1220 и Play86/87 (obsolete RegBattle157 TypeTint expectation). Она не является полным PASS. Final source delta e9277df меняет только это ожидание на authored STORM L1/noPropertyBlock/noGraybox с сохранёнными position/old-root assertions. [Команды и неуспешные попытки](commands.md), attempt-1-* reports и attempts.tar.gz.

Delivery/evidence head определяется committed manifest и актуальным read-only run; stdout финального запуска хранится в ignored TestResults для исключения self-hashing loop. Полный verifier независимо PASS на evidence head47a0f76:3671source/3616protected/92artifacts. [Receipt](reports/delivery.json), [итоговое независимое review](review.md):P1/P2нет. После добавления receipt/заключения актуальный manifest проверяется ещё раз; это evidence-only delta, без новых Unity runs.

## NOT VERIFIED / ограничения

**Device Deferred to prerelease QA #53/#54/#55.** Physical iPhone/native recording/benchmarks/player binaries/iOS export/signing/TestFlight не выполнялись. Editor не подтверждает touch/safe area/background/IL2CPP/Metal player/performance. Sparse capture и encode overhead не benchmark.

Synthetic singles/lineup/behind/dense задают только presentation positions/HP и копируют Wet из отдельного TIDE fixture для проверки существующих slots; это не basic status STORM и не earned combat. Видео — replay earned L1 checkpoint, не fresh NewGame. Gameplay prefix явно test-clock×4, финальный Combat8 — actual production Update.

Delivery verifier read-only принимает только ровно4known dirty inputs с exact SHA и проверенной Battle equivalence; не объявляет checkout буквально clean и не запускает Unity. Userfiles не заменяются canonical bytes. [Protected exclusions](verify_delivery.py): README/TECH_DESIGN, TestTower/binding/builder и точно перечисленные superseded tests; весь остальной baseline проверяется побайтово, включая accepted5семейств и старое evidence. Manifest не хеширует сам себя; финальный run stdout остаётся в ignored TestResults.

Художественная приёмка владельцем отдельно: мелкие черты головы/усики L1 с полной Battle camera имеют ограниченную детализацию. Parent44 остаётся5/6 до принятия/merge; закрывается model scope ART3, не весь MVP. Final lightningVFX47/G2/map/enemies/UI/device/release остаются отдельными gates. Self-merge/release не выполняются.
