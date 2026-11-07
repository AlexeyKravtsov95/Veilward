# P1.3 Targeting & Combat Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Добавить детерминированные First targeting, hitscan-атаки, урон/смерть и минимальное боевое Presentation для одной Test Tower.

**Architecture:** Чистый C# Domain хранит cooldown как `NextAttackTick`, использует int HP/урон и выдаёт ограниченный по lifecycle список `AttackEvent`. Unity Presentation только отображает состояние и события после каждого fixed step; существующие placement и grid сохраняются.

**Tech Stack:** Unity 6000.3.28f1, URP 17.3.0, C#, NUnit/Unity Test Framework 1.6.0, uGUI, LineRenderer.

## Global Constraints

- Рабочая ветка `feature/p1-3-targeting-combat` начинается с `main` после merged PR #4 (`36d9f391d5a80c965dd821bfd4604598b2a52d08`).
- Fixed step ровно 30 Hz; Domain не использует `UnityEngine`, `Time.deltaTime`, Scene или UI.
- Только First; Last/Strongest/Weakest, награды, другие башни, projectiles, стихии, новые волны и зависимости не добавляются.
- Стабильный порядок атак — возрастание `TowerId`; tie прогресса — минимальный `EnemyId`.
- HP, Kills и Base Health не становятся отрицательными; смерть и leak не дублируются.
- Не создавать новые коллекции в enemy/tower loops на тике; события явно очищаются Presentation.
- Сохранить Unity version, package versions, существующие GUID и `.meta`; Force Text и Visible Meta Files не менять.
- Итог — PR **P1.3: Targeting & Combat** без merge и без Apple signing/credentials.

## File Structure

- Create `Assets/_Game/Scripts/Domain/EnemyTargeting.cs`: allocation-free выбор First среди живых врагов в Range.
- Create `Assets/_Game/Scripts/Domain/AttackEvent.cs`: неизменяемый контракт одного выстрела.
- Modify `Assets/_Game/Scripts/Domain/TowerDefinition.cs`: int Damage и заранее вычисленный AttackIntervalTicks.
- Modify `Assets/_Game/Scripts/Domain/TowerState.cs`: NextAttackTick и MarkAttacked.
- Modify `Assets/_Game/Scripts/Domain/EnemyState.cs`: private-set Health, IsAlive и ApplyDamage.
- Modify `Assets/_Game/Scripts/Domain/BattleState.cs`: Kills и удаление погибших с конца.
- Modify `Assets/_Game/Scripts/Domain/BattleSimulation.cs`: заданный порядок тика и reused attack events.
- Create `Assets/_Game/Tests/EditMode/BattleCombatTests.cs`: боевые Domain tests.
- Modify `Assets/_Game/Scripts/Presentation/BattleScenarioConfig.cs`: serialized int towerDamage.
- Modify `Assets/_Game/Scripts/Presentation/BattleSceneController.cs`: drain событий, beam, health bars и Kills HUD.
- Modify `Assets/_Game/Scripts/Presentation/BattlePlacementController.cs`: Range Preview lifecycle.
- Modify `Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs`: P1.3 graybox, materials, range, HUD и references.
- Modify `Assets/_Game/Configs/TestWave.asset`: документированные Range 3, FireRate 2, Damage 25 через Unity serialization.
- Modify существующие config/visual tests только для новых serialized references и int Damage.
- Modify `Docs/TECH_DESIGN.md`, `README.md`: P1.3 contracts и verification.
- Create `Docs/review/P1.3/*.png`: реальные Game View screenshots.

---

### Task 1: First Targeting и целочисленный урон

**Files:**
- Create: `Assets/_Game/Scripts/Domain/EnemyTargeting.cs`
- Modify: `Assets/_Game/Scripts/Domain/TowerDefinition.cs`
- Modify: `Assets/_Game/Scripts/Domain/EnemyState.cs`
- Test: `Assets/_Game/Tests/EditMode/BattleCombatTests.cs`

