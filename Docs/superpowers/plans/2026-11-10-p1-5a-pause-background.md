# P1.5a Pause & Background Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add manual and lifecycle-driven pause/resume to the saved eight-wave Battle scene without changing deterministic Domain rules.

**Architecture:** A scene-owned `BattlePauseController` is the single source of latched pause, application-pause and focus state. `BattleSceneController` freezes fixed-step accumulation and rejects commands; `BattlePlacementController` uses the same pause flag for input/UI; a full-screen uGUI overlay blocks raycasts. No global `Time.timeScale` and no disk persistence.

**Tech Stack:** Unity 6000.3.28f1, C#, uGUI, Input System, Unity Test Framework (NUnit), existing Editor scene builder/validator.

## Global Constraints

- Execute inline in `Temp/agent-worktrees/p1-5a-pause-background`; do not dispatch subagents.
- Base is `origin/main` at merged PR #10 (`dc17cca`); branch is `feature/p1-5a-pause-background`.
- Follow `Docs/P1.5A_PAUSE_BACKGROUND.md` and REG-BATTLE-054…066; specification already committed as `3c835be`.
- Do not change Unity/package versions, Domain simulation behavior, saved combat balance or unrelated systems.
- No `Time.timeScale` changes, static event subscriptions, disk saves, process recovery or new dependencies.
- Every code task follows failing test → minimal implementation → passing test → focused commit.
- Final gates require non-empty full PASS with zero failed/inconclusive/skipped; physical iOS remains NOT VERIFIED without a device run.

---

### Task 1: Latched Pause State and Command Gate

**Files:**
- Create: `Assets/_Game/Scripts/Presentation/BattlePauseController.cs`
- Modify: `Assets/_Game/Scripts/Presentation/BattleSceneController.cs`
- Modify: `Assets/_Game/Scripts/Presentation/BattlePlacementController.cs`
- Test: `Assets/_Game/Tests/EditMode/BattlePauseTests.cs`

**Interfaces:**
- Consumes: existing `PlaceTowerResult.Failed(PlaceTowerFailureReason.InvalidPhase)`, `TowerOperationResult.Failed(TowerOperationFailureReason.InvalidPhase)`, `WaveTransitionResult.RejectedInvalidPhase`.
- Produces: `BattlePauseController.IsPaused`, `CanResume`, `Pause()`, `Resume()`, `OnApplicationPause(bool)`, `OnApplicationFocus(bool)`; `BattleSceneController.IsPaused`; `BattlePlacementController.SuppressPointerForCurrentFrame()`.

- [ ] **Step 1: Write failing lifecycle and command tests**

Create `BattlePauseTests` with real serialized controllers and these exact assertions:

```csharp
[Test]
public void LifecycleCallbacks_OverlapWithoutAutomaticResume()
{
    _pause.Pause();
    _pause.OnApplicationFocus(false);
    _pause.OnApplicationPause(true);
    _pause.OnApplicationFocus(true);
    _pause.OnApplicationPause(false);

    Assert.That(_pause.IsPaused, Is.True);
    Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
    Assert.That(_pause.Resume(), Is.True);
    Assert.That(_pause.IsPaused, Is.False);
    Assert.That(_pause.Resume(), Is.False);
}

[Test]
public void Resume_RequiresActiveFocusedApplication()
{
    _pause.OnApplicationPause(true);
    Assert.That(_pause.Resume(), Is.False);
    _pause.OnApplicationPause(false);
    _pause.OnApplicationFocus(false);
    Assert.That(_pause.Resume(), Is.False);
    _pause.OnApplicationFocus(true);
    Assert.That(_pause.Resume(), Is.True);
}

[Test]
public void PausedCommands_AreRejectedWithoutMutation()
{
    var gold = _battle.Simulation.State.Gold;
    _pause.Pause();

    Assert.That(_battle.PlaceTower(FirstCell).FailureReason, Is.EqualTo(PlaceTowerFailureReason.InvalidPhase));
    Assert.That(_battle.UpgradeTower(new TowerId(1)).FailureReason, Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
    Assert.That(_battle.SellTower(new TowerId(1)).FailureReason, Is.EqualTo(TowerOperationFailureReason.InvalidPhase));
    Assert.That(_battle.StartWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
    Assert.That(_battle.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.RejectedInvalidPhase));
    Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(gold));
    Assert.That(_battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
    Assert.That(_placement.TrySelectCell(FirstCell), Is.False);
}
```

