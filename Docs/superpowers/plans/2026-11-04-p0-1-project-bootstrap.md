# VEILWARD P0.1 Project Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Превратить текущий URP-шаблон Unity 6.3 в воспроизводимый iOS landscape-first фундамент VEILWARD с тремя сценами, smoke tests, документацией и первым PR.

**Architecture:** Сохранить текущий проект и URP. Runtime-код ограничить одним `BootstrapLoader`; сцены и настройки создавать через живой Editor, домен и игровые системы не добавлять. EditMode tests проверяют контракт сцен и project settings; локальный скрипт использует открытый Editor, а в CI запускает headless Unity с JUnit report.

**Tech Stack:** Unity `6000.3.28f1`, URP `17.3.0`, C#, NUnit через `com.unity.test-framework@1.6.0`, Unity CLI `1.0.0-beta.16`, Bash, GitHub CLI.

## Global Constraints

- Не менять закреплённую версию Unity `6000.3.28f1` и не добавлять пакеты.
- Не добавлять башни, пути, врагов, VFX, IAP, Game Center или другие игровые системы.
- iOS: IL2CPP, ARM64, минимальная версия `17.0`, только landscape left/right.
- Сериализация: Force Text и Visible Meta Files; все Unity assets сопровождаются `.meta`.
- Работа ведётся на `feature/p0-1-project-bootstrap`; merge запрещён.
- Apple signing и приватные credentials не запрашивать и не изменять.
- Удалённый репозиторий пуст: сначала нужен baseline `main`, затем отдельный feature branch.

---

### Task 1: PATH и baseline репозитория

**Files:**
- Modify outside repository: `~/.bash_profile`
- Create: `.gitignore`

**Interfaces:**
- Consumes: существующий `~/.unity/env` и Unity CLI `~/.unity/bin/unity`.
- Produces: `unity` в bash/zsh; baseline `main` и ветка `feature/p0-1-project-bootstrap`.

- [ ] **Step 1: Добавить существующий Unity CLI environment в bash**

Добавить в `~/.bash_profile` без изменения остальных строк:

```bash
# Unity CLI
if [ -f "$HOME/.unity/env" ]; then
  . "$HOME/.unity/env"
fi
```

Run: `zsh -ic 'command -v unity && unity --version' && bash -lc 'command -v unity && unity --version'`
Expected: обе команды выводят `1.0.0-beta.16`.

- [ ] **Step 2: Создать `.gitignore`**

```gitignore
# Unity generated directories
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Uu]ser[Ss]ettings/
[Ll]ogs/
[Bb]uild/
[Bb]uilds/
[Aa]rtifacts/
TestResults/
CodeCoverage/

# Unity generated IDE files
*.csproj
*.sln
*.suo
*.user
*.userprefs
*.pidb
*.booproj
*.svd
*.pdb
*.mdb

# IDE / OS
.vs/
.idea/
.vscode/
.DS_Store

# Crash reports and logs
*.log
sysinfo.txt
```

Run: `git check-ignore Library Temp UserSettings Logs TestResults/example.xml`
Expected: каждый путь признан ignored; `Packages/manifest.json`, `Packages/packages-lock.json` и `ProjectSettings/` не игнорируются.

- [ ] **Step 3: Зафиксировать исходный baseline и создать feature branch**

```bash
git add AGENTS.md Assets Docs/GDD.md Docs/TECH_DESIGN.md Docs/ROADMAP.md Docs/CONCEPTS.md Packages/manifest.json Packages/packages-lock.json ProjectSettings
git commit -m "chore: import Unity 6.3 project baseline"
git push -u origin main
git switch -c feature/p0-1-project-bootstrap
```

Expected: baseline содержит только исходники/docs/settings, без `Library`, `Temp`, `Logs`, `UserSettings`; текущая ветка — `feature/p0-1-project-bootstrap`.

- [ ] **Step 4: Зафиксировать этот план**

```bash
git add Docs/superpowers/plans/2026-11-04-p0-1-project-bootstrap.md .gitignore
git commit -m "docs: plan P0.1 project bootstrap"
```

---

### Task 2: Runtime assembly и сначала падающий EditMode smoke test

