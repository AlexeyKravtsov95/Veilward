# Проверка поставки VFX.1c / #92

## VERIFIED

- Production Battle использует captured атаки STEAM/STORM и сопоставляет Steam/Storm hits через Origin, Tick, ReactionSequenceInTick, Kind и SourceTowerId. Порядок hops отсортирован по HitIndex.
- Unity 6000.3.28f1, macOS Metal, Mobile quality, 1600×900 composited GameView. Direct 8×4 LineRenderer и reactions 24×5 используют имеющийся shared URP Unlit. Пулы FLAME 8/104 и basic 12/48 подтверждены в том же рендере. [Входы рендера и SHA-256](reports/render-inputs.json).
- Static **38/38**, EditMode **1297/1297**, PlayMode **99/99**. Failed, skipped и inconclusive равны 0. Проверки включают повторные вызовы полного builder, сохранённые references, версии packages, compile, .meta и GUID. [Static](reports/static-command.txt) · [EditMode](reports/edit-mode.json.gz) · [PlayMode](reports/play-mode.json.gz) · [Compile](reports/compile-full.json).
- Fresh before/after FLAME **2 371 375 байт**, GALE **2 078 626 байт** совпали побайтно. Оба старта завершают Victory8, включая boss kill, ability kills и все четыре положительные ячейки REG214. [Дайджесты и coverage](reports/replay-equality.json).
- Сохранены четыре локальных render input вместе с исходными SHA-256. Полный builder временно менял только ожидаемые сериализованные outputs. Все 754 исходных входа восстановлены и перечитаны в открытом Editor. Локальные untracked файлы проверяются отдельно. [Начальный аудит](reports/initial-audit.json) · [Восстановление](reports/gates-byte-audit.json).
- [Непрерывное видео и PNG](gallery.md) используют реальные Editor Play Mode кадры. Realtime-сегмент проходит через production Update после файлового Continue из earned FLAME Preparation8. Time scale 1, captureFramerate 0. Повторный Continue восстанавливает целый snapshot и исходные байты сейва, оставляя пулы и status owners пустыми. [Кадры, PTS и SHA](reports/video-proof.json) · [Console](reports/capture-console-errors-final.json).
- Отдельные earned test-clock кадры показывают обе причины реакций и оба L3. Fresh маршруты ART.3e и ART.3f покупают L3 за обычные 495 суммарных вложений, с оплатой апгрейдов 165 и 220. В synthetic сцене шесть семейств и живой Страж показаны вместе с HP и инспектором. [Атрибуция дополнительных кадров](reports/supplement.json).

## Точные команды

| Проверка | Команда и результат |
|---|---|
| Полные suites и побайтное восстановление | `python3 ArtSource/Review/run_vfx1c_gates.py`. Скрипт последовательно запускает `./Scripts/run-static-checks.sh`, `./Scripts/run-edit-mode-tests.sh` и `./Scripts/run-play-mode-tests.sh` в существующем Editor. Все exit 0. |
| Compile | `unity recompile --project-path "$PWD" --format json`. Errors 0. |
| Replay | `unity command script run --caller plugin --skill unity-cli --format json --file ArtSource/Review/CaptureHybridReplays.cs --entry CaptureHybridReplays.Before` до runtime-изменений и `CaptureHybridReplays.After` после них. |
| Editor capture | [Точный журнал CLI](reports/media-commands.txt). [Realtime harness](../../../ArtSource/Review/RecordHybridVfx.cs) · [Дополнительные кадры](../../../ArtSource/Review/CaptureHybridScenes.cs). |
| Видео и кадры | `PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Review/encode_hybrid_video.py`. [FFmpeg, FFprobe, команда и проверки](reports/video-proof.json). |
| Содержимое поставки | `python3 Docs/review/VFX.1c/verify_delivery.py --check-local-preservation` и `python3 Docs/review/VFX.1c/verify_controls.py`. [Git source, media и evidence anchor](manifest.json). |
| Whitespace | `git diff --check` и `git diff origin/main...HEAD --check`. |

Сохранены [первый полный тестовый прогон с ошибкой fixture Continue](attempt-gates-1), [запись с неверно выбранным menu store](attempt-video-1), [первый маршрут L3 с недостаточным золотом](attempt-supplement-1) и [первая проверка видимости попаданий](attempt-pre-visibility). Целевая ошибка MaterialPropertyBlock сохранена в [RED EditMode отчёте](reports/focused-edit-1.json). FFmpeg отклонил concat option при `-safe 1`. Исправление encoder выполнялось на тех же PNG. [Исходный stderr](reports/encoder-attempt-1-stderr.txt).

## NOT VERIFIED / DEFERRED

Физический iPhone отложен до общей QA #53/#54/#55. Запись Unity Editor фиксирует видимость, каденс и lifecycle в этом окружении. Native input, IL2CPP, frame pacing и performance остаются в prerelease QA. Общая приёмка плотного боя, ability и boss cues остаются в #93. Parent #47 находится в состоянии 2/4 до решения владельца.
