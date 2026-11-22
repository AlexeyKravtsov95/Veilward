# VEILWARD — Technical Design / Architecture v0.1

ART.3d/#82: STONE «Осколочная печать» L1–L3 и единый FLAME/TIDE/GALE/STONE binding. [Спецификация/regressions](ART3D_STONE_LEVELS.md), [актуальный Editor/source proof и read-only verifier](review/ART.3d/README.md). Добавляется optional stoneLevels, прежние serialized fields/GUID и один fallback owner сохраняются. Domain/configs/schema4 неизменны. Device Deferred to prerelease QA #53/#54/#55; Parent44 и STEAM/STORM отдельно.

ART.3b/#78: production `FlameModelBinding` сохраняет GUID/class и прежние serialized FLAME `levels`/`fallbackRenderers`, добавляет `tideLevels`. Это один владелец model/fallback, keyed по definitionId+level; другие типы/неизвестные уровни возвращаются к graybox. Старый model child отключается до deferred Destroy, palette renderers не tint-ятся. Отдельный TIDE builder не пересоздаёт FLAME assets. ShotSource остаётся presentation anchor без изменения Domain coordinates/generic beam. [Контракт и регрессии](ART3B_TIDE_LEVELS.md). Device Deferred to prerelease QA #53/#54/#55 по решению владельца20.11.2026; Editor proof не является iPhone PASS.

R2 ART.3b устраняет self-occlusion production Shade HP-полосы: её существующий local anchor поднимается над проекцией головы в Battle camera, между прежними неперекрывающимися status slots. Root/Domain positions и здоровье не меняются. Сохраняются ratio, Face/Layout/leader semantics, shared materials и число passes. Проверяются projected bounds и реальные повторные camera probes, включая comparison с FLAME; это минимальная presentation correction для ART3B-05.

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
- В текущем EightWave RNG отсутствует: fixed spawn schedule, First targeting и стабильные IDs. `IRandomSource` и сохранение RNG state — будущий контракт, если появится случайность.
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

`BattleSimulation.PlaceTower()` атомарно проверяет фазу `Preparation`, известный `TowerDefinitionId`, границы и состояние клетки и достаточность золота. При успехе он один раз списывает цену, занимает клетку и добавляет `TowerState` с уникальным последовательным положительным `TowerId`; при ошибке состояние не меняется. Повторная команда по занятой клетке не списывает золото. `BattleGrid.Map` и `GetState()` предоставляют публичный read-only доступ, а `TryOccupy()` доступен только внутри Domain. `TowerState` отклоняет `default(TowerId)` и любой неположительный ID. Текущий `TestTower` стоит 60 gold при стартовых 150 gold; продажа и улучшения добавлены отдельным контрактом P1.4b.

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

### P1.4b implemented contract

`BattleScenarioConfig` сохраняет legacy serialized поля Level 1 и добавляет `towerUpgrades` для Level 2/3. `BattleSimulation.TowerLevels` содержит упорядоченные immutable `TowerDefinition`; прежний `TowerDefinition` обозначает Level 1. Текущие уровни имеют Damage **25/35/50**, Range **3**, FireRate **2**, цену **60/50/80** и `InvestedGold` **60/110/190**. `TowerState.Level` одно-based, а `Definition` и `InvestedGold` меняются только после успешной атомарной операции.

`UpgradeTower(TowerId)` и `SellTower(TowerId)` возвращают `TowerOperationResult` с явным `TowerOperationFailureReason` и допустимы только в Preparation. Upgrade проверяет TowerId, max level, affordability и переполнение инвестиций, затем один раз списывает Gold и меняет уровень; TowerId, координата и `NextAttackTick` сохраняются. Sell заранее вычисляет `(int)((long)InvestedGold * 70 / 100)`, удаляет башню, освобождает только её Occupied cell через internal `BattleGrid.TryRelease` и начисляет refund через saturating Credit. Повторная команда для удалённого ID возвращает TowerNotFound; `_nextTowerId` не уменьшается и ID не переиспользуются.

Occupied cell selection отображает Level, Damage, Range, FireRate, следующую стоимость Upgrade и текущий refund. В Combat панель доступна только для чтения. Sell требует отдельного confirmation, привязанного к выбранному TowerId; прямой ConfirmSell без Request или после смены selection возвращает ConfirmationRequired без Domain mutation. Пока confirmation активно, Upgrade, повторный Sell, placement Confirm/Cancel и Start/Next Wave заблокированы как в controller API, так и в кнопках. Cancel и смена selection очищают pending TowerId без изменения состояния; после Cancel действия восстанавливаются. После успешного подтверждения Presentation удаляет соответствующий Tower View и очищает selection/confirmation, поэтому проданная башня не участвует в атаках и не оставляет stale view. UI не получает mutation API для Grid, Gold или TowerState.

`BattleSceneBuilder` создаёт Tower Panel, Upgrade/Sell и confirmation controls; `BattleAssetValidator` проверяет их serialized references и три уровня конфигурации. Полный контракт находится в `Docs/P1.4B_TOWER_ECONOMY.md`, regression scenarios — REG-BATTLE-025…050.

### P1.5a implemented pause/background contract

