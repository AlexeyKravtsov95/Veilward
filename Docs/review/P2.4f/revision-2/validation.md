# PR70 / #40 — целевой pass завершён, REG214 BLOCKED

**Один** новый pass после reviewed head `0e3a755`. [Спецификация до реализации](specification.md), research source `497ac70`, suite source `f25b6f3`. Продолжение того же draft PR70 / Refs #40. Баланс **1000HP/armor5**, ability120/1.5/600, цены/экономика/spawns/runtime/packages и REG214 неизменны. Прежняя trace/file/menu/rollback matrix и её inputs сохранены побайтно.

## BLOCKED — побед нет

Лимит100000outcomes и600s/start соблюдён. Получено **99992 wave-only outcomes +4 полных New Game replay =99996 outcomes**, **Victory0 / eligible0**. По49996wave outcomes и2full replay на старт; оставшиеся4слота были зарезервированы для дополнительных confirmation replay и не понадобились. Время FLAME41.50s, GALE40.70s; остановка по wave-evaluation budget, не по wall timeout. Второй длинный pass не запускался. **99992 — не число полных забегов.**

| Ordered history | Лучший fully covered boss HP перед leak | Applied по boss Direct/Burn/Reaction/Ability | Ability kills / total applied | Steam pair/intrinsic | Storm pair/intrinsic |
|---|---:|---|---|---|---|
|FLAME→TIDE→GALE|**376**|435/27/47/115|14/1351|542/54|20/66|
|FLAME→GALE→TIDE|**434**|374/28/49/115|13/1357|267/36|45/60|
|GALE→TIDE→FLAME|**376**|442/25/42/115|15/1360|128/46|122/54|
|GALE→FLAME→TIDE|**369**|429/29/58/115|13/1330|76/170|12/31|

У всех **natural Defeat8**, boss kills0 и boss leak1; все четыре reaction cells и ability kills положительны **в том же прохождении**. Ни один из этих reports не объявляется REG214 PASS. [Точные inputs](inputs/), [full traces/HP-Gold accounting/per-wave snapshots](full-replays/), [per-wave CSV](per-wave.csv). Каждый candidate перепройден от public production New Game150Gold/20HP, без поздней выдачи армии; отдельные fixed regression replays4/4 повторяют полные reports/hash. Existing mandatory Victory assertions обоих стартов остаются FAIL; ни skip/Explicit, ни ослабления acceptance нет.

## Чем этот pass отличается от прежнего

Prefix search выполняет оба ordered history каждого старта, а не только TIDE-first. Wave clones — exact DeepCopy фактически достигнутых Preparation через public factory/commands; planner не пишет HP/Gold/IDs/progression/cooldown в DTO/live state. Retained parents содержат реальные safe state и полный command/cast prefix. Branch receipts фиксируют до/после Gold, price/refund, actual TowerId и failure reason.

Hold/Place/Upgrade/Sell/rebuild, L2/L3, сохранение Gold, перестановки/разные типы и уровни финальной армии. Публичные calls исполняют построенный virtual plan; virtual army используется только для heuristic. Возможный урон с учётом armor/Gust/Wet/exposure задаёт приоритет, результат даёт только настоящая simulation. Beam32/history сохраняет здоровье, wealth, earned cells, состав/levels и cooldown. Материалы interleaved по score/profile/reserve; для material/layout пробуются две разные cast policies до переключения. Exact duplicate plans **16301** отклонён до evaluation; invalid suffixes0. Все Victory сохранялись бы до ranking/pruning, фактически их нет.

| History | Wave-only outcomes | Fully covered final outcomes | Outcomes с L2 / L3 | Самый ранний L3 | Лучший boss HP при max final level2 /3 |
|---|---:|---:|---|---|---|
|FLAME→TIDE→GALE|25146|5734|5309/42|TIDE,6|419/650|
|FLAME→GALE→TIDE|24850|3743|5234/42|FLAME,6; TIDE,8|440/не получен fully covered L3 final|
|GALE→TIDE→FLAME|25120|5752|5287/82|TIDE,6|399/582|
|GALE→FLAME→TIDE|24876|5422|5541/56|GALE и FLAME,6; TIDE,8|375/596|

L2/L3 outcome counts могут пересекаться: одна армия может содержать оба уровня. Это измеренные оценки, не все возможные upgrades всех типов. Новые лучшие финальные армии действительно различны: F→G→T продаёт Steam после полученного intrinsic, G→T→F продаёт Storm; финальные покупки не фиксируются на SteamL1. Все лучшие здесь имеют L1; это результат сравнения, а не ограничение генератора. [Все counts/earliest levels/command types/final profiles](search-analysis.json).

