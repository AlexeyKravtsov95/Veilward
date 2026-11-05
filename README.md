# VEILWARD: Spirit Defense

Unity-проект мобильной tower defense игры для iOS. Текущий статус — **P1.2 Grid & Tower Placement**: детерминированная 30 Hz симуляция одной тестовой волны, fixed-path movement, build grid и интерактивная установка тестовой башни в graybox Battle. Атаки башен, продажа, улучшения, стихии, VFX, сохранения, IAP и Game Center ещё не реализованы.

## Окружение

- Unity: **6000.3.28f1 (8f082d2fe476)** — Unity 6.3 LTS.
- Render pipeline: **URP 17.3.0**.
- Unity CLI: **1.0.0-beta.13**.
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

Graybox-сцену и assets можно повторно сгенерировать через пункт меню **Veilward → Build P1.2 Battle Scene**. Builder получает карту, маршрут и цену из `BattleScenarioConfig`, создаёт 96 cells, tower prefab, selection/preview, HUD и Input System EventSystem. Обязательные ссылки контроллеров проверяются после сохранения.

Порядок сцен в Build Settings:

1. `Assets/_Game/Scenes/Bootstrap.unity`
2. `Assets/_Game/Scenes/MainMenu.unity`
3. `Assets/_Game/Scenes/Battle.unity`

`MainMenu` не изменялось; переход из меню в Battle в этом тикете не добавляется.

## Настройки репозитория

- Asset Serialization: **Force Text**.
- Version Control: **Visible Meta Files**.
- Unity `.meta` файлы коммитятся вместе с assets.
- `Library`, `Temp`, `Logs`, `UserSettings`, результаты тестов и сборки исключены через `.gitignore`.

## EditMode tests

Тесты проверяют project settings, геометрию пути, spawn schedule, движение, детерминизм, leaks, terminal phases, преобразование serialized scenario в Domain и соответствие визуальной дороги изменённой конфигурации. Отдельно покрыты rasterization при `CellSize = 0.5`, отсутствие публичной мутации занятости Grid и запрет `TowerState` с неположительным ID. Assembly test проверяет, что `Veilward.Editor` и `BattleSceneBuilder.cs` исключены из player compilation. Проверка platform settings использует `NamedBuildTarget.iOS` и не зависит от локального `EditorUserBuildSettings.activeBuildTarget`, поэтому suite воспроизводимо запускается после чистого клонирования и при активной Standalone-платформе.

При открытом Unity Editor с Pipeline package тесты выполняются в нём. Runner принимает только полный успех: `Total > 0`, `Passed == Total`, а `Failed`, `Inconclusive` и `Skipped` равны нулю. Некорректный JSON и ненулевой exit code Unity CLI приводят к ошибке запуска.

Без доступного Editor скрипт запускает Unity в batch mode с `-buildTarget iOS` и создаёт JUnit report.

```bash
./Scripts/run-edit-mode-tests.sh
```

### CI

Используйте macOS runner с установленным Unity **6000.3.28f1**, iOS Build Support, Unity CLI **1.0.0-beta.13** и активной лицензией. Выполните ту же команду:

```bash
./Scripts/run-edit-mode-tests.sh
```

При headless-запуске опубликуйте `TestResults/EditMode.xml` как test artifact. Каталог `TestResults` не коммитится. Не запускайте второй Editor для того же проекта, пока проект открыт в другом Editor.

## iOS build

Unity создаёт Xcode project для iOS. Apple signing, provisioning и установка на устройство выполняются владельцем проекта; приватные credentials в репозиторий не добавляются.

Для P1.2 выполнен чистый Unity iOS **Device SDK export без signing**: Xcode project создан в ignored `Builds/iOS/P1.2-Review`, результат `Succeeded`, **0 errors / 0 warnings**. Экспортированные IL2CPP sources содержат `Veilward.cpp` и `Veilward.Domain.cpp`, но не `Veilward.Editor` или QA; `Veilward.Editor.dll` отсутствует. Дополнительно ARM64 Simulator export и `xcodebuild` без signing завершились успешно; приложение установлено и визуально запущено в landscape на iPhone 17 Pro, iOS 26.1.

Play Mode с virtual mouse проверен по сценарию select → cancel → select → confirm → start wave; маршрут не изменился, Confirm/Cancel не выбирают клетку позади UI. Game View evidence находится в `Docs/review/P1.2/`. **iOS Simulator touch: NOT VERIFIED** — визуальный preview работает, но поддерживаемый ввод для Unity uGUI недоступен; Editor mouse не считается touch-проверкой. **Физический iPhone, signing, safe area и производительность: NOT VERIFIED.**