**Files:**
- Create: `Assets/_Game/Scripts/Veilward.asmdef`
- Create: `Assets/_Game/Scripts/Veilward.asmdef.meta`
- Create: `Assets/_Game/Scripts/Presentation/BootstrapLoader.cs`
- Create: `Assets/_Game/Scripts/Presentation/BootstrapLoader.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/Veilward.Tests.EditMode.asmdef`
- Create: `Assets/_Game/Tests/EditMode/Veilward.Tests.EditMode.asmdef.meta`
- Create: `Assets/_Game/Tests/EditMode/ProjectBootstrapTests.cs`
- Create: `Assets/_Game/Tests/EditMode/ProjectBootstrapTests.cs.meta`

**Interfaces:**
- Consumes: `UnityEngine.SceneManagement.SceneManager`, NUnit и Unity Editor APIs.
- Produces: `Veilward.Presentation.BootstrapLoader : MonoBehaviour`; три smoke tests контракта проекта.

- [ ] **Step 1: Создать структуру папок через Editor**

Через `unity command asset create_folder` создать:

```text
Assets/_Game/Art
Assets/_Game/Audio
Assets/_Game/Prefabs
Assets/_Game/Scenes
Assets/_Game/Configs
Assets/_Game/Scripts/Domain
Assets/_Game/Scripts/Application
Assets/_Game/Scripts/Infrastructure
Assets/_Game/Scripts/Presentation
Assets/_Game/Scripts/Editor
Assets/_Game/Tests/EditMode
Assets/_Game/Tests/PlayMode
```

- [ ] **Step 2: Добавить runtime asmdef**

`Assets/_Game/Scripts/Veilward.asmdef`:

```json
{
  "name": "Veilward",
  "rootNamespace": "Veilward",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 3: Добавить минимальный BootstrapLoader**

`Assets/_Game/Scripts/Presentation/BootstrapLoader.cs`:

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Veilward.Presentation
{
    public sealed class BootstrapLoader : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";

        private void Start()
        {
            if (!Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            {
                Debug.LogError($"Bootstrap cannot load scene '{MainMenuSceneName}'. Check Build Settings.");
                return;
            }

            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        }
    }
}
```

- [ ] **Step 4: Добавить EditMode test assembly**

`Assets/_Game/Tests/EditMode/Veilward.Tests.EditMode.asmdef`:

