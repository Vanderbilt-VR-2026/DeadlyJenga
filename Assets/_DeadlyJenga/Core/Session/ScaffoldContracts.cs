using UnityEngine;

namespace DeadlyJenga.Core
{
    public interface IScaffold
    {
        Transform Visual { get; }
        Vector3Int Cells { get; }
        float CellSize { get; }
        bool PlayerSolid { get; }
        void SetPlayerSolid(bool solid);
    }
    public interface IBuildUpControls
    {
        bool Available { get; }
        int Stage { get; }
        int StageCount { get; }
        bool Delivering { get; }
        float ClimbSecondsRemaining { get; }
        bool CanAdvance { get; }
        void AdvanceStage();
    }
}
