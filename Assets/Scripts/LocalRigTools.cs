using Unity.XR.CoreUtils;
using UnityEngine;

// local only. Helpers for moving and scaling your own XR rig.
public static class LocalRigTools
{
    const float DropHeight = 0.05f;

    public static XROrigin FindRig() => Object.FindFirstObjectByType<XROrigin>();

    public static bool TeleportTo(Vector3 position, Quaternion rotation)
    {
        var hands = LocalHandInput.FindOrCreate();
        if (hands == null)
        {
            Debug.LogWarning("no local XR rig found.");
            return false;
        }
        hands.MoveToSpawn(position, rotation);
        return true;
    }

    // raycasts down to find top of tower, so works for any tower size
    public static bool RespawnOnTower(TowerGenerator tower, float sideOffset = 0f)
    {
        if (tower == null) return false;

        var t = tower.transform;
        Vector3 origin = t.position + t.right * sideOffset + Vector3.up * 1000f;
        if (!Physics.Raycast(origin, Vector3.down, out var hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning("nothing under the tower center to stand on.");
            return false;
        }
        return TeleportTo(hit.point + Vector3.up * DropHeight, t.rotation);
    }

    public static bool RespawnAtSpawnPoint()
    {
        var spawn = GameObject.Find("Spawn Points");
        if (spawn == null) return false;
        return TeleportTo(spawn.transform.position, spawn.transform.rotation);
    }

    public static float Scale
    {
        get
        {
            var rig = FindRig();
            return rig != null ? rig.transform.localScale.x : 1f;
        }
        set
        {
            var rig = FindRig();
            if (rig == null) return;
            rig.transform.localScale = Vector3.one * Mathf.Max(0.05f, value);
        }
    }
}