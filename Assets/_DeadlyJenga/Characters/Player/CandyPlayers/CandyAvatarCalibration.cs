using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    // Confirmed visual fit. Old device calibration preferences are intentionally ignored.
    // Offsets are in avatar metres; rotations are in degrees, relative to each grip pose.
    public sealed class CandyAvatarCalibration
    {
        public readonly float headSize = .9f;
        public readonly Vector3 headOffset = Vector3.zero;
        public readonly float bodyHeight = 1f, shoulderWidth = 1.15f;
        public readonly Vector3 bodyOffset = new Vector3(0, .05f, -.11f);
        public readonly Vector3 leftPosition = new Vector3(0, .02f, -.03f);
        public readonly Vector3 rightPosition = new Vector3(0, .02f, -.03f);
        // Same symmetry as the calibration panel's Both hands control.
        public readonly Vector3 leftRotation = new Vector3(60, 14, 110);
        public readonly Vector3 rightRotation = new Vector3(60, -14, -110);
        public static CandyAvatarCalibration Current { get; } = new CandyAvatarCalibration();
    }
}
