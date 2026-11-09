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

## P1.4a — Multi-Wave Lifecycle & Rewards

Новые сценарии используют отдельный восьмиволновый config либо явно созданные Domain definitions. Сохранённый `TestWave.asset` остаётся legacy single-wave: reward/bonus равны нулю, последняя фаза — `WaveComplete`. `EightWave.asset` использует общую карту/бой TestWave, 5 gold за Shade, 20 gold за успешную волну и финальный `Victory`.

| ID | Сценарий и ожидаемый результат | Автоматизация |
|---|---|---|
| REG-BATTLE-009 | Восемь волн проходят последовательно с WaveIndex 0…7; после нефинальных волн WaveComplete и ручной Continue, после восьмой Victory. | `BattleWaveLifecycleTests.EightWaves_CompleteInOrderAndFinishWithVictory` |
| REG-BATTLE-010 | Spawn offsets второй и последующих волн отсчитываются от tick их StartWave; глобальный Tick не сбрасывается. | `BattleWaveLifecycleTests.SecondWave_UsesSpawnTicksRelativeToWaveStart` |
| REG-BATTLE-011 | Continue увеличивает WaveIndex ровно один раз; повторные Start/Continue не создают enemies и не меняют состояние после rejected result. | `BattleWaveLifecycleTests.WaveCommands_AreIdempotentAndRejectedCommandsDoNotMutate` |
| REG-BATTLE-012 | Gold/HP/Towers/Kills и глобальные cooldown сохраняются между волнами; строительство разрешено в новой Preparation. | `BattleWaveLifecycleTests.BetweenWaves_PreservesMatchStateAndAllowsPlacement` |
| REG-BATTLE-013 | EnemyId и TowerId уникальны и монотонны в пределах полного матча. | `BattleWaveLifecycleTests.Ids_RemainUniqueAcrossAllWaves` |
| REG-BATTLE-014 | Kill начисляет configured Reward и Kills ровно один раз даже при нескольких атаках одного тика. | `BattleWaveLifecycleTests.KillReward_IsGrantedExactlyOnce` |
| REG-BATTLE-015 | Leak не начисляет Gold/Kills; wave bonus начисляется один раз, включая финальную Victory. | `BattleWaveLifecycleTests.LeakAndWaveBonuses_FollowEconomyInvariants` |
| REG-BATTLE-016 | Defeat имеет приоритет над завершением, не даёт wave bonus и вместе с Victory замораживает Step/transition commands. | `BattleWaveLifecycleTests.DefeatAndVictory_AreTerminalAndDefeatPaysNoBonus` |
| REG-BATTLE-017 | Недостаток денег, occupied/blocked/invalid phase и rejected wave commands не меняют snapshot. | `BattleWaveLifecycleTests.RejectedCommands_PreserveCompleteState` |
| REG-BATTLE-018 | Reward/bonus неотрицательны; Gold никогда не становится отрицательным и насыщается на `int.MaxValue` без overflow. | `BattleWaveLifecycleTests.Rewards_CannotOverflowGold` |
| REG-BATTLE-019 | Два одинаковых полных восьмиволновых матча дают одинаковые snapshots и AttackEvents. | `BattleWaveLifecycleTests.IdenticalFullMatches_ProduceIdenticalSnapshotsAndEvents` |
| REG-BATTLE-020 | Сохранённый TestWave сохраняет прежние 150 Gold, 15 HP, 0 Kills и WaveComplete. | `BattleRegressionTests` (REG-BATTLE-001…008) |
| REG-BATTLE-021 | EightWave содержит counts 5…12, reward 5, bonus 20 и использует общий Path/Tower без копии маршрута. | `BattleScenarioConfigTests.EightWaveAsset_UsesSharedMapAndConfiguredWaveSequence` |
| REG-BATTLE-022 | Battle HUD отображает Wave current/count, Domain stats, Phase и Victory/Defeat result; кнопки доступны только в допустимых фазах. | `BattleWavePresentationTests` |
| REG-BATTLE-023 | Battle builder/validator сохраняют TestWave, создают EightWave и валидные references без Missing Scripts и с воспроизводимым output. | `BattleAssetValidationTests`; `BattleSceneBuilderTests` |
| REG-BATTLE-024 | Сохранённая Battle scene запускается в Play Mode с Preparation, WaveIndex 0, WaveCount 8; реальные screenshots фиксируют пять фаз матча. | `BattleSmokeTests.Battle_LoadsIntoPreparation`; `Docs/review/P1.4a` |

