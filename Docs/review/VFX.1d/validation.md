# Проверки #93

## VERIFIED

| Проверка | Результат |
|---|---|
| Static | 38/38 и проверка Unity version, .meta, GUID, assembly definitions, manifest/lock |
| EditMode | 1298/1298, включая полный builder, scene и prefab references, REG214 и новые сценарии Печати |
| PlayMode | 100/100, включая полный lifecycle Стража, background/Pause и geometry, Sell/Upgrade, Retry, NewGame и файловый Continue |
| Compile и Git | Unity compile с 0 errors, git diff --check, сверка защищённых путей и версий пакетов |
| Editor media | 946 earned кадров за 36,629 с, 571 dense кадров за 19,576 с и 15 исходных PNG. Максимальная ошибка PTS 0,896 мс |
| FLAME / GALE | 2 371 375 / 2 078 626 байт полного replay. Равенство before/after, Victory8, 12 / 14 ability kills, по 1 boss kill, положительные Steam/Storm × Intrinsic/StatusPair для обоих стартов |
| Repeated Editor profile | Два прохода с восстановлением исходной численности GameObjects, Renderer и material slots. Сырые draw calls, batches и GC allocated per frame |

Failed, skipped и inconclusive равны нулю в обеих финальных suites. [Static](reports/static-command.txt) · [EditMode](reports/edit-mode.json.gz) · [PlayMode](reports/play-mode.json.gz) · [Compile](reports/compile-final.json) · [Replay](reports/replay-equality.json).

## Воспроизведение

База 913adb1d4d255fe4fc6683c18091b8379f38f917. Точные tested и media commits, защищённые пути и SHA-256 артефактов в [manifest](manifest.json). macOS 27.0.1 arm64, Unity 6000.3.28f1, Metal, Mobile. GameView 1600×900, timeScale 1, captureFramerate 0. Установленные package.json подтверждают URP/Core 17.3.0, Pipeline 0.10.0-exp.1, Input System 1.20.1 и Test Framework 1.6.0. [Фактические render inputs](reports/earned-render-inputs.json) · [Dense](reports/dense-all-open-render-inputs.json).

```bash
unity recompile --project-path "$PWD" --format json
python3 ArtSource/Review/run_vfx1d_gates.py
git diff --check
python3 ArtSource/Review/run_vfx1d_editor.py After Earned Dense Profile
python3 ArtSource/Review/pack_vfx1d_evidence.py
python3 Docs/review/VFX.1d/verify_delivery.py --controls
```

Gate runner выполняет последовательно `./Scripts/run-static-checks.sh`, `./Scripts/run-edit-mode-tests.sh` и `./Scripts/run-play-mode-tests.sh`. Запись использует уже открытый Editor. [Команды Unity и восстановление его настроек](reports/media-commands.txt) · [Точные exit codes и восстановление serialized inputs](reports/gates-byte-audit.json).

Before выполнен на базе до правок. В существующем `CaptureHybridReplays.cs` изменён только локальный output path в копии этого capture script. `CaptureHybridReplays.Before` и `CaptureHybridReplays.After` использовали тот же код генерации трасс. Положительные отчёты соответствуют новым запускам. [Before](reports/replay-before.json) · [After](reports/replay-after.json).

Первый прогон зафиксировал 156 EditMode и 97 PlayMode failures после создания MaterialPropertyBlock в field initializer MonoBehaviour. Исправление перенесло создание блоков в prewarm. Предыдущий графический проход помог поднять контраст искры на светлой дороге и сдвинуть leak cue перед Сердцем. [Первый EditMode](reports/attempt-first-edit-mode.json.gz) · [Первый PlayMode](reports/attempt-first-play-mode.json.gz).

Файловый Continue вернул целый штатный Preparation8 с Grid и очистил эффекты, статусных владельцев и инспектор. [Заработанный snapshot](reports/earned-state.txt) · [Повторный snapshot](reports/continued-state.txt) · [Безопасный save](reports/safe-file.json) · [Save после Continue](reports/continued-file.json).

## Ёмкость визуалов

| Группа | Слоты | LineRenderer и material slots | Политика |
|---|---:|---:|---|
| FLAME | 8 | 104 | Свободный, затем старейший |
| TIDE / GALE / STONE | 12 | 48 | Свободный, затем старейший |
| STEAM / STORM direct | 8 | 32 | Свободный, затем старейший |
| Реакции | 24 | 120 | Циклическая замена |
| Печать, область и попадания | 4 + 28 | 8 + 28 | Два отдельных циклических пула |
| Preview Печати | 1 | 2 | Владелец AbilityController |
| Страж | 4 | 4 | Свободный, затем старейший |
| Range | 1 | 1 | Владелец BattlePlacementController |

347 линий сцены используют три shared material assets. Максимум две overhead позиции на владельца. ART5 tails ограничены 16. Уже выпущенные эффекты успевают закончить короткий lifetime после исчезновения источника или цели. [Конкретные renderer, slots и materials](reports/dense-all-open-render-inputs.json).

## Editor profile

Два повтора одного all-open сценария. 5 с warmup, затем 6 с ProfilerRecorder на обычном Update с Печатью. Измерены все GameObjects, Renderer и CanvasRenderer сцены Battle, включая неактивные slots. Material slots посчитаны по компонентам Renderer. Оба прохода с тика 150 до 330 дали одинаковые counts.

| Момент | GameObjects | Renderer | CanvasRenderer | Material slots | Shared materials |
|---|---:|---:|---:|---:|---:|
| Подготовка | 835 | 515 | 83 | 529 | 10 |
| Плотный бой, начало | 1011 | 627 | 115 | 663 | 16 |
| Плотный бой, конец | 1033 | 641 | 119 | 679 | 16 |
| После restore и выгрузки владельцев | 835 | 515 | 83 | 529 | 10 |

В двух повторах записано 5983 и 6153 samples. Медиана в обоих проходах — 319 draw calls, 317 batches и 11 231 B GC allocation за кадр. p95 — 331, 323 и 11 231 B соответственно. Счётчики включают стоимость Editor и инструментирования. Первые нули draw/batch сохранены, редкие пики до 1,32 MB включают измерение counts и буфер сборщика. PNG/FFmpeg выполнялись отдельным прогоном. [Сырые измерения](reports/profile.json) · [Сводные статистики](reports/profile-summary.json).

## NOT VERIFIED / DEFERRED

Editor profile измеряет два воспроизводимых окна и возврат числа объектов после restore. GPU/frame time, native resident memory и длинная device-сессия этим инструментом не измерялись. Физический iPhone и native performance отложены до prerelease QA #53/#54/#55. Плотный тест с шестью семействами использует явно обозначенный all-open stress fixture, поскольку штатный прогресс открывает три стихии.

Локальные carry-in проверяются по отдельным SHA и участвовали в визуальном прогоне. Их Git blobs совпадают с базой. Для full verifier нужны фактические четыре локальных render inputs, Unity PackageCache, Python с Pillow, FFmpeg и FFprobe. Порядок MP4 проверяется по стабильным semantic fields. Необязательные FFprobe metadata игнорируются. [Carry-in и исходное состояние](reports/initial-audit.json).
