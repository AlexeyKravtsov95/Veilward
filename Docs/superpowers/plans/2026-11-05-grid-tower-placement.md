# Grid & Tower Placement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Добавить выбор клетки, подтверждаемое строительство одной тестовой башни и ручной запуск существующей волны в Battle.

**Architecture:** Чистая C# `BattleSimulation` владеет картой, занятостью, золотом и уникальными ID; Unity Presentation только адаптирует Input System/uGUI и отражает read-only состояние. Существующий `BattleScenarioConfig` и `BattleSceneBuilder` остаются источниками конфигурации и graybox-сцены.

**Tech Stack:** Unity 6000.3.28f1, URP 17.3.0, Input System 1.20.1, uGUI 2.0.0, NUnit EditMode/PlayMode, C# без UnityEngine в Domain.

## Global Constraints

- Работать только в `feature/p1-2-grid-tower-placement`, созданной от `origin/main` после merge PR #3; не merge PR.
- Не менять Unity/URP/package versions и не добавлять зависимости.
- Не добавлять атаки, targeting, upgrades, sell, элементы, сохранения или финальный art.
- Начальные serialized-значения: grid 12×8, origin `(-6,-4)`, cell size 1, gold 150, tower price 60.
- Первый pointer tap только выбирает клетку; покупка выполняет исключительно Confirm.
- Строительство разрешено только в `Preparation`; `StartWave` идемпотентен.
- Все новые runtime assets получают `.meta`; Domain не ссылается на `UnityEngine`.
- Каждый завершённый task проверяется `./Scripts/run-edit-mode-tests.sh`; перед PR также Play Mode, screenshots и iOS export без signing при доступности.

---

### Task 1: Grid map и заблокированный маршрут

**Files:**
- Create: `Assets/_Game/Scripts/Domain/GridCellState.cs`
- Create: `Assets/_Game/Scripts/Domain/GridMapDefinition.cs`
- Create: `Assets/_Game/Scripts/Domain/BattleGrid.cs`
- Create: `Assets/_Game/Tests/EditMode/BattleGridTests.cs`

**Interfaces:**
- Consumes: `GridCoordinate`, `PathPosition`, `PathDefinition`.
- Produces: `GridMapDefinition(int width, int height, GridCoordinate origin, float cellSize)`, `Contains`, `TryGetCoordinate`, `GetCellCenter`; `BattleGrid.GetState`, `TryOccupy`; `GridCellState.{Available,Blocked,Occupied}`.

- [ ] **Step 1: Write failing grid tests**

Создать NUnit-тесты со следующими проверками:

```csharp
[Test]
public void CoordinateConversion_RoundTripsCellCenter()
{
    var map = new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 1f);
    var cell = new GridCoordinate(-5, -2);
    var center = map.GetCellCenter(cell);
    Assert.That(map.TryGetCoordinate(center, out var actual), Is.True);
    Assert.That(actual, Is.EqualTo(cell));
}

[Test]
public void Constructor_PathBlocksIntermediateCells()
{
    var map = new GridMapDefinition(12, 8, new GridCoordinate(-6, -4), 1f);
    var path = new PathDefinition(new[]
    {
        new GridCoordinate(-5, -2), new GridCoordinate(0, -2),
        new GridCoordinate(0, 2), new GridCoordinate(5, 2)
    });
    var grid = new BattleGrid(map, path);
    Assert.That(grid.GetState(new GridCoordinate(-3, -2)), Is.EqualTo(GridCellState.Blocked));
    Assert.That(grid.GetState(new GridCoordinate(0, 1)), Is.EqualTo(GridCellState.Blocked));
    Assert.That(grid.GetState(new GridCoordinate(-3, 0)), Is.EqualTo(GridCellState.Available));
}

[TestCase(-7, -4)]
[TestCase(6, -4)]
[TestCase(-6, -5)]
[TestCase(-6, 4)]
public void GetState_OutsideMap_Throws(int x, int y)
{
    var grid = CreateEmptyGrid();
    Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetState(new GridCoordinate(x, y)));
}
```

Добавить тесты diagonal segment rasterization, boundary conversion и `TryOccupy`: blocked/outside не меняются, available однократно становится occupied, повтор возвращает false.

- [ ] **Step 2: Run tests to verify compilation failure**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: FAIL, отсутствуют `GridMapDefinition`, `BattleGrid`, `GridCellState`.

- [ ] **Step 3: Implement focused pure C# grid types**

