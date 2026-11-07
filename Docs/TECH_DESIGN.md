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

После исчерпания одной legacy тестовой волны и последнего non-lethal leak фаза становится `WaveComplete`; lethal leak устанавливает `Defeat`. Начиная с P1.4a отдельный восьмиволновой scenario использует `Victory` после успешной финальной волны; legacy single-wave сохраняет прежний контракт без Victory.

`BattleSceneBuilder` находится в Editor-only `Veilward.Editor.asmdef` и не компилируется в iOS player. Единый source of truth маршрута — `BattleScenarioConfig.CreateSimulation().Path`: builder создаёт дорожные сегменты, Spawn Marker и Seal Heart по Domain waypoints, без дублирующего массива координат. При повторной генерации существующий `BattleScenarioConfig` не сериализуется заново: сохранённые `towerRange`, `towerFireRate` и `towerDamage` остаются источником значений для сцены и Domain. Боевые значения по умолчанию назначаются только веткой создания отсутствующего asset.

### P1.2 implemented contract

`GridMapDefinition` задаёт карту 12×8 с origin `(-6,-4)` и cell size 1 в текущем scenario; поддерживаются любые конечные положительные значения `CellSize`. `BattleGrid` переводит world-space waypoints в координаты клеток делением на `CellSize`, затем применяет тот же DDA/supercover rasterizer для промежуточных и diagonal cells. Визуальная дорога использует исходные world-space waypoints и ширину `0.92 × CellSize`, совпадая с заблокированными клетками и не перекрывая соседние buildable cells. Tower view, preview и selection highlight масштабируются по `CellSize`; regression tests при `CellSize = 0.5` проверяют реальные `Renderer.bounds` дороги и соседних башен. Маршрут при строительстве не пересчитывается и не меняется.

`BattleSimulation.PlaceTower()` атомарно проверяет фазу `Preparation`, известный `TowerDefinitionId`, границы и состояние клетки и достаточность золота. При успехе он один раз списывает цену, занимает клетку и добавляет `TowerState` с уникальным последовательным положительным `TowerId`; при ошибке состояние не меняется. Повторная команда по занятой клетке не списывает золото. `BattleGrid.Map` и `GetState()` предоставляют публичный read-only доступ, а `TryOccupy()` доступен только внутри Domain. `TowerState` отклоняет `default(TowerId)` и любой неположительный ID. Текущий `TestTower` стоит 60 gold при стартовых 150 gold; продажа и улучшения не входят в контракт.

`BattlePlacementController` только преобразует Input System mouse/touch в выбор клетки и вызывает Presentation/Domain API. Первый tap показывает selection и preview без покупки, Confirm применяет команду, Cancel очищает selection. UI raycasts не передаются на поле. Start Wave доступен вручную только в `Preparation`; при входе в Combat selection отменяется и строительство запрещено. `BattleSceneController` создаёт tower view только после успеха Domain и показывает Gold/Towers в HUD.

### P1.3 implemented contract

Боевой `Step()` выполняет шесть стадий в фиксированном порядке: **Advance Tick → Spawn → Movement/Leaks → Tower Attacks → Death/Removal → Defeat/WaveComplete**. `StartWave()` обрабатывает только spawn tick 0 и не производит атак до первого `Step()`. Terminal phase замораживает tick и боевое состояние.

`TowerDefinition.Damage` — целое положительное число. `AttackIntervalTicks = max(1, round(30 / FireRate))` с `MidpointRounding.AwayFromZero` и верхним ограничением `int.MaxValue`. `NextAttackTick` изначально равен 0; после атаки становится `tick + AttackIntervalTicks`. Отсутствие цели не запускает cooldown. Башни атакуют в порядке `TowerId`, не более одной атаки на башню за тик.

`EnemyTargeting.SelectFirst()` выбирает живого врага в пределах `Range` с максимальным `DistanceAlongPath`; равный прогресс разрешается минимальным `EnemyId`. Выбор не создаёт коллекций. Урон ограничивает HP нулём, смерть и увеличение `BattleState.Kills` происходят один раз. Все башни завершают атаки текущего тика до удаления погибших; убитый враг не наносит leak damage. `AttackEvent` содержит Tower/Enemy IDs, `PathPosition TargetPosition`, tick, configured damage (включая overkill) и lethal flag. Domain вычисляет `TargetPosition` из прогресса цели непосредственно перед `ApplyDamage`, поэтому событие сохраняет точку попадания после удаления врага.

`BattleSimulation.AttackEvents` — reused read-only list. Presentation обрабатывает события после каждого fixed step и сразу вызывает `ClearAttackEvents()` в `finally`; автоматической очистки между тиками нет. `BattleSceneController` создаёт короткоживущий world-space beam строго по `AttackEvent.TargetPosition`, не используя текущий список врагов или позицию Enemy View предыдущего кадра. Поэтому lethal first-shot и каждый из нескольких fixed steps перед render сохраняют собственную точку попадания. Health bars обновляются из Domain HP, Kills показываются в HUD. `BattlePlacementController` показывает XZ-круг configured range при selection и скрывает его при Confirm/Cancel/Start Wave.

