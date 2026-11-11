# VEILWARD: Spirit Defense

Unity-проект мобильной tower defense игры для iOS. Текущий статус — **P1.4b Tower Economy**: детерминированная 30 Hz симуляция восьми волн с ручными переходами, наградами, Victory/Defeat, fixed-path movement, build grid, First targeting, улучшениями Test Tower до Level 3 и продажей с подтверждением в graybox Battle. Стихии, сохранения, IAP и Game Center ещё не реализованы.

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

Для проверки откройте `Assets/_Game/Scenes/Battle.unity` напрямую и войдите в Play Mode. Начальная фаза — `Preparation`, врагов нет. Первый tap по свободной клетке только показывает selection и preview; **Confirm** создаёт одну башню и списывает 60 gold, **Cancel** отменяет выбор. Клетки маршрута, занятые клетки и строительство в `Combat` отклоняются без изменения золота. **Start Wave** запускает волну вручную и идемпотентен. HUD показывает Gold, Towers, tick, фазу, здоровье базы и число активных врагов. После пяти leaks фаза становится `WaveComplete`, а здоровье базы — 15.

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

Static checks не требуют Unity license: Python stdlib tests проверяют сами validators, CI security contract и fake Unity runner backends, затем Unity version/revision, frozen direct package baseline и lock, paired/orphan `.meta`, GUID и Assembly Definitions. Текущий локальный результат P1.5a: **38/38 static self-tests passed**, project checks PASS.

EditMode suite покрывает Domain, regression contract `REG-BATTLE-001…066`, визуальные правила, сохранённую Battle scene, config/prefabs/materials и безопасную воспроизводимость генератора. Текущий локальный результат P1.5a: **221/221 EditMode tests passed**, 0 failed/inconclusive/skipped. PlayMode проверяет загрузку Battle, Upgrade/Sell/Rebuild, модальные confirmations, Pause/Resume, blocked commands, pending-action cleanup, отсутствие catch-up, click-through и отсутствие duplicate listeners: **9/9 PlayMode tests passed**, 0 failed/inconclusive/skipped. `git diff --check`: PASS; `Packages/manifest.json` и `Packages/packages-lock.json` не изменялись.

Runners принимают только полный успех: `Total > 0`, `Passed == Total`, а `Failed`, `Inconclusive` и `Skipped` равны нулю. XML validator независимо проверяет NUnit/JUnit reports. Default `auto` сохраняет локальный Unity CLI path с JSON summary; во время Play Mode domain reload временный `EDITOR_NOT_RUNNING` повторно опрашивается до общего deadline, остальные ошибки завершают run. Self-hosted CI явно вызывает `run-unity-tests.sh <Mode> batch` и получает NUnit XML и log для EditMode/PlayMode. Batch отклоняет resident Editor или live descriptor и не запускает второй Editor. В clean checkout используется `UNITY_EDITOR_PATH` либо стандартный Unity Hub path; batch mode получает `-buildTarget iOS` и сохраняет report/log в ignored `TestResults`. Batch test invocation не передаёт `-quit`, потому что на Unity 6000.3.28f1 это завершало Editor до фактического Test Runner run; report проверяется только после реального завершения tests.

GitHub Actions workflow `Quality Gates` выполняет static checks на PR/push в `main` на `ubuntu-latest`. Отдельный workflow `Unity Personal Tests` запускает EditMode/PlayMode только вручную при `github.ref == refs/heads/main` через `[self-hosted, macOS, veilward-unity-personal]`. Checkout закреплён на `github.sha` и сверяет фактический HEAD с ожидаемым SHA. Unity Personal активируется владельцем через Unity Hub: workflow не использует GameCI, ALF/ULF, Unity account secrets или serial. Сейчас Unity CI **NOT VERIFIED / BLOCKED**, потому что runner не зарегистрирован и полный manual PASS ещё не получен. Автоматический запуск непроверенного PR-кода на self-hosted runner не разрешён. Процедура изоляции и включения описана в `Docs/QUALITY_GATES.md`; регрессионный контракт — в `Docs/REGRESSION.md`.

## iOS build и Play Mode evidence

Unity создаёт Xcode project для iOS. Apple signing, provisioning и установка на устройство выполняются владельцем проекта; приватные credentials в репозиторий не добавляются. Для P1.5a новый iOS export не запускался; предыдущие exports без signing не являются device-проверкой текущего scope.

P1.5a проверен в настоящем Editor Play Mode через реальные `Button.onClick.Invoke` и Presentation placement API: Preparation pause, Combat pause с неизменными Tick/HP/Gold/Kills/позициями/cooldown на восьми кадрах и Resume с продолжением Tick 40 → 41 без catch-up. Три кадра **1600×900**, точная команда, telemetry и ограничения находятся в `Docs/review/P1.5a/qa.log`. Scenario Console listener записал **0 errors** и 2 rendering warnings; временный QA-код и Game View profile не входят в изменения.

**Физический iPhone не проверялся.** Built-player touch, signing, safe area, производительность, Simulator и реальные iOS background/focus для P1.5a также не проверялись. Ветка передаётся на независимый review; self-merge не выполняется.
