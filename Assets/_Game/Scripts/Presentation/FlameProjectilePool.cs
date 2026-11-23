using UnityEngine;
using Veilward.Domain;

namespace Veilward.Presentation
{
    // Ограничивает только картинку: damage/rewards уже разрешены Domain, очереди нет.
    public sealed class FlameProjectilePool
    {
        public const int Capacity = 8;
        private readonly FlameStrikeView[] _views = new FlameStrikeView[Capacity];
        private readonly AttackEvent[] _events = new AttackEvent[Capacity];
        private readonly int[] _levels = new int[Capacity];
        private readonly long[] _order = new long[Capacity];
        private long _sequence;
        public FlameStrikeView GetView(int index) => _views[index];
        public AttackEvent GetEvent(int index) => _events[index];
        public int GetLevel(int index) => _levels[index];

        public FlameProjectilePool(Transform parent, Material flame, Material hot)
        {
            for (var i = 0; i < Capacity; i++) _views[i] = new FlameStrikeView(parent, flame, hot, i);
        }

        public void Play(AttackEvent attack, int level, Vector3 source)
        {
            var slot = 0;
            for (var i = 0; i < Capacity; i++)
            {
                if (_views[i].Remaining <= 0) { slot = i; break; }
                // Consumption order различает несколько shots в одном render frame.
                if (_order[i] < _order[slot]) slot = i;
            }
            _events[slot] = attack; _levels[slot] = level; _order[slot] = ++_sequence;
            _views[slot].Play(source, new Vector3(attack.TargetPosition.X, .6f, attack.TargetPosition.Y),
                attack.TargetEnemyId, attack.WasLethal);
        }

        public void Advance(float seconds)
        {
            for (var i = 0; i < Capacity; i++)
            {
                _views[i].Advance(seconds);
                if (_views[i].Remaining <= 0) ClearMetadata(i);
            }
        }

        private void ClearMetadata(int index)
        { _events[index] = default; _levels[index] = 0; _order[index] = 0; }

        public void Clear()
        {
            _sequence = 0;
            for (var i = 0; i < Capacity; i++) { _views[i].Clear(); ClearMetadata(i); }
        }
    }
}