Текущий `TestWave.asset` использует Range **3**, FireRate **2**, Damage **25**, сохраняя маршрут, пять Shade, spawn schedule, цену 60 и стартовые 150 gold. Пулинг beams, armor/resistance, status effects и дополнительные targeting modes остаются за пределами текущего scope. Награды за kills и завершение волн добавлены отдельным контрактом P1.4a.

### P1.4a implemented contract

`BattleScenarioConfig` сохраняет legacy serialized поля. Пустой `waves` создаёт один `WaveDefinition` с bonus 0 и финальной фазой WaveComplete. Непустой `waves` создаёт последовательность, а `sharedScenario` предоставляет общие map/path/enemy/tower/base/gold параметры. `EightWave.asset` ссылается на `TestWave.asset`, содержит counts **5…12**, `enemyReward = 5` и `waveCompletionBonus = 20`; повторный Build не перезаписывает существующие значения.

`StartWave()` возвращает `WaveTransitionResult`, успешен только из Preparation и задаёт `_waveStartTick = State.Tick`. Абсолютный spawn tick вычисляется от начала текущей волны с long-арифметикой. `ContinueToNextWave()` успешен только из WaveComplete при наличии следующей волны, увеличивает zero-based `WaveIndex` ровно один раз и возвращает Preparation. Следующая волна требует отдельного `StartWave()`; автозапуска нет. Terminal Victory/Defeat и rejected commands не меняют состояние.

Между волнами сохраняются глобальный Tick, `_nextEnemyId`, `_nextTowerId`, Towers и `NextAttackTick`, Gold, BaseHealth и Kills. Kill начисляет `EnemyDefinition.Reward` один раз вместе с удалением и `Kills += 1`; leak не платит. Успешное завершение каждой волны, включая финальную Victory, начисляет configured bonus один раз. Defeat имеет приоритет и не платит bonus. `Credit` использует насыщение на `int.MaxValue`; покупка остаётся атомарной и не допускает отрицательный Gold.

Боевой порядок расширен до **Advance Tick → Spawn → Movement/Leaks → Tower Attacks → Death/Removal + kill rewards → Defeat/WaveComplete/Victory + wave bonus**. Presentation вызывает Domain commands для Start/Next Wave, отражает `WaveIndex + 1 / WaveCount`, Phase и terminal Result, но не реализует экономику или переходы самостоятельно. Полный контракт и transition table находятся в `Docs/P1.4A_WAVE_LIFECYCLE.md`, regression scenarios — REG-BATTLE-009…024.

### P0.3 quality gates contract

`Scripts/check_unity_project.py` — независимый stdlib gate для Unity version/revision, direct package baseline/lock, paired/orphan `.meta`, 32-hex GUID, duplicate GUID и Assembly Definitions. `Veilward.Domain` сохраняет `noEngineReferences: true`; Editor и EditMode test assemblies остаются Editor-only, PlayMode test assembly может компилироваться для player test targets.

`Scripts/verify_unity_test_results.py` принимает NUnit/JUnit XML только при non-empty полном PASS: `Passed == Total`, нулевые failed/errors/inconclusive/skipped. Static checks обязательны на каждом PR/push в `main`. Отдельный `Unity Personal Tests` workflow запускает EditMode/PlayMode только через manual dispatch при `github.ref == refs/heads/main` на изолированном macOS self-hosted runner с Hub-activated Unity Personal. Checkout использует `github.sha` и проверяет точное совпадение HEAD. Явный `batch` backend создаёт NUnit XML для обоих режимов; локальный default `auto` сохраняет Unity CLI JSON path. Batch не запускает второй Editor. License/account secrets, serial и ALF/ULF не используются. До регистрации runner и полного manual PASS Unity CI явно остаётся BLOCKED; automatic PR/push runs на self-hosted runner запрещены.

`BattleAssetValidator.ValidateSavedBattle()` — Editor-only read-only проверка сохранённой Battle scene, config и обязательных serialized references, exact prefab paths, missing scripts, materials и shaders. Validator не сохраняет scene и закрывает её только при additive open самим validator.

Reproducibility test резервирует scene/config/prefabs/materials и их `.meta` в памяти, выполняет два Build и восстанавливает originals в `finally`. Scene сравнивается как canonical YAML: Unity назначает новые local fileIDs и может менять порядок YAML documents при recreate, поэтому IDs получают content-based canonical tokens, documents сортируются, а внешние GUID references и serialized content сохраняются в сравнении. Для остальных YAML trailing whitespace пустых значений игнорируется как serialization noise; весь остальной text сравнивается. Восстановление originals всегда проверяется побайтово. После suite `git diff` подтверждает отсутствие неожиданных tracked changes.

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
