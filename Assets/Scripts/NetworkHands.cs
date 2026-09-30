using Fusion;
using UnityEngine;

public class NetworkHands : NetworkBehaviour
{
    [SerializeField] NetworkTransform leftHand;
    [SerializeField] NetworkTransform rightHand;

    public override void Spawned()
    {
        // The owner already sees responsive controller models on their local XR rig.
        // forceRenderingOff is local presentation state; restore it for remote
        // avatars too so a reused object cannot retain its previous owner's hiding.
        foreach (var renderer in leftHand.GetComponentsInChildren<Renderer>(true))
            renderer.forceRenderingOff = Object.HasInputAuthority;
        foreach (var renderer in rightHand.GetComponentsInChildren<Renderer>(true))
            renderer.forceRenderingOff = Object.HasInputAuthority;
    }

    public override void FixedUpdateNetwork()
    {
        // Fusion supplies this player's input to the host and the owning client.
        // Proxies receive the resulting poses through the existing NetworkTransforms.
        if (!GetInput<HandPoseInput>(out var pose)) return;

        transform.SetPositionAndRotation(pose.RootPosition, pose.RootRotation);
        leftHand.transform.SetPositionAndRotation(pose.LeftPosition, pose.LeftRotation);
        rightHand.transform.SetPositionAndRotation(pose.RightPosition, pose.RightRotation);
    }
}
