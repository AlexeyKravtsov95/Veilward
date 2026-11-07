# VEILWARD: Spirit Defense

Unity-проект мобильной tower defense игры для iOS. Текущий статус — **P1.3 Targeting & Combat**: детерминированная 30 Hz симуляция одной тестовой волны, fixed-path movement, build grid, установка башен и первая боевая модель First targeting в graybox Battle. Продажа, улучшения, стихии, сохранения, IAP и Game Center ещё не реализованы.

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
```

Static checks не требуют Unity license: Python stdlib tests проверяют сам validator, затем Unity version/revision, frozen direct package baseline и lock, paired/orphan `.meta`, GUID и Assembly Definitions. Текущий локальный результат: **21/21 static self-tests passed**, project checks PASS.

EditMode suite покрывает Domain, regression contract `REG-BATTLE-001…008`, визуальные правила, сохранённую Battle scene, config/prefabs/materials и безопасную воспроизводимость генератора. Текущий локальный результат: **158/158 EditMode tests passed**, 0 failed/inconclusive/skipped. PlayMode smoke загружает Battle и проверяет Preparation: **1/1 PlayMode test passed**, 0 failed/inconclusive/skipped.

Runners принимают только полный успех: `Total > 0`, `Passed == Total`, а `Failed`, `Inconclusive` и `Skipped` равны нулю. XML validator независимо проверяет NUnit/JUnit reports. При открытом проекте без доступного Unity CLI runner завершается кодом 2 и не запускает второй Editor. В clean checkout используется `UNITY_EDITOR_PATH` либо стандартный Unity Hub path; batch mode получает `-buildTarget iOS` и сохраняет report/log в ignored `TestResults`.

GitHub Actions workflow `Quality Gates` всегда выполняет static checks на PR/push в `main`. Hosted Unity EditMode/PlayMode jobs используют GameCI, но сейчас **BLOCKED** до настройки владельцем полного license secret set и проверки manual run. Это не считается Unity CI PASS. Точная процедура включения и ограничения описаны в `Docs/QUALITY_GATES.md`; регрессионный контракт — в `Docs/REGRESSION.md`.

## iOS build

Unity создаёт Xcode project для iOS. Apple signing, provisioning и установка на устройство выполняются владельцем проекта; приватные credentials в репозиторий не добавляются.

После review fixes повторно выполнен Unity iOS **Device SDK export без signing** в открытом Editor: Xcode project создан в ignored `Builds/iOS/P1.3-Review`, результат `Succeeded`, **0 errors / 0 warnings**. Экспортированные IL2CPP sources содержат `Veilward.Domain.cpp`; `Veilward.Editor` и QA в player не входят. Simulator build и `xcodebuild` в этом тикете не запускались.

Play Mode после review fixes проверен с виртуальной мышью Input System. Обычный бой с башней в `(-4,0)`: подтверждённые selection/Confirm/Start Wave, beam, первая смерть с **Kills 1, Base Health 20**, финал **Kills 1, Base Health 16, Active Enemies 0, WaveComplete, tick 399**. Отдельный lethal first-shot с временным QA Damage 100 только в памяти Play Mode: на захваченном кадре противник уже удалён, **Kills 1, Base Health 20, Active Enemies 0**, beam виден в сохранённой точке попадания. После остановки сохранённый asset остался с Damage 25. Console errors — **0**. Game View evidence находится в `Docs/review/P1.3/review-fix-normal/` и `Docs/review/P1.3/review-fix-lethal/`.

**Физический iPhone не проверялся.** Signing, touch на устройстве, safe area, производительность и Simulator для P1.3 также не проверялись.
