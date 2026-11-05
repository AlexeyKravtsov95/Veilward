# VEILWARD: Spirit Defense

Unity-проект мобильной tower defense игры для iOS. Текущий статус — **P1.1 Enemy Path & Movement**: детерминированная 30 Hz симуляция одной тестовой волны, fixed-path movement, leaks и graybox Battle. Башни, экономика, стихии, VFX, IAP и Game Center ещё не реализованы.

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

## P1.1 Enemy Path & Movement

Чистая C#-сборка `Veilward.Domain.asmdef` имеет `noEngineReferences: true` и не зависит от `UnityEngine`, Scene или `MonoBehaviour`. `PathDefinition` предрасчитывает длины сегментов и интерполирует позицию по скалярному расстоянию. `BattleSimulation` владеет spawn, движением, leaks и фазами; `BattleState` и `EnemyState` изменяются только внутри Domain, а публично доступны для чтения.

`Assets/_Game/Configs/TestWave.asset` задаёт пять Shade с HP 100, speed 1.5, leak damage 1 и spawn на тиках **0/30/60/90/120**. Здоровье базы — 20. Маршрут: `(-5,-2) → (0,-2) → (0,2) → (5,2)`. Домен не использует `Time.deltaTime`; Presentation накапливает frame time и вызывает фиксированные шаги **30 Hz**.

Для проверки откройте `Assets/_Game/Scenes/Battle.unity` напрямую и войдите в Play Mode. Волна запускается автоматически; HUD показывает tick, фазу, здоровье базы и число активных врагов. Дошедший до Seal Heart Shade удаляется и наносит один урон. После последнего leak фаза становится `WaveComplete`, последующие шаги не изменяют состояние; lethal leak по-прежнему приводит к `Defeat`.

Graybox-сцену и assets можно повторно сгенерировать через пункт меню **Veilward → Build P1.1 Battle Scene**. Builder получает маршрут из `BattleScenarioConfig.CreateSimulation().Path` и создаёт по нему видимые сегменты и markers; отдельного списка waypoints в Presentation нет. Обязательные ссылки контроллера проверяются после сохранения.

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

Тесты проверяют project settings, геометрию пути, spawn schedule, движение, детерминизм, leaks, terminal phases, преобразование serialized scenario в Domain и соответствие визуальной дороги изменённой конфигурации. Отдельный assembly test проверяет, что `Veilward.Editor` и `BattleSceneBuilder.cs` исключены из player compilation. Проверка platform settings использует `NamedBuildTarget.iOS` и не зависит от локального `EditorUserBuildSettings.activeBuildTarget`, поэтому suite воспроизводимо запускается после чистого клонирования и при активной Standalone-платформе.

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

Для P1.1 успешно выполнен реальный Unity iOS export без signing: Xcode project создан в ignored `Builds/iOS/P1.1-Review`, экспортированные scripting assemblies не содержат `Veilward.Editor.dll`. EditMode tests и Play Mode с Game View также проверены. Последующая сборка через `xcodebuild`, физическое iOS-устройство, signing и safe area **не проверялись**.
