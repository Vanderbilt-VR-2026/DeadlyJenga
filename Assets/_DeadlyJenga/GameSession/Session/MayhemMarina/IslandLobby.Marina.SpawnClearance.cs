using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using P=DeadlyJenga.CastlePrototype.CastleLanProtocol;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobby
    {
        MeshCollider marinaSpawnFloor;
        bool marinaFloorSetupCompleted;
        int marinaFloorRevision;
        bool marinaArrivalPending,marinaArrivalPlaced,marinaWasConnected;
        bool marinaArrivalAnnounced,marinaArrivalCuePrepared,marinaArrivalBeforeDeparture;
        int marinaPlacedGeneration=int.MinValue,marinaPlacedFloorRevision=-1;
        float nextMarinaArrival,nextMarinaRecovery;
        static readonly Vector3[] MarinaRecoveryPoints =
        {
            new(-10,1.33f,-8),new(-11.5f,1.33f,-8),new(-8.5f,1.33f,-8),
            new(-10,1.33f,-6.5f),new(-10,1.33f,-9.5f),new(-7,1.33f,-7),
            new(-13,1.33f,-7),new(-13,1.33f,-9),new(-7,1.33f,-9),new(-10,1.33f,-5.2f)
        };
        readonly Collider[] marinaSpawnOverlaps=new Collider[64];
        internal bool UsesMarinaSpawns=>Radius>=30&&PlayerPrefs.GetInt(LegacyLobbyPreference,0)!=1;
        internal int MarinaFloorRevision=>marinaFloorRevision;
        internal void InvalidateMarinaSpawnClearance(){marinaFloorRevision++;nextMarinaArrival=0;}
        static bool MarinaFinite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
        static bool MarinaFinite(Quaternion q)
        {
            if(!float.IsFinite(q.x)||!float.IsFinite(q.y)||!float.IsFinite(q.z)||!float.IsFinite(q.w))return false;
            float length=Quaternion.Dot(q,q);return float.IsFinite(length)&&length>.99f&&length<1.01f;
        }
        void MarinaEnvelope(out float radius,out float height,out Vector3 center,out float bottom)
        {
            radius=.32f;height=2.2f;center=Vector3.zero;bottom=0;
            if(!NetworkActors.Alive(Player))return;
            var controller=Player.gameObject.GetComponent<CharacterController>();
            if(controller)
            {
                var scale=Player.transform.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
                radius=Mathf.Max(radius,controller.radius*Mathf.Max(scale.x,scale.z)+controller.skinWidth);
                float bodyHeight=Mathf.Max(controller.height*scale.y,radius*2);
                center=Vector3.Scale(controller.center,scale);bottom=center.y-bodyHeight*.5f;
                height=Mathf.Max(height,bodyHeight);
            }
            if(Player.ViewCamera)height=Mathf.Max(height,Player.ViewCamera.transform.position.y-Player.transform.position.y-bottom+.2f);
            center.y=bottom+height*.5f;
        }
        internal MarinaSpawnResult TryMarinaSpawnPose(int slot,out P.Pose pose,out string reason,GameObject ignoreActor=null,bool occupancy=true)
        {
            pose=default;reason="";
            if(slot<0||slot>=10){reason="Lobby spawn slot is outside capacity.";return MarinaSpawnResult.Rejected;}
            if(!UsesMarinaSpawns){pose=new P.Pose(SpawnPoint(slot),Quaternion.identity);return MarinaSpawnResult.Accepted;}
            if(!marinaFloorSetupCompleted){reason="Waiting for lobby floor setup to finish.";return MarinaSpawnResult.Wait;}
            Collider floor=marinaSpawnFloor;
            bool gray=!floor&&flatFloor&&flatFloor.activeInHierarchy;
            if(gray)floor=flatFloor.GetComponent<Collider>();
            if(!floor||!floor.enabled){reason="Waiting for the lobby collision floor.";return MarinaSpawnResult.Wait;}
            if(!NetworkActors.Alive(Player)){reason="Waiting for the local lobby body.";return MarinaSpawnResult.Wait;}
            MarinaEnvelope(out float radius,out float height,out var center,out float bottom);
            if(!float.IsFinite(radius)||!float.IsFinite(height)||!MarinaFinite(center)||radius<=0||height<radius*2)
            {reason="The current lobby body envelope is invalid.";return MarinaSpawnResult.Rejected;}
            if(radius*2+.1f>1.2f){reason="The current body is too wide for the ten lobby spawn slots.";return MarinaSpawnResult.Rejected;}
            if((radius+new Vector2(center.x,center.z).magnitude)*2+.1f>1.2f)
            {reason="Move your head and body toward your play-space center to finish lobby arrival.";return MarinaSpawnResult.Wait;}
            Physics.SyncTransforms();
            var forward=Vector3.ProjectOnPlane(transform.forward,Vector3.up);
            if(forward.sqrMagnitude<.01f){reason="Lobby orientation is invalid.";return MarinaSpawnResult.Rejected;}
            var rotation=Quaternion.LookRotation(forward,Vector3.up);
            var target=transform.TransformPoint(new Vector3(-2.4f+1.2f*(slot%5),1.86f,6.2f+1.4f*(slot/5)));
            float expected=gray?floor.bounds.max.y:target.y;
            var ray=new Ray(new Vector3(target.x,expected+.35f,target.z),Vector3.down);
            if(!floor.Raycast(ray,out var hit,1.2f)||Mathf.Abs(hit.point.y-expected)>.12f||hit.normal.y<.85f)
            {reason="Lobby slot "+slot+" has no safe platform floor.";return MarinaSpawnResult.Rejected;}
            pose=new P.Pose(hit.point+Vector3.up*(.06f-bottom),rotation);
            return CheckMarinaCapsule(pose.position,pose.rotation,radius,height,center,ignoreActor,occupancy,out reason);
        }
        MarinaSpawnResult CheckMarinaCapsule(Vector3 position,Quaternion rotation,float radius,float height,Vector3 center,GameObject ignoreActor,bool occupancy,out string reason)
        {
            reason="";var at=position+rotation*center;float stem=Mathf.Max(0,height*.5f-radius);
            int count=gameObject.scene.GetPhysicsScene().OverlapCapsule(at-Vector3.up*stem,at+Vector3.up*stem,radius,
                marinaSpawnOverlaps,~0,occupancy?QueryTriggerInteraction.Collide:QueryTriggerInteraction.Ignore);
            if(count==marinaSpawnOverlaps.Length){reason="Lobby spawn clearance query is full; retrying.";return MarinaSpawnResult.Wait;}
            for(int i=0;i<count;i++)
            {
                var other=marinaSpawnOverlaps[i];if(!other)continue;
                if(ignoreActor&&other.transform.IsChildOf(ignoreActor.transform))continue;
                var actor=other.GetComponentInParent<IModeActor>();
                if(other.isTrigger&&actor==null)continue;
                bool moving=actor!=null||other is CharacterController||other.GetComponentInParent<IMovementState>()!=null||
                    other.GetComponentInParent<IFlyingState>()!=null||other.attachedRigidbody;
                if(!occupancy&&moving)continue;
                reason=moving?"A player or moving object occupies this lobby slot; waiting.":"Lobby spawn is obstructed by "+other.name+".";
                return moving?MarinaSpawnResult.Wait:MarinaSpawnResult.Rejected;
            }
            return MarinaSpawnResult.Accepted;
        }
        internal void PrepareMarinaArrivalCue()
        {
            if(marinaArrivalCuePrepared)return;
            marinaArrivalBeforeDeparture=marinaArrivalAnnounced;
            marinaArrivalAnnounced=false;marinaArrivalCuePrepared=true;
        }
        internal void CancelMarinaArrivalCue()
        {
            if(!marinaArrivalCuePrepared)return;
            marinaArrivalAnnounced=marinaArrivalBeforeDeparture;marinaArrivalCuePrepared=false;
        }
        void AnnounceMarinaArrival()
        {
            if(marinaArrivalAnnounced||MatchResident||VisitOwner||!ReferenceEquals(Services.Session,this)||
                !NetworkActors.Alive(Player)||!Player.gameObject.activeInHierarchy)return;
            marinaArrivalAnnounced=true;marinaArrivalCuePrepared=false;
            GameEvents.RaiseEmoted(Player.gameObject,"PlayerArrival");
        }
        internal bool ApplyReservedMarinaSpawn(P.Pose pose)
        {
            if(!NetworkActors.Alive(Player)||MatchResident&&!VisitOwner||!MarinaFinite(pose.position)||!MarinaFinite(pose.rotation))return false;
            Player.Place(pose.position,pose.rotation);marinaArrivalPending=false;marinaArrivalPlaced=true;
            marinaWasConnected=Room&&Room.Connected;marinaPlacedGeneration=Room?Room.MarinaSpawnGeneration:int.MinValue;marinaPlacedFloorRevision=marinaFloorRevision;
            SetMenu(MenuOpen);AnnounceMarinaArrival();return true;
        }
        void RecoverMarinaPlayer()
        {
            if (Time.unscaledTime < nextMarinaRecovery) return;
            nextMarinaRecovery=Time.unscaledTime+.25f;
            if (TryMarinaRecoveryPose(out var pose))
                Player.Place(pose.position,pose.rotation);
        }

        bool TryMarinaRecoveryPose(out P.Pose pose)
        {
            pose=default;
            Collider floor=marinaSpawnFloor;
            if (!floor && flatFloor) floor=flatFloor.GetComponent<Collider>();
            if (!floor||!floor.enabled) return false;
            MarinaEnvelope(out float radius,out float height,out var center,out float bottom);
            if (!MarinaFinite(center)||!float.IsFinite(radius)||!float.IsFinite(height)) return false;
            var rotation=Quaternion.Euler(0,transform.eulerAngles.y,0);
            Physics.SyncTransforms();
            int count=MarinaRecoveryPoints.Length;
            int first=Room&&Room.Connected?(int)((uint)Room.Runner.LocalPlayer.RawEncoded % (uint)count):0;
            for (int i=0;i<count;i++)
            {
                var target=transform.TransformPoint(MarinaRecoveryPoints[(first+i)%count]);
                if (!floor.Raycast(new Ray(target+Vector3.up*.5f,Vector3.down),out var hit,1f) ||
                    hit.normal.y<.85f || hit.point.y<=WaterHeight(hit.point)) continue;
                var feet=hit.point+Vector3.up*(.06f-bottom);
                if (CheckMarinaCapsule(feet,rotation,radius,height,center,Player.gameObject,true,out _) != MarinaSpawnResult.Accepted)
                    continue;
                pose=new P.Pose(feet,rotation);
                return true;
            }
            return false;
        }

        bool PlaceLocalLobbyArrival(bool returning=false)
        {
            if(!UsesMarinaSpawns){Player.Place(SafeSpawnPoint(),Quaternion.identity);AnnounceMarinaArrival();return true;}
            bool connected=Room&&Room.Connected&&!VisitOwner;
            // Keep the same admission when the persistent lobby resumes on the same floor and room generation.
            bool alreadyAdmitted=marinaArrivalPlaced&&marinaWasConnected==connected&&
                marinaPlacedFloorRevision==marinaFloorRevision&&(!connected||marinaPlacedGeneration==Room.MarinaSpawnGeneration);
            P.Pose pose;string reason;MarinaSpawnResult result;
            if(connected)result=Room.TryGetLocalMarinaSpawn(out pose,out reason);
            else result=TryMarinaSpawnPose(2,out pose,out reason,Player.gameObject);
            if(result==MarinaSpawnResult.Accepted)return ApplyReservedMarinaSpawn(pose);
            if((!connected||returning&&alreadyAdmitted)&&TryMarinaRecoveryPose(out pose))
                return ApplyReservedMarinaSpawn(pose);
            marinaArrivalPending=true;SetupHint=reason;SetMenu(MenuOpen);nextMarinaArrival=Time.unscaledTime+.25f;
            return false;
        }
        void TickMarinaLocalArrival()
        {
            if(!UsesMarinaSpawns||VisitOwner||MatchResident||departing||!NetworkActors.Alive(Player)||Time.unscaledTime<nextMarinaArrival)return;
            bool connected=Room&&Room.Connected;int stamp=Room?Room.MarinaSpawnGeneration:int.MinValue;
            if(!marinaArrivalPlaced||marinaArrivalPending||connected!=marinaWasConnected||connected&&stamp!=marinaPlacedGeneration||marinaPlacedFloorRevision!=marinaFloorRevision)
                PlaceLocalLobbyArrival();
        }
        internal GameObject MarinaParticipantRoot(string id)
        {
            return peers.TryGetValue(id,out var view)&&view is Component component&&component?component.gameObject:null;
        }
        internal MarinaSpawnResult ValidateMarinaAcceptedPose(P.Pose pose,out string reason,bool occupancy=true)
        {
            reason="";
            if(!MarinaFinite(pose.position)||!MarinaFinite(pose.rotation))
            {reason="Host lobby pose is malformed.";return MarinaSpawnResult.Rejected;}
            int matched=-1;float nearest=float.PositiveInfinity;
            for(int i=0;i<10;i++)
            {
                var at=transform.TransformPoint(new Vector3(-2.4f+1.2f*(i%5),1.86f,6.2f+1.4f*(i/5)));
                float distance=new Vector2(at.x-pose.position.x,at.z-pose.position.z).sqrMagnitude;
                if(distance<nearest){nearest=distance;matched=i;}
            }
            if(matched<0||nearest>.15f*.15f)
            {reason="Host lobby pose is outside the accepted spawn grid.";return MarinaSpawnResult.Rejected;}
            var result=TryMarinaSpawnPose(matched,out var expected,out reason,Player.gameObject,false);
            if(result!=MarinaSpawnResult.Accepted)return result;
            if(Vector3.Distance(expected.position,pose.position)>=.15f||Quaternion.Angle(expected.rotation,pose.rotation)>=5)
            {reason="Host lobby pose disagrees with the current collision floor.";return MarinaSpawnResult.Rejected;}
            if(!occupancy)return MarinaSpawnResult.Accepted;
            MarinaEnvelope(out float radius,out float height,out var center,out _);
            return CheckMarinaCapsule(pose.position,pose.rotation,radius,height,center,Player.gameObject,true,out reason);
        }
    }
}