`GridCellState` — enum. `GridMapDefinition` валидирует positive width/height и finite positive cellSize. `origin` — координата минимальной клетки; `Contains` использует origin ≤ coordinate < origin + size. Центры клеток совпадают с integer grid positions при cellSize 1: `GetCellCenter` возвращает `new PathPosition(cell.X * cellSize, cell.Y * cellSize)`. `TryGetCoordinate` выполняет `Math.Floor(axis / cellSize + 0.5f)` по каждой оси, возвращает false вне карты и для non-finite position.

`BattleGrid` хранит одномерный `GridCellState[]`. При создании проходит каждый сегмент пути grid DDA/supercover traversal и помечает каждую пересекаемую клетку blocked, включая обе клетки при прохождении точно через grid corner. Публичный `GetState` валидирует координату. `TryOccupy` возвращает false для outside/non-available; единственный успешный переход — available → occupied. Не использовать UnityEngine или collections allocation в `GetState`/`TryOccupy`.

- [ ] **Step 4: Run grid and regression tests**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: PASS, новые grid tests и все P1.1 tests зелёные.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Game/Scripts/Domain Assets/_Game/Tests/EditMode/BattleGridTests.cs*
git commit -m "feat: add build grid domain"
```

### Task 2: Tower definitions и атомарная команда размещения

**Files:**
- Create: `TowerDefinitionId.cs`, `TowerId.cs`, `TowerDefinition.cs`, `TowerState.cs`, `PlaceTowerCommand.cs`, `PlaceTowerResult.cs`, `PlaceTowerFailureReason.cs` в `Assets/_Game/Scripts/Domain/`
- Modify: `Assets/_Game/Scripts/Domain/BattleState.cs`
- Modify: `Assets/_Game/Scripts/Domain/BattleSimulation.cs`
- Modify: `Assets/_Game/Scripts/Domain/IBattleSimulation.cs`
- Create: `Assets/_Game/Tests/EditMode/TowerPlacementTests.cs`

**Interfaces:**
- Consumes: Task 1 `BattleGrid`, existing `BattlePhase` and `BattleState`.
- Produces: `BattleSimulation(GridMapDefinition, PathDefinition, WaveDefinition, TowerDefinition, int baseHealth, int startingGold)`, `PlaceTowerResult PlaceTower(PlaceTowerCommand)`, `BattleState.Towers`, `BattleState.Gold`, `BattleSimulation.Grid`.

- [ ] **Step 1: Write the twelve placement acceptance tests**

Создать named NUnit tests для: success, road, outside, occupied, insufficient gold, debit once, repeat command no debit, invalid definition rejection, unique IDs, non-negative gold, Combat rejection, route unchanged. Основной успешный сценарий:

```csharp
var simulation = CreateSimulation(startingGold: 150, price: 60);
var result = simulation.PlaceTower(new PlaceTowerCommand(
    simulation.TowerDefinition.Id, new GridCoordinate(-3, 0)));
Assert.That(result.Success, Is.True);
Assert.That(simulation.State.Gold, Is.EqualTo(90));
Assert.That(simulation.State.Towers.Count, Is.EqualTo(1));
Assert.That(simulation.Grid.GetState(new GridCoordinate(-3, 0)), Is.EqualTo(GridCellState.Occupied));
```

Повтор той же команды проверяет `Occupied`, Gold 90, Towers.Count 1 и тот же единственный TowerId. Combat test сначала вызывает `StartWave`. Route test сохраняет `PathDefinition` reference/TotalLength/positions до и после покупки. Invalid definition tests проверяют ArgumentException/ArgumentOutOfRangeException в `TowerDefinition` и `UnknownTowerDefinition` для неизвестного ID.

- [ ] **Step 2: Run tests to verify compilation failure**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: FAIL на новых Tower API.

- [ ] **Step 3: Implement value objects and state**

`TowerDefinitionId` следует валидации `EnemyDefinitionId`; `TowerId` — positive integer value object. `TowerDefinition` принимает ID, non-negative integer price, finite positive range/fireRate/damage. `TowerState` требует non-null definition. `PlaceTowerFailureReason`: `None`, `InvalidPhase`, `UnknownTowerDefinition`, `OutsideMap`, `Blocked`, `Occupied`, `InsufficientGold`. `PlaceTowerResult` предоставляет `Success`, `FailureReason`, `Tower`; failure не содержит tower.

`BattleState.Gold` становится private set; добавляются reused `List<TowerState>` и read-only wrapper, конструктор проверяет уникальность tower IDs и координат. Internal `Debit` отклоняет negative amount и amount > Gold, затем вычитает один раз. Internal `AddTower` добавляет уже проверенный экземпляр.

- [ ] **Step 4: Implement atomic simulation command**

Сохранить старый constructor как delegation с default empty 1×1 map вне тестового пути и `startingGold: 0` только для обратной совместимости P1.1; основной config использует новый constructor. Новый constructor создаёт `BattleGrid(map, path)` и хранит одну tower definition. `PlaceTower` выполняет проверки в порядке: Preparation, known ID, map bounds, grid state, gold. При success создать ID из `_nextTowerId++`, вызвать `Grid.TryOccupy`, `State.Debit`, `State.AddTower`; ни один UI/Unity тип не используется. `IBattleSimulation` добавляет только `PlaceTower`, не ломая `Step`.

- [ ] **Step 5: Run placement and P1.1 tests**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: PASS; все 12 acceptance cases и прежние wave/leak tests зелёные.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Game/Scripts/Domain Assets/_Game/Tests/EditMode/TowerPlacementTests.cs*
git commit -m "feat: add atomic tower placement command"
```

