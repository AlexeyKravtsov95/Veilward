# #39 / PR69 — R3, Editor acceptance VERIFIED

Выбран **HP1000 / armor5** из фиксированного разрешённого набора. [Решение владельца/границы](owner-authorization.md), [сравнение и обоснование выбора](decision.md), [матрица JSON](candidate-matrix.json) / [CSV](candidate-matrix.csv). Это минимальный подтверждённый прошедший вариант по выбранной оси из разрешённых значений, не математический optimum и не final G2 balance.

**Tested production source `f721e8ba07253e5888310b5b055d51e792088022`.** Последующие commits — docs/evidence. Три runtime/config строки относительно R2: `boss.armor:10→5` в новом wrapper, его validation и builder default. HP1000/speed1/leak20/reward50/bonus20,11Shade0…300+boss330, ability120/1.5/600, башни/цены/экономика/waves1–7/старые factories и saves не менялись. Исходный пользовательский checkout не трогался.

## VERIFIED — полные gates

| Проверка | Фактический итог |
|---|---|
| Static | **38/38 PASS**, meta/GUID/asmdefs/packages/version PASS |
| Полный EditMode | **1048/1048 PASS** |
| Полный PlayMode | **58/58 PASS** |
| Graphical | **3/3 PASS**, **34** новых реальных Play Mode PNG |
| Целевые selected EditMode / PlayMode | **32/32**, **4/4 PASS** |
| Immutable matrix / optional layouts / optional rebuild | **10/10**, **10/10**, **10/10** завершённых research cases |
| Console трёх graphical cases | **0 unexpected errors/exceptions/asserts**, **2 expected injected IO errors**, **16 rendering warnings** |
| Source/scope/history | Все **772** прежних evidence файла unchanged; `git diff --check` PASS |

Во всех raw NUnit reports **failed/inconclusive/skipped0**, nonempty totals. Штатные runners/XML validator вернули PASS. Compile, builder/reproducibility/refs, legacy/final schema4/mixed slots и Combat8 whole rollback включены в полный suite. Полный PlayMode повторён после возврата import/builder serialization drift к committed assets; перед graphical assets также восстановлены. После всех прогонов возвращён только этот drift своей worktree.

[Точные команды/среда/report/config/input/media SHA256](gates.json), [manifest](source-sha256.json), [raw XML](reports/). Unity6000.3.28f1(8f082d2fe476), CLI1.0.1, macOS ARM64, targetiOS Editor; player/device/export не запускались. Первые незавершённые compile/disk-space attempts не засчитаны за тесты. Недостаток места устранён удалением только воспроизводимых кэшей и уже опубликованной generated R2 TestResults копии; исходники и R1/R2 evidence сохранены.

## Матрица разрешённых кандидатов

Каждый вариант создаёт новый immutable EnemyDefinition и wave8 **до New Game**. Никаких live/DTO HP/Gold/progression/cooldown edits. Общий BossMissionScript задаёт те же public commands и Cast ticks/centers;20 route reports содержат старт150/20, choices, покупки/Upgrade/Sell, receipts, полные восемь safe snapshots, per-wave HP/Gold/Kills/cooldown/sequence, boss damage origins/kill/leak. Все первые семь whole snapshots побайтно совпадают между пятью вариантами для каждого route.

| HP / armor | FLAME Stone | GALE Stone | Четыре coverage cells обоих starts | R3 eligible |
|---|---|---|---|---|
|1000/10 control|Defeat, boss154|Defeat, boss68|PASS, coverage outcome Defeat|нет|
|900/10|Defeat, boss54|Victory, kill relative782|PASS, Defeat|нет|
|800/10|Victory, kill645, base10|Victory, kill720, base14|PASS, Defeat|да|
|1000/8|Defeat, boss102|Defeat, boss8|PASS, Defeat|нет|
|1000/5|Victory, kill705, base10|Victory, kill722, base14|PASS, Defeat|**выбран**|

Внутри HP оси900 не прошло; внутри armor оси8 не прошло. Между прошедшими800/10 и1000/5 выбран armor5: сохраняет1000HP, позднее FLAME-убийство705 вместо645 и только44ticks до контрольного leak749; GALE timing722/720 и baseHP10/14 почти одинаковы. F/T/G атаки меньше подавлены armor, что видно в фактическом damage. Подробности и ограничения — [decision](decision.md).

**Уточнение контракта:** первоначальная локальная spec добавляла обязательную Victory именно у дополнительных coverage routes. Независимая сверка точного R3 §2 исправила это избыточное требование: нужны Victory обоих natural starts плюс отдельные четыре positive coverage cells. Coverage Defeat честно публикуется и не является новым R3 blocker; полная #40 matrix отдельно. Исходные mandatory natural Victory assertions не удалены, не skipped/Explicit и не ослаблены.