**Interfaces:**
- Consumes: `PathDefinition.GetPosition(float)`, `EnemyState.DistanceAlongPath`, `EnemyDefinition.MaxHealth`.
- Produces: `EnemyTargeting.SelectFirst(PathPosition, float, IReadOnlyList<EnemyState>, PathDefinition)`, `TowerDefinition.AttackIntervalTicks`, `EnemyState.IsAlive`.

- [ ] **Step 1: Write failing targeting and conversion tests**

```csharp
[Test]
public void SelectFirst_ReturnsMaximumProgressInsideRange()
{
    var path = StraightPath();
    var enemies = new[] { Enemy(1, 1f), Enemy(2, 4f), Enemy(3, 2f) };
    var selected = EnemyTargeting.SelectFirst(new PathPosition(0f, 0f), 10f, enemies, path);
    Assert.That(selected.Id, Is.EqualTo(new EnemyId(2)));
}

[Test]
public void SelectFirst_EqualProgress_ReturnsLowestEnemyId()
{
    var path = StraightPath();
    var enemies = new[] { Enemy(9, 2f), Enemy(3, 2f), Enemy(5, 2f) };
    var selected = EnemyTargeting.SelectFirst(new PathPosition(0f, 0f), 10f, enemies, path);
    Assert.That(selected.Id, Is.EqualTo(new EnemyId(3)));
}

[TestCase(1f, 30)]
[TestCase(2f, 15)]
[TestCase(30f, 1)]
[TestCase(60f, 1)]
[TestCase(1.6f, 19)]
public void TowerDefinition_ConvertsFireRateToTicks(float fireRate, int expected)
{
    var definition = Definition(range: 10f, fireRate: fireRate, damage: 25);
    Assert.That(definition.AttackIntervalTicks, Is.EqualTo(expected));
}
```

`Enemy(id, distance)` создаёт `EnemyDefinition("shade", 100, 1.5f, 1, 0)` и `EnemyState(new EnemyId(id), definition, 100, distance)`. `StraightPath()` возвращает путь `(0,0) → (20,0)`.

- [ ] **Step 2: Run tests and verify RED**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: compilation FAIL because `EnemyTargeting` and `AttackIntervalTicks` do not exist.

- [ ] **Step 3: Implement minimal targeting and numeric contracts**

```csharp
public static EnemyState SelectFirst(
    PathPosition towerPosition,
    float range,
    IReadOnlyList<EnemyState> enemies,
    PathDefinition path)
{
    EnemyState selected = null;
    var rangeSquared = (double)range * range;
    for (var index = 0; index < enemies.Count; index++)
    {
        var enemy = enemies[index];
        if (!enemy.IsAlive) continue;
        var position = path.GetPosition(enemy.DistanceAlongPath);
        var dx = (double)position.X - towerPosition.X;
        var dy = (double)position.Y - towerPosition.Y;
        if (dx * dx + dy * dy > rangeSquared) continue;
        if (selected == null || enemy.DistanceAlongPath > selected.DistanceAlongPath ||
            (enemy.DistanceAlongPath == selected.DistanceAlongPath && enemy.Id.Value < selected.Id.Value))
        {
            selected = enemy;
        }
    }
    return selected;
}
```

В `TowerDefinition` заменить `float damage` на `int damage`, проверять `damage > 0`, вычислить:

```csharp
var roundedTicks = Math.Round(
    (double)SimulationTiming.TicksPerSecond / fireRate,
    MidpointRounding.AwayFromZero);
AttackIntervalTicks = (int)Math.Min(int.MaxValue, Math.Max(1d, roundedTicks));
```

В `EnemyState` добавить `public int Health { get; private set; }`, `public bool IsAlive => Health > 0;` и internal `ApplyDamage(int)` с clamp к нулю и возвратом `true` только при первом переходе к смерти.