## P1.4b — Tower Economy

Новые сценарии используют три serialized уровня Test Tower: стоимость 60/50/80, Damage 25/35/50, Range 3 и FireRate 2. Upgrade/Sell разрешены только в Preparation; rejected commands сохраняют полный snapshot. Refund равен `InvestedGold * 70 / 100` с округлением вниз.

| ID | Сценарий и ожидаемый результат | Автоматизация |
|---|---|---|
| REG-BATTLE-025 | Upgrade L1→L2 списывает 50, сохраняет TowerId/coordinate/NextAttackTick, задаёт Level 2, Damage 35 и InvestedGold 110. | `BattleTowerEconomyTests.Upgrade_LevelOneToTwo_UpdatesStatsAndInvestment` |
| REG-BATTLE-026 | Повторный Upgrade L2→L3 списывает 80, задаёт Level 3, Damage 50 и InvestedGold 190; Range/FireRate остаются configured. | `BattleTowerEconomyTests.Upgrade_LevelTwoToThree_UpdatesStatsAndInvestment` |
| REG-BATTLE-027 | Upgrade после L3 возвращает MaxLevel и не меняет состояние. | `BattleTowerEconomyTests.Upgrade_MaxLevel_IsRejectedWithoutMutation` |
| REG-BATTLE-028 | Недостаток Gold возвращает InsufficientGold без списания и изменения башни. | `BattleTowerEconomyTests.Upgrade_InsufficientGold_IsRejectedWithoutMutation` |
| REG-BATTLE-029 | Несуществующий TowerId отклоняет Upgrade/Sell как TowerNotFound. | `BattleTowerEconomyTests.Commands_UnknownTowerId_AreRejectedWithoutMutation` |
| REG-BATTLE-030 | Upgrade/Sell в Combat отклоняются как InvalidPhase без изменения snapshot. | `BattleTowerEconomyTests.Commands_Combat_AreRejectedWithoutMutation` |
| REG-BATTLE-031 | Upgrade/Sell в Victory и Defeat отклоняются; terminal state заморожен. | `BattleTowerEconomyTests.Commands_TerminalPhases_AreRejectedWithoutMutation` |
| REG-BATTLE-032 | Sell L1/L2/L3 начисляет ровно 42/77/133 от InvestedGold 60/110/190. | `BattleTowerEconomyTests.Sell_AllLevels_GrantsExactFloorRefund` |
| REG-BATTLE-033 | Повторная продажа возвращает TowerNotFound и не начисляет Gold повторно. | `BattleTowerEconomyTests.Sell_Repeated_DoesNotGrantRefundTwice` |
| REG-BATTLE-034 | После Sell принадлежащая клетка становится Available; Blocked соседних клеток не меняется. | `BattleTowerEconomyTests.Sell_ReleasesOnlyOwnedCell` |
| REG-BATTLE-035 | На освобождённой клетке разрешена новая покупка с обычным списанием Level 1 price. | `BattleTowerEconomyTests.SellThenPlace_RebuildsOnReleasedCell` |
| REG-BATTLE-036 | Rebuild получает новый монотонный TowerId; проданный ID не переиспользуется. | `BattleTowerEconomyTests.SellThenPlace_RebuildsOnReleasedCell` |
| REG-BATTLE-037 | Internal Grid release отклоняет Blocked и повторный Available без изменения состояния. | `BattleGridTests.Release_OnlyOccupiedCell_Succeeds` |
| REG-BATTLE-038 | Upgrade сохраняет NextAttackTick между волнами и не даёт бесплатную атаку. | `BattleTowerEconomyTests.Upgrade_PreservesCooldownBetweenWaves` |
| REG-BATTLE-039 | Последующая атака upgraded башни публикует configured Damage нового уровня; First и порядок атак не меняются. | `BattleTowerEconomyTests.Upgrade_SubsequentAttack_UsesNewDamage` |
| REG-BATTLE-040 | InvestedGold не переполняется, Gold остаётся в диапазоне 0…int.MaxValue, refund/credit насыщаются без overflow. | `BattleTowerEconomyTests.EconomyOperations_CannotOverflow` |
| REG-BATTLE-041 | MaxLevel, InsufficientGold, TowerNotFound, InvalidPhase и Grid errors оставляют полный snapshot включая Level/InvestedGold/cooldown/grid/events неизменным. | `BattleTowerEconomyTests.RejectedCommands_PreserveCompleteState` |
| REG-BATTLE-042 | Два полных восьмиволновых матча с Upgrade/Sell/rebuild дают одинаковые snapshots и AttackEvents и заканчиваются Victory. | `BattleTowerEconomyTests.IdenticalFullMatchesWithEconomy_ProduceIdenticalTimelines` |
| REG-BATTLE-043 | Все REG-BATTLE-001…024 и прежние Domain/config/scene tests сохраняются без подгонки legacy ожиданий. | Полный EditMode/PlayMode suite |
| REG-BATTLE-044 | Play Mode: occupied selection, L1/L2/L3 stats, Upgrade affordability/max state, Sell confirmation/refund, удаление View и rebuild новым ID. | `BattleSmokeTests.Battle_TowerEconomyFlow_UpgradesSellsAndRebuilds`; `Docs/review/P1.4b` |
| REG-BATTLE-045 | ConfirmSell без Request возвращает ConfirmationRequired без изменения Gold/Grid; при активном диалоге Upgrade, Sell, placement Confirm/Cancel, Start/Next Wave заблокированы в API и кнопках. | `BattleSellConfirmationTests.ConfirmSell_WithoutRequest_DoesNotMutateDomain`; `SellConfirmation_BlocksConflictingActionsAndButtons`; `BattleSellConfirmationPlayModeTests.Battle_SellConfirmation_EnforcesModalContract` |
| REG-BATTLE-046 | Pending confirmation привязано к TowerId; после смены selection нельзя продать другую башню или изменить Gold/Grid. | `BattleSellConfirmationTests.ConfirmSell_AfterSelectionChange_DoesNotSellAnotherTower`; PlayMode modal contract |
| REG-BATTLE-047 | Cancel закрывает confirmation без Domain mutation и немедленно восстанавливает обычные действия и кнопки. | `BattleSellConfirmationTests.CancelSellConfirmation_RestoresActionsAndPreservesCombatStats`; PlayMode modal contract |
| REG-BATTLE-048 | Успешная подтверждённая продажа начисляет refund один раз, освобождает клетку и очищает selection/confirmation. | `BattleSellConfirmationTests.ConfirmedSell_ClearsSelectionAndConfirmation`; PlayMode modal contract |
| REG-BATTLE-049 | В Combat occupied selection продолжает показывать актуальные характеристики, но Upgrade/Sell и confirmation остаются недоступны. | `BattleSellConfirmationTests.CancelSellConfirmation_RestoresActionsAndPreservesCombatStats` |
| REG-BATTLE-050 | Измерение Tower Stats использует изолированный preview-scene экземпляр, восстанавливает ресурсы в finally и не меняет текст или dirty state сохранённой Battle scene. | `BattleAssetValidationTests.SavedBattleScene_TowerStatsRect_FitsAllEconomyLines`; `SavedBattleScene_TowerStatsMeasurement_DoesNotMutateScene` |

