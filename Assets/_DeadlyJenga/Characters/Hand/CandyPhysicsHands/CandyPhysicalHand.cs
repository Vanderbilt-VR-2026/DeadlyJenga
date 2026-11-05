using UnityEngine;
using UnityEngine.SceneManagement;
using DeadlyJenga.CandyPlayers;
namespace DeadlyJenga.CandyPhysicsHands
{
    // No grab detection, object joints, object parenting or object pose writes. Contacts do the work.
    public sealed class CandyPhysicalHand : MonoBehaviour
    {
        public CandyHandSettings settings;
        public bool left;
        public SkinnedMeshRenderer skin;
        public Transform[] visualBones;
        public float[] visualReferenceLengths;
        public Rigidbody[] bodies;
        public float Strength {get;private set;}
        public float MaximumAppliedForce {get;private set;}
        public float AppliedDriveEffortNewtons {get;private set;}
        public float AppliedLinearForceNewtons {get;private set;}
        public float AppliedTorqueNewtonMetres {get;private set;}
        public HandDriveProfile Profile {get;set;}
        public bool RequiresProfile {get;set;}
        readonly Vector3[] driveForces=new Vector3[16];
        readonly float[] driveTorques=new float[16];
        Vector3 palmTorque;
        readonly Pose[] authoritativePreview=new Pose[16];
        float authoritativePreviewAt=-100;
        bool hasAuthoritativePreview;
        public bool HasFreshAuthoritativePreview=>NetworkPreview&&Time.unscaledTime-authoritativePreviewAt<.5f;
        public void ReceiveAuthoritativePreview(Pose[] poses,float receivedAt)
        {
            if(!NetworkPreview||poses==null||poses.Length!=16)return;
            System.Array.Copy(poses,authoritativePreview,16);
            authoritativePreviewAt=receivedAt;hasAuthoritativePreview=true;
        }
        public void ClearAuthoritativePreview()=>authoritativePreviewAt=-100;
        public void RenderAuthoritativePreview(float dt)
        {
            if(!HasFreshAuthoritativePreview||bodies==null)return;
            float blend=1-Mathf.Exp(-20*Mathf.Clamp(dt,0,.1f));
            for(int i=0;i<16;i++)
            {
                bodies[i].position=Vector3.Lerp(bodies[i].position,authoritativePreview[i].position,blend);
                bodies[i].rotation=Quaternion.Slerp(bodies[i].rotation,authoritativePreview[i].rotation,blend);
            }
        }
        public CandyHandPose Target {get;private set;}
        Collider[] contacts;
        PhysicsMaterial friction;
        float builtSize;
        float[] segmentLengths;
        ConfigurableJoint[] joints;
        Quaternion[] referenceRelative;
        bool active;
        readonly Collider[] activationOverlaps=new Collider[64];
        public bool ActivationBlocked {get;private set;}
        public bool DesktopRecoveryOnly {get;set;}
        Vector3 queuedRecovery;
        public string ActivationReason {get;private set;}
        public bool IsInteractive=>active;
        public bool NetworkPreview {get;set;}
        bool previousNetworkPreview;
        public float GeometrySize=>builtSize;
        // AI fingers follow their joints instead of pulling independently through a contacted block.
        public bool JointDrivenFingers {get;set;}
        public bool ReplicatedBones {get;set;}
        Mesh originalSkin;
        static readonly System.Collections.Generic.Dictionary<Mesh,Mesh> leftSkins=new();
        void FitSkinHandedness()
        {
            if(!skin||!skin.sharedMesh)return;
            if(!originalSkin)originalSkin=CandySmoothHandMesh.SeparateDigitWeights(skin.sharedMesh,1,5);
            if(!left){skin.sharedMesh=originalSkin;return;}
            if(!leftSkins.TryGetValue(originalSkin,out var mesh)||!mesh)
            {
                mesh=Instantiate(originalSkin);mesh.name=originalSkin.name+" (left)";
                var vertices=mesh.vertices;var normals=mesh.normals;var tangents=mesh.tangents;
                for(int i=0;i<vertices.Length;i++)vertices[i].x=-vertices[i].x;
                for(int i=0;i<normals.Length;i++)normals[i].x=-normals[i].x;
                for(int i=0;i<tangents.Length;i++){tangents[i].x=-tangents[i].x;tangents[i].w=-tangents[i].w;}
                mesh.vertices=vertices;mesh.normals=normals;mesh.tangents=tangents;
                var mirror=Matrix4x4.Scale(new Vector3(-1,1,1));var binds=mesh.bindposes;
                for(int i=0;i<binds.Length;i++)binds[i]=mirror*binds[i]*mirror;
                mesh.bindposes=binds;
                for(int i=0;i<mesh.subMeshCount;i++)
                {
                    var indices=mesh.GetTriangles(i);
                    for(int j=0;j<indices.Length;j+=3)(indices[j+1],indices[j+2])=(indices[j+2],indices[j+1]);
                    mesh.SetTriangles(indices,i,false);
                }
                mesh.RecalculateBounds();leftSkins[originalSkin]=mesh;
            }
            skin.sharedMesh=mesh;
        }
        public void RebuildInactive(CandyHandPose pose)
        {
            if(active)throw new System.InvalidOperationException("Release contacts before resizing the hand.");
            if(bodies!=null)foreach(var b in bodies)if(b){if(Application.isPlaying)Destroy(b.gameObject);else DestroyImmediate(b.gameObject);}
            if(friction){if(Application.isPlaying)Destroy(friction);else DestroyImmediate(friction);}
            Build(pose);
        }
        public void SetTarget(CandyHandPose target,float strength){Target=target;Strength=Mathf.Clamp01(strength);}
        public void Build(CandyHandPose initial)
        {
            builtSize=settings.size;Target=initial;contacts=new Collider[16];bodies=new Rigidbody[16];segmentLengths=new float[15];joints=new ConfigurableJoint[15];referenceRelative=new Quaternion[15];
            friction=new PhysicsMaterial("Candy contact friction"){staticFriction=settings.staticFriction,dynamicFriction=settings.dynamicFriction,frictionCombine=PhysicsMaterialCombine.Average,bounciness=0};
            for(int i=0;i<16;i++)
            {
                var go=new GameObject(i==0?"Physical palm":"Physical finger "+(i-1)/3+" joint "+(i-1)%3);go.transform.SetParent(transform,false);go.layer=0;
                var rb=go.AddComponent<Rigidbody>();rb.mass=Mathf.Max(.001f,i==0?settings.palmMass:settings.fingerMass);rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.solverIterations=settings.solverIterations;rb.solverVelocityIterations=settings.solverVelocityIterations;rb.maxAngularVelocity=settings.maxAngularSpeed;rb.maxLinearVelocity=settings.maxSpeed;rb.maxDepenetrationVelocity=1;
                bodies[i]=rb;
                if(i==0){var col=go.AddComponent<BoxCollider>();col.center=new Vector3(0,0,.020f)*builtSize;col.size=new Vector3(.078f,.04f,.078f)*builtSize;contacts[i]=col;}
                else
                {
                    int f=(i-1)/3,j=(i-1)%3;Vector3 a=initial.skeleton.Joint(f*4+j),b=initial.skeleton.Joint(f*4+j+1);segmentLengths[i-1]=Mathf.Max(.005f,(b-a).magnitude);
                    float radius=((f==0?.011f:.0078f)-j*.001f)*builtSize,length=segmentLengths[i-1]*builtSize;
                    if(j==2)
                    {
                        // A broad fingertip contact patch resists rolling/ejection; the rendered skin stays rounded.
                        var pad=go.AddComponent<BoxCollider>();pad.center=Vector3.up*length*.5f;pad.size=new Vector3(radius*1.8f,length+radius*.5f,radius*1.5f);contacts[i]=pad;
                    }
                    else{var col=go.AddComponent<CapsuleCollider>();col.direction=1;col.radius=radius;col.height=length+2*radius;col.center=Vector3.up*length*.5f;contacts[i]=col;}
                }
                contacts[i].sharedMaterial=friction;contacts[i].contactOffset=Mathf.Clamp(.0005f*builtSize,.0001f,.02f);
            }
            ResetInactive(initial); // Safe only before activating contacts.
            for(int i=1;i<16;i++)
            {
                int j=(i-1)%3;var joint=bodies[i].gameObject.AddComponent<ConfigurableJoint>();joint.connectedBody=bodies[j==0?0:i-1];joint.autoConfigureConnectedAnchor=false;joint.anchor=Vector3.zero;
                joint.connectedAnchor=Quaternion.Inverse(joint.connectedBody.rotation)*(bodies[i].position-joint.connectedBody.position);
                joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
                joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Free;
                if(JointDrivenFingers)
                {
                    joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Limited;
                    joint.lowAngularXLimit=new SoftJointLimit{limit=-25};
                    joint.highAngularXLimit=new SoftJointLimit{limit=25};
                    float swing=i<=3?25:10;
                    joint.angularYLimit=new SoftJointLimit{limit=swing};joint.angularZLimit=new SoftJointLimit{limit=swing};
                }
                joint.enableCollision=false;joint.enablePreprocessing=true;joint.projectionMode=JointProjectionMode.None;
                joint.rotationDriveMode=RotationDriveMode.Slerp;joint.configuredInWorldSpace=false;joints[i-1]=joint;
                referenceRelative[i-1]=Quaternion.Inverse(joint.connectedBody.rotation)*bodies[i].rotation;
            }
            // Internal contacts produce jitter, never gripping force. Ignore only this hand's pairs.
            for(int i=0;i<16;i++)for(int j=i+1;j<16;j++)Physics.IgnoreCollision(contacts[i],contacts[j]);
            FitSkinHandedness();if(skin)skin.sharedMaterial=settings.skin;active=true;SetInteractive(false);
        }
        public static Quaternion SegmentRotation(Vector3 delta,Vector3 normal)
        {var forward=Vector3.ProjectOnPlane(normal,delta.normalized);if(forward.sqrMagnitude<.001f)forward=Vector3.ProjectOnPlane(Vector3.right,delta.normalized);return Quaternion.LookRotation(forward.normalized,delta.normalized);}
        public void ResetInactive(CandyHandPose pose)
        {
            if(active)throw new System.InvalidOperationException("Cannot teleport an interacting physical hand.");
            for(int i=0;i<bodies.Length;i++)
            {
                Desired(pose,i,out var position,out var rotation);
                bodies[i].transform.SetPositionAndRotation(position,rotation);bodies[i].position=position;bodies[i].rotation=rotation;if(!bodies[i].isKinematic){bodies[i].linearVelocity=Vector3.zero;bodies[i].angularVelocity=Vector3.zero;}
            }
        }
        void Desired(CandyHandPose pose,int i,out Vector3 position,out Quaternion rotation)
        {
            position=pose.position;rotation=pose.rotation;if(i==0)return;
            int f=(i-1)/3,j=(i-1)%3;Vector3 a=pose.skeleton.Joint(f*4+j),b=pose.skeleton.Joint(f*4+j+1);
            position+=pose.rotation*a*builtSize;rotation=SegmentRotation(pose.rotation*(b-a),pose.rotation*Vector3.up);
        }
        public void SetInteractive(bool value)
        {
            if(contacts==null)return;
            if(previousNetworkPreview!=NetworkPreview)
            {
                foreach(var c in contacts)c.enabled=false;
                foreach(var b in bodies){if(!b.isKinematic){b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;}b.isKinematic=true;}
                active=false;previousNetworkPreview=NetworkPreview;
            }
            if(NetworkPreview)
            {
                active=value;if(skin)skin.enabled=value;
                foreach(var c in contacts)c.enabled=false;
                foreach(var b in bodies)b.isKinematic=true;
                return;
            }
            if(active==value){if(skin)skin.enabled=active;return;}
            if(value&&!CanActivate()){ActivationBlocked=true;if(skin)skin.enabled=false;return;}ActivationBlocked=false;active=value;if(skin)skin.enabled=value;
            foreach(var c in contacts)c.enabled=value;
            foreach(var b in bodies){if(!value&&!b.isKinematic){b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;}b.isKinematic=!value;}
        }
        public void IgnoreContactsWith(CandyPhysicalHand other)
        {
            if(!other||contacts==null||other.contacts==null)return;
            foreach(var a in contacts)foreach(var b in other.contacts)if(a&&b)Physics.IgnoreCollision(a,b);
        }
        public bool CanActivate()
        {
            ActivationReason="";
            var scene=gameObject.scene.GetPhysicsScene();
            foreach(var c in contacts)
            {
                bool wasEnabled=c.enabled;c.enabled=true;
                try
                {
                    int count=scene.OverlapSphere(c.transform.position,.18f*builtSize,activationOverlaps,~0,QueryTriggerInteraction.Ignore);
                    if(count==activationOverlaps.Length){ActivationReason="Overlap query full";return false;}
                    for(int i=0;i<count;i++)
                    {
                        var other=activationOverlaps[i];if(other.transform.IsChildOf(transform)||Physics.GetIgnoreLayerCollision(c.gameObject.layer,other.gameObject.layer)||Physics.GetIgnoreCollision(c,other))continue;
                        if(Physics.ComputePenetration(c,c.transform.position,c.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)&&depth>.001f*builtSize){ActivationReason=c.name+" intersects "+other.name+" by "+depth.ToString("F5")+"m";return false;}
                    }
                }
                finally{c.enabled=wasEnabled;}
            }
            return true;
        }
        void FixedUpdate(){StepDrives(Time.fixedDeltaTime);}
        public void StepDrives(float dt)
        {
            MaximumAppliedForce=AppliedDriveEffortNewtons=AppliedLinearForceNewtons=AppliedTorqueNewtonMetres=0;
            if(ReplicatedBones||!settings||bodies==null)return;
            if(NetworkPreview)
            {
                if(hasAuthoritativePreview)return;
                PreviewWithoutAuthority(dt);
                return;
            }
            if(!active)
            {
                if(DesktopRecoveryOnly)
                {
                    var recovery=queuedRecovery;queuedRecovery=Vector3.zero;
                    if(Profile&&Profile.Confirmed&&Profile.ForceNewtons>0&&recovery.sqrMagnitude>0&&RecoveryTranslationClear(recovery))
                        foreach(var body in bodies)body.MovePosition(body.position+recovery);
                    return;
                }
                queuedRecovery=Vector3.zero;
                if(Strength>0)for(int i=0;i<bodies.Length;i++)
                {Desired(Target,i,out var p,out var q);bodies[i].MovePosition(Vector3.MoveTowards(bodies[i].position,p,settings.maxSpeed*dt));bodies[i].MoveRotation(Quaternion.RotateTowards(bodies[i].rotation,q,settings.maxAngularSpeed*Mathf.Rad2Deg*dt));}
                return;
            }
            float forceBudget=RequiresProfile?(Profile&&Profile.Confirmed?Profile.ForceNewtons:0):float.PositiveInfinity;
            float grip=RequiresProfile&&Profile?Profile.Friction:settings.staticFriction;
            float sliding=RequiresProfile?grip*.85f:settings.dynamicFriction;
            if(friction.staticFriction!=grip)friction.staticFriction=grip;
            if(friction.dynamicFriction!=sliding)friction.dynamicFriction=sliding;
            friction.frictionCombine=RequiresProfile?PhysicsMaterialCombine.Multiply:PhysicsMaterialCombine.Average;
            float requestedEffort=0;
            for(int i=0;i<bodies.Length;i++)
            {
                var rb=bodies[i];Desired(Target,i,out var p,out var q);
                rb.maxLinearVelocity=RequiresProfile?Mathf.Min(settings.maxSpeed,12):settings.maxSpeed;
                float denominator=1+dt*settings.positionDamping/rb.mass+dt*dt*settings.positionSpring/rb.mass;
                driveForces[i]=i==0||!JointDrivenFingers?Vector3.ClampMagnitude(
                    ((p-rb.position)*settings.positionSpring-rb.linearVelocity*settings.positionDamping)/denominator,
                    (i==0?settings.palmForce:settings.fingerForce)*Strength):Vector3.zero;
                if(i>0)
                {
                    int parent=(i-1)%3==0?0:i-1;
                    Desired(Target,parent,out _,out var parentRotation);
                    Quaternion relative=Quaternion.Inverse(parentRotation)*q;
                    joints[i-1].targetRotation=Quaternion.Inverse(relative)*referenceRelative[i-1];
                    driveTorques[i]=settings.fingerTorque*Strength;
                }
                else
                {
                    palmTorque=RequestedPalmTorque(rb,q,dt);
                    driveTorques[0]=palmTorque.magnitude;
                }
                requestedEffort+=driveForces[i].magnitude+driveTorques[i]/DriveLever(i);
            }
            float scale=requestedEffort>0?Mathf.Min(1,forceBudget*Strength/requestedEffort):0;
            for(int i=0;i<bodies.Length;i++)
            {
                Vector3 force=driveForces[i]*scale;
                float torque=driveTorques[i]*scale;
                bodies[i].AddForce(force,ForceMode.Force);
                MaximumAppliedForce=Mathf.Max(MaximumAppliedForce,force.magnitude);
                AppliedLinearForceNewtons+=force.magnitude;
                AppliedTorqueNewtonMetres+=torque;
                AppliedDriveEffortNewtons+=force.magnitude+torque/DriveLever(i);
                if(i==0)bodies[0].AddTorque(palmTorque*scale,ForceMode.Force);
                else joints[i-1].slerpDrive=new JointDrive
                {
                    positionSpring=torque>0?settings.rotationSpring*(JointDrivenFingers?6:1):0,
                    positionDamper=torque>0?settings.rotationDamping*(JointDrivenFingers?2.5f:1):0,
                    maximumForce=torque
                };
            }
        }
        readonly RaycastHit[] previewSweep=new RaycastHit[64];
        readonly Collider[] previewOverlap=new Collider[64];
        void PreviewWithoutAuthority(float dt)
        {
            var palm=(BoxCollider)contacts[0];
            var pose=Target;
            Vector3 position=bodies[0].position;
            Quaternion rotation=bodies[0].rotation;
            bool canMove=!RequiresProfile||Profile&&Profile.Confirmed&&Profile.ForceNewtons>0;
            if(!canMove)return;
            var next=Vector3.MoveTowards(position,Target.position,Mathf.Min(settings.maxSpeed,12)*dt);
            var turn=Quaternion.RotateTowards(rotation,Target.rotation,settings.maxAngularSpeed*Mathf.Rad2Deg*dt);
            Vector3 delta=next-position;
            float distance=delta.magnitude;
            Vector3 half=palm.size*.5f;
            float skin=Mathf.Max(.001f,palm.contactOffset);
            var scene=gameObject.scene.GetPhysicsScene();
            int mask=(1<<DeadlyJenga.Core.Layers.Block)|(1<<DeadlyJenga.Core.Layers.Surface)|1;
            if(distance>.0001f)
            {
                int hits=scene.BoxCast(position+rotation*palm.center,half,delta/distance,previewSweep,
                    rotation,distance+skin,mask,QueryTriggerInteraction.Ignore);
                float clear=hits==previewSweep.Length?0:distance;
                for(int i=0;i<hits;i++)
                    if(previewSweep[i].collider&&!previewSweep[i].collider.transform.IsChildOf(transform))
                        clear=Mathf.Min(clear,Mathf.Max(0,previewSweep[i].distance-skin));
                next=position+delta/distance*clear;
            }
            int overlaps=scene.OverlapBox(next+turn*palm.center,half,previewOverlap,turn,mask,QueryTriggerInteraction.Ignore);
            bool blocked=overlaps==previewOverlap.Length;
            for(int i=0;i<overlaps;i++)
                if(previewOverlap[i]&&!previewOverlap[i].transform.IsChildOf(transform))blocked=true;
            if(!blocked){position=next;rotation=turn;}
            pose.position=position;pose.rotation=rotation;
            for(int i=0;i<bodies.Length;i++)
            {
                Desired(pose,i,out var p,out var q);
                bodies[i].MovePosition(p);bodies[i].MoveRotation(q);
            }
        }

