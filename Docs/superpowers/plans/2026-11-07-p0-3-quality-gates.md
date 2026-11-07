# P0.3 Quality Gates & Development Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Добавить воспроизводимые static/Unity quality gates, регрессионную спецификацию и безопасные проверки Battle assets без изменения игровых правил.

**Architecture:** Независимые Python stdlib checks выполняются на каждом PR/push без Unity license. GameCI EditMode/PlayMode jobs включаются явной настройкой лицензии и repository variable; XML reports дополнительно проверяются локальным validator, чтобы skipped/empty suites не давали PASS. Editor-only validator только читает сохранённые assets; отдельный reproducibility test резервирует generated outputs в памяти, сравнивает два Build и гарантированно восстанавливает исходные bytes.

**Tech Stack:** Unity 6000.3.28f1, C#/NUnit, Python 3 stdlib, Bash, GitHub Actions, GameCI unity-test-runner v4.

## Global Constraints

- Не менять gameplay, `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json` и `Packages/packages-lock.json`.
- Domain остаётся pure C# с `noEngineReferences: true`.
- Не добавлять secrets, license files, signing credentials или новые Unity packages.
- Не запускать destructive scene/asset operations без snapshots и `finally` restore.
- Не считать empty, failed, inconclusive или skipped tests успешными.
- Все результаты отчётности разделять на VERIFIED и NOT VERIFIED/BLOCKED.
- Работа выполняется только в `feature/p0-3-quality-gates`; merge запрещён.

---

### Task 1: Static project checks and result validation

**Files:**
- Create: `Scripts/unity-project-baseline.json`
- Create: `Scripts/check_unity_project.py`
- Create: `Scripts/verify_unity_test_results.py`
- Create: `Scripts/tests/test_quality_scripts.py`
- Create: `Scripts/run-static-checks.sh`

**Interfaces:**
- Produces: `python3 Scripts/check_unity_project.py [project_root]` — exit 0 только при пустом списке ошибок.
- Produces: `python3 Scripts/verify_unity_test_results.py <xml-or-directory>` — принимает NUnit/JUnit XML, требует `total > 0`, `passed == total`, нулевые failed/errors/inconclusive/skipped.
- Produces: `./Scripts/run-static-checks.sh` — запускает self-tests и проверку текущего проекта.

- [ ] **Step 1: Write failing stdlib tests**

Создать temporary Unity fixtures и тесты, которые проверяют обнаружение missing `.meta`, duplicate GUID, package drift, broken local asmdef reference, empty suite и skipped test:

```python
def test_missing_meta_is_rejected(self):
    project = make_project()
    (project / "Assets/_Game/New.cs").write_text("// test", encoding="utf-8")
    errors = check_project(project)
    self.assertTrue(any("missing .meta" in error for error in errors))


def test_skipped_junit_test_is_rejected(self):
    path = write_junit(tests=2, passed=1, skipped=1)
    errors = verify_path(path)
    self.assertTrue(any("skipped" in error for error in errors))
```

- [ ] **Step 2: Run tests and confirm RED**

Run: `python3 Scripts/tests/test_quality_scripts.py`
Expected: import failure, потому что check modules ещё не созданы.

- [ ] **Step 3: Implement minimal validators and frozen baseline**

Baseline содержит точную Unity version/revision и все direct dependencies текущего manifest. Checker реализует:

```python
def check_project(root: Path) -> list[str]:
    errors = []
    errors.extend(check_version_and_packages(root))
    errors.extend(check_meta_files(root / "Assets"))
    errors.extend(check_assembly_definitions(root / "Assets"))
    return errors
```

Meta checks обходят files и directories, проверяют paired `.meta`, уникальный 32-hex GUID и отсутствие orphan `.meta`. Package checks сравнивают manifest с baseline и direct lock entries. Asmdef checks парсят JSON, проверяют unique names, local references, `Veilward.Domain.noEngineReferences is True` и Editor-only platform constraints.

Result validator суммирует `<test-run>` либо JUnit `<testsuite>` и отклоняет malformed/missing XML:

```python
if total == 0 or passed != total or failed or errors or inconclusive or skipped:
    return ["Unity tests are not a complete PASS"]
return []
```

- [ ] **Step 4: Run self-tests and project checks**

Run: `./Scripts/run-static-checks.sh`
Expected: все Python self-tests PASS; текущий проект PASS.

- [ ] **Step 5: Commit static gates**

```bash
git add Scripts
git commit -m "test: add reproducible Unity static quality gates"
```

### Task 2: GitHub Actions and reproducible Unity runners

**Files:**
- Create: `.github/workflows/quality-gates.yml`
- Create: `Scripts/run-unity-tests.sh`
- Modify: `Scripts/run-edit-mode-tests.sh`
- Create: `Scripts/run-play-mode-tests.sh`

**Interfaces:**
- Consumes: Task 1 commands и `TestResults/*.xml`.
- Produces: `./Scripts/run-edit-mode-tests.sh`, `./Scripts/run-play-mode-tests.sh` и workflow `Quality Gates`.

