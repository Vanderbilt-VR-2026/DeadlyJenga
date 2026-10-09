using Fusion;
using UnityEngine;
using Unity.XR.CoreUtils;

public struct HandPoseInput : INetworkInput
{
    public Vector3 RootPosition;
    public Quaternion RootRotation;
    public Vector3 LeftPosition;
    public Quaternion LeftRotation;
    public Vector3 RightPosition;
    public Quaternion RightRotation;
}

// Lives only on the local XR rig, never on the spawned avatar.
public class LocalHandInput : MonoBehaviour
{
    [SerializeField] Transform leftVisual;
    [SerializeField] Transform rightVisual;

    public static LocalHandInput FindOrCreate()
    {
        var input = FindFirstObjectByType<LocalHandInput>();
        if (input != null) return input;

        // Only the hardware rig has an XROrigin; network avatars must not have one.
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin == null) return null;
        return origin.gameObject.AddComponent<LocalHandInput>();
    }

    void Awake()
    {
        if (leftVisual == null)
            leftVisual = transform.Find("Camera Offset/Left Controller/Left Controller Visual");
        if (rightVisual == null)
            rightVisual = transform.Find("Camera Offset/Right Controller/Right Controller Visual");

        if (!leftVisual || !rightVisual)
            Debug.LogError("LocalHandInput cannot find the local controller visuals. Assign Left Visual and Right Visual on the local XR rig.", this);
    }

    public bool TryRead(out HandPoseInput pose)
    {
        pose = default;
        if (!leftVisual || !rightVisual) return false;

        // Use the visual poses to preserve the controller model's tracking offsets.
        pose = new HandPoseInput
        {
            RootPosition = transform.position,
            RootRotation = transform.rotation,
            LeftPosition = leftVisual.position,
            LeftRotation = leftVisual.rotation,
            RightPosition = rightVisual.position,
            RightRotation = rightVisual.rotation
        };
        return true;
    }

    public void MoveToSpawn(Vector3 position, Quaternion rotation)
    {
        var origin = GetComponentInParent<XROrigin>();
        var root = origin != null ? origin.transform : transform;
        var controller = root.GetComponent<CharacterController>();
        bool restoreController = controller != null && controller.enabled;
        if (restoreController) controller.enabled = false;

        root.SetPositionAndRotation(position, rotation);
        if (origin != null && origin.Camera != null)
        {
            origin.MatchOriginUpCameraForward(Vector3.up, rotation * Vector3.forward);
            // Preserve tracked height while placing the headset over the spawn point.
            var offset = position - origin.Camera.transform.position;
            offset.y = 0f;
            root.position += offset;
        }

        if (restoreController) controller.enabled = true;
    }
}