### Task 3: Serialized scenario configuration

**Files:**
- Modify: `Assets/_Game/Scripts/Presentation/BattleScenarioConfig.cs`
- Modify: `Assets/_Game/Configs/TestWave.asset` через Unity serialization
- Modify: `Assets/_Game/Tests/EditMode/BattleScenarioConfigTests.cs`

**Interfaces:**
- Consumes: Task 2 main constructor and `TowerDefinition`.
- Produces: config-driven grid, starting gold, test tower; unchanged `CreateSimulation()` public entry point.

- [ ] **Step 1: Add failing config tests**

Через `SerializedObject` задать `gridWidth=10`, `gridHeight=6`, `gridOrigin=(-5,-3)`, `cellSize=1`, `startingGold=175`, `towerId="dummy"`, `towerPrice=65`, range/fireRate/damage. Проверить `State.Gold`, `Grid.Map` properties, `TowerDefinition` values и успешную покупку по новой цене. Default config test проверяет 12×8, origin `(-6,-4)`, 150/60.

- [ ] **Step 2: Run tests to verify failure**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: FAIL до добавления serialized properties.

- [ ] **Step 3: Extend config and validate**

Добавить `[SerializeField]` fields с указанными defaults; `CreateSimulation` создаёт `GridMapDefinition`, `TowerDefinition` и вызывает новый constructor. `OnValidate` clamps width/height/cellSize/gold/price и numeric tower stats; пустой tower ID не маскировать silently — Domain validation остаётся gate. Обновить asset через open Editor/Unity MCP, не текстовой подменой GUID.