- [ ] **Step 4: Run focused suite and verify GREEN**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: новые targeting/conversion tests PASS; существующие float Damage call sites исправлены на int до компиляции.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Game/Scripts/Domain Assets/_Game/Tests/EditMode/BattleCombatTests.cs*
git commit -m "feat: add first targeting and integer damage"
```

---

### Task 2: Cooldown, порядок атак, смерть и события

**Files:**
- Create: `Assets/_Game/Scripts/Domain/AttackEvent.cs`
- Modify: `Assets/_Game/Scripts/Domain/TowerState.cs`
- Modify: `Assets/_Game/Scripts/Domain/BattleState.cs`
- Modify: `Assets/_Game/Scripts/Domain/BattleSimulation.cs`
- Test: `Assets/_Game/Tests/EditMode/BattleCombatTests.cs`

**Interfaces:**
- Consumes: Task 1 `SelectFirst`, `AttackIntervalTicks`, `ApplyDamage`.
- Produces: `BattleSimulation.AttackEvents`, `ClearAttackEvents()`, `BattleState.Kills`, `AttackEvent(TowerId, EnemyId, long, int, bool)`.

- [ ] **Step 1: Write failing simulation tests**

```csharp
[Test]
public void Step_Cooldown_AttacksOnExactTicks()
{
    var simulation = CombatSimulation(fireRate: 1f, damage: 25);
    Place(simulation, new GridCoordinate(0, 2));
    simulation.StartWave();
    Step(simulation, 1);
    Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(75));
    Step(simulation, 29);
    Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(75));
    Step(simulation, 1);
    Assert.That(simulation.State.Enemies[0].Health, Is.EqualTo(50));
    Assert.That(simulation.AttackEvents.Select(e => e.Tick), Is.EqualTo(new long[] { 1, 31 }));
}

[Test]
public void Step_TwoTowers_AttackInTowerIdOrderAndSkipDeadTarget()
{
    var simulation = CombatSimulation(fireRate: 30f, damage: 100, count: 2, spawnInterval: 1);
    Place(simulation, new GridCoordinate(-1, 2));
    Place(simulation, new GridCoordinate(1, 2));
    simulation.StartWave();
    simulation.Step();
    Assert.That(simulation.AttackEvents.Select(e => e.AttackerTowerId.Value), Is.EqualTo(new[] { 1, 2 }));
    Assert.That(simulation.AttackEvents.Select(e => e.TargetEnemyId.Value), Is.EqualTo(new[] { 1, 2 }));
    Assert.That(simulation.State.Kills, Is.EqualTo(2));
}
```

Добавить отдельные tests: без цели HP не меняется; 25/100/150 damage даёт 75/0/0 HP; lethal event один; Kills не растёт повторно; убитый не вызывает leak; Preparation/WaveComplete/Defeat не создают событий; событие содержит IDs, tick, Damage, WasLethal; два прогона имеют одинаковые tick/phase/base/gold/Kills/enemies/tower cooldowns/events.

- [ ] **Step 2: Run tests and verify RED**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: compilation FAIL because cooldown, events and Kills APIs do not exist.

- [ ] **Step 3: Implement fixed simulation order**

`TowerState`: `NextAttackTick` изначально 0; `MarkAttacked(long tick)` устанавливает `NextAttackTick = tick + Definition.AttackIntervalTicks`.

`BattleSimulation.Step()` после существующих movement/leaks вызывает:

```csharp
var towers = State.Towers;
for (var towerIndex = 0; towerIndex < towers.Count; towerIndex++)
{
    var tower = towers[towerIndex];
    if (State.Tick < tower.NextAttackTick) continue;
    var center = Grid.Map.GetCellCenter(tower.Coordinate);
    var target = EnemyTargeting.SelectFirst(center, tower.Definition.Range, State.Enemies, _path);
    if (target == null) continue;
    var wasLethal = target.ApplyDamage(tower.Definition.Damage);
    tower.MarkAttacked(State.Tick);
    _attackEvents.Add(new AttackEvent(
        tower.Id, target.Id, State.Tick, tower.Definition.Damage, wasLethal));
}
State.RemoveDefeatedEnemies();
```

Затем сохранить существующие проверки Defeat/WaveComplete. `RemoveDefeatedEnemies()` идёт от `Count - 1` к 0, увеличивает Kills на каждый удалённый `!IsAlive`. Список `_attackEvents` и read-only wrapper создаются один раз; `ClearAttackEvents()` только вызывает `Clear()`.

- [ ] **Step 4: Run full EditMode suite**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: all Domain tests PASS, включая P1.1 no-tower Base Health 15 и P1.2 placement invariants.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Game/Scripts/Domain Assets/_Game/Tests/EditMode/BattleCombatTests.cs*
git commit -m "feat: simulate tower attacks and enemy deaths"
```

