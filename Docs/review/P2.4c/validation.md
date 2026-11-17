# P2.4c — schema4 и сохранение способности (#37)

## Source / scope

Fresh origin/main после PR #66: `9adc17f251ad98b72304753cc0b69173e502c9c8`, fetch/merge проверены через git и GitHub issue #36. Отдельный worktree `/Users/alexeykravtsov/.codex/worktrees/p24c-ability-checkpoint/Veilward`, branch `codex/p2.4c-ability-checkpoint`; пользовательские изменения исходного checkout не затрагивались. D-01…D-07 приняты владельцем17.11.2026.

Scope: immutable AbilityCheckpoint, schema4 strict wire validation, migrations1/2/3→4, shared exact-ID routing, full restore/rollback и safe-save orchestration. Acceptance REG198–203; риски — mixed mode slots, partial restore, repeated reward/cast при Retry. Specification/новые regression tests созданы до implementation. [Schema/content/phase/IO matrix](matrix.md).

**Production ability-disabled.** Enabled saves проверяются только internal synthetic fixture `synthetic-p24-ability-v1`, вне production whitelist/MainMenu/New Game/Continue. Final mission/UI/boss не активированы — #38–40. Данные/баланс/волны/packages/исторические evidence не менялись.

## VERIFIED

Финальные totals/UTC timestamps/XML+source SHA256 и список новых cases: [gates.json](gates.json). Static **38/38**, EditMode **901/901** (69 новых cases), PlayMode **45/45**; оба Unity suite имеют failed/inconclusive/skipped **0/0/0**, exit0. `git diff --check` и `check_unity_project.py` PASS. Независимо повторно проверены оба raw XML и совпадение source manifest с текущими28 changed Assets. После I-01 повторены полные gates; EditMode также повторён на восстановленном serialized baseline.

Byte audit: **807** прежних файлов совпали побайтово, включая все исторические evidence/raw goldens, serialized assets/packages/settings и tests вне девяти явно перечисленных technical projections. [Результат](historical-audit.json), [воспроизводимая проверка](audit.py). Новых C# файлов5, каждый с `.meta`; GUID/version/packages gates PASS. Scope/secrets audit: только разрешённые Domain/infrastructure/orchestration/tests/docs; credentials и новые dependencies не добавлялись.

## RED → GREEN

1. Initial API RED: новые AbilityCheckpointTests/AbilityCheckpointStorageTests/AbilityCheckpointControllerTests с отсутствующими snapshot/wire/observer API. Unity compiler errors, XML отсутствует — **не выполненный suite**. [Runner log](red-api.log), [только compiler diagnostics](red-api-editor.log). В первом fixture также исправлены два ошибочных positional constructor arguments, не продуктовая ошибка.
2. Промежуточные full runs выявили потерю required ability в прежних direct JsonUtility projections и Unity inline-null defaults. Исправлены явный wire DTO и технические legacy fixture writers, создающие настоящий schema1/2 JSON без progression. Повреждённые пустые source objects по-прежнему rejected.
3. После компилируемой реализации отдельно воспроизведён прежний enabled Export unsupported guard: behavioral RED **894=843passed/51failed**, skipped/inconclusive0, все failures зависят от enabled export. Это replay старого guard после API RED, а не заявление, что готовая реализация отсутствовала к моменту прогона. [Raw XML gzip](red-edit.xml.gz), [runner](red-behavior.log).
4. Guard удалён, полный GREEN **894/894**. Затем добавлены семь случаев: completion без наблюдавшегося Combat, write/read-back/commit failures, запрет synthetic/final/unknown production sessions. Final full EditMode **901/901**. Окончательные static/PlayMode/diff/audit и source hashes — gates.json.

