# P2.2c — runtime Steam/Storm и transient damage telemetry

Scope: только Task 2 `Docs/P2.2A_IMPLEMENTATION_PLAN.md`. Base — `2b75d2172e2ad1eb38ac1a4b47bf01b76c290cd5` (merged #19). Ветка `codex/p2-2c-reaction-runtime`, отдельный managed worktree. Проверяемый source/test commit — `df3ba4614e82e873414576c498eaeebeea5e5ffa`; последующий documentation commit не меняет source/test trees. Точные tree IDs, SHA256 исходников/configs/reports и totals находятся в [gates.json](gates.json).

Rules label: `p2.2-steam-storm-v1`. Production reactions **не включены**. Все reaction fixtures синтетические; они не являются измерением saved EightWave с реакциями или утверждением баланса.

## Реализовано

- Pure C# resolver: incoming-status candidates, Steam priority/Storm fallback, intrinsic только своего вида, lethal/expiry/DoT/secondary suppression.
- Steam — primary + nearest три, inclusive 1.25; Storm — два дополнительных greedy hop по inclusive 2.0, полный план до damage. Double conversion до subtraction, distance²/EnemyId ties, bounded reused buffer из четырёх целей/позиций.
- Nullable read-only timestamps, shared primary/secondary per-kind cooldown 60 ticks, reserve всех выбранных участников до damage. No-neighbor Storm не расходует окно.
- Long scale → floor/saturation → armor/Crack → min 1 → HP clamp. Без Gust/penetration/status refresh/recursion во secondary; единый death/reward/bonus pipeline.
- Damage events для Direct/Burn/Reaction: captured positions, configured/raw/resolved/applied damage, zero-based общий sequence на Step, correlation/HitIndex. Старый AttackEvent остаётся configured base и direct-only WasLethal.
- Explicit clear, успешный Restore очищает transients, rejected Restore сохраняет их. SceneController отбрасывает новую telemetry без VFX и очищает все потоки в `finally` после каждого fixed Step, включая несколько Steps до render и consumer exception.
- Internal config factory + friend только EditMode tests; historical fixture в каждой точке создания/restore явно использует Empty и прежние definition objects. Старые числа/команды/JSON `Docs/review/P2.1e/*` не изменялись.

## Test-first и review

Первый test-only commit `b10562f`: 415 EditMode total, **378 passed / 37 expected failures**, без compile error, skipped или inconclusive. [RED output](red-results.json). Failures показывают отсутствие reaction damage, timestamps/events API и hybrid/global validation. Отдельный factory RED: 418 total, 417 passed / 1 failure `Missing historical test factory`; [краткие результаты](red-factory-and-mutations.json).

Read-only независимый reviewer проверил runtime/source `065113f357ee69d09e9d9a8f749d163da4d9a559`: runtime bugs не обнаружены, найдены два важных пробела покрытия. Они закрыты в `df3ba46`: Storm на изгибе пути отличает nearest от First; две башни в одном Step проверяют secondary→primary для обоих видов. [Review и исправления](review.md).

Дополнительная mutation-проверка: nearest→First вызывает 3 expected failures, включая новый Storm fixture. Принудительное float-округление delta перед double вызывает 1 expected failure в precision boundary test. Mutants восстановлены побайтово; production resolver не менялся. После восстановления **430/430 EditMode PASS**, включая 52 новых cases и все 378 прежних. Source commit для окончательных gates указан выше.

## Воспроизведение

Из отдельного checkout source commit, Unity **6000.3.28f1 (8f082d2fe476)**, macOS **27.0.1 (26A434)**, arm64:

```bash
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
git diff --check
```

Default `auto` выбрал batch backend: resident Editor этой worktree отсутствовал. Runner вызвал установленный Unity с `-batchmode -nographics -buildTarget iOS -runTests`, NUnit reports — `TestResults/EditMode.xml` и `TestResults/PlayMode.xml`. Это Editor/Mono tests при выбранном iOS build target; IL2CPP player/export/device не запускались. Unity/URP/packages не менялись.

Тесты builder/Unity import пересериализуют некоторые materials, scenes, prefab и RP asset в собственной worktree. После suite эти побочные изменения восстановлены из HEAD, затем проверено отсутствие diff у saved configs/scenes/prefabs/art/settings/packages. Исходный пользовательский checkout не изменялся. Static gate отдельно проверяет paired/orphan `.meta`, GUID, asmdefs, pinned versions/packages; review diff проверяет отсутствие новых зависимостей/секретов.

Точные финальные totals: [gates.json](gates.json); список результатов reaction/historical cases: [test-results.json](test-results.json). Требуется `Total > 0`, `Passed == Total`, failed/inconclusive/skipped = 0. Raw reports остаются в ignored TestResults; их SHA256 записаны, старые totals не подставлялись.

[traces.log](traces.log) получен из NUnit TestContext output сценариев, а не написан вручную. В cooldown traces сохранены first proc и текущий t+59/t+60; промежуточные direct-only строки опущены. Сценарии проверяют точные HP/timestamps/event counts до вывода trace. Для полного output повторить EditMode suite и открыть `<output>` нужного test-case.

## VERIFIED

Финальные static/EditMode/PlayMode и `git diff --check` имеют полный непустой PASS по gates.json. REG-132…145 сопоставлены фактическим tests в Docs/REGRESSION.md. Включены pair symmetry, expiry/lethal, cooldown/radii, same-tick secondary/primary и restored TowerId order, Storm chain/lethal intermediate/no-neighbor, double precision, armor/overkill/saturation, mixed-origin lethal/rewards/Defeat, event lifecycle и настоящий adapter Update. Historical P2.1e assertions проходят с прежним outcome/JSON; production public config остаётся Empty.

## NOT VERIFIED / BLOCKED

- Production wiring/hybrid saved configs/schema integration — Task 3, не входит в этот PR.
- Новые UI/VFX, визуальная читаемость и Play Mode screenshots реакций — Task 4, не реализованы здесь. PR не меняет визуальный результат.
- Full production reaction replay/rollback, matched impact и balance validation — Task 5. Historical replay сохранён отдельно.
- Physical iPhone, built-player touch/safe area/background, IL2CPP/Xcode export, performance/allocations/thermal и внешний плейтест не проверялись. Bounded target buffer и отсутствие LINQ/new collection per attack подтверждены source review, не profiler measurements.
- GitHub self-hosted Unity CI и независимое GitHub PR review ещё не подтверждены. Локальный read-only reviewer не заменяет approval владельца. Self-merge/release не выполнялись.