---

### Task 3: Beam, health bars, Kills и Range Preview

**Files:**
- Modify: `Assets/_Game/Scripts/Presentation/BattleSceneController.cs`
- Modify: `Assets/_Game/Scripts/Presentation/BattlePlacementController.cs`
- Modify: `Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs`
- Modify: `Assets/_Game/Scripts/Presentation/BattleScenarioConfig.cs`
- Modify: `Assets/_Game/Tests/EditMode/BattlePlacementVisualTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/BattleScenarioConfigTests.cs`
- Test: `Assets/_Game/Tests/EditMode/BattleSceneBuilderTests.cs`

**Interfaces:**
- Consumes: `BattleSimulation.AttackEvents`, `ClearAttackEvents()`, `EnemyState.Health`, `BattleState.Kills`.
- Produces: serialized `attackBeamMaterial`, `healthBarBackgroundMaterial`, `healthBarFillMaterial`, `worldCamera`, `rangePreview`; реальная P1.3 Battle scene.

- [ ] **Step 1: Add failing Presentation tests**

```csharp
[Test]
public void Selection_ShowsRangePreviewAndCancelHidesIt()
{
    Invoke(placement, "Start");
    Assert.That(placement.TrySelectCell(new GridCoordinate(-3, 1)), Is.True);
    Assert.That(rangePreview.gameObject.activeSelf, Is.True);
    placement.CancelSelection();
    Assert.That(rangePreview.gameObject.activeSelf, Is.False);
}
```

Добавить builder test, проверяющий `Range Preview` с `LineRenderer.loop == true`, 64 positions и радиусом serialized Tower Range. Обновить setup существующих visual tests тремя `Material`, tagged MainCamera и `LineRenderer`, чтобы обязательные references были реальными, не null.

- [ ] **Step 2: Run tests and verify RED**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: FAIL на отсутствующем `rangePreview` serialized property/новых Presentation contracts.

- [ ] **Step 3: Implement event-driven combat visuals**

В fixed-step while сразу после `_simulation.Step()` вызвать `ProcessAttackEvents()`. Метод перебирает события, берёт текущие tower/enemy positions, создаёт два-точечный world-space LineRenderer и `Destroy(beamObject, 0.12f)`, затем `_simulation.ClearAttackEvents()` в `finally`.

Enemy view создаёт background/fill quads из общих materials. На синхронизации:

```csharp
var ratio = (float)enemy.Health / enemy.Definition.MaxHealth;
view.HealthFill.localScale = new Vector3(HealthBarWidth * ratio, 0.1f, 1f);
view.HealthFill.localPosition = new Vector3(-HealthBarWidth * (1f - ratio) * 0.5f, 0f, -0.01f);
view.HealthBarRoot.rotation = worldCamera.transform.rotation;
```

HUD добавляет `$"Kills: {state.Kills}\n"`. Placement Start настраивает 64 точки XZ-круга по `battle.Simulation.TowerDefinition.Range`; selection задаёт центр и активирует объект, Cancel/Start Wave деактивируют.

- [ ] **Step 4: Rebuild Battle through Unity Editor**

Run: использовать существующий Editor/Pipeline или one-shot batch fallback и вызвать `Veilward.Editor.BattleSceneBuilder.Build` с menu item `Veilward/Build P1.3 Battle Scene`.
Expected: Battle сохранена, `ValidateRequiredReferences` проходит, 0 compilation errors; TestWave GUID не изменился.

- [ ] **Step 5: Run tests and commit**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: all EditMode tests PASS.