`BattlePauseController` — единственный владелец latched `_isPaused`, `_applicationPaused` и `_hasFocus`. Ручной Pause, `OnApplicationPause(true)` и потеря focus устанавливают паузу; возврат из background или focus не снимает её автоматически. `Resume()` успешен только при активном application и наличии focus. Victory/Defeat остаются terminal; disk checkpoint и process recovery в P1.5a не входят.

`BattleSceneController.IsPaused` делегирует этому controller. Place/Upgrade/Sell и Start/Next Wave при паузе возвращают существующие `InvalidPhase`/`RejectedInvalidPhase` без Domain mutation. Fixed-step accumulator при паузе обнуляется, а первый Update после Resume пропускает delta, поэтому background time не создаёт catch-up ticks и не меняет детерминированный порядок последующих атак и наград. Глобальный `Time.timeScale` не изменяется.

Pause очищает selection и Sell confirmation без экономических изменений. `BattlePlacementController` блокирует gameplay APIs/buttons, а Resume подавляет world pointer на текущий кадр, чтобы нажатие не прошло сквозь modal overlay. Полноэкранный uGUI `Pause Overlay` имеет `raycastTarget = true`; lifetime `AttackBeamView` также не advance при паузе. Saved Battle scene содержит один полностью wired `BattlePauseController`, а builder/validator проверяет references и воспроизводимость. Контракт — `Docs/P1.5A_PAUSE_BACKGROUND.md`, regression scenarios — REG-BATTLE-054…066.

### P1.5b implemented wave checkpoint contract

`BattleSimulation.ExportCheckpoint()` допускает только Preparation, WaveComplete, Victory и Defeat. Snapshot содержит phase/wave/tick, HP/Gold/Kills, next Enemy/Tower IDs и towers с ID/cell/Level/InvestedGold/NextAttackTick; enemies, spawn index, RNG, AttackEvents и UI transients не сериализуются. Прерванный Combat восстанавливает последнюю Preparation перед Start Wave. `RestoreCheckpoint()` сначала полностью проверяет snapshot и только затем атомарно публикует State/Grid, сбрасывает `_nextSpawnIndex` и очищает AttackEvents; rejected restore не меняет текущий матч.

На этапе P1.5b `WaveCheckpointStore` записывал JSON envelope `schemaVersion = 1` и `scenarioContentId` в `wave-checkpoint-v1.json`; актуальная schema 2 описана ниже. Temp перечитывается и валидируется, primary заменяется через `File.Replace`, а `.bak` сохраняет только предыдущий валидный primary. Load сначала проверяет primary, затем backup и отклоняет unknown schema/content, corrupt/truncated и невалидные snapshots, включая исчерпанные `nextEnemyId`/`nextTowerId`. Сохранения выполняются после successful Build/Upgrade/Sell/Next Wave и переходов WaveComplete/terminal; failed save блокирует Start Wave, но не откатывает уже успешную live economy command.

New Game сначала удаляет stale temp и копирует primary (либо backup при отсутствии primary) в temp как recovery copy до удаления слотов. При failed reset сохранившийся primary/backup используется для Continue; если оба слота уже удалены, recovery temp перемещается обратно в primary. Это покрывает ошибку каталога по BackupPath после удаления primary. При недостатке оставшихся безопасных Enemy IDs Start Wave возвращает `RejectedIdExhausted` до смены фазы; исчерпанный Tower counter возвращает `IdentifierExhausted` без Gold/Grid mutation. Последний допустимый next ID в checkpoint — `int.MaxValue - 1`.

`BattleSessionContext` передаёт MainMenu session/store в Battle. New Game и Continue выполняются только явным click; Continue доступен для Preparation/WaveComplete и не вызывает Step или повторные rewards. Прямой запуск Battle без session context использует in-memory store и не обращается к реальному `persistentDataPath`. Saved MainMenu/Battle и `BattleSceneBuilder` содержат полностью wired controls; воспроизводимость canonicalized для обеих сцен. Контракт — `Docs/P1.5B_WAVE_CHECKPOINT.md`, regression scenarios — REG-BATTLE-067…089; реальные Play Mode кадры и sanitized JSON — `Docs/review/P1.5b/`.

### P2.1b implemented definitions, catalog and checkpoint schema 2 contract

`StatusEffectDefinition` — immutable validated data (`Kind`, `Power`, `DurationTicks`, `DamageIntervalTicks`); Wet Power ограничен **1…100**, Burn требует положительный damage interval, остальные статусы — нулевой. `TowerDefinition.StatusEffect/ArmorPenetration` и `EnemyDefinition.Armor` доступны Domain без Unity-зависимостей. `TowerCatalog` хранит уникальные `TowerArchetypeDefinition` с тремя уровнями; saved `TestWave.asset` содержит точные FLAME/TIDE/GALE/STONE определения и прежний скрытый `test-tower` без статуса. Builder заполняет только отсутствующий каталог и при повторном Build сохраняет существующие настройки; Domain и `BattleAssetValidator` отклоняют невалидные saved values без молчаливой нормализации.

`PlaceTower` выбирает Level 1 по `PlaceTowerCommand.DefinitionId`, Upgrade использует следующий уровень того же `tower.Definition.Id`, а Sell рассчитывает refund от InvestedGold выбранного типа. Restore ищет определение по паре `towerTypeId + level` и проверяет InvestedGold по ценам именно этого архетипа. Missing/empty/unknown type и любые rejected commands не публикуют частичное State/Grid изменение. Прежние constructors и `TowerLevels` сохранены как compatibility API для существующего Presentation и fixtures.

