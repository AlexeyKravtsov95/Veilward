# P2.4f / #40 — draft, REG214 BLOCKED

База — свежий main `a9a9ae4fd02e86a1d1f59ee689d4a941976e146e`, merged PR #69. Отдельная ветка `codex/p2.4f-production-validation`. [Спецификация до реализации](specification.md). Production boss **1000HP/armor5**, ability120/1.5/600,150Gold/20HP, башни/цены/экономика/spawns/configs/runtime/packages/исторические tests и evidence сохранены. Это tests/evidence PR, **Refs #40**, без Closes, merge/release/TestFlight.

## NOT VERIFIED / BLOCKED — главный acceptance

Для каждого старта требуется **один** natural Victory8 с ability kill, boss kill и положительными Steam/Storm × StatusPair/Intrinsic в том же прохождении. Ни committed controls, ни конечный поиск этого не подтвердили. Обязательные `RegBattle214_SameProductionRunMustWinWithAbilityBossAndFourPositiveCells` остаются **двумя FAIL**, без Explicit/Skip/ослабления assertions. Детерминизм и зелёная file/menu matrix не закрывают REG214.

| Полное production прохождение от150/20 | FLAME→TIDE→GALE | GALE→TIDE→FLAME |
|---|---:|---:|
| Outcome | Defeat8 | Defeat8 |
| Boss HP перед leak20 |392|462|
| Boss kills |0|0|
| Ability kills / AppliedHpLoss |13 /1348|15 /1568|
| Steam StatusPair / Intrinsic AppliedHpLoss |442 /180|133 /152|
| Storm StatusPair / Intrinsic AppliedHpLoss |98 /6|118 /174|

[FLAME inputs](flame.commands.json), [GALE inputs](gale.commands.json), [reports/traces](natural/). Это законные **coverage/rollback controls**, не winning routes. Пропущен Cast6, Cast7 — после Step1; GALE продаёт Storm7 после его ранее полученного intrinsic и покупает Steam8. Все армии заработаны public commands. Никакие live/DTO HP/Gold/IDs/cooldown/progression/definitions не исправлялись. Malformed DTO в rejection probes и явно synthetic old-mode slots не являются источниками production армии.

**Предложение владельцу:** сохранить #40 открытой; отдельным продолжением согласовать более целевой strategy pass по F/T/G prefix/ordered choices/ранним Upgrade/позициям с предельным бюджетом. Если затем потребуется продуктовое изменение — сначала отдельно согласовать его параметры и критерии. Текущие результаты показывают остаток392/440HP у лучших fully covered seeds данного поиска, а не общую непроходимость. Смена1000/5 или критериев в этом PR не предлагается как готовое решение.

## VERIFIED — traces, accounting и независимая поздняя matrix

Новый `AbilityBossTrace` не меняет historical writer. На каждом command/Step до clear: phase/tick/HP/Gold/Kills/Grid, вся армия/levels/investment/IDs/nextAttack, следующие IDs, progression/history, ability remaining/sequence, enemy HP/distance/status/timers/reaction timestamps. Четыре streams содержат все поля, включая ability attribution, общий sequence, captured positions, configured/raw/resolved/applied/overkill. Receipt Cast включает expected wave/tick/sequence, center, accepted sequence и boundary перед следующим Step. HP по каждому EnemyId сходится с origins+leaked/live HP; Gold=150+command deltas+одна reward каждого kill+один bonus каждой успешной волны. Base HP сверяется с фактическими leaks. Closed/stale/double/invalid probes сравнивают весь state/events.

