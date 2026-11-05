# VEILWARD — Technical Design / Architecture v0.1

## 1. Stack

- **Unity 6.3 LTS** (конкретный патч закрепить в `ProjectVersion.txt`), C#.
- **2.5D:** URP, orthographic camera, оптимизированные shader graph/cel-shading (при необходимости кастомный лёгкий shader).
- **iOS:** Xcode актуальной совместимой версии, Metal, IL2CPP, ARM64. Начальная продуктовая цель iOS 17+ — подтвердить устройствами и рынком; совместимость Unity/iOS проверить по точному патчу перед сборкой.
- **Input:** Unity Input System, touch action maps.
- **UI:** uGUI для world-space выбора, мобильного HUD и динамического UI; UI Toolkit допустим для статичных меню после проверки реальной совместимости и workflow — не мешать два подхода без причины.
- **Content:** ScriptableObject definitions с validation; Addressables — вводить при появлении обоснованной потребности в управлении активами, без обязательного CDN на MVP.
- **Save:** версионированный локальный JSON/бинарный файл с атомарной заменой и резервным предыдущим сохранением; никакого PlayerPrefs как основного хранилища прогресса.
- **Testing:** Unity Test Framework (EditMode, PlayMode), профилирование Unity Profiler на устройстве и Xcode Instruments.
- **CI:** GitHub Actions for compile/tests; macOS runner/локальный Mac для iOS Xcode build; подпись/публикация — отдельно, секреты не выдавать агенту.

## 2. Architecture: data / logic / presentation

```
Assets/_Game/
  Art/
  Audio/
  Prefabs/
  Scenes/            Bootstrap, MainMenu, Battle, Results
  Configs/           ScriptableObject definitions
  Scripts/
    Domain/           pure C#; no MonoBehaviour; battle rules/state
    Application/      orchestration, commands, game phases
    Infrastructure/   file saves, config loader, platform adapters
    Presentation/     MonoBehaviours, pooling views, HUD, VFX
    Editor/           importers, config validators, build helpers
  Tests/
    EditMode/
    PlayMode/
Docs/
  GDD.md
  TECH_DESIGN.md
  ROADMAP.md
  AGENTS.md
```

**Domain independence:** battle engine must be unit-testable without Scene, prefab, frame time, or Camera. No `GameObject.Find`/`FindObjectOfType` in battle loop. `MonoBehaviour` is a renderer/controller adapter, not the source of game rules. Домен выделен в `Veilward.Domain.asmdef` с `noEngineReferences: true`; основная `Veilward.asmdef` ссылается на него.

## 3. Simulation rules

- Fixed simulation step: **30 Hz** (`SimulationTiming.TicksPerSecond`). `IBattleSimulation.Step()` explicitly called by orchestration. Presentation синхронизирует views с текущим read-only state; визуальная интерполяция между тиками — отдельная задача. Доменное состояние не использует `Time.deltaTime`.
- Seeded deterministic RNG wrapped by `IRandomSource`; preserve RNG state in save/replay checkpoint.
- Currency and health: integer arithmetic; event ordering strictly defined.
- Path distance as scalar progression along precomputed segments/waypoints; no per-enemy NavMesh or runtime path search for fixed lanes.
- Enemy targeting strategies: first (distance to exit), last, strongest, weakest. Ties resolve stable by enemy ID.
- Damage order: base hit → armor/resistance modifiers → status application → eligible reaction (cooldown/priority) → death → reward once.
- Slow stacking: multiplicative or max-based policy fixed in one centralized combat rule (for first prototype recommend maximum effect and cap to avoid perma-lock).
- Burst/chains: bounded number of secondary targets and no recursive proc from secondary damage unless explicitly enabled with tests.
- Spawn/reward/phase state must support repeated load safely: never give duplicate rewards for a wave.

### P1.1 implemented contract

`PathDefinition` предрасчитывает Euclidean lengths и cumulative boundaries в constructor. `GetPosition(distance)` ограничивает distance границами пути и линейно интерполирует между waypoints, не выделяя collections; diagonal segments используют Euclidean length.

`WaveDefinition` хранит validated single-group schedule. `BattleSimulation.StartWave()` идемпотентно переводит Preparation в Combat и обрабатывает spawn tick 0. Каждый `Step()` сначала увеличивает tick, затем выполняет due spawns, после чего движение и leaks в порядке стабильных enemy IDs. Скорость задаётся в path units/second и делится на 30 за тик. Leak сначала удаляет врага, затем один раз применяет integer damage. Health ограничено нулём; Defeat приоритетнее WaveComplete; terminal `Step()` не изменяет состояние.

`BattleState` хранит один reused enemies list и read-only wrapper; `EnemyState` допускает только internal Domain mutation. Step не создаёт snapshots, lists или enemy state objects на каждом тике. `BattleSceneController` — тонкий Unity adapter: accumulator с ограничением frame delta вызывает fixed steps, а LateUpdate синхронизирует prefab views и HUD без изменения Domain rules из UI.

После исчерпания одной тестовой волны и последнего non-lethal leak фаза становится `WaveComplete`; это не кампания, results flow или полноценная win condition. Lethal leak устанавливает `Defeat`. Состояние `Victory` зарезервировано для будущей полноценной победы и не используется симуляцией тестовой волны.

`BattleSceneBuilder` находится в Editor-only `Veilward.Editor.asmdef` и не компилируется в iOS player. Единый source of truth маршрута — `BattleScenarioConfig.CreateSimulation().Path`: builder создаёт дорожные сегменты, Spawn Marker и Seal Heart по Domain waypoints, без дублирующего массива координат.