`WaveCheckpointStore.CurrentSchemaVersion = 2`; каждая tower entry содержит обязательный `towerTypeId`. Physical slot name остаётся `wave-checkpoint-v1.json` вместе с `.bak/.tmp`. Совместимый schema 1 envelope мигрируется через deep copy, каждая башня явно получает `test-tower`, затем результат проходит current Domain validation. Load не перезаписывает source JSON; primary и backup проверяются независимо, а следующий обычный Save атомарно записывает schema 2.

P2.1b не создаёт active `StatusInstance`, не применяет Wet/Gust/Crack/Burn, не запускает timers/DoT и не использует Armor/ArmorPenetration в damage pipeline; новый Build UI относится к следующему PR. Контракт — `Docs/P2.1A_ELEMENTAL_TOWERS.md`, regression scenarios — REG-BATTLE-090…099.

### P2.1c–e: реализованные статусы, каталог и интеграционная проверка

P2.1c добавил active `StatusInstance` в `EnemyState.Statuses`: один экземпляр Kind, Power/RemainingTicks/NextDamageTick/DamageIntervalTicks/SourceTowerId. Актуальный порядок тика заменяет исторический порядок P1.3: **Advance Tick → Spawn → Status Damage/Expiry → Movement/Leaks → Tower Attacks → Death/Removal/rewards → Defeat/WaveComplete/Victory**. Due Burn выполняется до decrement/expiry, включая последний активный тик; погибшая цель не двигается и не атакуется. Reapply продлевает duration через max и сохраняет due tick; более сильный или равный Power обновляет параметры/источник. Pause не выполняет тики.

Прямой урон: Crack → ArmorPenetration → min 1 damage → Gust (floor, saturating int) → HP → статус только выжившей цели. Burn учитывает Crack/armor, но не penetration/Gust; Wet влияет на последующее Movement и удаляется до движения в тик истечения. Все смерти и награды проходят единый `RemoveDefeatedEnemies`. `AttackEvent.Damage` остаётся номинальным Damage definition; событие не является журналом эффективного damage и не публикует DoT. Контракт событий для статистики не менялся.

P2.1d предоставляет FLAME/TIDE/GALE/STONE selection до Confirm, числовые stats/range preview, typed placement, per-type Upgrade/MAX/Sell и read-only Combat. Legacy Test Tower скрыта. Новые панели учитывают `Screen.safeArea`/Canvas scale; Pause/Sell modal блокируют сквозной ввод. Реальные Editor Play Mode кадры: [каталог](review/P2.1d/01-catalog.png), [preview](review/P2.1d/02-preview.png), [бой](review/P2.1d/03-status-combat.png), [Upgrade](review/P2.1d/04-upgraded-panel.png), [Sell](review/P2.1d/05-sell-confirmation.png), [Continue](review/P2.1d/06-continued-tower.png); [QA log](review/P2.1d/qa.log).

Schema 2 сохраняет тип/уровень/вложения/cooldown/next IDs, но не enemies/statuses. Прерванный Combat откатывается к сохранённой Preparation вместе с HP/Gold/Kills попытки. WaveComplete уже содержит reward; Load и Next Wave повторно его не начисляют. Terminal checkpoint сохраняет итог и башни, но не оставшихся при Defeat врагов; сравнение live terminal enemies с восстановленным terminal не является контрактом Continue.

P2.1e merged в PR #17: `BattleElementalReplayTests` использует сохранённый EightWave/shared TestWave, реальные 150 Gold/20 HP и public API. Повторные полные timelines, статусы, public AttackEvents, выводимые из due Burn смерти и дальнейший replay после файлового Continue совпадают. Зафиксирован Defeat на волне 8, tick 3997, HP 0/Gold 112/Kills 46, 8 DoT kills; баланс не изменялся. [Измерения и ограничения](review/P2.1e/balance-review.md), [проверяемый JSON](review/P2.1e/replay-summary.json); REG-BATTLE-125…127. Это Editor regression evidence, не physical iPhone/performance/balance approval.

### P2.2c: additive runtime и damage telemetry

`BattleSimulation` принимает optional `ReactionCatalog` в catalog constructors (null — совместимый Empty). Все уровни с intrinsic payload требуют соответствующую global definition, включая compatibility constructors. До P2.2d production config передавал Empty; отдельный internal `BattleScenarioConfig.CreateSimulation(ReactionCatalog)` доступен через friend assembly только EditMode tests. Только explicit Empty test path выбирает неизменённые basic+legacy objects из validated каталога; все пути historical P2.1e replay используют его.

Primary attack: direct HP damage → `DamageResolvedEvent` → обычный status только выжившей цели → синхронный `ReactionResolver` → tower cooldown/прежний `AttackEvent`. Lethal direct не расходует reaction window. Incoming Burn допускает Steam, Wet — Steam, затем Storm fallback, Gust — Storm; intrinsic допускает только свой вид. STONE, DoT и secondary не входят в resolver. Реакция не потребляет/обновляет статусы.

