# P2.4d / #38 — проверка UI способности

Дата:18.11.2026 (Asia/Novosibirsk). Base `5af212101d8528a6756197765268a7f6c1220835`; GitHub PR #67 `merged=true`, head `dc99a52` входит в main. Изолированные branch/worktree `codex/p2.4d-ability-ui`, `/Users/alexeykravtsov/.codex/worktrees/p24d-ability-ui/Veilward`; исходный пользовательский checkout не изменялся.

Проверенный source: **`2ad43c5dd7d8c8aec206b7610d562a6416f614b7`**. Последующие commits — только docs/evidence. [Команды, среда, raw report SHA256 и totals](gates.json), [source SHA256](source-sha256.json), [новые tests/cases](new-tests.json), [input matrix](matrix.md), [независимое review](review.md).

## VERIFIED

| Gate | Фактический результат |
|---|---|
| Main baseline | Static38/38, EditMode901/901, PlayMode45/45 |
| Initial RED | EditMode901 passed/14 failed (915total), PlayMode45passed/1failed (46total); все новые cases ожидаемо падали на отсутствующем AbilityController/wiring |
| Final full static | **38/38**, version/revision/packages/meta/GUID/asmdefs PASS |
| Final full EditMode | **922/922**, +21cases к base |
| Final full PlayMode | **54/54**, +9cases к base |
| Graphical Visual | **2/2**,22реальных кадров,11×1600×900 +11×1280×960 |
| Compile | Оба полных Unity suites скомпилировались и выполнились на source SHA |
| diff/scope | `git diff --check` PASS; Packages/ProjectSettings/configs и existing tests не менялись; нет новых dependencies/secrets; import/builder drift возвращён только в собственной worktree |

Во всех итоговых suites failed/inconclusive/skipped **0/0/0**, nonempty positive totals. Первоначальные RED не считаются PASS. Позднее добавленные boundary cases не заявляются как часть initial RED. Raw NUnit reports: [EditMode](EditMode.xml.gz), [PlayMode](PlayMode.xml.gz), [Visual](Visual.xml.gz), baseline/RED находятся рядом. Исторические tests/goldens/evidence проверяются побайтово скриптом; точный count в `gates.json`. Saved Battle создан реальным Editor builder; canonical reproducibility/validator выполнялись в полном EditMode suite. Генератор меняет local fileIDs, поэтому большой YAML diff Battle не означает замену GUID; assets `.meta` сохранены. Соседние materials/prefabs/MainMenu восстановлены до исходных bytes.

## Реальные Play Mode кадры и Console

| Состояние | Wide1600×900 | Narrow1280×960 |
|---|---|---|
| Ready | [кадр](screenshots/1600x900/01-ready.png) | [кадр](screenshots/1280x960/01-ready.png) |
| Target preview | [кадр](screenshots/1600x900/02-preview.png) | [кадр](screenshots/1280x960/02-preview.png) |
| Новый центр | [кадр](screenshots/1600x900/03-moved-center.png) | [кадр](screenshots/1280x960/03-moved-center.png) |
| Confirm/hits | [кадр](screenshots/1600x900/04-confirm-hits.png) | [кадр](screenshots/1280x960/04-confirm-hits.png) |
| Cooldown | [кадр](screenshots/1600x900/05-cooldown.png) | [кадр](screenshots/1280x960/05-cooldown.png) |
| Pause | [кадр](screenshots/1600x900/06-pause.png) | [кадр](screenshots/1280x960/06-pause.png) |
| Resume | [кадр](screenshots/1600x900/07-resume.png) | [кадр](screenshots/1280x960/07-resume.png) |
| Cancel | [кадр](screenshots/1600x900/08-cancel.png) | [кадр](screenshots/1280x960/08-cancel.png) |
| Failed save/live kill | [кадр](screenshots/1600x900/09-failed-save.png) | [кадр](screenshots/1280x960/09-failed-save.png) |
| Retry | [кадр](screenshots/1600x900/10-retry-saved.png) | [кадр](screenshots/1280x960/10-retry-saved.png) |
| Clean fresh Continue | [кадр](screenshots/1600x900/11-clean-continue.png) | [кадр](screenshots/1280x960/11-clean-continue.png) |

На каждом кадре явно написано **SYNTHETIC synthetic-p24-ability-v1 • test clock • Editor Play Mode**. Это настоящие ScreenCapture из graphical Editor, не generated/mock images. Контроллер simulation отключён для явного test clock: callbacks/Step/Update/LateUpdate вызываются из сценария, поэтому сравнимые моменты фиксируются на tick0. Rendering frames реально меняются: [wide telemetry](screenshots/1600x900/telemetry.txt), [narrow telemetry](screenshots/1280x960/telemetry.txt). Этот clock не доказывает realtime performance или natural прохождение.

[Console audit](console-audit.json): **0 unexpected errors**,2ожидаемых injected save errors (по1на сценарий),16Editor rendering warnings `Ignoring depth surface load/store action as it is memoryless`. Полные scenario logs рядом с кадрами. Failed save сохраняет live WaveComplete/Gold175/Kills1/sequence2; Retry не повторяет cast/reward. Fresh synthetic Continue восстанавливает WaveComplete/Remaining600 без owners/target/summary/ability effects. Дополнительно dirty restore/Preparation rollback проверен отдельным PlayMode test.

Первый cold-cache graphical запуск прошёл assertions, но первые narrow screenshots содержали cyan async shader placeholders. Они были просмотрены и отброшены. Итоговые22кадра захвачены повторно на том же source SHA с прогретым shader cache; размеры и отсутствие больших cyan placeholders проверены, preview/Pause/failed-save/Continue просмотрены. Это ограничение процедуры первого запуска, не скрытый первый visual PASS.

## NOT VERIFIED / BLOCKED

Production остаётся **ability-disabled**: старые content IDs/factory/whitelist/menu; wired Ability Button скрыт, Begin/Confirm API не кастуют. Enabled исключительно internal exact-ID synthetic fixture, те же production controllers. Synthetic fresh-scene Continue использует Store/CheckpointController test path; **enabled production MainMenu Continue не заявляется** — меню правильно отвергает synthetic. Boss/final content совместно активируются только #39; natural replay/баланс #40.

Physical iPhone/touch/safe area/background/force quit, iOS player/IL2CPP/Metal/performance, Simulator, signing, self-hosted Unity CI, G2/human balance и ART approval не проверены. Editor queued Mouse/Touchscreen — synthetic devices с явно включённым IgnoreFocus/Editor routing и ручным input test clock; исходные настройки восстанавливаются. Реальные hardware/lifecycle эти tests не заменяют. Новых packages/Unity version нет. Self-merge/release/TestFlight не выполнялись.

## Повторная проверка evidence

`python3 Docs/review/P2.4d/verify-evidence.py` проверяет decoded raw XML и SHA256, positive totals, source bytes/commit, все media и исторические файлы. Он **не** запускает Unity и не заменяет full gates. Для graphical capture нужна прогретая shader cache и реальный graphical Editor; команда в gates.json. Полные gates запускаются штатными Scripts, без второго resident Editor.
