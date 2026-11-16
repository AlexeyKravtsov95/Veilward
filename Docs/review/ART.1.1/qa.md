# ART.1.1 — QA / evidence

2026-11-16→17, Asia/Novosibirsk. База: `origin/main e33142a`, merge PR #24. Отдельный native worktree `veilward-art-1-1/Veilward`, branch `codex/art-1-1-visual-refinement`. Основной checkout с локальными изменениями не изменялся.

## VERIFIED

| Проверка | Точная команда / результат |
|---|---|
| Static | `./Scripts/run-static-checks.sh` → 38/38 self-tests, Unity structure PASS; [полный вывод](static-checks.txt) |
| EditMode | `./Scripts/run-edit-mode-tests.sh` → **547/547**, failed/skipped/inconclusive 0; [реальный CLI report](EditMode.json) |
| PlayMode | `./Scripts/run-play-mode-tests.sh` → **30/30**, failed/skipped/inconclusive 0; [реальный CLI report](PlayMode.json) |
| Source geometry | `/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART1/tests/check_art11_geometry.py` → три deep connected masses, уменьшенный roof footprint, холодный широкий подол и outward mantle PASS; [вывод](geometry-green.txt) |
| Mirrored hulls | `/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART1/tests/check_shade_winding.py` → обе стороны Shoulder/CrownHorn в saved blend и FBX PASS; [вывод](winding-source-fbx.txt) |
| Negative exporter | `python3 ArtSource/Blender/ART1/tests/test_reexport_failure.py` → **1/1**, настоящий Blender exception/exit 1, без PASS/второго snapshot/записи golden evidence, все FBX SHA неизменны; [вывод](export-negative.txt) |
| Full round trip | `python3 ArtSource/Blender/ART1/verify_reexport.py` → **8/8 saved .blend**, exact Unity geometry hash, GUID/local IDs, slots/materials/dependencies, 26 serialized hashes; [вывод](reexport.txt), [before](reexport-before.json), [after](reexport-after.json) |
| Asset manifest | `unity command 'script run' --caller plugin --skill unity-cli --project-path <ART11_WORKTREE> --format json -- --file ArtSource/Review/AuditArt11.cs --entry AuditArt11.Run` → [фактические Unity counts/texture](imported-models.json); [таблица и происхождение](asset-manifest.md) |
| PR24 references | [Исходный imported snapshot](before-refinement-import.json) → все 8 model identities/materials/dependencies, `.meta` и prefab hashes сохранены. Только ArtReference scene намеренно добавляет kit clusters; [итог](reference-audit.json) |
| Protected scope | `git diff --exit-code e33142a -- Assets/_Game/Scripts/Domain Assets/_Game/Scripts/Infrastructure Assets/_Game/Configs Assets/_Game/Scenes/Battle.unity Assets/_Game/Scenes/MainMenu.unity Packages ProjectSettings` → без изменений |
| Whitespace | `git diff --check` и `git diff --cached --check` → PASS |
| Compile / Console | `unity command 'console get' --caller plugin --skill unity-cli --project-path <ART11_WORKTREE> --format json -- --tail 100 --level log` → capture session 0 errors / 0 warnings, compilationFailed=false; [аудит](console-audit.json) |

`<ART11_WORKTREE>` = `/Users/alexeykravtsov/.codex/worktrees/veilward-art-1-1/Veilward`. Все Editor-команды адресуют именно этот проект, поскольку старый ART.1 Editor открыт отдельно. Unity **6000.3.28f1 / 8f082d2fe476**, URP **17.3.0**, CLI **1.0.0**, macOS Apple Silicon, active target StandaloneOSX; iOS importer override сохранён. Blender **5.3.0 / 02f0899d78e3**. Версии проекта/packages не менялись. Попытка проверить/обновить установленный CLI обнаружила 1.0.1, но download не завершился; использован рабочий 1.0.0. Это не изменение Unity/packages.

Новые tests: три непустых EditMode geometry regressions (island components/support под saved route, roof/base/fire, cold hem/весь mantle/culling) и один PlayMode regression исходных camera/tower/pad transforms/scale. Общие suites также проверяют production economy, repeated commands/rewards, reactions/checkpoint/rollback. Начальный baseline до изменений: static 38/38 и EditMode 544/544.

## Обнаруженные ошибки и исправления

- Source regression сначала падал на прежней глубине острова: [RED](geometry-red.txt). После source-правки проверка непустых connected masses и новых размеров проходит.
- Первый полный EditMode обновлённых моделей дал **546/547**, один failure наружного winding нижней крышки Shade: [RED report](EditMode-red.json). Source ngon триангулировался Blender и FBX разными диагоналями. В editable `.blend` добавлен явный outward fan к внутренней нижней точке; теперь Unity проверяет все **98 triangles mantle**, включая крышки. Затем заново выполнены полный EditMode 547/547, PlayMode 30/30, source/FBX checks, round trip и final capture. Неуспешный run не выдан за PASS.
- Существующие builder tests/Editor автоматически сериализовали production сцены, graybox materials/prefab и URP asset. Эти 19 побочных изменений восстановлены **только в новом worktree**, итоговый protected diff пуст. Все `.meta` относительно базы неизменны.
- Полный round trip экспортировал все восемь sources. Для пяти неизменённых kit models FBX timestamp/header noise восстановлен побайтово из базы; последующий force-import snapshot **точно совпал** с успешным round-trip after. В PR остаются три геометрически изменённых FBX.

