# P2.3e — проверочный контракт

База: c1546c3282d9b5ce4bf67aa0465ac9138330c8cf, merge PR #30. Scope: только tests/evidence/docs, без изменения runtime, configs, packages и арта.

REG-186: command scripts A (Flame→Tide→Gale), B (Gale→Tide→Flame), Stone (Flame→Stone→Tide). Saved production factory, 150 Gold/20 HP, только публичные команды/Step. Два прогона каждого script до естественного terminal, сравнение полного trace (state/grid/IDs/statuses/events/receipts/progression). Каждая операция проверяет атомарность и Gold/investment/refund. Запрещённые покупки, Next до Confirm, повторный/stale Confirm не меняют state. Нулевой требуемый proc — FAIL coverage, даже при PASS детерминизма.

REG-187: реальные isolated file slots и production routing; целое состояние до/после Confirm3/6, failed save/restart и Retry, Combat4/7 rollback после kill+proc, repeated Load/Continue/Next, native↔legacy backup, schema1/2 с legacy hybrid army. Load не пишет bytes; restore очищает events/UI/VFX. Реальный MainMenu→Battle→Continue trace. Недостигнутые production milestones — BLOCKED, отдельно от synthetic unit coverage.

Gates: static, полные EditMode/PlayMode, diff --check; точные totals/report hashes. Evidence: scripts, traces, per-wave snapshots, первые права/покупки/procs, естественные outcomes, screenshots/Console audit. Сохранить исторические P2.1e/P2.2f/ART файлы. P2.3 не DONE до acceptance/review/merge; device/G2/баланс/арт/TestFlight отдельно.
