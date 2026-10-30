using Fusion;
using UnityEngine;
using Unity.XR.CoreUtils;
using DeadlyJenga.CandyPlayers;

public struct HandPoseInput : INetworkInput
{
    public Vector3 RootPosition;
    public Quaternion RootRotation;
    public Vector3 LeftPosition;
    public Quaternion LeftRotation;
    public Vector3 RightPosition;
    public Quaternion RightRotation;
    public Vector3 HeadPosition;
    public Quaternion HeadRotation;
    public float AvatarScale;
    public uint HandFlags;
    public uint AvatarAccessories;
    public uint AvatarStyle;
    public CandyHandSkeleton LeftSkeleton,RightSkeleton;
}

// Lives only on the local XR rig, never on the spawned avatar.
public class LocalHandInput : MonoBehaviour, DeadlyJenga.Core.ILocalPlayerInput
{
    [SerializeField] Transform leftVisual;
    [SerializeField] Transform rightVisual;
    CandyPlayerAvatar candyAvatar;

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
        if(Resources.Load<CandyPlayerRig>("CandyPlayerVisual"))
        {
            candyAvatar=GetComponent<CandyPlayerAvatar>();if(!candyAvatar)candyAvatar=gameObject.AddComponent<CandyPlayerAvatar>();
        }
        if (leftVisual == null)
            leftVisual = transform.Find("Camera Offset/Left Controller/Left Controller Visual");
        if (rightVisual == null)
            rightVisual = transform.Find("Camera Offset/Right Controller/Right Controller Visual");

        if ((!leftVisual || !rightVisual)&&!candyAvatar)
            Debug.LogError("LocalHandInput cannot find the local controller visuals. Assign Left Visual and Right Visual on the local XR rig.", this);
        if(candyAvatar)
        {
            if(candyAvatar.tracking)
            {
                candyAvatar.tracking.leftController = leftVisual ? leftVisual.parent : null;
                candyAvatar.tracking.rightController = rightVisual ? rightVisual.parent : null;
            }
            foreach(var visual in new[]{leftVisual,rightVisual})if(visual)foreach(var renderer in visual.GetComponentsInChildren<Renderer>())renderer.forceRenderingOff=true;
        }
    }

    // ILocalPlayerInput: the lobby's Photon input, filled in here so networking never names HandPoseInput.
    public bool TryWriteInput(object networkInput)
    {
        if (!(networkInput is NetworkInput input) || !TryRead(out var pose)) return false;
        input.Set(pose);
        return true;
    }
    public bool TryRead(out HandPoseInput pose)
    {
        pose = default;
        if(candyAvatar&&candyAvatar.Rig)
        {
            candyAvatar.RefreshNow();var frame=candyAvatar.LastFrame;
            pose=new HandPoseInput{RootPosition=frame.feet,RootRotation=Quaternion.Euler(0,frame.headRotation.eulerAngles.y,0),HeadPosition=frame.head,HeadRotation=frame.headRotation,AvatarScale=frame.scale,
                LeftPosition=frame.left.position,LeftRotation=frame.left.rotation,RightPosition=frame.right.position,RightRotation=frame.right.rotation,LeftSkeleton=frame.left.skeleton,RightSkeleton=frame.right.skeleton,
                AvatarStyle=CandyPlayerAvatar.StyleBits(candyAvatar.Appearance),AvatarAccessories=(uint)candyAvatar.Appearance.accessories,HandFlags=(frame.left.skeletonTracked?1u:0)|(frame.right.skeletonTracked?2u:0)|(frame.left.tracked?4u:0)|(frame.right.tracked?8u:0)};
            return true;
        }
        if (!leftVisual || !rightVisual) return false;

        // Use the visual poses to preserve the controller model's tracking offsets.
        pose = new HandPoseInput
        {
            RootPosition = transform.position,
            RootRotation = transform.rotation,
            LeftPosition = leftVisual.position,
            LeftRotation = leftVisual.rotation,
            RightPosition = rightVisual.position,
            RightRotation = rightVisual.rotation,
            HeadPosition=GetComponent<XROrigin>()?.Camera?GetComponent<XROrigin>().Camera.transform.position:transform.position+Vector3.up*1.6f,
            HeadRotation=GetComponent<XROrigin>()?.Camera?GetComponent<XROrigin>().Camera.transform.rotation:transform.rotation,
            AvatarScale=1
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