## P1.4c — Gameplay Validation & Balance Baseline

| ID | Сценарий и ожидаемый результат | Автоматизация |
|---|---|---|
| REG-BATTLE-051 | Два replay сохранённого EightWave с одинаковыми начальными условиями и последовательностью публичных команд дают одинаковые состояния на каждом тике, Gold, TowerId/EnemyId, AttackEvents и terminal result. | `BattleGameplayReplayTests.SavedFlexibleReplay_ProducesIdenticalCompleteTimelines` |
| REG-BATTLE-052 | Стратегии Tower Expansion, Tower Upgrades и Flexible Economy используют неизменённый saved EightWave и штатные Domain API; журналы содержат пословновые Gold/HP/kills/leaks, экономические команды и фактический Victory/Defeat без гарантии искусственной победы. | `BattleGameplayReplayTests.SavedStrategies_ProduceReproducibleMechanicalResults`; `Docs/review/P1.4c` |
| REG-BATTLE-053 | Editor Play Mode подтверждает полный UI cycle: build/Confirm, wave start, selection, L1→L2→L3, Sell Confirm/Cancel, rebuild, Next Wave и фактический terminal; landscape screenshots показывают ключевые фазы и последнюю волну без обрезаний HUD. | `Docs/review/P1.4c`; реальный Play Mode QA |

