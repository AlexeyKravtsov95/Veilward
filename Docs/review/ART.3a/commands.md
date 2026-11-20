# Команды ART.3a

Из корня task worktree; version/packages сохранены. Локальные абсолютные пути и device identifiers в публичные outputs не входят.

```sh
git fetch origin main
git merge-base --is-ancestor 7dfdf0ec1bc1ef63e6df76810cfd08ad6de5181f origin/main
# Отдельный worktree / codex/art3a-flame-levels от origin/main.
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/derive_flame_levels.py
# Для каждого L1/L2/L3, затем проверка sources:
/Applications/Blender.app/Contents/MacOS/Blender --background ArtSource/Blender/ART3/Tower_FlameLanternL1.blend --python-exit-code 1 --python ArtSource/Blender/ART3/export_flame.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/check_geometry.py
unity open . --editor-version 6000.3.28f1 --format json
unity recompile --project-path . --format json
unity command eval --caller plugin --skill unity-cli --project-path . --format json 'Veilward.Editor.FlameModelBuilder.Build();'
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
```

EditMode штатный runner завершился1123/1123. PlayMode shell preflight ошибочно считал endpoint недоступным и отказался запускать второй Editor. Pending runner clients остановлены; фактический полный run выполнен последовательно через доступный Pipeline API:

```sh
unity command 'editor set_auto_tick' --caller plugin --skill unity-cli --project-path . --format json
unity command 'editor.playmode set_run_in_background' --caller plugin --skill unity-cli --project-path . --format json
unity command eval --caller plugin --skill unity-cli --project-path . --format json 'System.Environment.SetEnvironmentVariable("ART3A_CAPTURE", "1");'
unity command 'test run' --caller plugin --skill unity-cli --project-path . --mode playmode --async-tests --format json
unity command 'test status' --caller plugin --skill unity-cli --project-path . --format json
```

Финальный completed JSON из `Temp/pipeline_test_status.json`:75/75. Для обоих отчётов буквально извлечён и выполнен Python JSON-validator из `Scripts/run-unity-tests.sh`; он проверил strict summary/status/counts. XML validator к JSON не применялся. Итерация вызова XML validator с `--help` вернула отказ и не является gate result. Reports скопированы до static self-tests, использующих fake Unity backend и стандартные TestResults paths.

```sh
unity command eval --caller plugin --skill unity-cli --project-path . --format json 'System.Environment.SetEnvironmentVariable("ART3A_CAPTURE", null); UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");'
unity command 'editor.playmode start' --caller plugin --skill unity-cli --project-path . --format json
unity command 'script run' --caller plugin --skill unity-cli --project-path . --format json -- --file ArtSource/Review/CaptureArt3a.cs --entry CaptureArt3a.Capture
unity command 'console clear' --caller plugin --skill unity-cli --project-path . --format json
unity command 'script run' --caller plugin --skill unity-cli --project-path . --format json -- --file ArtSource/Review/CaptureArt3a.cs --entry CaptureArt3a.PrepareMotion
# Public TrySelectCell / SelectTowerType / CancelSelection: inspect и preview без покупки.
unity command 'capture game_view' --caller plugin --skill unity-cli --project-path . --format json -- --source screen --width 1600 --height 900 --output-path Docs/review/ART.3a/playmode/inspect-L3.png
unity command 'script run' --caller plugin --skill unity-cli --project-path . --format json -- --file ArtSource/Review/CaptureArt3a.cs --entry CaptureArt3a.StartMotion
unity command 'console get' --caller plugin --skill unity-cli --project-path . --format json -- --tail 30 --level log
unity command 'editor.playmode stop' --caller plugin --skill unity-cli --project-path . --format json
python3 ArtSource/Blender/ART3/verify_reexport.py
unity close . --timeout 30 --format json
PYTHONDONTWRITEBYTECODE=1 ./Scripts/run-static-checks.sh
git diff --check
```

Synthetic capture — actual Camera.Render в Play Mode, HUD не включён и подписи находятся в самом кадре. Natural test captures/inspect/build — Game View с настоящим Overlay HUD. Real-time sequence использует обычный Battle.Update и unmodified checkpoint из natural test, изолированный file store. Ни custom simulation clock, ни позиции/HP/Gold не редактируются для этой sequence. Это последовательные PNG, не видео/FPS measurement.

Повторный roundtrip после закрытия/повторного открытия Editor проверяет также финальную whitespace-normalized сериализацию. Static/diff, exact-SHA commit, manifest и read-only verifier из нового detached checkout завершают delivery. Свои import serialization deltas восстанавливались только в task worktree; исходный пользовательский checkout не сбрасывался.

## Physical device / build76

Локальные device/team значения подставлены из существующего подключённого device и успешной прежней подписи; private signing credentials не запрашивались. Ниже placeholders намеренно не являются готовыми идентификаторами.

```sh
PYTHONDONTWRITEBYTECODE=1 python3 Scripts/export-ios.py --version 0.1.0 --build-number 76
xcodebuild -project Builds/iOS/0.1.0-76/Xcode/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Debug -sdk iphoneos -destination 'platform=iOS,id=<DEVICE>' -derivedDataPath Builds/iOS/0.1.0-76/DerivedData DEVELOPMENT_TEAM=<EXISTING_TEAM> CODE_SIGN_STYLE=Automatic -allowProvisioningUpdates build
codesign --verify --deep --strict Builds/iOS/0.1.0-76/DerivedData/Build/Products/Debug-iphoneos/VEILWARDSpiritDefense.app
xcrun devicectl device install app --device <DEVICE> <SIGNED_APP>
xcrun devicectl device process launch --device <DEVICE> --console com.veilward.spiritdefense
xcrun devicectl device capture screenshot --device <DEVICE> --destination <NATIVE_PNG>
xcrun devicectl device copy from --device <DEVICE> --domain-type appDataContainer --domain-identifier com.veilward.spiritdefense --source Documents/wave-checkpoint-v1.json --destination <READ_ONLY_COPY>
xcrun xctrace record --template 'Activity Monitor' --device <DEVICE> --all-processes --time-limit 20s --output <PRIVATE_TRACE>
xcrun xctrace export --input <PRIVATE_TRACE> --xpath '/trace-toc/run[@number="1"]/data/table[@schema="activity-monitor-process-live"]' --output <PRIVATE_XML>
python3 Docs/review/ART.2b/extract_native_memory.py <PRIVATE_XML> > <GAME_ONLY_CSV>
```

Build export source receipt aa6e0e1;580 runtime input hashes cross-checked against tested d2bf5bf. Screenshots/JSON copied after observed UI actions; helper only reads app container, never writes. Device source/settings import patch retained. See [device scope](device.md) for failures/limitations. Screen Sharing отключён после последнего Continue; запись остановлена и сохранена iOS. Raw MP4 не импортирован из-за Image Capture AX timeouts.
