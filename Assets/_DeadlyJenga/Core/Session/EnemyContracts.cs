using UnityEngine;

namespace DeadlyJenga.Core
{
    [System.Serializable]
    public sealed class EnemySpawnSettings
    {
        public string archetypeId="panther";
        public float hitDamage=20;
    }
    public interface IEnemyActor
    {
        bool IsEnemy { get; }
        string ArchetypeId { get; }
        float HitDamage { get; }
        void Despawn();
    }
    public interface IEnemyPopulation
    {
        int EnemyCount { get; }
        GameObject SpawnEnemy(EnemySpawnSettings settings);
        void ClearEnemies();
    }
}