        public bool ClearForAutomaticSideSwitch()
        {
            if(!active)return true;
            if(contacts==null||bodies==null||NetworkPreview&&!HasFreshAuthoritativePreview)return false;
            var scene=gameObject.scene.GetPhysicsScene();
            for(int i=0;i<contacts.Length;i++)
            {
                var contact=contacts[i];var body=bodies[i];
                float gap=Mathf.Max(.02f,contact.contactOffset*2);
                int count;
                if(contact is BoxCollider box)
                    count=scene.OverlapBox(body.position+body.rotation*box.center,box.size*.5f+Vector3.one*gap,
                        recoveryOverlaps,body.rotation,~0,QueryTriggerInteraction.Ignore);
                else
                {
                    var capsule=(CapsuleCollider)contact;var axis=Vector3.zero;axis[capsule.direction]=1;
                    var center=body.position+body.rotation*capsule.center;
                    var offset=body.rotation*axis*Mathf.Max(0,capsule.height*.5f-capsule.radius);
                    count=scene.OverlapCapsule(center-offset,center+offset,capsule.radius+gap,
                        recoveryOverlaps,~0,QueryTriggerInteraction.Ignore);
                }
                if(count==recoveryOverlaps.Length)return false;
                for(int hit=0;hit<count;hit++)
                {
                    var other=recoveryOverlaps[hit];
                    if(!other||other.transform.IsChildOf(transform)||Physics.GetIgnoreLayerCollision(contact.gameObject.layer,other.gameObject.layer)||Physics.GetIgnoreCollision(contact,other))continue;
                    return false;
                }
            }
            return true;
        }

