// Reproducible whole-trace capture from the saved production config.
using System;
using System.IO;
using UnityEditor;
using Veilward.Domain;
using Veilward.Presentation;
using Veilward.Tests.Shared;

public static class CaptureHybridReplays
{
    public static string Before() => Capture("before");
    public static string After() => Capture("after");

    private static string Capture(string phase)
    {
        const string output = "TestResults/vfx1c/replay";
        Directory.CreateDirectory(output);
        var config = AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset");
        foreach (var start in new[] { "flame", "gale" })
        {
            var run = new AbilityBossTrace.Run(config.CreateSimulation(), AbilityBossTrace.Load(start));
            run.Play();
            if (run.sim.State.Phase != BattlePhase.Victory)
                throw new InvalidOperationException("Expected production Victory8 for " + start);
            AbilityBossTrace.Write(Path.Combine(output, phase + "-" + start), run);
        }
        return phase + ": full production FLAME and GALE traces";
    }
}
