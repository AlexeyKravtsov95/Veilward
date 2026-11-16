# ART.1 — QA / evidence

Дата: 2026-11-16 (Asia/Novosibirsk). База: `origin/main a506bed` / merge PR #23. Отдельный worktree `veilward-art-1/Veilward`, branch `codex/art-1-visual-prototype`.

## VERIFIED

| Проверка | Точная команда / результат |
|---|---|
| Static | `./Scripts/run-static-checks.sh` → 38/38 self-tests, Unity structure PASS |
| EditMode | `./Scripts/run-edit-mode-tests.sh` → 539/539, failed/skipped/inconclusive 0 |
| PlayMode | `./Scripts/run-play-mode-tests.sh` → 28/28, failed/skipped/inconclusive 0 |
| Reexport | `python3 ArtSource/Blender/ART1/verify_reexport.py` → восемь saved .blend, exact imported geometry/GUID/local IDs/dependencies; prefab/scene/.meta hashes совпали |
| Whitespace | `git diff --cached --check` и `git diff --check` → PASS перед commit |
| Protected scope | `git diff --exit-code origin/main -- Assets/_Game/Scripts/Domain Assets/_Game/Configs Assets/_Game/Scenes/Battle.unity Assets/_Game/Scenes/MainMenu.unity Packages ProjectSettings` → без изменений |
| Compile / Console | `unity command 'console get' --caller plugin --skill unity-cli --format json -- --tail 100 --level log` → 0 errors, 0 warnings, compilationFailed=false |

10 новых EditMode cases и 3 PlayMode cases: `ArtReferenceAssetTests`, `ArtReferencePlayModeTests`. Общий suite включает production economy/reward/duplicate commands/checkpoint/reaction tests. Исходный EditMode run имел 1 failure в cleanup нового теста (RestoreSceneManagerSetup для пустой временной сцены); исправлен guard, затем весь suite 539/539. Незавершённые промежуточные результаты не считаются PASS.

Unity 6000.3.28f1 / 8f082d2fe476; URP 17.3.0, CLI 1.0.0; active target iOS; macOS Apple Silicon. Blender 5.3.0 / 02f0899d78e3. Packages и версия Editor не менялись. При импорте Unity автоматически сериализовал URP asset, а существующие builder tests — graybox assets/scenes; эти побочные diff восстановлены **только в новом worktree**. В финальном PR они отсутствуют. В новых Unity YAML удалены только trailing whitespace для diff gate; GUID не генерировались вручную.

## Capture

Открыть ArtReference в Editor Play Mode. Команды:

```sh
unity command 'script run' --caller plugin --skill unity-cli --format json -- --file ArtSource/Review/CaptureArt1.cs --entry CaptureArt1.Stills
unity command 'editor.playmode set_run_in_background' --caller plugin --skill unity-cli --format json
unity command 'script run' --caller plugin --skill unity-cli --format json -- --file ArtSource/Review/CaptureArt1.cs --entry CaptureArt1.Video
ffmpeg -y -framerate 30 -i Temp/Art1Video/%04d.png -c:v libx264 -pix_fmt yuv420p -crf 20 -movflags +faststart Docs/review/ART.1/09-playmode-combat.mp4
```

Still capture фиксирует **реальный** Play Mode на выбранном public simulation tick (`SetPaused(true)` + `AdvanceForReview`); models, HP и VFX от настоящих events. Главная камера остаётся pose `(8,11,8)`, orthographic 8.5. Supplemental closeups меняют framing и открыто помечены в README/capture-log.

Видео — 180 camera frames реального player loop, Unity frame 38487→38667, tick 0→180, 6 атак; `Time.captureFramerate=30`, H.264 / 1280×720 / 30 fps / 6 s. Это offline-paced Editor recording, не realtime/device performance measurement. Предварительная остановившаяся запись (background override был сброшен после suites) заменена полной advancing записью. `Time.captureFramerate` восстановлен; Editor run-in-background override выключен после capture. В build настройки не добавлены.

Console audit относится к отдельному capture session после `console clear`, включая два informational capture logs. Ошибок/предупреждений нет. Это не утверждение об отсутствии ошибок во всех возможных игровых сценариях.

## NOT VERIFIED / OPEN

Физического iPhone и device traces не было. Touch/safe area, ASTC output, frame time/allocations/memory/thermal, interruptions/force quit на устройстве, signing, iOS player build, TestFlight и CI runner не проверялись. Художественное утверждение владельца, G2, баланс и массовое производство открыты. Нет self-merge/release.
