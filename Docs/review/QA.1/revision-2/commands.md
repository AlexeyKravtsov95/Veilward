# Фактически выполненные команды revision2

Source `8537c47f320987bb8ea68545831af12d876b0a13`. Private values заменены shell variables; они не коммитятся. Запуски из task checkout, Editor экспортера работает в disposable checkout этого SHA.

```bash
unity editors --installed --format json
unity install-modules --editor-version 6000.3.28f1 --list --format json
unity self-update --check --format json
# Read-only probe открытого6000.3.28f1, без изменения сцены/исходников:
unity command 'script eval' --project-path "$OPEN_PROJECT" --caller plugin --skill unity-cli \
  --code 'return new { version = UnityEngine.Application.unityVersion, iosSupported = UnityEditor.BuildPipeline.IsBuildTargetSupported(UnityEditor.BuildTargetGroup.iOS, UnityEditor.BuildTarget.iOS), editorPath = UnityEditor.EditorApplication.applicationPath, contentsPath = UnityEditor.EditorApplication.applicationContentsPath };' --format json
python3 Scripts/tests/test_ios_export.py
./Scripts/run-static-checks.sh
python3 Scripts/export-ios.py --version 0.1.0 --build-number 42
# Первая попытка exit2: UPM com.unity.burst TAR_ENTRY_ERROR ENOSPC.
# Удалены только Library двух созданных этой задачей checkout; основной проект не тронут.
python3 Scripts/export-ios.py --version 0.1.0 --build-number 42
python3 Scripts/export-ios.py --version 0.1.0 --build-number 42
# Последние две попытки exit0; receipts,2418 source hashes и import audits совпали.
xcodebuild -list -project "$IOS_EXPORT/Unity-iPhone.xcodeproj"
xcodebuild -project "$IOS_EXPORT/Unity-iPhone.xcodeproj" -scheme Unity-iPhone \
  -configuration Debug -sdk iphoneos -destination "platform=iOS,id=$IOS_DEVICE_ID" \
  -derivedDataPath "$IOS_ROOT/DerivedData" DEVELOPMENT_TEAM="$IOS_TEAM_ID" \
  CODE_SIGN_STYLE=Automatic -allowProvisioningUpdates build
xcodebuild -project "$IOS_EXPORT/Unity-iPhone.xcodeproj" -scheme Unity-iPhone \
  -configuration Debug -sdk iphoneos -derivedDataPath "$IOS_ROOT/DerivedData" \
  -showBuildSettings -json
codesign --verify --deep --strict "$IOS_APP_PATH"
lipo -archs "$IOS_APP_PATH/$IOS_EXECUTABLE"
xcrun devicectl device install app --device "$IOS_DEVICE_ID" "$IOS_APP_PATH"
xcrun devicectl device process launch --device "$IOS_DEVICE_ID" --terminate-existing com.veilward.spiritdefense
# Для трёх участков stdout также использован --console.
xcrun devicectl device process launch --device "$IOS_DEVICE_ID" --terminate-existing --console com.veilward.spiritdefense
xcrun devicectl device capture screenshot --device "$IOS_DEVICE_ID" --destination "$SCREENSHOT_PNG"
xcrun devicectl device copy from --device "$IOS_DEVICE_ID" \
  --domain-type appDataContainer --domain-identifier com.veilward.spiritdefense \
  --source Documents/wave-checkpoint-v1.json --destination "$CHECKPOINT_COPY"
xcrun devicectl device capture screen-record --device "$IOS_DEVICE_ID" \
  --destination "$VIDEO_MP4" --duration 30
# Screen Recording capability unavailable (CoreDevice1001), video отсутствует.
git diff --check
python3 Docs/review/P2.4f/revision-3/verify-evidence.py
python3 Docs/review/QA.1/revision-2/verify-evidence.py
```

Editor и executable совпадают: стандартный6000.3.28f1 arm64, `UNITY_EDITOR_PATH` unset; support **adjacent**, не Contents/PlaybackEngines. Directory preflight был ошибкой, не отсутствием модуля. Capability true проверена в Editor и реальным экспортом.

Подпись: единственная development team подтверждена локальным profile для подключённого устройства и локальной signing identity. Старые два profiles не подходили bundle ID; Xcode с уже настроенным локальным account и `-allowProvisioningUpdates` выполнил automatic provisioning. Team/identity/profile/Apple account/device IDs/raw private paths не публикуются. Application identifier сохранён.

Управление smoke: macOS Device Hub → выбран **физический iPhone16Pro/iOS27.0.1**, View Screen; клики по реальному device view, не Simulator/Editor, без test clock/Domain injection. Viewer повёрнут в landscape для работы. Это remote pointer→device input, не полноценная human multi-touch/lifecycle матрица QA.2. PNG сняты напрямую CoreDevice и не редактировались.01/11 — честно подписанные startup splash,21 — MainMenu.

Для restart использован CoreDevice `launch --terminate-existing`: app получает SIGKILL9, console client завершает наблюдение exit1. Финальный console client остановлен намеренным host SIGINT, app сообщает signal2, client exit1; затем normal launch exit0 оставил MainMenu с готовым checkpoint. Это запланированные завершения для smoke, не unexpected crash. Полный OS unified log/profiling не собирался.
