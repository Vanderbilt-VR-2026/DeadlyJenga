using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadlyJenga.DevTools.Editor
{
    public static partial class MayhemMarinaChecks
    {
        const string KeepKey="NeedlefrostPhysics";
        const string PeerKey="NeedlefrostPeers";
        public static void RunNeedlefrostWithPeers()
        {
            SessionState.SetBool(PeerKey,true);
            RunNeedlefrost();
        }
        public static void RunNeedlefrost()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report,"Needlefrost Keep / Game.unity\n");
            SessionState.SetBool(KeepKey,true);SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));
            EditorApplication.EnterPlaymode();
        }

        static IEnumerator RunKeepScene()
        {
            bool verifyPeers=SessionState.GetBool(PeerKey,false);
            SessionState.EraseBool(PeerKey);
            yield return RunSoloScene();
            MatchSetup.MapId="mode-needlefrost-keep";MatchSetup.ModeId="demo";
            Check(MatchSetup.SelectedMap?.title=="Needlefrost Keep","Needlefrost registered in live catalog");
            MatchSetup.ApplyMap();
            var map=Services.World as CastleGenerator;
            Check(map && map.settings.layoutPreset=="needlefrost-keep" && !map.foundation,"Physical map loaded without foundation");
            captureOrigin=map.transform.position;
            var placements=CastleLayout.Build(map.settings);
            yield return AuditSettle(map,placements);
            var session=Object.FindFirstObjectByType<CastlePlaySession>();
            var motor=session.Survivor();var spawn=map.SafeSpawnPoint();
            bool west=false,east=false,low=true;
            for(int n=0;n<6;n++)
            {
                var sample=map.transform.InverseTransformPoint(spawn);
                west|=sample.x<0;east|=sample.x>0;low&=sample.y>=2.8f && sample.y<10;
                File.AppendAllText(Report,$"Supported spawn {n} at {sample}\n");
                spawn=map.SafeSpawnPoint();
            }
            motor.Place(spawn,Quaternion.identity);
            yield return new WaitForSeconds(3);
            var eye=new Vector3(4.7f,29.7f,-122);var target=new Vector3(-5.7f,15.8f,2);
            const float overviewFov=30;
            var menu=Object.FindFirstObjectByType<CastleWorldMenu>();
            var tablet=menu?menu.GetComponentInParent<Canvas>():null;
            bool tabletVisible=tablet && tablet.enabled;
            if(tabletVisible)tablet.enabled=false;
            try
            {
                Capture(eye,target,overviewFov,"Needlefrost-Overview.png");
                Capture(new Vector3(-16,4.35f,-20),new Vector3(-10,8,3),67,"Needlefrost-Coast.png");
                Capture(new Vector3(12.8f,4.35f,-15),new Vector3(10,5,-9),70,"Needlefrost-Camp.png");
                Capture(new Vector3(.3f,7.55f,-3),new Vector3(0,12,16),67,"Needlefrost-Bridge.png");
                Capture(new Vector3(15,17.4f,12),new Vector3(-2,15,16),75,"Needlefrost-Tower.png");
                Capture(new Vector3(2.5f,26,-62),new Vector3(0,27,14),36,"Needlefrost-GrandCastle.png");
                Capture(new Vector3(38.7f,3.9f,-20.5f),new Vector3(37,6.2f,-9),66,"Needlefrost-EastTerraces.png");
            }
            finally{if(tabletVisible)tablet.enabled=true;}
            var westFlight=map.Bodies[placements.FindIndex(p=>p.Position.x==-8 && p.Position.z==-11 && p.Size.y==3)];
            bool flightGrip=westFlight.GetComponentsInChildren<MeshCollider>().Any(shape=>
                shape.enabled && shape.gameObject.layer==Layers.Surface && Climbables.TryGet(shape,out var grip) &&
                grip.CanClimb && grip.Body==westFlight);
            File.AppendAllText(Report,$"West stair has a grippable survivor surface on its moving body: {flightGrip}\n");
            float drift=placements.Select((p,i)=>Vector3.Distance(map.Bodies[i].position,map.transform.TransformPoint(p.Position))).Max();
            foreach(var index in Enumerable.Range(0,placements.Count).OrderByDescending(i=>Vector3.Distance(map.Bodies[i].position,map.transform.TransformPoint(placements[i].Position))).Take(6))
                File.AppendAllText(Report,$"Settle #{index} course {placements[index].Course} home {placements[index].Position} actual {map.Bodies[index].position}\n");
            File.AppendAllText(Report,"Initial maximum drift "+drift+" m; bodies "+placements.Count+"\n");
            int sculptures = 0, seated = 0;
            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i].Course < 100) continue;
                var body = map.Bodies[i];
                if (body.GetComponent<MeshCollider>()?.sharedMesh) sculptures++;
                // The seat is the joint anchored at the member's base; tie joints to engaged neighbours are separate.
                var seam = body.GetComponents<FixedJoint>().FirstOrDefault(j=>j.anchor.y<-.4f);
                if (seam && seam.connectedBody && !body.isKinematic) seated++;
            }
            File.AppendAllText(Report,$"Blender sculpted members {sculptures}; breakable seats {seated}\n");
            bool outerRoute = false;
            yield return WalkKeep(motor, new Vector3(38.48f,3.1f,-19.65f), new[]{
                new Vector3(38.7f,4.89f,-14.55f), new Vector3(39.4f,4.89f,-12.95f)
            },result=>outerRoute = result,"Blender east outer shelf ascent");
            bool routes=west && east && low && flightGrip;
            yield return WalkKeep(motor,new Vector3(-8,3.1f,-16),new[]{new Vector3(-8,6,-5),new Vector3(-6,6,-3),
                new Vector3(0,6.15f,-3),new Vector3(7,6,-3)},result=>routes &= result,"intact coast stair and low bridge");
            yield return WalkKeep(motor,new Vector3(16,3.1f,-8),new[]{new Vector3(16,3,-6.45f)},result=>routes &= result,"camp to grotto interior");
            yield return WalkKeep(motor,new Vector3(20.4f,9.1f,5.80f),new[]{new Vector3(20.4f,16,16.77f),
                new Vector3(17.55f,16,16.77f),new Vector3(17.55f,16,14.45f),new Vector3(16,16,13),new Vector3(13,16,13),
                new Vector3(10,16,13),new Vector3(5,16.15f,13)},result=>routes &= result,"right tower to upper crossing");
            var cornice=map.Bodies[placements.FindIndex(p=>Mathf.Abs(p.Position.x+29.28f)<.02f)];
            float carried=0,drop=0;
            yield return DeadlyJenga.HandValidation.FlyingHandFeelValidation.PullMapMember(cornice,(travel,fallDepth)=>{carried=travel;drop=fallDepth;});
            File.AppendAllText(Report,$"Production hand cornice pull {carried:F2} m outward while closed, drop {drop:F2} m after opening\n");
            LogNeighbours(map,placements,"cornice",cornice);
            var pier=map.Bodies[placements.FindIndex(p=>Mathf.Abs(p.Position.x+26)<.01f && p.Position.z==-19 && p.Position.y<1)];
            var deck=map.Bodies[placements.FindIndex(p=>Mathf.Abs(p.Position.x+26)<.01f && p.Position.z==-19 && p.Position.y>1)];
            float deckY=deck.position.y;
            // A horizontal load fractures the socket, then gravity carries the now unsupported slab.
            pier.GetComponent<IPushable>().Push(Vector3.left*44,null);
            deck.WakeUp();
            for(int i=0;i<5;i++)
            {
                yield return new WaitForSeconds(.15f);
                Capture(eye,target,overviewFov,"Needlefrost-Fracture-"+i+".png");
            }
            float fall=deckY-deck.position.y;
            int eastPierIndex=placements.FindIndex(p=>Mathf.Abs(p.Position.x-40.76f)<.02f &&
                p.Position.z==-22 && p.Position.y<1);
            int eastDeckIndex=placements.FindIndex(p=>Mathf.Abs(p.Position.x-40.76f)<.02f &&
                p.Position.z==-22 && p.Position.y>1);
            var eastPier=map.Bodies[eastPierIndex];
            var eastDeck=map.Bodies[eastDeckIndex];
            float eastBefore=eastDeck.position.y;
            eastPier.GetComponent<IPushable>().Push(Vector3.back*48,null);
            eastDeck.WakeUp();
            yield return new WaitForSeconds(.85f);
            float eastFall=eastBefore-eastDeck.position.y;
            File.AppendAllText(Report,$"Outer east deck fell {eastFall:F2} m after its socket load\n");
            var bridge=map.Bodies[placements.FindIndex(p=>p.Position.x==0 && p.Position.z==-3)];
            bridge.GetComponent<IPushable>().Push(Vector3.forward*12,null);
            yield return new WaitForSeconds(1);
            File.AppendAllText(Report,$"Displaced low bridge now at {bridge.position}\n");
            var driver=new MarinaWalkDriver();
            motor.externallyDriven=true;motor.InputDriver=driver;motor.InputSuspended=false;
            motor.Place(new Vector3(-8,3.12f,-16)+captureOrigin,Quaternion.identity);
            driver.target=new Vector3(-8,6,-5)+captureOrigin;
            float end=Time.time+12;
            while(Time.time<end && (driver.target-motor.transform.position).sqrMagnitude>1)yield return null;
            float walk=Vector3.Distance(driver.target,motor.transform.position);
            File.AppendAllText(Report,$"Motor ended at {motor.transform.position}, target {driver.target}, status {motor.ActionStatus}\n");
            motor.InputDriver=null;motor.externallyDriven=false;
            var upper=map.Bodies[placements.FindIndex(p=>Mathf.Abs(p.Position.x-8.55f)<.01f && p.Size.x>8 && p.Size.y==1)];
            // The grand keep's floor now flanks the deck on both sides, so a shove along the deck only wedges it.
            // Knock out the arch it stands on instead; the deck then drops away.
            var upperArch=map.Bodies[placements.FindIndex(p=>Mathf.Abs(p.Position.x-8.55f)<.01f && p.Size.x>8 && p.Size.y>5)];
            var oldUpper=upper.position;upperArch.GetComponent<IPushable>().Push(Vector3.forward*16,null);
            yield return new WaitForSeconds(1.2f);
            float upperTravel=Vector3.Distance(upper.position,oldUpper);
            LogNeighbours(map,placements,"upper bridge",upper);
            yield return WalkKeep(motor,new Vector3(16,3.1f,-8.8f),new[]{new Vector3(24.3f,6,-8.8f),new Vector3(24.3f,6,-5)},result=>routes &= result,"east ascent after upper crossing loss");
            bool eliminated=false;
            void Observe(GameObject who,GameObject source){if(who==motor.gameObject)eliminated=true;}
            GameEvents.Eliminated+=Observe;
            try
            {
                motor.Place(captureOrigin+new Vector3(0,1,-16),Quaternion.identity);
                float deadline=Time.time+3;
                while(!eliminated && Time.time<deadline)yield return null;
            }
            finally{GameEvents.Eliminated-=Observe;}
            map.Rebuild();yield return new WaitForSeconds(1);
            motor.Place(map.SafeSpawnPoint(),Quaternion.identity);
            File.AppendAllText(Report,$"Ocean elimination {eliminated}; upper bridge moved {upperTravel:F2} m; route checks {routes}\n");
            Check(placements.Count==map.Bodies.Count,"Reset restores physical body count");
            bool bot=session.SurvivorBots.SpawnOne();
            File.AppendAllText(Report,$"Coast fall {fall:F2} m; ascent remaining {walk:F2} m; bot spawn {bot}\n");
            Check(sculptures == 7 && seated == 7, "Seven sculpted Blender members retain individual dynamic bodies and breakable bearing seats");
            Check(outerRoute, "Survivor reaches the raised outer shelf from the new east terrace");
            Check(eastFall > .4f, "New east shelf drops after its own support breaks");
            Check(drift<.6f && fall>.45f && walk<1.2f && bot && routes && eliminated && upperTravel>1,"Intact settling, coast collapse, alternate ascent and bot smoke");
            if(verifyPeers)yield return NeedlefrostPeers.Run(map,session,Check);
            // A flying-hand grip fixture, reported on its own so it does not hide the map's physics results.
            Check(carried>.5f && drop>.3f,"Production hand pulls the loose coast cornice outward and drops it");
        }

        // Lists the members that start inside each other and the first ones to move after load.
        static IEnumerator AuditSettle(CastleGenerator map,System.Collections.Generic.List<CastlePlacement> placements)
        {
            Physics.SyncTransforms();
            var shapes=map.Bodies.Select(b=>b.GetComponentsInChildren<Collider>().Where(c=>c.enabled && !c.isTrigger).ToArray()).ToArray();
            int overlaps=0;
            for(int i=0;i<shapes.Length;i++)
            for(int j=i+1;j<shapes.Length;j++)
            {
                float deepest=0;
                foreach(var a in shapes[i])
                foreach(var b in shapes[j])
                {
                    if(Physics.GetIgnoreLayerCollision(a.gameObject.layer,b.gameObject.layer) || Physics.GetIgnoreCollision(a,b) || !a.bounds.Intersects(b.bounds))continue;
                    if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth))
                        deepest=Mathf.Max(deepest,depth);
                }
                if(deepest<.02f)continue;
                overlaps++;
                if(overlaps<=40)File.AppendAllText(Report,$"OVERLAP #{i} course {placements[i].Course} at {placements[i].Position} with #{j} course {placements[j].Course} at {placements[j].Position} depth {deepest:F3}\n");
            }
            File.AppendAllText(Report,$"Initial colliding overlaps deeper than 2 cm: {overlaps}\n");
            var moved=new System.Collections.Generic.HashSet<int>();
            float start=Time.time;
            foreach(float at in new[]{.05f,.1f,.2f,.4f,.8f,1.6f,3f})
            {
                while(Time.time-start<at)yield return new WaitForFixedUpdate();
                int seats=map.Bodies.Count(b=>b.GetComponents<FixedJoint>().Any(s=>s.connectedBody && s.anchor.y<-.4f));
                File.AppendAllText(Report,$"SETTLE t={at:F2} sculpted seats {seats}\n");
                for(int i=0;i<placements.Count;i++)
                {
                    float d=Vector3.Distance(map.Bodies[i].position,map.transform.TransformPoint(placements[i].Position));
                    if(d<.1f || !moved.Add(i) || moved.Count>30)continue;
                    File.AppendAllText(Report,$"MOVED t={at:F2} #{i} course {placements[i].Course} home {placements[i].Position} by {d:F2} velocity {map.Bodies[i].linearVelocity}\n");
                }
            }
        }

        // What a member touches, is jointed to or ignores, for checks that expect it to move.
        static void LogNeighbours(CastleGenerator map,System.Collections.Generic.List<CastlePlacement> placements,string label,Rigidbody body)
        {
            foreach(var joint in body.GetComponents<Joint>())
                File.AppendAllText(Report,$"NEAR {label} joint to {(joint.connectedBody?map.Bodies.IndexOf(joint.connectedBody).ToString():"world")}\n");
            foreach(var own in body.GetComponentsInChildren<Collider>().Where(c=>c.enabled && !c.isTrigger))
            {
                var bounds=own.bounds;
                foreach(var other in Physics.OverlapBox(bounds.center,bounds.extents+Vector3.one*.05f))
                {
                    if(!other.attachedRigidbody || other.attachedRigidbody==body)continue;
                    int index=map.Bodies.IndexOf(other.attachedRigidbody);
                    bool touching=Physics.ComputePenetration(own,own.transform.position,own.transform.rotation,
                        other,other.transform.position,other.transform.rotation,out _,out float depth);
                    File.AppendAllText(Report,$"NEAR {label} {own.name} touches #{index} course {(index>=0?placements[index].Course:-1)} {other.name} ignored {Physics.GetIgnoreCollision(own,other)} depth {(touching?depth:0):F3}\n");
                }
            }
        }

        static IEnumerator WalkKeep(CastleSurvivor motor,Vector3 start, Vector3[] goals,System.Action<bool> result,string label)
        {
            var driver=new MarinaWalkDriver();
            motor.externallyDriven=true;motor.InputDriver=driver;motor.InputSuspended=false;
            motor.Place(captureOrigin+start,Quaternion.identity);
            try
            {
                foreach(var local in goals)
                {
                    driver.target=captureOrigin+local;
                    float deadline=Time.time+4+Vector3.Distance(motor.transform.position,driver.target)*1.6f;
                    while(Time.time<deadline && motor.Alive &&
                        Vector3.Distance(motor.transform.position,driver.target)>.49f)yield return null;
                    float remaining=Vector3.Distance(motor.transform.position,driver.target);
                    File.AppendAllText(Report,$"WALK {label}: goal {local}, feet {motor.transform.position-captureOrigin}, remaining {remaining:F2}, grounded {motor.Grounded}, alive {motor.Alive}\n");
                    if(!motor.Alive || remaining>.65f){result(false);yield break;}
                }
                result(true);
            }
            finally{motor.InputDriver=null;motor.externallyDriven=false;}
        }
    }
}
