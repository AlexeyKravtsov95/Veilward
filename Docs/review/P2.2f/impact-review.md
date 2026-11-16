# P2.2f — воспроизводимость, rollback и измеренный impact

**VERIFIED:** технические replay/Store/Controller проверки на существующем runtime, historical контроль, matched и две отдельные hybrid серии. Runtime, balance, сцены, configs и packages в этом PR не меняются. **P2.2 остаётся открытым до независимого review/merge.** Данные измеряют влияние фиксированного script, не утверждают баланс, веселье или готовность TestFlight.

База: merged PR #22, `8159f4c530bea98ec8bc495f3168299342e12831`; rules label **`p2.2-steam-storm-v1`**. Полный commit проверенных исходников и SHA256 — в [gates.json](gates.json) и [source-sha256.txt](source-sha256.txt); config hashes также в [replay-summary.json](replay-summary.json). Unity 6000.3.28f1 (8f082d2fe476), macOS 27.0.1 (26A434), arm64; `-batchmode -nographics -buildTarget iOS`, Editor Mono, player settings IL2CPP/ARM64. Это Editor tests, не запуск iOS player. Fixed 30 Hz, RNG отсутствует, seed неприменим.

## Источники и точные scripts

- Historical: `BattleElementalReplayTests.Prepare` и saved EightWave/shared TestWave, с явным internal `ReactionCatalog.Empty`. В каждой точке create/restore используется прежний fixture. `RegBattle126_CommittedBalanceEvidence_MatchesReplay` сравнивает весь исторический JSON byte-for-byte. `Docs/review/P2.1e/*`, исходный test script и числа не изменены.
- Matched: те же запросы из `BattleReactionReplayTests.Prepare`: wave 1 Place flame (-2,-1), gale (1,0); wave 3 Place tide (-1,0); wave 5 Place stone (-3,-1); wave 7 Sell 3, repeated Sell 3 (rejected), Place tide (-1,0), Upgrade 5. Тот же saved путь/позиции/волны/базовые definitions/First/150 Gold/20 HP, изменён только production ReactionCatalog. Между волнами Start, Next и повторный rejected Next. Нет выдачи ресурсов/замены команд.
- Отдельные `steam`/`storm`: wave 1 Place выбранный гибрид (-2,-1) за 110; wave 2 Sell 1 за 77, повторный Sell 1 rejected, Place того же типа/клетки за 110 с новым ID 2; wave 3 Upgrade 2 rejected InsufficientGold. Затем Start/Next до естественного terminal. Все вложения L1 = 110; один refund 77, суммарные покупки 220; L2/L3 в этих матчах не достигнуты. Их таблицы/успешная экономика остаются в прежних `BattleHybridTowerTests`, этот отчёт не выдаёт rejected Upgrade за успешное покрытие L2/L3.

Каждая серия выполнена дважды, сравнение всех строк timeline и JSON до естественного terminal. Assertions не требуют Victory. Гибриды намеренно показаны как отдельные стратегии; результаты не являются оценкой оптимальной игры этими башнями.

| Серия | Terminal / волна | Tick | HP / Gold / Kills | Leaks всего | Timeline entries |
|---|---|---:|---|---:|---:|
| historical | Defeat / 8 | 3997 | 0 / 112 / 46 | 20 | 4058 |
| matched | Victory / 8 | 3787 | 11 / 197 / 59 | 9 | 3848 |
| steam | Defeat / 4 | 1716 | 0 / 87 / 4 | 20 | 1747 |
| storm | Defeat / 4 | 1596 | 0 / 67 / 0 | 20 | 1627 |

## Достигнутый общий префикс matched/control

Полностью идентичны первые **10 из 30 receipts**. Первый differing receipt:

- `C|w=4|t=1287|Next=Continued|gold=50`
- `C|w=4|t=1287|Next=Continued|gold=60`

Доступность и результаты requested-команд совпали для всех **30/30** receipts: прежние покупки доступны, repeated Sell/Next отклонены в обеих сериях. Боевой timeline совпадает до первого Steam на wave 3 (точный первый differing snapshot ниже). После этого сравниваются одинаковые запросы/позиции, но HP, доход, targeting и позже длительность волн расходятся. Matched прошёл все 8 волн до Victory, historical закончил wave 8 Defeat; после terminal historical дальнейшего общего боевого префикса нет. Delta не является строгой причинной атрибуцией каждого спасённого HP.

Первое различие полного timeline: строка 872, `['Combat', '2', '851']` (phase/waveIndex/tick; waveIndex zero-based). Сырые строки доступны в compressed traces.

## По волнам

Ticks — число боевых Steps в волне; HP/Gold/Kills — cumulative после боя, leaks — за эту волну. Investment — активные вложения в башни, purchases/refunds и все receipts отдельно в JSON. Direct/Burn/Reaction order и Steam/Storm order зафиксированы в JSON.

### historical