## Play Mode capture

Финальные PNG и видео сняты после explicit Shade cap fix. До экспортирования изменений FBX были отдельно сняты `before-*` на исходной модели PR24 в этом же worktree. [Capture log](capture-log.jsonl) хранит real Play Mode flag, Unity frame/tick, attacks/kills, pose/ortho и tower transform. Все основные кадры — **1600×900**, `(8,11,8)`, ortho **8.5**; FLAME **(-2,.16,-1)**, scale 1. Два supplementary Shade closeups используют ortho **1.25** и явно отделены от игрового масштаба.

```sh
unity command 'script eval' --caller plugin --skill unity-cli --project-path <ART11_WORKTREE> --format json -- --code 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/ArtReference.unity");'
unity command 'editor.playmode set_run_in_background' --caller plugin --skill unity-cli --project-path <ART11_WORKTREE> --format json
unity command 'editor.playmode start' --caller plugin --skill unity-cli --project-path <ART11_WORKTREE> --format json
unity status --until-ready --timeout 50 --project-path <ART11_WORKTREE> --format json
# Stage marker отсутствует для before и существует для after: Temp/art11-after.
unity command 'script run' --caller plugin --skill unity-cli --project-path <ART11_WORKTREE> --format json -- --file ArtSource/Review/CaptureArt11.cs --entry CaptureArt11.Stills
unity command 'script run' --caller plugin --skill unity-cli --project-path <ART11_WORKTREE> --format json -- --file ArtSource/Review/CaptureArt11.cs --entry CaptureArt11.Video
ffmpeg -hide_banner -loglevel error -y -framerate 30 -i Temp/Art11Video/%04d.png -c:v libx264 -pix_fmt yuv420p -crf 20 -movflags +faststart Docs/review/ART.1.1/09-playmode-combat.mp4
```

Stills используют `SetPaused(true)` / `AdvanceForReview` через реальную public simulation. `01-island`/`02-combat` — tick **64**, **3** настоящие атаки; HP и impact от сохранённой модели боя. Остальные static probes явно помечены synthetic: они проверяют форму/контраст/HP layout и не меняют saved gameplay coordinates/баланс. Capture возвращает camera/renderers/environment в исходное состояние; временные probe objects уничтожаются.

Видео: **240 фактически advancing frames**, tick **0→239**, **8** атак, **1280×720 / 30 fps / 8 s**. [Telemetry](video-capture.json) фиксирует разные Unity frame counters. `Time.captureFramerate=30` задаёт offline-paced запись, это не realtime/device performance. После записи прежнее captureFramerate восстановлено, Editor run-in-background override выключен, Play Mode остановлен.

## Occlusion render probe

Для отдельного воспроизводимого сравнения используется старый Tower `.blend` из `git show e33142a:ArtSource/Blender/ART1/Tower_FlameLantern.blend`, сохранённый в `Temp/Art11Baseline/Tower_FlameLantern.blend`. `ExportBaselineMeshes.py` читает только этот source и выводит временный mesh JSON в Unity coordinates `(x,z,y)` с обращением winding. Runtime assets/import settings не создаются. `MeasureArt11Occlusion.cs` рендерит flat white enemy body при black tower/background со стандартным временным URP/Unlit material; HP и окружение выключены только в probe.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Review/ExportBaselineMeshes.py
unity command 'script run' --caller plugin --skill unity-cli --timeout 60 --project-path <ART11_WORKTREE> --format json -- --file ArtSource/Review/MeasureArt11Occlusion.cs --entry MeasureArt11Occlusion.Run
```

Сравниваются **41 одинаковая позиция saved path** (distance 1…5 m, step .1 m) для прежней и новой геометрии FLAME, текущий Shade неизменен. Камера/позиция башни/scale прежние. Full-body denominator — тот же enemy без tower, numerator — видимые white pixels с tower. [82 raw samples](occlusion-sweep.json): minimum **14,6%→33,6%**, mean **74,0%→83,7%**, longest sampled run <20% body **5→0**, приблизительно **0,33→0 s** при saved speed 1.5. Погрешность дискретизации — около .067 s/sample; это диагностическая метрика, не утверждённый UX threshold. Прямо за центром башни сохраняется краткое значительное перекрытие, отдельно показанное на static frames. Probe не измеряет HP/маску/точку попадания отдельно; их оценивают по настоящему combat PNG/видео и на iPhone.

[Независимое read-only technical review](technical-review.md) завершено без открытых замечаний; suites повторно reviewer не запускал.

## NOT VERIFIED / OPEN

Физический iPhone не использовался. Не проверены device readability, touch/safe area, ASTC output/resident memory, allocations/frame time/thermal, built-player interruptions/force quit, signing/iOS build, TestFlight, CI runner и внешний плейтест. Художественное approval владельца, G2, баланс и массовое производство остаются открытыми. BACKLOG — документы, без новых карт/кампании/сохранений. Self-merge и release не выполняются.
