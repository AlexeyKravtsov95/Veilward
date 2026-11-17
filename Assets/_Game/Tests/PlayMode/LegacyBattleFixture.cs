using System.Reflection;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Tests.PlayMode
{
    // Исторические UI/combat controls используют только compatibility factory, без production toggle.
    internal static class LegacyBattleFixture
    {
        internal static BattleSimulation Create(BattleScenarioConfig scenario)
        {
            return scenario.CreateSimulationForCheckpoint(new WaveCheckpointEnvelope
            {
                schemaVersion = 3, scenarioContentId = "eight-wave-v1",
                state = new BattleCheckpoint { elementProgression = ElementProgressionCheckpoint.CreateLegacy() }
            });
        }
        internal static void Activate(WaveCheckpointStore store = null)
        {
            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var scenario = (BattleScenarioConfig)typeof(BattleSceneController).GetField("scenario", flags).GetValue(battle);
            var simulation = Create(scenario);
            typeof(BattleSceneController).GetField("_simulation", flags).SetValue(battle, simulation);
            var checkpoint = Object.FindFirstObjectByType<BattleCheckpointController>();
            checkpoint.Configure(store, simulation, "eight-wave-v1", scenario.CreateSimulationForCheckpoint);
            checkpoint.SaveCurrentState();
            battle.ResetPresentation();
            Object.FindFirstObjectByType<ElementChoiceController>().Refresh();
        }
    }
}
