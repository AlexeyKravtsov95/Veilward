# ART.2b — команды и окружение

19.11.2026. Все paths ниже относительные к task checkout; локальные device/team значения не публикуются. Unity6000.3.28f1/8f082d2fe476, URP17.3.0, UnityCLI1.0.1, Xcode27.1/27A9275, iphoneos27.1, macOS Apple Silicon. Production/default settings и source scenes не перенастраивались.

## Quality gates

```sh
python3 Scripts/tests/test_ios_export.py
PYTHONDONTWRITEBYTECODE=1 python3 Scripts/tests/test_art2b_measurements.py
unity open . --build-target StandaloneOSX --until-ready --timeout 120 --format json
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
unity command 'script eval' --caller plugin --skill unity-cli --project-path . --format json -- --code 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/ArtReference.unity");'
unity command 'editor.playmode set_run_in_background' --caller plugin --skill unity-cli --project-path . --format json
unity command 'editor.playmode start' --caller plugin --skill unity-cli --project-path . --format json
unity command 'console clear' --caller plugin --skill unity-cli --project-path . --format json
unity command 'script run' --caller plugin --skill unity-cli --project-path . --format json -- --file ArtSource/Review/CaptureArt2b.cs --entry CaptureArt2b.Capture
unity command 'console get' --caller plugin --skill unity-cli --project-path . --format json -- --tail 30 --level log
unity command 'editor.playmode stop' --caller plugin --skill unity-cli --project-path . --format json
unity close . --timeout 30 --format json
./Scripts/run-static-checks.sh
git diff --check
```

Play Mode PNG сняты на4141764 до iOS activation fix; визуальные meshes/shaders/layout/projectile и showcase после него не менялись. Device44 кадры относятся к актуальному7b753e3. Последние непустые полные suites —1111EditMode/74PlayMode; static38, host22, analyzer4. Static self-tests используют fake Unity и перезаписывают стандартные TestResults JSON; настоящие suite outputs предварительно сохранены отдельно. Fake1/1 не доставляется как Unity PASS.

Неудачные попытки сохранены отдельно: первый compile namespace Application conflict (без XML); первый EditMode1110/1111 из-за ref equality нового теста; один PlayMode invocation попал в transient endpoint reload и попытался batch, который отказал из-за уже открытого Editor (без XML). Они не засчитаны как PASS. Повторные полные runs прошли. Test/build import serialization сохранена локальными patches, затем восстановлены только generated changes собственного checkout; исходные/параллельные рабочие деревья не сбрасывались. Source .meta нового probe создан Unity importer.

## Exact-SHA export → signing/install/launch

Source4141764/build43 и затем исправленный7b753e3/build44 — отдельные сохранённые outputs. Пример ниже — фактически исполненный workflow с локальными переменными вместо идентификаторов:

```sh
python3 Scripts/export-ios.py --profile art-reference --version 0.1.0 --build-number 44
xcodebuild -project "$IOS_EXPORT/Unity-iPhone.xcodeproj" -scheme Unity-iPhone \
  -configuration Debug -sdk iphoneos -destination "platform=iOS,id=$IOS_DEVICE_ID" \
  -derivedDataPath "$IOS_ROOT/DerivedData" DEVELOPMENT_TEAM="$IOS_TEAM_ID" \
  CODE_SIGN_STYLE=Automatic -allowProvisioningUpdates build
codesign --verify --strict --deep "$IOS_APP_PATH"
xcrun devicectl device install app --device "$IOS_DEVICE_ID" "$IOS_APP_PATH"
xcrun devicectl device process launch --device "$IOS_DEVICE_ID" --terminate-existing \
  --console --environment-variables '{"VEILWARD_ART2B_SCENARIO":"normal"}' \
  com.veilward.spiritdefense -- -art2b normal
```

Build44 receipt также проверен непосредственно в signed `.app/Data/Raw/art2b-build.json`, затем в device run.json. Signing использует team из предыдущего успешного локального QA.1 development app, без угадывания identifier/team. Подпись, install и запуск локальные; archive/distribution/TestFlight не выполнялись.

