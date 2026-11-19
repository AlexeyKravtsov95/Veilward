# QA.1 — локальный iPhone build

Статус: SPECIFIED; Refs #42. Основа: main `de8b9b58f26ca86bfbc352eb214eaee7f7a6992c` (PR70, reviewed head4c09210).

Scope: воспроизводимый unsigned Unity export, локальный Xcode development build/install/launch и короткий smoke текущего slice. Runtime, баланс, арт, packages, исторические режимы и schema4 не меняются. Полный acceptance требует физического iPhone; отсутствие support/signing означает частичный BLOCKED, issue42 остаётся open. QA.2/#53, QA.3/#54, ART/#41/#43 и TestFlight отдельно.

Существующий контракт: Unity6000.3.28f1/8f082d2fe476, URP17.3.0, IL2CPP/ARM64, iOS17+, landscape; identifier `com.veilward.spiritdefense`, version0.1.0/build0, team/profile пустые. Graphics API Metal, automatic selection включён; exporter временно закрепляет только Metal. Enabled scenes: Bootstrap→MainMenu→Battle. MainMenu/Battle ссылаются на EightWaveAbilityBossV2: `eight-wave-ability-boss-v2`, boss600/armor5, historical v1/native/all-open и schema4 сохраняются.

Acceptance:

1. Явные version/build number; повторяемый путь `Builds/iOS/<version>-<build>/Xcode`; sourceSHA и content в manifest. Это воспроизводимый процесс, не обещание побайтово одинакового Xcode binary.
2. Закрытый Editor, чистый committed checkout, установленный iOS support. Export в новый staging и disposable detached worktree того же sourceSHA; старый успешный output сохраняется при ошибке. Unity не открывает source checkout. Любой failure/отсутствующий Xcode project/receipt/source drift даёт nonzero. Ни builder, ни scene regeneration не запускаются.
3. BuildPipeline использует IL2CPP/ARM64/device SDK/Metal и точный список enabled scenes; временные PlayerSettings восстанавливаются. Signing/team не подставляются и credentials не добавляются в git. Никакого mandatory signing CI.
4. Реальные Unity export→Xcode build→install→launch на iPhone, smoke обоих стартов и evidence model/OS/sourceSHA/logs/screenshots. Editor/simulator/старые кадры этого не заменяют.
5. Static38, tooling regressions, compile/EditMode/PlayMode если доступны, diff check; tracked evidence доставляется из чистого checkout; независимый review до Ready for Review.

Риски: отсутствующий Unity iOS module, неподтверждённая совместимость Xcode27.1, локальная лицензия, team/provisioning, auto-generated source changes при импорте. Export локален и не выполняет signing. Проверка drift сигнализирует о любом изменении tracked bytes и не сбрасывает пользовательские файлы.

Regression scenarios (сначала спецификация, затем реализация):

- QA1-01: неверные version/build отклоняются до Unity; нет output/source mutation.
- QA1-02: отсутствующий iOS support, dirty checkout, resident descriptor, небезопасный/symlink output отклоняются nonzero.
- QA1-03: Unity failure или exit0 без project/receipt не заменяет предыдущий успешный export.
- QA1-04: два успешных exports заменяют только собственный ignored output, сохраняют Assets/ProjectSettings/Packages и .meta/GUID bytes исходного checkout. Автоматические import migrations в disposable checkout фиксируются отдельным import audit и patch для review, не переносятся в исходники. Mock runner проверяет оркестрацию, реальный repeat остаётся отдельным gate.
- QA1-05: tracked drift после Unity делает run FAIL; неверный receipt/sourceSHA/version/content не принимается.
- QA1-06: Editor regression проверяет сохранённый scene/content contract и отказ при неверном version/build; host tests не доказывают IL2CPP.
- QA1-07: physical smoke: New Game→FLAME, отдельно New Game→GALE; preview/Cancel/Confirm, placement, Start Wave/progress, ability preview/Cancel/Confirm, Pause/Resume, safe checkpoint→terminate/relaunch→Continue без повторной награды. Записать Gold/HP/wave до/после. Полная lifecycle/touch/performance matrix остаётся QA.2/QA.3.

## Повторение владельцем с чистого checkout

Требуются macOS, Unity точной версии с Hub-активированной license, **iOS Build Support** именно для6000.3.28f1, Xcode и доверенный разблокированный iPhone с Developer Mode. Уточнение19.11.2026: Hub/CLI показывает iOS Installed для того же6000.3.28f1 arm64; модуль найден рядом с Unity.app в version-directory/PlaybackEngines/iOSSupport. Старый Python preflight искал только внутри app bundle и ошибочно останавливался до Unity. Exporter теперь учитывает оба layout и resolved executable. Наличие capability подтверждать внутри выбранного Editor через BuildPipeline.IsBuildTargetSupported, не только по directory или Hub metadata. Повторная установка не назначается. Актуальная диагностика/результаты — [revision2](review/QA.1/revision-2/validation.md).

