# UI.REWORK.2 — значки эффектов в GameView

## VERIFIED

- Заработанная волна 8 v2 загружена через Continue и завершилась победой с 16 HP Сердца. [Входное сохранение](../VFX.1d/reports/safe-file.json) · [Shade](earned/shade-two.png) · [Страж в бою](earned/boss-combat.png) · [Два значка у HP](earned/boss-two.png) · [Victory](earned/victory.png) · [События боя](earned/events.tsv)
- В управляемом сценарии эффекты и captured hits заданы через типизированные данные. При 600/600 HP Стража Steam исчезает после Resume и возвращается действующая пара эффектов. [Два слота](fixture/two-slots.png) · [Steam](fixture/steam.png) · [Storm](fixture/storm.png) · [Полный инспектор](fixture/inspector-full.png) · [Истечение cue](fixture/cue-expired.png) · [Истечение статусов](fixture/status-expired.png) · [Pause](fixture/pause.png) · [×2](fixture/x2.png) · [Плотная группа](fixture/dense-fixture.png) · [Видео GameView](fixture/gameview.mp4) · [События постановки](fixture/events.tsv)
- Тап по Shade идёт через существующий world dispatcher и открывает его инспектор. Цель у полосы Стража передаёт актуальный EnemyId тому же инспектору. В PlayMode проверены RaycastAll и pointer events, повторное открытие, закрытие, защита прицеливания и очистка после rollback с повторным EnemyId. Пройден REG156 с полными статусами, cooldown, причинами и AppliedHpLoss.

| GameView | DPR | Инсеты (px) | Цель Стража (logical pt) |
| --- | ---: | --- | --- |
| [1600×900](profiles/baseline.png) | 1 | 0 | 531.89 × 129.73 |
| [1136×640](profiles/narrow.png) | 2 | 0 | 188.82 × 46.05 |
| [2556×1179](profiles/modern.png) | 3 | 132 по бокам, 63 снизу | 241.70 × 58.95 |
| [2048×1536](profiles/ipad.png) | 2 | 40 снизу | 340.41 × 83.03 |

- Прежде у каждого врага были два Text, два Outline и два StatusCueGraphic. Сейчас у Shade два Image на world-space Canvas, у Стража два Image и одна область ввода на HUD. Общий атлас 1024×512 содержит 30 спрайтов вместо 24. PNG вырос с 162 884 до 182 499 байт, верхняя оценка RGBA32 составляет 2 МиБ. Шесть слайсов 68×68 размещены в свободной области. Регенерация совпала побайтово, включая пиксели и fileID 24 прежних слайсов. Тест 1000 прогретых обновлений пары с повторным layout дал 0 managed bytes на потоке Editor. [Атлас](reports/atlas-audit.json) · [Сериализация](reports/serialization.json)
- Static 38/38, EditMode 1324/1324, PlayMode 111/111. В Unity suites ноль failed, skipped и inconclusive. Компиляция и `git diff --check` прошли. [Static](reports/static.txt) · [EditMode](reports/EditMode.json) · [PlayMode](reports/PlayMode.json) · [Компиляция](reports/compile.json)
- Реализация `36d4ec47554ac37c9b157d21ba8f3618a80ebfec` проверена в Unity 6000.3.28f1, macOS arm64, Metal, Editor target iOS. При визуальном захвате использовались исходные локальные изменения Mobile_RPAsset и трёх Graybox-материалов. Их хеши зафиксированы, суммарный diff совпал с `6637fa9754b759d911484eb7eeca09619689db3d0bed238df43a72e2424b8285`. Пакет содержит 19 полных PNG и 10-секундное MP4 из 200 кадров. [SHA-256 входов](reports/inputs.json) · [Отчёт проверяющего скрипта](reports/verified.json)

На narrow различимы силуэты горения и капли, HP и броня читаются рядом с ними. На modern и iPad у полосы есть отступ от safe area. В плотном сценарии осталась одна видимая пара у полосы HP. Значки Стража собраны в его полосе на HUD.

## Воспроизведение

- В редакторе с открытым Bootstrap выполнить `unity command script recompile`, дождаться результата `unity command script recompile_status --format json`.
- Статические проверки запускаются `./Scripts/run-static-checks.sh`. Для EditMode применена [обёртка snapshot](../UI.REWORK.1/tools/run_edit_with_snapshots.py) из #103 с каталогом `TestResults/UIREWORK2`. Она выполняет `unity command test run --mode editor --timeout 600 --format json`, затем открывает Bootstrap и восстанавливает 13 переписанных тестами файлов. Для PlayMode использованы `unity command test run --mode playmode --async-tests true --timeout 600 --format json` и `unity command test status --format json` до `completed`.
- Временно поместить [драйвер](tools/StatusBadgeCapture.cs) в `Assets/_Game/Scripts/Presentation/`, выполнить компиляцию и запустить Play Mode. По очереди вызвать `unity command script eval --code 'Veilward.Presentation.StatusBadgeCapture.Begin("earned");'`, затем `"fixture"` и `"profiles"`, дожидаясь отдельного `complete.txt` в `TestResults/UIREWORK2/capture/`. Игра использует изолированные слоты сохранений. PNG сняты через ScreenCapture в конце кадра, видео через ScreenCapture, AsyncGPUReadback и FFmpeg. H.264, 1600×900, 20 кадров/с. При задержке текущий кадр повторяется для сохранения темпа. После записи драйвер и его временный meta убраны из Assets.
- `python3.12 Docs/review/UI.REWORK.2/tools/verify.py` проверяет SHA, границы сериализации, полные результаты и декодирует видео целиком. Оригинальный `UI.REWORK.1/tools/verify_media.py --polish` отдельно проверил 27 PNG и два MP4 PR106. Оригинальный `VFX.1d/verify_delivery.py` выполнен на текущей ветке и остановился на сравнении исторического набора Git-путей. Он проверяет точный состав поставки #93. Этот результат указан отдельно от проверок UI.REWORK.2.

## NOT VERIFIED / BLOCKED

- Физические iPhone и iPad, нативная производительность и задержка касаний отложены в #53/#54/#55.
- Художественная приёмка и решение о слиянии остаются у владельца.