Resolver переиспользует буфер максимум четырёх целей/позиций. Steam: primary + nearest три по Euclidean distance²/EnemyId, inclusive 1.25; Storm: максимум два дополнительных greedy hop по inclusive 2.0 от предыдущего узла, вся цепь планируется до damage. Nullable read-only timestamps per enemy/kind резервируются всем участникам до damage; разница ticks проверяет окно 60 без сложения. Secondary: long scale/floor и saturation, armor с Crack, min 1, HP clamp; Gust/penetration/statuses/recursion отсутствуют. Death/removal/rewards/bonus остаются едиными.

Reused read-only `ReactionEvents`/`DamageEvents` хранят captured positions, configured/raw/resolved/applied damage и общий zero-based sequence на Step; reaction hits коррелируют по sequence инициирующего event. Burn публикуется до Movement/expiry, со старым SourceTowerId даже без башни. Явные `ClearReactionEvents()`/`ClearDamageEvents()` не меняют State; нет auto-clear между Step. На этапе P2.2c SceneController отбрасывал новую telemetry без отображения; P2.2e потребляет её для VFX/inspector и очищает все потоки в одном `finally` после каждого Step. Успешный Restore очищает потоки/sequence вместе со сменой State; rejected Restore ничего не меняет. Формат checkpoint и старый AttackEvent не изменены. [REG-132…145/evidence](review/P2.2c/README.md).

### P2.2d: production data, гибриды и schema 2

Публичный `BattleScenarioConfig.CreateSimulation()` всегда создаёт непустой validated production `ReactionCatalog` из shared settings. Steam: 60 ticks / 100% / radius 1.25 / всего 4 / priority 0; Storm: 60 ticks / 75% / hop 2 / две дополнительные / priority 1. Пустые/неполные global definitions, invalid payload и несогласованные kind/priority отклоняются, fallback на Empty отсутствует. Internal override сначала валидирует production data; только явный Empty test path выбирает прежние basic+legacy objects.

Adapter требует шесть видимых IDs в порядке FLAME/TIDE/GALE/STONE/STEAM/STORM, matching basic status и matching hybrid intrinsic каждого из трёх уровней. `hasIntrinsicReaction` задаёт наличие serialized payload. Unity inline class serialization превращает null `statusEffect` в нулевые поля: только полностью пустое представление допускается у гибрида и преобразуется в Domain null. Любой непустой status, penetration или несовпадающий payload отвергается. Гибриды используют таблицы spec §6 без изменения resolver/formulas; Upgrade сохраняет identity/cell/NextAttackTick. Base/legacy stats прежние.

Builder дополняет только валидный полный прежний каталог из четырёх типов: clone получает два новых definition, проходит полную validation до публикации; имя/данные исходного asset сохраняются. Корректный набор шести типов не перезаписывается. EightWave использует только shared TestWave: допустима миграция полных неизменённых initializer copies; частичные/изменённые локальные массивы отвергаются до записи. Saved validator проверяет ссылку и отсутствие локальных дублей. Новые настройки не нормализуются OnValidate в defaults.

Все шесть типов доступны в Preparation по утверждённому P-05; unlock gating — P2.3. Place/Upgrade/Sell и schema 2/прежние content IDs не менялись. Новые башни сохраняют только существующие tower fields; enemies/statuses/reaction timestamps/events отсутствуют в save. Неизвестный primary type не блокирует независимый valid backup, Load не меняет bytes. Специальная читаемость реализована P2.2e поверх прежнего generic UI. [REG-146…153 / evidence](review/P2.2d/README.md).

### P0.3 quality gates contract

`Scripts/check_unity_project.py` — независимый stdlib gate для Unity version/revision, direct package baseline/lock, paired/orphan `.meta`, 32-hex GUID, duplicate GUID и Assembly Definitions. `Veilward.Domain` сохраняет `noEngineReferences: true`; Editor и EditMode test assemblies остаются Editor-only, PlayMode test assembly может компилироваться для player test targets.

`Scripts/verify_unity_test_results.py` принимает NUnit/JUnit XML только при non-empty полном PASS: `Passed == Total`, нулевые failed/errors/inconclusive/skipped. Unity CLI JSON envelope принимается только со строгим boolean `success: true`; любое другое значение или тип отклоняются и при финальной проверке, и при PlayMode polling. Без envelope raw asynchronous response может иметь `status: running` только для продолжения polling либо `status: completed` с корректным non-empty summary для результата. Во время Play Mode domain reload временный `EDITOR_NOT_RUNNING` повторно опрашивается до общего deadline; остальные CLI errors прекращают run. Static checks обязательны на каждом PR/push в `main`. Отдельный `Unity Personal Tests` workflow запускает EditMode/PlayMode только через manual dispatch при `github.ref == refs/heads/main` на изолированном macOS self-hosted runner с Hub-activated Unity Personal. Checkout использует `github.sha` и проверяет точное совпадение HEAD. Явный `batch` backend создаёт NUnit XML для обоих режимов; локальный default `auto` сохраняет Unity CLI JSON path. Batch test invocation не использует `-quit`, поскольку Unity 6000.3.28f1 с этим сочетанием завершал Editor до фактического Test Runner run. Batch не запускает второй Editor. License/account secrets, serial и ALF/ULF не используются. До регистрации runner и полного manual PASS Unity CI явно остаётся BLOCKED; automatic PR/push runs на self-hosted runner запрещены.

