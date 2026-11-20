# R2 — фактический workflow

Только task worktree codex/art3a-flame-levels, Unity6000.3.28f1, Blender5.3.0. Unity CLI1.0.1, pinned Pipeline0.10.0-exp.1; пакеты не обновлялись. Перед implementation записана R2 specification в Docs/ART3A_FLAME_LEVELS.md.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/derive_flame_levels.py -- 2 3
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/check_geometry.py
unity recompile --project-path . --format json
unity command 'test run' --caller plugin --skill unity-cli --project-path . --mode editmode --async-tests --format json
unity command 'test status' --caller plugin --skill unity-cli --project-path . --format json
unity command eval --caller plugin --skill unity-cli --project-path . --format json 'System.Environment.SetEnvironmentVariable("ART3A_CAPTURE", "1");'
unity command 'test run' --caller plugin --skill unity-cli --project-path . --mode playmode --async-tests --format json
unity command 'test status' --caller plugin --skill unity-cli --project-path . --format json
# Скопированы completed Temp/pipeline_test_status.json и проверены штатным JSON validator.
unity command 'script run' --caller plugin --skill unity-cli --project-path . --format json -- --file ArtSource/Review/CaptureArt3a.cs --entry CaptureArt3a.Capture
python3 ArtSource/Blender/ART3/verify_reexport.py
PYTHONDONTWRITEBYTECODE=1 ./Scripts/run-static-checks.sh
git diff --check
PYTHONDONTWRITEBYTECODE=1 python3 Scripts/export-ios.py --version 0.1.0 --build-number 77
PYTHONDONTWRITEBYTECODE=1 python3 Scripts/export-ios.py --profile art-reference --version 0.1.0 --build-number 78
```

PlayMode capture вызывается при реально запущенном Play Mode, исходная Battle camera8/11/8/ortho8.5 и scale1. Оба full suites повторены после последней runtime/test правки; именно их отчёты опубликованы. CLI open завершился преждевременно в окружении tool; Editor затем запущен непосредственно foreground executable и удерживался живым exec session. Перед exports он штатно закрыт. User checkout/его Editor не сбрасывались.

Оба xcodebuild использовали существующий локальный team, Automatic provisioning и `DEBUG_INFORMATION_FORMAT=dwarf`; private identifiers не публикуются. Шаблон фактически исполненных команд:

```sh
xcodebuild -project "$EXPORT/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Debug -sdk iphoneos -destination "platform=iOS,id=$IOS_DEVICE_ID" -derivedDataPath "$BUILD/DerivedData" DEVELOPMENT_TEAM="$EXISTING_TEAM" CODE_SIGN_STYLE=Automatic DEBUG_INFORMATION_FORMAT=dwarf -allowProvisioningUpdates build
codesign --verify --deep --strict "$SIGNED_APP"
xcrun devicectl device install app --device "$IOS_DEVICE_ID" "$SIGNED_APP"
xcrun devicectl device process launch --device "$IOS_DEVICE_ID" --terminate-existing --console com.veilward.spiritdefense
# Restore только неотредактированного earned checkpoint, app terminated; copy обратно и exact bytes assertion.
xcrun devicectl device copy to --device "$IOS_DEVICE_ID" --domain-type appDataContainer --domain-identifier com.veilward.spiritdefense --source "$EARNED_CHECKPOINT" --destination Documents/wave-checkpoint-v1.json
xcrun devicectl device capture screenshot --device "$IOS_DEVICE_ID" --destination "$NATIVE_PNG"
xcrun devicectl device copy from --device "$IOS_DEVICE_ID" --domain-type appDataContainer --domain-identifier com.veilward.spiritdefense --source Documents/wave-checkpoint-v1.json --destination "$READBACK"
```

Native video — Control Center Screen Recording, microphone OFF → Image Capture, ровно два известных новых task MP4. Import To возвращён на исходный Pictures; файлы с iPhone не удалялись. Только temporal trim/transcode с исходной скоростью1×, точные команды в device/video-proof.json.

```sh
# Один процесс последовательно для ART1/L1/L2/L3, всегда fresh terminate-existing, одна art78:
xcrun devicectl device process launch --device "$IOS_DEVICE_ID" --terminate-existing --console --environment-variables '{"VEILWARD_ART2B_SCENARIO":"dense","VEILWARD_ART3A_MODEL":"L2"}' com.veilward.spiritdefense
# Ждать ART2B_COMPLETE dense; не останавливать console launcher до complete.
xcrun xctrace record --template 'Activity Monitor' --device "$IOS_DEVICE_ID" --all-processes --time-limit 20s --output "$PRIVATE_TRACE" --no-prompt
xcrun xctrace export --input "$PRIVATE_TRACE" --xpath '/trace-toc/run[@number="1"]/data/table[@schema="activity-monitor-process-live"]' --output "$PRIVATE_XML"
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.2b/extract_native_memory.py "$PRIVATE_XML" > "$PUBLIC_GAME_ONLY_CSV"
xcrun devicectl device copy from --device "$IOS_DEVICE_ID" --source Documents/art2b --domain-type appDataContainer --domain-identifier com.veilward.spiritdefense --destination "$PRIVATE_RUNS"
python3 Scripts/analyze-art2b.py "$ONE_COMPLETE_RUN"
```

Front/side/behind visual-only launches используют тот же model selector и соответствующий scenario. PNG после ART2B_SETTINGS; эти partial runs не называются measurements. Separate Activity Monitor/captures только после завершения трёх timing windows. Private all-process XML/signing logs/raw notifications остаются в ignored TestResults/Builds; опубликованы numeric filtered CSV, receipts/audits, игровые PNG и clean clips.

Финальная проверка из clean detached checkout: `PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3a/verify_delivery.py`; требует также ffprobe. Negative controls — untracked file и committed foreign C# отвергаются. Verifier read-only, не заменяет actual Unity/device runs.
