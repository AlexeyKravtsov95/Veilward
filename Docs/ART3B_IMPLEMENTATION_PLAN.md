# ART.3b Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Задание #78 уже задаёт scope и самостоятельный выбор конструкции; выполнять без повторного согласования.

**Goal:** три оригинальных TIDE production модели с единым FLAME/TIDE binding и честным Editor evidence.
**Architecture:** расширение существующего FlameModelBinding с сохранением GUID/serialized compatibility; отдельные TIDE source/export/builder, общая палитра и fallback owner.
**Tech Stack:** Unity6000.3.28f1, C#, Blender, Python stdlib, существующий Unity CLI.
**Spec:** Docs/ART3B_TIDE_LEVELS.md

## Global Constraints

- Только TIDE и минимальный production binding. ART1/FLAME sources/models/prefabs и исторические evidence неизменны.
- Domain/Infrastructure/configs/packages/project settings/scene gameplay/content600/5/schema4/balance не изменять.
- .meta только Unity-generated, budgets≤5000tri/≤2slots, footprint≤.94m, root identity/ground0.
- Device Deferred to prerelease QA #53/#54/#55. Без телефона/native benchmark/new device binary/self-merge/release.
- Русские spec/summary/review, technical identifiers English. VERIFIED и NOT VERIFIED / BLOCKED отдельно.

## Review Focus

1. Cache должен учитывать type и level — alternating-family test Task1.
2. Единственный fallback owner и palette isolation — model/fallback assertion Task1.
3. Missing TIDE refs в старом authored prefab не ломают FLAME — compatibility test Task1.
4. Builder повторно не стирает refs другого семейства/сохранённые модели — idempotence/reference test Task1.
5. Все source и evidence действительно tracked и привязаны к tested source — verifier Task2.

### Task 1: TIDE sources, production binding и regression tests

**Files:** Create ArtSource/Blender/ART3/TIDE/*, Assets/_Game/Art/Models/ART3/TIDE/*, Assets/_Game/Prefabs/ART3/TIDE/*, Scripts/Editor/TideModelBuilder.cs (inside Assets/_Game), Tests/EditMode/TideModelAssetTests.cs and Tests/PlayMode/TideModelPlayModeTests.cs (inside Assets/_Game). Modify Presentation/FlameModelBinding.cs, Editor/FlameModelBuilder.cs and BattleSceneBuilder as necessary, Prefabs/TestTower.prefab, old FlameModelAssetTests only replaced TIDE fallback case. No other systems.

**Interfaces:** `TideModelBuilder.Build()`, `Bind(GameObject root)`, `PrefabPath(int level)`, `ModelFolder`, `PrefabFolder`; binding preserves `Model`, `Level`, `ShotSource`, `Apply(TowerDefinitionId,int)` and adds authoritative type cache. New sources `Tower_TideBellL1/2/3.blend` → same stems FBX → `P_Tower_TideBellL1/2/3.prefab`.

- [ ] Read spec, current Flame binding/builders/tests, ART_PIPELINE and ART3A R2 evidence. Add tests exposing missing TIDE binding, same-level family cache, fallback isolation and compatibility.
- [ ] Generate editable original TIDE sources with separate family-only author/export/check_geometry scripts and provenance README. Save 3 sources and export same proven preset. Never touch FLAME generation scripts or outputs.
- [ ] Implement one-owner binding and narrow Tide builder, preserving GUID/serialized fields and existing Flame builder compatibility. Root model includes ShotSource and reused stencil mask, no new materials.
- [ ] Implement meaningful import/footprint/palette/builder refs/idempotence tests; alternating supported/unsupported and legacy-authored fixture. Actual earned Battle public-command TIDE lifecycle test with file/menu Continue/rollback/Sell/restart, not synthetic Gold/HP mutation.
- [ ] Coordinate Unity execution with controller; do not open/control other checkouts. Run targeted tests when controller supplies Editor, then commit only owned paths after results and self-review. Do not weaken historical goldens.

### Task 2: evidence, roundtrip, delivery и PR

**Files:** ArtSource/Review/CaptureArt3b.cs; ArtSource/Blender/ART3/TIDE/verify_reexport.py; Docs/review/ART.3b/*; additive README/TECH_DESIGN/ART_PIPELINE device-deferral/scope notes.

**Interfaces:** consumes committed Task1, TideModelBuilder and new tests. Produces tracked screenshots/video/reports/manifests and read-only `verify_delivery.py`.

- [ ] Run targeted/full Unity suites after final runtime delta, real reexport all3, assert geometry/UV/normals/materials/GUID/localIDs/references before==after. Record exact commands and Console/compile.
- [ ] Capture actual advancing PlayMode singles/lineup/comparison, synthetic labeled front/side/behind/dense, natural earned Upgrade/Continue/rollback/Sell and Editor video. Visually inspect every meaningful result; fix real defects, repeat affected gates.
- [ ] Pin tested SourceSHA, hash source inventory and all artifacts, preserve protected baseline bytes. Implement read-only verifier requiring tracked artifacts, positive full suites/roundtrip checks. Run in clean checkout and negative controls.
- [ ] Publish one Russian PR Closes78/Refs44 with architecture/tests/evidence/docs/limitations and Device Deferred; independent whole-branch code/asset/visual/evidence review; no self-merge.