- [ ] **Step 2: Run EditMode and verify RED**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: compilation FAIL because `BattlePauseController` and pause APIs do not exist; no implementation is marked PASS.

- [ ] **Step 3: Implement the minimal latched state and gates**

Create the controller with this state machine:

```csharp
public sealed class BattlePauseController : MonoBehaviour
{
    [SerializeField] private BattleSceneController battle;
    [SerializeField] private BattlePlacementController placement;
    [SerializeField] private GameObject pauseOverlay;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;

    private bool _isPaused;
    private bool _applicationPaused;
    private bool _hasFocus = true;

    public bool IsPaused => _isPaused;
    public bool CanResume => _isPaused && !_applicationPaused && _hasFocus;

    public void Pause()
    {
        if (_isPaused) return;
        _isPaused = true;
        if (placement != null) placement.CancelSelection();
        RefreshUi();
    }

    public bool Resume()
    {
        if (!CanResume) return false;
        _isPaused = false;
        if (placement != null) placement.SuppressPointerForCurrentFrame();
        RefreshUi();
        return true;
    }

    public void OnApplicationPause(bool paused)
    {
        _applicationPaused = paused;
        if (paused) Pause(); else RefreshUi();
    }

    public void OnApplicationFocus(bool focused)
    {
        _hasFocus = focused;
        if (!focused) Pause(); else RefreshUi();
    }
}
```

Add null-safe listener registration/removal and `RefreshUi()` that sets overlay active from `_isPaused`, Pause interactable from `!_isPaused`, and Resume interactable from `CanResume`. In `BattleSceneController`, add optional serialized `pauseController`, delegate `IsPaused`, and return the specified rejected results before Domain calls. In `BattlePlacementController`, reject gameplay APIs while `battle.IsPaused`; add `_pointerSuppressedUntilFrame` and skip pointer processing for that frame.

- [ ] **Step 4: Run EditMode and verify GREEN**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: all previous tests plus `BattlePauseTests` PASS, zero failed/inconclusive/skipped.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Game/Scripts/Presentation/BattlePauseController.cs* \
  Assets/_Game/Scripts/Presentation/BattleSceneController.cs \
  Assets/_Game/Scripts/Presentation/BattlePlacementController.cs \
  Assets/_Game/Tests/EditMode/BattlePauseTests.cs*
git commit -m "feat: add latched battle pause state"
```

---

### Task 2: Freeze Fixed Steps, Pending Actions and Beams

**Files:**
- Modify: `Assets/_Game/Scripts/Presentation/BattleSceneController.cs`
- Modify: `Assets/_Game/Scripts/Presentation/AttackBeamView.cs`
- Modify: `Assets/_Game/Scripts/Presentation/BattlePlacementController.cs`
- Test: `Assets/_Game/Tests/EditMode/BattlePauseTests.cs`

**Interfaces:**
- Consumes: `BattleSceneController.IsPaused`, `BattlePauseController.Pause()/Resume()`.
- Produces: pause-safe accumulator boundary; `AttackBeamView.Initialize(Material, Vector3, Vector3, BattleSceneController)`; pending selection/Sell cleanup.

- [ ] **Step 1: Add failing freeze and pending-action tests**

```csharp
[Test]
public void Pause_ClearsPendingPlacementAndSellConfirmation()
{
    Assert.That(_battle.PlaceTower(FirstCell).Success, Is.True);
    var gold = _battle.Simulation.State.Gold;
    Assert.That(_placement.TrySelectCell(FirstCell), Is.True);
    _placement.RequestSellSelectedTower();

    _pause.Pause();

    Assert.That(_placement.HasSelection, Is.False);
    Assert.That(_placement.IsSellConfirmationVisible, Is.False);
    Assert.That(_battle.Simulation.State.Gold, Is.EqualTo(gold));
    Assert.That(_placement.ConfirmSellSelectedTower().FailureReason,
        Is.EqualTo(TowerOperationFailureReason.ConfirmationRequired));
}

[Test]
public void Resume_DoesNotCatchUpAndPreservesDeterministicContinuation()
{
    _battle.StartWave();
    var tick = _battle.Simulation.State.Tick;
    _pause.Pause();
    Invoke(_battle, "Update");
    Assert.That(_battle.Simulation.State.Tick, Is.EqualTo(tick));
    _pause.Resume();
    Invoke(_battle, "Update");
    Assert.That(_battle.Simulation.State.Tick, Is.EqualTo(tick),
        "The resume boundary must discard the current frame delta.");
}

