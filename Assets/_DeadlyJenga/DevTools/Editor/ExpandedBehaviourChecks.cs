using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Modes;
using DeadlyJenga.Presentation;
using DeadlyJenga.CharacterNetwork;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace DeadlyJenga.DevTools.Editor
{
    public static class ExpandedBehaviourChecks
    {
        public static IEnumerator Run(CastlePlaySession session,Action<bool,string> check)
        {
            var runtime=session.Modes;var player=session.Survivor();session.SetMenu(false);var avatar=player.GetComponent<CandyPlayerAvatar>();var appearance=avatar.Appearance;
            var recordings=Resources.LoadAll<AudioClip>("CandyArsenal/ChainGun");check(recordings.Length==5&&recordings.All(c=>c&&c.length>.1f&&c.channels==1),"Five real-source chain-gun samples import as small mono clips");
            var wardrobe=Object.Instantiate(Resources.Load<CandyPlayerRig>("CandyPlayerVisual"));wardrobe.name="Standalone wardrobe model fixture";wardrobe.SetColor(0);var feet=new Vector3(2000,1000,2000);var modelFrame=new CandyAvatarFrame{feet=feet,head=feet+Vector3.up*1.6f,headRotation=Quaternion.identity,scale=1,left=new CandyHandPose{position=feet+new Vector3(-.3f,.75f,.09f),rotation=Quaternion.identity},right=new CandyHandPose{position=feet+new Vector3(.3f,.75f,.09f),rotation=Quaternion.identity}};
            using(var gallery=new WardrobeGallery(feet))
            foreach(AvatarSkin skin in Enum.GetValues(typeof(AvatarSkin)))
            {
                var value=new AvatarAppearance{skin=skin,hair=AvatarHair.LibertySpikes,eyewear=AvatarEyewear.HalfMoon,clothing=AvatarClothing.RoyalMantle,accessories=AvatarAccessory.DragonWings};var decoded=CandyPlayerAvatar.WithStyle(value,CandyPlayerAvatar.StyleBits(value));
                check(decoded.skin==skin&&decoded.hair==value.hair&&decoded.eyewear==value.eyewear&&decoded.clothing==value.clothing,"Appearance wire round trip: "+skin);
                wardrobe.SetAppearance(new AvatarAppearance{skin=skin});wardrobe.ApplyFrame(modelFrame);yield return null;
                check(wardrobe.GetComponentsInChildren<Renderer>().Any(r=>r.enabled)&&wardrobe.headRenderer.bounds.size.sqrMagnitude<100,"Finite wearable geometry: "+skin);foreach(var part in wardrobe.GetComponentsInChildren<Transform>(true))part.gameObject.layer=31;gallery.Capture(skin);
            }
            Object.Destroy(wardrobe.gameObject);
            for(int side=0;side<2;side++)for(int level=0;level<=4;level++)
            {
                var open=CandyHandSkeleton.Rest(side==0);var curled=CandyHandSkeleton.Rest(side==0,level*.25f);float worst=0;
                for(int f=0;f<5;f++)for(int j=0;j<3;j++)worst=Mathf.Max(worst,Mathf.Abs(Vector3.Distance(open.Joint(f*4+j),open.Joint(f*4+j+1))-Vector3.Distance(curled.Joint(f*4+j),curled.Joint(f*4+j+1))));check(worst<.003f,"Finger lengths survive curl, side="+side+" curl="+level);
            }
            check(Enum.GetValues(typeof(CastleBotEmotes.Emote)).Length>=58,"At least fifty additional named bot reactions");
            var rules=ModeSimulation.Defaults(GameModeKind.Demo);rules.countdownSeconds=0;rules.healthEnabled=true;rules.deathEnabled=false;rules.fallDamage=false;rules.island=IslandRule.Walkable;runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.3f);player.Place(new Vector3(150,10.03f,150),Quaternion.identity);player.InputSuspended=true;
            check(runtime.GiveEquipment(EquipmentKind.RocketLauncher),"RPG grant accepted for magazine test");yield return null;
            var pose=new Pose(player.ViewPose.position+Vector3.forward*.4f,Quaternion.identity);
            for(int i=0;i<5;i++){check(runtime.RequestEquipmentAttack(pose),"RPG round "+(i+1)+" fires");yield return new WaitForSeconds(i==4?.05f:1.85f);}
            var magazine=runtime.EquipmentFor(runtime.LocalActorId);check(magazine.rounds==0&&magazine.reloadRemaining>0,"Five RPG rounds start authoritative reload");check(!runtime.RequestEquipmentAttack(pose),"RPG cannot fire during reload");yield return new WaitForSeconds(3.6f);check(runtime.EquipmentFor(runtime.LocalActorId).rounds==5,"RPG magazine refills after reload");
            var body=avatar.Rig.CreateMysteryBody();body.Follow(null,new Vector3(155,10.05f,150));yield return new WaitForSeconds(2);
            check(body.gameObject.activeInHierarchy&&body.Position.y>9,"Mystery corpse collides with real Surface-layer floor instead of passing through");Object.Destroy(body.gameObject);
            var enemies=session.SurvivorBots as IEnemyPopulation;var mushroom=enemies.SpawnEnemy(new EnemySpawnSettings{archetypeId="mushroom",hitDamage=10});check(mushroom,"Mushroom spawns on actual map perimeter");yield return new WaitForSeconds(1.2f);
            if(mushroom){var motor=mushroom.GetComponent<CastleSurvivor>();var actor=mushroom.GetComponent<IModeActor>();check(mushroom.GetComponent<IEnemyActor>().ArchetypeId=="mushroom"&&!runtime.RulesFor(mushroom).canClimb,"Mushroom identity and no-climb rules");check(motor.jumpHeight>=player.jumpHeight*4.9f&&motor.playerScale<.8f,"Mushroom gets short stature and fivefold jump");check(runtime.EquipmentFor(actor.ActorId).kind==EquipmentKind.DualUzi,"Mushroom uses authoritative spore projectile weapon");check(mushroom.GetComponent<CandyEnemyHealth>(),"Mushroom receives live health presentation without constructor errors");}enemies.ClearEnemies();
            var jaguar=enemies.SpawnEnemy(new EnemySpawnSettings{archetypeId="jaguar"});check(jaguar,"Jaguar spawns for posture and health checks");jaguar.GetComponent<CastleBotBrain>().enabled=false;yield return new WaitForSeconds(1.2f);
            var jaguarAvatar=jaguar.GetComponent<CandyPlayerAvatar>();check(jaguarAvatar.Rig.HeadAnchor.position.y-jaguar.transform.position.y<1.1f,"Idle jaguar head is lowered into quadruped posture");jaguar.GetComponent<IDamageable>().Damage(25,player.gameObject);yield return new WaitForSeconds(.2f);
            using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))jaguarAvatar.WriteState(writer);stream.Position=0;using(var reader=new BinaryReader(stream)){CharacterWire.ReadSurvivor(reader,out _,out _,out _,out var decodedEnemy);check(Mathf.Abs(decodedEnemy.healthRatio-.75f)<.001f,"Enemy health fraction survives actual character wire serialization");}}
            var properties=new MaterialPropertyBlock();jaguarAvatar.Rig.bodyRenderer.GetPropertyBlock(properties);check(properties.GetFloat("_Enemy")>.5f&&Mathf.Abs(properties.GetFloat("_Health")-.75f)<.02f,"Jaguar health uses gray bottom-up shader state");jaguar.GetComponent<IDamageable>().Damage(100,player.gameObject);yield return null;check(Object.FindFirstObjectByType<CandyEnemyHairball>(),"Jaguar death produces hairball presentation");enemies.ClearEnemies();
            yield return CaptureMissingMaps(session,check);
            runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.3f);check(runtime.GiveEquipment(EquipmentKind.FlyingHand),"Flying-hand powerup accepted");yield return new WaitForSeconds(.2f);check(session.PlayingHand&&runtime.Frame.players.Any(p=>p.id==runtime.LocalActorId&&p.handPowerRemaining>29),"Powerup activates real hand for thirty seconds");runtime.ActivateHandPower(1);yield return new WaitForSeconds(1.3f);check(!session.PlayingHand&&session.Survivor().Alive,"Temporary hand returns to living survivor");
        }
        static IEnumerator CaptureMissingMaps(CastlePlaySession session,Action<bool,string> check)
        {
            var catalog=IslandLobbyCatalog.Runtime(Resources.Load<IslandLobbyCatalog>("IslandLobby/Catalog"));var old=Services.World;var oldRoot=old as Component;if(oldRoot)oldRoot.gameObject.SetActive(false);var cameraRoot=new GameObject("Map thumbnail capture",typeof(Camera));var camera=cameraRoot.GetComponent<Camera>();camera.enabled=false;camera.fieldOfView=48;camera.nearClipPlane=.2f;camera.farClipPlane=1500;camera.clearFlags=CameraClearFlags.Skybox;var rt=new RenderTexture(640,360,24);camera.targetTexture=rt;
            string folder=ProjectPath.FolderOf("castle.png");int captured=0;
            try
            {
                foreach(var map in catalog.maps)
                {
                    if(!map.mapPrefab||File.Exists(Path.Combine(folder,map.id+".png")))continue;
                    var root=Object.Instantiate(map.mapPrefab);var world=root.GetComponentsInChildren<MonoBehaviour>().OfType<IWorldQuery>().FirstOrDefault();if(world==null){Object.Destroy(root);continue;}Services.World=world;(world as IRebuildable)?.Rebuild();yield return new WaitForSeconds(.15f);
                    var bounds=world.PlayableBounds;foreach(var renderer in root.GetComponentsInChildren<Renderer>())if(renderer.enabled&&renderer.bounds.size.sqrMagnitude<1000000)bounds.Encapsulate(renderer.bounds);float radius=Mathf.Max(8,Mathf.Max(bounds.size.x,bounds.size.z));var center=bounds.center;camera.transform.position=center+new Vector3(-.8f,.8f,-1.1f)*radius;camera.transform.LookAt(center);camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var pixels=new Texture2D(640,360,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,640,360),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(folder,map.id+".png"),pixels.EncodeToPNG());Object.Destroy(pixels);RenderTexture.active=previous;captured++;Object.Destroy(root);yield return null;
                }
                check(catalog.maps.Where(m=>m.mapPrefab).All(m=>File.Exists(Path.Combine(folder,m.id+".png"))),"Every available prefab map has an actual Unity-rendered lobby thumbnail (new="+captured+")");
            }
            finally{Services.World=old;if(oldRoot)oldRoot.gameObject.SetActive(true);camera.targetTexture=null;Object.Destroy(cameraRoot);rt.Release();Object.Destroy(rt);}
        }
    }
}
