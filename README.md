# VEILWARD: Spirit Defense

Unity-проект мобильной tower defense игры для iOS. **Технический P2.3 DONE** после merged [PR #32](https://github.com/AlexeyKravtsov95/Veilward/pull/32), `8128010`: native FLAME/GALE, unlock3/6, schema3/legacy Continue, natural Victory8 обоих стартов и late file/menu/Combat7. [Evidence P2.3e.1](Docs/review/P2.3e.1/validation.md) дополняет сохранённый исторический P2.3e Defeat5. Баланс/G2, device QA и ART approval отдельно открыты.

P2.4a–d merged в PR #65–68; D-01…D-07 приняты владельцем. **P2.4e/#39 — R3 Review:** по разрешённому balance pass выбран boss1000HP/armor5; оба natural starts Victory8, прежние saves/rules сохранены. Editor acceptance и полные suites зелёные; merge/release не выполнялись. [Evidence P2.4e](Docs/review/P2.4e/validation.md), [план #36–40](Docs/P2.4A_IMPLEMENTATION_PLAN.md). M0/device/G2/ART и полная #40 integration matrix — отдельные gates.

## Окружение

- Unity: **6000.3.28f1 (8f082d2fe476)** — Unity 6.3 LTS.
- Render pipeline: **URP 17.3.0**.
- Unity CLI: **1.0.0-beta.17**.
- Python 3 для проверки JSON-результата при запуске тестов в открытом Editor.
- Для iOS-сборки необходимы установленный модуль **iOS Build Support** и активная Unity license.
- Активная платформа: **iOS**; IL2CPP, ARM64, минимальная iOS **17.0**.
- Ориентация: только **Landscape Left / Landscape Right**.

Не изменяйте patch-версию Unity и версии пакетов без отдельного согласования.

## Структура проекта

```text
Assets/_Game/
├── Art/
├── Audio/
├── Configs/
├── Prefabs/
├── Scenes/
├── Scripts/
│   ├── Application/
│   ├── Domain/
│   ├── Editor/
│   ├── Infrastructure/
│   └── Presentation/
└── Tests/
    ├── EditMode/
    └── PlayMode/
```

Runtime-сборка: `Veilward.asmdef`. Editor-only код вынесен в `Veilward.Editor.asmdef` с `includePlatforms: ["Editor"]`, поэтому `BattleSceneBuilder` не входит в player-сборки. `BootstrapLoader` загружает `MainMenu`; при прямом запуске Battle `BattleSceneController` управляет тестовой симуляцией и её отображением.

## P1.2 Grid & Tower Placement

Чистая C#-сборка `Veilward.Domain.asmdef` имеет `noEngineReferences: true` и не зависит от `UnityEngine`, Scene или `MonoBehaviour`. `BattleSimulation` владеет картой, золотом, занятостью клеток, башнями, spawn, движением, leaks и фазами. Presentation обрабатывает ввод и отражает состояние, но не изменяет золото или занятость напрямую.

`Assets/_Game/Configs/TestWave.asset` задаёт сетку **12×8**, origin `(-6,-4)`, cell size 1, стартовые **150 gold** и Test Tower ценой **60 gold**. Маршрут неизменяемый: `(-5,-2) → (0,-2) → (0,2) → (5,2)`. Пять Shade имеют HP 100, speed 1.5, leak damage 1 и spawn на тиках **0/30/60/90/120**. Здоровье базы — 20; Presentation вызывает фиксированные шаги **30 Hz**.

Для проверки откройте `Assets/_Game/Scenes/Battle.unity` напрямую и войдите в Play Mode. Начальная фаза — `Preparation`, врагов нет. Первый tap по свободной клетке открывает каталог без покупки; выбор FLAME/TIDE/GALE/STONE показывает stats и preview. **Confirm** строит выбранный тип за 70/60/80/90 gold соответственно; **Cancel** отменяет выбор. Клетки маршрута, занятые клетки и строительство в `Combat` отклоняются без изменения золота. **Start Wave** запускает волну вручную и идемпотентен. HUD показывает Gold, Towers, tick, фазу, здоровье базы и число активных врагов. После пяти leaks фаза становится `WaveComplete`, а здоровье базы — 15.

Graybox-сцену и assets можно повторно сгенерировать через пункт меню **Veilward → Build P1.3 Battle Scene**. Builder получает карту, маршрут и боевые параметры из `BattleScenarioConfig`, создаёт 96 cells, tower prefab, selection/preview, range preview, health bars, HUD и Input System EventSystem. Повторная генерация не перезаписывает `towerRange`, `towerFireRate` или `towerDamage` существующего `TestWave.asset`; значения по умолчанию назначаются только при создании отсутствующей конфигурации. Обязательные ссылки контроллеров проверяются после сохранения.

## P1.3 Targeting & Combat

Каждый боевой тик выполняет стадии **Advance Tick → Spawn → Movement/Leaks → Tower Attacks → Death/Removal → Defeat/WaveComplete**. Test Tower имеет **Range 3, FireRate 2, Damage 25**: интервал атаки вычисляется как `max(1, round(30 / FireRate))`, то есть 15 тиков. Без цели cooldown не запускается.

Стратегия **First** выбирает живого врага в радиусе с максимальным прогрессом по пути; при равенстве выбирается минимальный Enemy ID. Башни атакуют по Tower ID. HP ограничено нулём, смерть засчитывается один раз, погибшие удаляются после всех атак тика и не наносят leak damage. Атаки публикуют `AttackEvent` с Domain-позицией цели, зафиксированной непосредственно в момент урона. Presentation обрабатывает и очищает список после каждого fixed step, создавая короткий beam по координатам события без обращения к Enemy View или уже удалённой цели. Health bars отражают Domain HP, HUD дополнительно показывает **Kills**, а selection отображает configured range.

Для ручной проверки выберите свободную клетку рядом с началом маршрута, подтвердите постройку и нажмите **Start Wave**. Видимые beams и уменьшение health bars соответствуют атакам; уничтоженные враги исчезают и увеличивают Kills без уменьшения Base Health.

Порядок сцен в Build Settings:

1. `Assets/_Game/Scenes/Bootstrap.unity`
2. `Assets/_Game/Scenes/MainMenu.unity`
3. `Assets/_Game/Scenes/Battle.unity`

`MainMenu` не изменялось; переход из меню в Battle в этом тикете не добавляется.

## P1.4a Multi-Wave Lifecycle & Rewards

`Assets/_Game/Configs/EightWave.asset` задаёт восемь волн с количеством Shade **5, 6, 7, 8, 9, 10, 11, 12** и ссылается на `TestWave.asset` через `sharedScenario`, не дублируя карту, маршрут и боевые параметры. Каждый kill приносит **5 gold**, завершение волны — **20 gold**, включая финальную. Leak не даёт награды; Defeat не даёт wave bonus; начисление Gold насыщается на `int.MaxValue`.

**Start Wave** переводит Preparation в Combat, **Next Wave** доступен только в WaveComplete и переводит к следующей Preparation. Между волнами сохраняются глобальный tick, Enemy/Tower IDs, башни и cooldown, Gold, Base Health и Kills. Последняя волна завершается Victory; повторные и недопустимые команды не меняют состояние. HUD показывает `Wave: current / count`, Phase и terminal Result. Legacy `TestWave.asset` остаётся single-wave с bonus 0 и финальной фазой WaveComplete.

Актуальную graybox-сцену и оба config можно повторно сгенерировать через **VEILWARD → P1.4b Build**. Builder сохраняет существующие настройки волн, наград, боя и уровней башни; validator проверяет EightWave, экономические панели, кнопки и остальные обязательные ссылки.

## P1.4b Tower Economy

Test Tower имеет три serialized уровня с Damage **25/35/50**, Range **3**, FireRate **2** и стоимостью покупки/улучшений **60/50/80 gold**. `InvestedGold` равен **60/110/190**, а refund при продаже округляется вниз до 70%: **42/77/133 gold**. Level 2 и Level 3 хранятся в `towerUpgrades`; прежние serialized поля остаются источником Level 1.

Occupied cell в Preparation открывает Tower Panel с характеристиками, стоимостью следующего Upgrade и текущим refund. **Upgrade** и **Sell** выполняются только через атомарные Domain-команды в Preparation; в Combat характеристики доступны только для чтения. Upgrade сохраняет TowerId, координату и cooldown. Sell требует отдельного подтверждения, привязанного к выбранному TowerId: прямой ConfirmSell без Request или после смены selection отклоняется без изменения Gold/Grid. При открытом диалоге конфликтующие действия и кнопки заблокированы; Cancel восстанавливает их без Domain mutation. Подтверждённая продажа удаляет Tower View, освобождает только принадлежащую башне клетку, начисляет refund один раз и очищает selection/confirmation. Повторная постройка получает новый монотонный TowerId.

Полный контракт находится в `Docs/P1.4B_TOWER_ECONOMY.md`, regression scenarios — `REG-BATTLE-025…050` в `Docs/REGRESSION.md`.

## P1.4c Gameplay Validation & Balance Baseline

PR #10 (`dc17cca`) merged в `main` 2026-11-10. Добавлены deterministic replay трёх стратегий и сохранённого FlexibleEconomy flow, включая Sell/Rebuild с новым TowerId; зафиксированы результаты восьми волн и десять реальных Play Mode кадров 1600×900. Финальные локальные gates: **38/38 static self-tests**, **208/208 EditMode**, **3/3 PlayMode**, 0 failed/inconclusive/skipped и `git diff --check` PASS. Контракт и balance review находятся в `Docs/P1.4C_VALIDATION_SPEC.md` и `Docs/P1.4C_BALANCE_REVIEW.md`.

## P1.5a Pause & Background

`BattlePauseController` хранит latched manual pause, application pause и focus. Background/focus loss ставят игру на паузу, но возврат не снимает её автоматически; Resume доступен только при активном focused application. Во время паузы fixed-step simulation, gameplay commands/buttons и attack beams останавливаются, accumulator очищается, а первый resumed Update не выполняет catch-up. Selection и Sell confirmation отменяются без изменения Gold/Grid. Полноэкранный Pause Overlay блокирует raycasts, а Resume подавляет сквозной world pointer на текущий кадр. `Time.timeScale`, Domain balance и disk persistence не изменяются.

Полный контракт находится в `Docs/P1.5A_PAUSE_BACKGROUND.md`, regression scenarios — `REG-BATTLE-054…066`. Три кадра 1600×900, telemetry Pause/Resume и фактический scenario Console report с 0 errors находятся в `Docs/review/P1.5a/`. Это Editor Play Mode evidence, не physical iOS verification.

## P1.5b Wave Checkpoint & Continue

Версионированный JSON checkpoint содержит только safe-wave состояние Preparation, WaveComplete, Victory или Defeat: phase/wave/tick, HP/Gold/Kills, next IDs и towers с координатами, Level/InvestedGold/NextAttackTick. Enemies, spawn index, RNG, AttackEvents и UI transients не сериализуются. Прерванный Combat восстанавливается в Preparation перед Start Wave; повторный Continue не начисляет награды повторно. `wave-checkpoint-v1.json` записывается через validated temp и атомарную замену с `.bak`; load проверяет primary, затем backup. Failed save блокирует Start Wave, но не откатывает успешную live Build/Upgrade/Sell command. New Game создаёт recovery copy до удаления слотов и восстанавливает primary при failed reset, включая ошибку на BackupPath; повреждённые исчерпанные next IDs вызывают fallback на backup, а безопасное исчерпание счётчиков отклоняет команду без Domain mutation.

MainMenu предоставляет явные **New Game** и **Continue**. Continue доступен только для совместимых Preparation/WaveComplete; terminal checkpoint не перезапускает матч. Прямой запуск Battle без session context использует in-memory store и не читает/пишет реальный `persistentDataPath`. Полный контракт — `Docs/P1.5B_WAVE_CHECKPOINT.md`, regression scenarios — `REG-BATTLE-067…089`. Три реальных Editor Play Mode кадра 1600×900, sanitized `checkpoint-example.json`, telemetry и Console audit с 0 errors находятся в `Docs/review/P1.5b/`.

## P2.1b Elemental Definitions & Checkpoint Schema 2

`TestWave.asset` содержит четыре validated архетипа **flame/tide/gale/stone**, по три уровня с полными параметрами статусов и ArmorPenetration; Shade имеет Armor 0, а legacy `test-tower` сохранён без статуса и скрыт из Build UI. Domain поддерживает строительство по `TowerDefinitionId`, Upgrade следующего уровня того же типа и Sell с refund от фактического InvestedGold. Активные статусы добавлены в P2.1c; актуальный порядок тика и формулы описаны в `Docs/TECH_DESIGN.md`.

Checkpoint schema 2 добавляет обязательный `towerTypeId`. При Continue совместимый schema 1 мигрируется в памяти: каждая старая башня явно становится `test-tower`, остальные поля сохраняются, source JSON не перезаписывается. Следующий Save использует прежние `wave-checkpoint-v1.json`, temp/atomic replace и backup, но записывает schema 2. Missing/empty/unknown type отклоняется, а валидный backup остаётся доступен. Подробности — `Docs/P2.1A_ELEMENTAL_TOWERS.md` и REG-BATTLE-090…099 в `Docs/REGRESSION.md`.

## P2.1c–e: стихийный матч и проверяемые результаты

Burn наносит DoT, Wet замедляет движение, Gust усиливает последующие прямые атаки, Crack уменьшает броню. Каталог показывает четыре типа, уровни/статусы/цену Upgrade и refund Sell; Combat read-only, Pause и подтверждение продажи блокируют действия. Сохраняются типы, уровни, вложения, cooldown и next IDs; прерванный Combat повторяется с сохранённой Preparation без наград попытки.

`BattleElementalReplayTests` воспроизводит saved EightWave через public commands. Зафиксирована стратегия со всеми четырьмя статусами, Upgrade и Sell/Rebuild: **Defeat, волна 8, tick 3997, HP 0, Gold 112, Kills 46, 8 DoT kills**. Победа не является условием теста. Два полных прогона и прохождение с файловым Continue/rollback совпадают. [Balance review](Docs/review/P2.1e/balance-review.md) и [JSON по волнам](Docs/review/P2.1e/replay-summary.json) отделяют измерения от гипотез; P2.1e merged в PR #17.

Сохранено визуальное evidence P2.1d: [каталог](Docs/review/P2.1d/01-catalog.png), [preview](Docs/review/P2.1d/02-preview.png), [статус в бою](Docs/review/P2.1d/03-status-combat.png), [Upgrade](Docs/review/P2.1d/04-upgraded-panel.png), [Sell](Docs/review/P2.1d/05-sell-confirmation.png), [Continue](Docs/review/P2.1d/06-continued-tower.png), [QA log](Docs/review/P2.1d/qa.log). Новые визуальные изменения в P2.1e отсутствуют. Editor tests/screenshots не подтверждают iPhone, реальные touch/safe area/background, производительность или готовность баланса.

## P2.2c: runtime реакций (без production включения)

Pure C# `ReactionResolver` разрешает Steam/Storm после direct damage event и обычного статуса выжившей primary. Он планирует ограниченный набор целей, резервирует общие primary/secondary окна по виду на 60 ticks, затем наносит secondary damage через long floor/saturation и armor/Crack. Смерти и награды остаются в существующем pipeline. `EnemyState.LastSteamTick/LastStormTick` доступны только для чтения.

`BattleSimulation` принимает optional `ReactionCatalog`, сохраняет прежние constructors и предоставляет read-only `ReactionEvents`/`DamageEvents` с явными clear methods. `BattleSceneController` очищает все три потока в `finally` после каждого fixed Step; на этапе P2.2c новая telemetry отбрасывалась без VFX; P2.2e добавил её отображение. Успешный Restore очищает transient state, rejected Restore его сохраняет. До P2.2d public config использовал Empty; internal factory с явным Empty сохраняет historical P2.1e fixture. Saved configs, сцены, UI, schema и баланс не менялись. [Проверки и ограничения](Docs/review/P2.2c/README.md); независимый PR review обязателен до merge.

## P2.3c: schema 3 и совместимость progression

Safe-wave checkpoint теперь пишет schema **3** с deep-copy progression. На этапе P2.3c public New Game оставался all-open; P2.3d ниже включает native. Native contract проверяется через opt-in test factory; `eight-wave-unlocks-v1` принимает родной checkpoint или ровно прежний `eight-wave-v1`, single-wave остаётся отдельным.

Schema 1/2 мигрируются только в памяти: прежние башни v1 → `test-tower`, v2 сохраняет точные типы/уровни/вложения/cooldown/ID/cells, включая Steam/Storm. Legacy получает все четыре стихии без earned/spent/history/pending и backfill. Следующий Save пишет schema 3 с прежним content ID. Decode → normalize → factory → Domain validation выполняются независимо для primary/backup; Load не меняет bytes. Повреждённый primary не вытесняет валидный backup.

Успешный Confirm сохраняется даже без смены фазы. Failed save оставляет live-команду, блокирует Start/Next до успешного Retry Save и допускает полный откат к durable snapshot после выхода. Eligibility проверяется до файловых side effects; Next сохраняет WaveComplete до перехода и Preparation после него. [REG-174…183](Docs/REGRESSION.md), [evidence и ограничения](Docs/review/P2.3c/validation.md). P2.3c не подтверждал production UI; проверки P2.3d приведены ниже, device QA и баланс остаются открыты.

## Настройки репозитория

- Asset Serialization: **Force Text**.
- Version Control: **Visible Meta Files**.
- Unity `.meta` файлы коммитятся вместе с assets.
- `Library`, `Temp`, `Logs`, `UserSettings`, результаты тестов и сборки исключены через `.gitignore`.

## Quality Gates

Обязательные команды:

```bash
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
git diff --check
```

Static checks не требуют Unity license: Python stdlib tests проверяют сами validators, CI security contract и fake Unity runner backends, затем Unity version/revision, frozen direct package baseline и lock, paired/orphan `.meta`, GUID и Assembly Definitions. Локальный результат P2.1e: **38/38 static self-tests passed**, project checks PASS.

EditMode suite покрывает Domain, regression contract `REG-BATTLE-001…127`, typed catalog/economy, checkpoint round-trip/migration/validation/storage, MainMenu, сохранённые сцены и воспроизводимость генератора. Локальный результат P2.1e: **317/317 EditMode tests passed**, 0 failed/inconclusive/skipped. PlayMode дополнительно проверяет реальные MainMenu/Battle scene reloads, interrupted Combat → restored Preparation, repeated Continue без duplicate rewards и изоляцию direct Battle: **15/15 PlayMode tests passed**, 0 failed/inconclusive/skipped. `git diff --check`: PASS; `Packages/manifest.json` и `Packages/packages-lock.json` не изменялись.

Runners принимают только полный успех: `Total > 0`, `Passed == Total`, а `Failed`, `Inconclusive` и `Skipped` равны нулю. XML validator независимо проверяет NUnit/JUnit reports. Default `auto` сохраняет локальный Unity CLI path с JSON summary; во время Play Mode domain reload временный `EDITOR_NOT_RUNNING` повторно опрашивается до общего deadline, остальные ошибки завершают run. Self-hosted CI явно вызывает `run-unity-tests.sh <Mode> batch` и получает NUnit XML и log для EditMode/PlayMode. Batch отклоняет resident Editor или live descriptor и не запускает второй Editor. В clean checkout используется `UNITY_EDITOR_PATH` либо стандартный Unity Hub path; batch mode получает `-buildTarget iOS` и сохраняет report/log в ignored `TestResults`. Batch test invocation не передаёт `-quit`, потому что на Unity 6000.3.28f1 это завершало Editor до фактического Test Runner run; report проверяется только после реального завершения tests.

GitHub Actions workflow `Quality Gates` выполняет static checks на PR/push в `main` на `ubuntu-latest`. Отдельный workflow `Unity Personal Tests` запускает EditMode/PlayMode только вручную при `github.ref == refs/heads/main` через `[self-hosted, macOS, veilward-unity-personal]`. Checkout закреплён на `github.sha` и сверяет фактический HEAD с ожидаемым SHA. Unity Personal активируется владельцем через Unity Hub: workflow не использует GameCI, ALF/ULF, Unity account secrets или serial. Сейчас Unity CI **NOT VERIFIED / BLOCKED**, потому что runner не зарегистрирован и полный manual PASS ещё не получен. Автоматический запуск непроверенного PR-кода на self-hosted runner не разрешён. Процедура изоляции и включения описана в `Docs/QUALITY_GATES.md`; регрессионный контракт — в `Docs/REGRESSION.md`.

## iOS build и Play Mode evidence

Unity создаёт Xcode project для iOS. Apple signing, provisioning и установка на устройство выполняются владельцем проекта; приватные credentials в репозиторий не добавляются. Для P1.5b новый iOS export не запускался; предыдущие exports без signing не являются device-проверкой текущего scope.

P1.5b проверен в настоящем Editor Play Mode через реальные `Button.onClick.Invoke` и Presentation placement API: New Game → Build/Upgrade → interrupted Combat с Gold 45/Kills 1 → MainMenu Continue → restored Preparation с Tick 0/Gold 40/Kills 0/Tower Level 2, затем отдельный New Game flow до WaveComplete с Gold 135/Kills 5. Три кадра **1600×900**, точная команда, telemetry и ограничения находятся в `Docs/review/P1.5b/qa.log`. Scenario Console listener записал **0 errors** и 6 Editor rendering warnings; временный QA-код и raw Editor log не входят в изменения.

**Физический iPhone не проверялся.** Force quit/relaunch, built-player touch, signing, safe area, производительность, Simulator и реальные iOS background/focus для P1.5b также не проверялись. Ветка передаётся на независимый review; self-merge не выполняется.


## P2.2d — гибриды и production config

Shared `TestWave.asset` содержит global Steam/Storm (60 ticks) и три уровня `steam`/`storm` по §6 [спецификации](Docs/P2.2A_REACTIONS.md). Каждый гибрид делает обычный direct hit и единственный intrinsic запрос общего resolver, без StatusEffect/ArmorPenetration. `EightWave.asset` хранит ссылку на TestWave без собственных копий tower/reaction catalogs. Invalid/частичные данные отклоняются; builder расширяет только полный прежний набор четырёх типов и сохраняет корректные данные при повторном запуске.

Schema version **2**, content IDs и Store/migration API сохранены: payload определяется типом/уровнем, transient бой не сериализуется. Historical P2.1e replay использует явный internal Empty и прежние expectations/evidence. Новые данные, экономика/отказы, совместимость saves и публичная production factory покрыты REG-BATTLE-146…153. [Evidence, реальные Play Mode кадры и ограничения](Docs/review/P2.2d/README.md). Причинная визуализация проверена в P2.2e; physical device, performance и balance не подтверждены; G2 открыт.

## P2.2e — читаемость реакций и inspector

Graybox Presentation показывает captured Steam ring/impacts и Storm chain, два overhead slots с cause/intrinsic cue, read-only enemy inspector и общий итог фактических hits/AppliedHpLoss. Preview/panel объясняют intrinsic параметры/условия и базовые пары. Pool ограничен, fixed-Step events очищаются в finally, Pause и Restore/Continue/New Game покрыты regressions. Domain/balance/configs/packages не менялись.

Локальные gates: static 38/38, EditMode 520/520, PlayMode 24/24, 0 failed/skipped/inconclusive. [14 реальных Editor Play Mode кадров, Console audit, source SHA и ограничения](Docs/review/P2.2e/README.md). Synthetic fixtures явно обозначены; physical iPhone/performance/баланс/G2 не подтверждены, полный replay/impact проверяется в P2.2f. PR требует независимого review; self-merge/release не выполняются.

## P2.2f — полные replay и файловый rollback

`BattleReactionReplayTests` сравнивает каждый tick/receipt/event полного матча дважды: явный internal `ReactionCatalog.Empty` сохраняет исторический P2.1e JSON; matched использует тот же basic command script с production реакциями; STEAM/STORM имеют отдельные scripts с обычными 150 Gold / 20 HP. HP loss/overkill/lethal сверяются по enemy ID с реальным HP и единственной reward. Suppressions вычисляет test-only observer по snapshots/events, без production log. Synthetic armor fixture сохранён отдельно от saved матчей.

Файловый Store/CheckpointController rollback после настоящего reaction kill возвращает Preparation, HP/Gold/Kills/IDs/cooldown/Grid и очищает enemies/statuses/timestamps/events. Два повторения prefix и последующий полный матч совпадают с control; настоящий MainMenu Continue/Next в PlayMode не повторяет наград. [Результаты, команды, SHA и ограничения](Docs/review/P2.2f/impact-review.md), [проверяемый JSON](Docs/review/P2.2f/replay-summary.json). Runtime/цены/волны не изменены.

[План до первой TestFlight](Docs/ROADMAP.md): после P2.2f — утверждённый на iPhone эталон арта, P2.3, P2.4, оформление среза, P2.5 и device QA. Обязательное условие владельца — готовый арт первого среза: одна оформленная карта, шесть башен, восемь волн, босс, обучение и согласованные UI/VFX/звук. Полный релиз (10 башен/3 карты/15 волн) — отдельная цель. Физические device/performance, external playtests, balance approval и G2 открыты.

Финальные локальные gates P2.2f: **38/38 static**, **529/529 EditMode**, **25/25 PlayMode**, 0 failed/skipped/inconclusive; `git diff --check` PASS. [XML, SHA256 и commit проверенных исходников](Docs/review/P2.2f/gates.json).

## P2.3d: native EightWave и выбор стихий

New Game использует `eight-wave-unlocks-v1`: обязательный FLAME/GALE без preselect, затем только закрытые стихии после волн 3/6. Tap меняет preview, Cancel очищает его, Confirm открывает стихию и сохраняет checkpoint. Межволновой Confirm оставляет WaveComplete: Next и Start требуют отдельных действий. Malformed/missing native settings отклоняются; общие combat data по-прежнему в TestWave.

Все шесть карточек показывают цену, доступность и missing elements; locked description не создаёт world preview и не покупается. Pause/background очищают preview и замораживают симуляцию, Resume не подтверждает выбор. Единый guard защищает UI callbacks и public gameplay API от Pause/чужого modal. При failed save видна ошибка и Retry Save; Retry только сохраняет, Start/Next могут повторить safe save после восстановления storage. Continue восстанавливает pending/confirmed без preview и повторной награды. Старые armies продолжаются с отметкой прежних all-open правил; historical combat suites используют явно именованную внутреннюю factory.

[REG-173/184/185 и Play Mode evidence](Docs/review/P2.3d/validation.md). Natural replay/проходимость — P2.3e; physical iPhone, device QA/G2, TestFlight и balance approval этим PR не закрываются.


## P2.3e: production replay и восстановление

[Command scripts, traces, outcomes и acceptance matrix](Docs/review/P2.3e/validation.md). Saved production factory, 150 Gold/20 HP, публичные команды: A (FLAME→TIDE→GALE) и B (GALE→TIDE→FLAME) дважды дают **Defeat на волне 5, tick 2246**, HP0/Kills13; Gold34 и21 соответственно. Stone route: Defeat5/tick2205/Gold60/Kills12. Неисполненный суффикс после6 не дорисован.

VERIFIED: детерминизм целых traces и файлового replay; Steam intrinsic4/pair5 в A, Storm intrinsic4/pair5 в B; экономика с обычными ценами/refund, accepted Upgrade и repeated Sell; реальные меню/controllers и isolated filesystem, pending/confirmed3, failed-save restart, Combat4 kill+proc rollback, schema1/2/legacy hybrid army и native↔legacy backup. [Результаты](Docs/review/P2.3e/replay-summary.json) и реальные Play Mode кадры отделены от исторических fixtures.

FAIL/BLOCKED acceptance: в обоих scripts ранний Defeat, нет after6, второй пары/гибрида в одном забеге и native Combat7 rollback. Это не доказательство невозможности выиграть другими стратегиями. Это историческое ограничение P2.3e; P2.3e.1 нашёл natural Victory8 обоих стартов и закрыл late acceptance без изменения gameplay/configs. После review и merge #32 технический P2.3 **DONE**; исходные отчёты сохранены; physical iPhone, IL2CPP/Metal player, device QA/G2, баланс, ART approval и TestFlight отдельно.


### P2.4b — Domain способности (#36)

`BattleSimulation.CheckCastAbility` проверяет команду read-only, `CastAbility` синхронно повторяет validation и захватывает живые цели inclusive radius в порядке EnemyId. «Печать Разрыва»: damage120/radius1.5/cooldown600CombatSteps, initially Ready; armor/Crack учитываются, Gust/status/reaction не применяются. Step и Cast используют один death/reward/completion handler; cast не advance tick. `AbilityCastEvent` и optional ability source в `DamageResolvedEvent` сохраняют прежние AttackEvent/call sites и общий sequence текущего tick.

**Production/public factories остаются ability-disabled.** Enabled setup доступен только internal Domain fixture для REG190–197. Enabled Export/Restore/validation явно unsupported до #37, чтобы не потерять cooldown/sequence через старый checkpoint. Текущие saves/schema3, UI и старая wave8 сохранены; schema4/UI/boss/новый content реализуются отдельно в #37–40. [Контракт/план](Docs/P2.4A_IMPLEMENTATION_PLAN.md), [проверки и ограничения](Docs/review/P2.4b/validation.md).

### P2.4d — UI способности (#38)

AbilityController/preview/Confirm/Cancel, Ready/cooldown, одноразовый lease и Steps-before-intent используют production controllers и Domain validation. Общий owner guard защищает callbacks/API/Retry; mouse/touch dispatcher поглощает UI/held/secondary pointers. Четыре event streams очищаются после каждого Step/Cast в finally; bounded ability VFX использует captured positions и замораживается Pause. Restore очищает UI/VFX/intents.

**Production остаётся ability-disabled.** Enabled UI — только internal exact-ID `synthetic-p24-ability-v1` fixture; MainMenu/production whitelist его не принимают. Boss/final mission — совместная активация #39, natural integration #40. [Спецификация/REG204–208](Docs/P2.4D_ABILITY_UI.md), [полные gates, реальные wide/narrow Play Mode кадры, Console и review](Docs/review/P2.4d/validation.md). Editor synthetic test clock не подтверждает physical iPhone/touch/safe area/background/IL2CPP/Metal/performance/G2/ART. Self-merge/release/TestFlight не выполняются.

### P2.4e / #39 — R3, Review

Новый `eight-wave-ability-boss-v1`: ability и rift-warden восьмой волны включаются совместно; прежние saves продолжают старые правила/ID. Добавлены bounded multi-group schedule, boss notice/HP/armor и final schema4 routing. **R3 Editor acceptance VERIFIED:** natural Victory обоих стартов, четыре coverage counters, Victory IO/Retry/terminal Continue и34реальных кадра; выбран armor5 из разрешённого набора. [Scope](Docs/P2.4E_RIFT_WARDEN.md) · [Evidence и точные gates](Docs/review/P2.4e/validation.md). Не готово к merge/release; #39 не закрывается.

Доработка PR #69: расширены все ordered choices и законные стратегии, лучшие попытки FLAME/GALE оставляют boss154/68 HP. **Draft / BLOCKED**, Victory assertions не ослаблены; EditMode1016/1018, PlayMode56/58, graphical1/3. Новые26 Play Mode кадров и точные лимиты поиска — [R2 evidence](Docs/review/P2.4e/revision-2/validation.md). Старый FAIL evidence сохранён; Refs #39 / Refs #40, без merge/release/TestFlight.

**Актуальный R3 итог PR #69:** выбран1000HP/armor5. Static38/38, EditMode1048/1048, PlayMode58/58, graphical3/3 PASS; failed/skipped/inconclusive0. [Матрица/выбор/34кадра/persistence/границы](Docs/review/P2.4e/revision-3/validation.md). R1/R2 выше — история; G2/device/ART/#40 отдельно, без self-merge/release/TestFlight.