[Test]
public void Beams_FreezeAndResumeWithoutGlobalTimeScale()
{
    var timeScale = Time.timeScale;
    var beam = new GameObject("Beam").AddComponent<AttackBeamView>();
    beam.Initialize(_beamMaterial, Vector3.zero, Vector3.one, _battle);
    _pause.Pause();
    Invoke(beam, "Update");
    Assert.That(RemainingSeconds(beam), Is.EqualTo(0.12f).Within(0.0001f));
    Assert.That(Time.timeScale, Is.EqualTo(timeScale));
}
```

Also test Preparation, Combat, WaveComplete, Victory and Defeat snapshots using the existing public Domain commands; no internal mutation is allowed.

- [ ] **Step 2: Run EditMode and verify RED**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: new freeze/beam assertions FAIL because the accumulator and beam lifetime still advance.

- [ ] **Step 3: Implement pause boundaries and beam freeze**

In `BattleSceneController`, track `_skipNextDelta` and use this update order:

```csharp
private void Update()
{
    if (_simulation == null) return;
    if (IsPaused)
    {
        _accumulator = 0f;
        _skipNextDelta = true;
        return;
    }
    if (_skipNextDelta)
    {
        _skipNextDelta = false;
        _accumulator = 0f;
        return;
    }

    _accumulator += Mathf.Min(Time.deltaTime, 0.25f);
    var steps = 0;
    while (_accumulator >= StepSeconds && steps < MaxStepsPerFrame)
    {
        _simulation.Step();
        ProcessAttackEvents();
        _accumulator -= StepSeconds;
        steps++;
    }
}
```

Pass `this` when initializing a beam. In `AttackBeamView`, store `BattleSceneController battle`, subtract `Time.deltaTime` only when `battle == null || !battle.IsPaused`, and keep the existing destruction behavior. Disable all placement/upgrade/sell/wave buttons in `RefreshUi()` when paused while leaving presentation-only Cancel safe.

- [ ] **Step 4: Run EditMode and verify GREEN**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: full suite PASS, including deterministic continuation and terminal preservation.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Game/Scripts/Presentation/BattleSceneController.cs \
  Assets/_Game/Scripts/Presentation/AttackBeamView.cs \
  Assets/_Game/Scripts/Presentation/BattlePlacementController.cs \
  Assets/_Game/Tests/EditMode/BattlePauseTests.cs
git commit -m "feat: freeze battle simulation and effects on pause"
```

---

### Task 3: Pause Overlay, Saved Scene Wiring and Validation

**Files:**
- Modify: `Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs`
- Modify: `Assets/_Game/Scripts/Editor/BattleAssetValidator.cs`
- Modify: `Assets/_Game/Scenes/Battle.unity` (generated only through `BattleSceneBuilder.Build()`)
- Test: `Assets/_Game/Tests/EditMode/BattleAssetValidationTests.cs`
- Test: `Assets/_Game/Tests/EditMode/BattleSceneBuilderTests.cs`

**Interfaces:**
- Consumes: Task 1 `BattlePauseController` serialized fields and `BattleSceneController.pauseController`.
- Produces: saved `Pause Button`, `Pause Overlay`, `Resume Button`, exactly one fully wired `BattlePauseController`.

- [ ] **Step 1: Write failing saved-scene assertions**

```csharp
[Test]
public void SavedBattleScene_PauseOverlayAndReferences_AreValid()
{
    EditorSceneManager.OpenScene(BattleScenePath);
    var pause = Object.FindFirstObjectByType<BattlePauseController>();
    var overlay = GameObject.Find("Pause Overlay");
    var pauseButton = GameObject.Find("Pause Button").GetComponent<Button>();
    var resumeButton = GameObject.Find("Resume Button").GetComponent<Button>();

    Assert.That(pause, Is.Not.Null);
    Assert.That(overlay, Is.Not.Null);
    Assert.That(overlay.GetComponent<Image>().raycastTarget, Is.True);
    Assert.That(overlay.GetComponent<RectTransform>().anchorMin, Is.EqualTo(Vector2.zero));
    Assert.That(overlay.GetComponent<RectTransform>().anchorMax, Is.EqualTo(Vector2.one));
    Assert.That(pauseButton, Is.Not.Null);
    Assert.That(resumeButton, Is.Not.Null);
    Assert.That(BattleAssetValidator.ValidateSavedBattle(), Is.Empty);
}
```