- [ ] **Step 1: Specify workflow failure behavior**

Workflow имеет triggers `pull_request`/`push` для `main` и `workflow_dispatch`. `static-checks` всегда выполняется на Ubuntu. Unity matrix выполняется при manual dispatch либо `vars.VEILWARD_UNITY_CI == 'enabled'`; в противном случае документируется BLOCKED, без dummy success job. При включении variable отсутствие полного Personal или Professional secret set завершает job с явной ошибкой.

```yaml
- name: Validate Unity test report
  run: python3 Scripts/verify_unity_test_results.py "TestResults/${{ matrix.testMode }}"
```

- [ ] **Step 2: Add direct batch fallback**

Runner использует resident Unity CLI при доступном открытом Editor; иначе требует executable `UNITY_EDITOR_PATH` либо стандартный macOS Hub path и запускает:

```bash
"$UNITY_BIN" -batchmode -nographics -projectPath "$PROJECT_PATH" \
  -buildTarget iOS -runTests -testPlatform "$TEST_MODE" \
  -testResults "$RESULT_PATH" -logFile "$LOG_PATH" -quit
python3 Scripts/verify_unity_test_results.py "$RESULT_PATH"
```

- [ ] **Step 3: Verify static workflow commands locally**

Run: `./Scripts/run-static-checks.sh`
Expected: PASS. Проверить workflow на forbidden secret values и убедиться, что artifacts загружаются через `if: always()`.

- [ ] **Step 4: Commit CI pipeline**

```bash
git add .github/workflows/quality-gates.yml Scripts/run-*
git commit -m "ci: add Unity quality gate workflow"
```

### Task 3: Battle regression and PlayMode smoke

**Files:**
- Create: `Docs/REGRESSION.md`
- Create: `Assets/_Game/Tests/EditMode/BattleRegressionTests.cs`
- Create: `Assets/_Game/Tests/PlayMode/Veilward.Tests.PlayMode.asmdef`
- Create: `Assets/_Game/Tests/PlayMode/BattleSmokeTests.cs`
- Create: Unity-generated `.meta` files for these assets/folder.

**Interfaces:**
- Consumes: `BattleScenarioConfig.CreateSimulation()`, public `BattleSimulation` API, Build Settings scene `Battle`.
- Produces: сценарии `REG-BATTLE-001…008` и их точные NUnit test mappings.

- [ ] **Step 1: Write regression specification and failing tests**

Спецификация фиксирует initial state, exact 60 gold debit, occupied/route rejection, five Shade IDs, movement/leaks, WaveComplete с 15 HP и полный deterministic timeline. Новые tests используют сохранённый `TestWave.asset`, а не новый gameplay API:

```csharp
[Test]
public void InitialState_SavedScenario_MatchesRegressionContract()
{
    var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ConfigPath);
    var simulation = config.CreateSimulation();
    Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
    Assert.That(simulation.State.Gold, Is.EqualTo(150));
    Assert.That(simulation.State.BaseHealth, Is.EqualTo(20));
    Assert.That(simulation.State.Towers, Is.Empty);
    Assert.That(simulation.State.Enemies, Is.Empty);
}
```

- [ ] **Step 2: Run EditMode tests and confirm new scenarios execute**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: новые tests обнаружены и полный suite PASS; XML validator отклоняет skipped/missing results.

- [ ] **Step 3: Add PlayMode smoke**

```csharp
[UnityTest]
public IEnumerator Battle_LoadsIntoPreparation()
{
    yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
    yield return null;
    var battle = Object.FindFirstObjectByType<BattleSceneController>();
    Assert.That(battle, Is.Not.Null);
    Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
    Assert.That(battle.Simulation.State.Gold, Is.EqualTo(150));
}
```

Run: `./Scripts/run-play-mode-tests.sh`
Expected: `total > 0`, полный PASS без skipped.

- [ ] **Step 4: Commit regression coverage**

```bash
git add Docs/REGRESSION.md Assets/_Game/Tests
git commit -m "test: specify Battle regression scenarios"
```

### Task 4: Scene and asset validation with safe reproducibility check

**Files:**
- Create: `Assets/_Game/Scripts/Editor/BattleAssetValidator.cs`
- Create: `Assets/_Game/Tests/EditMode/BattleAssetValidationTests.cs`
- Create: corresponding Unity `.meta` files.

**Interfaces:**
- Produces: `BattleAssetValidator.ValidateSavedBattle()` — read-only validation сохранённой Battle scene, config, prefabs, materials и missing scripts.
- Produces: reproducibility test, который сравнивает outputs двух последовательных `BattleSceneBuilder.Build()` и восстанавливает originals в `finally`.

- [ ] **Step 1: Write failing validator tests**