- Два независимых full traces каждого control совпадают побайтно; raw SHA256/размеры находятся в JSON рядом с gzip. [Double](double/).
- Whole file-continued trace совпадает с uninterrupted control на каждом tick/receipt/event. Initial pending/confirmed start, pending/confirmed3/6, Preparation7/8, completion/terminal; реальные Store/compatibility factory/CheckpointController. Повторный Load не меняет primary/backup bytes. [File matrix](files/).
- По13 failed safe saves на старт:3Confirm,8completion,Next7/8. Live success сохранён, restart видит прежний durable snapshot, eligibility/save guards блокируют Start/Next при IO fault, Retry дважды только сохраняет. Награды, casts и transitions не повторяются.
- Combat7 **и** Combat8 каждого старта останавливаются после accepted ability kill + positive reaction: Phase==Combat, живые enemies, непустые streams. Rejected restore сохраняет dirty state/events; dirty и fresh restore возвращают весь pre-Start Preparation, включая IDs/nextAttack/progression/cooldown/sequence и чистые streams/enemies. Первый prefix — dirty instance, второй — fresh file-restored instance; они равны. Полный остаток матча из fresh также равен control. Combat cast не записывает отдельную ability durability.
- Реальные Session/MainMenu/Buttons, placement/sell UI, world pointer dispatcher, ability lease/queued intent/двойной Confirm. Initial и pending/confirmed3/6 проходят повторный Continue; actual failed Choose3→restart, Choose6→Retry и Next→Preparation7/8 second-save fault→Retry. Rejected restore сохраняет queued intent и owner, успешный Continue чистит owners/lease/intents/selection/inspector/summary/beams/ability+reaction pools/streams. Pause и первый resumed Update сохраняют tick/cooldown/state. Показанные поздние army — результат New Game, не загруженная выдача.
- Ordered new-native/old-native/all-open slots в обоих направлениях, corrupt primary→whole backup — явно synthetic routing controls с настоящим естественно достигнутым новым Preparation8. Existing full suites отдельно проверяют schema1/2/3/4, impossible schema/content, malformed ability, factory routing и temp/read-back/commit faults.

Это VERIFIED для указанных проигрывающих controls; matrix **winning coverage routes** остаётся BLOCKED вместе с REG214.

## Per-wave результаты controls

Числа — safe phase после completion, до следующего Choose/Next/покупок; ability kills накопительные.

| Wave | FLAME HP/Gold/Kills | cooldown/sequence | ability kills | GALE HP/Gold/Kills | cooldown/sequence | ability kills |
|---|---|---|---:|---|---|---:|
|1|18/45/3|201/2|1|18/105/3|231/2|2|
|2|18/95/9|425/3|4|16/145/7|492/3|5|
|3|15/135/13|600/4|6|10/170/8|33/3|5|
|4|10/110/16|99/4|6|9/55/15|158/4|8|
|5|10/65/25|259/5|9|9/120/24|313/5|11|
|6|7/120/32|0/5|9|3/160/28|0/5|11|
|7|7/115/43|158/6|10|3/132/39|96/6|12|
|8|0/60/54|536/8|13|0/77/50|476/8|15|

Полные per-wave snapshots/cells, per-enemy HP/origins/overkill/rewards и command budgets находятся в JSON, не заменены этой таблицей.

## Конечный поиск и его ограничения

`P24F_SEARCH=1`,20000attempts/start,600s wall cap/start, hard stop при первом eligible win. Поиск исчерпал attempts раньше wall cap. FLAME:77631927ticks,6981exact duplicate plans,77.26s; GALE:70380911ticks,7875duplicates,71.35s. Illegal0, Victory0, eligible0. Все Victory сохранялись бы до ранжирования/dedup; фактически их не было. Dedup здесь — измерение после evaluation, повторные планы не отсекались; pruning отсутствует, нелегальные/недоступные покупки read-only отбрасывались до команд.

Эвристика: geometry16/offset5, starter1/2, вторая башня2/3, starter Upgrade3, Tide-first unlock, threshold100/200/300, boss delay0/60/120/180/240, early8/hold7, Sell extra7,4rebuild recipes8,5final spend flavors. Она не исчерпывает ordered choices, все ранние prefix/уровни/расположения и непрерывные centers. Исторические P2.3e.1/R3 seeds использованы только как исходные controls новой factory.