Keep the existing two-build canonical YAML reproducibility assertion unchanged; it must cover the new scene objects automatically.

- [ ] **Step 2: Run EditMode and verify RED**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: saved scene tests FAIL because pause objects/references are absent.

- [ ] **Step 3: Generate and validate the overlay**

Extend `HudReferences` with `PauseButton`, `PauseOverlay`, `ResumeButton`. Create Pause at top-right below wave controls, then create overlay last with:

```csharp
var pauseOverlay = CreateUiObject("Pause Overlay", canvasObject.transform);
var overlayImage = pauseOverlay.AddComponent<Image>();
overlayImage.color = new Color(0.02f, 0.03f, 0.05f, 0.88f);
overlayImage.raycastTarget = true;
var overlayRect = overlayImage.rectTransform;
overlayRect.anchorMin = Vector2.zero;
overlayRect.anchorMax = Vector2.one;
overlayRect.offsetMin = Vector2.zero;
overlayRect.offsetMax = Vector2.zero;
var pausedLabel = CreateText("Paused Label", pauseOverlay.transform, 42, TextAnchor.MiddleCenter);
pausedLabel.text = "Paused";
pausedLabel.raycastTarget = false;
SetRect(pausedLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(400f, 70f));
var resumeButton = CreateButton("Resume Button", pauseOverlay.transform, "Resume", new Vector2(0f, -30f));
pauseOverlay.SetActive(false);
```

Add `BattlePauseController` to `Battle Controller`, wire both directions through `SerializedObject`, and require exactly one controller plus `battle`, `placement`, `pauseOverlay`, `pauseButton`, `resumeButton` in `BattleAssetValidator`. Add `pauseController` to required battle references. Run `BattleSceneBuilder.Build()` in Editor; never string-edit `Battle.unity`.

- [ ] **Step 4: Run EditMode and verify GREEN**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: validator, scene layout and two-build reproducibility PASS with no missing scripts/references.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs \
  Assets/_Game/Scripts/Editor/BattleAssetValidator.cs \
  Assets/_Game/Scenes/Battle.unity \
  Assets/_Game/Tests/EditMode/BattleAssetValidationTests.cs \
  Assets/_Game/Tests/EditMode/BattleSceneBuilderTests.cs
git commit -m "feat: add pause overlay to battle scene"
```

---

### Task 4: PlayMode Regression and Real Visual Evidence

**Files:**
- Create: `Assets/_Game/Tests/PlayMode/BattlePausePlayModeTests.cs`
- Create temporarily and remove after capture: `Assets/_Game/Editor/PausePlayModeQa.cs`
- Create: `Docs/review/P1.5a/01-preparation-paused.png`
- Create: `Docs/review/P1.5a/02-combat-paused.png`
- Create: `Docs/review/P1.5a/03-resumed.png`
- Create: `Docs/review/P1.5a/qa.log`
- Create: `Docs/review/P1.5a/console-errors.json`

**Interfaces:**
- Consumes: saved scene Pause/Resume buttons and public controller APIs from Tasks 1–3.
- Produces: REG-BATTLE-054…060, 063 and 065 executable/visual evidence.

- [ ] **Step 1: Write failing PlayMode scenarios**

Implement `UnityTest` coroutines that load `Battle` and use actual buttons:

```csharp
[UnityTest]
public IEnumerator Battle_PauseFreezesSimulationAcrossFrames()
{
    yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
    yield return null;
    var battle = Object.FindFirstObjectByType<BattleSceneController>();
    var pause = Object.FindFirstObjectByType<BattlePauseController>();
    battle.StartWave();
    var tick = battle.Simulation.State.Tick;
    var gold = battle.Simulation.State.Gold;
    var kills = battle.Simulation.State.Kills;

    FindButton("Pause Button").onClick.Invoke();
    yield return null;
    yield return null;
    yield return null;

    Assert.That(pause.IsPaused, Is.True);
    Assert.That(battle.Simulation.State.Tick, Is.EqualTo(tick));
    Assert.That(battle.Simulation.State.Gold, Is.EqualTo(gold));
    Assert.That(battle.Simulation.State.Kills, Is.EqualTo(kills));
    FindButton("Resume Button").onClick.Invoke();
    Assert.That(pause.IsPaused, Is.False);
}
```

Add separate coroutines for paused buttons/API, stale Sell confirmation, no catch-up, no Resume click-through and scene reload listener count. Use `Object.FindObjectsByType<BattlePauseController>(...)` and actual `Button.onClick.GetPersistentEventCount()` plus runtime invocation counters where persistent count cannot see runtime listeners.

- [ ] **Step 2: Run PlayMode and verify behavior**

Run: `./Scripts/run-play-mode-tests.sh`
Expected: all old and new PlayMode tests PASS; if RED, fix the responsible pause contract before collecting evidence.

- [ ] **Step 3: Capture real Play Mode screenshots and state log**

Run the saved Battle scene in actual Editor Play Mode at 1600×900. Use real `Button.onClick.Invoke` for Pause/Resume and existing public APIs for setup. Capture Preparation paused, Combat paused after several frozen frames, and resumed gameplay. `qa.log` records exact commands, Tick/HP/Gold/Kills/enemy positions/cooldown before/during/after, and that callbacks are Editor-simulated rather than physical iOS evidence.

- [ ] **Step 4: Audit Console and inspect images**

Export actual Console errors to `console-errors.json`; require an empty error list for the final visual run. Open all three screenshots and confirm full landscape HUD, readable Paused/Resume, no clipping and visible resumed gameplay. Remove temporary QA runner code; retain only tests and evidence.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Game/Tests/PlayMode/BattlePausePlayModeTests.cs* Docs/review/P1.5a
git commit -m "test: verify battle pause in play mode"
```

