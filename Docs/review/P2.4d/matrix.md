# P2.4d — input / lifecycle / persistence matrix

Все строки покрывают **synthetic** enabled session и существующие production controllers. Полные имена и results — [new-tests.json](new-tests.json). REG204–208 — [spec](../../P2.4D_ABILITY_UI.md).

| Граница | Проверенное поведение | Где |
|---|---|---|
| Begin/target/Confirm | Без preselect; отдельный Confirm intent после Step; второй spawn tick1 включён:2hits/240HP, sequence+1 один раз | RegBattle204_RealButtonsConfirmAfterStepsExactlyOnce |
| Empty/Cancel/disabled | Нет cast/cooldown; отказ потребляет lease; production button скрыт | RegBattle204_CancelOrEmptyNeverCastsAndConsumesLease / DisabledProductionAndNonCombatRejectPublicApi; RegBattle208_ProductionUsesWiredControllersButAbilityIsHidden |
| Moving target | Step5000 уводит цели; Refresh даёт NoTargets/red _BaseColor/Confirm disabled; forced callback тоже не кастует | RegBattle204_TargetMovedAfterPreviewRejectsAtDispatch |
| Lethal Step | Накопленный Update Step даёт Defeat раньше queued intent; sequence остаётся1 | RegBattle204_AccumulatedLethalStepPrecedesIntent |
| Foreign owners | Placement/Sell/Inspector/Choice/Pause блокируют Ability API/Buttons | RegBattle205_ForeignOwnerBlocksAbilityCallbacksAndDirectApi |
| Ability owner | Заблокированы placement/inspector/Place/Upgrade/Sell/Choose/Start/Next/Retry и чужие real callbacks; whole snapshot unchanged | RegBattle205_AbilityOwnerBlocksAllForeignMutationAndRetryCallbacks |
| Lease/reentrancy | Stale token не трогает новый preview; double onClick один intent/cast | RegBattle205_StaleCallbackCannotCastNewGeneration; RealButtonsConfirmAfterStepsExactlyOnce |
| Pointer lifetime | Mouse/touch Down меняют центр, Up только освобождает; второй pointer/held через Pause не выбирает; release+новый Down выбирает | RegBattle204_EnabledButtonsWorldMouseAndTouchDispatcherMoveWithoutCast; RegBattle205_TwoPointersAndHeldThroughPauseRequireReleaseNewDown |
| Real Input System | Queued Mouse и Touchscreen, UI Down→world Up без выбора; новый world Down выбирает через production polling Update | RegBattle205_QueuedMouseAcceptsFreshDownAfterOwnerSwitch / QueuedTouchUiDownCannotBecomeWorldTapOnUp |
| Pause/lifecycle | manual/background/focus инвалидируют preview/intent; focus не Resume, clock/cooldown/VFX freeze; первый resumed Update без catch-up | RegBattle206_PauseInvalidatesQueuedIntentAndLease / PauseFreezesAbilityEffectsAndCooldownNoAutoResume |
| Choice3/6 от Cast | Обычные commands/Steps→cast completion, обязательный owner без preselect | RegBattle206_CompletionOpensMandatoryChoiceWithoutPreview(2)/(5) |
| Event exception | Четыре непустых streams очищаются в finally даже при fault-injected uninitialized reaction consumer | RegBattle207_AllFourStreamsClearEvenWhenConsumerThrows |
| Same-tick lethal | Captured impact после удаления; same-tick view не остаётся; summary фактический100HP | RegBattle207_ResetClearsIntentAndSameTickLethalView; graphical failed-save кадры |
| Restore | Rejected snapshot сохраняет intent; successful dirty/fresh whole restore чистит lease/UI/VFX | RegBattle207_RejectedRestorePreservesIntentSuccessfulRestoreClearsIt / LethalCompletionFailedSaveRetryAndDirtyFreshContinue |
| Failed save/Retry | Успех Cast/kill/bonus остаётся live; Retry только persistence | RegBattle207_LethalCompletionFailedSaveRetryAndDirtyFreshContinue; оба RegBattle208_Visual*Scenario |
| Builder/assets/safe area | Saved hidden wiring, canonical Build×2, validator refs,10safe-area controls;22реальных wide/narrow кадров | Полный existing builder/validator suite + RegBattle208_* |

Общие historical reaction tests сохраняют multi-Step-before-render контракт; новый consume merge добавляет AbilityEvents по общему SequenceInTick. Tests не подменяют физическое устройство или final boss content.
