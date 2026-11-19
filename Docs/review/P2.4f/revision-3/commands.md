# Точные команды

Рабочий каталог: `/Users/alexeykravtsov/Desktop/workspace/Veilward-p24f`, branch `codex/p2.4f-production-validation`. Исходный пользовательский checkout не изменялся.

```bash
unity self-update --check --format json
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh batch
./Scripts/run-play-mode-tests.sh batch
```

Runner принимает только complete nonempty XML с passed==total и failed/skipped/inconclusive0. Unity6000.3.28f1 запускается с `-batchmode -nographics -projectPath "$PWD" -buildTarget iOS -runTests -testPlatform EditMode|PlayMode -testResults "$PWD/TestResults/EditMode.xml|PlayMode.xml" -logFile "$PWD/TestResults/EditMode.log|PlayMode.log"`. Без `-quit`.

Graphical:

```bash
P24F_CAPTURE=1 P24E_CAPTURE=1 \
/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity \
-projectPath "$PWD" -buildTarget iOS -runTests -testPlatform PlayMode \
-testFilter 'AbilityBossIntegrationPlayModeTests;RegBattle213_Visual' \
-testResults "$PWD/TestResults/p24f-r3-Visual.xml" \
-logFile "$PWD/TestResults/p24f-r3-visual.log"
```

Первоначальная focused matrix использовала тот же batch EditMode вызов с `-testFilter AbilityBossBalanceTests` (8cases). Затем добавлены конечные route/robustness regressions. Raw XML `p24f-r3-*` содержат точные cases/counts; каждый experiment проходит полный150/20 run.

Validator RED: filter `RegBattle214_ProductionValidatorRejectsValidHistoricalPayloadAtCurrentAssetPath`; test изafc5661 с validator455a2ca, одно expected failure. Последующие полные suites с guard зелёные.

Evidence:

```bash
python3 Docs/review/P2.4f/revision-3/analyze-balance.py \
--input Docs/review/P2.4f/revision-3 \
--output Docs/review/P2.4f/revision-3/balance-analysis.json
python3 Docs/review/P2.4f/revision-3/verify-evidence.py
git diff --check a5faca3
```

Старые verifiers запускались без изменения source в read-only detached worktreea5faca3. Их precondition на прежний runtime/config/tests source несовместим с v2; их не переписывали ради нового PASS. [Outputs](reports/historical-verifiers.log). Новый verifier проверяет все старые review bytes и frozen v1/native/shared/package/version.

Между Unity runs восстанавливался только известный generated serialization drift в этом worktree: Settings/Mobile_RPAsset, Materials, Shade prefab и saved scenes. Исходники восстанавливались из текущего source commit; пользовательский checkout не сбрасывался. Финальные static и Unity runs выполнялись последовательно. Один ранний Edit launch455a2ca, начавшийся до завершения static self-tests, был остановлен; его outputs не используются как gates.