`BattleAssetValidator.ValidateSavedBattle()` — Editor-only read-only проверка сохранённой Battle scene, config и обязательных serialized references, exact prefab paths, missing scripts, materials и shaders. Validator не сохраняет scene и закрывает её только при additive open самим validator. Tower Stats measurement использует временный экземпляр Text в отдельной preview scene и `try/finally`; исходный `stats.text` и dirty state Battle scene не изменяются даже при failed assertion.

Reproducibility test резервирует scene/config/prefabs/materials и их `.meta` в памяти, выполняет два Build и восстанавливает originals в `finally`. Scene сравнивается как canonical YAML: Unity назначает новые local fileIDs и может менять порядок YAML documents при recreate, поэтому IDs получают content-based canonical tokens, documents сортируются, а внешние GUID references и serialized content сохраняются в сравнении. Для остальных YAML trailing whitespace пустых значений игнорируется как serialization noise; весь остальной text сравнивается. Восстановление originals всегда проверяется побайтово. После suite `git diff` подтверждает отсутствие неожиданных tracked changes.

### P2.4c: schema4 и полный safe-state restore

`BattleCheckpoint.ability` — immutable Domain `AbilityCheckpoint` (rules/id/remaining/next sequence); `DeepCopy` создаёт независимую копию. Domain не зависит от Unity. Поскольку JsonUtility игнорирует readonly/get-only values, storage использует явный `WaveCheckpointWire` DTO, а ability помечен NonSerialized в исторической JsonUtility-проекции Domain checkpoint. Эта историческая проекция сохраняет trace/outcome goldens; actual disk schema4 всегда содержит обязательную ability. Для записи checkpoint используется `WaveCheckpointStore`, не прямой JsonUtility.ToJson Domain snapshot.

Строгий parser проверяет presence/types/integer ranges/duplicate/unknown fields до JsonUtility. Schema1/2 legacy проходят source shape validation до добавления legacy-all-open/disabled defaults; schema3 native сохраняет progression/content и получает disabled ability. Normalize идемпотентен, Load read-only; Save пишет4 с тем же ID/именем файла. Пустая source progression не превращается в валидный legacy state. В validator test path разрешены только exact test IDs; synthetic-p24-ability-v1 требует enabled ability и native progression. Production whitelist/session/menu не принимают synthetic/final ID до #39.

Каждый primary/backup независимо normalize→factory→Domain validation; Save не заменяет backup повреждённым primary. Restore сначала строит весь candidate State/Grid/ability, затем публикует его и очищает четыре event streams/cursors; rejection оставляет даже dirty Combat buffers нетронутыми. Controller замечает safe completion от синхронной команды и в LateUpdate кадра без fixed Step. Только whole safe snapshot; Combat Export запрещён. Failed completion/Choose/second Next save сохраняет live success, Retry только пишет snapshot. Start/Next проверяют eligibility до IO и требуют durable текущего safe state.

Контракт REG198–203 и [evidence P2.4c](review/P2.4c/validation.md). Production остаётся ability-disabled; enabled persistence synthetic, UI/boss/new final mission — следующие задачи.

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

### P2.2e: transient reaction presentation

`ReactionPresentationState` связывает только Reaction-origin hits по tick/reaction sequence/kind/source. HUD и инспектор используют фактический AppliedHpLoss и hit count; configured/direct Damage не является суммой реакции. Память на живого врага хранит последние объяснения обоих видов; общий HUD сохраняет lethal результат. Domain, damage, definitions, цены и balance configs не меняются.

`BattleSceneController` потребляет все события после каждого fixed Step, включая несколько Step до render, и очищает все три stream в одном finally. Ограниченный pool из 24 `ReactionEffectView` переиспользует oldest slot; Steam ring/impacts и Storm hop order используют исключительно captured geometry. Отдельный unlit material/PropertyBlock задаёт цвета; callbacks не вызывают симуляцию.

Enemy prefab имеет ровно два слота: cause cue на 0.5 s active presentation time с Steam tie priority, затем status priority Burn/Wet/Gust/Crack с учётом ready-pair cooldown. Intrinsic подписан отдельно. Локальный layout разводит существующие подписи плотной группы, не добавляя badges. `EnemyInspectorController` выбирает живой EnemyId и читает все statuses/power/ticks, оба remaining CD и оба последних объяснения. Смерть закрывает selection.

Pause/background замораживают clock/VFX и обнуляют accumulator/первый delta при Resume. Placement сначала проверяет UI/modals, затем enemy hit area; inspector поглощает world taps до явного закрытия. Успешный controller Restore очищает pool/cues/selection и пересоздаёт enemy/tower views; rejected Restore не вызывает reset. Continue/New Game создают новую сцену. Schema 2 неизменна.

Preview и occupied panel показывают intrinsic percent/range/limit/global cooldown и условия (surviving primary, Storm neighbor); FLAME/TIDE/GALE объясняют пары. Builder создаёт refs/два slots/material; validator проверяет новые refs, missing scripts и inspector layout. [Проверки, реальные Editor Play Mode кадры и ограничения](review/P2.2e/README.md). Physical iPhone, performance, баланс и G2 не подтверждены; полный replay/impact — P2.2f.

