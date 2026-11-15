using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    internal sealed class ReactionResolver
    {
        // Primary + максимум три AoE victim; Storm использует только первые три слота.
        private readonly EnemyState[] _targets = new EnemyState[4];
        private readonly PathPosition[] _positions = new PathPosition[4];

        internal bool TryResolve(
            BattleState state,
            PathDefinition path,
            TowerState source,
            EnemyState primary,
            PathPosition primaryPosition,
            ReactionCatalog catalog,
            List<ReactionEvent> reactions,
            List<DamageResolvedEvent> damages,
            ref int sequenceInTick)
        {
            if (!primary.IsAlive)
            {
                return false;
            }

            var intrinsic = source.Definition.IntrinsicReaction;
            if (intrinsic != null)
            {
                return TryCandidate(intrinsic.Kind, intrinsic, state, path, source, primary,
                    primaryPosition, catalog, reactions, damages, ref sequenceInTick);
            }

            var incoming = source.Definition.StatusEffect;
            if (incoming == null)
            {
                return false;
            }

            if ((incoming.Kind == StatusKind.Burn || incoming.Kind == StatusKind.Wet) &&
                primary.Burn != null && primary.Wet != null &&
                TryCandidate(ReactionKind.Steam, null, state, path, source, primary,
                    primaryPosition, catalog, reactions, damages, ref sequenceInTick))
            {
                return true;
            }

            return (incoming.Kind == StatusKind.Wet || incoming.Kind == StatusKind.Gust) &&
                   primary.Wet != null && primary.Gust != null &&
                   TryCandidate(ReactionKind.Storm, null, state, path, source, primary,
                       primaryPosition, catalog, reactions, damages, ref sequenceInTick);
        }

        private bool TryCandidate(
            ReactionKind kind,
            ReactionPayload intrinsic,
            BattleState state,
            PathDefinition path,
            TowerState source,
            EnemyState primary,
            PathPosition primaryPosition,
            ReactionCatalog catalog,
            List<ReactionEvent> reactions,
            List<DamageResolvedEvent> damages,
            ref int sequenceInTick)
        {
            if (!catalog.TryGet(kind, out var definition) ||
                !IsReady(primary, kind, state.Tick, definition.CooldownTicks))
            {
                return false;
            }

            var payload = intrinsic ?? definition.Payload;
            _targets[0] = primary;
            _positions[0] = primaryPosition;
            var count = 1;
            var limit = kind == ReactionKind.Steam ? payload.TargetLimit : payload.TargetLimit + 1;
            while (count < limit)
            {
                var origin = kind == ReactionKind.Steam ? primaryPosition : _positions[count - 1];
                var nearest = FindNearest(state, path, kind, definition.CooldownTicks, origin, payload.Range, count);
                if (nearest == null)
                {
                    break;
                }

                _targets[count] = nearest;
                _positions[count] = path.GetPosition(nearest.DistanceAlongPath);
                count++;
            }

            if (kind == ReactionKind.Storm && count == 1)
            {
                _targets[0] = null;
                return false;
            }

            // Сначала резервируем всех участников, затем наносим урон по неизменному плану.
            for (var index = 0; index < count; index++)
            {
                _targets[index].ReserveReaction(kind, state.Tick);
            }

            var reactionSequence = sequenceInTick++;
            reactions.Add(new ReactionEvent(
                state.Tick, reactionSequence, kind,
                intrinsic == null ? ReactionTriggerKind.StatusPair : ReactionTriggerKind.Intrinsic,
                source.Id, primary.Id, primaryPosition,
                kind == ReactionKind.Steam ? StatusKind.Burn : StatusKind.Wet,
                kind == ReactionKind.Steam ? StatusKind.Wet : StatusKind.Gust,
                payload.Range, payload.TargetLimit));

            var raw = (int)Math.Max(1L, Math.Min(int.MaxValue,
                (long)source.Definition.Damage * payload.DamagePercent / 100));
            for (var index = kind == ReactionKind.Steam ? 0 : 1; index < count; index++)
            {
                var target = _targets[index];
                var crack = target.Crack == null ? 0 : target.Crack.Power;
                var armor = Math.Max(0, target.Definition.Armor - crack);
                var resolved = Math.Max(1, raw - armor);
                var healthBefore = target.Health;
                var wasLethal = target.ApplyDamage(resolved);
                damages.Add(new DamageResolvedEvent(
                    state.Tick, sequenceInTick++, DamageOrigin.Reaction, source.Id, target.Id,
                    kind == ReactionKind.Steam ? primaryPosition : _positions[index - 1],
                    _positions[index], source.Definition.Damage, raw, resolved,
                    healthBefore - target.Health, target.Health, wasLethal, kind, reactionSequence, index));
            }

            // Буфер не удерживает удалённых врагов между запросами или после Restore.
            Array.Clear(_targets, 0, count);
            return true;
        }

        private EnemyState FindNearest(
            BattleState state, PathDefinition path, ReactionKind kind, int cooldown,
            PathPosition origin, float range, int selectedCount)
        {
            EnemyState nearest = null;
            var nearestDistance = (double)range * range;
            for (var index = 0; index < state.Enemies.Count; index++)
            {
                var enemy = state.Enemies[index];
                if (!enemy.IsAlive || !IsReady(enemy, kind, state.Tick, cooldown) || IsSelected(enemy, selectedCount))
                {
                    continue;
                }

                var position = path.GetPosition(enemy.DistanceAlongPath);
                var dx = (double)position.X - origin.X;
                var dy = (double)position.Y - origin.Y;
                var distance = dx * dx + dy * dy;
                if (distance < nearestDistance ||
                    (distance == nearestDistance && (nearest == null || enemy.Id.Value < nearest.Id.Value)))
                {
                    nearest = enemy;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private bool IsSelected(EnemyState enemy, int count)
        {
            for (var index = 0; index < count; index++)
            {
                if (_targets[index].Id == enemy.Id)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsReady(EnemyState enemy, ReactionKind kind, long tick, int cooldown)
        {
            var last = kind == ReactionKind.Steam ? enemy.LastSteamTick : enemy.LastStormTick;
            return !last.HasValue || (tick >= last.Value && tick - last.Value >= cooldown);
        }
    }
}
