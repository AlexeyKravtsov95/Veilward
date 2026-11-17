using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;

namespace Veilward.Tests.PlayMode
{
    // Только исторические combat/economy controls. Новые production suites этот fixture не используют.
    public abstract class HistoricalAllOpenPlayModeFixture
    {
        [SetUp] public void InstallHistoricalRules() => SceneManager.sceneLoaded += Install;
        [TearDown] public void RemoveHistoricalRules() => SceneManager.sceneLoaded -= Install;
        private static void Install(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Battle") Object.FindFirstObjectByType<BattleSceneController>().gameObject.AddComponent<HistoricalAllOpenSceneFixture>();
        }
    }

    [DefaultExecutionOrder(-90)]
    public sealed class HistoricalAllOpenSceneFixture : MonoBehaviour
    {
        private void Start()
        {
            var battle = GetComponent<BattleSceneController>();
            if (battle.Simulation.State.ElementProgression.RulesId == "legacy-all-open-v1") return;
            var config = (BattleScenarioConfig)Get(battle, "scenario");
            var historical = config.CreateHistoricalAllOpenSimulation();
            typeof(BattleSceneController).GetField("_simulation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(battle, historical);
            var checkpoint = GetComponent<BattleCheckpointController>();
            var store = (WaveCheckpointStore)Get(checkpoint, "_store");
            checkpoint.Configure(store, historical, "eight-wave-v1", config.CreateSimulationForCheckpoint);
            if (store != null) Assert.That(checkpoint.SaveCurrentState(), Is.True);
            GetComponent<ElementChoiceController>().Refresh();
        }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