### P2.2f: test-only replay и impact

`BattleReactionReplayTests` не меняет runtime: четыре серии historical/matched/steam/storm используют saved EightWave/shared TestWave и публичные команды. Только historical вызывает internal explicit Empty. Полные timelines содержат tower state/IDs/cooldown, enemy HP/distance/status/timestamps, Grid, checkpoints в safe phases, все поля Attack/Reaction/Damage events и rejected/accepted receipts. Golden comparison исключает только host OS metadata; actual report сохраняет реальную ОС, отдельный regression принимает OS-only отличие и отвергает HP drift. SHA256 несжатого timeline проверяет повторяемость; compressed traces сохраняются как evidence. Межверсионная/межплатформенная bit identity не заявлена.

Observer на каждом Step сохраняет независимые shadow values до мутации, учитывает spawn, due Burn/expiry и movement/leaks, затем читает direct damage в sequence order и применённый incoming status. Кандидаты Steam → Storm, surviving primary, intrinsic-only, ready primary window, nearest eligible alive/unvisited neighbors и reservation всех участников сверяются с captured events. Cooldown suppression — один отклонённый кандидат при закрытом primary окне; no-neighbor — один готовый Storm-кандидат без допустимого первого hop (в том числе если соседи закрыты cooldown). Отсутствие complementary status, lethal direct, cooldown secondary exclusion и неиспытанный после успешного Steam кандидат не считаются отдельными suppressions. Новый production combat log не нужен.

AppliedHpLoss + HP ушедших в leak + HP живых на terminal = spawned × MaxHealth; каждому lethal ID соответствует полная потеря MaxHealth, один Kills и одна kill reward. Overkill = ResolvedDamage − AppliedHpLoss, по Direct/Burn/Reaction отдельно. Gold включает только commands, kill rewards и единственный bonus; terminal Defeat не получает bonus. Synthetic armored/dense observer fixture отделён от saved Shade Armor 0.

File rollback использует temporary roots и настоящие WaveCheckpointStore/BattleCheckpointController APIs: restore грязной simulation и fresh instance, два replay interrupted prefix, full remaining match, повторные WaveComplete Load и Next. PlayMode дополнительно проходит настоящий MainMenu Continue и проверяет чистые presentation transients. Schema 2 неизменна; terminal enemies не сериализуются и не требуются при restore. [Измерения/ограничения](review/P2.2f/impact-review.md); техническое закрытие P2.2 только после review/merge, G2 и balance/device gates открыты.


### P2.3c: persistence progression без production activation

Schema 3 добавляет `ElementProgressionCheckpoint`/`SpentElementChoiceCheckpoint`. Domain валидирует snapshot целиком до замены State/Grid и очистки events: canonical unlocked set, exact ordered history prefix, credits по completed waves, pending по фазе/волне, requirements армии и прежние scalar/economy/ID constraints. Export/Restore не разделяют mutable DTO с live state. JSON shape проверяется отдельно до JsonUtility, включая обязательные zero-valued поля, типы, диапазоны и duplicate keys.

`WaveCheckpointCompatibility.TryNormalize` реализует whitelist schema/content/rules и deep migration. `BattleScenarioConfig.CreateSimulationForCheckpoint` выбирает native/legacy rules по нормализованному файлу, используя прежние validated combat data. На этапе P2.3c public New Game оставался all-open. Factory overload `WaveCheckpointStore.TryLoad` выполняет независимую decode/normalize/factory/validation для обоих слотов; старый validator overload остаётся exact-ID synthetic/legacy path. Production MainMenu/Scene/CheckpointController используют factory routing до создания simulation; legacy content ID сохраняется при следующем schema3 Save.

TrySave проверяет source, temp/read-back и previous primary; только валидный previous primary заменяет backup. Internal writer boundary отделяет durable temp write/atomic commit и позволяет воспроизводимо проверять IO отказы без повторения Domain-команд. Load/Normalize не пишут файлы. Pending initial/completion и successful Confirm сохраняются явно. Read-only eligibility предшествует записи; Next: save WaveComplete → Domain transition → save Preparation, Start: save Preparation → Combat. Failed save не отменяет live mutation; Start/Next могут повторить запись, но переход разрешён только после успешного SaveCurrentState текущего snapshot. Retry SaveCurrentState не вызывает Choose/Next/Step. Failed New Game reset сохраняет прежний recovery contract.

[Проверки P2.3c](review/P2.3c/validation.md). UI выбора/production activation и natural unlock replay относятся к P2.3d/e. Исторический P2.2f trace по-прежнему сравнивает полный schema2-shaped combat checkpoint; progression проверяется новым suite отдельно, golden файлы не менялись.

### P2.3d: native config и Presentation

`BattleScenarioConfig.progression` сериализует rules ID, стартовые options и choice waves. Public EightWave требует точные Flame/Gale, 3/6 и восемь волн; отсутствие/подмена настроек не включает all-open. Builder обновляет прежний EightWave wrapper через SerializedObject, сохраняя GUID/shared TestWave. Legacy Continue выбирается существующей compatibility factory по content/rules checkpoint. `CreateHistoricalAllOpenSimulation` — internal test path для прежних combat/economy controls, без пользовательского переключателя.