        public bool QueueInactiveRecovery(Vector3 delta)
        {
            if(active||NetworkPreview||!DesktopRecoveryOnly||queuedRecovery.sqrMagnitude>0||
                !Profile||!Profile.Confirmed||Profile.ForceNewtons<=0||bodies==null)return false;
            if(!RecoveryTranslationClear(delta))return false;
            queuedRecovery=delta;return true;
        }
        bool RecoveryTranslationClear(Vector3 delta)
        {
            float distance=delta.magnitude;
            if(!float.IsFinite(distance)||distance<.00001f||distance>.10001f)return false;
            var scene=gameObject.scene.GetPhysicsScene();
            int mask=(1<<DeadlyJenga.Core.Layers.Block)|(1<<DeadlyJenga.Core.Layers.Surface)|1;
            bool wasOverlapping=false,improved=false;
            float tolerance=Mathf.Max(.00001f,builtSize*.00001f);
            for(int bone=0;bone<contacts.Length;bone++)
            {
                var contact=contacts[bone];var body=bodies[bone];
                int hits;
                if(contact is BoxCollider box)
                    hits=scene.BoxCast(body.position+body.rotation*box.center,box.size*.5f,delta/distance,recoverySweep,
                        body.rotation,distance,mask,QueryTriggerInteraction.Ignore);
                else
                {
                    var capsule=(CapsuleCollider)contact;var axis=Vector3.zero;axis[capsule.direction]=1;
                    var center=body.position+body.rotation*capsule.center;
                    var offset=body.rotation*axis*Mathf.Max(0,capsule.height*.5f-capsule.radius);
                    hits=scene.CapsuleCast(center-offset,center+offset,capsule.radius,delta/distance,recoverySweep,
                        distance,mask,QueryTriggerInteraction.Ignore);
                }
                if(hits==recoverySweep.Length)return false;
                for(int i=0;i<hits;i++)
                {
                    var other=recoverySweep[i].collider;
                    if(!other||other.transform.IsChildOf(transform))continue;
                    if(!Physics.ComputePenetration(contact,body.position,body.rotation,other,other.transform.position,other.transform.rotation,out _,out _))return false;
                }
                int count=scene.OverlapSphere(body.position+delta,.18f*builtSize,recoveryOverlaps,mask,QueryTriggerInteraction.Ignore);
                if(count==recoveryOverlaps.Length)return false;
                for(int i=0;i<count;i++)
                {
                    var other=recoveryOverlaps[i];if(!other||other.transform.IsChildOf(transform))continue;
                    bool start=Physics.ComputePenetration(contact,body.position,body.rotation,other,other.transform.position,other.transform.rotation,out var escape,out float before);
                    if(start&&Vector3.Dot(delta,escape)<-tolerance)return false;
                    bool end=Physics.ComputePenetration(contact,body.position+delta,body.rotation,other,other.transform.position,other.transform.rotation,out _,out float after);
                    wasOverlapping|=start;
                    if(end&&(!start||after>before+tolerance))return false;
                    if(start&&(!end||after<before-tolerance))improved=true;
                }
            }
            return !wasOverlapping||improved;
        }
        readonly RaycastHit[] recoverySweep=new RaycastHit[64];
        readonly Collider[] recoveryOverlaps=new Collider[64];

