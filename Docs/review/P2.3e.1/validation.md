# P2.3e.1 — результаты поиска стратегий

**FLAME: путь до6 и Victory8 найден; Steam/Storm status-pair и intrinsic положительны. GALE: путь до6 и Victory8 найден; Steam/Storm status-pair и intrinsic положительны.**

Это проверочный PR от merge PR31 `082ee6f6868b5e124e2bcc915f8c882557ff3a6a`. Gameplay/runtime, баланс, serialized configs, packages и арт сохранены. Старые P2.3e scripts и Defeat5 golden остаются историческим контролем. P2.3 не объявляется DONE до завершения acceptance/review/approval; device/G2/ART approval/баланс/TestFlight отдельно.

## Выбранные кандидаты

| Script | Естественный итог | Tick | HP | Gold | Kills | Положительные виды proc |
|---|---|---:|---:|---:|---:|---|
| [A-survival](A-survival.commands.json) | Victory8 | 3847 | 6 | 229 | 54 | Steam/StatusPair |
| [B-survival](B-survival.commands.json) | Victory8 | 3820 | 8 | 290 | 56 | Storm/StatusPair |
| [A-coverage](A-coverage.commands.json) | Victory8 | 3654 | 6 | 90 | 54 | Steam/StatusPair, Steam/Intrinsic, Storm/StatusPair, Storm/Intrinsic |
| [B-coverage](B-coverage.commands.json) | Victory8 | 3582 | 8 | 112 | 56 | Storm/StatusPair, Storm/Intrinsic, Steam/StatusPair, Steam/Intrinsic |

Survival и coverage разделены: выигрыш без нужного intrinsic остаётся доказательством выживания. Во всех четырёх выбранных scripts старт150 Gold/20 HP, public production factory и только публичные команды. Full New Game replay с probes/receipts не использует Restore или search shortcuts. Два trace сравнивают каждый command/tick/state/Grid/IDs/army/cooldown/status/event/progression, а не только итог. Выбранные scripts — лучшие среди ограниченного набора по Victory, положительному coverage (для coverage), HP, Gold, Kills и более раннему terminal tick при равенстве; все четыре из v2. Глобальная оптимальность не утверждается.

## Состояния по волнам (coverage)

| Wave | A HP / Gold / Kills | B HP / Gold / Kills | Progression до Confirm |
|---:|---|---|---|
| 1 | 17 / 40 / 2 | 16 / 95 / 1 | earned/spent 0/0; none |
| 2 | 13 / 70 / 4 | 13 / 50 / 4 | earned/spent 0/0; none |
| 3 | 9 / 35 / 7 | 9 / 85 / 7 | earned/spent 1/0; after-wave-3 |
| 4 | 6 / 69 / 12 | 8 / 60 / 14 | earned/spent 1/1; none |
| 5 | 6 / 74 / 21 | 8 / 65 / 23 | earned/spent 1/1; none |
| 6 | 6 / 76 / 31 | 8 / 81 / 33 | earned/spent 2/1; after-wave-6 |
| 7 | 6 / 120 / 42 | 8 / 86 / 44 | earned/spent 2/2; none |
| 8 | 6 / 90 / 54 | 8 / 112 / 56 | earned/spent 2/2; none |

Полная армия, Level/InvestedGold/NextAttackTick, IDs, решения и все receipts находятся в отдельных `*.summary.json`. Состояния WaveComplete3/6 сняты **до** Confirm; после второго Confirm earned/spent2/2, pending none и отдельный Next → Preparation7. Положительный damage и первые фактические procs сопоставляются по tick/sequence/kind/source с DamageResolvedEvent.AppliedHpLoss, а не номинальным уроном.

## Конечный поиск

Исследовательский бюджет заранее ограничен: на каждый проход/старт максимум8×16000 wave rollouts, beam24 и максимум20000 ticks на rollout. Выполнены три ограниченные ревизии эвристики; после v3 поиск остановлен. Всего **672826** уникальных evaluated Preparation states / wave rollouts, **344.146 секунды** внутри search (без Unity import/startup), верхняя граница трёх запусков1536000. RNG нет, seed не требуется. Все кандидаты производятся public commands от New Game либо immutable ExportCheckpoint, Restore выполняется public factory; DTO не редактируется.

Порядок запуска: A-survival, B-survival, A-coverage, B-coverage. Cells — все Available из Grid в порядке y,x (81 клетка исходной карты; точный список в v3 logs). Types ordinal по ID, towers по ID. Parents и move streams обходятся round-robin. Hold; доступный Upgrade; Place каждого unlocked типа на каждой доступной клетке; Sell; Sell→Place для каждой башни/типа/клетки, включая прежнюю освобождённую. Вторая глубина расширяет до8 лучших Preparation после первой операции; допускает две покупки, двойной Upgrade, Build→Upgrade и до3 команд при composite Sell→Place. Ограничения: нет более длинных Preparation-планов, Sell-all, произвольной задержки между tick-командами, иных unlock routes, полного перебора всех сочетаний/порядков армии.

