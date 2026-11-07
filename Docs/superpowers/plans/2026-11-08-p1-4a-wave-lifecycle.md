# P1.4a Wave Lifecycle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Добавить детерминированный восьмиволновый матч с ручными переходами, однократными наградами, терминальными состояниями и минимальным Battle UI, сохранив legacy single-wave regression.

**Architecture:** Чистый C# Domain хранит последовательность `WaveDefinition`, глобальные counters и насыщаемое начисление Gold. Unity config ссылается на общий `TestWave.asset` для карты/боя и добавляет только waves/rewards; существующие Controllers адаптируют Domain state к HUD без игровых правил.

**Tech Stack:** Unity 6000.3.28f1, URP 17.3.0, C#, NUnit EditMode/PlayMode, uGUI, Input System.

## Global Constraints

- Не менять Unity, URP, package baseline и маршрут карты.
- Не добавлять Upgrade/Sell, новые башни, packages, автозапуск или UI framework.
- Сохранить `TestWave.asset`: 150 Gold после пяти non-lethal leaks, 15 HP, 0 Kills, WaveComplete.
- Каждая функция сначала получает regression test; полный PASS не подменяется skipped/inconclusive.
- Для визуального PR нужны реальные Play Mode screenshots; self-merge запрещён.

---

### Task 1: Контракт и failing Domain tests

**Files:**
- Create: `Docs/P1.4A_WAVE_LIFECYCLE.md`
- Modify: `Docs/REGRESSION.md`
- Create: `Assets/_Game/Tests/EditMode/BattleWaveLifecycleTests.cs` + `.meta`
- Create: `Assets/_Game/Scripts/Domain/WaveTransitionResult.cs` + `.meta`

**Interfaces:**
- Produces: `WaveTransitionResult { Started, Continued, RejectedInvalidPhase, RejectedNoNextWave }`; `BattleSimulation.StartWave(): WaveTransitionResult`; `ContinueToNextWave(): WaveTransitionResult`; `BattleState.WaveCount`; saturated `Credit(int amount)`.

- [ ] **Step 1: Зафиксировать state/economy contract**

Сохранить таблицу переходов и инварианты из `Docs/P1.4A_WAVE_LIFECYCLE.md`; добавить REG-BATTLE-009…024 со ссылками на `BattleWaveLifecycleTests`.

- [ ] **Step 2: Написать failing tests восьми волн и команд**

```csharp
var simulation = MultiWave(counts: new[] { 5, 6, 7, 8, 9, 10, 11, 12 }, baseHealth: 100);
Assert.That(simulation.State.WaveCount, Is.EqualTo(8));
for (var wave = 0; wave < 8; wave++)
{
    Assert.That(simulation.State.WaveIndex, Is.EqualTo(wave));
    Assert.That(simulation.StartWave(), Is.EqualTo(WaveTransitionResult.Started));
    while (simulation.State.Phase == BattlePhase.Combat) simulation.Step();
    if (wave < 7)
    {
        Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.WaveComplete));
        Assert.That(simulation.ContinueToNextWave(), Is.EqualTo(WaveTransitionResult.Continued));
    }
}
Assert.That(simulation.State.Phase, Is.EqualTo(BattlePhase.Victory));
```

Отдельные tests проверяют relative spawn ticks, глобальные Enemy/Tower IDs, сохранение Gold/HP/Towers/cooldown, rejected-command snapshots и terminal freeze.

- [ ] **Step 3: Написать failing economy/determinism tests**

```csharp
Assert.That(simulation.State.Gold, Is.EqualTo(goldBefore + killedEnemy.Definition.Reward));
Assert.That(simulation.State.Kills, Is.EqualTo(killsBefore + 1));
// Leak: Gold/Kills не меняются. WaveComplete/Victory: bonus строго один раз.
// startingGold = int.MaxValue и reward/bonus > 0: Gold остаётся int.MaxValue.
```

Два полных матча с одинаковыми командами сериализуют WaveIndex/WaveCount/phase/tick/HP/Gold/Kills, enemies/towers и AttackEvents в одинаковые timelines.

- [ ] **Step 4: Запустить EditMode и подтвердить RED**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: FAIL компиляции из-за отсутствующих `WaveCount`, `ContinueToNextWave`, `WaveTransitionResult`; это фиксируется как expected RED, не как regression PASS.

### Task 2: Multi-wave Domain и economy

**Files:**
- Modify: `Assets/_Game/Scripts/Domain/BattleSimulation.cs`
- Modify: `Assets/_Game/Scripts/Domain/BattleState.cs`
- Modify: `Assets/_Game/Scripts/Domain/IBattleSimulation.cs`
- Test: `Assets/_Game/Tests/EditMode/BattleWaveLifecycleTests.cs`

**Interfaces:**
- Consumes: существующие `WaveDefinition`, `EnemyDefinition.Reward`, `PlaceTowerResult`.
- Produces: перегрузка `BattleSimulation(..., IReadOnlyList<WaveDefinition> waves, ..., int waveCompletionBonus)`; старые constructors делегируют одноволновому legacy пути.

- [ ] **Step 1: Реализовать последовательность и относительный schedule**

Скопировать и провалидировать непустой список волн в constructor; хранить `_waveStartTick` и `_nextSpawnIndex`. Старый constructor создаёт одну волну с bonus 0 и legacy WaveComplete; новый список завершается Victory.

```csharp
private long NextSpawnTick()
{
    return _waveStartTick + (long)CurrentWave.FirstSpawnTick +
           (long)_nextSpawnIndex * CurrentWave.SpawnIntervalTicks;
}
```

