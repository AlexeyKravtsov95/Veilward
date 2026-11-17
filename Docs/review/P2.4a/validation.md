# P2.4a — фактическая проверка документационного scope

База fresh `origin/main` **8128010051c2b048b32b0a48df5b862bb2d5a7da**, PR #32 `mergedAt=2026-11-17T15:04:32Z`, mergeSHA сверён через `gh pr view 32 --json state,mergedAt,mergeCommit,title,url` и `git fetch origin main`. Задача #35 и план #33 прочитаны полностью, включая #36–40. Все mandatory reads присутствуют. Изолированная ветка `codex/p2.4a-ability-boss`, worktree `/Users/alexeykravtsov/Desktop/workspace/Veilward-p24a`; исходный пользовательский checkout с локальными материалами/URP не изменялся.

## VERIFIED

- Созданы [контракт](../../P2.4A_ABILITY_BOSS.md) и [план/API/REG matrix](../../P2.4A_IMPLEMENTATION_PLAN.md). D-01…D-07 — предложения владельцу, не approval. REG190…217 —28 сценариев, только SPECIFIED. Ни тестов способности, ни нового босса этим PR не добавлено.
- Технический P2.3 отмечен DONE по merge #32; исторические P2.3e Defeat5 и P2.3e.1 Victory8/reports/traces/screenshots не переписаны. README/ROADMAP/TECH_DESIGN/REGRESSION и статусные баннеры P2.3a согласованы; device/G2/ART/баланс отдельно.
- `./Scripts/run-static-checks.sh` → **38/38**, project checks PASS (Unity version/revision, package baseline/lock, paired/orphan meta, GUID/asmdef). [Raw output](reports/static.txt).
- `./Scripts/run-edit-mode-tests.sh` → **761/761**, failed/inconclusive/skipped0. [NUnit XML gzip](reports/EditMode.xml.gz), [runner](reports/edit-runner.txt).
- `./Scripts/run-play-mode-tests.sh` → **45/45**, failed/inconclusive/skipped0. [NUnit XML gzip](reports/PlayMode.xml.gz), [runner](reports/play-runner.txt).
- `git diff --check` → PASS. Final scope/REG/link/hash audit: [audit.txt](audit.txt). Independent read-only technical [review](review.md), исправления документированы отдельно от product approval.

Окружение: macOS27.0.1 (26A434), arm64, Unity6000.3.28f1 (8f082d2fe476), Editor Mono, active target iOS, Unity CLI1.0.1;30Hz, RNG отсутствует. Оба runner `auto` использовали batch в новой worktree, без resident Editor. Точные UTC timestamps/totals/report hashes — [gates.json](gates.json). Static выполнен раньше Unity, без конфликтующих fake runner outputs. EditMode builder suites вызвали serialization drift scenes/materials/prefab/URP: в **этой isolated worktree** восстановлен committed `Assets` перед PlayMode; после PlayMode восстановлен только повторный URP drift. Final tree Assets/Packages/ProjectSettings/Scripts соответствует базе побайтово; никакой drift не включён в PR.

Это полный PASS существующих suites на неизменном runtime. Он подтверждает baseline compile/regressions, а не REG190+ или gameplay гипотезы. Документация проверена отдельным review и scope audit; новые runtime tests появятся в #36–40.

## NOT VERIFIED / BLOCKED

D-числа/правила требуют решения владельца перед соответствующим runtime PR. Ability/boss API, schema4/new content и новые REG пока только SPECIFIED; новые natural outcomes и баланс не измерены. Нет новой визуальной функции — новые Play Mode кадры не создавались; historical UI evidence сохранено. Physical iPhone, real touch/safe area/background/force quit, signing, iOS export/player, IL2CPP/Metal, profiling/performance/thermal, Unity self-hosted CI, G2, ART approval, TestFlight/release не проверялись и не закрываются. PR передаётся на review без self-merge/release. Issue #35 остаётся открытой до подтверждённого merge/acceptance; Project Status отдельно от статуса issue/PR.