        float DriveLever(int index)=>Mathf.Max(.01f,index==0?.078f*builtSize:segmentLengths[index-1]*builtSize);
        Vector3 RequestedPalmTorque(Rigidbody body,Quaternion target,float dt)
        {
            var error=target*Quaternion.Inverse(body.rotation);
            if(error.w<0)error=new Quaternion(-error.x,-error.y,-error.z,-error.w);
            error.ToAngleAxis(out float angle,out Vector3 axis);
            if(angle>180)angle-=360;
            if(axis.sqrMagnitude<=.001f)return Vector3.zero;
            Quaternion inertiaFrame=body.rotation*body.inertiaTensorRotation;
            Vector3 e=Quaternion.Inverse(inertiaFrame)*(axis*angle*Mathf.Deg2Rad);
            Vector3 w=Quaternion.Inverse(inertiaFrame)*body.angularVelocity;
            Vector3 inertia=body.inertiaTensor,torque=Vector3.zero;
            for(int n=0;n<3;n++)torque[n]=(e[n]*settings.rotationSpring-w[n]*settings.rotationDamping)/
                (1+dt*settings.rotationDamping/Mathf.Max(.00000001f,inertia[n])+dt*dt*settings.rotationSpring/Mathf.Max(.00000001f,inertia[n]));
            return Vector3.ClampMagnitude(inertiaFrame*torque,settings.palmTorque*Strength);
        }
        void LateUpdate()
        {
            if(!skin||visualBones==null||bodies==null)return;
            var rest=CandyHandSkeleton.Rest(left);
            for(int i=0;i<16;i++)
            {
                visualBones[i].SetPositionAndRotation(bodies[i].transform.position,bodies[i].transform.rotation);var scale=Vector3.one*builtSize;
                if(i>0){int f=(i-1)/3,j=(i-1)%3;float reference=visualReferenceLengths!=null&&visualReferenceLengths.Length==15?visualReferenceLengths[i-1]:(rest.Joint(f*4+j+1)-rest.Joint(f*4+j)).magnitude;scale.y*=segmentLengths[i-1]/Mathf.Max(.001f,reference);}
                visualBones[i].localScale=scale;
            }
            var bounds=new Bounds(bodies[0].position,Vector3.one*(.3f*builtSize));
            for(int i=1;i<16;i++){bounds.Encapsulate(bodies[i].position);bounds.Encapsulate(bodies[i].position+bodies[i].rotation*(Vector3.up*segmentLengths[i-1]*builtSize));}
            bounds.Expand(.08f*builtSize);skin.localBounds=new Bounds(transform.InverseTransformPoint(bounds.center),bounds.size);
        }
        void OnDisable(){SetInteractive(false);}
        void OnDestroy(){if(friction){if(Application.isPlaying)Destroy(friction);else DestroyImmediate(friction);}}
    }
}
