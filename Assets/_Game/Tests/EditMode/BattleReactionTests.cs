using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Veilward.Domain;

namespace Veilward.Tests.EditMode
{
    public sealed class BattleReactionTests
    {
        [TestCase(StatusKind.Burn, StatusKind.Wet, ReactionKind.Steam, 970)]
        [TestCase(StatusKind.Wet, StatusKind.Burn, ReactionKind.Steam, 970)]
        [TestCase(StatusKind.Wet, StatusKind.Gust, ReactionKind.Storm, 980)]
        [TestCase(StatusKind.Gust, StatusKind.Wet, ReactionKind.Storm, 978)]
        public void RegBattle132_133_BothPairOrders(StatusKind first, StatusKind second, ReactionKind kind, int hp)
        {
            var s = Create(new[] { Tower("a", first), Tower("b", second) });
            Place(s, "a", 0); Place(s, "b", 1); s.StartWave(); s.State.Enemies[0].Advance(5);
            var primary = s.State.Enemies[0]; Add(s, 2, 4.5f);
            s.Step();
            Assert.That(primary.Health, Is.EqualTo(hp));
            Assert.That(Reactions(s).Count, Is.EqualTo(1));
            Assert.That(Reactions(s)[0].Kind, Is.EqualTo(kind));
            Assert.That(Reactions(s)[0].SourceTowerId.Value, Is.EqualTo(2));
        }

        [TestCase(StatusKind.Burn)] [TestCase(StatusKind.Wet)] [TestCase(StatusKind.Gust)]
        public void RegBattle132_133_OneElementDoesNotProc(StatusKind kind)
        {
            var s = Create(new[] { Tower("a", kind) }); Place(s, "a", 0); s.StartWave(); s.State.Enemies[0].Advance(5); s.Step();
            Assert.That(s.State.Enemies[0].Health, Is.EqualTo(990));
            Assert.That(Reactions(s), Is.Empty);
        }

        [Test]
        public void RegBattle134_StoneIgnoresReadyPairsAndExpiredComplementCannotProc()
        {
            var s = Create(new[] { Tower("stone", StatusKind.Crack) }); Place(s, "stone", 0); s.StartWave(); s.State.Enemies[0].Advance(5);
            var p = s.State.Enemies[0]; Status(p, StatusKind.Burn); Status(p, StatusKind.Wet); Status(p, StatusKind.Gust);
            s.Step(); Assert.That(Reactions(s), Is.Empty); Assert.That(p.Health, Is.EqualTo(988));
            var expired = Create(new[] { Tower("a", StatusKind.Wet) }); Place(expired, "a", 0); expired.StartWave();
            Status(expired.State.Enemies[0], StatusKind.Burn, duration: 1);
            expired.Step(); Assert.That(Reactions(expired), Is.Empty); Assert.That(expired.State.Enemies[0].Burn, Is.Null);
        }

        [Test]
        public void RegBattle134_IncomingExpiredKindReappliesBeforeReactionWithoutConsumingComplement()
        {
            var s = Create(new[] { Tower("a", StatusKind.Wet) }); Place(s, "a", 0); s.StartWave(); s.State.Enemies[0].Advance(5);
            var p = s.State.Enemies[0]; Status(p, StatusKind.Burn); Status(p, StatusKind.Wet, duration: 1);
            var burn = p.Burn; var due = burn.NextDamageTick;
            s.Step(); Assert.That(p.Health, Is.EqualTo(980));
            Assert.That(p.Burn, Is.SameAs(burn)); Assert.That(burn.NextDamageTick, Is.EqualTo(due));
            Assert.That(burn.RemainingTicks, Is.EqualTo(999)); Assert.That(p.Wet.RemainingTicks, Is.EqualTo(1000));
        }

        [Test]
        public void RegBattle135_LethalDirectDoesNotProcOrReserveCooldown()
        {
            var s = Create(new[] { Tower("a", StatusKind.Wet, damage: 10) }, hp: 10);
            Place(s, "a", 0); s.StartWave(); s.State.Enemies[0].Advance(5); var p = s.State.Enemies[0]; Status(p, StatusKind.Burn);
            s.Step(); Assert.That(s.State.Kills, Is.EqualTo(1)); Assert.That(p.Wet, Is.Null);
            Assert.That(Reactions(s), Is.Empty); Assert.That(Last(p, ReactionKind.Steam), Is.Null);
            Assert.That(s.AttackEvents[0].WasLethal, Is.True);
        }