Дополнительно выполнены два ограниченных F/T/G passes: по3000 layouts на каждую из10пар variant/start, всего **60000 candidate attempts**, без найденной дополнительной coverage Victory. [Все candidates/best scripts/receipts/limits](coverage-search.tar.gz), [raw member SHA256](coverage-search-sha256.json), [counts](search-summary.json). Эти optional passes не являются acceptance PASS. Пространство207360/route не исчерпано; stride7919,12 layout offsets, starter1/2, два ordered choices, thresholds100/200/300, delays0/60/120/240, early8/hold7, upgrades0/3/7, flavor5, sellExtra/cellOffset3. Нет dedup/pruning, совпадающие plans возможны. Rebuild pass выбирает один из трёх sell/Steam/upgrade recipes по index и разрешает его только после реальных четырёх counters до8. Не перебираются все префиксы/позиции/уровни/перестройки/непрерывные cast centers. Это не доказательство невозможности других побед.

## Выбранный production — натуральные outcomes

| Script из150Gold/20HP | Preparation8 HP/Gold | Outcome | Boss | Ability по boss |
|---|---:|---|---|---:|
| FLAME→STONE→GALE |10/10|**Victory8**, tick3755, Gold135, Kills58|killed, relative705|**117**|
| GALE→STONE→TIDE |14/16|**Victory8**, tick3500, Gold141, Kills62|killed, relative722|**117**|
| Отдельный FLAME→STONE→TIDE, no-cast8 |13/2|**boss-leak Defeat8**, tick3956, Gold57, Kills60|живой276HP перед leak20|**0**|

[FLAME input](flame.commands.json) / [receipts](natural/flame-victory.json) / [whole waves](natural/flame-victory.waves.jsonl); [GALE input](gale.commands.json) / [receipts](natural/gale-victory.json) / [waves](natural/gale-victory.waves.jsonl). R2 Stone commands/casts скопированы без подгонки; применена только разрешённая armor5. Ability115 до Crack, фактически117 при Crack2. Natural boss kills выполняются tower/Step; положительная ability по boss — отдельный реальный hit.

[Leak receipts](natural/flame-leak.json) / [waves](natural/flame-leak.waves.jsonl). Используются неизменённые R1 commands и только casts1–7 через Original(); он читает inputs/tick/center, не загружает state. База13 перед8, затем20leak clamp0. Награды за boss50/bonus20 нет. Synthetic20→0 unit boundary остаётся отдельно.

### Дополнительные законные F/T/G coverage routes

[FLAME input](flame-coverage.commands.json) / [полный report](production-coverage/flame.json); [GALE input](gale-coverage.commands.json) / [report](production-coverage/gale.json). Новый production factory, старт150/20, без Stone. У обоих фактический **Defeat8**, boss393/424HP; это не выдано за Victory. R3 требует здесь reproducible counters; двойная trace/file/menu matrix не заявляется.

| AppliedHpLoss за матч | FLAME | GALE |
|---|---:|---:|
| Steam / StatusPair |418|174|
| Steam / Intrinsic |234|72|
| Storm / StatusPair |84|21|
| Storm / Intrinsic |12|276|

DamageEvents связаны с ReactionEvent по Tick+Kind+ReactionSequenceInTick; считаются только фактические positive HP loss после clamp, не число visual events или nominal power. Первоначальные P2.3e.1 coverage routes также проверены во всех5кандидатах; их outputs сохранены отдельно в matrix.

## Victory persistence / IO / Retry

`BossVictoryPersistenceTests`: оба production New Game целиком до real boss kill/Victory; whole Export/dirty Combat8 + fresh Restore, пустые transient streams/enemies и frozen terminal commands. Два Load primary/backup без записи, целый schema4/native/enabled snapshot.

Три fault boundary × оба starts: temp write, invalid read-back и commit failure. Непосредственно natural lethal tower/Step даёт **один kill +50 и bonus+20 = delta70**; completion save fails, live whole snapshot и durable primary/backup сохраняются. Повтор Observe не повторяет IO/command, два Retry сохраняют тот же snapshot без изменения Gold/Kills/tick/cooldown/sequence. Terminal Step/Start/Next/Cast не дают ресурсов. [Шесть IO audits и snapshots](persistence/).

