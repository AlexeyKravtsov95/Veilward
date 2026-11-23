# Команды ART.3f

Существующий checkout `/Users/alexeykravtsov/Desktop/workspace/Veilward`, ветка `codex/art3f-storm-levels`, baseline d7895fe (merged85), media/source81a510a, final tested source — manifest.json. Новые worktree/клоны/полные копии не создавались. Historical Editor art3d-stone32445 не изменялся. Unity commands ниже всегда с `--caller plugin --skill unity-cli --project-path "$PWD"`, последовательно одним владельцем.

Окружение: Unity6000.3.28f1/8f082d2fe476, CLI1.0.1 (self-update --check current), Blender5.3.0/02f0899d78e3, URP17.3.0, Pipeline0.10.0-exp.1. Actual PlayMode qualityMobile/Mobile_RPAsset/Metal, камера(8,11,8)/ortho8.5,1600×900. [Полный фактический snapshot](reports/actual-render-inputs.json). iOS active target; player/device builds отсутствуют.

```sh
git fetch origin
git switch -c codex/art3f-storm-levels origin/main
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/STORM/author_storm.py
for level in 1 2 3; do
  /Applications/Blender.app/Contents/MacOS/Blender --background "ArtSource/Blender/ART3/STORM/Tower_StormCarpL${level}.blend" --python-exit-code 1 --python ArtSource/Blender/ART3/STORM/export_storm.py
done
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/STORM/check_geometry.py
unity open "$PWD" --editor-version 6000.3.28f1 --args "-logFile $PWD/TestResults/art3f-editor.log -job-worker-count 2"
unity command script eval --caller plugin --skill unity-cli --project-path "$PWD" --code 'Veilward.Editor.StormModelBuilder.Build();'
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/SearchStormTiming.cs --entry SearchStormTiming.Search --timeout 180
unity command editor set_auto_tick --caller plugin --skill unity-cli --project-path "$PWD"
unity command editor.playmode set_run_in_background --caller plugin --skill unity-cli --project-path "$PWD"
unity command script eval --caller plugin --skill unity-cli --project-path "$PWD" --code 'System.Environment.SetEnvironmentVariable("ART3F_CAPTURE","1");'
unity command test run --caller plugin --skill unity-cli --project-path "$PWD" --mode playmode --filter StormModelPlayModeTests --async-tests true
unity command test status --caller plugin --skill unity-cli --project-path "$PWD"
# После targeted run environment ART3F_CAPTURE возвращён в null.
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/CaptureArt3f.cs --entry CaptureArt3f.ConfigureView
unity command editor.playmode start --caller plugin --skill unity-cli --project-path "$PWD"
unity status --until-ready --project-path "$PWD"
unity command job wait --caller plugin --skill unity-cli --project-path "$PWD" -- --condition '{"member":"UnityEngine.Time.frameCount","op":"changed"}' --timeout 10
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/CaptureArt3f.cs --entry CaptureArt3f.Capture
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/RecordArt3f.cs --entry Art3fEditorRecording.BeginRecording
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Review/encode_art3f_video.py
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/CaptureArt3f.cs --entry CaptureArt3f.LoadPreview
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/CaptureArt3f.cs --entry CaptureArt3f.SelectPreview
unity command capture game_view --caller plugin --skill unity-cli --project-path "$PWD" --width 1600 --height 900 --source screen --output-path Docs/review/ART.3f/playmode/preview-selection.png
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/RenderInputsArt3f.cs --entry RenderInputsArt3f.Capture
unity command editor.playmode stop --caller plugin --skill unity-cli --project-path "$PWD"
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Blender/ART3/STORM/verify_reexport.py
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Review/run_art3f_gates.py
# Wrapper исполняет ровно ./Scripts/run-static-checks.sh, ./Scripts/run-edit-mode-tests.sh, ./Scripts/run-play-mode-tests.sh последовательно.
git diff --check
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3f/verify_render_inputs.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3f/verify_controls.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3f/verify_delivery.py
```

## Непрошедшие попытки

[Raw attempts archive](reports/attempts.tar.gz) сохраняет FAIL, а не переименовывает их в PASS. Первая geometry check унаследовала сравнение ширины STEAM: выбранные STORM позы меняют изгиб/высоту головы/открытые fins, checker исправлен на эти формы. Первый bounded search не создал свой output folder; создание перенесено в начало. Первый broad Storm Edit прогон85/86: obsolete Flame fallback case заменён неизвестным ID, которого нет в catalog; устаревший case удалён, новый six-family и legacy/missing storm coverage остаются. Изменение точечно отражено в total.

Первая Play попытка0tests: PlayMode asmdef не ссылается на Newtonsoft, который ошибочно использован test receipt. Исправлено на JsonUtility и явные receipt DTO, без package/asmdef changes; final compile и positive targeted2/2 получены отдельно. Capture helper collision local names исправлен; это не runtime ошибка. Первый recorder отказал до записи, потому что ConfigureView не скомпилировался; task-owned failed recorder удалён, GameView настроен, actual composited RT проверен, новая запись завершилась. Первый rendering receipt попытался сериализовать Color.linear рекурсивно; цвет заменён явным RGBA float[4], никаких consumer/settings изменений.

Первый Unity CLI launch сообщил PID, но Editor завершился до Pipeline, относительный log не появился. Повтор с абсолютным log запустил task Editor93460 в том же checkout/Library. Ни одна такая попытка не считается compile/test PASS. Console не очищалась.

Full suites могут сериализовать известные builder outputs. Wrapper сохраняет только малые известные serialized файлы в ignored TestResults, фиксирует hashes всех input files и восстанавливает только generated bytes своего запуска в finally. [Byte audit](reports/gates-byte-audit.json) проверяет, что пользовательские exact carry-in4 и остальные inputs возвращены. Проект/Library не копируются.

Четыре пользовательских carry-in файла совпадают с принятым85certificate. Новый applicability check повторно проверяет142 dependencies/19settings/exact4 и допускает ровно две точные model-wiring замены в builder/binding, плюс exact shared-palette Storm builder adaptation. Исторический shader/include proof и21controls не выдаются за новые runs. Новый verifier допускает только эти4 dirty files с exact SHA и certificate; он read-only, не называется clean-checkout Unity run и ничего не перезаписывает.

Первая full-suite попытка: Edit1220/1220, Play86/87. RegBattle157 сохранял obsolete STORM TypeTint expectation; заменён на authored STORM L1/noPropertyBlock/no graybox, сохраняя changed-cell/old-root assertions. Это единственная source delta после media head81a510a. Полные gates повторяются на новом test head; исходные результаты сохранены как attempt-1-*.

После suites canonical snapshot прочитан без повторного export и совпал с reexport-after. Служебный preview переснят в новом PlayMode без DontDestroyOnLoad recorder; actual-render-inputs переснят на final source e9277df. GameView index18 восстановлен, session runInBackground/autoTick отключены, task Editor закрыт через `unity close "$PWD" --if-saved`; historical Editor не затронут.

Первый cached diff check нашёл завершающую пустую строку в экспортированном video-inspector.txt. Удалён только пустой EOF, Inspector content не менялся; полный baseline→HEAD diff check повторён отдельно.
