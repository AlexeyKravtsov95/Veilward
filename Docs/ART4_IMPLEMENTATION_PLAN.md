# ART.4 Implementation Plan

> Выполнение: последовательно в текущем checkout, одним владельцем Unity/записей. Применяется superpowers:executing-plans; итоговый независимый reviewer выполняет read-only source/visual audit.

**Goal:** законченная первая production Battle-карта при exact gameplay invariants.
**Architecture:** отдельный Editor environment builder + статический original Blender kit; Domain/runtime бой не меняются.
**Tech Stack:** Unity6000.3.28f1, URP17.3.0, Blender5.3, C#/Python, существующий Unity CLI.
**Spec:** [ART4_PRODUCTION_MAP](ART4_PRODUCTION_MAP.md).

## Global constraints и review focus

Все ограничения spec/#45; никаких worktrees/клонов/копий проекта, чужие bytes сохраняются, Unity последовательно. Проверить96 cells/края, перекрытие UI/HP, endpoint topology, повторный builder/import, фактические inputs вместо canonical claims. Device deferred.

## 1. Baseline и regression

- [ ] Capture `ArtSource/Review/CaptureArt4.cs`: readback production geometry/camera/render inputs и baseline replay/PNG в `Docs/review/ART.4/`.
- [ ] `Assets/_Game/Tests/EditMode/BattleEnvironmentTests.cs`: production refs/geometry/double build/source budgets; убедиться в отсутствии ART4 до реализации.
- [ ] Commit spec/baseline/tests перед product implementation.

## 2. Original kit и production binding

- [ ] `ArtSource/Blender/ART4/{author_environment,export_environment,check_geometry}.py`: separate editable island/path/socket/gate/heart/props, stable palette/materials/meters/pivots; manifest/provenance.
- [ ] `Assets/_Game/Scripts/Editor/BattleEnvironmentBuilder.cs`: import shared remaps, generate prefab kit, `Create(BattleScenarioConfig)` от actual Domain geometry, статический decor, нет collider.
- [ ] `BattleSceneBuilder.Build`: заменить только production graybox creation, сохранить legacy geometry APIs; environment background/light дельты явные.
- [ ] Run targeted Edit/Play; actual camera visual draft, fix pass.

## 3. Behaviour и media

- [ ] `Assets/_Game/Tests/PlayMode/BattleEnvironmentPlayModeTests.cs`:96 screen-cell/input checks, protected state equality/rollback, actual scene refs; existing six-family/lifecycle suites сохраняют силу.
- [ ] Capture before/after/6families L1–3/fallback/preview/blocked/occupied/occlusion/dense/boss/ability; raw source settings/counts.
- [ ] `ArtSource/Review/RecordArt4.cs`: continuous Editor MainMenu/NewGame/public UI/production Update/Continue с изолированным file store; encode реальных captured PTS без cuts.
- [ ] Real source→FBX→Unity roundtrip; exact geometry/ref before/after receipt.

## 4. Gates и передача

- [ ] Commit tested source, выполнить static/fullEdit/fullPlay/compile/diff, сохранить raw failures/final totals и byte restoration audit.
- [ ] Узкий ART4 source/protected/artifact verifier + small controls, frozen manifests, actual input inventory.
- [ ] README/TECH_DESIGN/ROADMAP/evidence: VERIFIED и NOT VERIFIED.
- [ ] Push PR (Closes45 только при полном acceptance; Refs33), независимое source/visual review, исправить P1/P2 и перепроверить затронутые gates. Без self-merge/release.
