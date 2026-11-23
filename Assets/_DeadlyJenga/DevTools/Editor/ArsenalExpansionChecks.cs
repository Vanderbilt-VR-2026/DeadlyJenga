using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeadlyJenga.Core;
using DeadlyJenga.Core.Testing;
using DeadlyJenga.CastlePrototype;
using DeadlyJenga.CastleBots;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Modes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using UnityEngine.Rendering.Universal;

namespace DeadlyJenga.DevTools.Editor
{
    [InitializeOnLoad] public static class ArsenalExpansionChecks
    {
        const string Key="ArsenalExpansion";
        static int checks,errors;static bool failed,completed;
        static bool SawStaffOnly => SessionState.GetBool(Key+"SawStaffOnly",false);
        static string Report=>ProjectPath.Report(SawStaffOnly ? "SawWheelStaffContacts.txt" : "ArsenalExpansion.txt");
        sealed class PickupSource:IEquipmentPickupSource {public EquipmentKind Kind{get;set;}public Vector3 Position{get;set;}public float Duration{get;set;}=1;}
        [Serializable] sealed class Scenes{public SceneSetup[] values;}
        static ArsenalExpansionChecks()
        {
            EditorApplication.update+=()=>
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                string path=ProjectPath.Report("ArsenalExpansionMain.request");
                if(!File.Exists(path)||File.ReadAllText(path).Trim()!=System.Diagnostics.Process.GetCurrentProcess().Id.ToString()+":v9"||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
                if(!ValidationRunGate.TryAcquire(Key))return;
                File.Delete(path);Run();
            };
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false))return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {checks=errors=0;failed=completed=false;AssetDatabase.DisallowAutoRefresh();EditorApplication.LockReloadAssemblies();SessionState.SetBool(Key+"Locked",true);Application.logMessageReceived+=Log;EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(Guard());}
                if(state==PlayModeStateChange.EnteredEditMode)
                {
                    ValidationRunGate.Release(Key);
                    if(SessionState.GetBool(Key+"Locked",false)){SessionState.SetBool(Key+"Locked",false);AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();}
                    Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                    SpectatorVideoCapture.ApplyChoices(JsonUtility.FromJson<SpectatorRecordingPlan>(SessionState.GetString(Key+"Choices","")));
                    MatchSetup.FromLobby=SessionState.GetBool(Key+"Lobby",false);MatchSetup.StartLocalSession=SessionState.GetBool(Key+"Local",false);
                    EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(SessionState.GetString(Key+"Scenes","")).values);
                    failed|=!completed;File.AppendAllText(Report,$"COMPLETE checks={checks} runtimeErrors={errors} failed={failed}. Desktop runtime; headset not tested.\n");
                    if (SawStaffOnly)
                    {
                        int result = failed || errors > 0 ? 1 : 0;
                        if (!string.IsNullOrEmpty(SessionState.GetString("DJWarm.id","")))
                        {
                            Directory.CreateDirectory("Logs/Warm");
                            File.WriteAllText("Logs/Warm/exit.txt",result.ToString());
                        }
                        else if (Application.isBatchMode)
                            EditorApplication.Exit(result);
                    }
                }
            };
        }
        [MenuItem("Tools/Deadly Jenga/Validation/Check Arsenal Expansion")]
        public static void Run()=>Start(false);

        public static void RunBarbedBat()=>Start(true);

        public static void RunCosmeticWeapons()=>Start(false, true);

        public static void RunRocketCandy()=>Start(false, false, true);

        public static void RunSawStaff()=>Start(false, sawStaffOnly:true);

        public static void RunMarshmallow()=>Start(false, marshmallowOnly:true);

        public static void RunIronSuitPalette()=>Start(false, ironSuitOnly:true);

        static void Start(bool barbedBatOnly, bool cosmeticWeaponsOnly=false, bool candyOnly=false, bool sawStaffOnly=false, bool marshmallowOnly=false, bool ironSuitOnly=false)
        {
            SessionState.SetBool(Key+"BarbedBatOnly",barbedBatOnly);
            SessionState.SetBool(Key+"CosmeticWeaponsOnly",cosmeticWeaponsOnly);
            SessionState.SetBool(Key+"RocketCandyOnly",candyOnly);
            SessionState.SetBool(Key+"SawStaffOnly",sawStaffOnly);
            SessionState.SetBool(Key+"MarshmallowOnly",marshmallowOnly);
            SessionState.SetBool(Key+"IronSuitOnly",ironSuitOnly);
            SessionState.SetBool(Key+"VisualOnly",false);
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(setup.Any(s=>s.isLoaded&&UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))throw new InvalidOperationException("Save edited scenes first.");
            SessionState.SetString(Key+"Scenes",JsonUtility.ToJson(new Scenes{values=setup}));
            SessionState.SetString(Key+"Choices",JsonUtility.ToJson(SpectatorRecordingPlan.Current()));
            SessionState.SetBool(Key+"Lobby",MatchSetup.FromLobby);SessionState.SetBool(Key+"Local",MatchSetup.StartLocalSession);SessionState.SetBool(Key,true);
            MatchSetup.FromLobby=false;MatchSetup.StartLocalSession=false;MatchSetup.MapId="castle";MatchSetup.ModeId="demo";MatchSetup.BotTarget=0;MatchSetup.HandCount=0;
            File.WriteAllText(Report,barbedBatOnly?"Barbed Bat focused Play Mode contact checks.\n":cosmeticWeaponsOnly?"Dagger and compact sidearm Play Mode checks.\n":"Arsenal expansion v9 (main editor): actual Game mechanics, categorized menus, expanded behaviors and captures.\n");
            EditorSceneManager.OpenScene(ProjectPath.Find("Game.unity"));EditorApplication.EnterPlaymode();
        }
        static void Log(string message,string trace,LogType kind)
        {
            if(Fusion.NetworkProjectConfig.Global.PeerMode==Fusion.NetworkProjectConfig.PeerModes.Multiple&&message.EndsWith("PeerModes.Multiple requires a scene to be set in StartGameArgs.Scene. Set a scene or disable Multiple Peer mode in the NetworkProjectConfig.",StringComparison.Ordinal)){File.AppendAllText(Report,"TRANSPORT FIXTURE NOTE: empty scene multiple-peer diagnostic; packet test, not replicated gameplay scene.\n");return;}
            if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert){errors++;File.AppendAllText(Report,"ERROR "+message+"\n");}
        }
        static void Check(bool pass,string label)
        {File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+label+"\n");checks++;if(!pass)throw new InvalidOperationException(label);}
        static IEnumerator Guard()
        {
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Test());
            while(stack.Count>0)
            {
                bool more=false;object value=null;Exception error=null;
                try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}catch(Exception e){error=e;}
                if(error!=null){failed=true;File.AppendAllText(Report,"FAIL "+error+"\n");break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}if(value is IEnumerator child){stack.Push(child);continue;}yield return value;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();completed=!failed;EditorApplication.ExitPlaymode();
        }
        static IEnumerator CheckMarshmallow(MatchModeRuntime runtime,CastleSurvivor player,CastleSurvivor target)
        {
            const EquipmentKind kind=EquipmentKind.FlamingMarshmallow;
            var origin=player.ViewPose.position+Vector3.forward*.4f;
            float cooldown=EquipmentSpecifications.Get(kind).cooldown+.08f;
            Check(runtime.GiveEquipment(kind)&&runtime.EquipmentFor(runtime.LocalActorId).drawn,
                "Flaming Marshmallow equips in the actual game");

            target.Place(new Vector3(150,10.02f,160),Quaternion.identity);
            Physics.SyncTransforms();
            int sequence=runtime.Frame.equipmentSequence;
            float health=target.Health;
            var air=new Pose(origin,Quaternion.LookRotation(Vector3.up,Vector3.forward));
            Check(runtime.RequestEquipmentAttack(air),"Air swing is accepted");
            var airActions=runtime.Frame.equipmentActions.Where(a=>a.id>sequence&&a.kind==kind).ToArray();
            Check(airActions.Count(a=>a.action==EquipmentAction.Shot)==1&&
                !airActions.Any(a=>a.action==EquipmentAction.Impact)&&target.Health==health,
                "Air swing announces the swing without a hit or damage");
            yield return new WaitForSeconds(cooldown);

            target.Place(new Vector3(150,10.02f,151),Quaternion.identity);
            Physics.SyncTransforms();
            var aim=new Pose(origin,Quaternion.LookRotation(target.transform.position+Vector3.up*.55f-origin));
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name="Marshmallow contact fixture block";
            block.layer=Layers.Block;
            block.transform.position=origin+aim.rotation*Vector3.forward*.38f;
            block.transform.localScale=new Vector3(.36f,.38f,.17f);
            Physics.SyncTransforms();
            try
            {
                sequence=runtime.Frame.equipmentSequence;
                Check(runtime.RequestEquipmentAttack(aim),"Block swing is accepted");
                var impact=runtime.Frame.equipmentActions.SingleOrDefault(a=>a.id>sequence&&a.kind==kind&&a.action==EquipmentAction.Impact);
                Check(impact!=null&&Vector3.Distance(impact.position,block.transform.position)<.30f&&target.Health==health,
                    "Block produces an impact at the collider and shields the player behind it");
            }
            finally
            {
                block.SetActive(false);
                Object.Destroy(block);
            }
            yield return new WaitForSeconds(cooldown);

            target.Place(new Vector3(150,10.02f,151),Quaternion.identity);
            Physics.SyncTransforms();
            sequence=runtime.Frame.equipmentSequence;
            Check(runtime.RequestEquipmentAttack(aim),"Player hit is accepted");
            var state=runtime.EquipmentFor(runtime.LocalActorId);
            Check(Mathf.Abs(health-target.Health-target.MaxHealth*.5f)<.05f&&
                runtime.Frame.equipmentActions.Any(a=>a.id>sequence&&a.kind==kind&&a.action==EquipmentAction.Impact)&&
                state.embedRemaining>0&&!string.IsNullOrEmpty(state.embeddedVictim),
                "Player impact deals half maximum health and publishes contact feedback");
        }

        static IEnumerator IronSuitPalette(CastlePlaySession session,MatchModeRuntime runtime,
            CastleSurvivor player,CastleSurvivor target)
        {
            var prefab=Resources.Load<CandyPlayerRig>("CandyPlayerVisual");
            Check(prefab!=null,"Production player rig resource is present");
            var rig=Object.Instantiate(prefab);
            rig.transform.position=new Vector3(150,10.05f,151);
            try
            {
                rig.SetAppearance(new AvatarAppearance { poweredArmor=true });
                var props=new MaterialPropertyBlock();
                var shadeType=typeof(CandyPlayerRig).Assembly.GetType("DeadlyJenga.CandyPlayers.CandyCosmeticShade");
                var shadeMethod=shadeType?.GetMethod("PlayerShade");
                Check(shadeMethod!=null,"The shared cosmetic shade resolver is available");
                void CheckPart(string name,Color expected,string property="_BaseColor")
                {
                    var renderer=rig.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(
                        r=>r.name=="Fitted Powered Iron Suit "+name && r.gameObject.activeInHierarchy);
                    Check(renderer!=null,"Powered suit part exists: "+name);
                    renderer.GetPropertyBlock(props);
                    var value=props.GetColor(property);
                    Check(Mathf.Abs(value.r-expected.r)<.002f&&Mathf.Abs(value.g-expected.g)<.002f&&
                        Mathf.Abs(value.b-expected.b)<.002f,"Palette matches "+name+" "+property);
                }
                foreach (var baseColor in new[] { new Color(.09f,.76f,.72f),new Color(.50f,.25f,.88f) })
                {
                    rig.SetTint(baseColor);
                    yield return null;
                    var dark=(Color)shadeMethod.Invoke(null,new object[]{baseColor,AvatarHairShade.Dark});
                    var light=(Color)shadeMethod.Invoke(null,new object[]{baseColor,AvatarHairShade.Light});
                    CheckPart("0 Armor",baseColor);
                    CheckPart("0 Dark",dark);
                    CheckPart("1 Silver",light);
                    CheckPart("0 Glow",light);
                    CheckPart("0 Glow",light*2f,"_EmissionColor");
                }
            }
            finally { Object.Destroy(rig.gameObject); }

            Check(runtime.GiveEquipment(EquipmentKind.PoweredArmor),"Production Powered Iron Suit equips");
            Check(runtime.RequestEquipment(false,false,false)&&runtime.RequestEquipment(true,false,false),
                "Production suit can holster and activate again");
            var feedback=session.GetComponent<DeadlyJenga.Presentation.CandyPoweredSuitFeedback>();
            Check(feedback!=null,"Powered suit presentation attached to the session");
            target.Place(player.transform.position+Vector3.right*8,Quaternion.identity);
            yield return null;
            yield return null;
            Check(feedback.GetComponentsInChildren<LineRenderer>().Any(line=>
                line.name=="Powered suit expanding energy ring"&&line.enabled),"Draw emits its ignition ring");

            Check(runtime.RequestThrust(true),"Production suit accepts held thrust");
            yield return null;
            yield return null;
            Check(feedback.ActiveSuits==1,"Thrust activates the live jet rig");
            Check(runtime.RequestThrust(false),"Production suit accepts thrust release");
            yield return null;
            yield return null;
            Check(feedback.ActiveSuits==0,"Releasing thrust stops the jets");

            var origin=player.ViewPose.position+Vector3.forward*.45f;
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name="Powered suit plasma impact fixture";
            cube.layer=Layers.Block;
            cube.transform.position=origin+Vector3.forward*4;
            cube.transform.localScale=Vector3.one*.85f;
            try
            {
                Physics.SyncTransforms();
                int sequence=runtime.Frame.equipmentSequence;
                Check(runtime.RequestEquipmentAttack(new Pose(origin,Quaternion.identity)),"Palm plasma attack accepted");
                bool seenShot=false,seenImpact=false,seenGlints=false;
                float deadline=Time.realtimeSinceStartup+5;
                while (Time.realtimeSinceStartup<deadline && !seenGlints)
                {
                    yield return null;
                    seenShot|=runtime.Frame.blaster?.shots.Length>0;
                    seenImpact|=runtime.Frame.equipmentActions.Any(a=>a.id>sequence&&
                        a.action==EquipmentAction.Impact&&a.kind==EquipmentKind.PoweredArmor);
                    seenGlints=feedback.GetComponentsInChildren<ParticleSystem>().Any(system=>
                        system.name=="Pooled Powered Iron Suit impact glints"&&system.particleCount>0);
                }
                Check(seenShot,"Palm shot travels through the production plasma path");
                Check(seenImpact,"Authoritative impact retains its Powered Iron Suit identity");
                Check(seenGlints,"Suit impact spawns the cyan glints");
            }
            finally { Object.Destroy(cube); }
        }

        static IEnumerator BarbedBatContacts(MatchModeRuntime runtime,CastleSurvivor player,CastleSurvivor target)
        {
            var kind=EquipmentKind.BarbedBat;
            var spec=EquipmentSpecifications.Get(kind);
            Check(runtime.GiveEquipment(kind),"Barbed Bat equips through the production path");
            target.Place(player.transform.position+Vector3.forward*1.05f,Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.25f);
            var emitters=Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            var sparks=emitters.FirstOrDefault(p=>p.name=="Barbed Bat contact sparks");
            var chips=emitters.FirstOrDefault(p=>p.name=="Barbed Bat candy chips");
            Check(sparks && chips,"Both pooled Barbed Bat contact emitters attached");
            var origin=player.ViewPose.position+Vector3.forward*.35f;
            var aim=Quaternion.LookRotation(target.transform.position+Vector3.up*.55f-origin);
            int before=runtime.Frame.equipmentSequence;
            float health=target.Health;
            Check(runtime.RequestEquipmentAttack(new Pose(origin,aim)),"Barbed Bat player swing accepted");
            yield return null;
            yield return null;
            Check(target.Health<health,"Player contact applies existing bat damage");
            Check(runtime.Frame.equipmentActions.Any(e=>e.id>before&&e.kind==kind&&e.action==EquipmentAction.Impact),
                "Player contact publishes an impact");
            Check(sparks.particleCount>0&&chips.particleCount==0,"Player contact emits sparks only");

            target.Place(player.transform.position+Vector3.right*8,Quaternion.identity);
            yield return new WaitForSeconds(spec.cooldown+.35f);
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name="Barbed Bat contact fixture block";
            block.layer=Layers.Block;
            block.transform.position=origin+Vector3.forward*(spec.range*.58f);
            block.transform.localScale=Vector3.one*.38f;
            Physics.SyncTransforms();
            before=runtime.Frame.equipmentSequence;
            Check(runtime.RequestEquipmentAttack(new Pose(origin,Quaternion.identity)),"Barbed Bat block swing accepted");
            yield return null;
            yield return null;
            Check(runtime.Frame.equipmentActions.Any(e=>e.id>before&&e.kind==kind&&e.action==EquipmentAction.Impact),
                "Block contact publishes an impact");
            Check(sparks.particleCount>0&&chips.particleCount>0,"Block contact emits sparks and chips");

            Object.Destroy(block);
            yield return new WaitForSeconds(spec.cooldown+.45f);
            Physics.SyncTransforms();
            before=runtime.Frame.equipmentSequence;
            Check(runtime.RequestEquipmentAttack(new Pose(origin,Quaternion.identity)),"Barbed Bat air swing accepted");
            yield return null;
            yield return null;
            Check(runtime.Frame.equipmentActions.Any(e=>e.id>before&&e.kind==kind&&e.action==EquipmentAction.Shot),
                "Air swing still publishes the swing event");
            Check(!runtime.Frame.equipmentActions.Any(e=>e.id>before&&e.kind==kind&&e.action==EquipmentAction.Impact),
                "Air swing publishes no impact");
            Check(sparks.particleCount==0&&chips.particleCount==0,"Air swing leaves both impact emitters clear");
        }

        static IEnumerator CosmeticWeapons(MatchModeRuntime runtime,CastleSurvivor player,CastleSurvivor target)
        {
            const EquipmentKind dagger=EquipmentKind.Dagger;
            var spec=EquipmentSpecifications.Get(dagger);
            target.Place(player.transform.position+Vector3.forward*1.07f,Quaternion.identity);
            Check(runtime.GiveEquipment(dagger),"The color-matched dagger equips");
            yield return new WaitForSeconds(.24f);
            CheckPalette(player,dagger,"CosmeticCandy");

            var origin=player.ViewPose.position+Vector3.forward*.32f;
            var aim=Quaternion.LookRotation(target.transform.position+Vector3.up*.55f-origin);
            float health=target.Health;
            Check(runtime.RequestEquipmentAttack(new Pose(origin,aim)),"Player thrust accepted");
            yield return null;
            yield return null;
            Check(target.Health<health,"Player contact deals dagger damage");
            CheckStrike("Visual - dagger player sparks",player.Color,"Player contact emits the attacker-colored sparks");

            target.Place(player.transform.position+Vector3.right*8,Quaternion.identity);
            yield return new WaitForSeconds(spec.cooldown+.15f);
            var testBlock=GameObject.CreatePrimitive(PrimitiveType.Cube);
            testBlock.name="Dagger contact fixture block";
            testBlock.layer=Layers.Block;
            testBlock.transform.position=origin+Vector3.forward*(spec.range*.67f);
            testBlock.transform.localScale=Vector3.one*.38f;
            Physics.SyncTransforms();
            try
            {
                Check(runtime.RequestEquipmentAttack(new Pose(origin,Quaternion.identity)),"Block thrust accepted");
                yield return null;
                yield return null;
                CheckStrike("Visual - dagger block sparks",player.Color,"Block contact emits the attacker-colored block sparks");
            }
            finally
            {
                testBlock.SetActive(false);
                Object.Destroy(testBlock);
            }

            yield return new WaitForSeconds(spec.cooldown+.15f);
            int first=runtime.Frame.equipmentSequence;
            Check(runtime.RequestEquipmentAttack(new Pose(origin,Quaternion.identity)),"Air thrust accepted");
            yield return null;
            yield return null;
            CheckStrike("Visual - dagger trail",Color.Lerp(player.Color,Color.white,.18f),"Air thrust shows the colored trail");
            Check(!runtime.Frame.equipmentActions.Any(e=>e.id>first&&e.kind==dagger&&e.action==EquipmentAction.Impact),
                "Air thrust produces no contact event");

            Check(runtime.GiveEquipment(EquipmentKind.StockPistol),"Legacy stock pistol remains a distinct weapon");
            yield return null;
            var legacy=player.transform.Find("Visual - "+EquipmentKind.StockPistol);
            Check(legacy&&legacy.GetComponentsInChildren<MeshRenderer>(true).All(r=>!r.sharedMaterial||
                  !r.sharedMaterial.name.StartsWith("Foundry Cosmetic",StringComparison.Ordinal)),
                  "Legacy stock pistol retains its model and palette");
            const EquipmentKind pistol=EquipmentKind.CompactSidearm;
            Check(runtime.GiveEquipment(pistol),"Compact sidearm equips under its own weapon identity");
            yield return new WaitForSeconds(.26f);
            CheckPalette(player,pistol,"SidearmGlossLight");
            var rig=player.GetComponent<CandyPlayerAvatar>().Rig;
            var previous=rig.BaseAppearanceColor;
            try
            {
                rig.SetTint(new Color(.16f,.63f,.92f));
                yield return null;
                yield return null;
                CheckPalette(player,pistol,"SidearmGlossLight");
            }
            finally
            {
                rig.SetTint(previous);
            }
            target.Place(player.transform.position+Vector3.forward*5,Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.23f);
            aim=Quaternion.LookRotation(target.transform.position+Vector3.up*.55f-origin);
            health=target.Health;
            Check(runtime.RequestEquipmentAttack(new Pose(origin,aim)),"Compact sidearm fires through the existing attack path");
            Check(target.Health<health,"Compact sidearm damages the player target");
            Check(runtime.EquipmentFor(runtime.LocalActorId).rounds==EquipmentSpecifications.Get(pistol).magazine-1,
                "Sidearm consumes a round from the existing magazine");
        }

        static void CheckStrike(string name,Color expected,string label)
        {
            var line=Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)
                .FirstOrDefault(item=>item.name==name&&item.enabled);
            Check(line&&SameRgb(line.startColor,expected),label);
        }

        static void CheckPalette(CastleSurvivor player,EquipmentKind kind,string lightFinish)
        {
            var model=player.transform.Find("Visual - "+kind);
            Check(model,kind+" visible model exists");
            var parts=model.GetComponentsInChildren<MeshRenderer>(true);
            var block=new MaterialPropertyBlock();
            Color Read(string suffix)
            {
                var part=parts.FirstOrDefault(item=>item.sharedMaterial&&item.sharedMaterial.name=="Foundry "+suffix);
                Check(part,kind+" has the "+suffix+" region");
                part.GetPropertyBlock(block);
                return block.GetColor("_BaseColor");
            }
            Color light=Read(lightFinish),dark=Read("CosmeticDark"),normal=Read(kind==EquipmentKind.CompactSidearm?"SidearmGlossBase":"CosmeticBase");
            var expected=player.GetComponent<CandyPlayerAvatar>().Rig.BaseAppearanceColor;
            var shadeType=typeof(CandyWeaponVisual).Assembly.GetType("DeadlyJenga.CandyPlayers.CandyCosmeticShade");
            var palette=shadeType.GetMethod("PlayerShade");
            var expectedLight=(Color)palette.Invoke(null,new object[]{expected,AvatarHairShade.Light});
            var expectedDark=(Color)palette.Invoke(null,new object[]{expected,AvatarHairShade.Dark});
            Check(SameRgb(normal,expected)&&SameRgb(light,expectedLight)&&SameRgb(dark,expectedDark),
                  kind+" applies the exact wardrobe shades for "+expected);
        }

        static bool SameRgb(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)<.02f;

        static IEnumerator CheckRocketCandy(MatchModeRuntime runtime,CastleSurvivor player)
        {
            const EquipmentKind kind=EquipmentKind.RocketLauncher;
            string actor=runtime.LocalActorId;
            Check(runtime.GiveEquipment(kind),"Single-load RPG equips");
            var start=runtime.EquipmentFor(actor);
            Check(start.rounds==1&&start.rocketCandyVariant>=1&&start.rocketCandyVariant<=RocketCandy.VariantCount,
                "The RPG begins with one colored lollipop");
            float deadline=Time.realtimeSinceStartup+2;
            GameObject loaded=null;
            while(!loaded&&Time.realtimeSinceStartup<deadline)
            {
                yield return null;
                loaded=GameObject.Find("Front-loaded lollipop");
            }
            Check(loaded,"A lollipop is visible in the muzzle");
            var tint=new MaterialPropertyBlock();
            var candy=loaded.transform.Find("Round lollipop").GetComponent<MeshRenderer>();
            candy.GetPropertyBlock(tint);
            Check(tint.GetColor("_BaseColor")==RocketCandy.ColorFor(start.rocketCandyVariant),
                "The equipped avatar shows the authoritative loaded color");
            int first=runtime.Frame.equipmentSequence;
            var muzzle=new Pose(player.ViewPose.position+Vector3.forward*.4f,
                Quaternion.LookRotation(new Vector3(0,.12f,1)));
            var target=GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name="Rocket candy contact fixture";
            target.layer=Layers.Block;
            target.transform.position=muzzle.position+muzzle.rotation*Vector3.forward*7;
            target.transform.localScale=new Vector3(1.4f,1.5f,.42f);
            Physics.SyncTransforms();
            Check(runtime.RequestEquipmentAttack(muzzle),"Loaded lollipop fires");
            var incoming=runtime.EquipmentFor(actor);
            var shot=runtime.Frame.weaponProjectiles.SingleOrDefault(p=>p.kind==kind&&p.owner==actor);
            var cue=runtime.Frame.equipmentActions.SingleOrDefault(a=>a.id>first&&a.kind==kind&&a.action==EquipmentAction.Shot);
            Check(shot!=null&&cue!=null&&shot.rocketCandyVariant==start.rocketCandyVariant&&
                cue.rocketCandyVariant==start.rocketCandyVariant,"Projectile, muzzle flash and rear exhaust use the fired color");
            float showUntil=Time.realtimeSinceStartup+.35f;
            TrailRenderer exhaust=null;
            while(!exhaust&&Time.realtimeSinceStartup<showUntil)
            {
                yield return null;
                var tip=GameObject.Find("Lollipop stick-tip exhaust");
                if(tip)exhaust=tip.GetComponent<TrailRenderer>();
            }
            Check(exhaust&&Mathf.Abs(exhaust.startColor.b-RocketCandy.ColorFor(start.rocketCandyVariant).b)<.005f&&
                Mathf.Abs(exhaust.startColor.r-RocketCandy.ColorFor(start.rocketCandyVariant).r)<.005f,
                "Visible propulsion starts at the lollipop stick in the fired color");
            float impactUntil=Time.time+.9f;
            EquipmentActionFrame impact=null;
            while(impact==null&&Time.time<impactUntil)
            {
                yield return null;
                impact=runtime.Frame.equipmentActions.FirstOrDefault(a=>a.id>first&&a.kind==kind&&a.action==EquipmentAction.Impact);
            }
            Check(impact!=null&&impact.rocketCandyVariant==start.rocketCandyVariant&&
                Vector3.Distance(impact.position,target.transform.position)<.65f,"Block detonation uses the same fired color");
            target.SetActive(false);
            Object.Destroy(target);
            Check(incoming.rounds==0&&incoming.reloadRemaining>0&&
                incoming.rocketCandyVariant!=start.rocketCandyVariant,"Shot empties the chamber and selects a different reload color");
            Check(!runtime.RequestEquipmentAttack(muzzle),"Second shot is blocked by the reload");
            float remaining=runtime.EquipmentFor(actor).reloadRemaining;
            yield return new WaitForSeconds(Mathf.Max(0,remaining-1.58f));
            Check(!runtime.RequestEquipmentAttack(muzzle),"Cooldown ending does not skip the reload");
            candy.GetPropertyBlock(tint);
            Check(loaded.activeInHierarchy&&loaded.transform.localPosition.z>1.22f&&
                tint.GetColor("_BaseColor")==RocketCandy.ColorFor(incoming.rocketCandyVariant),
                "The replacement color is already visible as the avatar brings the round to the muzzle");
            deadline=Time.realtimeSinceStartup+6;
            while(runtime.EquipmentFor(actor).reloadRemaining>0&&Time.realtimeSinceStartup<deadline)
                yield return null;
            var reloaded=runtime.EquipmentFor(actor);
            Check(reloaded.rounds==1&&reloaded.reloadRemaining==0&&
                reloaded.rocketCandyVariant==incoming.rocketCandyVariant,"One new lollipop seats with the chosen color");
            Check(ModeFrameValidation.Frame(JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson()),Services.World.Blocks.Count),
                "Snapshot with a colored lollipop passes validation");
            Check(runtime.RequestEquipmentAttack(muzzle),"The newly loaded lollipop can fire");
        }

        static IEnumerator SawStaffContacts(MatchModeRuntime runtime, CastleSurvivor player, CastleSurvivor target)
        {
            const EquipmentKind kind = EquipmentKind.SawStaff;
            var spec = EquipmentSpecifications.Get(kind);
            Check(runtime.GiveEquipment(kind) && runtime.EquipmentFor(runtime.LocalActorId).drawn,
                "Saw Wheel Staff equips and draws");

            target.Place(player.transform.position + Vector3.right * 8, Quaternion.identity);
            yield return new WaitForSeconds(.25f);
            var origin = player.ViewPose.position + Vector3.forward * .35f;
            var forward = new Pose(origin, Quaternion.identity);
            float health = target.Health;
            int before = runtime.Frame.equipmentSequence;
            Check(runtime.RequestEquipmentAttack(forward), "Empty-air swing accepted");
            var events = SawStaffEvents(runtime, before);
            Check(events.Length == 1 && events[0].action == EquipmentAction.Shot &&
                events[0].endpoints.Length == 1 &&
                Vector3.Distance(events[0].endpoints[0], origin + Vector3.forward * spec.range) < .01f,
                "Air swing reaches full range and emits no impact");
            Check(Mathf.Approximately(target.Health, health), "Air swing leaves the survivor unharmed");

            yield return new WaitForSeconds(spec.cooldown + .12f);
            var original = Services.World.Blocks.FirstOrDefault(body =>
                body && body.TryGetComponent<CastleBlock>(out _) && body.GetComponent<BoxCollider>().enabled);
            Check(original, "Game scene provides a native CastleBlock for the fixture");
            var block = Object.Instantiate(original.gameObject, origin + Vector3.forward * (spec.range * .60f),
                Quaternion.identity);
            try
            {
                var body = block.GetComponent<Rigidbody>();
                body.isKinematic = true;
                var collider = block.GetComponent<BoxCollider>();
                Physics.SyncTransforms();
                block.transform.position += origin + Vector3.forward * (spec.range * .60f) - collider.bounds.center;
                Physics.SyncTransforms();
                Check(collider.enabled && block.layer == Layers.Block, "Native block collider is enabled on the Block layer");
                before = runtime.Frame.equipmentSequence;
                Check(runtime.RequestEquipmentAttack(forward), "Block swing accepted");
                events = SawStaffEvents(runtime, before);
                Check(events.Length == 2 && events[0].action == EquipmentAction.Shot &&
                    events[1].action == EquipmentAction.Impact &&
                    Vector3.Distance(collider.ClosestPoint(events[1].position), events[1].position) < .03f,
                    "Block swing emits one impact on the native collider");
                Check(Mathf.Approximately(target.Health, health), "Block swing leaves the off-axis survivor unharmed");
            }
            finally
            {
                block.SetActive(false);
                Object.Destroy(block);
            }

            yield return new WaitForSeconds(spec.cooldown + .12f);
            target.Place(player.transform.position + Vector3.forward * 1.05f, Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.25f);
            origin = player.ViewPose.position + Vector3.forward * .35f;
            var playerSwing = new Pose(origin,
                Quaternion.LookRotation(target.transform.position + Vector3.up * .55f - origin));
            health = target.Health;
            before = runtime.Frame.equipmentSequence;
            Check(runtime.RequestEquipmentAttack(playerSwing), "Survivor swing accepted");
            events = SawStaffEvents(runtime, before);
            Check(events.Length == 2 && events[0].action == EquipmentAction.Shot &&
                events[1].action == EquipmentAction.Impact, "Survivor swing emits a single impact");
            float damaged = target.Health;
            float deadline = Time.realtimeSinceStartup + 1.5f;
            while (damaged >= health && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                damaged = target.Health;
            }
            Check(damaged < health, "Production survivor takes damage with Demo deaths disabled");
            File.AppendAllText(Report, $"Survivor health {health:F1} -> {damaged:F1}
");
        }

        static EquipmentActionFrame[] SawStaffEvents(MatchModeRuntime runtime, int after)
        {
            return runtime.Frame.equipmentActions.Where(action => action.id > after &&
                action.actor == runtime.LocalActorId && action.kind == EquipmentKind.SawStaff).ToArray();
        }

        static IEnumerator Test()
        {
            var session=Object.FindFirstObjectByType<CastlePlaySession>();float until=Time.realtimeSinceStartup+30;
            while((session.Player==null||session.Modes==null)&&Time.realtimeSinceStartup<until)yield return null;
            Check(session.Player!=null&&session.Modes!=null,"Actual Game initializes");session.SetRole(false);session.SetMenu(false);session.SurvivorBots.AutoFill=false;session.SurvivorBots.Clear();session.HandBots.Clear();if(session.Social!=null)session.Social.VoiceEnabled=false;
            var runtime=session.Modes;var player=session.Survivor();var rules=ModeSimulation.Defaults(GameModeKind.Demo);rules.countdownSeconds=0;rules.healthEnabled=true;rules.deathEnabled=false;rules.fallDamage=false;rules.punchEnabled=true;rules.island=IslandRule.Walkable;runtime.Configure(rules);runtime.StartRound();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Arsenal native collision fixture";floor.layer=Layers.Surface;floor.transform.position=new Vector3(150,9,150);floor.transform.localScale=new Vector3(70,2,70);
            player.Place(new Vector3(150,10.02f,150),Quaternion.identity);player.InputSuspended=true;
            var created=session.SurvivorBots.SpawnOne();Check(created,"Target bot spawns");var target=created.GetComponent<CastleSurvivor>();target.GetComponent<CastleBotBrain>().enabled=false;target.InputSuspended=true;
            yield return new WaitForSeconds(.4f);
            if(SessionState.GetBool(Key+"MarshmallowOnly",false))
            {
                yield return CheckMarshmallow(runtime,player,target);
                yield break;
            }
            if(SessionState.GetBool(Key+"IronSuitOnly",false))
            {
                yield return IronSuitPalette(session,runtime,player,target);
                yield break;
            }
            if(SessionState.GetBool(Key+"BarbedBatOnly",false))
            {
                yield return BarbedBatContacts(runtime,player,target);
                yield break;
            }
            if(SessionState.GetBool(Key+"CosmeticWeaponsOnly",false))
            {
                yield return CosmeticWeapons(runtime,player,target);
                yield break;
            }
            if(SessionState.GetBool(Key+"RocketCandyOnly",false))
            {
                yield return CheckRocketCandy(runtime,player);
                yield break;
            }
            if (SawStaffOnly)
            {
                yield return SawStaffContacts(runtime,player,target);
                yield break;
            }
            Check(EquipmentSpecifications.All.Length==Enum.GetValues(typeof(EquipmentKind)).Length,"Every serialized equipment kind has one shared specification");
            Check((int)EquipmentKind.PoweredArmor==8&&(int)EquipmentKind.Blaster==3,"Existing equipment wire identities stay unchanged");
            foreach(EquipmentKind kind in Enum.GetValues(typeof(EquipmentKind)))
            {
                if(kind<=EquipmentKind.PoweredArmor||EquipmentSpecifications.Get(kind).power)continue;
                player.Place(new Vector3(150,10.02f,150),Quaternion.identity);player.InputSuspended=true;
                target.Place(new Vector3(150,10.02f,EquipmentSpecifications.Get(kind).melee?151:kind==EquipmentKind.GrenadeLauncher?155:158),Quaternion.identity);target.InputSuspended=true;Physics.SyncTransforms();yield return new WaitForSeconds(.24f);
                Check(runtime.GiveEquipment(kind),"Demo grant equips "+kind);Check(runtime.EquipmentFor(runtime.LocalActorId).drawn,"Granted weapon is drawn: "+kind);
                float before=target.Health;var origin=player.ViewPose.position+Vector3.forward*.4f;var pose=new Pose(origin,Quaternion.LookRotation(target.transform.position+Vector3.up*.55f-origin));
                if(EquipmentSpecifications.Get(kind).charge>0)
                {
                    var held=(IHeldEquipmentRuntime)runtime;
                    Check(held.RequestEquipmentTrigger(true,pose),"Held trigger accepted for "+kind);
                    float start=Time.time;while(Time.time-start<EquipmentSpecifications.Get(kind).charge-.12f){held.RequestEquipmentTrigger(true,pose);yield return new WaitForSeconds(.07f);}
                    Check(target.Health==before,"No early damage before spin-up: "+kind);
                    while(Time.time-start<EquipmentSpecifications.Get(kind).charge+(kind==EquipmentKind.ChainGun?.9f:.3f)){held.RequestEquipmentTrigger(true,pose);yield return new WaitForSeconds(.07f);}held.RequestEquipmentTrigger(false,pose);
                }
                else if(EquipmentSpecifications.Get(kind).automatic)
                {var held=(IHeldEquipmentRuntime)runtime;for(int i=0;i<4;i++){held.RequestEquipmentTrigger(true,pose);yield return new WaitForSeconds(.08f);}held.RequestEquipmentTrigger(false,pose);}
                else {Check(runtime.RequestEquipmentAttack(pose),"Authoritative attack accepted for "+kind);if(EquipmentSpecifications.Get(kind).projectileSpeed>0)Check(runtime.Frame.weaponProjectiles.Any(p=>p.kind==kind),"Travelling projectile publishes immediately for "+kind);}
                float end=Time.realtimeSinceStartup+3;while(target.Health>=before&&Time.realtimeSinceStartup<end)yield return null;
                Check(target.Health<before,"Physical target takes damage from "+kind+(target.Health<before?"":"; shooter="+player.transform.position+" target="+target.transform.position+" state="+JsonUtility.ToJson(runtime.EquipmentFor(runtime.LocalActorId))));
                Check(ModeFrameValidation.Frame(JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson()),Services.World.Blocks.Count),"Snapshot validates after "+kind);
                yield return new WaitForSeconds(.25f);
            }
            target.Place(new Vector3(150,10.02f,158),Quaternion.identity);target.InputSuspended=true;yield return new WaitForSeconds(.25f);
            var muzzle=new Pose(player.ViewPose.position+Vector3.forward*.4f,Quaternion.LookRotation(target.transform.position+Vector3.up*.55f-player.ViewPose.position-Vector3.forward*.4f));var trigger=(IHeldEquipmentRuntime)runtime;
            Check(runtime.GiveEquipment(EquipmentKind.StockPistol)&&runtime.RequestEquipment(true,false,true),"Stock pistol switches to burst mode");yield return new WaitForSeconds(.45f);int first=runtime.Frame.equipmentSequence;
            for(int i=0;i<7;i++){trigger.RequestEquipmentTrigger(true,muzzle);yield return new WaitForSeconds(.08f);}
            Check(runtime.Frame.equipmentActions.Count(a=>a.id>first&&a.kind==EquipmentKind.StockPistol&&a.action==EquipmentAction.Shot)==3,"One held burst fires exactly three rounds");trigger.RequestEquipmentTrigger(false,muzzle);
            target.Place(new Vector3(150,10.02f,156),Quaternion.identity);target.InputSuspended=true;yield return new WaitForSeconds(.25f);
            Check(runtime.GiveEquipment(EquipmentKind.Chainsaw)&&runtime.RequestEquipment(true,false,true),"Chainsaw switches to wheel launcher");
            yield return new WaitForSeconds(.45f);
            muzzle=new Pose(player.ViewPose.position+Vector3.forward*.4f,Quaternion.LookRotation(target.transform.position+Vector3.up*.55f-player.ViewPose.position-Vector3.forward*.4f));
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name="Wheel lodge test surface";
            wall.layer=Layers.Surface;
            wall.transform.position=muzzle.position+muzzle.rotation*Vector3.forward*3;
            wall.transform.localScale=new Vector3(2,2,.3f);
            Physics.SyncTransforms();
            first=runtime.Frame.equipmentSequence;
            float chargeStart=Time.time;
            bool sawFlying=false,sawLodged=false,earlyWheel=false;
            while(Time.time-chargeStart<1.48f)
            {
                trigger.RequestEquipmentTrigger(true,muzzle);
                yield return new WaitForSeconds(.05f);
                sawFlying|=runtime.Frame.weaponProjectiles.Any(p=>p.kind==EquipmentKind.Chainsaw);
                sawLodged|=runtime.Frame.equipmentActions.Any(a=>a.id>first&&a.kind==EquipmentKind.Chainsaw&&
                    a.action==EquipmentAction.Impact&&a.shotgun);
                if(Time.time-chargeStart<.53f)
                    earlyWheel|=sawFlying;
            }
            trigger.RequestEquipmentTrigger(false,muzzle);
            int wheels=runtime.Frame.equipmentActions.Count(a=>a.id>first&&a.kind==EquipmentKind.Chainsaw&&a.action==EquipmentAction.Shot);
            Check(sawFlying&&wheels>=2,"Held trigger launches multiple wheels after one spin-up");
            Check(sawLodged,"A world collision publishes a wheel lodging cue");
            Check(!earlyWheel,"Wheel launcher waits for its initial spin-up");
            var lodged=Object.FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None)
                .FirstOrDefault(t=>t.name=="Visual - Chainsaw projectile"&&!t.emitting);
            Check(lodged!=null,"A wheel remains visible after impact");
            var lodgedPosition=lodged?lodged.transform.position:Vector3.zero;
            yield return new WaitForSeconds(1);
            Check(lodged&&lodged.gameObject.activeInHierarchy&&
                Vector3.Distance(lodged.transform.position,lodgedPosition)<.03f,"The wheel stays seated on the surface");
            bool fractured=false;
            float breakDeadline=Time.time+3.4f;
            while(!fractured&&Time.time<breakDeadline)
            {
                var fragments=GameObject.Find("Pooled wheel fragments");
                fractured=fragments&&fragments.GetComponent<ParticleSystem>().particleCount>0;
                yield return null;
            }
            Check(fractured,"A dropped wheel emits fragments");
            wall.SetActive(false);
            Object.Destroy(wall);
            Check(runtime.GiveEquipment(EquipmentKind.LaserAxe)&&runtime.RequestEquipment(true,false,true),"Axe switches to full-width laser");yield return new WaitForSeconds(.45f);float health=target.Health;Check(runtime.RequestEquipmentAttack(muzzle),"Axe laser attack accepted");Check(target.Health<health,"Axe beam damages a physical target at range");
            target.Place(new Vector3(150,10.02f,156),Quaternion.identity);target.InputSuspended=true;yield return new WaitForSeconds(.25f);
            Check(runtime.GiveEquipment(EquipmentKind.WristLaser),"Oxblood wrist laser equips");
            health=target.Health;
            for(int i=0;i<4;i++)
            {
                trigger.RequestEquipmentTrigger(true,muzzle);
                yield return new WaitForSeconds(.08f);
            }
            var beam=Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)
                .FirstOrDefault(line=>line.name=="Oxblood beam core"&&line.enabled);
            Check(beam&&beam.startColor.b>beam.startColor.r,"Held Oxblood laser displays a continuous cyan core");
            trigger.RequestEquipmentTrigger(false,muzzle);
            float stopped=target.Health;
            yield return new WaitForSeconds(.19f);
            Check(stopped<health&&target.Health==stopped,"Continuous beam deals repeated damage and stops on release");
            Check(beam&&!beam.enabled,"Oxblood beam fades promptly on release");
            rules.defaultDagger=true;runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.3f);
            player.Place(new Vector3(150,10.02f,150),Quaternion.identity);player.InputSuspended=true;target.Place(new Vector3(150,10.02f,151),Quaternion.identity);target.InputSuspended=true;yield return new WaitForSeconds(.25f);
            Check(runtime.EquipmentFor(runtime.LocalActorId).kind==EquipmentKind.Dagger&&runtime.EquipmentFor(runtime.LocalActorId).builtIn,"Configured mode supplies an always-ready fallback dagger");
            var knifeOrigin=player.ViewPose.position+Vector3.forward*.4f;var knifePose=new Pose(knifeOrigin,Quaternion.LookRotation(target.transform.position+Vector3.up*.55f-knifeOrigin));float knifeHealth=target.Health;
            Check(runtime.RequestEquipmentAttack(knifePose)&&Mathf.Abs(knifeHealth-target.Health-EquipmentSpecifications.Get(EquipmentKind.Dagger).damage)<.01f,"Fallback dagger inflicts its stronger PvP melee damage");
            Check(runtime.RequestEquipment(true,true,false)&&runtime.EquipmentFor(runtime.LocalActorId).leftHanded,"Fallback dagger respects dominant-hand selection");runtime.RequestEquipment(true,false,false);
            Check(runtime.GiveEquipment(EquipmentKind.Speed),"Speed power equips beside fallback dagger");yield return new WaitForSeconds(.15f);
            Check(runtime.EquipmentFor(runtime.LocalActorId).kind==EquipmentKind.Dagger&&runtime.EquipmentFor(runtime.LocalActorId).activePower==EquipmentKind.Speed&&player.PowerupSpeedMultiplier==2,"Movement power keeps the fallback blade visible and applies its real effect");
            var power=runtime.RegisterPickup(new PickupSource{Kind=EquipmentKind.Jump,Position=player.transform.position+Vector3.forward*.5f+Vector3.up*.3f});
            Check(!runtime.RequestPickup(power,false)&&runtime.RequestPickup(power,true),"A carried timed power still requires normal swap confirmation");yield return new WaitForSeconds(.15f);
            Check(runtime.EquipmentFor(runtime.LocalActorId).activePower==EquipmentKind.Jump&&Mathf.Approximately(player.EffectiveJumpHeight,player.jumpHeight*3),"Jump power coexists with the blade");
            var timed=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());Check(ModeFrameValidation.Frame(timed,Services.World.Blocks.Count),"Combined fallback and timed-power snapshot validates");
            typeof(MatchModeRuntime).GetMethod("RestoreEquipment",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(runtime,new object[]{timed});
            Check(runtime.EquipmentFor(runtime.LocalActorId).activePower==EquipmentKind.Jump,"Authority restore keeps the timed power beneath the fallback weapon");yield return new WaitForSeconds(1.2f);
            Check(runtime.EquipmentFor(runtime.LocalActorId).kind==EquipmentKind.Dagger&&runtime.EquipmentFor(runtime.LocalActorId).activePower==EquipmentKind.None,"Timed-power expiry returns to the ordinary fallback blade");
            int pickups=runtime.Frame.pickups.Length;Check(runtime.GiveEquipment(EquipmentKind.RocketLauncher),"Pickup weapon replaces fallback dagger");Check(runtime.Frame.pickups.Length==pickups,"Fallback dagger does not generate a ground drop");
            Check(new[]{GameModeKind.CaptureFlag,GameModeKind.KingOfHill,GameModeKind.BlockControl,GameModeKind.TeamTickets}.All(m=>ModeSimulation.Defaults(m).defaultDagger)&&!ModeSimulation.Defaults(GameModeKind.MurderMystery).defaultDagger,"PvP defaults use daggers while Mystery keeps role weapons");
            rules.defaultDagger=false;runtime.Configure(rules);runtime.StartRound();yield return new WaitForSeconds(.3f);player.Place(new Vector3(150,10.02f,150),Quaternion.identity);player.InputSuspended=true;
            Check(runtime.GiveEquipment(EquipmentKind.NeedleDrill),"Snapshot fixture equips charged weapon");trigger.RequestEquipmentTrigger(true,muzzle);yield return new WaitForSeconds(.12f);
            var saved=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());Check(saved.equipment.Any(e=>e.kind==EquipmentKind.NeedleDrill&&e.triggerHeld&&e.charge>0&&e.charge<1),"Charge progress is replicated as finite bounded data");trigger.RequestEquipmentTrigger(false,muzzle);
            saved.equipment.First(e=>e.kind==EquipmentKind.NeedleDrill).charge=float.NaN;Check(!ModeFrameValidation.Frame(saved,Services.World.Blocks.Count),"Malformed charge snapshot is rejected");
            Check(runtime.SpawnSettings.types.Length==EquipmentSpecifications.All.Length-1,"Random spawner exposes every item weight");
            BotObjectiveContractChecks.Run();Check(BotObjectiveContractChecks.LastPassed,"Existing objective contracts pass");
            Check(runtime.GiveEquipment(EquipmentKind.RocketLauncher),"Transport fixture equips rocket launcher");
            var sky=new Pose(player.ViewPose.position+Vector3.up*.2f,Quaternion.LookRotation((Vector3.forward+Vector3.up).normalized));Check(runtime.RequestEquipmentAttack(sky),"Transport fixture launches a rocket");
            var snapshot=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());Check(snapshot.weaponProjectiles.Length>0,"Immediate snapshot includes an airborne rocket");
            typeof(MatchModeRuntime).GetMethod("RestoreEquipment",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(runtime,new object[]{snapshot});yield return null;Check(runtime.Frame.weaponProjectiles.Any(p=>p.kind==EquipmentKind.RocketLauncher),"Authority restore retains airborne rocket state");
            Check(runtime.GiveEquipment(EquipmentKind.PoweredArmor)&&runtime.RequestThrust(true),"Transport fixture equips active suit beside airborne rocket");var active=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());runtime.RequestThrust(false);var inactive=JsonUtility.FromJson<MatchFrame>(runtime.CaptureFrameJson());
            yield return PoweredSuitNetworkChecks.Run(runtime,active,inactive,Check);
            yield return Gallery(player.GetComponent<CandyPlayerAvatar>().Rig.style.material);
            var hud=session.GetComponent<CandyDemoHud>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            Check(DemoMenuSections.Tabs.Length==4&&DemoMenuSections.Sections.SelectMany(x=>x).Contains("Disasters")&&DemoMenuSections.Sections.SelectMany(x=>x).Contains("Powerup spawning"),"Four main tabs retain disasters and niche powerup controls");
            for(int i=0;i<4;i++){typeof(CandyDemoHud).GetField("page",flags).SetValue(hud,i);typeof(CandyDemoHud).GetField("subsection",flags).SetValue(hud,0);session.SetMenu(true);yield return new WaitForSeconds(.15f);yield return new WaitForEndOfFrame();var shot=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(ProjectPath.Report("Arsenal-Menu-"+i+".png"),shot.EncodeToPNG());Object.Destroy(shot);}
            var menu=Object.FindFirstObjectByType<CastleWorldMenu>(FindObjectsInactive.Include);Check(menu,"VR menu exists");menu.Show(true,session.ActiveCamera);typeof(CastleWorldMenu).GetField("menuCategory",flags).SetValue(menu,-1);typeof(CastleWorldMenu).GetMethod("PointOrganizedMenu",flags).Invoke(menu,new object[]{session.ActiveCamera,false});var texts=menu.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t=>t.text).ToArray();Check(DemoMenuSections.Tabs.All(t=>texts.Contains(t)),"VR root shows exactly the four requested main categories");menu.Show(false,session.ActiveCamera);
            Check(Shader.Find("DeadlyJenga/Pickup Cloud")&&!UnityEditor.ShaderUtil.ShaderHasError(Shader.Find("DeadlyJenga/Pickup Cloud")),"Static-cloud pickup shader compiles");
            session.SetMenu(false);yield return ExpandedBehaviourChecks.Run(session,Check);
            Check(errors==0,"No unexpected runtime errors");Object.Destroy(floor);
        }
        public static IEnumerator CaptureModels(CastlePlaySession session,Action<bool,string> verify)=>Gallery(session.Survivor().GetComponent<CandyPlayerAvatar>().Rig.style.material,verify);
        static IEnumerator Gallery(Material material,Action<bool,string> verify=null)
        {
            var validate=verify??new Action<bool,string>(Check);
            var root=new GameObject("Arsenal visual inspection gallery");var origin=new Vector3(2000,1000,2000);int index=0;
            foreach(var offset in new[]{new Vector3(-3,3,-3),new Vector3(4,1,-2),new Vector3(0,-2,-3)}){var lamp=new GameObject("Gallery softbox",typeof(Light));lamp.transform.SetParent(root.transform,false);lamp.transform.position=origin+offset;var light=lamp.GetComponent<Light>();light.type=LightType.Point;light.range=20;light.intensity=offset.y>2?24:12;light.color=offset.x>0?new Color(.5f,.8f,1):Color.white;light.shadows=LightShadows.None;}
            foreach(EquipmentKind kind in Enum.GetValues(typeof(EquipmentKind)))
            {
                if(kind<=EquipmentKind.PoweredArmor||EquipmentSpecifications.Get(kind).power)continue;
                var holder=new GameObject(kind.ToString());holder.transform.SetParent(root.transform,false);var position=origin+new Vector3((index%5-2)*1.9f,(1-index/5)*1.6f,0);var rotation=Quaternion.Euler(-15,-55,0);
                var frame=new CandyAvatarFrame{scale=1,feet=position-Vector3.up,head=position+Vector3.up*.5f,headRotation=rotation,left=new CandyHandPose{position=position+Vector3.left*.2f,rotation=rotation},right=new CandyHandPose{position=position,rotation=rotation}};
                var visual=new CandyWeaponVisual(holder.transform,material);visual.Present(new EquipmentState{kind=kind,drawn=true,triggerHeld=true,charge=.5f},frame,true);
                validate(holder.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)>2&&holder.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)<=10&&holder.GetComponentsInChildren<Collider>().Length==0,"Detailed collider-free model: "+kind);
                validate(EquipmentModelData.Parts.TryGetValue(kind,out var parts)&&parts.Length>2,"Live pickup geometry catalog includes "+kind);
                var label=new GameObject(kind+" label",typeof(TextMesh));label.transform.SetParent(root.transform,false);label.transform.position=position+new Vector3(0,-.50f,-1);var text=label.GetComponent<TextMesh>();text.text=EquipmentSpecifications.Get(kind).name.Replace(" / ","\n");text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.characterSize=.035f;text.fontSize=44;text.color=Color.white;index++;
            }
            var go=new GameObject("Gallery camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.transform.position=origin+Vector3.back*12;camera.transform.rotation=Quaternion.identity;camera.orthographic=true;camera.orthographicSize=3.2f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.12f,.18f);camera.farClipPlane=30;camera.nearClipPlane=.01f;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
            var rt=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;camera.targetTexture=rt;yield return null;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();File.WriteAllBytes(ProjectPath.Report("Arsenal-Gallery.png"),pixels.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(pixels);
            foreach(var text in root.GetComponentsInChildren<TextMesh>())text.gameObject.SetActive(false);
            foreach(var kind in new[]{EquipmentKind.RocketLauncher,EquipmentKind.StockPistol,EquipmentKind.NeedleDrill,EquipmentKind.LaserAxe})
            {
                var holder=root.transform.Find(kind.ToString());var renderers=holder.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);camera.transform.position=bounds.center+Vector3.back*4;camera.orthographicSize=Mathf.Max(bounds.size.y,bounds.size.x/1.6f)*.65f;camera.Render();RenderTexture.active=rt;var detail=new Texture2D(1600,1000,TextureFormat.RGB24,false);detail.ReadPixels(new Rect(0,0,1600,1000),0,0);detail.Apply();File.WriteAllBytes(ProjectPath.Report("Arsenal-"+kind+".png"),detail.EncodeToPNG());Object.Destroy(detail);RenderTexture.active=previous;
            }
            foreach(Transform child in root.transform)if(!child.GetComponent<Light>())child.gameObject.SetActive(false);
            var runtime=(Services.Session as CastlePlaySession).Modes;var showcase=new[]{EquipmentKind.RocketLauncher,EquipmentKind.NeedleDrill,EquipmentKind.Speed,EquipmentKind.Jump,EquipmentKind.PoweredArmor};for(int i=0;i<showcase.Length;i++)runtime.RegisterPickup(new PickupSource{Kind=showcase[i],Position=origin+new Vector3((i-(showcase.Length-1)*.5f)*.7f,0,0),Duration=20});
            camera.transform.position=origin+Vector3.back*5;camera.orthographicSize=1.15f;yield return new WaitForSeconds(.25f);camera.Render();RenderTexture.active=rt;var pickups=new Texture2D(1600,1000,TextureFormat.RGB24,false);pickups.ReadPixels(new Rect(0,0,1600,1000),0,0);pickups.Apply();File.WriteAllBytes(ProjectPath.Report("Arsenal-Pickups.png"),pickups.EncodeToPNG());Object.Destroy(pickups);RenderTexture.active=previous;
            validate(Object.FindFirstObjectByType<DeadlyJenga.Presentation.CandyEquipmentView>().GetComponentsInChildren<Transform>().Any(t=>t.name=="Live equipment model"),"Actual world pickups contain live equipment geometry");
            camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(go);Object.Destroy(root);yield return null;
        }
    }
}