## CI-сценарии

| ID | Сценарий и ожидаемый результат | Автоматизация |
|---|---|---|
| REG-CI-001 | Static checks остаются на GitHub-hosted runner для PR/push в `main`. Unity EditMode/PlayMode запускаются только вручную при `github.ref == refs/heads/main`, на выделенном `self-hosted` macOS runner с label `veilward-unity-personal` и read-only permissions. Checkout закреплён на `github.sha`, фактический HEAD обязан совпасть с ожидаемым SHA. | `UnityWorkflowContractTests.test_static_quality_gates_keep_hosted_pr_and_push_coverage`; `test_unity_tests_are_manual_main_on_dedicated_macos_runner` |
| REG-CI-002 | Unity workflow не использует GameCI, license/account secrets, serial или ALF/ULF activation. Оба suite вызывают общий runner с явным `batch` backend, независимую проверку полного NUnit PASS и публикацию diagnostics. | `UnityWorkflowContractTests.test_unity_ci_does_not_use_license_secrets_or_game_ci`; `test_unity_tests_validate_and_publish_both_reports` |
| REG-CI-003 | Явный `batch` backend создаёт NUnit XML и log для EditMode и PlayMode без Unity CLI JSON. Default `auto` сохраняет локальный CLI path. Batch отклоняет live Editor descriptor или доступный resident Editor и не запускает второй Editor. | `UnityBatchRunnerTests.test_batch_backend_produces_nunit_xml_for_both_modes`; `test_batch_backend_refuses_live_editor_descriptor`; `test_batch_backend_refuses_resident_editor`; `UnityRunnerTests.test_play_mode_uses_async_run_and_polls_completed_result` |
| REG-CI-004 | Missing/stale, failed, skipped, inconclusive и empty NUnit reports, а также nonzero Unity exit при внешне passing report завершают batch runner ошибкой. | `UnityBatchRunnerTests.test_batch_backend_rejects_invalid_reports`; `test_batch_backend_rejects_nonzero_unity_exit` |
| REG-CI-005 | Unity CLI envelope принимается только со строгим boolean `success: true`; `false`, `null`, строки и числа отклоняются при финальной проверке и PlayMode polling. Без envelope разрешены только raw `status: running` для продолжения polling и `status: completed` с корректным summary для результата. Во время Play Mode domain reload временный `EDITOR_NOT_RUNNING` повторно опрашивается до общего deadline; остальные CLI errors прекращают run. Missing/invalid summary, failed/skipped/empty и несовпадающие counts завершают runner ошибкой; batch XML path не меняется. | `UnityRunnerTests.test_edit_mode_valid_envelope_is_accepted`; `test_play_mode_valid_raw_completed_response_is_accepted`; `test_edit_mode_envelope_rejects_non_boolean_success`; `test_play_mode_polling_rejects_non_boolean_success`; `test_play_mode_raw_running_is_not_accepted_as_completed`; `test_missing_or_invalid_summary_is_rejected`; `test_failed_skipped_empty_and_mismatched_reports_are_rejected`; `UnityBatchRunnerTests.test_batch_backend_produces_nunit_xml_for_both_modes` |

## Правила сопровождения

- Новая игровая функция добавляет или обновляет сценарий до implementation и сопровождается автоматическим regression test.
- При намеренной смене баланса сначала отдельно утверждается новый контракт; нельзя менять ожидания только для получения зелёного suite.
- Integration tests не подменяют сохранённый config значениями по умолчанию и не используют internal Domain mutation.
- Полный suite обязан быть non-empty; failed, inconclusive и skipped не считаются PASS.
- REG-BATTLE-007 фиксирует уже существующее P1.3 покрытие. Тесты визуализации выполняются в EditMode и не заменяют реальные Play Mode screenshots для визуальных PR.
