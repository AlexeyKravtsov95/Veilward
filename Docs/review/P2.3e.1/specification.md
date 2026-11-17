# P2.3e.1 — ограниченный поиск и production verification

База: `082ee6f6868b5e124e2bcc915f8c882557ff3a6a`, merge PR31. Scope: test/editor utility, отдельные scripts/tests/evidence; runtime/config/packages/art/исторические golden не изменяются.

REG-BATTLE-188: конечный deterministic beam search A FLAME→TIDE→GALE и B GALE→TIDE→FLAME. Public EightWave factory, реальные150/20; все Available клетки из Grid в порядке y,x. Только public Choose/Place/Upgrade/Sell/Start/Next/Step, checkpoint только Export/Restore без редактирования. Выживание, экономика и реакции оцениваются отдельно. Лучшие scripts запускаются дважды с New Game, полный trace по REG186, legals и положительные AppliedHpLoss проверяются отдельно.

REG-BATTLE-189: при достижении production WaveComplete6 проверить pending/confirmed, Preparation7, file Continue до/после Confirm6, failed-save/restart/retry; Combat7 после kill+positive reaction — две одинаковые попытки и полный rollback. Контроль настоящими MainMenu/controllers, clean UI/VFX, реальные Play Mode screenshots/Console. Недостигнутое остаётся BLOCKED; synthetic существующие fixtures отдельно.

Предварительный бюджет: два прохода survival и coverage, для каждого старта до8 волн ×16000 wave simulations (максимум512000). Beam24, до2 последовательных операций в Preparation плюс атомарный кандидат Sell→Place, без RNG. Hold всегда допустимый кандидат; Upgrade/Build на разных Preparation, обычная продажа и перестановка. Полный перечень отсечений/счётчики/время публикуются после запуска. Ranking survival сначала фаза/HP/экономика, coverage сначала выживание и число положительных видов реакций. Это ограниченная эвристика, не доказательство глобальной непроходимости. Изменение бюджета/эвристики фиксировать как отдельный запуск.

Acceptance: точный source commit, commands/totals/hashes/config audit; полные static/EditMode/PlayMode/diff-check; P2.3 без DONE до полного acceptance и independent review. Device/G2/ART approval/баланс/TestFlight вне scope.