        [Test]
        public void RegBattle135_DueBurnKillsBeforeMovementAndNeverTriggersPairs()
        {
            var s = Create(new[] { Tower("unused", null) }, hp: 5, speed: 30);
            s.StartWave(); s.State.Enemies[0].Advance(5); var p = s.State.Enemies[0]; p.Advance(99);
            Status(p, StatusKind.Burn, power: 5, duration: 1, interval: 1); Status(p, StatusKind.Wet); Status(p, StatusKind.Gust);
            s.Step(); Assert.That(p.Health, Is.Zero); Assert.That(s.State.BaseHealth, Is.EqualTo(20));
            Assert.That(Reactions(s), Is.Empty); Assert.That(Damages(s)[0].Origin, Is.EqualTo(DamageOrigin.Burn));
            Assert.That(Damages(s)[0].TargetPosition.X, Is.EqualTo(99));
        }

        [TestCase(ReactionKind.Steam)] [TestCase(ReactionKind.Storm)]
        public void RegBattle136_FirstProcAndExactCooldownBoundary(ReactionKind kind)
        {
            var incoming = kind == ReactionKind.Steam ? StatusKind.Burn : StatusKind.Gust;
            var s = Create(new[] { Tower("a", incoming, fireRate: 30) }, hp: 10000);
            Place(s, "a", 0); s.StartWave(); s.State.Enemies[0].Advance(5); var p = s.State.Enemies[0]; Status(p, StatusKind.Wet); Add(s, 2, 4.5f, hp: 10000);
            s.Step(); Assert.That(Last(p, kind), Is.EqualTo(1));
            StepTo(s, 60); Assert.That(Reactions(s).Count, Is.EqualTo(1)); Assert.That(Last(p, kind), Is.EqualTo(1));
            s.Step(); Assert.That(Reactions(s).Count, Is.EqualTo(2)); Assert.That(Last(p, kind), Is.EqualTo(61));
            Assert.That(Last(s.State.Enemies[1], kind), Is.EqualTo(61));
        }

        [TestCase(ReactionKind.Steam)] [TestCase(ReactionKind.Storm)]
        public void RegBattle137_SecondarySharesPrimaryWindowAndOtherKindIsIndependent(ReactionKind kind)
        {
            var s = Create(new[] { Tower("a", kind == ReactionKind.Steam ? StatusKind.Burn : StatusKind.Gust, fireRate: 30) });
            Place(s, "a", 0); s.StartWave(); s.State.Enemies[0].Advance(5); var p = s.State.Enemies[0]; Status(p, StatusKind.Wet);
            var secondary = Add(s, 2, 4.5f); Status(secondary, StatusKind.Wet);
            s.Step(); Assert.That(Last(secondary, kind), Is.EqualTo(1));
            secondary.Advance(6); s.Step(); Assert.That(Reactions(s).Count, Is.EqualTo(1));
            Assert.That(Last(secondary, kind == ReactionKind.Steam ? ReactionKind.Storm : ReactionKind.Steam), Is.Null);
        }

        [Test]
        public void RegBattle138_SteamPriorityThenStormFallbackAndRestoredTowerIdOrder()
        {
            var s = Create(new[] { Tower("a", StatusKind.Wet), Tower("b", StatusKind.Wet) });
            Place(s, "a", 0); Place(s, "b", 1);
            var cp = s.ExportCheckpoint(); Array.Reverse(cp.towers); s.RestoreCheckpoint(cp); s.StartWave(); s.State.Enemies[0].Advance(5);
            var p = s.State.Enemies[0]; Status(p, StatusKind.Burn); Status(p, StatusKind.Gust); Add(s, 2, 4.5f);
            s.Step(); Assert.That(Reactions(s).Count, Is.EqualTo(2));
            Assert.That(Reactions(s)[0].Kind, Is.EqualTo(ReactionKind.Steam));
            Assert.That(Reactions(s)[1].Kind, Is.EqualTo(ReactionKind.Storm));
            Assert.That(Reactions(s)[0].SourceTowerId.Value, Is.EqualTo(1));
            Assert.That(Reactions(s)[1].SourceTowerId.Value, Is.EqualTo(2));
            Assert.That(p.Health, Is.EqualTo(966));
        }