`ElementChoiceController` хранит только preview option/ожидаемый pending ID. Immutable Domain progression остаётся источником options и подтверждённых открытий; смена snapshot, Pause/background и Restore очищают preview. Confirm идёт через `BattleSceneController.ChooseElement`, включая same-phase save. Cancel не отменяет обязательный pending. После3/6 Confirm не запускает следующую волну.

`BattleSceneController.CanAcceptInput` сопоставляет Pause и владельца активного modal: Sell, Inspector, Choice, Placement. Публичные gameplay API не получают права чужого modal; internal owner routes доступны только нужному controller. Buttons используют те же guards. Read-only Inspector принимает world tap при собственном owner и может забрать обычный выбор построенной башни; Sell/ElementChoice/Pause это исключение не получают. World pointer dispatcher сначала маршрутизирует inspector hit test и поглощает miss при открытом инспекторе, поэтому чтение не проходит в placement. Domain независимо проверяет все правила. Pause очищает selection/preview, reset accumulator и первый delta; Resume подавляет world pointer текущего кадра. Read-only locked cards используют Domain requirements и availability, Gold reason отдельный. Hidden test-tower parameterless path в native запрещён.

`BattleCheckpointController.RetrySave` вызывает только SaveCurrentState после input guard; кнопка появляется при failed save. Safe-save retry перед Start/Next сохранён из P2.3c. `BattleHudSafeArea` корректирует edge controls, выбор имеет отдельный safe-area root, fullscreen Pause/choice blockers не пропускают world taps. uGUI Text/Button сохраняются; новые packages/Domain dependencies не добавлены. [Тесты, кадры, Console audit и ограничения](review/P2.3d/validation.md).


### P2.3e: production validation без runtime изменений

`NativeEightWaveReplayTests` читает committed JSON scripts и saved EightWave через public factory; observer фиксирует каждый public Step/command/result, весь Grid, IDs, tower investment/cooldown, enemy statuses/timestamps и все поля Attack/Reaction/Damage events. Safe snapshots включают полный schema3 progression; повтор сравнивается с первым trace и committed golden summary/hash. `FAIL` proc coverage остаётся явным полем отчёта: зелёный deterministic Defeat не подтверждает playable acceptance.

`NativeEightWaveFileTests` использует temporary slots, production compatibility factory/Store и CheckpointController. Повторный Load не пишет bytes, полный safe restore в replay совпадает с control; dirty Combat4 после kill+proc повторяется идентично. Pending/confirmed3 и failed save/restart/Retry проверяются целым snapshot. Legacy schema1/2 army — явно synthetic migration input, проходящий настоящий файловый routing в обе стороны backup. Существующие synthetic suites покрывают поздние границы/credits/caps отдельно.

`NativeEightWaveIntegrationPlayModeTests` выполняет настоящий MainMenu→New Game→Battle→Continue через кнопки и controllers; экономика проходит placement/sell UI. Каждый достигнутый wave snapshot сравнивается с Domain production golden. Меню после failed Confirm возвращает durable pending; повторный Continue и Next не удваивают ресурсы. Combat rollback очищает Domain streams, reaction memory/pool, attack beams, inspector/placement/choice preview. Только тестовый clock ускоряет public Step и вызывает штатные presentation consumers; это реальные Play Mode кадры production data, но не realtime VFX/performance или device verification.

[Evidence/матрица acceptance](review/P2.3e/validation.md): оба старта Defeat5/tick2246; собственные Steam/Storm pair/intrinsic положительны, after6/вторая пара/второй гибрид и Combat7 не достигнуты. Это исторический результат P2.3e; P2.3e.1 закрыл поздние acceptance, после review и merge #32 технический P2.3 DONE. Runtime, config hashes, packages и историческое evidence сохранены.


### P2.3 completion / следующий контракт P2.4a

PR #32 merged (`8128010`): технический P2.3 завершён, оба native старта имеют natural Victory8 и полный late file/menu/Combat7 matrix; [evidence](review/P2.3e.1/validation.md). Device/G2/баланс/ART approval отдельно. Исторические P2.3e/e.1 отчёты описывают состояние на момент подготовки и не переписываются.

