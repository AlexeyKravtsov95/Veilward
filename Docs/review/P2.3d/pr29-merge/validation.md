# PR #29 — разрешение конфликтов после merge #30

Пользователь явно поручил разрешить конфликты и влить PR #29. Актуальный main — `c1546c3`, merge PR #30. Он уже содержит другую реализацию P2.3d, fixes Next/создания EightWave и world tap/inspector. Исходная реализация #29 (`0f83c4f`, evidence `746b9d1`) конфликтовала с этими изменениями.

Решение: полностью сохранить `Assets` из main, включая production runtime/config/scenes/tests/.meta и reviewed fixes; `Packages` и `ProjectSettings` не менять. Текущие Docs/evidence из main сохранены. Исходное evidence #29 перенесено в [отдельный архив](../pr29-original/README.md) с явной маркировкой исторического source; оно не подменяет текущие результаты. README summary обновлён по фактическому состоянию main.

Scope этого объединения — merge reconciliation, архив и status docs. Новых gameplay/visual изменений относительно main нет; новые screenshots для этой операции не требуются. Реальные актуальные кадры main: [P2.3d](../validation.md), [world-tap fixes](../inspector-world-tap/validation.md). Natural replay/P2.3e, physical device/G2/TestFlight остаются открытыми.

## VERIFIED

Проверенный merge source — **59297f6b3f6397021b7e1b3a1f392f77a179f9cd**, оба родителя исходного #29 и main сохранены. Последующий evidence commit меняет только Docs. [Git tree/blob audit](conflict-audit.json): Assets/Packages/ProjectSettings идентичны main; все 340 исходных Docs-файлов main сохранены, 42 файла evidence #29 перенесены byte-identical.

Свежие полные gates: static **38/38**, EditMode **741/741**, PlayMode **41/41**; failed/inconclusive/skipped **0/0/0**, `git diff --check` PASS. PlayMode включает три world-tap regression tests из main. Unity **6000.3.28f1 (8f082d2fe476)**, macOS **27.0.1 arm64**, Hub batchmode/-nographics/-buildTarget iOS, Editor Mono. Это Editor compile/tests, не iOS player/device run.

Точные команды из worktree:

```sh
./Scripts/run-static-checks.sh
./Scripts/run-unity-tests.sh EditMode batch
./Scripts/run-unity-tests.sh PlayMode batch
git diff --check
```

Static выполнен первым; XML сохранены сразу после каждого suite, чтобы fake runner self-tests не удалили reports. [Totals, source и SHA256](gates.json), [EditMode.xml.gz](EditMode.xml.gz), [PlayMode.xml.gz](PlayMode.xml.gz), [static output](static-checks.txt), [Edit runner](edit-runner.txt), [Play runner](play-runner.txt).

## NOT VERIFIED / BLOCKED

Новые visual/gameplay функции в этом merge не добавлялись; прежние real Play Mode screenshots и их ограничения сохранены. Физический iPhone, iOS touch/safe area/background/performance, natural replay/P2.3e, device QA/G2 и TestFlight не закрыты. Merge выполняется по явному разрешению пользователя «реши конфликты и влей» после прохождения локальных и hosted checks. Release не запрашивался и не выполняется.
