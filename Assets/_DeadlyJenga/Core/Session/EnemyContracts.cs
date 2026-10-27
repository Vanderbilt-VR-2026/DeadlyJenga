using UnityEngine;

namespace DeadlyJenga.Core
{
    [System.Serializable]
    public sealed class EnemySpawnSettings
    {
        public string archetypeId="panther";
        public float hitDamage=20;
        public bool preparingMelee;
        public float healthRatio=1;
    }
    public interface IEnemyActor
    {
        bool IsEnemy { get; }
        string ArchetypeId { get; }
        float HitDamage { get; }
        void Despawn();
    }
    public interface IEnemyPose
    {
        bool PreparingMelee { get; }
    }
    public interface IEnemyMovement
    {
        bool CanClimb { get; }
        float JumpMultiplier { get; }
    }
    public interface IEnemyPopulation
    {
        int EnemyCount { get; }
        GameObject SpawnEnemy(EnemySpawnSettings settings);
        void ClearEnemies();
    }
}
