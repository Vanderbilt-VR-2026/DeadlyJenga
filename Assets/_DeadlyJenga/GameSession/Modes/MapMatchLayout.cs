using System;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    [CreateAssetMenu(menuName="Deadly Jenga/Map Match Layout")]
    public sealed class MapMatchLayout : ScriptableObject
    {
        [Serializable] public sealed class TeamArea
        {
            public string name;
            public Vector3 flagHome;
            public Vector3[] spawns=Array.Empty<Vector3>();
        }
        [Tooltip("Exact ISyncedWorld layout token. Geometry changes must update both assets.")]
        public string layoutSignature;
        public bool playOpeningOnly;
        [Tooltip("Resolve authored room-floor starts onto nearby static ground; never onto loose blocks.")]
        public bool groundSpawns;
        [Tooltip("Exposed ground sinks survivors. Authored starts require a surviving structural block.")]
        public bool sinkingGround;
        public Vector3[] freeSpawns=Array.Empty<Vector3>();
        public TeamArea[] teams=Array.Empty<TeamArea>();
        public Vector3 hill;
        [Tooltip("Move each authored start onto the nearest top of a standing block (a gift) instead of the floor. For maps whose floor sinks.")]
        public bool topSpawns;
        [Tooltip("How far (m) from an authored start to look for a block top.")]
        public float topSearch=24;
        public bool TrySpawnPosition(IWorldQuery world, Vector3 local, out Vector3 point)
        {
            point=((ISyncedWorld)world).Root.TransformPoint(local);
            if(sinkingGround)
            {
                if(!Physics.Raycast(point+Vector3.up*.2f,Vector3.down,out var floor,.45f,
                    (1<<Layers.Surface)|(1<<Layers.Block)|1,QueryTriggerInteraction.Ignore) ||
                    floor.normal.y<.8f || !Climbables.TryGet(floor.collider,out var support) || !support.Body) return false;
                point=floor.point+Vector3.up*.08f;
                return true;
            }
            if(topSpawns)return TryBlockTop(world,ref point);
            if(!groundSpawns)return true;
            if(!Physics.Raycast(point+Vector3.up*.25f,Vector3.down,out var hit,2f,
                (1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore)||hit.normal.y<.9f)return false;
            point=hit.point+Vector3.up*.08f;
            return true;
        }
        // Look outward from the start in rings for the closest flat top of a block body,
        // far enough from its edges that a player fits.
        bool TryBlockTop(IWorldQuery world,ref Vector3 point)
        {
            float top=world.PlayableBounds.max.y+5;
            for(float ring=0;ring<=topSearch;ring+=1.5f)
            {
                int steps=ring<=0?1:Mathf.Max(6,Mathf.RoundToInt(ring*2.5f));
                for(int i=0;i<steps;i++)
                {
                    float angle=i*Mathf.PI*2/steps;
                    var probe=new Vector3(point.x+Mathf.Cos(angle)*ring,top,point.z+Mathf.Sin(angle)*ring);
                    if(!BlockTop(probe,point.y,top,out var hit))continue;
                    bool roomy=true;
                    for(int k=0;k<4&&roomy;k++)
                    {
                        var side=probe+new Vector3(k==0?.45f:k==1?-.45f:0,0,k==2?.45f:k==3?-.45f:0);
                        roomy=BlockTop(side,point.y,top,out var near)&&near.collider==hit.collider;
                    }
                    if(!roomy)continue;
                    point=hit.point+Vector3.up*.08f;
                    return true;
                }
            }
            return false;
        }
        static bool BlockTop(Vector3 probe,float floor,float top,out RaycastHit hit)
        {
            return Physics.Raycast(probe,Vector3.down,out hit,top-floor+2,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore)&&
                hit.collider.gameObject.layer==Layers.Block&&hit.collider.attachedRigidbody&&hit.normal.y>=.9f&&hit.point.y>floor+.3f;
        }
        public bool Matches(IWorldQuery world)=>world is ISyncedWorld synced&&!string.IsNullOrEmpty(layoutSignature)&&synced.SaveLayout()==layoutSignature;
    }
}
