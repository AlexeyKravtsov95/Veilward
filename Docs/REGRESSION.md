# VEILWARD — Battle & CI Regression Contract

## Назначение и источник данных

Этот набор фиксирует наблюдаемый контракт текущего vertical slice без изменения игровых правил. Все интеграционные сценарии используют сохранённый `Assets/_Game/Configs/TestWave.asset` и публичный API `BattleScenarioConfig.CreateSimulation()` / `BattleSimulation`.

Базовые значения: Preparation, tick 0, 150 gold, 20 HP, цена башни 60, Range 3, FireRate 2, Damage 25. Маршрут имеет длину 14 path units; пять Shade по 100 HP двигаются со скоростью 1.5 units/second, spawn ticks 0/30/60/90/120, leak damage 1.

## Обязательные сценарии

| ID | Сценарий и ожидаемый результат | Автоматизация |
|---|---|---|
| REG-BATTLE-001 | При загрузке сохранённого config: Preparation, tick 0, 150 gold, 20 HP, нет башен и врагов, Kills 0; путь длиной 14. | `BattleRegressionTests.InitialState_SavedScenario_MatchesRegressionContract` |
| REG-BATTLE-002 | Строительство `test-tower` на свободной клетке `(-3,0)` успешно, создаёт TowerId 1 и списывает ровно 60: остаётся 90 gold. | `BattleRegressionTests.PlacementAndRejectedCommands_PreserveEconomyAndGrid` |
| REG-BATTLE-003 | Строительство на дороге `(-3,-2)` отклоняется как Blocked без списания. Повторная команда в occupied `(-3,0)` отклоняется как Occupied, сохраняет исходный TowerId и 90 gold. | `BattleRegressionTests.PlacementAndRejectedCommands_PreserveEconomyAndGrid` |
| REG-BATTLE-004 | `StartWave()` создаёт первого Shade на tick 0. Далее появляются ровно пять Shade с уникальными EnemyId 1…5 на ticks 0/30/60/90/120. | `BattleRegressionTests.NoTowerWave_SpawnsFiveUniqueShadeOnSchedule` |
| REG-BATTLE-005 | Движение фиксировано на 0.05 path units за тик; первый Shade после первого `Step()` имеет distance 0.05. Leak удаляет врага и уменьшает HP ровно один раз. | `BattleRegressionTests.NoTowerWave_MovesLeaksAndCompletes` |
| REG-BATTLE-006 | Без башен происходят ровно пять последовательных leaks; волна завершается на последнем leak в WaveComplete с 15 HP, 150 gold, нулём Kills и пустым списком врагов. После terminal phase `Step()` ничего не меняет. Точные boundary ticks не округляются в спецификации из-за накопления float distance. | `BattleRegressionTests.NoTowerWave_MovesLeaksAndCompletes` |
| REG-BATTLE-007 | После завершения P1.3 выбор первой цели, cooldown, атака/урон, one-time kill, точный AttackEvent и beam position проверяются отдельно от P1.2 regression. | `BattleCombatTests`; `BattleCombatVisualTests`; `TowerPlacementTests` |
| REG-BATTLE-008 | Два запуска сохранённого scenario с одинаковой командой размещения дают полный одинаковый timeline каждого тика до terminal phase: phase/tick/HP/gold/Kills, все enemy/tower states и все AttackEvents. | `BattleRegressionTests.IdenticalSavedScenarioRuns_ProduceIdenticalCompleteTimelines` |

## CI-сценарии

| ID | Сценарий и ожидаемый результат | Автоматизация |
|---|---|---|
| REG-CI-001 | Static checks остаются на GitHub-hosted runner для PR/push в `main`. Unity EditMode/PlayMode запускаются только вручную при `github.ref == refs/heads/main`, на выделенном `self-hosted` macOS runner с label `veilward-unity-personal` и read-only permissions. Checkout закреплён на `github.sha`, фактический HEAD обязан совпасть с ожидаемым SHA. | `UnityWorkflowContractTests.test_static_quality_gates_keep_hosted_pr_and_push_coverage`; `test_unity_tests_are_manual_main_on_dedicated_macos_runner` |
| REG-CI-002 | Unity workflow не использует GameCI, license/account secrets, serial или ALF/ULF activation. Оба suite вызывают общий runner с явным `batch` backend, независимую проверку полного NUnit PASS и публикацию diagnostics. | `UnityWorkflowContractTests.test_unity_ci_does_not_use_license_secrets_or_game_ci`; `test_unity_tests_validate_and_publish_both_reports` |
| REG-CI-003 | Явный `batch` backend создаёт NUnit XML и log для EditMode и PlayMode без Unity CLI JSON. Default `auto` сохраняет локальный CLI path. Batch отклоняет live Editor descriptor или доступный resident Editor и не запускает второй Editor. | `UnityBatchRunnerTests.test_batch_backend_produces_nunit_xml_for_both_modes`; `test_batch_backend_refuses_live_editor_descriptor`; `test_batch_backend_refuses_resident_editor`; `UnityRunnerTests.test_play_mode_uses_async_run_and_polls_completed_result` |
| REG-CI-004 | Missing/stale, failed, skipped, inconclusive и empty NUnit reports, а также nonzero Unity exit при внешне passing report завершают batch runner ошибкой. | `UnityBatchRunnerTests.test_batch_backend_rejects_invalid_reports`; `test_batch_backend_rejects_nonzero_unity_exit` |

## Правила сопровождения

- Новая игровая функция добавляет или обновляет сценарий до implementation и сопровождается автоматическим regression test.
- При намеренной смене баланса сначала отдельно утверждается новый контракт; нельзя менять ожидания только для получения зелёного suite.
- Integration tests не подменяют сохранённый config значениями по умолчанию и не используют internal Domain mutation.
- Полный suite обязан быть non-empty; failed, inconclusive и skipped не считаются PASS.
- REG-BATTLE-007 фиксирует уже существующее P1.3 покрытие. Тесты визуализации выполняются в EditMode и не заменяют реальные Play Mode screenshots для визуальных PR.