Build43 показал настоящую ArtReference, но без probe overlay/logs и без Documents/art2b; managed launch activation не подтвердилась. Build44 добавляет art-receipt fallback и process environment; фактические ART2B_ATTACH/settings/complete обязательны до учёта samples. Перед первым44 export preflight отверг generated Scripts/__pycache__; каталог удалён, повтор начался с чистого checkout.

## Измерения и captures

Normal/behind/dense запускаются отдельно с явным environment scenario. Каждый default run —5s warmup +30s,3окна. Во время основных timing windows DeviceHub screen sharing выключен; profiler/video/capture не запускаются. Console attached, frame timing stats включён, Unity Development/Profiler connection выключены; Xcode Debug signing/build configuration отдельно от Unity Development flag.

```sh
xcrun devicectl device copy from --device "$IOS_DEVICE_ID" --source Documents/art2b \
  --domain-type appDataContainer --domain-identifier com.veilward.spiritdefense \
  --destination "$ART_RUNS_OUTPUT"
python3 Scripts/analyze-art2b.py "$ONE_COMPLETE_RUN_DIRECTORY"
xcrun devicectl device capture screenshot --device "$IOS_DEVICE_ID" --destination "$PNG_PATH"
xcrun xctrace record --template 'Activity Monitor' --device "$IOS_DEVICE_ID" \
  --attach "$IOS_PID" --time-limit 20s --output "$TRACE_PATH" --no-prompt
xcrun xctrace export --input "$TRACE_PATH" --toc --output "$TRACE_TOC_PATH"
xcrun devicectl device capture screen-record --device "$IOS_DEVICE_ID" \
  --destination "$VIDEO_PATH" --duration 8
```

Исторически Screen-record вернул capability unavailable/CoreDevice1001. QuickTime Movie Recording→iPhone Screen дважды вернул `The operation could not be completed`, включая попытку после Stop Screen Sharing в DeviceHub. Затем native iOS Control Center Screen Recording успешно записал normal combat; ровно один новый MP4 импортирован через Image Capture. Публичный game-only фрагмент сохранён при исходной скорости1×; processing/PTS — reports/video-proof.json. Дополнительная dense recording не получена: Device Hub перестал отдавать окно (cgWindowNotFound); dense PNG не названы continuous video. Native memory/profiler outcome и точные измерения — в validation. CoreDevice screenshots не редактируются; technical UI подписывает synthetic HP/placements. Remote pointer input не считается human multi-touch acceptance.

Native Activity Monitor PID attach вернул Cannot find process; повтор с `--all-processes` записал21sample/20s для каждого сценария. Экспорт таблицы и фильтрация:

```sh
xcrun xctrace record --template 'Activity Monitor' --device "$IOS_DEVICE_ID" \
  --all-processes --time-limit 20s --output "$TRACE_PATH" --no-prompt
xcrun xctrace export --input "$TRACE_PATH" \
  --xpath '/trace-toc/run[@number="1"]/data/table[@schema="activity-monitor-process-live"]' \
  --output "$PRIVATE_MEMORY_XML"
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.2b/extract_native_memory.py "$PRIVATE_MEMORY_XML" > "$PUBLIC_GAME_ONLY_CSV"
ffmpeg -hide_banner -loglevel error -ss 40 -i "$PRIVATE_NATIVE_VIDEO" -t 28 \
  -map 0:v:0 -an -c:v libx264 -preset fast -crf 18 -fps_mode passthrough \
  -enc_time_base:v 1:60000 -video_track_timescale 60000 -map_metadata -1 \
  -movflags +faststart Docs/review/ART.2b/device/normal-native.mp4
```

Extractor проверяет exact executable/PID и единственный PID, разрешает XML ref, публикует только6 числовых столбцов. Все3CSV воспроизведены побайтно, неверный executable отвергнут (native-memory-extraction.json). Raw all-process XML не публикуется. Metal System Trace10s успешно записан отдельно; encoder rows/counter definitions не выдаются за draw calls/overdraw, capability summary — reports/metal-capability.json.

Raw signed outputs/trace/device identifiers/logs остаются локально в ignored Builds/TestResults. Публикуются sanitized copies (локальные identifiers скрыты, trailing whitespace/CRLF нормализованы), source audits/receipt и исходные игровые PNG. Финальный read-only verifier запускается из чистого detached checkout; manifest не хеширует сам себя.
