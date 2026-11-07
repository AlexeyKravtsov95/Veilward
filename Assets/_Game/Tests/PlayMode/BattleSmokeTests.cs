using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Veilward.Domain;
using Veilward.Presentation;

namespace Veilward.Tests.PlayMode
{
    public sealed class BattleSmokeTests
    {
        [UnityTest]
        public IEnumerator Battle_LoadsIntoPreparation()
        {
            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            yield return null;

            var battle = Object.FindFirstObjectByType<BattleSceneController>();
            Assert.That(battle, Is.Not.Null);
            Assert.That(battle.Simulation, Is.Not.Null);
            Assert.That(battle.Simulation.State.Phase, Is.EqualTo(BattlePhase.Preparation));
            Assert.That(battle.Simulation.State.Gold, Is.EqualTo(150));
            Assert.That(battle.Simulation.State.BaseHealth, Is.EqualTo(20));
            Assert.That(battle.Simulation.State.Towers, Is.Empty);
            Assert.That(battle.Simulation.State.Enemies, Is.Empty);
        }
    }
}