```bash
git add Assets/_Game Docs/review/P1.3
git commit -m "feat: present tower combat in battle scene"
```

---

### Task 4: Документация и полная verification

**Files:**
- Modify: `Docs/TECH_DESIGN.md`
- Modify: `README.md`
- Create: `Docs/review/P1.3/01-range.png`, `02-attack.png`, `03-wave-complete.png`
- Create: `TestResults/` (ignored), `Builds/` (ignored)

**Interfaces:**
- Consumes: рабочая P1.3 scene и все Domain contracts.
- Produces: воспроизводимые команды, фактические результаты и review evidence.

- [ ] **Step 1: Document exact rules**

В `TECH_DESIGN.md` добавить P1.3 implemented contract с шестью стадиями тика, int Damage, формулой AttackIntervalTicks, initial/next cooldown, First tie, обратным удалением и lifecycle событий. README обновить до P1.3, указать Range 3 / FireRate 2 / Damage 25 и команды запуска.

- [ ] **Step 2: Run all automated checks**

```bash
./Scripts/run-edit-mode-tests.sh
git diff --check
```

Expected: Total > 0, Passed == Total, Failed/Inconclusive/Skipped = 0; diff check без вывода. Grep Domain подтверждает отсутствие `UnityEngine`.

- [ ] **Step 3: Verify real Play Mode without towers**

Запустить настоящий Unity Play Mode Battle без построек, дождаться WaveComplete. Expected: пять leaks, Base Health 15, Kills 0, terminal tick заморожен. Сохранить итоговый screenshot.

- [ ] **Step 4: Verify real Play Mode with towers**

Через поддерживаемый virtual input выбрать свободную клетку, Confirm, Start Wave. Expected: Range виден при selection, beam соответствует событию, health bars уменьшаются, минимум один Kills, уничтоженный исчезает, Base Health не получает урон от убитого. Сохранить Range/attack/final screenshots и фактические state values.

- [ ] **Step 5: Export iOS without signing and inspect artifacts**

Использовать установленный Unity 6000.3.28f1 iOS Build Support, Device SDK export в ignored `Builds/iOS/P1.3-Review`, без signing. Expected при успехе: Build Succeeded, `Veilward.Domain.cpp` присутствует, Editor-only сборка отсутствует. При недоступности записать точную причину, не выдумывать результат.

- [ ] **Step 6: Commit docs and evidence**

```bash
git add README.md Docs/TECH_DESIGN.md Docs/review/P1.3
git commit -m "docs: verify P1.3 targeting combat"
```

---

### Task 5: Review и PR

**Files:**
- Inspect: все изменённые файлы, `.meta`, package manifests и git diff.

**Interfaces:**
- Consumes: все предыдущие commits и verification artifacts.
- Produces: GitHub PR **P1.3: Targeting & Combat**, не merged.

- [ ] **Step 1: Review scope and generated metadata**

```bash
git status --short
git diff origin/main...HEAD --stat
git diff origin/main...HEAD -- Packages/manifest.json Packages/packages-lock.json ProjectSettings/ProjectVersion.txt
git diff --check
```

Expected: только P1.3 files; Unity/package versions не изменены; новые `.cs` имеют `.meta`; `.kimi-inbox`, TestResults и Builds не добавлены.

- [ ] **Step 2: Push branch**

```bash
git push -u origin feature/p1-3-targeting-combat
```

Expected: remote branch создан без force.

- [ ] **Step 3: Create Russian PR without merge**

`gh pr create --base main --head feature/p1-3-targeting-combat --title "P1.3: Targeting & Combat"` с Summary, Architecture decisions, шестью стадиями симуляции, exact test commands/results, Play Mode evidence/скриншотами, iOS result и Known limitations. Не вызывать `gh pr merge`.

- [ ] **Step 4: Final verification**

```bash
gh pr view --json url,state,isDraft,mergeStateStatus,title
git status --short --branch
```

Expected: PR OPEN, merge не выполнен; в ответе указаны фактические Passed/Total, Play Mode values, iOS status и непроверенный физический iPhone.