        [Test]
        public void RegBattle139_SteamNearestThreeInclusiveRadiusAndEnemyIdTies()
        {
            var s = Create(new[] { Tower("a", StatusKind.Burn) }); Place(s, "a", 0); s.StartWave(); s.State.Enemies[0].Advance(5);
            var p = s.State.Enemies[0]; p.Advance(5); Status(p, StatusKind.Wet);
            var outside = Add(s, 2, 3.749f); var edge = Add(s, 3, 3.75f);
            Add(s, 5, 4); Add(s, 4, 4); var excluded = Add(s, 6, 4);
            s.Step(); var hits = ReactionHits(s);
            Assert.That(hits.ConvertAll(h => h.TargetEnemyId.Value), Is.EqualTo(new[] { 1, 4, 5, 6 }));
            Assert.That(edge.Health, Is.EqualTo(1000)); Assert.That(outside.Health, Is.EqualTo(1000));
            Assert.That(excluded.Health, Is.EqualTo(990)); Assert.That(Last(edge, ReactionKind.Steam), Is.Null);
            var boundary = Create(new[] { Tower("a", StatusKind.Burn) }); Place(boundary, "a", 0); boundary.StartWave();
            boundary.State.Enemies[0].Advance(5); Status(boundary.State.Enemies[0], StatusKind.Wet);
            var b = Add(boundary, 2, 3.75f); var o = Add(boundary, 3, 3.749f); boundary.Step();
            Assert.That(b.Health, Is.EqualTo(990)); Assert.That(o.Health, Is.EqualTo(1000));
        }

        [Test]
        public void RegBattle140_StormPlansChainBeforeLethalIntermediateAndCapturesPreviousOrigin()
        {
            var s = Create(new[] { Tower("a", StatusKind.Gust) }); Place(s, "a", 0); s.StartWave(); s.State.Enemies[0].Advance(5);
            var p = s.State.Enemies[0]; p.Advance(5); Status(p, StatusKind.Wet);
            var b = Add(s, 2, 3, hp: 7); var c = Add(s, 3, 1); var outside = Add(s, 4, 0.999f);
            s.Step(); var hits = ReactionHits(s);
            Assert.That(hits.Count, Is.EqualTo(2)); Assert.That(b.Health, Is.Zero); Assert.That(c.Health, Is.EqualTo(993));
            Assert.That(outside.Health, Is.EqualTo(1000)); Assert.That(p.Health, Is.EqualTo(990));
            Assert.That(hits[0].FromPosition.X, Is.EqualTo(5)); Assert.That(hits[1].FromPosition.X, Is.EqualTo(3));
            Assert.That(hits[0].HitIndex, Is.EqualTo(1)); Assert.That(hits[1].HitIndex, Is.EqualTo(2));
            Assert.That(Last(p, ReactionKind.Storm), Is.EqualTo(1)); Assert.That(Last(c, ReactionKind.Storm), Is.EqualTo(1));
        }

        [TestCase(0)] [TestCase(1)]
        public void RegBattle141_StormZeroOrOneNeighborDoesNotInventHits(int neighbors)
        {
            var s = Create(new[] { Tower("a", StatusKind.Gust, fireRate: 30) }); Place(s, "a", 0); s.StartWave(); s.State.Enemies[0].Advance(5);
            var p = s.State.Enemies[0]; p.Advance(5); Status(p, StatusKind.Wet);
            if (neighbors == 1) Add(s, 2, 3);
            s.Step(); Assert.That(Reactions(s).Count, Is.EqualTo(neighbors)); Assert.That(ReactionHits(s).Count, Is.EqualTo(neighbors));
            Assert.That(Last(p, ReactionKind.Storm), neighbors == 0 ? Is.Null : Is.EqualTo(1));
            Assert.That(s.State.Towers[0].NextAttackTick, Is.EqualTo(2));
        }

        [Test]
        public void RegBattle141_StormSkipsDeadLeakedLockedAndVisitedWithIdTie()
        {
            var s = Create(new[] { Tower("a", StatusKind.Gust) }); Place(s, "a", 0); s.StartWave(); s.State.Enemies[0].Advance(5);
            var p = s.State.Enemies[0]; p.Advance(5); Status(p, StatusKind.Wet);
            Add(s, 2, 4.75f, hp: 0); var locked = Add(s, 3, 4.5f); Reserve(locked, ReactionKind.Storm, 0);
            Add(s, 5, 4); Add(s, 4, 4); Add(s, 6, 100);
            s.Step(); Assert.That(ReactionHits(s).ConvertAll(h => h.TargetEnemyId.Value), Is.EqualTo(new[] { 4, 5 }));
            Assert.That(locked.Health, Is.EqualTo(1000)); Assert.That(s.State.BaseHealth, Is.EqualTo(19));
        }

