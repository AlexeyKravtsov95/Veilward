# #40 / PR70 — один целевой конечный pass

Основание: раздел «Актуальная доработка — GPT Sol6.1/High» issue40, reviewed head `0e3a755ca72cef9ad56859330c0502704b0d93f9`. Дата19.11.2026. Продолжается та же ветка/PR70; никакого нового feature PR. Scope: новый test-only search, его fixed replays/evidence и docs. Existing trace/file/menu/rollback реализации, проигрывающие controls и весь historical evidence сохраняются.

## Неизменный acceptance

REG214: для каждого FLAME/GALE старта один natural Victory8 от150Gold/20HP; в том же матче ability AppliedHpLoss>0 и ability kill, boss kill1, все четыре positive Steam/Storm × StatusPair/Intrinsic AppliedHpLoss. Дубли/stale/отказы не дают ресурсов. Assertions не skip/Explicit/ослаблять. Production1000HP/armor5, ability, towers/цены/экономика/spawns/runtime/packages не менять.

## Бюджет и остановка до реализации

Один opt-in запуск `P24F_TARGETED_SEARCH=1`. Лимит **50000 evaluated outcomes на старт /100000 суммарно**, **600s wall/start**. `waveOutcomes` и `fullNewGameOutcomes` считаются отдельно, их сумма ограничена; генерация виртуальных планов и отказы до Start не считаются outcomes и имеют отдельные counters. Зарезервировать до4полных replay на старт внутри лимита. Остановка на первом подтверждённом REG214 каждого старта; до ranking сохранять каждую Victory, включая неполный coverage. После исчерпания budget/wall второго pass не выполнять.

Wave1–7: бюджеты на ordered history300/1000/1800/2600/2400/2400/4500 wave outcomes, затем remaining budget финальной wave8, round-robin между двумя histories/сохранёнными parents. Оба порядка проверяются по префиксам до финальных ветвлений: FLAME→TIDE→GALE и FLAME→GALE→TIDE; GALE→TIDE→FLAME и GALE→FLAME→TIDE. Неиспользованная квота идёт в финальную стадию, общий cap неизменен. Обычная regression suite использует только короткий smoke с отдельным малым budget; полная исследовательская работа — opt-in.

## Поиск и pruning

- Public production factory/New Game/Choose/Place/Upgrade/Sell/Start/Next/Cast/Step. Clone только exact DeepCopy реально достигнутого safe checkpoint; не менять поля DTO/live. Полный prefix command/cast/history хранится у каждого retained node.
- Branches до wave7 включают Hold, разные позиции/число башен, L2/L3 разных архетипов, экономию Gold, Sell одной/нескольких башен и последующий rebuild. Каталог ограничен фактическими native unlocks. После всех4positive cells нет требования сохранять5типов.
- Для7/8: subsets retained army, разные новые типы/levels/количество, upgrades retained towers, Hold/Sell+rebuild. Предварительное armour-aware damage/exposure — только heuristic для приоритета, результат всегда настоящая simulation. Новая virtual army используется только для планирования публичных commands, не для записи в checkpoint.
- Сохранение beam разнообразия: HP, Gold/investment/refund wealth, earned proc cells, армия/levels, remaining cooldown. Не выбирать всё только по bossHP. Bounded material/layout candidates, ранние и финальные ability policies различны: cluster threshold/hold7/early8/boss delay/center policy. Все timestamps захватываются **до** lethal completion.
- Exact command plan + parent safe-state + cast policy dedup **до evaluation**. Отдельно публикуются generated/dedup/illegal/pruning/retained/evaluated/steps/wall counters. Неоценённые branch не выдаются за проверенные.
- Лучший fully covered terminal candidate каждого ordered history и каждая Victory сохраняются с commands/casts/receipts/real costs/armies/waves/boss damage; retained parents и suffix causes позволяют восстановить происхождение. Fixed full-NewGame replay этих inputs проверяет тот же результат и все positive cells через прежний observer.

## Затем

Если winning inputs найдены: сохранить прежние controls/history и прогнать новые через всю existing double trace/file/menu/fault/Combat7–8 dirty+fresh/prefix/continued/PlayMode/Pause matrix, full static/Edit/Play/graphical, actual screenshots/Console и independent review. Только при полном acceptance Ready for Review/Closes40. Иначе draft/Refs40/BLOCKED с конкретными результатами каждого порядка и следующим необходимым решением, без заявления общей непроходимости.

Уточнение старого evidence: в `AbilityBossFileTests` fresh snapshot создаётся из **in-memory safe snapshot**; реальное LoadReadOnly отдельно используется для dirty restore и full file-continued suite. Предыдущее описание «fresh file-restored» было неточным. Можно оставить код и точно разделять эти проверки; эта revision не выдаёт in-memory clone за чтение файла.
