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
        var rig = FindRig();
        Vector3 origin = t.position + t.right * sideOffset + Vector3.up * 1000f;

        var hits = Physics.RaycastAll(origin, Vector3.down, 2000f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (rig != null && hit.collider.transform.IsChildOf(rig.transform)) continue;
            return TeleportTo(hit.point + Vector3.up * DropHeight, t.rotation);
        }

        Debug.LogWarning("LocalRigTools: nothing under the tower center to stand on.");
        return false;
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