---

### Task 5: Documentation, Full Gates and Review PR

**Files:**
- Modify: `Docs/TECH_DESIGN.md`
- Modify: `README.md`
- Modify: `Docs/ROADMAP.md`
- Create: `Docs/review/P1.5a/static.log`
- Create: `Docs/review/P1.5a/edit-mode.log`
- Create: `Docs/review/P1.5a/play-mode.log`
- Create: `Docs/review/P1.5a/diff-check.log`

**Interfaces:**
- Consumes: completed implementation and observed test totals.
- Produces: reviewable PR with explicit VERIFIED / NOT VERIFIED / BLOCKED.

- [ ] **Step 1: Update implemented contract and status**

Document the actual latched pause state, no-catch-up boundary, rejected commands, overlay and cleanup in TECH_DESIGN. Update README/ROADMAP to record P1.4c as merged in PR #10 and P1.5a status using only observed evidence; do not claim device callbacks or Unity CI.

- [ ] **Step 2: Run final gates and save exact output**

```bash
./Scripts/run-static-checks.sh 2>&1 | tee Docs/review/P1.5a/static.log
./Scripts/run-edit-mode-tests.sh 2>&1 | tee Docs/review/P1.5a/edit-mode.log
./Scripts/run-play-mode-tests.sh 2>&1 | tee Docs/review/P1.5a/play-mode.log
git diff --check 2>&1 | tee Docs/review/P1.5a/diff-check.log
```

Expected: static project PASS; non-empty EditMode/PlayMode full PASS; diff check exit 0. Remove generated `Scripts/__pycache__` and confirm no changes to `Packages/manifest.json`, `Packages/packages-lock.json`, Unity version or unrelated assets.

- [ ] **Step 3: Review the complete diff and evidence**

Run `git status --short`, `git diff --stat origin/main`, `git diff --check` and inspect changed scene/scripts/docs. Confirm `.meta` pairs, no secrets, no temporary QA code, screenshots readable and test totals match logs. Split VERIFIED from physical iOS/CI NOT VERIFIED.

- [ ] **Step 4: Commit documentation and evidence**

```bash
git add Docs/TECH_DESIGN.md README.md Docs/ROADMAP.md Docs/review/P1.5a
git commit -m "docs: record P1.5a validation evidence"
```

- [ ] **Step 5: Push and open PR without merging**

```bash
git push -u origin feature/p1-5a-pause-background
gh pr create --repo AlexeyKravtsov95/Veilward --base main \
  --head feature/p1-5a-pause-background \
  --title "P1.5a: Pause & Background" \
  --body-file <Russian PR summary containing architecture, tests, evidence and limitations>
```

Confirm the PR URL and leave merge to the owner/independent reviewer.