- [ ] **Step 4: Run tests**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: PASS, config values и P1.1 path conversion остаются корректными.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Game/Scripts/Presentation/BattleScenarioConfig.cs* Assets/_Game/Configs/TestWave.asset* Assets/_Game/Tests/EditMode/BattleScenarioConfigTests.cs*
git commit -m "feat: configure grid and test tower"
```

### Task 4: Battle placement input, HUD и graybox scene

**Files:**
- Create: `Assets/_Game/Scripts/Presentation/BattlePlacementController.cs`
- Modify: `Assets/_Game/Scripts/Presentation/BattleSceneController.cs`
- Modify: `Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs`
- Modify: `Assets/_Game/Scenes/Battle.unity` через Unity Editor/MCP
- Create: `Assets/_Game/Prefabs/TestTower.prefab` через Unity Editor/MCP
- Create/modify: graybox materials в `Assets/_Game/Art/Materials/`
- Modify: `Assets/_Game/Tests/EditMode/BattleSceneBuilderTests.cs`

**Interfaces:**
- Consumes: `BattleSimulation.State`, `Grid`, `TowerDefinition`, `PlaceTower`, `StartWave`.
- Produces: interactive Battle scene with Confirm/Cancel/Start Wave, tower views, selection and preview.

- [ ] **Step 1: Add failing scene contract tests**

Проверить builder output: controller references scenario/enemy/tower/HUD/placement, grid root содержит 96 cells, path cells blocked visuals, Confirm/Cancel/Start Wave buttons имеют onClick handlers или serialized controller references, tower prefab существует и не содержит Domain/Unity attack stubs. Добавить pure presentation helper tests для выбора: selection не меняет Gold/Towers; Confirm success создаёт view только после Domain success; Cancel сбрасывает selection.

- [ ] **Step 2: Run tests to verify failure**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: FAIL на отсутствующих scene/controller references.

- [ ] **Step 3: Implement Battle controller orchestration**

Удалить `_simulation.StartWave()` из `Start`. Добавить публичные `StartWave()`, `ConfirmPlacement()`/`CancelPlacement()` только как adapter calls. Синхронизировать `_towerViews` по `TowerId` так же, как enemy views, без создания/списания при selection. HUD добавляет Gold и Phase. StartWave очищает selection и вызывает Domain один раз; повторная кнопка не даёт spawn duplicates. Simulation accumulator остаётся fixed 30 Hz.

- [ ] **Step 4: Implement Input System placement adapter**

`BattlePlacementController` использует `UnityEngine.InputSystem.EnhancedTouch` и `Mouse.current`/`Touchscreen.current` через installed Input System, без нового package. На press начало, если `EventSystem.current.IsPointerOverGameObject()` для mouse или pointerId для touch — ignore. Ray через `Camera.ScreenPointToRay` пересекает математическую plane поля Y=0; Domain `TryGetCoordinate` определяет клетку. Первый release/tap доступной клетки устанавливает selection и preview, но не вызывает `PlaceTower`. UI Confirm отдельно вызывает command; Cancel/StartWave/Combat очищают preview. UI повторно не передаёт input на поле.

- [ ] **Step 5: Rebuild graybox scene through Unity MCP**

Builder создаёт 12×8 thin tile primitives с разными materials available/blocked; blocked route geometry остаётся из Domain path. Tower placeholder — оригинальная композиция primitives, не licensed asset. Selection — thin highlight, preview — отдельный translucent/transparent-style primitive instance, не входящий в `State.Towers`. Создать Confirm/Cancel contextual panel, Gold text и Start Wave в существующем uGUI Canvas с GraphicRaycaster/EventSystem+InputSystemUIInputModule. Обновить menu item до `Veilward/Build P1.2 Battle Scene`, сохранить все required references и `.meta`.

- [ ] **Step 6: Run EditMode suite and compile**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: PASS; Console 0 errors, Editor-only builder excluded from player.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Game Docs/TECH_DESIGN.md README.md
git commit -m "feat: add interactive tower placement battle"
```

### Task 5: Play Mode evidence, iOS export, docs and PR

**Files:**
- Modify: `README.md`, `Docs/TECH_DESIGN.md`
- Create: ignored review evidence under `TestResults/P1.2/` and `Builds/iOS/P1.2-Review/`
- Create: PR `P1.2: Grid & Tower Placement`

**Interfaces:**
- Consumes: completed Tasks 1–4.
- Produces: reproducible verification record and unmerged review PR.

- [ ] **Step 1: Run Play Mode interaction checks**

Через Unity MCP открыть Battle, войти Play Mode и записать: initial Preparation/Gold150/no enemies; first click selection with Gold150/Towers0; Cancel clears; road click rejected; Confirm creates one tower/Gold90; occupied repeat no change; Start Wave enters Combat and disables build; repeated StartWave keeps spawn schedule; five Shade traverse existing path and final BaseHealth15/WaveComplete. Захватить Game View before/after placement в `TestResults/P1.2/`.

- [ ] **Step 2: Run full tests and inspect Console**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: PASS, Total>0, Failed=Inconclusive=Skipped=0. Проверить Console 0 errors и сохранить точные totals/log excerpts.

- [ ] **Step 3: Verify iOS player export**

Выполнить Unity iOS export без signing в `Builds/iOS/P1.2-Review`; проверить, что exported assemblies не содержат `Veilward.Editor.dll`. Не запрашивать credentials. Не утверждать physical device/safe-area/touch verification без устройства.

- [ ] **Step 4: Update docs and self-check scope**

README заменяет автозапуск P1.1 на Preparation/Start Wave и описывает Confirm/Cancel, config 150/60 и команду builder. TECH_DESIGN добавляет P1.2 contract. Проверить `git diff --check`, status, `.meta`, changed package files absent, Domain `UnityEngine` matches absent, no secrets, only scoped files.

- [ ] **Step 5: Commit, push and open unmerged PR**

```bash
git add README.md Docs/TECH_DESIGN.md Assets/_Game
git commit -m "docs: document P1.2 placement workflow"
git push -u origin feature/p1-2-grid-tower-placement
gh pr create --base main --title "P1.2: Grid & Tower Placement" --body-file <russian-pr-body>
```

PR body включает summary, architecture, exact commands/results, before/after screenshots, known limitations (no attacks, no physical iPhone unless actually tested) и чек-лист AGENTS. Не merge; дождаться review.
