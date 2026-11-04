using System;

namespace Veilward.Domain
{
    public sealed class BattleSimulation : IBattleSimulation
    {
        private readonly PathDefinition _path;
        private readonly WaveDefinition _wave;
        private int _nextEnemyId = 1;
        private int _nextSpawnIndex;

        public BattleSimulation(PathDefinition path, WaveDefinition wave, int baseHealth)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _wave = wave ?? throw new ArgumentNullException(nameof(wave));
            State = BattleState.CreateInitial(baseHealth, startingGold: 0);
        }

        public BattleState State { get; }

        public void StartWave()
        {
            if (State.Phase != BattlePhase.Preparation)
            {
                return;
            }

            State.SetPhase(BattlePhase.Combat);
            SpawnDueEnemies();
        }

        public void Step()
        {
            if (State.Phase != BattlePhase.Combat)
            {
                return;
            }

            State.AdvanceTick();
            SpawnDueEnemies();

            var index = 0;
            while (index < State.Enemies.Count)
            {
                var enemy = State.Enemies[index];
                var distance = Math.Min(
                    _path.TotalLength,
                    enemy.DistanceAlongPath + enemy.Definition.Speed / SimulationTiming.TicksPerSecond);
                if (distance >= _path.TotalLength)
                {
                    State.RemoveEnemyAt(index);
                    State.ApplyLeak(enemy.Definition.LeakDamage);
                    continue;
                }

                enemy.Advance(distance);
                index++;
            }

            if (State.BaseHealth == 0)
            {
                State.SetPhase(BattlePhase.Defeat);
            }
            else if (_nextSpawnIndex == _wave.Count && State.Enemies.Count == 0)
            {
                State.SetPhase(BattlePhase.Victory);
            }
        }

        private void SpawnDueEnemies()
        {
            while (_nextSpawnIndex < _wave.Count && NextSpawnTick() <= State.Tick)
            {
                State.AddEnemy(EnemyState.CreateInitial(new EnemyId(_nextEnemyId), _wave.Enemy));
                _nextEnemyId++;
                _nextSpawnIndex++;
            }
        }

        private long NextSpawnTick()
        {
            return (long)_wave.FirstSpawnTick + (long)_nextSpawnIndex * _wave.SpawnIntervalTicks;
        }
    }
}