PlayMode идёт настоящим MainMenu New Game/Continue8 и Buttons. Fault directory создаётся **после successful saved Start8**. Real boss kill/Victory сохраняется live при IO failure; настоящая Retry Save Button успешно записывает whole Victory. Два file Load не меняют primary/backup; Restore очищает targeting/ownership/inspector/effects/streams. Реальный terminal MainMenu отключает Continue и PrepareContinueSession возвращаетfalse.

Эти natural tests покрывают completion через tower/Step. Отдельный natural lethal-Cast completion заново не построен; существующие synthetic Cast controller tests и все lethal-origin unit boundaries остались зелёными. Они не называются natural evidence.

## Реальные финальные Play Mode кадры

Все34 PNG — ScreenCapture из graphical Editor на tested production source. Telemetry содержит реальные frameCount/wave/tick/HP/Gold/Kills. Battle.Update выключен для ускоренного synchronous public-command Step clock, что написано на кадрах. Накопленные beams/VFX не являются realtime/performance доказательством; clean restore показан отдельно. Injected44px insets — Editor geometry, не iPhone.

| Состояние | FLAME1600×900 | GALE1280×960 |
|---|---|---|
| Preparation8 / notice |[кадр](screenshots/flame-victory-1600x900/01-preparation8-notice.png)|[кадр](screenshots/gale-victory-1280x960/01-preparation8-notice.png)|
| Чистый реальный Continue8 |[кадр](screenshots/flame-victory-1600x900/02-clean-continue8.png)|[кадр](screenshots/gale-victory-1280x960/02-clean-continue8.png)|
| Boss1000 / armor5 |[кадр](screenshots/flame-victory-1600x900/04-boss-hp-armor.png)|[кадр](screenshots/gale-victory-1280x960/04-boss-hp-armor.png)|
| Ability117 |[кадр](screenshots/flame-victory-1600x900/05-boss-ability-hit.png)|[кадр](screenshots/gale-victory-1280x960/05-boss-ability-hit.png)|
| Real kill/Victory |[кадр](screenshots/flame-victory-1600x900/08-boss-kill-victory.png)|[кадр](screenshots/gale-victory-1280x960/08-boss-kill-victory.png)|
| Failed completion save |[кадр](screenshots/flame-victory-1600x900/09-victory-completion-save-failed.png)|[кадр](screenshots/gale-victory-1280x960/09-victory-completion-save-failed.png)|
| Retry saved / same rewards |[кадр](screenshots/flame-victory-1600x900/10-victory-retry-saved.png)|[кадр](screenshots/gale-victory-1280x960/10-victory-retry-saved.png)|
| Clean Victory restore |[кадр](screenshots/flame-victory-1600x900/11-clean-victory-restored.png)|[кадр](screenshots/gale-victory-1280x960/11-clean-victory-restored.png)|
| Terminal Continue off |[кадр](screenshots/flame-victory-1600x900/12-terminal-continue-off.png)|[кадр](screenshots/gale-victory-1280x960/12-terminal-continue-off.png)|

Отдельный [boss-leak Defeat](screenshots/flame-leak-1600x900/08-defeat.png). Остальные waiting-spawn/insets/status/inspector/Pause кадры, telemetry и Console находятся рядом. [Console/frame audit](console-audit.json): warnings6/6/4 — `Ignoring depth surface ... memoryless`; expected IO errors1/1/0 маркированы отдельно и подтверждены LogAssert.Expect, unexpected0. Это case scope от Setup до Cleanup, не обещание отсутствия всех инфраструктурных сообщений Editor вне scope.

## История, review и границы

R1/R2 и все772 прежних evidence файла сохранены побайтно. Их красные raw XML не переписаны и не переименованы в PASS: [R1](../validation.md), [R2](../revision-2/validation.md). Старые asset-source manifests проверяются по их Git commit, а не привязываются к изменённому R3 рабочему дереву. `python3 Docs/review/P2.4e/revision-3/verify-evidence.py` проверяет source/config/input/report/media/archive hashes, полные totals, контроль154/68, все5кандидатов, выбранные natural outcomes и сохранённую историю.

[Независимое review](review.md) отдельно проверяет финальный source/evidence; reviewer Unity самостоятельно не запускает. Этот этап закрывает Editor acceptance #39; issue/индекс переходят в Review, Done только после owner merge. Self-merge/release/TestFlight не выполнялись.

**NOT VERIFIED / BLOCKED отдельно:** physical iPhone/IL2CPP/Metal/touch/lifecycle/performance, human G2/final balance/ART approval, hosted Unity CI и полная отдельная #40 double trace/file/menu matrix. Coverage routes не имеют найденной дополнительной Victory; их честный Defeat и4positive counters соответствуют узкому R3 контракту, а будущий #40 потребует своего acceptance. Projects field не заявляется изменённым без доступного API/проверки.
