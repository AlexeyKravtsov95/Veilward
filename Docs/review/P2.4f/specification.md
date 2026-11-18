# P2.4f / #40 — спецификация интеграционной проверки

База: свежий main `a9a9ae4fd02e86a1d1f59ee689d4a941976e146e`, PR #69 merged. Дата:19.11.2026. Scope: только новые tests/evidence/docs; production1000HP/armor5, ability120/1.5/600, экономика, spawns, старые assets и evidence неизменны.

## Acceptance REG214–217

- REG214: по одному public-command script от реального New Game150Gold/20HP для FLAME и GALE. В **одном** прохождении каждого: Victory8, boss kill, ability AppliedHpLoss>0 и lethal ability hit, положительные AppliedHpLoss всех Steam/Storm × StatusPair/Intrinsic. Zero cell или Defeat означает FAIL, без объединения результатов разных scripts. Отдельный legal boss-leak Defeat control.
- REG214/216: два независимых полных replay каждого script; новый observer захватывает state/Grid/IDs/nextAttack/progression/ability, enemy HP/distance/statuses/reaction timestamps и четыре streams до clear, включая новые attribution/sequence/captured position/raw/resolved/applied/overkill. Cast boundary=после наблюдаемого tick, до следующего Step, expectedWave/tick/sequence/центр и receipt. Отдельные HP/Gold accounting assertions. Historical writer не изменяется.
- REG215: whole file-continued trace, реальные Store/Session/CheckpointController/MainMenu/Buttons, initial pending/confirmed, pending/confirmed3/6, Preparation7/8, terminal Continueoff, no-write repeated Load; failed completion/Choose/Next сохраняет live success, Retry только persistence. Corrupt primary/valid backup и ordered old/new/legacy permutations явно synthetic.
- REG215: явные Combat7/8 interruptions после accepted ability kill + positive reaction при живых enemies и непустых streams; rejected Restore сохраняет dirty state, successful dirty/fresh возвращает **весь** pre-Start snapshot и чистит Domain/Presentation transients. Два prefix и whole resumed match равны control. Нет ability-only durability/reward duplication.

## Конечный поиск (только opt-in)

Сначала R3 coverage seeds на production factory, обязательные winning assertions дают RED. Затем test-only поиск legal Preparation7/8 army reconstruction и cast policies; после него при необходимости вариация ранних placements/экономики. Максимум **20000 полных кандидатов на старт**, hard stop при первом полном winning coverage на старт; доп. wall cap600s на старт. Отдельные counters attempts/ticks/illegal/duplicates/pruned/victories/eligible; dedup по exact command plan и policy, pruning только нелегальных покупок/occupied/out-of-bounds/исчерпанного budget. Victory сохранять до ранжирования/pruning. Никаких edits checkpoint/live values и definitions. Committed script replay не запускает длинный поиск и не зависит от env.

## Gates / evidence

Baseline и финальные static/EditMode/PlayMode + отдельный graphical run, positive totals, raw XML; реальные ScreenCapture кадры и Console audit, test clock/injected data подписаны. Exact commands, host/Unity version, source/config/input/report/trace/media SHA256 и raw sizes; historical byte audit. Независимое review после PR. VERIFIED отдельно от NOT VERIFIED / BLOCKED. При неполном acceptance draft/Refs #40, обязательные assertions остаются FAIL. Device/G2/ART/release/TestFlight вне scope, self-merge запрещён.

Canonical REG217 — gates/evidence/history/review; REG216 — HP/Gold accounting и rejected exploit probes, выполняемые observer на каждой mutation. Имена suites следуют этим группам.

Поиск завершён20000×2, ни одной Victory. Независимые поздние controls строятся отдельно: пропуск Cast6, Cast7 после Step1, GALE Sell Storm7 (его intrinsic уже был ранее), Steam8. Это расширяет воспроизводимость rollback, не подтверждает Victory и не является продолжением balance-search.

Фиксированный replay лучших seeds выявил ошибку журнала поиска: запись `WaveElapsedTicks` после lethal Cast completion давала0. Исправлено захватом boundary **до** Cast, как в production replay. Gameplay decisions/outcomes40000 попыток от записи не зависели; исходный archive/report сохраняется с этим ограничением. Исправленные fixed-index replays сохраняют полные inputs/trace; дополнительный search не запускается.