[Предлагаемый P2.4a контракт](P2.4A_ABILITY_BOSS.md) и [план #36–40/REG190…217](P2.4A_IMPLEMENTATION_PLAN.md) имеют статус SPECIFIED. В них определены синхронный Combat-only cast между Steps, общий death/reward/completion pipeline, schema4/content whitelist, input owner Ability и boss wave8. Это будущие API/числа для review; текущие runtime/schema3/configs не изменены.


### P2.4b implemented Domain ability contract

Production/public factories сохраняют disabled ability. Explicit internal synthetic constructor добавляет immutable `AbilityDefinition` и read-only `AbilityState`; enabled Export/Restore/validation явно unsupported до P2.4c, без изменения schema3/старых content IDs.

`CheckCastAbility` read-only; `CastAbility` повторяет ту же ordered validation: Combat → enabled → ID → expected wave/tick/sequence → cooldown → finite half-open center bounds → alive targets → exhausted sequence. По принятому P2.4a bounds равны `[OriginX, OriginX+Width*CellSize) × [OriginY, OriginY+Height*CellSize)`; способность не использует grid snap/occupancy. Targets inclusive radius захватываются целиком с позициями, в EnemyId order; reused buffer заранее выделен по validated spawn budget. До validation success нет mutation/clear событий.

Успешный cast между Steps не advance tick: выставляет600CombatSteps и увеличивает sequence, публикует AbilityCastEvent перед damage, учитывает armor/Crack/min1/HP clamp без Gust/status/reaction. Step уменьшает cooldown перед Spawn/Burn, включая последний Combat Step; вне Combat freeze, между волнами carry. Step/Cast используют общий death/removal/reward/completion handler с прежним Defeat priority и one-time pending/bonus. Старый leak-before-tower порядок сохранён.

DamageOrigin.Ability добавлен после прежних enum values; optional SourceAbilityId/AbilityCastSequence дополняют DamageResolvedEvent, SourceTowerId=default, AttackEvent не публикуется. Ability/Reaction/Damage имеют общий возрастающий SequenceInTick: reset только на следующем tick/успешном Restore, Clear очищает только stream. Новые поля не изменяют historical trace writer. [REG190–197/evidence](review/P2.4b/validation.md); UI/input/Pause dispatch и four-stream consumption — #38.

### P2.4d: ability Presentation (#38)

AbilityController использует существующий BattleInputOwner.Ability: Begin только enabled Combat Ready, target preview не останавливает бой. Confirm потребляет lease epoch/generation/wave/sequence и публикует один intent; BattleSceneController разбирает его в LateUpdate после fixed Steps с актуальным ExpectedTick и повторной validation. Cancel/Pause/Restore/phase/simulation invalidation очищают lease/intents. Stale callback не уничтожает новое preview. Реальные callbacks и public APIs соблюдают общий guard; внутренний dispatch проверяет wired controller, произвольный owner снаружи не передаётся.

Mouse/touch dispatcher учитывает Down/Up/Cancel и удерживаемые pointer IDs; UI Down и второе касание не становятся world tap. Suppression охватывает кадр перехода и held input до release; Resume не даёт catch-up/auto-cast. Preview validity/причина читаются из Domain CheckCastAbility; definition radius доступен read-only, формулы не дублируются.

После каждого Step/успешного Cast четыре streams очищаются в общем finally; Reaction/Ability events сливаются по SequenceInTick, ability impact использует captured lethal positions и attribution. Bounded pool32 сохраняет только короткие presentation effects, не урон. Clock заморожен Pause; successful restore чистит pool/summary/owners/intent/beams, rejection сохраняет их. Same-tick removal проверяется по живым EnemyId, поскольку Cast не advance tick.

Production остаётся ability-disabled; saved Battle имеет wired, но скрытые controls. Enabled UI и persistence доступны только internal synthetic-p24-ability-v1 fixture в tests, не в production whitelist/menu. Boss/final mission активируются совместно в #39. [Спецификация](P2.4D_ABILITY_UI.md), REG204–208 и [evidence](review/P2.4d/validation.md). Физическое устройство/G2/ART не проверены.

### P2.4e — versioned boss mission (R3 Editor acceptance VERIFIED)

WaveDefinition принимает immutable WaveSpawnGroup, строит bounded schedule (до65536 entries/mission) с порядком relativeTick/groupIndex/spawnIndex. Старый constructor — single-group adapter. Start до phase mutation проверяет IDs и абсолютный last-spawn overflow. Step читает schedule без сортировки/allocation; прежние Burn→Movement/leaks→tower/status/reaction→death/reward и Defeat priority сохранены.

EightWaveAbilityBoss использует shared TestWave и отдельный historical EightWave reference: новый New Game включает rift-seal-v1 + одного rift-warden330; old content factories сохраняют12Shade8/disabled ability/IDs. Final-ID только schema4/native/enabled. Load каждого слота и Save previous-primary используют собственные factories; ни Load, ни Normalize не пишут bytes. Boss transient не сериализован. BossHud читает Domain, показывает notice/HP/armor/leak/reward, адаптирует геометрию между safe-area controls; прежние два markers и inspector используются без третьего slot. UI не наносит damage.

[Scope](P2.4E_RIFT_WARDEN.md), [проверки/ограничения](review/P2.4e/validation.md). R3 по разрешению владельца меняет только boss armor10→5: оба natural starts Victory8, whole Victory persistence/IO Retry и terminal menu проверены. Полные static38/38, EditMode1048/1048, PlayMode58/58 и graphical3/3 зелёные; [R3](review/P2.4e/revision-3/validation.md). Старые control1000/10 и R1/R2 FAIL evidence сохранены. Device/G2/ART/#40 отдельно.

### P2.4f — boss content v2

Новый wrapper EightWaveAbilityBossV2 с 600HP/armor5 хранит явные ссылки historicalBossScenario (v1/1000/5) и historicalScenario (native); shared catalogs, spawns, ability и economy прежние. Schema4 неизменна. Normalize не переименовывает ID: requested v2 поддерживает v1/native/all-open Continue через matching factory; обратное v2→v1 запрещено. Каждый слот и previous primary валидируется своей factory. Live controller Restore требует exact content ID до mutation; смена версии выполняется только созданием matching Session/Scene instance. Production asset validator закрепляет v2 ID в MainMenu/Battle. [Decision/evidence](review/P2.4f/revision-3/decision.md).