| Wave | Outcome | Ticks / absolute tick | HP | Gold | Kills | Leaks | Investment |
|---|---|---|---:|---:|---:|---:|---:|
| 1 | WaveComplete | 399 / 399 | 18 | 35 | 3 | 2 | 150 |
| 2 | WaveComplete | 429 / 828 | 15 | 70 | 6 | 3 | 150 |
| 3 | WaveComplete | 459 / 1287 | 12 | 50 | 10 | 3 | 210 |
| 4 | WaveComplete | 501 / 1788 | 8 | 90 | 14 | 4 | 210 |
| 5 | WaveComplete | 501 / 2289 | 6 | 55 | 21 | 2 | 300 |
| 6 | WaveComplete | 561 / 2850 | 3 | 110 | 28 | 3 | 300 |
| 7 | WaveComplete | 579 / 3429 | 1 | 67 | 37 | 2 | 390 |
| 8 | Defeat | 568 / 3997 | 0 | 112 | 46 | 1 | 390 |

### matched

| Wave | Outcome | Ticks / absolute tick | HP | Gold | Kills | Leaks | Investment |
|---|---|---|---:|---:|---:|---:|---:|
| 1 | WaveComplete | 399 / 399 | 18 | 35 | 3 | 2 | 150 |
| 2 | WaveComplete | 429 / 828 | 15 | 70 | 6 | 3 | 150 |
| 3 | WaveComplete | 459 / 1287 | 14 | 60 | 12 | 1 | 210 |
| 4 | WaveComplete | 501 / 1788 | 12 | 110 | 18 | 2 | 210 |
| 5 | WaveComplete | 441 / 2229 | 12 | 85 | 27 | 0 | 300 |
| 6 | WaveComplete | 549 / 2778 | 11 | 150 | 36 | 1 | 300 |
| 7 | WaveComplete | 457 / 3235 | 11 | 117 | 47 | 0 | 390 |
| 8 | Victory | 552 / 3787 | 11 | 197 | 59 | 0 | 390 |

### steam

| Wave | Outcome | Ticks / absolute tick | HP | Gold | Kills | Leaks | Investment |
|---|---|---|---:|---:|---:|---:|---:|
| 1 | WaveComplete | 399 / 399 | 16 | 65 | 1 | 4 | 110 |
| 2 | WaveComplete | 429 / 828 | 11 | 57 | 2 | 5 | 110 |
| 3 | WaveComplete | 459 / 1287 | 5 | 82 | 3 | 6 | 110 |
| 4 | Defeat | 429 / 1716 | 0 | 87 | 4 | 5 | 110 |

### storm

| Wave | Outcome | Ticks / absolute tick | HP | Gold | Kills | Leaks | Investment |
|---|---|---|---:|---:|---:|---:|---:|
| 1 | WaveComplete | 399 / 399 | 15 | 60 | 0 | 5 | 110 |
| 2 | WaveComplete | 429 / 828 | 9 | 47 | 0 | 6 | 110 |
| 3 | WaveComplete | 459 / 1287 | 2 | 67 | 0 | 7 | 110 |
| 4 | Defeat | 309 / 1596 | 0 | 67 | 0 | 2 | 110 |

## Damage, proc и suppression measurements

Все per-wave `applied`, `overkill`, `lethal`, `reactionApplied/reactionOverkill/reactionLethal`, `procs`, `targets`, `intrinsicProcs`, `cooldownSuppressions`, `noNeighborSuppressions` находятся в проверяемом JSON. Ниже суммы; targets — число фактических damage hits, не число уникальных врагов за матч. Storm primary резервирует timestamp, но не является damage victim.

| Серия | Applied D/B/R | Overkill D/B/R | Lethal D/B/R | Steam proc/targets | Storm proc/targets | CD Steam/Storm | No-neighbor Storm |
|---|---|---|---|---|---|---|---:|
| historical | 5261/847/0 | 216/8/0 | 38/8/0 | 0/0 | 0/0 | 0/0 | 0 |
| matched | 4964/591/878 | 323/6/26 | 46/7/6 | 46/58 | 25/47 | 32/88 | 5 |
| steam | 616/0/504 | 32/0/0 | 4/0/0 | 28/28 | 0/0 | 4/0 | 0 |
| storm | 594/0/246 | 0/0/0 | 0/0/0 | 0/0 | 23/41 | 0/34 | 9 |

`AppliedHpLoss` сверяется с HP до/после каждого event. По каждому killed ID суммарный loss = MaxHealth, lethal ровно один; изменение Kills/Gold каждого Step = lethal count и единственная reward 5, плюс единственный bonus 20 в успешном terminal волны. По волне `sum(applied) + leakedRemainingHp + liveRemainingHp == spawned × enemyHp`. Defeat не даёт bonus; живые terminal enemies остаются в timeline, но не требуются при checkpoint restore. Leak не даёт reward. Overkill = ResolvedDamage − AppliedHpLoss; не суммируется как эффективный урон.