        [TestCase(6, 75, 5, 2, 1, 4, 1)]
        [TestCase(25, 125, 5, 2, 7, 31, 28)]
        [TestCase(int.MaxValue, int.MaxValue, 0, 0, 7, int.MaxValue, int.MaxValue)]
        [TestCase(1, 1, 10, 0, 1, 1, 1)]
        public void RegBattle142_SecondaryFloorArmorCrackOverkillSaturation(int damage, int percent, int armor, int crack, int hp, int raw, int resolved)
        {
            var payload = new ReactionPayload(ReactionKind.Storm, percent, 2, 2);
            var s = Create(new[] { Tower("hybrid", null, damage, penetration: 99, intrinsic: payload) }, hp: int.MaxValue);
            Place(s, "hybrid", 0); s.StartWave(); s.State.Enemies[0].Advance(5); var p = s.State.Enemies[0];
            // Даже extreme direct должен оставить живую primary для запроса реакции.
            if (damage == int.MaxValue) p.ApplyStatus(new StatusEffectDefinition(StatusKind.Crack, 1, 100, 0), new TowerId(9), 0);
            var victim = Add(s, 2, 4.5f, hp: hp, armor: armor); Status(victim, StatusKind.Gust, power: 100);
            if (crack > 0) Status(victim, StatusKind.Crack, power: crack);
            if (damage == int.MaxValue)
            {
                // Direct после armor int.MaxValue-1, secondary saturation проверяет отдельную armor-0 цель.
                s = Create(new[] { Tower("hybrid", null, damage, intrinsic: payload) }, hp: int.MaxValue, armor: 1);
                Place(s, "hybrid", 0); s.StartWave(); s.State.Enemies[0].Advance(5); victim = Add(s, 2, 4.5f, hp: hp); Status(victim, StatusKind.Gust, power: 100);
            }
            s.Step(); var hit = ReactionHits(s)[0];
            Assert.That(hit.RawDamage, Is.EqualTo(raw)); Assert.That(hit.ResolvedDamage, Is.EqualTo(resolved));
            Assert.That(hit.AppliedHpLoss, Is.EqualTo(Math.Min(hp, resolved))); Assert.That(hit.ConfiguredBaseDamage, Is.EqualTo(damage));
        }

        [Test]
        public void RegBattle144_SecondaryDoesNotApplyRefreshStatusesOrRecurse()
        {
            var s = Create(new[] { Tower("a", StatusKind.Wet) }); Place(s, "a", 0); s.StartWave(); s.State.Enemies[0].Advance(5);
            Status(s.State.Enemies[0], StatusKind.Burn); var b = Add(s, 2, 4.5f);
            Status(b, StatusKind.Burn); Status(b, StatusKind.Wet); Status(b, StatusKind.Gust); Status(b, StatusKind.Crack);
            s.Step(); Assert.That(Reactions(s).Count, Is.EqualTo(1)); Assert.That(s.AttackEvents.Count, Is.EqualTo(1));
            Assert.That(b.Statuses.Count, Is.EqualTo(4)); foreach (var status in b.Statuses) Assert.That(status.RemainingTicks, Is.EqualTo(999));
            Assert.That(Last(b, ReactionKind.Storm), Is.Null);
        }

        [TestCase(ReactionKind.Steam)] [TestCase(ReactionKind.Storm)]
        public void RegBattle144_IntrinsicOnlyRequestsOwnKindWithoutStatuses(ReactionKind kind)
        {
            var s = Create(new[] { Tower("hybrid", null, intrinsic: new ReactionPayload(kind, 100, 2, 2)) });
            Place(s, "hybrid", 0); s.StartWave(); s.State.Enemies[0].Advance(5); var p = s.State.Enemies[0]; Add(s, 2, 4.5f);
            s.Step(); Assert.That(Reactions(s).Count, Is.EqualTo(1)); Assert.That(Reactions(s)[0].Kind, Is.EqualTo(kind));
            Assert.That(Reactions(s)[0].TriggerKind, Is.EqualTo(ReactionTriggerKind.Intrinsic)); Assert.That(p.Statuses, Is.Empty);
        }

        [Test]
        public void RegBattle144_HybridRequiresMatchingGlobalDefinitionIncludingCompatibilityConstructor()
        {
            var hybrid = Tower("hybrid", null, intrinsic: new ReactionPayload(ReactionKind.Steam, 100, 1, 1));
            Assert.Throws<ArgumentException>(() => Create(new[] { hybrid }, catalog: ReactionCatalog.Empty));
            var path = new PathDefinition(new[] { new GridCoordinate(0, 0), new GridCoordinate(10, 0) });
            Assert.Throws<ArgumentException>(() => new BattleSimulation(new GridMapDefinition(10, 4, new GridCoordinate(0, 0), 1),
                path, new WaveDefinition(new EnemyDefinition(new EnemyDefinitionId("e"), 10, 1, 1, 0), 1, 0, 1), hybrid, 20, 0));
        }

