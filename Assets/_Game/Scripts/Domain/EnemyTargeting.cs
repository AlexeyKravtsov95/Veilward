using System.Collections.Generic;

namespace Veilward.Domain
{
    public static class EnemyTargeting
    {
        public static EnemyState SelectFirst(
            PathPosition towerPosition,
            float range,
            IReadOnlyList<EnemyState> enemies,
            PathDefinition path)
        {
            EnemyState selected = null;
            var rangeSquared = (double)range * range;
            for (var index = 0; index < enemies.Count; index++)
            {
                var enemy = enemies[index];
                if (!enemy.IsAlive)
                {
                    continue;
                }

                var position = path.GetPosition(enemy.DistanceAlongPath);
                var deltaX = (double)position.X - towerPosition.X;
                var deltaY = (double)position.Y - towerPosition.Y;
                if (deltaX * deltaX + deltaY * deltaY > rangeSquared)
                {
                    continue;
                }

                if (selected == null ||
                    enemy.DistanceAlongPath > selected.DistanceAlongPath ||
                    (enemy.DistanceAlongPath == selected.DistanceAlongPath &&
                     enemy.Id.Value < selected.Id.Value))
                {
                    selected = enemy;
                }
            }

            return selected;
        }
    }
}
