# VEILWARD: Spirit Defense

Unity-проект мобильной tower defense игры для iOS. Текущий статус — **P0.2 Domain Foundation**: настроены фундамент проекта и чистая C#-модель домена без полноценного gameplay, башен, VFX, IAP и Game Center.

## Окружение

- Unity: **6000.3.28f1 (8f082d2fe476)** — Unity 6.3 LTS.
- Render pipeline: **URP 17.3.0**.
- Unity CLI: **1.0.0-beta.16**.
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

Runtime-сборка: `Veilward.asmdef`. Сейчас единственное runtime-поведение — `BootstrapLoader`, который загружает `MainMenu`.

## Domain Foundation

Чистая C#-сборка `Veilward.Domain.asmdef` имеет `noEngineReferences: true` и не зависит от `UnityEngine`, Scene или `MonoBehaviour`. В ней находятся `BattleState`, `BattlePhase`, `EnemyDefinition`, `EnemyState`, `EnemyDefinitionId`, `EnemyId`, `GridCoordinate`, `PathDefinition`, `IBattleSimulation` и `SimulationTiming` с фиксированной частотой **30 Hz**.

Реализованы только неизменяемые модели, проверки инвариантов и минимальный контракт симуляции. Spawn, движение врагов по фиксированному маршруту, health/leaks и детерминированный сценарий волны оставлены на **P1.1**. Волны, экономика, урон, стихии и другие игровые системы в P0.2 не реализуются.

Порядок сцен в Build Settings:

1. `Assets/_Game/Scenes/Bootstrap.unity`
2. `Assets/_Game/Scenes/MainMenu.unity`
3. `Assets/_Game/Scenes/Battle.unity`

`Battle` намеренно пустая. `MainMenu` содержит только стандартные объекты новой сцены; UI и gameplay ещё не реализованы.

## Настройки репозитория

- Asset Serialization: **Force Text**.
- Version Control: **Visible Meta Files**.
- Unity `.meta` файлы коммитятся вместе с assets.
- `Library`, `Temp`, `Logs`, `UserSettings`, результаты тестов и сборки исключены через `.gitignore`.

## EditMode smoke tests

Тесты проверяют наличие и порядок сцен, сохранённые iOS Player Settings (IL2CPP, ARM64, минимальная iOS 17.0), landscape orientation, Force Text и Visible Meta Files. Проверка использует `NamedBuildTarget.iOS` и не зависит от локального `EditorUserBuildSettings.activeBuildTarget`, поэтому suite воспроизводимо запускается после чистого клонирования и при активной Standalone-платформе.

При открытом Unity Editor с Pipeline package тесты выполняются в нём. Runner принимает только полный успех: `Total > 0`, `Passed == Total`, а `Failed`, `Inconclusive` и `Skipped` равны нулю. Некорректный JSON и ненулевой exit code Unity CLI приводят к ошибке запуска.

Без доступного Editor скрипт запускает Unity в batch mode с `-buildTarget iOS` и создаёт JUnit report.

```bash
./Scripts/run-edit-mode-tests.sh
```

### CI

Используйте macOS runner с установленным Unity **6000.3.28f1**, iOS Build Support, Unity CLI **1.0.0-beta.16** и активной лицензией. Выполните ту же команду:

```bash
./Scripts/run-edit-mode-tests.sh
```

При headless-запуске опубликуйте `TestResults/EditMode.xml` как test artifact. Каталог `TestResults` не коммитится. Не запускайте второй Editor для того же проекта, пока проект открыт в другом Editor.

## iOS build

Unity создаёт Xcode project для iOS. Apple signing, provisioning и установка на устройство выполняются владельцем проекта; приватные credentials в репозиторий не добавляются.

На этапе P0.1 физическое iOS-устройство, Xcode signing и фактическая iOS-сборка **не проверялись**.
