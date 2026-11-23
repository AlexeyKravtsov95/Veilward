# ART.4 Implementation Plan

> Выполнение: последовательно в текущем checkout, одним владельцем Unity/записей. Применяется superpowers:executing-plans; итоговый независимый reviewer выполняет read-only source/visual audit.

**Goal:** законченная первая production Battle-карта при exact gameplay invariants.
**Architecture:** отдельный Editor environment builder + статический original Blender kit; Domain/runtime бой не меняются.
**Tech Stack:** Unity6000.3.28f1, URP17.3.0, Blender5.3, C#/Python, существующий Unity CLI.
**Spec:** [ART4_PRODUCTION_MAP](ART4_PRODUCTION_MAP.md).

## Global constraints и review focus

Все ограничения spec/#45; никаких worktrees/клонов/копий проекта, чужие bytes сохраняются, Unity последовательно. Проверить96 cells/края, перекрытие UI/HP, endpoint topology, повторный builder/import, фактические inputs вместо canonical claims. Device deferred.

## 1. Baseline и regression

- [x] Capture `ArtSource/Review/CaptureArt4.cs`: readback production geometry/camera/render inputs и baseline replay/PNG в `Docs/review/ART.4/`.
- [x] `Assets/_Game/Tests/EditMode/BattleEnvironmentTests.cs`: production refs/geometry/double build/source budgets; убедиться в отсутствии ART4 до реализации.
- [x] Commit spec/baseline/tests перед product implementation.

## 2. Original kit и production binding

- [x] `ArtSource/Blender/ART4/{author_environment,export_environment,check_geometry}.py`: separate editable island/path/socket/gate/heart/props, stable palette/materials/meters/pivots; manifest/provenance.
- [x] `Assets/_Game/Scripts/Editor/BattleEnvironmentBuilder.cs`: import shared remaps, generate prefab kit, `Create(BattleScenarioConfig)` от actual Domain geometry, статический decor, нет collider.
- [x] `BattleSceneBuilder.Build`: заменить только production graybox creation, сохранить legacy geometry APIs; environment background/light дельты явные.
- [x] Run targeted Edit/Play; actual camera visual draft, fix pass.

## 3. Behaviour и media

- [x] `Assets/_Game/Tests/PlayMode/BattleEnvironmentPlayModeTests.cs`:96 screen-cell/input checks, protected state equality/rollback, actual scene refs; existing six-family/lifecycle suites сохраняют силу.
- [x] Capture before/after/6families L1–3/fallback/preview/blocked/occupied/occlusion/dense/boss/ability; raw source settings/counts.
- [x] `ArtSource/Review/RecordArt4.cs`: continuous Editor MainMenu/NewGame/public UI/production Update/Continue с изолированным file store; encode реальных captured PTS без cuts.
- [x] Real source→FBX→Unity roundtrip; exact geometry/ref before/after receipt.

## 4. Gates и передача

- [x] Commit tested source, выполнить static/fullEdit/fullPlay/compile/diff, сохранить raw failures/final totals и byte restoration audit.
- [x] Узкий ART4 source/protected/artifact verifier + small controls, frozen manifests, actual input inventory.
- [x] README/TECH_DESIGN/ROADMAP/evidence: VERIFIED и NOT VERIFIED.
- [x] Push PR (Closes45 только при полном acceptance; Refs33), независимое source/visual review, исправить P1/P2 и перепроверить затронутые gates. Без self-merge/release.

## Rulings / evidence ledger

- Source03af553: первый полныйEdit1232/Play88positive, но wrapper byte audit FAIL на2допgeneratedmats. Их exactpre-runGit SHA подтверждены, только собственные bytes восстановлены. Source6088367 расширяет узкийbackupсписок.
- Второй полныйEdit1231/1232:unusedlegacycellmaterialfloatdrift вstrict2Build. Принято убрать6unusedEditorwriters; legacyassets/meta/APIs сохраняются. Новыйbyteguard RED→GREEN. Independent reviewer подтвердил0GUIDconsumers.
- После task-owned restart mappingtestпоймал изменившийся Editor aspect; сравнениеfixed16:9+ResetAspect соответствуетcapturecontract. Focused13/13 и final b10e8b8 fullStatic38/Edit1233/Play88positive,714inputsrestored/unknown0.
- Media rendering tree03af553 сохранён; только exact6-lineunusedwriterdelta/test-onlyguards/Pythonbackup отличаютtestedb10. ActualART4settings/inputs/measuredlighting собственные; historicalART3certificate не расширяется.
- Final integrity/verifier/PR/review фиксируются вDocs/review/ART.4/manifest.json иreview.md. Никакихworktrees/clones/selfmerge/release.

## Follow-up P2: воспроизводимый delivery verifier

- [x] Воспроизвести старый full-entry отказ при отсутствии каждого personal3 без перемещения файлов.
- [x] Default delivery отделить от explicit local preservation; exact4/source/protected/artifacts/actual66 иостальные acceptance checks оставить строгими.
- [x] Добавить малые14fixturecontrols; source/gameplay/карта/lighting/Unity/media не меняются.
- [x] Полный read-only entrypoint control missing3, strict-preservation controls иновые receipts; текущиеStatic38/controls/verifier, manifest/commands/docs.
- [ ] Push втотжеPR88 и независимое re-review P2; безновогоUnityrun/reexport/video/merge/release.
