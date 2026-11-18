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
        readonly bool[] digitFree=new bool[5];
        readonly float[] tipReach=new float[16];
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
        // The drawn hand: which fingers are resting on something, and where each finger starts on the palm.
        readonly CandyHandContactProbe[] touches=new CandyHandContactProbe[16];
        readonly float[] fingerContact=new float[5];
        Mesh originalSkin;
        static readonly System.Collections.Generic.Dictionary<Mesh,Mesh> leftSkins=new();
        // The baked skin shares every finger vertex between neighbouring bones. On short finger bones that
        // squeezes the middle of a bent finger to half its thickness. Sharpening the shares keeps the middle
        // of each bone rigid (full thickness) and leaves the blend only at the joints.
        static readonly System.Collections.Generic.Dictionary<Mesh,Mesh> firmSkins=new();
        static Mesh FirmFingerWeights(Mesh source)
        {
            if(!source||!source.isReadable)return source;
            if(firmSkins.TryGetValue(source,out var cached)&&cached)return cached;
            var weights=source.boneWeights;
            for(int i=0;i<weights.Length;i++)
            {
                var w=weights[i];
                bool finger=w.weight0>0&&w.boneIndex0>=1&&w.boneIndex0<=15||w.weight1>0&&w.boneIndex1>=1&&w.boneIndex1<=15||
                    w.weight2>0&&w.boneIndex2>=1&&w.boneIndex2<=15||w.weight3>0&&w.boneIndex3>=1&&w.boneIndex3<=15;
                if(!finger)continue;
                float a=Mathf.Pow(w.weight0,4),b=Mathf.Pow(w.weight1,4),c=Mathf.Pow(w.weight2,4),d=Mathf.Pow(w.weight3,4);
                float total=a+b+c+d;if(total<=0)continue;
                w.weight0=a/total;w.weight1=b/total;w.weight2=c/total;w.weight3=d/total;weights[i]=w;
            }
            var mesh=Instantiate(source);mesh.name=source.name+" (firm fingers)";mesh.boneWeights=weights;
            firmSkins[source]=mesh;return mesh;
        }
        void FitSkinHandedness()
        {
            if(!skin||!skin.sharedMesh)return;
            if(!originalSkin)originalSkin=FirmFingerWeights(CandySmoothHandMesh.SeparateDigitWeights(skin.sharedMesh,1,5));
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
        public void SetTarget(CandyHandPose target,float strength){SetTarget(target,strength,null);}
        // exactBones: the 15 finger bone directions in palm space at full precision, when the caller has them.
        // The skeleton in a pose is stored to 1 mm, which makes slowly moving fingers shiver if drawn from it.
        public void SetTarget(CandyHandPose target,float strength,Vector3[] exactBones)
        {Target=target;Strength=Mathf.Clamp01(strength);if(exactBones!=null&&exactBones.Length==15){System.Array.Copy(exactBones,exactTargetBones,15);hasExactBones=true;}else hasExactBones=false;}
        readonly Vector3[] exactTargetBones=new Vector3[15];bool hasExactBones;
        public void Build(CandyHandPose initial)
        {
            builtSize=settings.size;Target=initial;contacts=new Collider[16];bodies=new Rigidbody[16];segmentLengths=new float[15];joints=new ConfigurableJoint[15];referenceRelative=new Quaternion[15];
            friction=new PhysicsMaterial("Candy contact friction"){staticFriction=settings.staticFriction,dynamicFriction=settings.dynamicFriction,frictionCombine=PhysicsMaterialCombine.Average,bounciness=0};
            for(int i=0;i<16;i++)
            {
                var go=new GameObject(i==0?"Physical palm":"Physical finger "+(i-1)/3+" joint "+(i-1)%3);go.transform.SetParent(transform,false);go.layer=0;
                var rb=go.AddComponent<Rigidbody>();rb.mass=Mathf.Max(.001f,i==0?settings.palmMass:settings.fingerMass);rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.solverIterations=settings.solverIterations;rb.solverVelocityIterations=settings.solverVelocityIterations;rb.maxAngularVelocity=settings.maxAngularSpeed;rb.maxLinearVelocity=settings.maxSpeed;rb.maxDepenetrationVelocity=1;
                bodies[i]=rb;touches[i]=go.AddComponent<CandyHandContactProbe>();touches[i].At=-100;
                if(i==0){var col=go.AddComponent<BoxCollider>();col.center=new Vector3(0,0,.020f)*builtSize;col.size=new Vector3(.078f,.04f,.078f)*builtSize;contacts[i]=col;}
                else
                {
                    int f=(i-1)/3,j=(i-1)%3;Vector3 a=initial.skeleton.Joint(f*4+j),b=initial.skeleton.Joint(f*4+j+1);segmentLengths[i-1]=Mathf.Max(.005f,(b-a).magnitude);
                    // Joints are stored to 1 mm. Snap near-rest lengths so every hand gets the same finger proportions.
                    var resting=CandyHandSkeleton.Rest(left);float restLength=(resting.Joint(f*4+j+1)-resting.Joint(f*4+j)).magnitude;
                    if(Mathf.Abs(segmentLengths[i-1]-restLength)<.0015f)segmentLengths[i-1]=restLength;
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
            // Player hands build their joints with straight fingers, so the joint limits below are measured from straight.
            var jointPose=initial;if(!JointDrivenFingers)jointPose.skeleton=Straightened(initial.skeleton);
            ResetInactive(jointPose); // Safe only before activating contacts.
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
                else
                {
                    // Firm fingers: they curl toward the palm but cannot bend backwards, fold sideways or roll.
                    // Pressing a fingertip on a block then pushes on the whole hand instead of folding the finger.
                    bool thumb=i<=3,knuckle=j==0;
                    // All three stay "Limited". Locking any one of them silently switches off the joint's drive
                    // (the slerp drive needs every turning axis unlocked), and the fingers then just flop.
                    joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Limited;
                    // The thumb's base swings every way (that is how it gets across the palm to oppose the fingers).
                    joint.angularYLimit=new SoftJointLimit{limit=thumb?(knuckle?70:15):3};
                    joint.lowAngularXLimit=new SoftJointLimit{limit=thumb?-90:-FingerBackBendDegrees};
                    joint.highAngularXLimit=new SoftJointLimit{limit=thumb?90:FingerCurlDegrees};
                    joint.angularZLimit=new SoftJointLimit{limit=thumb?(knuckle?70:25):knuckle?25:6};
                }
                joint.enableCollision=false;joint.enablePreprocessing=true;joint.projectionMode=JointProjectionMode.None;
                joint.rotationDriveMode=RotationDriveMode.Slerp;joint.configuredInWorldSpace=false;joints[i-1]=joint;
                referenceRelative[i-1]=Quaternion.Inverse(joint.connectedBody.rotation)*bodies[i].rotation;
            }
            // Internal contacts produce jitter, never gripping force. Ignore only this hand's pairs.
            for(int i=0;i<16;i++)for(int j=i+1;j<16;j++)Physics.IgnoreCollision(contacts[i],contacts[j]);
            if(!JointDrivenFingers)ResetInactive(initial);
            FitSkinHandedness();if(skin)skin.sharedMaterial=settings.skin;active=true;SetInteractive(false);
        }
        // Rotation of one finger bone, in palm space. Each bone only bends away from its rest pose and
        // carries its parent's roll along. A bone that rolls against its neighbour pinches the skin
        // into a thin thread, so roll is never taken from the palm direction (that flips past 90 degrees).
        public static Quaternion FingerRotation(in CandyHandSkeleton skeleton,bool left,int finger,int joint)
        {
            var rest=CandyHandSkeleton.Rest(left);
            Quaternion bend=Quaternion.identity;Vector3 restBone=Vector3.forward;
            for(int k=0;k<=joint;k++)
            {
                int at=finger*4+k;
                restBone=rest.Joint(at+1)-rest.Joint(at);
                Vector3 bone=skeleton.Joint(at+1)-skeleton.Joint(at);
                if(bone.sqrMagnitude>1e-10f)bend=Quaternion.FromToRotation(bend*restBone,bone)*bend;
            }
            return bend*SegmentRotation(restBone,Vector3.up);
        }
        public const float FingerCurlDegrees=115,FingerBackBendDegrees=12;
        // Share of the hand's strength one finger (or the thumb) can use. Grip adds up finger by finger:
        // a thumb-and-index pinch holds less than a whole hand.
        public const float FingerStrengthShare=.12f,ThumbStrengthShare=.25f;
        // Same knuckles and bone lengths, fingers pointing as they do at rest.
        CandyHandSkeleton Straightened(CandyHandSkeleton skeleton)
        {
            var rest=CandyHandSkeleton.Rest(left);var result=skeleton;
            for(int f=0;f<5;f++)
            {
                Vector3 at=skeleton.Joint(f*4);
                for(int j=0;j<3;j++)
                {
                    int i=f*4+j;
                    at+=(rest.Joint(i+1)-rest.Joint(i)).normalized*(skeleton.Joint(i+1)-skeleton.Joint(i)).magnitude;
                    result.SetJoint(i+1,at);
                }
            }
            return result;
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
            position+=pose.rotation*a*builtSize;rotation=pose.rotation*FingerRotation(pose.skeleton,left,f,j);
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
            // The profiled material multiplies with the block's coefficient. Add grip at
            // the hand surface so the drive can use less squeeze force.
            float grip=RequiresProfile&&Profile?Profile.Friction*2:settings.staticFriction;
            float sliding=RequiresProfile?grip*.85f:settings.dynamicFriction;
            if(friction.staticFriction!=grip)friction.staticFriction=grip;
            if(friction.dynamicFriction!=sliding)friction.dynamicFriction=sliding;
            friction.frictionCombine=RequiresProfile?PhysicsMaterialCombine.Multiply:PhysicsMaterialCombine.Average;
            float requestedEffort=0;
            // Only the palm chases the tracked hand. Fingers hold their pose on the real palm, so a hand pressed
            // against something keeps its fingers firm instead of dragging them on toward where tracking went.
            var onPalm=Target;onPalm.position=bodies[0].position;onPalm.rotation=bodies[0].rotation;
            for(int i=0;i<bodies.Length;i++)
            {
                var rb=bodies[i];Desired(i==0?Target:onPalm,i,out var p,out var q);
                rb.maxLinearVelocity=RequiresProfile?Mathf.Min(settings.maxSpeed,12):settings.maxSpeed;
                // The palm carries the whole hand, so damp it for the whole hand's mass. Damped for the palm alone,
                // the hand wobbled around its target and bounced off whatever it pressed on.
                float carried=0;if(i==0)foreach(var part in bodies)carried+=part.mass;
                float palmDamping=Mathf.Max(settings.positionDamping,1.8f*Mathf.Sqrt(settings.positionSpring*Mathf.Max(carried,rb.mass)));
                float denominator=1+dt*palmDamping/Mathf.Max(carried,rb.mass)+dt*dt*settings.positionSpring/Mathf.Max(carried,rb.mass);
                // Only the palm is pulled toward a place. A finger is held on by its joint, so pulling it as well
                // just fights that joint and spends the hand's strength on nothing. Fingers are turned, not pulled.
                driveForces[i]=i==0?Vector3.ClampMagnitude(
                    ((p-rb.position)*settings.positionSpring-rb.linearVelocity*palmDamping)/denominator,
                    settings.palmForce*Strength):Vector3.zero;
                if(i>0)
                {
                    int parent=(i-1)%3==0?0:i-1;
                    Desired(onPalm,parent,out _,out var parentRotation);
                    Quaternion relative=Quaternion.Inverse(parentRotation)*q;
                    joints[i-1].targetRotation=Quaternion.Inverse(relative)*referenceRelative[i-1];
                    // A finger asks for torque by how far it is from its pose: resting fingers leave the
                    // strength budget to whatever is actually pushing, and a blocked finger pushes harder.
                    float off=Quaternion.Angle(Quaternion.Inverse(bodies[parent].rotation)*rb.rotation,relative)*Mathf.Deg2Rad;
                    driveTorques[i]=(settings.fingerTorque*.1f+settings.rotationSpring*6*off)*Strength;
                }
                else
                {
                    palmTorque=RequestedPalmTorque(rb,q,dt);
                    driveTorques[0]=palmTorque.magnitude;
                }
            }
            // Holding its own angle is not pushing: the palm's turning strength is its own allowance (the hand's
            // strength at the palm's edge) instead of being taken out of the push. A hand that spends everything on
            // pressing would otherwise tip over on its fingers.
            float turnAllowance=float.IsInfinity(forceBudget)?float.PositiveInfinity:forceBudget*Strength*DriveLever(0);
            if(driveTorques[0]>turnAllowance){palmTorque*=turnAllowance/driveTorques[0];driveTorques[0]=turnAllowance;}
            requestedEffort=driveForces[0].magnitude;
            for(int f=0;f<5;f++)
            {
                // A finger touching nothing only moves itself. That costs none of the hand's strength, so it may
                // move as fast as a finger does. The strength rule applies to what the hand does to other things.
                bool touching=false;
                for(int j=0;j<3;j++)touching|=touches[1+f*3+j]&&Time.fixedTime-touches[1+f*3+j].At<.15f;
                digitFree[f]=!touching&&forceBudget>0;
                // A touching finger's strength is what it can press with at its tip: its share of the hand's
                // strength. Each joint may turn hard enough to deliver that at the fingertip, and no harder.
                float press=forceBudget*(f==0?ThumbStrengthShare:FingerStrengthShare)*Strength,effort=0;
                for(int j=0;j<3;j++)
                {
                    int i=1+f*3+j;float toTip=0;
                    for(int k=j;k<3;k++)toTip+=segmentLengths[f*3+k]*builtSize;
                    float most=digitFree[f]?settings.fingerTorque*Strength:Mathf.Min(press*toTip,settings.fingerTorque*2*Strength);
                    driveTorques[i]=Mathf.Min(driveTorques[i],most);
                    tipReach[i]=Mathf.Max(.01f,toTip);
                    effort=Mathf.Max(effort,driveTorques[i]/tipReach[i]);
                }
                if(!digitFree[f])requestedEffort+=effort;
            }
            float scale=requestedEffort>0?Mathf.Min(1,forceBudget*Strength/requestedEffort):0;
            for(int i=0;i<bodies.Length;i++)
            {
                Vector3 force=driveForces[i]*scale;
                bool free=i>0&&digitFree[(i-1)/3];
                float torque=driveTorques[i]*(free?1:scale);
                bodies[i].AddForce(force,ForceMode.Force);
                MaximumAppliedForce=Mathf.Max(MaximumAppliedForce,force.magnitude);
                AppliedLinearForceNewtons+=force.magnitude;
                if(i>0)AppliedTorqueNewtonMetres+=torque;
                if(!free)AppliedDriveEffortNewtons+=force.magnitude+(i==0?0:torque/(tipReach[i]*3));
                if(i==0){torque=driveTorques[0];bodies[0].AddTorque(palmTorque,ForceMode.Force);}
                else joints[i-1].slerpDrive=new JointDrive
                {
                    positionSpring=torque>0?settings.rotationSpring*6:0,
                    positionDamper=torque>0?settings.rotationDamping*3:0,
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

        // How briskly the palm follows a wrist turn (radians per second; higher = snappier). Torque stays capped by palmTorque.
        const float PalmTurnResponse=14;
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
            // The palm turns the whole hand, fingers included, so size the turning torque for all of it.
            // (Sized for the palm alone, a giant hand took seconds to follow a wrist turn.)
            Vector3 inertia=body.inertiaTensor,torque=Vector3.zero;
            Quaternion toFrame=Quaternion.Inverse(inertiaFrame);
            for(int i=1;i<bodies.Length;i++)
            {
                Vector3 r=toFrame*(bodies[i].worldCenterOfMass-body.worldCenterOfMass);float m=bodies[i].mass,square=r.sqrMagnitude;
                inertia.x+=m*(square-r.x*r.x);inertia.y+=m*(square-r.y*r.y);inertia.z+=m*(square-r.z*r.z);
            }
            for(int n=0;n<3;n++)
            {
                float spring=inertia[n]*PalmTurnResponse*PalmTurnResponse,damping=2*inertia[n]*PalmTurnResponse;
                torque[n]=(e[n]*spring-w[n]*damping)/(1+dt*damping/Mathf.Max(.00000001f,inertia[n])+dt*dt*spring/Mathf.Max(.00000001f,inertia[n]));
            }
            return Vector3.ClampMagnitude(inertiaFrame*torque,settings.palmTorque*Strength);
        }
        // Draws the hand as one whole glove. Bones are never copied straight from the physics bodies:
        // those can drift apart, lag or roll, and the skin between them would stretch into noodles.
        // Every finger starts at its knuckle on the palm and each bone starts where the last one ends.
        // A free finger shows the tracked pose exactly; a finger touching something shows where physics stopped it.
        void LateUpdate()
        {
            if(!skin||visualBones==null||bodies==null)return;
            var rest=CandyHandSkeleton.Rest(left);
            Vector3 palmAt=bodies[0].transform.position;Quaternion palmTurn=bodies[0].transform.rotation;
            Quaternion fromPalm=Quaternion.Inverse(palmTurn);
            visualBones[0].SetPositionAndRotation(palmAt,palmTurn);visualBones[0].localScale=Vector3.one*builtSize;
            // Network copies only know the bodies, so they always follow them.
            bool followBodies=ReplicatedBones||NetworkPreview&&HasFreshAuthoritativePreview;
            float limit=CandyHandPoseLimits.MaximumBendDegrees*Mathf.Deg2Rad;
            var bounds=new Bounds(palmAt,Vector3.one*(.3f*builtSize));
            for(int f=0;f<5;f++)
            {
                bool touching=false;
                if(active&&!followBodies)for(int j=0;j<3;j++)touching|=touches[1+f*3+j]&&Time.fixedTime-touches[1+f*3+j].At<.1f;
                fingerContact[f]=followBodies?1:Mathf.MoveTowards(fingerContact[f],touching?1:0,Time.unscaledDeltaTime*10);
                Vector3 at=palmAt+palmTurn*(rest.Joint(f*4)*builtSize),previous=Vector3.zero;
                Quaternion bend=Quaternion.identity;
                for(int j=0;j<3;j++)
                {
                    int i=1+f*3+j,joint=f*4+j;
                    Vector3 restBone=rest.Joint(joint+1)-rest.Joint(joint);
                    Vector3 tracked=hasExactBones?exactTargetBones[f*3+j]:Target.skeleton.Joint(joint+1)-Target.skeleton.Joint(joint);
                    Vector3 physical=fromPalm*bodies[i].transform.up;
                    Vector3 bone=tracked.sqrMagnitude>1e-10f?Vector3.Slerp(tracked.normalized,physical,fingerContact[f]):physical;
                    if(bone.sqrMagnitude<1e-6f)bone=restBone;
                    bone.Normalize();
                    if(j>0)bone=Vector3.RotateTowards(previous,bone,limit,0);
                    bend=Quaternion.FromToRotation(bend*restBone,bone)*bend;
                    visualBones[i].SetPositionAndRotation(at,palmTurn*bend*SegmentRotation(restBone,Vector3.up));
                    // Only length is scaled, so a finger keeps its full thickness whatever the hand size.
                    float reference=visualReferenceLengths!=null&&visualReferenceLengths.Length==15?visualReferenceLengths[i-1]:restBone.magnitude;
                    var scale=Vector3.one*builtSize;scale.y*=segmentLengths[i-1]/Mathf.Max(.001f,reference);
                    visualBones[i].localScale=scale;
                    at+=palmTurn*bone*(segmentLengths[i-1]*builtSize);previous=bone;
                    bounds.Encapsulate(at);
                }
            }
            bounds.Expand(.08f*builtSize);skin.localBounds=new Bounds(transform.InverseTransformPoint(bounds.center),bounds.size);
        }
        void OnDisable(){SetInteractive(false);}
        void OnDestroy(){if(friction){if(Application.isPlaying)Destroy(friction);else DestroyImmediate(friction);}}
    }
}
