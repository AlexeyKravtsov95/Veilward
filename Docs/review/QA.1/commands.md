# Фактически выполненные команды QA.1

Все команды из корня указанного task checkout. `$UNITY_EDITOR_PATH` — стандартный Unity6000.3.28f1 executable, `$CLEAN` — новый detached checkout tested source; значения private paths не публикуются.

```bash
git fetch origin main
git rev-parse origin/main
git merge-base --is-ancestor 4c09210 origin/main
git worktree add -b codex/qa1-ios-device "$TASK_CHECKOUT" origin/main
python3 Scripts/tests/test_ios_export.py
./Scripts/run-static-checks.sh
git diff --check
python3 Docs/review/P2.4f/revision-3/verify-evidence.py
git worktree add --detach "$CLEAN" ab9c17ad0f4f26e92f9a09851200de098f808412
cd "$CLEAN"
python3 Scripts/export-ios.py --version 0.1.0 --build-number 42
# exit2: missing iOSSupport; status clean. Дальше запускались host suites, не iOS build.
"$UNITY_EDITOR_PATH" -batchmode -nographics -projectPath "$CLEAN" \
  -buildTarget StandaloneOSX -runTests -testPlatform EditMode \
  -testResults "$CLEAN/TestResults/QA1-EditMode.xml" -logFile "$CLEAN/TestResults/QA1-EditMode.log"
python3 Scripts/verify_unity_test_results.py TestResults/QA1-EditMode.xml
"$UNITY_EDITOR_PATH" -batchmode -nographics -projectPath "$CLEAN" \
  -buildTarget StandaloneOSX -runTests -testPlatform PlayMode \
  -testResults "$CLEAN/TestResults/QA1-PlayMode.xml" -logFile "$CLEAN/TestResults/QA1-PlayMode.log"
python3 Scripts/verify_unity_test_results.py TestResults/QA1-PlayMode.xml
```

`-quit` не передавался test runner. Unity gates — **host StandaloneOSX**, не iOS и не device. Никакого research100000. Inventory commands/results — [inventory.txt](inventory.txt). Реальная export command остановилась до запуска Unity/iOS/Xcode; инструкции дальнейших действий владельца не выдаются за выполненные команды.

Unity импорт/tests мигрировали19 existing assets в task checkout: RP asset,15materials,Shade prefab,MainMenu/Battle. Эти generated изменения исследованы и восстановлены только в исходно clean task worktree из HEAD; ни gameplay/assets migration, ни historical evidence не включены в PR. Новый exporter запускает Unity в отдельном disposable worktree, поэтому такая сериализация не меняет исходный checkout. Успешный настоящий iOS export и его import audit остаются NOT VERIFIED.