Пределы пространства честно открыты: top80+profile64+reserve12 materials, beam32, army≤8, новых units≤4,12layouts,16prefix/24final policy variants, приоритетный bounded subset combinations. Не перебраны все наборы/порядки команд/координаты/центры и mixed multi-upgrade sequences. В diversity profile HP группируется по4, cooldown по150ticks, proc priority по числу положительных cells; distinct bit masks с одинаковым count не гарантированно сохраняются как отдельные profiles. Нет утверждения общей непроходимости.

## Prefix, экономика и следующее необходимое решение

Лучшие F→T→G и F→G→T проходят waves1–7 сHP16; G→T→F и G→F→T — сHP18. Первые три волны и экономика сохранены в [CSV](per-wave.csv), actual commands и сохранённые safe snapshots — рядом с inputs. Максимальный наблюдённый wealth (Gold+investment) после7: **550/545/560/560** соответственно; эти maxima могут принадлежать разным suffix и не являются гарантированным liquid Gold.

В лучших fully covered attempts недостаёт **369–434 actual HP loss по boss**, при одной ability hit115. Повышение уровня само по себе не стало победой: измеренные L2/L3 families приведены выше, а high-level hybrid armies ограничены экономикой. Например, один final SteamL3 или StormL3 после всех4cells до8 требует минимум **591gold**:495invested в гибрид + потери от продажи остальных обязательных базовых типов и второго гибрида. Максимум законно заработанного до Preparation8 —150+7×20+56×5=**570**. Это узкая нижняя граница **данной** final-army family, не доказательство непроходимости F/T/G вообще.

Следующее решение владельца: оставить #40 BLOCKED, отдельно согласовать дальнейший узкий strategy scope (например, prefix1–3/точный cooldown вместо coarse profiles) либо разрешить **согласованный balance experiment для F/T/G/boss** с1000/5 как контрольным вариантом и неизменным REG214. Конкретный вход в такое решение — текущие four-history scripts, prefix economy и измеренный deficit369–434HP; ни один непроверенный параметр не выдаётся за исправление. Этот PR не меняет баланс и не продолжает поиск сверх оговорённого pass.

## VERIFIED — сохранение matrix и quality gates

| Gate | Фактический итог |
|---|---|
| Targeted long pass |2/2 research tests завершены; **не acceptance PASS**, Victory0 |
| Fixed best full-NewGame controls |4/4 PASS, фактические Defeat повторены |
| Static |**38/38 PASS**|
| Полный EditMode |**1075passed /1077total,2failed REG214 — FAIL**, skipped/inconclusive0|
| Полный PlayMode |**60/60 PASS**, failed/skipped/inconclusive0|
| Graphical |**5/5 PASS**,108реальных PNG, failed/skipped/inconclusive0|
| Diff/meta/GUID/packages/version/compile/builder/reproducibility |проверены полными suites/static и diff --check|

Existing double traces, whole file-continued comparison, pending/confirmed start/3/6, faults/Retry, Combat7/8 prefixes, dirty/fresh/continued и real menu/clean presentation/Pause **сохранены и повторены на прежних controls**. Новые four-history best scripts имеют full Domain replays, но не выдаются за winning file/menu matrix: winners не найдены. Отдельные Stone Victory и legal boss-leak controls остаются.

**Коррекция небольшого описания прошлой revision:** в существующем FileTests fresh instance получает in-memory safe snapshot; dirty restore и whole file-continued suite используют реальный LoadReadOnly. Код matrix не менялся. Новый report точно разделяет эти проверки; прежнее «fresh file-restored» в старом evidence/комментарии не является утверждением отдельного disk-load в этой ветке теста.

[Raw XML](reports/), [commands/environment](gates.json), [source/input/report/raw-trace/media hashes](manifest.json), [полный исходный search journal](search.tar.gz), [member hashes](archive-members.json). `analyze-search.py` независимо проверяет receipts/budgets/ID monotonicity/eligibility/Gold/ability cooldown+sequence всех99992wave rows, source DAG parents и оба history/start. Прежний evidence сохраняется в history audit; generated builder/import drift возвращён только в собственной исходно чистой worktree.

## Реальные кадры, review и ограничения

[108 новых PNG](screenshots/) повторяют сохранённые coverage/menu/rollback controls392/462HP и Stone/leak controls, **не изображают новые best376/434/376/369 как Victory**. Telemetry подтверждает продвижение frameCount; actual ScreenCapture/production data/public-command test clock, `Battle.Update` выключен. Representative clean Continue8/interrupted Combat8 просмотрены. Console unexpected0; expected injected IO errors и rendering warnings отдельно в [audit](console-audit.json). Это Editor, не realtime/performance/device evidence.

Независимое [review](review.md) проверяет code/evidence отдельно. **NOT VERIFIED / BLOCKED:** REG214 и winning-route matrix; физический iPhone/IL2CPP/Metal/touch/lifecycle/force quit/performance, human G2/final balance/ART, hosted Unity CI/release/TestFlight. Projects поле не подтверждено. PR остаётся draft/Refs40, без self-merge/release/TestFlight.