```json
{
  "name": "Veilward.Tests.EditMode",
  "rootNamespace": "Veilward.Tests.EditMode",
  "references": [
    "Veilward",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": [
    "nunit.framework.dll"
  ],
  "autoReferenced": false,
  "defineConstraints": [
    "UNITY_INCLUDE_TESTS"
  ],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 5: Написать smoke tests до создания сцен и изменения настроек**

`Assets/_Game/Tests/EditMode/ProjectBootstrapTests.cs`:

```csharp
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Veilward.Tests.EditMode
{
    public sealed class ProjectBootstrapTests
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/_Game/Scenes/Bootstrap.unity",
            "Assets/_Game/Scenes/MainMenu.unity",
            "Assets/_Game/Scenes/Battle.unity"
        };

        [Test]
        public void GameScenes_ArePresentAndEnabledInLaunchOrder()
        {
            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            CollectionAssert.AreEqual(ScenePaths, enabledScenes);

            foreach (var scenePath in ScenePaths)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null, scenePath);
            }
        }

        [Test]
        public void IosSettings_UseLandscapeIl2CppArm64AndMinimumIos17()
        {
            Assert.That(EditorUserBuildSettings.activeBuildTarget, Is.EqualTo(BuildTarget.iOS));
            Assert.That(PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS), Is.EqualTo(ScriptingImplementation.IL2CPP));
            Assert.That(PlayerSettings.GetArchitecture(NamedBuildTarget.iOS), Is.EqualTo(1));
            Assert.That(PlayerSettings.iOS.targetOSVersionString, Is.EqualTo("17.0"));
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.LandscapeLeft));
            Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.False);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.True);
            Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.True);
        }

        [Test]
        public void EditorSettings_UseForceTextAndVisibleMetaFiles()
        {
            Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText));
            var versionControlSettings = File.ReadAllText("ProjectSettings/VersionControlSettings.asset");
            Assert.That(versionControlSettings, Does.Contain("m_Mode: Visible Meta Files"));
        }
    }
}
```

- [ ] **Step 6: Скомпилировать и убедиться, что suite падает по контракту**

Run: `unity recompile --project-path .` и `unity command "test run" --project-path . --mode editor --timeout 600 --format json`
Expected: компиляция успешна; тесты запускаются и suite FAIL на отсутствующих сценах, активном `StandaloneOSX`, portrait и iOS `15.0`. Ошибок компиляции нет.

---

### Task 3: iOS settings и три сцены

**Files:**
- Modify through Editor: `ProjectSettings/ProjectSettings.asset`
- Modify through Editor: `ProjectSettings/EditorBuildSettings.asset`
- Create: `Assets/_Game/Scenes/Bootstrap.unity` и `.meta`
- Create: `Assets/_Game/Scenes/MainMenu.unity` и `.meta`
- Create: `Assets/_Game/Scenes/Battle.unity` и `.meta`
- Delete template-only: `Assets/Scenes/SampleScene.unity`, `Assets/Readme.asset`, `Assets/TutorialInfo/`

**Interfaces:**
- Consumes: `Veilward.Presentation.BootstrapLoader` из Task 2.
- Produces: Build Settings `Bootstrap → MainMenu → Battle`; активный Build Target `iOS`.

- [ ] **Step 1: Переключить активный Build Target на iOS**

Run: `unity command "build.target switch" --project-path . --target iOS --confirm true --timeout 120`
Expected: команда принята; после polling `build.target switch_status` статус `completed`, `success=true`, `activeBuildTarget=iOS`.

- [ ] **Step 2: Применить Player/Editor settings через Editor API**

```csharp
UnityEditor.PlayerSettings.productName = "VEILWARD: Spirit Defense";
UnityEditor.PlayerSettings.defaultInterfaceOrientation = UnityEngine.UIOrientation.LandscapeLeft;
UnityEditor.PlayerSettings.allowedAutorotateToPortrait = false;
UnityEditor.PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
UnityEditor.PlayerSettings.allowedAutorotateToLandscapeLeft = true;
UnityEditor.PlayerSettings.allowedAutorotateToLandscapeRight = true;
UnityEditor.PlayerSettings.iOS.targetOSVersionString = "17.0";
UnityEditor.PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.iOS, 1);
UnityEditor.PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS, UnityEditor.ScriptingImplementation.IL2CPP);
UnityEditor.PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.veilward.spiritdefense");
UnityEditor.EditorSettings.serializationMode = UnityEditor.SerializationMode.ForceText;
UnityEditor.AssetDatabase.SaveAssets();
```

Run: применить блок через `unity command "script eval"`; затем `settings.player get` должен показать `build-target=iPhone` и `scripting-backend=IL2CPP`.

- [ ] **Step 3: Создать сцены и Build Settings через Editor API**

```csharp
var bootstrap = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultScene, UnityEditor.SceneManagement.NewSceneMode.Single);
var bootstrapObject = new UnityEngine.GameObject("Bootstrap");
bootstrapObject.AddComponent<Veilward.Presentation.BootstrapLoader>();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(bootstrap, "Assets/_Game/Scenes/Bootstrap.unity");

var mainMenu = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultScene, UnityEditor.SceneManagement.NewSceneMode.Single);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(mainMenu, "Assets/_Game/Scenes/MainMenu.unity");

var battle = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(battle, "Assets/_Game/Scenes/Battle.unity");

UnityEditor.EditorBuildSettings.scenes = new[]
{
    new UnityEditor.EditorBuildSettingsScene("Assets/_Game/Scenes/Bootstrap.unity", true),
    new UnityEditor.EditorBuildSettingsScene("Assets/_Game/Scenes/MainMenu.unity", true),
    new UnityEditor.EditorBuildSettingsScene("Assets/_Game/Scenes/Battle.unity", true)
};
UnityEditor.AssetDatabase.SaveAssets();
```

Expected: `scene list_open` подтверждает активную пустую `Battle`; `build get_settings` возвращает ровно три enabled сцены в заданном порядке.

- [ ] **Step 4: Удалить только шаблонные артефакты после успешного создания сцен**

```csharp
UnityEditor.AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
UnityEditor.AssetDatabase.DeleteAsset("Assets/Readme.asset");
UnityEditor.AssetDatabase.DeleteAsset("Assets/TutorialInfo");
UnityEditor.AssetDatabase.Refresh();
```

Expected: URP `Assets/Settings`, `Assets/InputSystem_Actions.inputactions` и весь `Assets/_Game` сохранены.

- [ ] **Step 5: Повторить smoke tests**

Run: `unity recompile --project-path . && unity command "test run" --project-path . --mode editor --timeout 600 --format json`
Expected: 3 tests passed, 0 failed; Console `compilationFailed=false`, 0 errors.

---

### Task 4: Документация, воспроизводимый запуск и PR

**Files:**
- Create: `README.md`
- Create: `Scripts/run-edit-mode-tests.sh`

**Interfaces:**
- Consumes: работающий проект и Unity CLI из Task 1.
- Produces: единая команда локального/CI EditMode run; PR с доказательствами проверок.

- [ ] **Step 1: Добавить локальный/CI test runner**

`Scripts/run-edit-mode-tests.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_VERSION="$(awk '/m_EditorVersion:/ { print $2; exit }' "$PROJECT_PATH/ProjectSettings/ProjectVersion.txt")"

