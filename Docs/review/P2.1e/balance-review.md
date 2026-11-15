# P2.1e — интеграционная проверка стихийного матча

**Статус: ожидает независимого review/merge.** Числа остаются гипотезами до внешних плейтестов; баланс и runtime не изменялись.

## Scope, критерии и воспроизведение

Task 4 из `Docs/P2.1A_IMPLEMENTATION_PLAN.md`, REG-BATTLE-125…127. Затронуты только EditMode integration tests, их `.meta` и документация. Acceptance: saved EightWave, реальные ресурсы, четыре активных статуса, DoT kill, Upgrade/Sell/Rebuild, естественный terminal, идентичные повторные timelines и дальнейшее прохождение после файлового Save/Restore/Continue/Combat rollback. Риски: доступность покупок на доходах данной стратегии, duplicate rewards, потеря cooldown/IDs и перенос незавершённой попытки. Новых зависимостей, configs или продуктовых решений нет.

- Исходный commit: `6b26a87cf1bd7e4b52cc8e9b756a1a23be2379f5` (`origin/main`, merge PR #16).
- Unity: **6000.3.28f1 (8f082d2fe476)**, локальный macOS Editor, build target iOS; fixed simulation **30 Hz**.
- Данные: `Assets/_Game/Configs/EightWave.asset`, его `sharedScenario` — сохранённый `TestWave.asset`. HP 20, Gold 150, Shade HP 100/Armor 0/Speed 1.5/LeakDamage 1, kill reward 5, wave bonus 20. Assets не подменяются `CreateDefault` или изменёнными checkpoints.
- Источник команд: `BattleElementalReplayTests.Prepare`, явная последовательность ниже. Выбрана вручную для покрытия интеграции после бюджетных проб, без автоматического поиска выигрышной стратегии. **RNG отсутствует**, seed неприменим; fixed spawn schedule, First targeting и стабильный порядок IDs.
- Прогон: `./Scripts/run-edit-mode-tests.sh`. `RegBattle126_CommittedBalanceEvidence_MatchesReplay` записывает фактический JSON в ignored `TestResults/P2.1e-replay-summary.actual.json` и сравнивает его посимвольно с [replay-summary.json](replay-summary.json). Тест не перезаписывает committed evidence.
- `RegBattle125_MixedReplay_IsDeterministicThroughNaturalTerminal`: два полных запуска, равенство каждого состояния после команды/Start/Next/Step, всех enemy HP/distance/status fields, tower type/level/investment/cooldown/Grid, stable checkpoint/next IDs, AttackEvents и выведенных Burn deaths. Дополнительно проверяется natural terminal и его неподвижность после Step.
- `RegBattle125_CheckpointContinueAndInterruptedCombat_MatchControl`: тот же сценарий с файловыми checkpoints во временном уникальном `TestResults/ElementalReplay` root, без доступа к пользовательским slots.

## Зафиксированная последовательность

Координаты — GridCoordinate; стоимость/остаток Gold получены из исполнения публичных команд. StartWave выполняется после каждой Preparation, Steps идут до естественной смены фазы, после WaveComplete выполняется один успешный Next Wave и один rejected repeat без mutation.

| Preparation волны | Команды | Gold после команд |
|---|---|---:|
| 1 | Place #1 FLAME (-2,-1), цена 70; Place #2 GALE (1,0), цена 80 | 0 |
| 2 | Нет покупок | 35 |
| 3 | Place #3 TIDE (-1,0), цена 60 | 10 |
| 4 | Нет покупок | 50 |
| 5 | Place #4 STONE (-3,-1), цена 90 | 0 |
| 6 | Нет покупок | 55 |
| 7 | Sell #3 TIDE: refund 42; repeated Sell rejected; Place #5 TIDE в ту же клетку за 60; Upgrade #5 до L2 за 90 | 2 |
| 8 | Нет покупок | 67 |

После rebuild ID **5**, nextTowerId **6**; удалённый #3 не переиспользуется. L2 TIDE имеет InvestedGold **150**, остальные башни L1. Upgrade сохраняет cooldown. Полные составы/уровни/вложения/NextAttackTick, next IDs и точные command receipts каждой волны находятся в JSON.

## Измеренные результаты — VERIFIED

**Defeat на волне 8, tick 3997, HP 0, Gold 112, Kills 46; 8 DoT kills.** Волны 1–7 завершены, восьмая прервана естественным смертельным leak. Victory не является обязательным assertion, конфиги/ожидания ради победы не менялись.

| Волна | Фаза | Tick | HP | Gold | Kills всего (за волну) | Leaks | DoT kills | Состав |
|---|---|---:|---:|---:|---:|---:|---:|---|
| 1 | WaveComplete | 399 | 18 | 35 | 3 (3) | 2 | 0 | FLAME L1, GALE L1 |
| 2 | WaveComplete | 828 | 15 | 70 | 6 (3) | 3 | 0 | FLAME L1, GALE L1 |
| 3 | WaveComplete | 1287 | 12 | 50 | 10 (4) | 3 | 2 | + TIDE L1 |
| 4 | WaveComplete | 1788 | 8 | 90 | 14 (4) | 4 | 2 | FLAME/GALE/TIDE L1 |
| 5 | WaveComplete | 2289 | 6 | 55 | 21 (7) | 2 | 1 | + STONE L1 |
| 6 | WaveComplete | 2850 | 3 | 110 | 28 (7) | 3 | 1 | Четыре типа L1 |
| 7 | WaveComplete | 3429 | 1 | 67 | 37 (9) | 2 | 1 | FLAME/GALE/STONE L1, TIDE L2 |
| 8 | Defeat | 3997 | 0 | 112 | 46 (9) | 1 | 1 | FLAME/GALE/STONE L1, TIDE L2 |

Всего 68 spawned = **46 kills + 20 leaks + 2 оставшихся живых**. После Defeat враги #67/#68 заморожены; их HP/позиции/статусы записаны в JSON. Leaks считаются по исчезнувшим врагам с исключением direct/DoT deaths, затем сверяются с потерей HP (у saved Shade LeakDamage 1). `leakDetails.beforeStep` содержит остаток HP/дистанцию/статусы непосредственно перед тиком leak, не точный HP после due Burn этого тика.

Экономический ledger: **150 − 360 (постройки) − 90 (Upgrade) + 42 (Sell) + 46×5 (kills) + 7×20 (волны) = 112**. Потеря на Sell/Rebuild L1 — **18 gold**. Defeat не выплатил финальный bonus. На каждой волне проверяется точный Gold через исходный Gold + kills×saved reward + bonus только при успешном завершении; каждый Step проверяет non-negative Gold.

| Статус | Наложения, включая refresh | Фактическое наблюдение / предел вывода |
|---|---:|---|
| Burn | 106 | Активный статус и его таймеры присутствуют в snapshots; 8 из 46 kills завершены due Burn до Movement. Это атрибуция последнего удара, не весь вклад FLAME в damage. |
| Wet | 68 | Активен с волны 3; на L1 slow 20%, на L2 25% по saved definitions. Замедление не предотвратило все leaks. Число спасённых HP без matched контрольного прогона не измерено. |
| Gust | 277 | Активен с волны 1; по контракту усиливает последующие direct hits на 20% у L1. Наложения не равны числу усиленных ударов; суммарный эффективный damage и выигрыш от бонуса здесь не измерялись. |
| Crack | 24 | Активен с волны 5. У всех saved Shade Armor 0: Crack и ArmorPenetration не могут уменьшить effectiveArmor ниже 0, поэтому этот матч не измеряет полезность STONE против брони. Direct burst STONE остаётся частью боя. |

`statusApplications` выводится из non-lethal public AttackEvents и definition атакующей башни; включает повторное наложение. Наличие всех четырёх **активных** видов отдельно проверяется через `EnemyState.Statuses`. DoT kills = прирост Kills за Step минус число lethal direct events; каждый такой kill дополнительно сверяется с enemy ID/HP и due Burn из состояния перед Step. `AttackEvent.Damage` — номинальный damage, поэтому не используется для суммирования эффективного урона. Контракт событий не менялся, новые runtime status events не добавлялись.

## Checkpoint evidence — VERIFIED

Восемь Preparation и семь WaveComplete проходят файловый Save/Load/Restore; каждый WaveComplete загружается дважды (**22 round-trips**). Полный exported checkpoint сравнивается до/после: type, level, investment, cooldown, nextEnemyId/nextTowerId и Grid. Восстанавливаются ненулевые cooldown и смешанные уровни L1/L2; Load не переписывает bytes primary. Повторный Next Wave отклоняется, Gold/Kills и весь snapshot остаются прежними. Сохранённый WaveComplete уже содержит свою однократную награду.

Прерывание волны 6: сохранена **Preparation tick 2289, HP 6, Gold 55, Kills 21**. Попытка дошла до **Combat tick 2353, Gold 60, Kills 22** с живыми врагами и активными статусами; ObservePhaseTransition не перезаписал slot. Restore вернул в точности Preparation: enemies/statuses/AttackEvents попытки удалены, HP/Gold/Kills/cooldown/next IDs откатились. Весь прерванный префикс воспроизведён поснимково и снова откатан; затем результат до Defeat совпал с контрольным прохождением без прерывания.

Terminal checkpoint намеренно не хранит оставшихся enemies/statuses: это прежний safe-wave контракт. Для идентичности дальнейшего матча восстанавливаются Preparation/WaveComplete; после Defeat нет следующего Continue. Сохранение terminal проверено существующим suite; живые enemies в terminal snapshots не выдаются за сериализованные.

## Наблюдения и гипотезы баланса

**Факты.** До появления STONE уже потеряно 12 HP; после волны 7 осталось 1 HP. На восьмой волне #66 сделал последний leak, тогда как #67/#68 ещё живы. Покупки откладываются из-за дохода от ограниченного числа kills; весь стартовый Gold потрачен до первой волны, перед волной 5 также осталось 0, после Upgrade на волне 7 — 2. По log перед leak у врагов остаётся положительное HP: они выходят из зоны достаточного урона, а не исчезают без объяснения.

**Гипотезы, требуют отдельных сравнений.** Раннее вложение в utility и общий First targeting могут оставлять хвост волны без достаточного покрытия. Стратегия откладывает damage upgrades и тратит 18 gold на демонстрационный Sell/Rebuild без смены клетки: это стоимость проверки, а не рекомендуемый ход игрока. Ранний Armor 0 делает роль Crack/penetration неразличимой; текущий legacy `test-tower` с 25 damage/2 fire rate за 60 выглядит сильнее элементальных L1 по номинальному single-target DPS, но скрыт в Build UI и не использовался здесь. Это потенциальные перекосы для будущего сравнения, не доказательство доминирующей сборки.

**Отдельное предложение, без изменения баланса:** провести matched прогоны с теми же ресурсами, отдельно меняя положение башен, порядок damage/utility upgrades и отказ от Sell/Rebuild; затем внешние плейтесты на понимание причин поражения. Ценность Crack проверить отдельно на согласованном armored encounter. Не менять цены/статы по одному scripted Defeat и не переносить power legacy tower в доступный каталог без продуктового решения. Победа и пригодность Normal для новичка здесь не доказаны.

## Quality gates

Точные команды из isolated worktree `feature/p2-1e-elemental-validation`:

```bash
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
git diff --check
```

Финальные локальные результаты (2026-11-15):

| Gate | Результат | SHA256 XML |
|---|---|---|
| `./Scripts/run-static-checks.sh` | 38/38 self-tests, project version/packages/meta/GUID/asmdefs PASS | — |
| EditMode | 317/317 PASS; failed/inconclusive/skipped = 0/0/0 | `15d19c7358350d5653f8bdf19ddb7e05b7086e57e78df55c9f4eb9f4c19b1f8a` |
| PlayMode | 15/15 PASS; failed/inconclusive/skipped = 0/0/0 | `ee3b16a56231e7808a70a32f4bb0ef6800d7deda09e10872a3a8a0cc2c258a42` |
| `git diff --check` | PASS после исключения побочных пересериализаций builder tests | — |

Оба suite имеют Total > 0, Passed == Total; failed/inconclusive/skipped = 0. Сохранённые исходные XML находятся в ignored `TestResults/P2.1e-final/`. Изменения ограничены новым integration test + `.meta`, двумя отчётами и четырьмя документами; Packages, configs, runtime и P2.1d screenshots побайтово совпадают с базой.

 Wrappers выбирают установленный Unity executable (нет resident Editor данного worktree): `-batchmode -nographics -projectPath "$PWD" -buildTarget iOS -runTests -testPlatform EditMode|PlayMode -testResults "$PWD/TestResults/<Mode>.xml" -logFile "$PWD/TestResults/<Mode>.log"`. Без `-quit`; оба XML проходят `Scripts/verify_unity_test_results.py`. Доступная license использована как есть, signing credentials не запрашивались. Время исполнения тестов не является performance evidence игры.

## История проверки / решения

Базовая версия: static **38/38**, EditMode **314/314**, полный PASS. Новые тесты сначала дали compile FAIL из-за несовместимого NUnit `AnyOf` и namespace `Application`; исправлен только тестовый код. Первые бюджетные пробы FLAME+TIDE не могли купить STONE на волнах 4/5 (на волне 5 Gold 70 при цене 90); это ошибка расписания команд, не runtime-дефект. FLAME+GALE позволил дойти дальше, но STONE у выхода наносила только lethal hits без Crack. Финальная последовательность переносит её ко входу для покрытия статуса, Upgrade выполняет TIDE L2 в рамках заработанных ресурсов. Эти пробы не являются полными balance runs и не учитываются в totals.

Ruling: сравнение live Defeat enemies после terminal restore исключено — safe-wave checkpoint по спецификации не сериализует enemies, а Continue предназначен для Preparation/WaveComplete. Вместо изменения runtime проверяется восстановление всех промежуточных фаз и полный дальнейший terminal.

После фиксации команд два replay-теста прошли; REG-126 ожидаемо упал из-за отсутствующего committed JSON (316/317). JSON скопирован из фактического output, исход Defeat сохранён. Runtime-дефектов, требующих production fix, не обнаружено. Существующие builder tests пересериализуют scenes/materials/URP в своей рабочей копии; эти побочные изменения исключены из PR, исходный checkout не изменялся.

## NOT VERIFIED / BLOCKED

Физический iPhone, реальные touch/safe area/background, force quit/relaunch, Simulator, iOS export/signing, device performance/allocations/thermal и Unity CI не проверялись. Автоматический Editor replay не является внешним плейтестом и не утверждает числа баланса. Реакции, гибриды, unlocks и armored enemies не входят в этот scope. Новых визуальных изменений нет; сохранены реальные Editor Play Mode [кадры P2.1d](../P2.1d/01-catalog.png), [preview](../P2.1d/02-preview.png), [бой](../P2.1d/03-status-combat.png), [Upgrade](../P2.1d/04-upgraded-panel.png), [Sell](../P2.1d/05-sell-confirmation.png), [Continue](../P2.1d/06-continued-tower.png) и [QA log](../P2.1d/qa.log). Они не заменяют on-device evidence. Self-merge и выпуск сборки не выполняются.