Минимальный bossHP по всем попыткам380 у FLAME имел неполный reaction coverage. **Fully covered** лучшие: FLAME index12450→392HP и GALE index24→440HP. Исправленные inputs/full traces и fixed-index replays — [known seeds](search-known-seeds/). R3 GALE coverage424 лучше нового поиска, но также проигрывает; исходный R3 evidence сохранён.

**Ограничение исходного search journal:** capture `WaveElapsedTicks` после lethal Cast completion записывал0. Fixed-index replay выявил это отдельным RED; исправлено capture до Cast. Outcomes/decisions исходных40000 evaluations от этой записи не зависели (casts array назначался после матча). Исходный search archive/его source/report сохранены **с этим ограничением**; его best scripts не объявляются проверенными replay inputs. Исправленные known-seed replays воспроизводят392/440 и4positive cells. Дополнительный поиск после budget не запускался. Обычный mandatory suite выполняет только2smoke candidates/start и2фиксированных known-seed replays; env не меняет winning assertions.

## Отдельные natural controls и история

Новые full traces R3 Stone scripts дают **Victory8**: FLAME baseHP10, GALE14, boss kill1, positive ability. Они не имеют обязательного полного F/T/G coverage и не засчитываются за REG214. Отдельный legal FLAME→STONE→TIDE no-cast8 достигает Preparation8 сHP13, затем живой boss276HP leak20→Defeat; boss reward/bonus отсутствуют. [Controls](controls/).

Все прежние P2.3e/e.1 и P2.4a–e/R1/R2/R3 evidence, source controls и assertions сохранены побайтно. [Historical audit](historical-audit.json), [source hashes](source-sha256.json). Builder/import serialization drift возвращён только в своей исходно чистой worktree; пользовательский checkout и другие worktrees не изменялись.

## Gates, реальные кадры и review

Точные source SHA/Unity/host/commands/totals/report hashes — [gates.json](gates.json), raw XML — [reports](reports/), artifact hashes — [manifest](manifest.json), independent [review](review.md). Штатный XML validator честно возвращает FAIL для полного EditMode с двумя обязательными REG214 failures. Ни один skipped/inconclusive/empty run не объявлен PASS. Compile/meta/GUID/package/version/builder/references/reproducibility включены в полные suites/static checks.

Graphical screenshots — реальные `ScreenCapture` в Editor Play Mode. New Game150/20, both starts, positive reactions, ability/queued intent, pending/confirmed6, Preparation7/8, interruption/clean Continue, failed saves/Retry, boss/terminal. Отдельные Stone Victory и leak кадры — controls, подписаны отдельно. Telemetry содержит фактический frameCount; frames продвигаются. `Battle.Update` выключен для ускоренного public-command test clock; это **не realtime VFX/performance**. Все четыре streams потребляются штатными presentation consumers. Injected IO и44px insets старых control captures обозначены; это не physical touch/safe area.

**Отдельно NOT VERIFIED:** физический iPhone/IL2CPP/Metal player, touch/lifecycle/force quit/performance/thermal, human G2/final balance, ART approval, hosted Unity CI, release/TestFlight. Projects поле отдельно не подтверждено/не изменено. PR draft, issue остаётся open; окончательная приёмка владельца отдельно.

Финальные локальные итоги: static**38/38**, EditMode**1069passed/1071total,2failed**, PlayMode**60/60**, graphical**5/5**; failed/skipped/inconclusive в Play/Visual0, skipped/inconclusive в Edit0. Всего**108** реальных PNG:74новых coverage/menu/rollback и34отдельных Stone/leak controls. Console case scope:0unexpected errors,10expected injected IO errors,64Editor rendering warnings. Representative interrupted7, clean Continue8, failed Next8 и Stone Victory кадры просмотрены.

Независимый reviewer проверил head `bbabcea`: дополнительных P1/P2 code/evidence blockers0; известный P1 REG214 остаётся. [Полный review](review.md). После него меняются только review/manifests metadata; source/tests7584e22 сохранены, финальный verifier/diff --check повторены.
