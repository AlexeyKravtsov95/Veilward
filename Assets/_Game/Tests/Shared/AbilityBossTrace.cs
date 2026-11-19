using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Veilward.Domain;
using Veilward.Infrastructure;
using Veilward.Presentation;

namespace Veilward.Tests.Shared
{
    // Новый test-only observer. Исторические writers/schema не меняются.
    public static class AbilityBossTrace
    {
        public const string Output = "TestResults/P2.4f";
        public static BossMissionScript Load(string start) => JsonUtility.FromJson<BossMissionScript>(
            File.ReadAllText("Docs/review/P2.4f/revision-3/inputs/" + start + ".commands.json"));
        public static WaveCheckpointEnvelope Envelope(BattleSimulation s) => new WaveCheckpointEnvelope
        { schemaVersion = 4, scenarioContentId = WaveCheckpointCompatibility.BalancedAbilityBossContentId, state = s.ExportCheckpoint() };
        public static string Wire(BattleSimulation s)
        {
            var cp=s.ExportCheckpoint();var a=cp.ability;
            return JsonUtility.ToJson(cp)+"|ability:"+a.RulesId+","+a.AbilityId+","+a.CooldownRemainingTicks+","+a.NextCastSequence;
        }
        public static int NextEnemy(BattleSimulation s) => (int)Field(s, "_nextEnemyId");
        private static object Field(BattleSimulation s, string name) => typeof(BattleSimulation)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s);
        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static string P(PathPosition p) => F(p.X) + "," + F(p.Y);
        public static string Snapshot(BattleSimulation s)
        {
            var state = s.State;
            var b = new StringBuilder("S|").Append(state.Phase).Append('|').Append(state.WaveIndex).Append('|').Append(state.Tick)
                .Append('|').Append(state.BaseHealth).Append('|').Append(state.Gold).Append('|').Append(state.Kills)
                .Append("|IDs:").Append(Field(s, "_nextEnemyId")).Append(',').Append(Field(s, "_nextTowerId"));
            var ability = s.AbilityState;
            b.Append("|Ability:").Append(ability.RulesId).Append(',').Append(ability.AbilityId.Value).Append(',')
                .Append(ability.CooldownRemainingTicks).Append(',').Append(ability.NextCastSequence);
            foreach (var t in state.Towers) b.Append("|T:").Append(t.Id.Value).Append(',').Append(t.Definition.Id.Value).Append(',')
                .Append(t.Level).Append(',').Append(t.Coordinate.X).Append(',').Append(t.Coordinate.Y).Append(',').Append(t.InvestedGold).Append(',').Append(t.NextAttackTick);
            foreach (var e in state.Enemies)
            {
                b.Append("|E:").Append(e.Id.Value).Append(',').Append(e.Definition.Id.Value).Append(',').Append(e.Health).Append(',').Append(F(e.DistanceAlongPath))
                    .Append(',').Append(e.LastSteamTick).Append(',').Append(e.LastStormTick);
                foreach (var st in e.Statuses) b.Append("|Status:").Append(st.Kind).Append(',').Append(st.Power).Append(',').Append(st.RemainingTicks)
                    .Append(',').Append(st.NextDamageTick).Append(',').Append(st.DamageIntervalTicks).Append(',').Append(st.SourceTowerId.Value);
            }
            var m = s.Grid.Map;
            for (var y = m.Origin.Y; y < m.Origin.Y + m.Height; y++)
                for (var x = m.Origin.X; x < m.Origin.X + m.Width; x++) b.Append('|').Append((int)s.Grid.GetState(new GridCoordinate(x, y)));
            var p = state.ElementProgression;
            b.Append("|P:").Append(p.RulesId).Append(',').Append(p.StartResonanceId).Append(',').Append(p.ChoicesEarned).Append(',').Append(p.ChoicesSpent).Append(',').Append(p.PendingChoiceId);
            foreach (var e in p.UnlockedElements) b.Append("|U:").Append(e);
            foreach (var h in p.SpentChoices) b.Append("|H:").Append(h.ChoiceId).Append(',').Append(h.ElementKind);
            foreach (var a in s.AttackEvents) b.Append("|A:").Append(a.Tick).Append(',').Append(a.AttackerTowerId.Value).Append(',').Append(a.TargetEnemyId.Value)
                .Append(',').Append(P(a.TargetPosition)).Append(',').Append(a.Damage).Append(',').Append(a.WasLethal);
            foreach (var e in s.ReactionEvents) b.Append("|R:").Append(e.Tick).Append(',').Append(e.SequenceInTick).Append(',').Append(e.Kind).Append(',').Append(e.TriggerKind)
                .Append(',').Append(e.SourceTowerId.Value).Append(',').Append(e.PrimaryEnemyId.Value).Append(',').Append(P(e.PrimaryPosition)).Append(',').Append(e.CauseFirstKind)
                .Append(',').Append(e.CauseSecondKind).Append(',').Append(F(e.RadiusOrHopRange)).Append(',').Append(e.TargetLimit);
            foreach (var e in s.AbilityEvents) b.Append("|Cast:").Append(e.Tick).Append(',').Append(e.SequenceInTick).Append(',').Append(e.AbilityId.Value)
                .Append(',').Append(e.CastSequence).Append(',').Append(P(e.Center)).Append(',').Append(F(e.Radius)).Append(',').Append(e.HitCount);
            foreach (var d in s.DamageEvents) b.Append("|D:").Append(d.Tick).Append(',').Append(d.SequenceInTick).Append(',').Append(d.Origin).Append(',').Append(d.SourceTowerId.Value)
                .Append(',').Append(d.SourceAbilityId?.Value).Append(',').Append(d.AbilityCastSequence).Append(',').Append(d.TargetEnemyId.Value)
                .Append(',').Append(P(d.FromPosition)).Append(',').Append(P(d.TargetPosition)).Append(',').Append(d.ConfiguredBaseDamage).Append(',').Append(d.RawDamage)
                .Append(',').Append(d.ResolvedDamage).Append(',').Append(d.AppliedHpLoss).Append(',').Append(d.ResolvedDamage - d.AppliedHpLoss)
                .Append(',').Append(d.HealthAfter).Append(',').Append(d.WasLethal).Append(',').Append(d.ReactionKind).Append(',').Append(d.ReactionSequenceInTick).Append(',').Append(d.HitIndex);
            if (state.Phase != BattlePhase.Combat) b.Append("|CP:").Append(Wire(s));
            return b.ToString();
        }
        public static void Clear(BattleSimulation s)
        { s.ClearAttackEvents(); s.ClearDamageEvents(); s.ClearReactionEvents(); s.ClearAbilityEvents(); }
        public static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
        public static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        public static void Write(string path, Run run)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var bytes = Encoding.UTF8.GetBytes(string.Join("\n", run.trace) + "\n");
            run.report.traceSha256 = Hash(bytes); run.report.rawBytes = bytes.Length; run.report.entries = run.trace.Count;
            using (var f = File.Create(path + ".trace.gz")) using (var zip = new GZipStream(f, System.IO.Compression.CompressionLevel.Optimal)) zip.Write(bytes, 0, bytes.Length);
            File.WriteAllText(path + ".json", JsonUtility.ToJson(run.report, true) + "\n");
        }
        [Serializable] public sealed class Cell { public string kind, trigger; public int procs, applied; }
        [Serializable] public sealed class EnemyAccount
        {
            public int id, maxHp, applied, remaining, leakedHp, reward; public bool lethal, leaked; public string type;
            public int[] byOrigin = new int[4], overkill = new int[4];
        }
        [Serializable] public sealed class Wave
        { public int wave, hp, gold, kills, abilityKills, bossKills, remaining; public long sequence, tick; public string phase, checkpoint; public int[] cells; }
        [Serializable] public sealed class Report
        {
            public string start, outcome, traceSha256; public int entries, rawBytes, abilityKills, abilityApplied, bossKills, bossLeaks, bossHpBeforeLeak, goldCommands, killGold, bonusGold;
            public Cell[] cells = { new Cell { kind="Steam",trigger="StatusPair" }, new Cell {kind="Steam",trigger="Intrinsic"},
                new Cell {kind="Storm",trigger="StatusPair"},new Cell {kind="Storm",trigger="Intrinsic"} };
            public List<Wave> waves = new List<Wave>(); public List<EnemyAccount> enemies = new List<EnemyAccount>();
            public List<string> receipts = new List<string>();
        }
        public sealed class Run
        {
            public BattleSimulation sim; public readonly BossMissionScript script;
            public readonly List<string> trace = new List<string>(); public readonly Report report = new Report();
            public Action<Run,string> stable;
            private int waveFirstId = 1, knownNext = 1;
            public Run(BattleSimulation simulation, BossMissionScript mission)
            { sim=simulation; script=mission; report.start=mission.start; Require(sim.State.Gold==150&&sim.State.BaseHealth==20,"production NewGame150/20"); Capture("NewGame"); }
            public void Capture(string receipt)
            { report.receipts.Add($"{sim.State.WaveIndex+1}|{sim.State.Tick}|{receipt}|Gold={sim.State.Gold}|HP={sim.State.BaseHealth}|Kills={sim.State.Kills}"); trace.Add("C|"+receipt); trace.Add(Snapshot(sim)); }
            public void Choose(string option)
            {
                var id=sim.State.ElementProgression.PendingChoiceId;
                var result=sim.ChooseElement(new ChooseElementCommand(id,option));Require(result.Success,"Choose "+option);
                Capture("Choose "+id+"="+option+" "+result.FailureReason); stable?.Invoke(this,"confirmed");
                Probe("repeated/stale Choose",()=>!sim.ChooseElement(new ChooseElementCommand(id,option)).Success);
            }
            public void Probe(string name,Func<bool> reject)
            { var before=Snapshot(sim);Require(reject(),name+" unexpectedly accepted");Require(Snapshot(sim)==before,name+" whole state/events changed"); Capture("Rejected "+name); }
            public void Closed()
            {
                foreach(var type in sim.TowerCatalog.Archetypes.Where(t=>t.RequiredElements.Any(e=>!sim.State.ElementProgression.UnlockedElements.Contains(e))))
                    Probe("closed "+type.Id.Value,()=>!sim.PlaceTower(new PlaceTowerCommand(type.Id,new GridCoordinate(-4,0))).Success);
            }
            public void Execute(BossMissionCommand c)
            {
                var beforeGold=sim.State.Gold;var price=0;var refund=0;bool success;
                if(c.op=="place") { price=sim.TowerCatalog.Get(new TowerDefinitionId(c.type)).Levels[0].Price;
                    success=sim.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(c.type),new GridCoordinate(c.x,c.y))).Success; }
                else
                {
                    var tower=sim.State.Towers.Single(t=>t.Id.Value==c.id);
                    if(c.op=="upgrade") { price=sim.TowerCatalog.Get(tower.Definition.Id).Levels[tower.Level].Price; success=sim.UpgradeTower(tower.Id).Success; }
                    else { refund=tower.InvestedGold*70/100;var result=sim.SellTower(tower.Id);success=result.Success;Require(result.Refund==refund,"refund"); }
                }
                Require(success,"illegal script command "+JsonUtility.ToJson(c));Require(sim.State.Gold==beforeGold-price+refund,"command budget");
                report.goldCommands+=refund-price; Capture(c.op+" "+c.type+" "+c.x+","+c.y+" #"+c.id+" price="+price+" refund="+refund);
                stable?.Invoke(this,"economy");
                if(c.op=="sell")Probe("repeated Sell #"+c.id,()=>!sim.SellTower(new TowerId(c.id)).Success);
                if(c.op=="place")Probe("occupied Place",()=>!sim.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(c.type),new GridCoordinate(c.x,c.y))).Success);
            }
            public void Start()
            {
                stable?.Invoke(this,"preparation");waveFirstId=NextEnemy(sim);
                Require(sim.StartWave()==WaveTransitionResult.Started,"Start");Account(BattlePhase.Preparation);Capture("Start"); Clear(sim);
                Probe("repeated Start",()=>sim.StartWave()!=WaveTransitionResult.Started);
            }
            public void Cast(PathPosition center)
            {
                var cmd=new CastAbilityCommand(sim.AbilityState.AbilityId,center,sim.State.WaveIndex,sim.State.Tick,sim.AbilityState.NextCastSequence);
                var phase=sim.State.Phase;var result=sim.CastAbility(cmd);Require(result.Succeeded,"script Cast rejected "+result.FailureReason);
                Account(phase);Capture($"Cast after tick={cmd.ExpectedTick} before next Step wave={cmd.ExpectedWaveIndex} expectedSequence={cmd.ExpectedCastSequence} center={P(center)} accepted={result.CastSequence} hits={result.HitCount} applied={result.TotalAppliedHpLoss}");
                Probe("stale/double Cast",()=>!sim.CastAbility(cmd).Succeeded);
                Probe("invalid center",()=>!sim.CastAbility(new CastAbilityCommand(sim.AbilityState.AbilityId,new PathPosition(99,99),sim.State.WaveIndex,sim.State.Tick,sim.AbilityState.NextCastSequence)).Succeeded);
                Clear(sim);
            }
            public void Step()
            { var phase=sim.State.Phase; sim.Step(); Account(phase); trace.Add("Step");trace.Add(Snapshot(sim)); Clear(sim); }
            private void Account(BattlePhase priorPhase)
            {
                for(var id=knownNext;id<NextEnemy(sim);id++)
                { var d=sim.Waves[sim.State.WaveIndex].Schedule[id-waveFirstId].Enemy;
                    report.enemies.Add(new EnemyAccount{id=id,maxHp=d.MaxHealth,remaining=d.MaxHealth,reward=d.Reward,type=d.Id.Value}); }
                knownNext=NextEnemy(sim);
                foreach(var d in sim.DamageEvents)
                {
                    var e=report.enemies.Single(x=>x.id==d.TargetEnemyId.Value);Require(!e.lethal&&!e.leaked,"damage after removal");
                    Require(d.AppliedHpLoss==Math.Min(e.remaining,d.ResolvedDamage),"HP clamp/accounting"); e.applied+=d.AppliedHpLoss;e.remaining-=d.AppliedHpLoss;
                    Require(e.remaining==d.HealthAfter,"per-ID health after");Require(d.WasLethal==(d.HealthAfter==0),"lethal flag");e.byOrigin[(int)d.Origin]+=d.AppliedHpLoss;e.overkill[(int)d.Origin]+=d.ResolvedDamage-d.AppliedHpLoss;
                    if(d.Origin==DamageOrigin.Ability) {report.abilityApplied+=d.AppliedHpLoss;Require(d.SourceAbilityId.HasValue&&d.AbilityCastSequence.HasValue&&d.SourceTowerId.Value==0,"ability attribution");}
                    if(d.WasLethal) {Require(e.remaining==0&&!e.lethal,"duplicate lethal");e.lethal=true;report.killGold+=e.reward;
                        if(d.Origin==DamageOrigin.Ability)report.abilityKills++;if(e.type=="rift-warden")report.bossKills++;}
                }
                foreach(var reaction in sim.ReactionEvents)
                {
                    var damage=sim.DamageEvents.Where(d=>d.Origin==DamageOrigin.Reaction&&d.Tick==reaction.Tick&&d.ReactionSequenceInTick==reaction.SequenceInTick&&d.ReactionKind==reaction.Kind).Sum(d=>d.AppliedHpLoss);
                    if(damage>0) {var c=report.cells.Single(x=>x.kind==reaction.Kind.ToString()&&x.trigger==reaction.TriggerKind.ToString());c.procs++;c.applied+=damage;}
                }
                foreach(var e in report.enemies.Where(e=>!e.lethal&&!e.leaked))
                { if(!sim.State.Enemies.Any(x=>x.Id.Value==e.id)) {e.leaked=true;e.leakedHp=e.remaining;if(e.type=="rift-warden"){report.bossLeaks++;report.bossHpBeforeLeak=e.remaining;}}
                    else Require(sim.State.Enemies.Single(x=>x.Id.Value==e.id).Health==e.remaining,"live HP accounting"); }
                if(priorPhase==BattlePhase.Combat&&sim.State.Phase!=BattlePhase.Combat&&sim.State.Phase!=BattlePhase.Defeat)report.bonusGold+=20;
                Require(sim.State.Gold==150+report.goldCommands+report.killGold+report.bonusGold,"Gold commands + unique kill + unique bonus");
                Require(sim.State.Kills==report.enemies.Count(x=>x.lethal),"Kills accounting");
                Require(sim.State.BaseHealth==Math.Max(0,20-report.enemies.Where(e=>e.leaked).Sum(e=>e.type=="rift-warden"?20:1)),"base HP/leak accounting");
                Require(report.enemies.All(e=>e.applied+(e.leaked?e.leakedHp:e.remaining)==e.maxHp),"spawnHP=applied+leak/live by EnemyId");
            }
            public void FinishWave()
            {
                Capture("Wave end"); report.waves.Add(new Wave{wave=sim.State.WaveIndex+1,hp=sim.State.BaseHealth,gold=sim.State.Gold,kills=sim.State.Kills,
                    phase=sim.State.Phase.ToString(),tick=sim.State.Tick,remaining=sim.AbilityState.CooldownRemainingTicks,sequence=sim.AbilityState.NextCastSequence,
                    abilityKills=report.abilityKills,bossKills=report.bossKills,cells=report.cells.Select(c=>c.applied).ToArray(),checkpoint=Wire(sim)});
                stable?.Invoke(this,"completed");
            }
            public void Play()
            {
                stable?.Invoke(this,"initial"); Choose(script.start);Closed();
                while(sim.State.Phase==BattlePhase.Preparation)
                {
                    Closed();foreach(var c in script.commands.Where(c=>c.wave==sim.State.WaveIndex+1))Execute(c);
                    Start();var guard=0;
                    while(sim.State.Phase==BattlePhase.Combat&&guard++<20000)
                    { if(script.TryCenter(sim,out var center))Cast(center); if(sim.State.Phase==BattlePhase.Combat)Step(); }
                    Require(guard<20000,"bounded replay");FinishWave();
                    if(sim.State.Phase!=BattlePhase.WaveComplete)break;
                    if(sim.State.ElementProgression.PendingChoiceId != "none") {Probe("Next pending",()=>sim.ContinueToNextWave()!=WaveTransitionResult.Continued);Choose(sim.State.WaveIndex==2?script.after3:script.after6);Closed();}
                    Require(sim.ContinueToNextWave()==WaveTransitionResult.Continued,"Next");Capture("Next");stable?.Invoke(this,"next");
                    Probe("repeated Next",()=>sim.ContinueToNextWave()!=WaveTransitionResult.Continued);
                }
                report.outcome=sim.State.Phase.ToString();Probe("terminal Start",()=>sim.StartWave()!=WaveTransitionResult.Started);
                Probe("terminal Next",()=>sim.ContinueToNextWave()!=WaveTransitionResult.Continued);Probe("terminal Step",()=>{sim.Step();return true;});
            }
        }
    }
}