На глубине0 лимит75% wave budget, на глубине1 остаток до16000. Некоторые streams могут остаться неисчерпанными даже если вторая глубина не использовала остаток. Dedup сравнивает **весь** checkpoint + coverage предыдущих волн (IDs/cooldown сохраняются); никакой геометрической симметрии не предполагается. Budget counts только реально симулированные уникальные Preparation, `generated` включает duplicates. `truncatedStreams` — число открытых streams на остановке, **не** оценка количества непроверенных кандидатов.

Ранжирование survival: alive, HP, Gold+сохранённый investment, Kills, порядковый номер кандидата. Coverage: alive, число положительных видов proc, затем HP/экономика/Kills/order. Это эвристика: высокий текущий HP не доказывает будущую жизнеспособность. Defeat не ветвится; dead nodes сохраняются в отчёте. Unaffordable/locked операции не перечисляются. Остальные отсечения: duplicate, beam width, local8, wave cap, natural terminal. Нулевой требуемый proc не считается PASS.

| Ревизия | Проверенные rollouts | Search seconds | Итог / ограничения |
|---|---:|---:|---|
| Pilot v1 | 201297 | 106.419 | A/B survival Victory8; A coverage Defeat6. Счётчики fixture были cumulative; положительный mask второй глубины мог учитывать discarded attempt. Не используется как reaction evidence. |
| v2 | 231944 | 122.951 | Счётчики изолированы; mask только реально исполненных предыдущих волн. Coverage beam сохраняет половину survival: оба coverage Victory8/mask15. |
| v3 | 239585 | 114.776 | Дополнительно сохраняет профили размера/уровней/типов армии: оба survival/coverage Victory8, L2 и L1, Upgrade/hold/Sell/relocate реально проверены. Лучшие найденные v2 не отбрасываются. |

v3 evaluated categories суммарно: hold676, place86126, sell1322, sell+place151101, upgrade360. Включены single L2 против нескольких L1, ранний hybrid против базовых пар, покупки/Upgrade/накопление/продажи на разных Preparation. Выбранный A продаёт tower1 в Preparation4, TIDE в6, FLAME в7; прежняя обязательная продажа tower2 после3 не применяется. B удерживает базовую оборону до возможности купить гибрид. Для каждого прохода сохранены command candidates, best-per-wave full checkpoints и точные счётчики: [search](search).

Сбой до успешного v2: один запуск не компилировался из-за неоднозначного CompressionLevel в новом test; квалифицирован System.IO.Compression.CompressionLevel. До frozen golden выполнен исследовательский bootstrap: 0/4 из-за отсутствующих новых `*.summary.json`, после всех gameplay/determinism/positive-proc assertions; [raw bootstrap](golden-bootstrap.xml). Новые golden взяты из его outputs и дальше только читаются. Это не PASS gate. Pilot/v2/v3 source snapshots сохранены; v2 восстановлен из того же исходника с точно прежним Retain и без добавленных v3 counters/profiles.

Воспроизведение полного поиска в отдельном checkout: `P23E1_SEARCH=1 <Unity> -batchmode -nographics -projectPath <checkout> -buildTarget iOS -runTests -testPlatform EditMode -testFilter NativeStrategySearchTests -testResults <absolute-report.xml> -logFile <absolute-log>`. Текущая utility — v3; для v2 заменить только `NativeStrategySearchTests.cs` содержимым [архива](search/v2.cs.txt), сохранив .meta. Default full gates запускают короткий128/1-wave search regression, это **не** повтор полной исследовательской выборки. Полные выбранные New Game scripts исполняются во всех полных gates.

## Проверки и ограничения

Полные gates, файловая/контроллерная late matrix, raw reports и Play Mode evidence добавляются после запуска. Отсутствующие проверки не объявлены PASS. Новые natural evidence хранятся отдельно от synthetic REG174…185 и legacy migration fixtures. Физический iPhone/IL2CPP/Metal/performance/touch/safe area/background/force quit/signing не проверяются этим исследованием. Баланс-эксперимент для доказательства существования пути не требуется; полученные победы не означают, что баланс утверждён или дружелюбен к новичку.

Первый полный EditMode: **760/761**, один failure нового REG189 из-за ошибочного требования reaction rollback в Combat4 у B (доTIDE5 реакций закономерно нет). Исправлен только test expectation: ранний rollback A4/B5 и обязательный late rollback7 обоих маршрутов; старый B Combat4 сохранён в историческом suite. Это не причина отбрасывать жизнеспособный script. [Raw initial report](initial-edit.xml.gz); production не менялся.