Тест вызывает `BattleAssetValidator.ValidateSavedBattle()` и ожидает отсутствие errors. Reproducibility test snapshot-ит scene/config/prefabs/materials и их `.meta`, делает два Build и восстанавливает original bytes даже при assertion failure. Config/prefabs/materials/`.meta` сравниваются побайтово. Для scene применяется canonical YAML comparison: при recreate Unity переназначает local fileIDs, поэтому только эти IDs и их local references получают стабильные tokens; GUID references и остальной serialized content сравниваются точно.

```csharp
try
{
    BattleSceneBuilder.Build();
    var first = Snapshot(paths);
    BattleSceneBuilder.Build();
    var second = Snapshot(paths);
    CollectionAssert.AreEqual(first.Keys, second.Keys);
    foreach (var path in first.Keys)
        AssertGeneratedContentEqual(first[path], second[path], path);
}
finally
{
    Restore(originals);
    AssetDatabase.Refresh();
}
```

- [ ] **Step 2: Implement read-only validation**

Validator открывает Battle additive и закрывает только открытую им scene; проверяет required serialized references, config `CreateSimulation()`, prefab asset paths, non-null materials/shaders и missing scripts через `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount` для всех descendants.

- [ ] **Step 3: Run full EditMode and verify tracked outputs**

Run: `./Scripts/run-edit-mode-tests.sh && git diff --exit-code -- Assets Packages ProjectSettings`
Expected: tests PASS; после test восстановлены все originals, tracked generated outputs не изменились. Если Build недетерминирован, исправляется только Editor generator/validation, не gameplay.

- [ ] **Step 4: Commit asset validation**

```bash
git add Assets/_Game/Scripts/Editor Assets/_Game/Tests/EditMode
git commit -m "test: validate Battle scenes and generated assets"
```

### Task 5: Agent workflow, ADR, PR checklist and art pipeline

**Files:**
- Modify: `AGENTS.md`
- Modify: `README.md`
- Modify: `Docs/TECH_DESIGN.md`
- Create: `Docs/QUALITY_GATES.md`
- Create: `Docs/ART_PIPELINE.md`
- Create: `Docs/ADR/README.md`
- Create: `.github/pull_request_template.md`

**Interfaces:**
- Consumes: точные commands/results Tasks 1–4.
- Produces: правила specification → implementation → tests → evidence → PR → review и инструкции включения Unity CI.

- [ ] **Step 1: Document mandatory gates and remove stale P0.1 instruction**

`AGENTS.md` требует regression tests для каждой новой функции, запрещает неоговорённые изменения соседних систем, требует реальные Play Mode screenshots для визуальных задач и отдельные VERIFIED / NOT VERIFIED секции.

- [ ] **Step 2: Document CI setup without collecting credentials**

`Docs/QUALITY_GATES.md` помечает hosted Unity CI как BLOCKED до действий владельца: добавить полный GameCI Personal (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`) или Professional (`UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`) secret set, выполнить manual workflow, проверить reports, затем установить `VEILWARD_UNITY_CI=enabled`. PlayMode включается той же matrix.

- [ ] **Step 3: Specify future art pipeline**

Зафиксировать Blender source вне `Assets`, FBX export в `Assets/_Game/Art/Models`, meters/axes/pivots, naming, optional Git LFS с учётом cost, preliminary mobile budgets, GUID preservation, Unity/iPhone validation и provenance/license records для AI-generated content.

- [ ] **Step 4: Add ADR and PR templates**

ADR README содержит status/date/context/decision/consequences template. PR checklist содержит scope, tests, meta/GUID, economy invariants, screenshots, VERIFIED / NOT VERIFIED и запрет self-merge.

- [ ] **Step 5: Commit documentation**

```bash
git add AGENTS.md README.md Docs .github/pull_request_template.md
git commit -m "docs: define quality gates and art pipeline"
```

### Task 6: Final verification and PR

**Files:**
- Update only evidence/counts in: `README.md`, `Docs/QUALITY_GATES.md` if actual results differ.

- [ ] **Step 1: Run static suite**

Run: `./Scripts/run-static-checks.sh`
Expected: PASS, zero skipped self-tests.

- [ ] **Step 2: Run Unity suites**

Run: `./Scripts/run-edit-mode-tests.sh && ./Scripts/run-play-mode-tests.sh`
Expected: точные positive totals, `passed == total`, zero failed/inconclusive/skipped.

- [ ] **Step 3: Audit scope and generated files**

Run: `git status --short && git diff --check origin/main...HEAD && git diff --name-only origin/main...HEAD`
Expected: только P0.3 files; нет gameplay, package или Unity version changes; все новые Unity assets имеют `.meta`.

- [ ] **Step 4: Push and create PR without merge**

```bash
git push -u origin feature/p0-3-quality-gates
gh pr create --base main --title "P0.3: Quality Gates & Development Pipeline" --body-file TestResults/p0-3-pr-body.md
```

PR body содержит summary, architecture notes, exact commands/results, CI BLOCKED setup, known issues и docs changes. Ссылка передаётся на независимое review; merge не выполняется.
