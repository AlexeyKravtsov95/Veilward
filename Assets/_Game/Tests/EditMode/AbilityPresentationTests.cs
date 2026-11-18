using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.EditMode
{
    public sealed class AbilityPresentationTests
    {
        private BattleSceneController _battle;
        private BattlePlacementController _placement;
        private Component _ability;
        private BattleSimulation _s;
        private void Open(bool enabled = true, int hp = 1000)
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");
            _battle = Object.FindFirstObjectByType<BattleSceneController>();
            _placement = _battle.GetComponent<BattlePlacementController>();
            Call(_battle, "Start"); Call(_placement, "Start");
            _ability = _battle.GetComponent("AbilityController");
            Assert.That(_ability, Is.Not.Null, "REG208 builder must wire AbilityController");
            Call(_ability, "Start");
            _s = AbilityCheckpointTests.Create(enabled, enemyHp: hp);
            AbilityCheckpointTests.Choose(_s);
            Set(_battle, "_simulation", _s);
            _battle.GetComponent<BattleCheckpointController>().Configure(null, _s, AbilityCheckpointTests.Content);
            _battle.ResetPresentation();
            _battle.StartWave(); Call(_ability, "Refresh");
        }
        [TearDown] public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        [Test]
        public void RegBattle204_RealButtonsConfirmAfterStepsExactlyOnce()
        {
            Open(); Button("Ability Button").onClick.Invoke();
            Assert.That(Get("HasInputOwnership"), Is.EqualTo(true)); Assert.That(Get("HasTarget"), Is.EqualTo(false));
            Assert.That(Call(_ability, "SelectTarget", new PathPosition(0, 0)), Is.EqualTo(true));
            Button("Confirm Ability Button").onClick.Invoke(); Button("Confirm Ability Button").onClick.Invoke();
            Assert.That(_s.AbilityState.NextCastSequence, Is.EqualTo(1), "callback queues intent, never casts before Steps");
            _s.Step(); Call(_battle, "ProcessAttackEvents"); Call(_battle, "LateUpdate");
            Assert.That(_s.AbilityState.NextCastSequence, Is.EqualTo(2));
            Assert.That(_s.AbilityState.CooldownRemainingTicks, Is.EqualTo(600));
            Assert.That(_s.DamageEvents, Is.Empty); Assert.That(_s.AbilityEvents, Is.Empty);
            Assert.That(Get("Status"), Is.EqualTo("Печать: 2 hits, 240 HP"));
        }
        [TestCase(false)] [TestCase(true)]
        public void RegBattle204_CancelOrEmptyNeverCastsAndConsumesLease(bool empty)
        {
            Open(); Call(_ability, "BeginPreview");
            Call(_ability, "SelectTarget", empty ? new PathPosition(4, 2) : new PathPosition(0, 0));
            if (empty) Call(_ability, "ConfirmPreview"); else Button("Cancel Ability Button").onClick.Invoke();
            Call(_battle, "LateUpdate");
            Assert.That(_s.AbilityState.NextCastSequence, Is.EqualTo(1));
            Assert.That(Get("HasInputOwnership"), Is.EqualTo(false));
            if (empty) Assert.That(Get("Status"), Does.Contain("NoTargets"));
        }
        [Test]
        public void RegBattle204_DisabledProductionAndNonCombatRejectPublicApi()
        {
            Open(false); Assert.That(Button("Ability Button").gameObject.activeSelf, Is.False);
            Assert.That(Call(_ability, "BeginPreview"), Is.EqualTo(false));
            Assert.That(Call(_ability, "SelectTarget", new PathPosition(0, 0)), Is.EqualTo(false));
            Button("Confirm Ability Button").onClick.Invoke(); Call(_battle, "LateUpdate");
            Assert.That(_s.AbilityState.NextCastSequence, Is.EqualTo(1));
        }
        [Test]
        public void RegBattle205_StaleCallbackCannotCastNewGeneration()
        {
            Open(); Call(_ability, "BeginPreview"); Call(_ability, "SelectTarget", new PathPosition(0, 0));
            var lease = Get("Lease"); Call(_ability, "CancelPreview");
            Call(_ability, "BeginPreview"); Call(_ability, "SelectTarget", new PathPosition(0, 0));
            Assert.That(Call(_ability, "ConfirmLease", lease), Is.EqualTo(false)); Call(_battle, "LateUpdate");
            Assert.That(_s.AbilityState.NextCastSequence, Is.EqualTo(1));
        }
        [TestCase("placement")] [TestCase("inspector")] [TestCase("pause")] [TestCase("choice")] [TestCase("sell")]
        public void RegBattle205_ForeignOwnerBlocksAbilityCallbacksAndDirectApi(string owner)
        {
            Open();
            if (owner == "pause") _battle.GetComponent<BattlePauseController>().Pause();
            if (owner == "placement" || owner == "sell")
            {
                _s = AbilityCheckpointTests.Create(); AbilityCheckpointTests.Choose(_s); Set(_battle, "_simulation", _s);
                _placement.TrySelectCell(new GridCoordinate(0, 1));
                if (owner == "sell")
                {
                    _placement.SelectTowerType(new TowerDefinitionId("flame")); _placement.ConfirmSelection();
                    _placement.TrySelectCell(new GridCoordinate(0, 1)); _placement.RequestSellSelectedTower();
                }
            }
            if (owner == "inspector") Assert.That(_battle.GetComponent<EnemyInspectorController>().Select(_s.State.Enemies[0].Id), Is.True);
            if (owner == "choice") { _s = AbilityCheckpointTests.Create(); Set(_battle, "_simulation", _s); }
            Button("Ability Button").onClick.Invoke(); Assert.That(Call(_ability, "BeginPreview"), Is.EqualTo(false));
            Button("Confirm Ability Button").onClick.Invoke(); Call(_battle, "LateUpdate");
            Assert.That(_s.AbilityState.NextCastSequence, Is.EqualTo(1)); Assert.That(Get("HasInputOwnership"), Is.EqualTo(false));
        }
        [TestCase("manual")] [TestCase("background")] [TestCase("focus")]
        public void RegBattle206_PauseInvalidatesQueuedIntentAndLease(string kind)
        {
            Open(); Call(_ability, "BeginPreview"); Call(_ability, "SelectTarget", new PathPosition(0, 0)); Call(_ability, "ConfirmPreview");
            var pause = _battle.GetComponent<BattlePauseController>();
            if (kind == "manual") pause.Pause(); else if (kind == "focus") pause.OnApplicationFocus(false); else pause.OnApplicationPause(true);
            var tick = _s.State.Tick; Call(_battle, "Update"); Call(_battle, "LateUpdate");
            Assert.That(_s.State.Tick, Is.EqualTo(tick)); Assert.That(_s.AbilityState.NextCastSequence, Is.EqualTo(1));
            if (kind == "focus") pause.OnApplicationFocus(true); if (kind == "background") pause.OnApplicationPause(false);
            Assert.That(pause.IsPaused, Is.True); pause.Resume(); Call(_battle, "Update"); Call(_battle, "LateUpdate");
            Assert.That(_s.State.Tick, Is.EqualTo(tick)); Assert.That(Get("HasInputOwnership"), Is.EqualTo(false));
        }
        [Test]
        public void RegBattle207_ResetClearsIntentAndSameTickLethalView()
        {
            Open(hp:100); Call(_ability, "BeginPreview"); Call(_ability, "SelectTarget", new PathPosition(0, 0));
            Call(_ability, "ConfirmPreview"); Call(_battle, "LateUpdate");
            Assert.That(_s.State.Kills, Is.EqualTo(1)); Assert.That(_s.State.Enemies, Is.Empty);
            Assert.That(Get("Status"), Does.Contain("100"));
            _battle.ResetPresentation(); Assert.That(Get("Status"), Is.EqualTo(string.Empty));
            Assert.That(Get("HasInputOwnership"), Is.EqualTo(false));
        }
        [Test]
        public void RegBattle204_TargetMovedAfterPreviewRejectsAtDispatch()
        {
            Open(); Call(_ability, "BeginPreview"); Call(_ability, "SelectTarget", new PathPosition(0,0));
            for (var i = 0; i < 5000; i++) _s.Step();
            Call(_ability,"Refresh"); Assert.That(Get("Status"),Is.EqualTo("NoTargets"));
            Assert.That(Button("Confirm Ability Button").interactable,Is.False);
            var line=Object.FindObjectsByType<AbilityPreviewView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single().GetComponent<LineRenderer>();
            var color=new MaterialPropertyBlock(); line.GetPropertyBlock(color); Assert.That(color.GetColor("_BaseColor"),Is.EqualTo(Color.red));
            Call(_ability, "ConfirmPreview"); Call(_battle, "LateUpdate");
            Assert.That(Get("Status"), Does.Contain("NoTargets")); Assert.That(_s.AbilityState.NextCastSequence, Is.EqualTo(1));
            Assert.That(Get("HasInputOwnership"), Is.EqualTo(false));
        }
        [Test]
        public void RegBattle205_AbilityOwnerBlocksAllForeignMutationAndRetryCallbacks()
        {
            Open(); Call(_ability, "BeginPreview");
            var snapshot = BattleAbilityTests.FullSnapshot(_s);
            Assert.That(_placement.TrySelectCell(new GridCoordinate(0,1)), Is.False);
            Assert.That(_battle.GetComponent<EnemyInspectorController>().Select(_s.State.Enemies[0].Id), Is.False);
            Assert.That(_battle.PlaceTower(new TowerDefinitionId("flame"), new GridCoordinate(0,1)).Success, Is.False);
            Assert.That(_battle.SellTower(new TowerId(1)).Success, Is.False); Assert.That(_battle.UpgradeTower(new TowerId(1)).Success, Is.False);
            Assert.That(_battle.ChooseElement(new ChooseElementCommand("start-resonance","gale")).Success, Is.False);
            Assert.That(_battle.GetComponent<BattleCheckpointController>().RetrySave(), Is.False);
            foreach (var name in new[]{"Confirm Button","Start Wave Button","Next Wave Button","Upgrade Button","Sell Button","Confirm Sell Button","Confirm Element Button","Retry Save Button"})
                Button(name).onClick.Invoke();
            Assert.That(BattleAbilityTests.FullSnapshot(_s), Is.EqualTo(snapshot));
        }
        [Test]
        public void RegBattle207_AllFourStreamsClearEvenWhenConsumerThrows()
        {
            Open(); var dirty = BattleAbilityTests.CheckpointDirtyFixture(); Set(_battle,"_simulation",dirty);
            Assert.That(dirty.AbilityEvents, Is.Not.Empty); Assert.That(dirty.AttackEvents, Is.Not.Empty);
            Assert.That(dirty.ReactionEvents, Is.Not.Empty); Assert.That(dirty.DamageEvents, Is.Not.Empty);
            var field = typeof(BattleSceneController).GetField("_reactionPool",BindingFlags.Instance|BindingFlags.NonPublic);
            var pool = (ReactionEffectView[])field.GetValue(_battle);
            pool[0] = new GameObject("uninitialized consumer fault fixture").AddComponent<ReactionEffectView>();
            Assert.Throws<TargetInvocationException>(()=>Call(_battle,"ProcessAttackEvents"));
            Assert.That(dirty.AbilityEvents, Is.Empty); Assert.That(dirty.AttackEvents, Is.Empty);
            Assert.That(dirty.ReactionEvents, Is.Empty); Assert.That(dirty.DamageEvents, Is.Empty);
        }
        [Test]
        public void RegBattle207_RejectedRestorePreservesIntentSuccessfulRestoreClearsIt()
        {
            Open(); var checkpoint = _battle.GetComponent<BattleCheckpointController>();
            var cp = AbilityCheckpointTests.Create(); AbilityCheckpointTests.Choose(cp);
            var envelope = AbilityCheckpointTests.Envelope(cp);
            Call(_ability,"BeginPreview"); Call(_ability,"SelectTarget",new PathPosition(0,0)); Call(_ability,"ConfirmPreview");
            var bad = AbilityCheckpointTests.Envelope(cp); bad.state.ability = new AbilityCheckpoint("rift-seal-v1","rift-seal",601,2);
            Assert.Throws<ArgumentException>(()=>checkpoint.RestoreCheckpoint(bad));
            Assert.That(Get("HasInputOwnership"), Is.EqualTo(true));
            checkpoint.RestoreCheckpoint(envelope); Call(_battle,"LateUpdate");
            Assert.That(Get("HasInputOwnership"), Is.EqualTo(false)); Assert.That(_s.AbilityState.NextCastSequence,Is.EqualTo(1));
        }
        [TestCase(2)] [TestCase(5)]
        public void RegBattle206_CompletionOpensMandatoryChoiceWithoutPreview(int waveIndex)
        {
            Open(hp:100);
            for(var wave=0;wave<waveIndex;wave++)
            {
                for(var i=0;i<600;i++) _s.Step();
                Assert.That(_s.CastAbility(AbilityCheckpointTests.Command(_s)).Succeeded,Is.True);
                if(_s.State.ElementProgression.PendingChoiceId!="none") AbilityCheckpointTests.Choose(_s);
                _s.ContinueToNextWave(); _s.StartWave();
            }
            for(var i=0;i<600;i++) _s.Step();
            Call(_ability,"BeginPreview"); Call(_ability,"SelectTarget",new PathPosition(0,0)); Call(_ability,"ConfirmPreview");
            Call(_battle,"LateUpdate");
            var choice = _battle.GetComponent<ElementChoiceController>(); Call(choice,"Refresh");
            Assert.That(_s.State.Phase,Is.EqualTo(BattlePhase.WaveComplete));
            Assert.That(_s.State.ElementProgression.PendingChoiceId,Is.EqualTo(waveIndex==2?"after-wave-3":"after-wave-6"));
            Assert.That(Get("HasInputOwnership"),Is.EqualTo(false)); Assert.That(_battle.ActiveModalOwner,Is.EqualTo(BattleInputOwner.ElementChoice));
            Assert.That(choice.PreviewOptionId,Is.Null);
        }
        [Test]
        public void RegBattle204_AccumulatedLethalStepPrecedesIntent()
        {
            Open(hp:100);
            var enemy=new EnemyDefinition(new EnemyDefinitionId("lethal-step-fixture"),100,120f,1,5);
            var sim=new BattleSimulation(_s.Grid.Map,_s.Path,Enumerable.Range(0,8).Select(_=>new WaveDefinition(enemy,1,0,1)).ToArray(),
                _s.TowerCatalog,1,150,20,false,_s.ReactionCatalog,BattleElementUnlockTests.Rules(),AbilityDefinition.RiftSeal);
            AbilityCheckpointTests.Choose(sim); Set(_battle,"_simulation",sim); _battle.GetComponent<BattleCheckpointController>().Configure(null,sim,AbilityCheckpointTests.Content);
            _battle.ResetPresentation(); _battle.StartWave();
            Call(_ability,"BeginPreview"); Call(_ability,"SelectTarget",new PathPosition(0,0)); Call(_ability,"ConfirmPreview");
            Set(_battle,"_skipNextDelta",false); Set(_battle,"_accumulator",.034f);
            Call(_battle,"Update"); Call(_battle,"LateUpdate");
            Assert.That(sim.State.Phase,Is.EqualTo(BattlePhase.Defeat)); Assert.That(sim.State.Tick,Is.EqualTo(1));
            Assert.That(sim.AbilityState.NextCastSequence,Is.EqualTo(1)); Assert.That(Get("HasInputOwnership"),Is.EqualTo(false));
        }

        private object Get(string name) => _ability.GetType().GetProperty(name).GetValue(_ability);
        internal static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == name);
        internal static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
        internal static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
