# ART.2a.1 — проверки и ограничения

19.11.2026, macOS Apple Silicon; Unity6000.3.28f1/8f082d2fe476, URP17.3.0, CLI1.0.1 (self-update check: current/latest1.0.1), targetStandaloneOSX/Metal. Отдельный managed worktree `art-2a-1-readability/Veilward`, branch`codex/art-2a-1-readability`, base`7783dd1`. Основной checkout и другие ветки не изменялись.

## VERIFIED

- Baseline static38/38, project structure PASS. Baseline полный EditMode1110/1110 — [actual report](baseline-EditMode.json).
- Captures — настоящее Editor Play Mode; [before Console](before-console.json)0errors/1existing C#warning, [очищенный after Console](after-console-clean.json)0errors/0warnings/compilationFailed=false.
- Before/after video имеют240 разных player-loop frames, tick0→239, 8атак; decode проверяется отдельно. Time.captureFramerate30 — offline pace, не FPS performance.
- Все геометрические sources, FBX, `.meta` существующих моделей, pivots/scale/material slots прежние. Новый source — только авторский C#/ShaderLab/HLSL, без внешних textures/3D/AI assets. Не требуется фиктивный Blender delta/reexport; сохранённые Unity geometry/reference tests входят в full EditMode.

Финальный полный **EditMode1110/1110**, failed/skipped/inconclusive0 — [actual report](EditMode.json). В visual regression старый поиск всех LineRenderer заменён выбором AttackBeamView; проверки endpoint каждой атаки сохранены. [Неуспешный run1108/1110](EditMode-red.json) сохранён: один неверный счёт beams+HP line, один failed checkpoint save после исчерпания диска. Повторный полный suite зелёный.

Финальный полный **PlayMode63/63**, failed/skipped/inconclusive0 — [actual report](PlayMode.json). Проверено наличие всех3 FullName ArtReadabilityPlayModeTests. DenseHp выполняет1600×900/1280×720/960×540, по3 повторных layout на каждом разрешении, проверяет ratios/реальные fill transforms/non-overlap/reset. Остальные new cases проверяют bounded strike lifecycle и live Battle HP/успешный restore без остаточных links. Существующие ArtReference pause/resume/no-catch-up/restart/event-consumption и production Continue suites тоже прошли. Ранний [60/60](PlayMode-stale.json) остаётся отдельным историческим прогоном.

Static38/38 и structure/package/meta/GUID/asmdef PASS — [полный вывод](static-checks.txt). `git diff --check` проверяется после восстановления suite side effects.

После final overlay правки полный static был прерван OSError28 в системном temporary directory; это не PASS. Освобождены только generated BurstCache/промежуточные video PNG этого worktree, MP4 сохранены. Финальный повтор static использует `TMPDIR="$PWD/Temp/QualityGates"` (тот же suite, временные файлы в ignored каталоге задачи). Gates выполняются последовательно: fake runner self-tests static могут перезаписывать ignored TestResults, поэтому final reports сохраняются отдельно после Unity suites.

## Точные команды

Все команды выполняются из `/Users/alexeykravtsov/.codex/worktrees/art-2a-1-readability/Veilward`; каждый CLI command получает `--caller plugin --skill unity-cli --project-path . --format json`.

```sh
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
git diff --check
unity self-update --check --format json
unity recompile --project-path . --format json
unity command 'script eval' --caller plugin --skill unity-cli --project-path . --format json -- --code 'Veilward.Editor.ArtReferenceBuilder.ApplyArt21();'
unity command 'editor.playmode set_run_in_background' --caller plugin --skill unity-cli --project-path . --format json
unity command 'editor.playmode start' --caller plugin --skill unity-cli --project-path . --format json
unity status --until-ready --timeout 40 --project-path . --format json
unity command 'script run' --caller plugin --skill unity-cli --project-path . --format json -- --file ArtSource/Review/CaptureArt21.cs --entry CaptureArt21.Stills
unity command 'script run' --caller plugin --skill unity-cli --project-path . --format json -- --file ArtSource/Review/CaptureArt21.cs --entry CaptureArt21.Phases
unity command 'script run' --caller plugin --skill unity-cli --project-path . --format json -- --file ArtSource/Review/MeasureArt21.cs --entry MeasureArt21.Run
unity command 'script run' --caller plugin --skill unity-cli --project-path . --format json -- --file ArtSource/Review/CaptureArt21.cs --entry CaptureArt21.Video
ffmpeg -hide_banner -loglevel error -y -framerate 30 -i Temp/Art21Video/after/%04d.png -c:v libx264 -pix_fmt yuv420p -crf 20 -movflags +faststart Docs/review/ART.2a.1/after-combat.mp4
```

Финальная версия после overlay перепроверяется теми же полными suites; итоговые reports выше относятся к последнему сохранённому source.

Before записан до runtime edits на base7783dd1; after определяется marker`Temp/art21-after`. Before encode использует`Temp/Art21Video/before/%04d.png`. Stills возвращают камеру/renderers/environment и удаляют synthetic objects. Video возвращает captureFramerate и latch pause после240frames. Для повторения сначала открыть saved ArtReference; перед исходным before нужен checkout базы и версия capture script без обращений к новым APIs (по сути исходный CaptureArt11 с новым output). Исторические captures не используются вместо нового before.

## Неуспешные попытки / исправления

- Первый запуск Editor через`unity open` не оставил доступный процесс; явный запуск того же установленного binary с`-projectPath ... -buildTarget StandaloneOSX -logFile /tmp/veilward73-editor.log` прошёл. Другой открытый Editor основного проекта не закрывался.
- Ошибка тестового namespace`Veilward.Editor` исправлена без изменения asmdef. Ранний PlayMode60/60 не содержал новых тестов: сохранён как исторический run, не выдаётся за текущую проверку новых regressions. Финальный отчёт должен содержать каждый `ArtReadabilityPlayModeTests`.
- Dense+impact выявил скрытый внутри opaque mesh всполох. Пробный depth bias оказался недостаточен для группы и удалён; финальный маленький unlit overlay shader сохраняет world/projected anchors и показывает локальный impact. Все after stills/phases/dense/video пересняты после этого изменения; промежуточные after не выдаются за final source.
- Первый запуск изменённого capture script имел typo в script-only probe; исправлен до финальных captures. Не является runtime compile/Console PASS.
- Новый PlayMode63-тестовый run дал62/63: BattleHP fixture не выполнил обязательный стартовый выбор, поэтому волна не начиналась. [Actual RED](PlayMode-red.json). Fixture теперь явно выполняет ChooseElement и assert Started; assertions непустых enemies/cleanup сохранены.
- Диск временно исчерпан при копировании отчёта: удалены только промежуточные PNG этой задачи после сохранения MP4; данные пользователя и другие worktrees не очищались.
- Существующие builder suites пересериализуют production сцены/config/materials/URP; их побочные изменения восстановлены только в этом изолированном worktree. Protected diff проверяется после suites.

## NOT VERIFIED / BLOCKED

Physical iPhone/Simulator, iOS export/signing/device run, touch/safe area/background в player, frame-time/memory/thermal/overdraw budgets, G2/баланс, production FLAME/art integration, готовый UI и весь VFX.1 не проверены. #43 не закрывается Editor evidence. До final source/visual review и полной проверки новых regressions PR использует Refs73. Художественная субъективная оценка и device proof не подменяются rectangles/mask samples. Self-merge/release/TestFlight не выполнялись.