if unity status --project-path "$PROJECT_PATH" --format json >/dev/null 2>&1; then
  unity command "test run" \
    --project-path "$PROJECT_PATH" \
    --mode editor \
    --timeout 600 \
    --format json
else
  mkdir -p "$PROJECT_PATH/TestResults"
  unity test "$PROJECT_PATH" \
    --editor-version "$UNITY_VERSION" \
    --mode EditMode \
    --report-format junit \
    --output "$PROJECT_PATH/TestResults/EditMode.xml" \
    --timeout 600
fi
```

Run: `chmod +x Scripts/run-edit-mode-tests.sh && ./Scripts/run-edit-mode-tests.sh`
Expected: при открытом Editor используется Pipeline, 3 tests passed. На macOS CI без открытого Editor создаётся `TestResults/EditMode.xml` с JUnit report.

- [ ] **Step 2: Написать README**

README должен содержать:
- название и краткий non-gameplay статус P0.1;
- точную версию `6000.3.28f1 (8f082d2fe476)` и URP `17.3.0`;
- структуру `Assets/_Game` и порядок сцен;
- требования Unity CLI `1.0.0-beta.16`, iOS Build Support и активную Unity license;
- запуск `./Scripts/run-edit-mode-tests.sh`;
- CI: macOS runner, установленный Unity того же патча, та же команда, JUnit artifact `TestResults/EditMode.xml`;
- iOS build: Unity создаёт Xcode project, signing выполняется владельцем; физическое устройство не проверялось.

- [ ] **Step 3: Выполнить финальные проверки**

```bash
./Scripts/run-edit-mode-tests.sh
unity command "console status" --project-path . --format json
unity command "build get_settings" --project-path . --format json
unity command "settings.player get" --project-path . --format json
git diff --check
git status --short --ignored
```

Expected: tests 3/3 PASS; compile errors 0; Build Target iOS; scenes 3; portrait false; Force Text/Visible Meta Files; нет staged/tracked `Library`, `Temp`, `Logs`, `UserSettings`; `.meta` всех новых assets существуют.

- [ ] **Step 4: Проверить Bootstrap без заявления device-test**

Открыть `Bootstrap`, войти в Play Mode через `editor.playmode start`, дождаться загрузки `MainMenu` через `scene list_open`, выйти через `editor.playmode stop`.
Expected: активная сцена `MainMenu`, Console errors 0. Не утверждать проверку UI или физического iPhone.

- [ ] **Step 5: Commit и PR без merge**

```bash
git add .gitignore AGENTS.md Assets Docs Packages ProjectSettings README.md Scripts
git commit -m "feat: prepare P0.1 iOS project bootstrap"
git push -u origin feature/p0-1-project-bootstrap
gh pr create --base main --head feature/p0-1-project-bootstrap \
  --title "P0.1: Unity 6.3 iOS project bootstrap" \
  --body $'## Summary\n- Unity 6.3 URP project configured for iOS landscape\n- Bootstrap, MainMenu and empty Battle scenes\n- EditMode smoke tests and reproducible local/CI runner\n\n## Architecture notes\n- Only BootstrapLoader runtime behavior; no gameplay systems\n- Existing URP and package versions preserved\n\n## Verification\n- `./Scripts/run-edit-mode-tests.sh` — 3/3 passed\n- Unity Console — 0 compile errors\n- `git diff --check` — PASS\n\n## Known limitations\n- iOS device / Xcode signing: NOT VERIFIED\n- No merge performed'
```

Expected: PR открыт в `main`, merge не выполняется. В PR-комментарии дополнительно указать фактические результаты tests/compile и ограничение `iOS device / Xcode signing: NOT VERIFIED`.
