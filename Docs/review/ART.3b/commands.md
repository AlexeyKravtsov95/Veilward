# Команды ART.3b

Все команды выполнялись в isolated `.tools/worktrees/art3b-tide`, branch `codex/art3b-tide-levels`; чужой открытый mainEditor не использовался. Unity6000.3.28f1(8f082d2fe476), URP17.3.0, Pipeline0.10.0-exp.1, CLI1.0.1(current), Blender5.3.0(02f0899d78e3), macOS27.0.1/ARM64. BuildTarget iOS, QualityMobile, camera(8,11,8)/ortho8.5, sameBattle light. Ни версии, ни packages, ни signing/license настройки не менялись.

```sh
git fetch origin main
git worktree add .tools/worktrees/art3b-tide -b codex/art3b-tide-levels origin/main
./Scripts/run-static-checks.sh
unity self-update --check --format json
unity open "$PWD" --editor-version 6000.3.28f1 --build-target iOS --format json
```

Начальный `unity open` не сохранил живой процесс, последующий LaunchServices запуск зависал в GetCocoaCurrentDirectory (локальный sample в ignored TestResults). Запущен прямой executable в удерживаемой PTY; импорт/Editor далее нормальны. Это не failure теста и не требование credentials.

```sh
/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity -projectPath "$PWD" -buildTarget iOS -logFile "$PWD/TestResults/Editor-direct.log"
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/TIDE/author_tide.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/TIDE/check_geometry.py
# Три раза L1/L2/L3:
/Applications/Blender.app/Contents/MacOS/Blender --background ArtSource/Blender/ART3/TIDE/Tower_TideBellL1.blend --python-exit-code 1 --python ArtSource/Blender/ART3/TIDE/export_tide.py
```

Все Editor-driving calls используют `--project-path "$PWD" --caller plugin --skill unity-cli`. Ниже указаны точные command/parameters; общие flags опущены только ради читаемости.

```sh
unity command 'script eval' --format json -- --code 'UnityEditor.AssetDatabase.Refresh(); Veilward.Editor.TideModelBuilder.Build(); return "TIDE production imported";'
unity command 'test run' --format json -- --mode editor --filter TideModelAssetTests
unity command 'test run' --format json -- --mode playmode --filter TideModelPlayModeTests --async-tests
unity command 'test status' --format json
python3 ArtSource/Blender/ART3/TIDE/verify_reexport.py
unity command 'script eval' --format json -- --code 'System.Environment.SetEnvironmentVariable("ART3B_CAPTURE","1"); return "task capture enabled";'
unity command 'test run' --format json -- --mode playmode --filter TideModelPlayModeTests --async-tests
unity command 'script eval' --format json -- --code 'System.Environment.SetEnvironmentVariable("ART3B_CAPTURE",null); UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity"); return "direct Battle ready";'
unity command 'editor set_auto_tick' --format json
unity command 'editor.playmode start' --format json
unity status --until-ready --project-path "$PWD" --format json
unity command 'script run' --format json -- --file ArtSource/Review/CaptureArt3b.cs --entry CaptureArt3b.Capture
unity command 'editor.playmode set_run_in_background' --format json
unity command 'script eval' --format json -- --code 'return new {frame=UnityEngine.Time.frameCount, elapsed=UnityEngine.Time.realtimeSinceStartupAsDouble};'
unity command 'script run' --format json -- --file ArtSource/Review/RecordArt3b.cs --entry Art3bEditorRecording.BeginRecording
python3 ArtSource/Review/encode_art3b_video.py
unity command 'editor.playmode stop' --format json
```

Начальный PlayMode status frame1 сам по себе не был advancement proof. После background override наблюдался frame42343, а video содержит204различных frame/time samples и tick progression с actualWet. Synthetic кадры intentionally frozen Domain; видео использует настоящий fixed-step real-time Update, без test clock. Capture overhead не benchmark. Pipeline восстанавливает project runInBackground при выходе; savedProjectSettings защищены относительно main.

Финальные gates после последнего test/runtime delta:

```sh
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
git diff --check
python3 Docs/review/ART.3b/verify_delivery.py
```

Runners проверяют positive full totals и zerofailed/skipped/inconclusive. Финальные SHA, результаты, ограничения, clean-checkout/negative controls — в validation.md и manifest.json. Телефон/native recordings/device binaries не запускались: Device Deferred QA53/54/55.

Финальная последовательность после исправления catalog assertions:

```sh
# Вернуть task-generated non-C# Assets из HEAD; exact list/diff в unity-generated-before-reset.*
unity command 'script eval' --caller plugin --skill unity-cli --project-path "$PWD" --format json -- --code 'UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport); Veilward.Editor.BattleSceneBuilder.Build(); return "Existing builder warmed after source restore; no authored asset delta";'
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
unity command 'editor.playmode start' --caller plugin --skill unity-cli --project-path "$PWD" --format json
unity status --until-ready --project-path "$PWD" --format json
unity command 'editor.playmode set_run_in_background' --caller plugin --skill unity-cli --project-path "$PWD" --format json
unity command 'script run' --caller plugin --skill unity-cli --project-path "$PWD" --format json -- --file ArtSource/Review/CaptureArt3b.cs --entry CaptureArt3b.LoadPreview
unity command 'script run' --caller plugin --skill unity-cli --project-path "$PWD" --format json -- --file ArtSource/Review/CaptureArt3b.cs --entry CaptureArt3b.SelectPreview
unity command 'capture game_view' --caller plugin --skill unity-cli --project-path "$PWD" --format json -- --source screen --width 1600 --height 900 --output-path Docs/review/ART.3b/playmode/catalog-build-preview.png
unity command 'editor.playmode stop' --caller plugin --skill unity-cli --project-path "$PWD" --format json
unity recompile --project-path "$PWD" --format json
# Вернуть generated Assets из HEAD, сохранять audit/patch; затем реальный roundtrip:
python3 ArtSource/Blender/ART3/TIDE/verify_reexport.py
# Вернуть только TIDE FBX в их exact tested bytes; snapshot() повторяет Unity import и сравнение.
git restore --source=HEAD -- Assets/_Game/Art/Models/ART3/TIDE/Tower_TideBellL1.fbx Assets/_Game/Art/Models/ART3/TIDE/Tower_TideBellL2.fbx Assets/_Game/Art/Models/ART3/TIDE/Tower_TideBellL3.fbx
git diff --check
```

Первую попытку последнего roundtrip прервала моя преждевременная команда close. Close timeout потребовал завершить только task Editor4613 и открыть тот же checkout заново (PID17352); original main51069 не затронут. PASS записан только после повторного полного reexport/after-snapshot и canonical FBX readback. Генерация .blend повторно не запускалась. `--width 1600 --height 900` в capture передаются отдельными arguments.