Test-only observer сохраняет shadow HP/status/cooldown до Step, учитывает expiry/Burn/spawn/leaks, затем обрабатывает direct events в sequence order и incoming status выжившей primary. Проверяет actual AttackEvent и predicted candidate/targets против Reaction/Damage events; reserve распространяется на primary и всех secondary до damage. Geometry — Euclidean distance² после Movement, inclusive boundary, nearest/ID, greedy Storm chain, skip dead/leaked/locked/visited. Cooldown suppression означает один отклонённый кандидат при закрытом **primary** окне; no-neighbor означает готовый Storm-кандидат без первого допустимого hop (включая исключение закрытых соседей). Отсутствующая пара, lethal direct, каждый исключённый secondary и неиспытанный после Steam кандидат не являются отдельными suppressions. Новый production combat log не добавлен.

### Отдельный synthetic armor/density fixture

`RegBattle162_SuppressionObserver_DistinguishesCooldownAndNoNeighbor(steam/storm)` использует production tower/reaction definitions, но явно **synthetic** enemy HP 1000, armor 7, speed 0.1, 3 spawn на 0/30/60 ticks, путь (0,0)→(100,0), tower (0,1), 100 Steps. Это проверка observer/armor/плотности, не результат saved EightWave и не оценка пользы Crack. Steam: direct applied 33, reaction 44, положительный cooldown suppression; Storm: direct 10, reaction 1, два no-neighbor suppression и положительный cooldown suppression. JSON/traces имеют префикс `synthetic-armor-` и не включены в saved-match totals. Геометрические boundary/Crack tests из P2.2c также не смешиваются с матчами.

## Файловый rollback и Continue

`RegBattle160_FileRollbackAfterReactionKill_ReplaysPrefixAndFullMatch`: temporary save root, настоящий WaveCheckpointStore + BattleCheckpointController. Preparation wave 3: tick 828, HP 15, Gold 10, Kills 6, nextEnemyId 12 / nextTowerId 4, nonzero tower cooldown. Interrupt tick 1079: Gold 30, Kills 10, lethal Steam против ID 15 после nonlethal direct, у живого ID 16 есть LastStormTick 1003. Primary save bytes всё ещё равны Preparation. Restore на грязной simulation очищает все три stream/enemies/statuses/timestamps; полный snapshot/serialized checkpoint/Grid возвращается к Preparation. Два повторения 252-entry interrupted prefix и полный последующий матч совпадают с control. На каждой safe phase дополнительно fresh-instance file round-trip; WaveComplete загружается дважды. Repeated Next не меняет Gold/Kills/state. [Prefix](rollback-prefix.timeline.txt.gz), [restore snapshot](rollback.txt).

`BattleReactionPlayModeTests.RegBattle160_ReactionKill_FileContinueAndRepeatedNextDoNotPay` выполняет тот же saved basic flow через реальную MainMenu/Battle session и temporary store. Три menu Continue восстанавливают Preparation (два prefix replay, третий для завершения волны); views/cues/inspector/streams чистые. После WaveComplete два menu Continue и повторный Next не начисляют rewards. Это настоящий Editor PlayMode session/file integration, не physical force quit/relaunch. Новых визуальных изменений нет; реальные предыдущие [P2.2d frames](../P2.2d/README.md) и [P2.2e frames](../P2.2e/README.md) доступны reviewer.

## Воспроизведение и gates

Из isolated worktree, на source commit из gates.json:

```bash
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
git diff --check
```

Replay tests пишут actual JSON/traces в ignored `TestResults/P2.2f/`, затем сравнивают JSON с committed evidence; никаких update-golden flags или автоматического переписывания исторического JSON нет. `timelineSha256` вычислен над несжатым UTF-8 с LF и завершающим LF; gzip metadata не влияет на этот hash. Финальные gates: **38/38 static**, **528/528 EditMode**, **25/25 PlayMode**, failed/skipped/inconclusive = 0; `git diff --check origin/main` PASS. [EditMode.xml](EditMode.xml), [PlayMode.xml](PlayMode.xml), [static log](static-checks.txt), [gates.json](gates.json). Gate manifests/XML — в этой папке. Побочные Unity import/builder пересериализации восстановлены из base только в этой worktree; неизменность source SHA после восстановления проверена.

Первые проверки существующего runtime сразу прошли; искусственный RED не создавался. В процессе исправлены только test defects: неоднозначное имя CompressionLevel, неверный тип waypoint в synthetic fixture и недостижимая до natural Defeat продажа hybrid на wave 5 (перенесена на wave 2). Отсутствующий committed JSON сначала дал ожидаемый evidence FAIL, после записи фактических измерений проверен повторно. Это не RED production bug. Production bugs не найдены, runtime/balance fix PR не потребовался.

**NOT VERIFIED / BLOCKED:** физический iPhone, реальный touch/safe area/background/force quit, iOS player/IL2CPP execution, performance/allocations/thermal, межплатформенная float bit identity, Unity CI, external playtests, G2 и balance approval. L2/L3 hybrid combat не выполнен этими бюджетными scripts. Ни art, P2.3/P2.4/P2.5, первая TestFlight, ни release не завершены этим PR. Обязательный готовый арт первого среза и дальнейшая последовательность — [ROADMAP](../../ROADMAP.md).