- [ ] **Step 2: Реализовать явные безопасные переходы**

`StartWave()` возвращает `RejectedInvalidPhase` вне Preparation; успех устанавливает `_waveStartTick = State.Tick`, сбрасывает spawn index, переводит Combat и вызывает due spawn. `ContinueToNextWave()` принимает только WaveComplete с существующей следующей волной, увеличивает WaveIndex один раз и переводит Preparation.

- [ ] **Step 3: Реализовать однократные выплаты и overflow guard**

В `RemoveDefeatedEnemies()` одновременно удалить мёртвого, увеличить Kills и вызвать `Credit(enemy.Definition.Reward)`. После проверки `BaseHealth == 0` не платить bonus; при успешном исчерпании волны вызвать `Credit(_waveCompletionBonus)` до установки WaveComplete/Victory. `Credit` насыщает на `int.MaxValue`.

- [ ] **Step 4: Запустить Domain tests до GREEN**

Run: `./Scripts/run-edit-mode-tests.sh`
Expected: новые lifecycle/economy tests и все прежние Domain tests PASS; если Unity runner BLOCKED, выполнить compile/test через доступный resident path и явно сохранить ограничение.

### Task 3: Сериализация восьми волн и Unity UI

**Files:**
- Modify: `Assets/_Game/Scripts/Presentation/BattleScenarioConfig.cs`
- Create: `Assets/_Game/Configs/EightWave.asset` + `.meta`
- Modify: `Assets/_Game/Scripts/Presentation/BattleSceneController.cs`
- Modify: `Assets/_Game/Scripts/Presentation/BattlePlacementController.cs`
- Modify: `Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs`
- Modify: `Assets/_Game/Scripts/Editor/BattleAssetValidator.cs`
- Modify: `Assets/_Game/Scenes/Battle.unity`
- Modify: config/builder/visual/PlayMode tests

**Interfaces:**
- Produces: `[Serializable] WaveSettings { enemyCount, firstSpawnTick, spawnIntervalTicks }`; `waves`, `sharedScenario`, `enemyReward`, `waveCompletionBonus`; `BattleSceneController.ContinueToNextWave()`; `nextWaveButton`.

- [ ] **Step 1: Добавить failing config/UI tests**

Проверить serialized counts `5…12`, reward 5, bonus 20, WaveCount 8, общий Path/Tower с `TestWave`; HUD содержит `Wave: 1 / 8`, `Phase`, `Result` для Victory/Defeat; Next Wave доступен только в WaveComplete.

- [ ] **Step 2: Реализовать config без дублирования маршрута**

При пустом `waves` использовать legacy поля и WaveComplete. При непустом списке создать `WaveDefinition[]`; map/path/enemy/tower/base/gold взять из `sharedScenario ?? this`. `OnValidate` ограничивает counts/ticks/rewards/bonus теми же доменными диапазонами.

- [ ] **Step 3: Обновить builder, scene и validators**

Builder создаёт `EightWave.asset` только при отсутствии, задаёт shared TestWave, reward 5, bonus 20 и counts `5…12`; Battle scene ссылается на EightWave. Добавить asset в reproducibility snapshots. Validator требует EightWave и `nextWaveButton`, сохраняет остальные reference/material/missing-script checks.

- [ ] **Step 4: Обновить существующий HUD**

Добавить Wave line и терминальный Result; разместить Next Wave под Start Wave тем же button style. Подписать/отписать listener, вызывать Domain command и обновлять `interactable` по фазе. Не менять placement raycast и mouse/touch flow.

- [ ] **Step 5: Запустить config/UI/legacy tests**

Run: `./Scripts/run-edit-mode-tests.sh && ./Scripts/run-play-mode-tests.sh`
Expected: все tests non-empty full PASS, включая REG-BATTLE-001…008 и загрузку Battle в Preparation с WaveCount 8.

### Task 4: Quality gates, Play Mode evidence и PR

**Files:**
- Modify: `README.md`, `Docs/TECH_DESIGN.md`, `Docs/REGRESSION.md`, `Docs/QUALITY_GATES.md`
- Create: `Docs/review/P1.4a/*.png` и evidence notes

- [ ] **Step 1: Запустить обязательные проверки**

```bash
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
git diff --check
```

Expected: положительные totals и нулевые failed/inconclusive/skipped; проверить `git diff --name-only`, `.meta`, GUID, packages и Missing Scripts.

- [ ] **Step 2: Захватить пять реальных Play Mode кадров**

Через Game View и виртуальный Input System получить Preparation 1/8, Combat 1/8, WaveComplete с увеличенным Gold, Preparation 2/8 и Victory или Defeat. Проверить каждый PNG визуально; console errors должны быть 0. Не выдавать Editor-only screenshots за Play Mode evidence.

- [ ] **Step 3: Обновить документацию фактическими результатами**

Записать P1.4a contract, точные команды/totals, environment, known limitations и отдельно VERIFIED / NOT VERIFIED. Не заявлять физический iPhone или незапущенный CI.

- [ ] **Step 4: Commit, push и PR без merge**

```bash
git add Assets Docs docs README.md
git commit -m "feat: add multi-wave lifecycle and rewards"
git push -u origin feature/p1-4a-wave-lifecycle
gh pr create --base main --title "P1.4a: Multi-Wave Lifecycle & Rewards" --body-file <pr-body>
```

Expected: PR содержит Summary, architecture, transition table, economy invariants, regression tests, gates, screenshots, VERIFIED / NOT VERIFIED и limitations. Merge не выполнять.