### P1.2 implemented contract

`GridMapDefinition` задаёт карту 12×8 с origin `(-6,-4)` и cell size 1. `BattleGrid` помечает клетки маршрута как `Blocked`, включая промежуточные и diagonal/supercover cells, а занятость изменяет только Domain. Маршрут при строительстве не пересчитывается и не меняется.

`BattleSimulation.PlaceTower()` атомарно проверяет фазу `Preparation`, известный `TowerDefinitionId`, границы и состояние клетки и достаточность золота. При успехе он один раз списывает цену, занимает клетку и добавляет `TowerState` с уникальным последовательным `TowerId`; при ошибке состояние не меняется. Повторная команда по занятой клетке не списывает золото. Текущий `TestTower` стоит 60 gold при стартовых 150 gold; атака, продажа и улучшения не входят в контракт.

`BattlePlacementController` только преобразует Input System mouse/touch в выбор клетки и вызывает Presentation/Domain API. Первый tap показывает selection и preview без покупки, Confirm применяет команду, Cancel очищает selection. UI raycasts не передаются на поле. Start Wave доступен вручную только в `Preparation`; при входе в Combat selection отменяется и строительство запрещено. `BattleSceneController` создаёт tower view только после успеха Domain и показывает Gold/Towers в HUD.

## 4. Core domain interfaces (illustrative, not API lock)

```
BattleState { Phase, Tick, WaveIndex, BaseHealth, Gold, Towers, Enemies, RNGState }
IBattleSimulation { Step(); Apply(BattleCommand); Snapshot(); }
BattleCommand = PlaceTower | UpgradeTower | SellTower | SetTargetMode | CastSkill | StartWave
TowerDefinition { Id, ElementRequirements, Price, Range, FireRate, Damage, StatusEffect }
EnemyDefinition { Id, HP, Speed, Armor, Resistances, LeakDamage, Reward }
WaveDefinition { Groups, SpawnOffsets, BreakDuration, BossMetadata }
ReactionDefinition { Pair, Effect, CooldownTicks, TargetLimit, Priority }
```

Use strongly typed IDs; validate unique keys/references; runtime must not depend on localised names. Define explicit invalid-command results (insufficient gold, occupied tile, unknown definition, missing element). 

## 5. Commands + invariants

- Place tower: tile in buildable area, empty, resources sufficient, requirements unlocked. Debit exactly once; spawn view only after domain success.
- Upgrade: correct tower ID, level below max, resources sufficient. Preserve total money invested for resale.
- Sell: remove tower and grant rounded down refund. Can't sell during invalid stage if design prohibits; rule defined in one place.
- CastSkill: cooldown complete, valid map coordinate and phase; cooldown begins only on success.
- StartWave: only during preparation; repeated taps idempotent.
- Pause/background: stop simulation ticks and input; save at safe point.

## 6. Config + balancing workflow

Source-of-truth: designer-accessible ScriptableObjects or checked-in JSON converted/validated into them. One ID per tower/enemy/wave; cross-reference validator, negative-stat validator, cycle detector for upgrade graph, localization-key validator. Auto-generate a balance CSV table from configs for review; formulas documented, not duplicated in UI logic.

## 7. Mobile optimization budgets (initial targets, must profile)

- 60 fps target: average 16.7ms/frame; prioritize p95/frame-time and thermal stability. 30 fps fallback quality level.
- Battle stress target: approximately 100 simultaneous enemies, 30 towers, visually pooled VFX/projectiles; parameters subject to real-device measurement.
- No per-frame heap allocations in enemy/tower loops; reuse arrays/lists where reasonable, object pooling for units/projectiles/VFX.
- Limit particle overdraw, transparent layers, shadow maps, texture resolution and dynamic lights.
- Use ASTC textures for iOS, compressed audio; monitor resident memory, not only total asset sizes.
- Every optimization should be justified by profiler evidence; do not introduce ECS/DOTS before an actual scale bottleneck.

## 8. iOS specifics

- True landscape layout on different aspect ratios and Dynamic Island/safe areas; adapt for iPad multitasking if enabled or explicitly constrain supported presentation modes.
- Handle app background/resume and audio interruptions gracefully.
- Validate touch target sizes and gestures on physical iPhone; editor mouse tests are insufficient.
- Release builds use IL2CPP/ARM64 and current Apple signing requirements; privacy manifest/required reason APIs depend on *actual* SDK usage.
- Native service bridges only when necessary (Game Center, IAP). Keep platform code behind interfaces.

## 9. Tests / gates

**EditMode:** deterministic seeded run; spawn count; order of targeting; cooldown; one-time payout; unlock requirements; invalid purchases; no negative gold; status expiry; each six reactions; reaction cap; boss leak damage; serialization round-trip and migration.

**PlayMode:** map prefab loads; camera framing; touch tap/build; UI reflects economy; save/resume from mid-battle checkpoint; no missing ScriptableObject or Addressable references; audio pause.

**Device:** app launch, memory/thermal, 3 consecutive full matches, airplane mode, interruptions, safe area, landscape on every supported form factor, 30/60 fps measurements, install/restore purchase in sandbox when added.

## 10. Agent engineering rules

Commit `.meta` files, use Force Text and Visible Meta Files. Prefer prefab variants to 10 near-identical prefabs. Avoid changing serialized Unity assets blindly through string substitutions. Do not silently import third-party assets or packages. Never commit provisioning profiles, API keys, personal Apple credentials or proprietary models. A green CLI test is not proof that the scene looks correct on a device.
