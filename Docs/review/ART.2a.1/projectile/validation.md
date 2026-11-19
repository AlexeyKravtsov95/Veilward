# Revision 2 — команды / VERIFIED / ограничения

19.11.2026, отдельный worktree `art-2a-1-readability/Veilward`, branch `codex/art-2a-1-readability`, продолжается Draft PR74. Before-runtime — ba87f0f, оригинальная база merge72 —7783dd1. Unity6000.3.28f1/8f082d2fe476, URP17.3.0, CLI1.0.1 (self-update current/latest), macOS Apple Silicon / StandaloneOSX / Metal. Packages/version неизменны.

## VERIFIED — проверки

[Static output](static-checks.txt), [EditMode actual](EditMode.json), [PlayMode actual](PlayMode.json), [compile](compile.json), [final capture Console](after-console.json), [before Console](before-console.json). Финальный Static38/38 PASS, EditMode1110/1110 PASS, PlayMode67/67 PASS; в обоих Unity suites failed/skipped/inconclusive=0. Все67PlayMode cases включают новый135-pixel render test. Полные totals фиксируются только по завершённым непустым suite reports, без скрытых skipped/inconclusive. Новый read-only verifier дополнительно проверяет все Passed cases и наличие projectile regressions.

Первый PlayMode66/66 ([report](PlayMode-first.json)) был до исправления HP overpaint; он не считается acceptance HP priority. Independent reviewer нашёл конкретный P2 на [RED dense](hp-overlap-red.png). Добавлен отдельный GPU render regression whole135fill/background pixels; текущий shared HP material priority только в ArtReference. [Synthetic pixel audit](hp-pixel-audit.json): 2016green pixels, 0overpaint в mid-flight против identical-static expired группы. Это ограниченный screenshot mask, не device UX threshold.

Три новых lifecycle/timeline tests сравнивают authoritative Domain состояние/HP/position/gold/kills/cooldown с independent public simulation, проверяют nonlethal/lethal arrival, stable EnemyId/view slots, pause/no-catch-up, restart во flight/impact, terminal drain и scene unload/re-entry. Существующие HP ratio/layout/resolution/Battle restore/Continue assertions сохраняются. Старые timing expectations обновлены под явно заменённый владельцем контракт: expiry теперь11ticks вместо7, движение/HP проверяются при arrival, а pre-hit snapshot проверен отдельно. Assertions не удалены ради зелёного результата.

## Точные команды

Из текущего worktree; каждый `unity command` адресует `--project-path .`, другой открытый Editor не затрагивается.

```sh
unity self-update --check --format json
unity command 'script eval' --caller plugin --skill unity-cli --project-path . --format json -- --code 'UnityEditor.AssetDatabase.Refresh();'
unity status --until-ready --timeout 40 --project-path . --format json
unity recompile --project-path . --format json
TMPDIR="$PWD/Temp/QualityGates" ./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
git diff --check
unity command 'script eval' --caller plugin --skill unity-cli --project-path . --format json -- --code 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/ArtReference.unity");'
unity command 'editor.playmode set_run_in_background' --caller plugin --skill unity-cli --project-path . --format json
unity command 'editor.playmode start' --caller plugin --skill unity-cli --project-path . --format json
unity status --until-ready --timeout 40 --project-path . --format json
unity command 'console clear' --caller plugin --skill unity-cli --project-path . --format json
unity command 'script run' --caller plugin --skill unity-cli --project-path . --format json -- --file ArtSource/Review/CaptureArt21Projectile.cs --entry CaptureArt21Projectile.Phases
unity command 'script run' --caller plugin --skill unity-cli --project-path . --format json -- --file ArtSource/Review/CaptureArt21Projectile.cs --entry CaptureArt21Projectile.Dense
unity command 'script run' --caller plugin --skill unity-cli --project-path . --format json -- --file ArtSource/Review/CaptureArt21Projectile.cs --entry CaptureArt21Projectile.Video
ffmpeg -hide_banner -loglevel error -y -framerate 30 -i Temp/Art21ProjectileVideo/after/%04d.png -c:v libx264 -pix_fmt yuv420p -crf 20 -movflags +faststart Docs/review/ART.2a.1/projectile/after-combat.mp4
unity command 'console get' --caller plugin --skill unity-cli --project-path . --format json -- --tail 30 --level log
unity command 'editor.playmode stop' --caller plugin --skill unity-cli --project-path . --format json
python3 Docs/review/ART.2a.1/projectile/verify_delivery.py
```

Первый финальный PlayMode invocation после EditMode завершился exit2 с сообщением `Exited play mode`, без test report; он не засчитан как PASS. Повторный штатный запуск выполнялся из ready Editor.

Before снят заново перед runtime edits в этом worktree на ba87f0f: временная копия прежнего CaptureArt21 с новым output/Stage=before. Для воспроизведения доставлен `ArtSource/Review/CaptureArt21ProjectileBefore.cs`; выполнять только на baseline runtime. Encode использует `Temp/Art21ProjectileVideo/before/%04d.png`. Нельзя переснять новый runtime с label before и выдать за baseline. Старые MP4/PNG ART1/1.1/2a/2a.1 не изменялись.

Phases/Dense используют pause+public AdvanceForReview без fake damage; video использует настоящий player loop и Time.captureFramerate30, возвращает его прежнее значение после240frames. Camera/RT восстановлены, synthetic objects удалены. Per-frame telemetry сохранена JSONL: вложенный samples-array в первом temporary capture не сериализовался, поэтому final video переснят с явной записью каждой строки. Root clock JSON содержит totals, а не массив.

Для обычной скорости финальный MP4 открыт и воспроизведён в QuickTime1×, наблюдены разные моменты боя. Computer-use возвращает дискретные screenshots; это playback check, не заявление о непрерывном human artistic approval. Дополнительно доступны lossless phase PNG и240frame telemetry. UI playback не используется как FPS/device measurement.

## NOT VERIFIED / BLOCKED

Непрерывное human normal-speed motion approval/окончательная художественная оценка снаряда ещё не получены. #73 остаётся open/In progress, PR74 Draft/Refs73. Физический iPhone/readability/performance#43, Simulator, player touch/safe area/background/force quit, frame time/memory/thermal, production FLAME integration/#47, G2 и TestFlight/release не проверены. Новый .2s snapshot delay — только ArtReference, не готовое production feedback решение. Signing/credentials не использовались; self-merge не выполнялся.

Common health/Battle и проверенные Shade/occlusion assets сохраняются. Новый source — оригинальный C#; никаких новых 3D/texture inputs, packages или Blender/FBX изменений, поэтому reexport/fictitious Blender delta не выполнялись. Existing imported-model reference/budget/serialization tests входят в EditMode; source/asset/history invariants проверяются Git blob/SHA256 verifier на clean checkout.
