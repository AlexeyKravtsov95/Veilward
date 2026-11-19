# Balance decision — HP600/armor5, content v2

Владелец 19.11.2026 делегировал численные решения по игровому балансу в #33/#40. Выбран **HP600/armor5** для нового `eight-wave-ability-boss-v2`. Это локальная HP-правка новой миссии. Speed1, leak20, reward50, bonus20, spawn330/11Shade, ability120/radius1.5/cooldown600 CombatSteps, towers/statuses/reactions/цены/refund/Gold/HP базы не меняются. REG214 сохранён. Гипотезы/лимиты опубликованы [до экспериментов](specification.md).

## Первая матрица: четыре исходных revision2 scripts

Каждый outcome получен целиком от нового 150Gold/20HP после задания immutable candidate definition. Не late checkpoint и не edits live/DTO. Четыре positive cells и ability kills положительны у всех 16 outcomes; Victory требуется в том же run.

| Script | HP1000 | HP700 | HP650 | HP600 |
|---|---:|---:|---:|---|
|FLAME→TIDE→GALE|Defeat/boss376|Defeat/76|Defeat/26|Victory|
|FLAME→GALE→TIDE|Defeat/434|Defeat/134|Defeat/84|Defeat/34|
|GALE→TIDE→FLAME|Defeat/376|Defeat/76|Defeat/26|Victory|
|GALE→FLAME→TIDE|Defeat/369|Defeat/69|Defeat/19|Victory|

**600 — самый высокий успешный кандидат этой матрицы**, с qualifying route каждого старта. Это не утверждение максимального HP во всём пространстве стратегий. Контроли 1000/5, прежние scripts/reports/traces/evidence сохранены. [Все 16 full reports/traces](experiments/matrix/), [машинный анализ всех исходов/экономики/timing](balance-analysis.json).

## Игровой результат и экономика

На исходных FTG/GTF при 600 boss убит на relative785/751 после spawn330:15.17/14.03 секунды жизни. Оба routes выигрывают при **каждом** сдвиге единственного boss cast −30/−15/+15/+30ticks с прежним центром. Третий победный GFT менее устойчив: поздние+15/+30 проигрывают. FGT остаётся проигрышной сборкой даже при 600. Поэтому 600 расширяет жизнеспособность F/T/G, сохраняя цену решений, а не гарантирует любую Victory.

FTG держит два FlameL1 + TideL1 + GaleL1 + SteamL1 + StormL1 (investment500). GTF после положительного Storm intrinsic продаёт Storm и завершает с GaleL1/TideL1/тремя FlameL1/SteamL1 (investment460). Не требуется держать все типы после получения cells. В финальных integration routes перед 8 HP16/18, Gold11/19; после Victory HP16/18, Gold136/144. Общая максимальная валовая экономика до 8 остаётся 570; реальные потери на пропусках Shade и Sell объясняются receipts/accounting. База переживает ранние ошибки, boss leak20 всё ещё смертелен.

L2/L3 не объявлены бесполезными: revision2 исследовал ранние upgrades, но bounded heuristic/beam не доказывает общий optimum. Новая HP-матрица проверяет четыре фиксированные L1-сборки; менять глобальную цену/upgrades на этих данных необоснованно. Сохранённые Stone smoke при 600 тоже L1, но четыре Stone/Crack и другие unlocks дают более сильную специализированную защиту: boss kill relative531/600,6.70/9.00s после spawn, HP10/14, Gold135/141. Это быстрее coverage routes, с меньшим остатком базы из-за раннего prefix. Stone smoke отдельно, за REG214 их counters не засчитываются.

## Маршруты полной matrix и чувствительность

Исходные FTG/GTF выигрывали, но cast8 бил только boss: не было требуемого ability kill при живом Combat8 для rollback. В пределах опубликованных 16+8 suffix/prefix-cast вариантов найдено решение без изменения армий: убрать cast6 FLAME / cast5 GALE; cast7 после первого Step; cast8 по Shade при готовности и затем по boss. Два финальных generated scripts перепройдены обычным public NewGame observer. Нового material search не было.

| Full integration route | HP/Gold после 7 | Wave8 casts relative | Boss origins Direct/Burn/Reaction/Ability | Boss kill | Запас до matched leak |
|---|---|---|---|---|---|
|FLAME→TIDE→GALE|16/79|169,769|421/23/41/115|784|38ticks /1.27s|
|GALE→TIDE→FLAME|18/80|196,796|451/26/53/70|796|26ticks /0.87s|

Запас измерен в отдельном полном контрольном run с тем же prefix/первым cast8 и пропущенным последним boss cast (ранняя попытка попадает в cooldown и не исполняется). До точки последнего cast весь trace совпадает; измеренный boss leak relative822, не оценка через speed без Wet. Boss живёт 15.13/15.53s. FLAME финально kills64/abilityKills12, cells Applied561/72/36/43; GALE kills66/abilityKills14, cells118/51/91/108. Ability Applied1157/1147 соответственно; boss kill и все 4positive cells в каждом том же Victory.

Эти double-cast маршруты чувствительнее к вводу, чем исходные single-boss-cast routes. Второй cast поставлен ровно на readiness. Сдвиг boss cast −15/−30 пропускает команду на cooldown: оба Defeat (boss103/69). +15 даёт Victory обоих; +30 FLAMEVictory, GALEDefeat/leak69 до позднего cast. Сдвиг раннего cast8 вперёд на 15/30 нарушает готовность второго: оба Defeat; назад — первый cast неисполнен, но поздний boss cast сохраняет Victory. Это результаты фактического исполнения/неисполнения заданного script, не обещание победы при любой ошибке. Все 8 spatial probes boss center±0.5 по отдельным осям дают Victory. [Timing](experiments/winning-timing/) · [center](experiments/winning-center/) · [устойчивые исходные scripts](experiments/timing/).

**Вывод игрового анализа:**600 достаточно для двух различающихся F/T/G-сборок с обычной экономикой, сохраняет смертельный leak и ценность ручного cast. Имеется более свободный single-boss-cast путь; более сложный double-cast script нужен для стресс-проверки rollback. Дополнительное снижение HP ради удобства автоматизированного rollback не требуется. Human G2/настоящий touch и восприятие сложности остаются отдельной приёмкой.

## Versioning и leak controls

Новый wrapper/config/builder/default MainMenu/Battle используют v2. Schema4 не меняется. Новый requestedv2 принимает старые v1/native/all-open для Continue через matching factory, сохраняет исходный ID при Save. Обратный v2→requestedv1 запрещён. V1asset1000/5, native и shareddata побайтно прежние. Dirty restore требует exact live contentID; нельзя восстановить v1 DTO в v2 simulation и незаметно сменить правила. Четыре режима проверены во всех 12ordered mixed-slot парах с no-writeLoad/backup/corrupt fallback.

Прежняя Stone/no-cast защита при 600 может сама убить boss; её нельзя назвать Defeat. Новый legal leak control использует настоящий FLAME prefix150/20, живую Preparation8 HP16, законные Sell всех 6tower и отсутствие cast8. Одиннадцать Shade уменьшают HP до 5, bossleak20 clamp0:Defeat, boss600HP, rewardsboss0. Отдельный исторический v1 no-cast control по-прежнему Defeat/boss276HP. [Inputs](inputs/) · [controls](controls/) · [historical control](experiments/historical-leak.json).