Не пересылать Apple account/private credentials агенту. Подпись настраивать локально в Xcode существующим доступным team/profile для identifier проекта; если подходящего профиля нет, это отдельный BLOCKED. Две найденные identities не доказывают наличие подходящего provisioning profile.

Из корня **чистого committed** checkout, с закрытым Editor этого checkout:

```bash
set -euo pipefail
python3 Scripts/tests/test_ios_export.py
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
# Suites могут сериализовать assets; для export используем новый pristine checkout.
# Сначала commit task changes; QA1_EXPORT_CHECKOUT должен указывать на новый путь.
: "${QA1_EXPORT_CHECKOUT:?choose a new local checkout directory}"
git worktree add --detach "$QA1_EXPORT_CHECKOUT" HEAD
cd "$QA1_EXPORT_CHECKOUT"
python3 Scripts/export-ios.py --version 0.1.0 --build-number 42
python3 Scripts/export-ios.py --version 0.1.0 --build-number 42
git diff --exit-code
git diff --check
```

Version/build обязательны, без timestamp/autoincrement. Для следующего кандидата выбрать явный новый build number. Успех двух exports должен оставить чистые source bytes, включая .meta/scenes/settings. Каждый run пишет Xcode/veilward-build.json (sourceSHA/version/build/content/Unity/warnings), source-audit.json (committed inputs), import-audit.json/import-diff.patch (изменённые импортом derived inputs) и локальный Unity-export.log. Непустой import audit требует проверки перед device acceptance; он не является согласованием изменения арта/настроек. В ignored `Builds/iOS/last-export.log` остаётся последняя попытка. Ни log, ни Xcode output автоматически не публикуются. После аварии inspect `.export-lock`/`.previous` и `git worktree list` (возможен stale disposable worktree после kill/power loss), убедиться, что процесса уже нет, затем вручную убрать lock/восстановить previous; exporter не удаляет чужие каталоги. Если найден source drift, изучить `git diff`: script не делает reset исходников и не объявляет export успешным.

