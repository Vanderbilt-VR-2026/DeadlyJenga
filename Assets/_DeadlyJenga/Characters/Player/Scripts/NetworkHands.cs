using Fusion;
using UnityEngine;
using DeadlyJenga.CandyPlayers;

public class NetworkHands : NetworkBehaviour
{
    [SerializeField] NetworkTransform leftHand;
    [SerializeField] NetworkTransform rightHand;
    [Networked] public Vector3 CandyHeadPosition{get;set;}
    [Networked] public Quaternion CandyHeadRotation{get;set;}
    [Networked] public CandyHandSkeleton CandyLeftSkeleton{get;set;}
    [Networked] public CandyHandSkeleton CandyRightSkeleton{get;set;}
    [Networked] public uint CandyHandFlags{get;set;}
    [Networked] public float CandyScale{get;set;}
    [Networked] public int CandyColor{get;set;}
    [Networked] public uint CandyAccessories{get;set;}
    [Networked] public uint CandyStyle{get;set;}
    CandyPlayerRig candyRig;

    public override void Spawned()
    {
        var prefab=Resources.Load<CandyPlayerRig>("CandyPlayerVisual");
        if(prefab)
        {
            if(Object.HasStateAuthority){CandyColor=Random.Range(0,prefab.style.colors.Length);CandyScale=1;CandyHeadPosition=transform.position+Vector3.up*1.6f;CandyHeadRotation=transform.rotation;}
            candyRig=Instantiate(prefab,transform);candyRig.name="Network candy body";candyRig.ownerCamera=null;
            candyRig.gameObject.SetActive(!Object.HasInputAuthority);
        }
        // The owner already sees responsive controller models on their local XR rig.
        // forceRenderingOff is local presentation state; restore it for remote
        // avatars too so a reused object cannot retain its previous owner's hiding.
        foreach (var renderer in leftHand.GetComponentsInChildren<Renderer>(true))
            renderer.forceRenderingOff = candyRig||Object.HasInputAuthority;
        foreach (var renderer in rightHand.GetComponentsInChildren<Renderer>(true))
            renderer.forceRenderingOff = candyRig||Object.HasInputAuthority;
    }

    public override void FixedUpdateNetwork()
    {
        // Fusion supplies this player's input to the host and the owning client.
        // Proxies receive the resulting poses through the existing NetworkTransforms.
        if (!GetInput<HandPoseInput>(out var pose)) return;

        transform.SetPositionAndRotation(pose.RootPosition, pose.RootRotation);
        leftHand.transform.SetPositionAndRotation(pose.LeftPosition, pose.LeftRotation);
        rightHand.transform.SetPositionAndRotation(pose.RightPosition, pose.RightRotation);
        CandyHeadPosition=pose.HeadPosition;CandyHeadRotation=pose.HeadRotation;
        CandyScale=Mathf.Max(.1f,pose.AvatarScale);CandyHandFlags=pose.HandFlags;
        CandyStyle=CandyPlayerAvatar.StyleBits(CandyPlayerAvatar.WithStyle(default,pose.AvatarStyle));
        CandyAccessories=pose.AvatarAccessories&(uint)CandyPlayerAvatar.SupportedAccessories;
        CandyLeftSkeleton=pose.LeftSkeleton;CandyRightSkeleton=pose.RightSkeleton;
    }
    public override void Render()
    {
        if(!candyRig)return;
        if(Object.HasInputAuthority)
        {var local=FindFirstObjectByType<LocalHandInput>();if(local&&local.TryGetComponent<CandyPlayerAvatar>(out var avatar))avatar.SetColor(CandyColor);return;}
        candyRig.SetColor(CandyColor);
        candyRig.SetAppearance(CandyPlayerAvatar.WithStyle(new DeadlyJenga.Core.AvatarAppearance{accessories=(DeadlyJenga.Core.AvatarAccessory)CandyAccessories},CandyStyle));
        candyRig.ApplyFrame(new CandyAvatarFrame{feet=transform.position,head=CandyHeadPosition,headRotation=CandyHeadRotation,scale=Mathf.Max(.1f,CandyScale),
            left=new CandyHandPose{position=leftHand.transform.position,rotation=leftHand.transform.rotation,skeleton=CandyLeftSkeleton,skeletonTracked=(CandyHandFlags&1)!=0,tracked=true},
            right=new CandyHandPose{position=rightHand.transform.position,rotation=rightHand.transform.rotation,skeleton=CandyRightSkeleton,skeletonTracked=(CandyHandFlags&2)!=0,tracked=true}},true,Time.deltaTime);
    }
}