5. Независимый reviewer выявил I-01: старый Reg203 ошибочно восстанавливался после завершения волны. Новый assert дал **901=900passed/1failed** (Expected Combat, actual WaveComplete), [review RED XML](review-red-edit.xml.gz). Исправлен только новый regression: stop после cast kill + positive reaction при обязательном Combat, непустые buffers сохраняются; rejected controller restore оставляет dirty state, успешный controller restore в dirty/fresh заменяет snapshot; два prefix и весь continued trace (остаток той же волны + будущие волны) совпадают с отдельным control. Финальные raw prefix/control traces и hashes приложены отдельно. Production source не менялся. Повторные final gates заменяют прежние901/45 отчёты.

## Команды / среда

```bash
git fetch origin main
git worktree add -b codex/p2.4c-ability-checkpoint /Users/alexeykravtsov/.codex/worktrees/p24c-ability-checkpoint/Veilward origin/main
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
python3 Scripts/check_unity_project.py
python3 Docs/review/P2.4c/audit.py
git diff --check
```

Unity6000.3.28f1 (`8f082d2fe476`), Editor/Mono, target iOS, macOS arm64. Default auto→batch, `-batchmode -nographics -buildTarget iOS -runTests`, без `-quit`. XML UTC clock принадлежит host (в отчётах17.11 UTC), пользовательская дата18.11.2026 Asia/Novosibirsk. Точные OS/environment в gates.json.

Один промежуточный запуск столкнулся с заполненным диском, не принят как PASS. Удалены только generated cache текущей задачи; PackageCache текущего worktree заново создан APFS clone из read-only cache прежней worktree с тем же package lock. Соседние worktrees не изменялись. Затем полные gates перезапущены. После Unity suites восстановлен только serialization drift isolated assets. Static self-tests очищают TestResults, поэтому raw XML архивируются после каждого окончательного suite.

## Architecture / review

Domain `AbilityCheckpoint` — get-only immutable value; full checkpoint deep copy не разделяет mutable progression/army и создаёт независимый ability value. Infrastructure `WaveCheckpointWire` отделяет JsonUtility DTO от Domain. Историческая JsonUtility-проекция Domain checkpoint по-прежнему не содержит ability и сохраняет outcome/trace goldens; actual store schema4 всегда использует wire DTO с ability. Это техническая schema projection, не изменение исходов.

Store/MainMenu/config/session/controller используют WaveCheckpointCompatibility. Существующая production factory выбирает native/all-open для каждого отдельного normalized slot. В validator-only test path сохранён прежний exact `test-scenario-v1` и добавлен только exact enabled synthetic; arbitrary/final content не принят. Schema1/2 source progression не затирается defaults при повреждении; legacy test-tower поддержан. Load/Normalize не пишут files. Temp/read-back/atomic replace/recovery/reset сохраняют прежний контракт.

Restore валидирует полный candidate до публикации State/Grid/ability. Success чистит четыре stream, targets, cursor и event epoch; rejection сохраняет state и непустые streams. Controller Restored вызывает прежний Presentation reset только после success. Safe completion от Cast виден в explicit observer и LateUpdate без fixed Step; failed save не откатывает принятую команду. Start/Next eligibility проверяется до IO, Retry не dispatch-ит команду.

Независимое fresh-context review после открытия PR: [review.md](review.md). Initial I-01 исправлен с RED→GREEN; окончательное закрытие связано с corrective commit и повторными full gates. Self-review не заменяет независимое заключение.

## NOT VERIFIED / BLOCKED

- Physical iPhone, force quit/relaunch/background/touch/safe area, iOS player/IL2CPP/Metal/profiling/thermal не проверялись. Это Editor tests, не device proof.
- Enabled production content, ability UI/input/preview/VFX, final mission и boss factory ещё не активированы. Final-ID permutations — #39; natural ability+boss150/20 replay — #40. Synthetic fixture не доказывает баланс.
- G2, ART approval и self-hosted Unity CI отдельно открыты. Нового визуального результата нет, screenshots в этом persistence PR неприменимы.
- Projects field изменяется только при реальном доступе; connector не предоставляет Projects mutation API. Issue/индекс будут Review после PR, Done только после фактического merge/acceptance.
- Merge/release/TestFlight не выполнялись.