Механизм BuildPipeline основан на [Unity6000.3 custom build script](https://docs.unity.com/en-us/engine/6000.3/manual/building-and-publishing/build-customize-build-pipeline/build-script-build). PlayerSettings временно меняются и восстанавливаются в finally изолированного worktree; files hash+git status исходного checkout проверяются после завершения Unity. Изолированный project удаляется вместе со своим Library после каждой попытки. Нет scene regeneration, SaveAssets, изменений application identifier/team/profile или настройки CI. IL2CPP/ARM64/device SDK/Metal закрепляются только на время export. Работа с устройством использует [командные инструменты Xcode](https://developer.apple.com/documentation/xcode/xcode-command-line-tool-reference); конкретный синтаксис сверять с локальным `xcrun devicectl help`.

## Xcode → install → launch (после успешного export)

Следующие команды — инструкция; статус фактического выполнения и blockers указан в [актуальном evidence](review/QA.1/revision-2/validation.md). Старый app-bundle-only preflight не доказывал отсутствие iOS capability. Simulator не используется. Приватные значения задать только в текущем shell, не в tracked `.env`/скриптах; `set -x` не включать.

```bash
set -euo pipefail
IOS_ROOT="$PWD/Builds/iOS/0.1.0-42"
IOS_EXPORT="$IOS_ROOT/Xcode"
xcodebuild -version
xcodebuild -showsdks
xcodebuild -list -project "$IOS_EXPORT/Unity-iPhone.xcodeproj" > "$IOS_ROOT/xcode-list.txt" 2>&1
xcrun devicectl list devices > "$IOS_ROOT/devices-private.txt"
```

Локально выбрать **connected physical iPhone**, не историческую paired запись. Записать модель/OS; device ID и пользовательское имя не публиковать. Проверить generated scheme Unity-iPhone в xcode-list. В Xcode открыть `Unity-iPhone.xcodeproj`, выбрать уже доступную команду в Signing & Capabilities; при необходимости владелец завершает локальный provisioning. Из новой вкладки shell задать `IOS_DEVICE_ID` и `IOS_TEAM_ID` по фактическому устройству/team. Не угадывать их и не коммитить экспортированный проект. Если identifier недоступен для данной team, остановиться и согласовать корректную настройку; эта инструкция не назначает новый identifier.

```bash
: "${IOS_DEVICE_ID:?set locally from connected physical iPhone}"
: "${IOS_TEAM_ID:?set locally from existing Xcode signing team}"
xcodebuild -project "$IOS_EXPORT/Unity-iPhone.xcodeproj" \
  -scheme Unity-iPhone -configuration Debug -sdk iphoneos \
  -destination "platform=iOS,id=$IOS_DEVICE_ID" \
  -derivedDataPath "$IOS_ROOT/DerivedData" \
  DEVELOPMENT_TEAM="$IOS_TEAM_ID" CODE_SIGN_STYLE=Automatic \
  build > "$IOS_ROOT/Xcode-build.log" 2>&1
xcodebuild -project "$IOS_EXPORT/Unity-iPhone.xcodeproj" \
  -scheme Unity-iPhone -configuration Debug -sdk iphoneos \
  -derivedDataPath "$IOS_ROOT/DerivedData" -showBuildSettings -json \
  > "$IOS_ROOT/build-settings-private.json"
IOS_APP_PATH="$(python3 - "$IOS_ROOT/build-settings-private.json" <<'PY'
import json, pathlib, sys
rows = json.load(open(sys.argv[1]))
apps = [r['buildSettings'] for r in rows if r['buildSettings'].get('WRAPPER_EXTENSION') == 'app']
if len(apps) != 1:
    raise SystemExit('Expected exactly one app target')
s = apps[0]
p = pathlib.Path(s['TARGET_BUILD_DIR']) / s['FULL_PRODUCT_NAME']
if not p.is_dir():
    raise SystemExit('Built app missing')
print(p)
PY
)"
IOS_BUNDLE_ID="$(/usr/libexec/PlistBuddy -c 'Print CFBundleIdentifier' "$IOS_APP_PATH/Info.plist")"
xcrun devicectl device install app --device "$IOS_DEVICE_ID" "$IOS_APP_PATH" \
  > "$IOS_ROOT/install-private.txt" 2>&1
xcrun devicectl device process launch --device "$IOS_DEVICE_ID" \
  --terminate-existing --console "$IOS_BUNDLE_ID" > "$IOS_ROOT/device-console-private.txt" 2>&1
```

`--console` остаётся attached до завершения процесса; launch отдельно подтвердить видимым UI на iPhone, exit0 установки сам по себе не доказывает smoke. Для повторного запуска с checkpoint завершить app на самом iPhone, затем снова launch без uninstall (uninstall удаляет saves). При permission/provisioning/DDI/build failures остановиться, сохранить локальный log и отметить BLOCKED, не продолжать шаги с несуществующим app. Публикация, archive/export IPA, TestFlight и `-allowProvisioningUpdates` не требуются.

## Device smoke и evidence

Выполнить QA1-07 обоих стартов реальными касаниями; записать wave/Gold/HP до checkpoint и после Continue, способность до/после Confirm и после Cancel, отсутствие auto-confirm при Pause. Из физического iPhone получить screenshots (Side+Volume Up) или короткую screen recording, локально перенести без идентификаторов/уведомлений. Это build/launch smoke, не полная QA.2/QA.3 и не ART approval. Проверить device Console/Xcode на unexpected exceptions/crashes; отсутствие access к Console означает NOT VERIFIED, не «0 errors».

В новом evidence report указать tested sourceSHA из receipt, Unity/Xcode/SDK/macOS, выбранную модель/iOS, version/build/content, шаги/исходы/команды/exit codes, hashes и настоящие кадры. Сначала вручную удалить из копий logs/XML device IDs, team/profile/certificate identifiers, Apple account и абсолютные private paths; raw оставить в ignored Builds/TestResults. Для tracked evidence использовать `.txt` или точечный `git add -f` для `.log`; manifest должен перечислять только существующие tracked artifacts. XML после редактирования обозначать sanitized copy, не raw. Из чистого detached checkout проверить все hashes, `git ls-files --error-unmatch` для каждого manifest artifact и соответствие source audit.

## Тесты tooling

`python3 Scripts/tests/test_ios_export.py` использует fake Unity process и временный Git checkout: проверяет orchestration/source preservation/отказы. Это **не** реальный Unity export. `IosBuildTests` проверяет version и production scene/content contract в Editor. Реальный repeat/export/IL2CPP compilation остаётся отдельным gate с установленным iOS support.


Revision2 regressions QA1-08…10: [log symlink safety и Editor/support attribution](review/QA.1/revision-2/specification.md). Последний локальный log публикуется временным файлом и atomic os.replace directory entry: symlink/hardlink target не открывается для записи. Source/external targets и previous-success output сохраняются также при failed Unity run с созданным логом.
