# VEILWARD: Spirit Defense

Unity-проект мобильной tower defense игры для iOS. Текущий статус — **P1.4a Multi-Wave Lifecycle & Rewards**: детерминированная 30 Hz симуляция восьми волн с ручными переходами, наградами за kills и завершение волн, Victory/Defeat, fixed-path movement, build grid и First targeting в graybox Battle. Продажа, улучшения, стихии, сохранения, IAP и Game Center ещё не реализованы.

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

Graybox-сцену и оба config можно повторно сгенерировать через **VEILWARD → P1.4a Build**. Builder сохраняет существующие настройки волн, наград и боя; validator проверяет EightWave, Next Wave и остальные обязательные ссылки.

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

Static checks не требуют Unity license: Python stdlib tests проверяют сами validators, CI security contract и fake Unity runner backends, затем Unity version/revision, frozen direct package baseline и lock, paired/orphan `.meta`, GUID и Assembly Definitions. Текущий локальный результат P1.4a: **32/32 static self-tests passed**, project checks PASS.

EditMode suite покрывает Domain, regression contract `REG-BATTLE-001…024`, визуальные правила, сохранённую Battle scene, config/prefabs/materials и безопасную воспроизводимость генератора. Текущий локальный результат: **175/175 EditMode tests passed**, 0 failed/inconclusive/skipped. PlayMode smoke загружает Battle и проверяет Preparation первой из восьми волн: **1/1 PlayMode test passed**, 0 failed/inconclusive/skipped. `git diff --check`: PASS; `Packages/manifest.json` и `Packages/packages-lock.json` не изменялись.

Runners принимают только полный успех: `Total > 0`, `Passed == Total`, а `Failed`, `Inconclusive` и `Skipped` равны нулю. XML validator независимо проверяет NUnit/JUnit reports. Default `auto` сохраняет локальный Unity CLI path с JSON summary; self-hosted CI явно вызывает `run-unity-tests.sh <Mode> batch` и получает NUnit XML и log для EditMode/PlayMode. Batch отклоняет resident Editor или live descriptor и не запускает второй Editor. В clean checkout используется `UNITY_EDITOR_PATH` либо стандартный Unity Hub path; batch mode получает `-buildTarget iOS` и сохраняет report/log в ignored `TestResults`.

GitHub Actions workflow `Quality Gates` выполняет static checks на PR/push в `main` на `ubuntu-latest`. Отдельный workflow `Unity Personal Tests` запускает EditMode/PlayMode только вручную при `github.ref == refs/heads/main` через `[self-hosted, macOS, veilward-unity-personal]`. Checkout закреплён на `github.sha` и сверяет фактический HEAD с ожидаемым SHA. Unity Personal активируется владельцем через Unity Hub: workflow не использует GameCI, ALF/ULF, Unity account secrets или serial. Сейчас Unity CI **NOT VERIFIED / BLOCKED**, потому что runner не зарегистрирован и полный manual PASS ещё не получен. Автоматический запуск непроверенного PR-кода на self-hosted runner не разрешён. Процедура изоляции и включения описана в `Docs/QUALITY_GATES.md`; регрессионный контракт — в `Docs/REGRESSION.md`.

## iOS build и Play Mode evidence

Unity создаёт Xcode project для iOS. Apple signing, provisioning и установка на устройство выполняются владельцем проекта; приватные credentials в репозиторий не добавляются. Для P1.4a новый iOS export не запускался; предыдущий P1.3 Device SDK export без signing завершился `Succeeded`, **0 errors / 0 warnings**, но не является device-проверкой P1.4a.

P1.4a проверен в настоящем Play Mode через виртуальную мышь Input System: Preparation 1/8, Combat 1/8, WaveComplete с Gold 170 и Preparation 2/8 с сохранёнными Gold/tick, затем Defeat 4/8 с видимым `Result: Defeat`. Пять кадров 1241×913 находятся в `Docs/review/P1.4a/`; `qa.log` фиксирует 24 шага, 5 screenshots и 0 logged errors, HUD audit — 0 проблем, Console — 0 errors. Terminal Victory проверен deterministic Domain tests; полный визуальный Victory не захватывался.

**Физический iPhone не проверялся.** Signing, touch на устройстве, safe area, производительность и Simulator для P1.4a также не проверялись.
