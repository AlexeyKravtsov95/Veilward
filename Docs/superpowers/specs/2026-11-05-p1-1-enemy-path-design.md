# P1.1 — Enemy Path & Movement: дизайн

## Scope и критерии приёмки

Реализуется одна детерминированная тестовая волна: пять Shade, spawn с интервалом 30 тиков, 100 HP, скорость 1.5 единицы/с, leak damage 1, здоровье базы 20. Враги движутся по авторскому маршруту с частотой симуляции 30 Hz; достижение финиша удаляет врага и однократно наносит урон базе. Добавляются graybox Battle, ортографическая камера, spawn/base markers и HUD со здоровьем, числом активных врагов и тиком.

Готово, когда новые и существующие EditMode-тесты проходят, Battle проверен в Play Mode, пять врагов видно на маршруте и у финиша уменьшается здоровье, Console не содержит compile errors, `.meta` и GUID сохранены, `git diff --check` проходит. Башни, атаки, экономика, стихии, полноценная кампания и новые пакеты не входят в изменения.

## Domain

`BattleSimulation` реализует `IBattleSimulation` и владеет одним изменяемым `BattleState`. Изменяемость состояния ограничена сборкой Domain: внешние потребители получают только getters и `IReadOnlyList<EnemyState>`. Это исключает создание snapshot, списка и объектов врагов на каждом тике; новые `EnemyState` создаются только при spawn.

`PathDefinition` один раз вычисляет длины и cumulative boundaries сегментов. `GetPosition(distance)` возвращает чистый C# value type с координатами, ограничивает distance пределами маршрута и линейно интерполирует соответствующий сегмент. `TotalLength` используется симуляцией для leak. Runtime path search, NavMesh и физика не применяются.

`WaveDefinition` описывает один тип врага, count, first spawn tick и interval. Значения передаются в `BattleSimulation` извне и не зашиты в алгоритм. Первый spawn выполняется `StartWave()` на тике 0; остальные — на тиках 30, 60, 90 и 120.

Порядок `Step()` в Combat:

1. Увеличить тик на 1.
2. Создать все враги, schedule которых наступил, в порядке расписания и с монотонными ID.
3. Обойти активных врагов в порядке возрастания ID, увеличить `DistanceAlongPath` на `Speed / 30`.
4. При достижении `TotalLength` удалить врага и применить `LeakDamage` один раз; здоровье ограничить нулём.
5. Если здоровье равно нулю, установить `Defeat`; иначе, когда расписание исчерпано и активных врагов нет, установить `Victory` как завершение только тестовой волны.

`StartWave()` работает только из `Preparation`; повторный вызов не создаёт врагов. `Step()` вне `Combat` ничего не меняет, поэтому после `Defeat` и `Victory` spawn и движение прекращаются. Если несколько врагов финишируют на одном тике, они обрабатываются по ID; `Defeat` имеет приоритет над завершением волны.

## Конфигурация и Unity Presentation

`BattleScenarioConfig` — `ScriptableObject` в Runtime/Presentation слое с serialized base health, enemy stats, spawn count/interval и waypoint array. Он создаёт Domain definitions и не переносит `UnityEngine` в Domain. Asset `TestWave.asset` содержит параметры тикета.

`BattleSceneController` создаёт симуляцию при запуске Battle и вызывает `StartWave()`. В `Update()` накапливается render time только для вызова целого числа `Step()` на 30 Hz; Domain не читает `Update`, `Time.deltaTime` или frame time. Контроллер синхронизирует dictionary enemy ID → view, устанавливает Transform по `PathDefinition.GetPosition(currentDistance)` и удаляет отсутствующие во state views. Для удаления используется переиспользуемый список ID; правил движения и leaks в Presentation нет.

Battle содержит ground, визуальные сегменты пути, отдельные spawn/base markers, примитивного enemy, directional light и orthographic camera с изометрическим углом. uGUI HUD только читает `BattleState`. Scene и материалы создаются Editor-скриптом, затем сцена реально открывается и проверяется в Unity Play Mode; MainMenu не меняется.

## Ошибки и инварианты

Конструкторы отклоняют нулевые/отрицательные count и interval, некорректные stats и слишком короткий path через существующие проверки моделей. SO `OnValidate` ограничивает значения в Inspector. Отсутствующий config или enemy prefab вызывает явную ошибку при запуске сцены, а не скрытый fallback. Повторный spawn, leak и terminal `Step()` не изменяют здоровье повторно. Здоровье и валюта не становятся отрицательными; награды и экономика не добавляются.

## Тесты и проверка

EditMode tests покрывают spawn ticks, стабильные ID, расстояние за N тиков, поворот и clamp path position, одинаковый результат двух симуляций, однократный leak, отсутствие повторного урона, clamp здоровья к нулю, `Defeat`, запрет spawn после terminal state и завершение тестовой волны. Существующие P0.1/P0.2 tests сохраняются.

Проверки: `./Scripts/run-edit-mode-tests.sh`, Unity compile/Console, запуск Battle в Play Mode не менее чем до нескольких spawn и leaks, screenshot Game View, проверка `.meta`, changed-file scope и `git diff --check`. Physical iPhone testing в P1.1 не выполняется и не заявляется.

## Риски

- Накопление render accumulator может отставать при сильной просадке; для graybox ограничивается максимальное число тиков за кадр без изменения Domain step.
- Float distance воспроизводим при одинаковых входных данных и runtime, но не является кросс-платформенным fixed-point replay контрактом.
- Визуальная проверка ограничена Editor Game View; читаемость на iPhone требует отдельного device-теста.
