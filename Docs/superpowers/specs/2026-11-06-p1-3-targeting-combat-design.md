# P1.3 — Targeting & Combat: технический дизайн

## Scope

Добавить в существующий Battle одну боевую механику: Test Tower автоматически выбирает First-цель в радиусе, атакует hitscan с фиксированным cooldown, наносит целочисленный урон и уничтожает врагов. Presentation показывает короткий beam, health bars, Kills и Range при выборе клетки. Награды, экономика убийств, другие режимы targeting, projectiles, стихии, новые волны и зависимости не входят в задачу.

## Рассмотренные варианты

1. **Целочисленный урон, `NextAttackTick`, явно очищаемый список событий — выбран.** Убирает неоднозначное округление HP, даёт точные границы cooldown и не теряет события при нескольких fixed steps в одном кадре. Списки событий и врагов переиспользуются.
2. Float-урон с округлением при каждом попадании и countdown с вычитанием. Сохраняет текущий тип `Damage`, но добавляет правила округления HP и накопление погрешностей/неоднозначность границ cooldown.
3. Отдельная event bus и projectile-сущности. Избыточны для одной hitscan-атаки и нарушают ограничение узкого PR.

## Domain

`TowerDefinition.Damage` становится положительным `int`. `AttackIntervalTicks` вычисляется один раз в конструкторе: `max(1, round(30 / FireRate))`, округление к ближайшему тику с `MidpointRounding.AwayFromZero`; результат ограничен сверху `int.MaxValue` для корректных экстремально малых положительных значений. Максимальная частота — 30 атак/секунда, одна атака башни за тик.

`TowerState` хранит `NextAttackTick`, изначально 0. Башня, готовая атаковать, но не нашедшая цель, не получает cooldown и может атаковать на следующем подходящем тике. После выстрела `NextAttackTick = текущий Tick + AttackIntervalTicks`. Повторная атака на одном тике невозможна.

`EnemyTargeting.SelectFirst` получает центр башни, Range, read-only список врагов и `PathDefinition`. Он не создаёт коллекций: проверяет `IsAlive`, squared distance в плоскости поля с включённой границей Range, выбирает максимальный `DistanceAlongPath`, при равенстве — минимальный `EnemyId`. Изолированный метод оставляет место для будущих Last/Strongest/Weakest без реализации этих режимов сейчас.

`EnemyState.Health` имеет private setter. Внутренний `ApplyDamage(int)` принимает только положительный урон, не меняет мёртвую цель, ограничивает HP нулём и сообщает, был ли переход в смерть. `IsAlive` эквивалентно `Health > 0`.

`BattleState.Kills` увеличивается только при удалении нового погибшего врага. После атак мёртвые враги удаляются обратным обходом списка; порядок остальных ID сохраняется. Leaked enemies удаляются раньше атак и не могут быть выбраны. Погибшие не участвуют в leaks и не умирают повторно.

## Порядок симуляции

`StartWave()` по-прежнему только переводит Preparation в Combat и выполняет due spawn на tick 0; атаки начинаются в `Step()`.

Каждый разрешённый `Step()` выполняет строго:

1. Advance Tick.
2. Spawn due enemies.
3. Enemy Movement и Leaks в стабильном порядке списка.
4. Tower Targeting и Attacks по возрастанию `TowerId` (список строительства уже хранится в этом порядке).
5. Enemy Death и удаление погибших обратным обходом.
6. Проверка Defeat, затем WaveComplete.

Preparation, Defeat и WaveComplete не выполняют шаг и не создают атак. Defeat остаётся приоритетнее WaveComplete.

## Combat events

`AttackEvent` — неизменяемый struct с `TowerId`, `EnemyId`, `Tick`, целочисленным `Damage` и `WasLethal`. `Damage` — конфигурированный урон попадания, а не усечённый остаток HP; `WasLethal` отмечает единственный переход цели к нулю HP.

`BattleSimulation` владеет одним reused list и read-only `AttackEvents`. Presentation обрабатывает события сразу после каждого `StartWave`/`Step`, затем вызывает `ClearAttackEvents()`. Симуляция не очищает необработанные события сама в начале следующего тика, поэтому несколько fixed steps в кадре не перезаписывают друг друга. Универсальная event bus не создаётся.

## Configuration

`BattleScenarioConfig.towerDamage` становится serialized int; существующий GUID и asset сохраняются. Для наблюдаемого P1.3 graybox Test Tower документированно настраивается Range 3, FireRate 2, Damage 25 при HP Shade 100: четыре попадания убивают одного врага за две секунды. Маршрут, пять Shade, spawn schedule, цена и золото не меняются. Без башен итог остаётся Base Health 15.

## Presentation

`BattleSceneController` после каждого fixed step читает и очищает боевые события. Для события создаётся короткий world-space `LineRenderer` от башни к зафиксированной позиции цели; Presentation не пересчитывает попадание или урон. Beam уничтожается по короткому визуальному таймеру и не влияет на Domain.

При создании enemy view добавляются два простых quad health bar с общими serialized materials. Fill обновляется из `Health / MaxHealth`, исчезает вместе с уничтоженным enemy view. HUD добавляет `Kills`.

`BattlePlacementController` получает один заранее созданный `LineRenderer` Range Preview. Первый tap показывает окружность с текущим Range вокруг выбранной клетки; Confirm/Cancel/Start Wave скрывают её. Правила ввода и покупки не меняются.

`BattleSceneBuilder` обновляет только graybox Battle, материалы, HUD и ссылки контроллеров. Все GUID существующих assets сохраняются; новые зависимости не добавляются.

## Tests и verification

EditMode покрывает First, tie по EnemyId, Range и включённую границу, отсутствие цели, округление FireRate, точные тики cooldown, одну атаку за тик, стабильный порядок нескольких башен, урон и clamp HP, однократную смерть/Kills, отсутствие leak после смерти, запрет атак вне Combat, поля событий и детерминизм. Старые P1.1/P1.2 suites запускаются полностью; тесты конфигурации обновляются только для смены Damage на int.

Обязательная проверка: `./Scripts/run-edit-mode-tests.sh`, 0 compilation errors, реальный Play Mode без башен до Base Health 15 и с башнями до хотя бы одного kill, Game View screenshots с Range/beam/health bars/итогом, `git diff --check`, отсутствие `UnityEngine` в Domain и проверка `.meta`. iOS export без signing выполняется при доступном модуле; физический iPhone не заявляется как проверенный без device-запуска.

## Acceptance

- First выбирает максимальный прогресс только среди живых врагов в Range; tie разрешается минимальным EnemyId.
- Hitscan и cooldown полностью принадлежат Domain, работают на 30 Hz и не атакуют вне Combat.
- HP не отрицательно, смерть/Kills происходят один раз, мёртвый враг не наносит LeakDamage; следующая башня выбирает другую живую цель в том же тике.
- События содержат TowerId, EnemyId, Tick, Damage и WasLethal и имеют явный lifecycle.
- Play Mode показывает beam, health bars, Kills и Range; безбашенный сценарий завершается с 15 Base Health, с башней наблюдается kill.
- Все тесты и diff checks проходят; документация и evidence включены в PR, merge не выполняется.