        internal static ReactionCatalog Catalog() => new ReactionCatalog(new[] {
            new ReactionDefinition(new ReactionPayload(ReactionKind.Steam, 100, 1.25f, 4), 60, 0),
            new ReactionDefinition(new ReactionPayload(ReactionKind.Storm, 75, 2, 2), 60, 1) });

        internal static TowerDefinition Tower(string id, StatusKind? kind, int damage = 10, float fireRate = .01f,
            int penetration = 0, ReactionPayload intrinsic = null) => new TowerDefinition(new TowerDefinitionId(id), 0, 20, fireRate, damage,
                kind.HasValue ? new StatusEffectDefinition(kind.Value, kind == StatusKind.Burn ? 1 : 20, 1000, kind == StatusKind.Burn ? 1000 : 0) : null,
                penetration, intrinsic);

        internal static BattleSimulation Create(TowerDefinition[] definitions, int hp = 1000, int speed = 0, int armor = 0,
            int gold = 0, int reward = 5, int bonus = 0, ReactionCatalog catalog = null)
        {
            var map = new GridMapDefinition(100, 8, new GridCoordinate(0, 0), 1);
            var path = new PathDefinition(new[] { new GridCoordinate(0, 0), new GridCoordinate(100, 0) });
            var enemy = new EnemyDefinition(new EnemyDefinitionId("e"), hp, speed == 0 ? .000001f : speed, 1, reward, armor);
            var towers = new TowerArchetypeDefinition[definitions.Length];
            for (var i = 0; i < towers.Length; i++) towers[i] = TowerArchetypeDefinition.CreateCompatibility(new[] { definitions[i] });
            var tc = new TowerCatalog(towers); var waves = new[] { new WaveDefinition(enemy, 1, 0, 1) };
            // RED bridge: существующая simulation продолжает работать, поэтому failures показывают отсутствие поведения, а не compile errors.
            var ctor = typeof(BattleSimulation).GetConstructor(new[] { typeof(GridMapDefinition), typeof(PathDefinition), typeof(IReadOnlyList<WaveDefinition>),
                typeof(TowerCatalog), typeof(int), typeof(int), typeof(int), typeof(ReactionCatalog) });
            if (ctor == null) return new BattleSimulation(map, path, waves, tc, 20, gold, bonus);
            try { return (BattleSimulation)ctor.Invoke(new object[] { map, path, waves, tc, 20, gold, bonus, catalog ?? Catalog() }); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }

        internal static void Place(BattleSimulation s, string id, int x) => Assert.That(s.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(id), new GridCoordinate(x, 2))).Success, Is.True);
        internal static EnemyState Add(BattleSimulation s, int id, float distance, int hp = 1000, int armor = 0)
        {
            var e = new EnemyState(new EnemyId(id), new EnemyDefinition(new EnemyDefinitionId("n"), Math.Max(1, hp), .000001f, 1, 5, armor), hp, distance);
            s.State.AddEnemy(e); return e;
        }
        internal static void Status(EnemyState e, StatusKind kind, int power = 20, int duration = 1000, int interval = 1000) =>
            e.ApplyStatus(new StatusEffectDefinition(kind, power, duration, kind == StatusKind.Burn ? interval : 0), new TowerId(99), 0);
        internal static void StepTo(BattleSimulation s, long tick) { while (s.State.Tick < tick) s.Step(); }
        internal static IReadOnlyList<ReactionEvent> Reactions(BattleSimulation s) => Read<IReadOnlyList<ReactionEvent>>(s, "ReactionEvents");
        internal static IReadOnlyList<DamageResolvedEvent> Damages(BattleSimulation s) => Read<IReadOnlyList<DamageResolvedEvent>>(s, "DamageEvents");
        internal static long? Last(EnemyState e, ReactionKind kind) => Read<long?>(e, kind == ReactionKind.Steam ? "LastSteamTick" : "LastStormTick");
        internal static void Reserve(EnemyState e, ReactionKind kind, long tick)
        {
            var p = e.GetType().GetProperty(kind == ReactionKind.Steam ? "LastSteamTick" : "LastStormTick");
            Assert.That(p, Is.Not.Null, "Missing cooldown state"); p.SetValue(e, tick);
        }
        internal static T Read<T>(object o, string name)
        { var p = o.GetType().GetProperty(name); Assert.That(p, Is.Not.Null, "Missing runtime API " + name); return (T)p.GetValue(o); }
        internal static List<DamageResolvedEvent> ReactionHits(BattleSimulation s)
        { var result = new List<DamageResolvedEvent>(); foreach (var d in Damages(s)) if (d.Origin == DamageOrigin.Reaction) result.Add(d); return result; }
    }
}
