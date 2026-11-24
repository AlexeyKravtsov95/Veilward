using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public enum BasicShotKind { None, Tide, Gale, Stone }

    // Общий малый бюджет новых basic shots; FLAME сохраняет свой принятый pool.
    public sealed class BasicProjectilePool
    {
        public const int Capacity = 12;
        private readonly BasicStrikeView[] _views = new BasicStrikeView[Capacity];
        private readonly AttackEvent[] _events = new AttackEvent[Capacity];
        private readonly int[] _levels = new int[Capacity];
        private readonly long[] _order = new long[Capacity];
        private long _sequence;
        public BasicStrikeView GetView(int index) => _views[index];
        public AttackEvent GetEvent(int index) => _events[index];
        public int GetLevel(int index) => _levels[index];

        public BasicProjectilePool(Transform parent, Material material)
        { for (var i = 0; i < Capacity; i++) _views[i] = new BasicStrikeView(parent, material, i); }

        public static BasicShotKind KindFor(TowerDefinitionId id)
        {
            switch (id.Value)
            { case "tide": return BasicShotKind.Tide; case "gale": return BasicShotKind.Gale;
                case "stone": return BasicShotKind.Stone; default: return BasicShotKind.None; }
        }

        public void Play(BasicShotKind kind, AttackEvent attack, int level, Vector3 source)
        {
            if (kind == BasicShotKind.None) return;
            var slot = 0;
            for (var i = 0; i < Capacity; i++)
            {
                if (_views[i].Remaining <= 0) { slot = i; break; }
                if (_order[i] < _order[slot]) slot = i;
            }
            _events[slot] = attack; _levels[slot] = level; _order[slot] = ++_sequence;
            _views[slot].Play(kind, source, new Vector3(attack.TargetPosition.X, .6f, attack.TargetPosition.Y));
        }

        public void Advance(float seconds)
        {
            for (var i = 0; i < Capacity; i++)
            {
                _views[i].Advance(seconds);
                if (_views[i].Remaining <= 0) ClearMetadata(i);
            }
        }
        private void ClearMetadata(int i) { _events[i] = default; _levels[i] = 0; _order[i] = 0; }
        public void Clear()
        {
            _sequence = 0;
            for (var i = 0; i < Capacity; i++) { _views[i].Clear(); ClearMetadata(i); }
        }
    }
}
