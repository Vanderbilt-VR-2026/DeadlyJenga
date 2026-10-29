using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;

namespace DeadlyJenga.CastlePrototype
{
    // World-space menu is visible in both eyes; gaze + trigger requires no mouse in VR.
    // Every action and value goes through Core (ISessionInfo and its Demo, Social and Multiplayer controls).
    public sealed partial class CastleWorldMenu : MonoBehaviour
    {
        ISessionInfo session;
        ISurvivorSettings Settings=>session.Player as ISurvivorSettings;
        IDemoControls Demo=>session.Demo;
        bool DemoMode=>Demo!=null&&(Demo.DemoMode||Demo.CanCommand);
        bool CanCommand=>Demo!=null&&Demo.CanCommand;
        Vector3 openingForward;
        readonly Button[] buttons = new Button[15];
        readonly BoxCollider[] targets = new BoxCollider[15];
        readonly Text[] labels = new Text[15];
        bool networkPage,comfortPage,tuningPage,disasterPage,modePage;
        string disasterStatus=string.Empty;
        int disasterOffset;
        int socialSelection;bool socialMuted;
        Text heading,help;
        readonly CandyHudIcon[] icons={CandyHudIcon.Play,CandyHudIcon.Hand,CandyHudIcon.Home,CandyHudIcon.Home,CandyHudIcon.Survivor,CandyHudIcon.Friends,CandyHudIcon.Settings,CandyHudIcon.Survivor,CandyHudIcon.Hand,CandyHudIcon.Settings,CandyHudIcon.Hand,CandyHudIcon.Friends};
        readonly string[] explanations={"Return to the action.","Swap between surviving and flying as a giant hand.","Return to the highest upright block as a survivor.","Restore the castle. Only the host can rebuild.","Add a survivor with the selected personality.","Fill empty survivor slots, including human players.","Choose the personality used by the next survivor.","Remove survivor bots and stop automatic filling.","Add an AI giant hand using the current hand prefab.","Choose the next giant hand's personality.","Remove AI hands; keep the human players.","Find nearby players and manage invitations."};
        public void Build(ISessionInfo owner)
        {
            session = owner;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rect = GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(600, 1040); transform.localScale = Vector3.one * .0018f;
            var bg = gameObject.AddComponent<Image>();bg.sprite=CandyHudVisuals.Pill;bg.type=Image.Type.Sliced;bg.color = CandyHudVisuals.Ink;
            heading=Text("DEADLY JENGA / DEMO\nLook at a button  /  RIGHT trigger to select", new Vector2(0, 355), new Vector2(570, 95), 24);
            help=Text("Left Y closes  /  Hand tracking: left thumb + middle opens / closes",new Vector2(0,-480),new Vector2(550,65),19);help.color=CandyHudVisuals.Muted;
            string[] titles = { "Resume", "Switch survivor / flying hand", "Respawn on top", "Rebuild castle", "Spawn AI survivor", "Fill to 10 players", "NPC personality: Balanced", "Clear survivors / stop auto-fill", "Spawn AI giant hand", "Hand personality: Playful", "Clear AI hands", "Local multiplayer / invites", "VR movement / turning", "Host stamina / audio", "Natural disasters" };
            for (int i = 0; i < buttons.Length; i++)
            {
                var go = new GameObject(titles[i], typeof(RectTransform), typeof(Image), typeof(Button), typeof(BoxCollider));
                go.transform.SetParent(transform, false);
                var r = go.GetComponent<RectTransform>(); r.sizeDelta = new Vector2(540, 46); r.anchoredPosition = new Vector2(0, 277 - i * 50);
                buttons[i] = go.GetComponent<Button>(); buttons[i].image.color = CandyHudVisuals.Card;buttons[i].image.sprite=CandyHudVisuals.Pill;buttons[i].image.type=Image.Type.Sliced;
                targets[i] = go.GetComponent<BoxCollider>(); targets[i].size = new Vector3(540, 46, 4); targets[i].isTrigger = true;
                var label = Text(titles[i], Vector2.zero, new Vector2(472,46), 21); labels[i]=label;label.transform.SetParent(go.transform, false);
                label.rectTransform.anchoredPosition = new Vector2(20,0);
                var glyph=new GameObject("Candy menu icon",typeof(RectTransform),typeof(RawImage));glyph.transform.SetParent(go.transform,false);var image=glyph.GetComponent<RawImage>();image.texture=CandyHudVisuals.Icon(i<icons.Length?icons[i]:CandyHudIcon.Settings);image.color=CandyHudVisuals.Lavender;image.raycastTarget=false;image.rectTransform.sizeDelta=new Vector2(27,27);image.rectTransform.anchoredPosition=new Vector2(-243,0);
            }
            BuildScaffoldButton();
            BuildMapControls();
            gameObject.SetActive(false);
        }
        Slider CreateDisasterSlider(string name,out Text caption,out BoxCollider hit)
        {
            var root=new GameObject(name,typeof(RectTransform),typeof(Slider),typeof(BoxCollider));root.transform.SetParent(transform,false);
            var rect=root.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(500,48);hit=root.GetComponent<BoxCollider>();hit.size=new Vector3(500,48,4);hit.isTrigger=true;
            caption=Text(name,new Vector2(0,14),new Vector2(500,30),19);caption.transform.SetParent(root.transform,false);caption.rectTransform.anchoredPosition=new Vector2(0,14);
            var bar=new GameObject("Track",typeof(RectTransform),typeof(Image));bar.transform.SetParent(root.transform,false);var track=bar.GetComponent<Image>();track.sprite=CandyHudVisuals.Pill;track.type=Image.Type.Sliced;track.color=CandyHudVisuals.Card;track.rectTransform.sizeDelta=new Vector2(500,9);track.rectTransform.anchoredPosition=new Vector2(0,-13);
            var fill=new GameObject("Fill",typeof(RectTransform),typeof(Image));fill.transform.SetParent(bar.transform,false);var ink=fill.GetComponent<Image>();ink.color=CandyHudVisuals.Mint;ink.sprite=CandyHudVisuals.Pill;ink.type=Image.Type.Sliced;ink.rectTransform.sizeDelta=Vector2.zero;ink.rectTransform.anchorMin=Vector2.zero;ink.rectTransform.anchorMax=Vector2.one;
            var thumb=new GameObject("Handle",typeof(RectTransform),typeof(Image));thumb.transform.SetParent(bar.transform,false);var knob=thumb.GetComponent<Image>();knob.sprite=CandyHudVisuals.Pill;knob.color=Color.white;knob.rectTransform.sizeDelta=new Vector2(22,22);
            var slider=root.GetComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.fillRect=ink.rectTransform;slider.handleRect=knob.rectTransform;slider.targetGraphic=knob;
            root.SetActive(false);return slider;
        }
        bool PointWaveSlider(Ray ray,Slider slider,BoxCollider collider,bool pressed)
        {
            if(!slider.gameObject.activeSelf||!slider.interactable||!collider.Raycast(ray,out var hit,4))return false;
            if(pressed){float t=Mathf.InverseLerp(-250,250,slider.transform.InverseTransformPoint(hit.point).x);slider.value=Mathf.Lerp(slider.minValue,slider.maxValue,t);}
            help.text="Next launch only  /  look along the slider and hold trigger / pinch to adjust";return true;
        }
        Text Text(string value, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(value, typeof(RectTransform), typeof(Text)); go.transform.SetParent(transform, false);
            var text = go.GetComponent<Text>(); text.text = value; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=Mathf.Min(18,fontSize);text.resizeTextMaxSize=fontSize;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position; return text;
        }
        public void Show(bool show, Camera camera)
        {
            gameObject.SetActive(show);
            if (!show) return;
            openingForward = camera.transform.forward;
            transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * 1.6f, camera.transform.rotation);
            Physics.SyncTransforms();
        }
        public void Point(Camera camera, bool pressed,bool held=false)
        {
            if(PointMapControls(camera,pressed))return;
            if(giveEquipmentPage){PointGiveEquipmentPage(camera,pressed);return;}
            if(randomPowerupPage){PointRandomPowerups(camera,pressed);return;}
            if(powerupPage){PointPowerupPage(camera,pressed);return;}
            PointScaffold(camera,pressed);
            if(tabletopPage){PointTabletop(camera,pressed);return;}
            if(hudPage){PointHudPage(camera,pressed);return;}
            if(modePage){PointModePage(camera,pressed);return;}
            if(disasterPage){PointDisasterPage(camera,pressed,held);return;}
            if(!networkPage&&!comfortPage&&!tuningPage){PointOrganizedMenu(camera,pressed);return;}
            HideDisasterSliders();
            var net=session.Multiplayer;
            bool watching=false;var match=MatchRuntime.Current;if(match?.Frame!=null)foreach(var row in match.Frame.players)if(row.id==match.LocalActorId&&row.role=="spectator")watching=true;
            var active=session.PlayingHand?session.Hand:session.Player;bool trackedHands=active!=null&&active.HasHandTracking;
            string select=trackedHands?"Look at a button  /  RIGHT pinch to select":"Look at a button  /  RIGHT trigger to select";
            heading.text=networkPage?"MULTIPLAYER\n"+(net!=null?net.Status:"Finding nearby demos..."):"DEADLY JENGA\n"+select;
            for(int j=0;j<buttons.Length;j++){var r=(RectTransform)buttons[j].transform;r.anchoredPosition=new Vector2(0,277-j*50);r.sizeDelta=new Vector2(540,46);}
            if (networkPage && net!=null && net.IsFusion)
            {
                for (int j = 0; j < buttons.Length; j++) buttons[j].gameObject.SetActive(j == 0 || j == 6 || j == 11 || j==1 || j==2);
                labels[1].text="Demo controls: "+(net.SharedControls?"EVERYONE":"HOST ONLY");targets[1].enabled=net.CanToggleSharedControls;
                labels[2].text="Invite everyone on Wi-Fi";targets[2].enabled=net.CanInviteNearby;
                labels[0].text = "Resume"; labels[6].text = net.IsClient ? "Leave game" : "Leave / transfer host"; labels[11].text = "Back";
                bool invitation=net.InvitationFrom.Length>0;
                buttons[3].gameObject.SetActive(invitation);buttons[4].gameObject.SetActive(invitation);
                labels[3].text=invitation?"Invite: "+net.InvitationFrom:"";targets[3].enabled=false;
                labels[4].text="Dismiss invite  /  leave room to accept";targets[4].enabled=invitation;
                targets[0].enabled = targets[6].enabled = targets[11].enabled = true;
            }
            else if(tuningPage&&Settings!=null)
            {
                var rules=Settings.CurrentRules;var audio=CandyAudioEngine.Instance;
                string[] titles={"Resume","Grip capacity: "+rules.gripSeconds.ToString("F1")+" s  (+)","Grip capacity  (-)","Drain: "+rules.drainRate.ToString("F2")+" / s  (cycle)","Recovery: "+rules.recoveryRate.ToString("F2")+" / s  (cycle)","Recovery delay: "+rules.recoveryDelay.ToString("F2")+" s  (cycle)","Drain curve: "+rules.drainCurve,"Recovery curve: "+rules.recoveryCurve,"Drain degree: "+rules.drainDegree.ToString("F1"),"Recovery degree: "+rules.recoveryDegree.ToString("F1"),"Master volume: "+(audio&&audio.bank?audio.bank.master.ToString("P0"):"unavailable"),"Music volume: "+(audio&&audio.bank?audio.bank.music.ToString("P0"):"unavailable"),"Back to demo controls","Preview sound"};
                for(int i=0;i<buttons.Length;i++){buttons[i].gameObject.SetActive(i<titles.Length);if(i<titles.Length){labels[i].text=titles[i];targets[i].enabled=i==0||i>=10||session.CanEditWorld;}}
            }
            else if(comfortPage&&Settings!=null)
            {
                var p=Settings;var social=session.Social;
                string[] titles={"Resume","Movement: "+p.MovementLabel,"Right stick: "+p.TurnLabel,p.StandingPlay?"Jump gesture: "+p.JumpGestureLabel:"Recalibrate seated eye height","Smooth speed: "+p.SmoothTurnSpeed+" deg / sec","Seated / smooth turn"+(!p.StandingPlay?" (active)":""),"Standing / physical turn"+(p.StandingPlay?" (active)":""),"Left stick moves in both modes","Physical head turns always look around","Seated: A jumps; hold to climb","Standing jump: "+p.JumpGestureLabel,"Tap to jump; hold to climb; release to drop","Back to demo controls"};
                if(social!=null)
                {
                    titles[7]=social.MicrophoneOn?"Mute microphone":"Enable microphone (permission)";
                    titles[8]="Bot voices: "+(social.VoiceEnabled?"on":"off");
                    var ids=social.AiPlayerIds;
                    titles[9]=ids.Count>0?"AI player: "+social.AiPlayerName(ids[socialSelection%ids.Count])+" (next)":"No AI players yet";
                    titles[10]="Toggle mute selected AI";titles[11]="Bot volume: "+social.BotVolume.ToString("P0")+" (cycle)";
                }
                for(int i=0;i<labels.Length;i++){buttons[i].gameObject.SetActive(i<titles.Length);if(i<titles.Length){labels[i].text=titles[i];targets[i].enabled=social!=null||i<7||i==12;}}
            }
            else if(networkPage&&net!=null)
            {
                foreach(var button in buttons)button.gameObject.SetActive(true);
                string[] titles={"Resume","Nearby: "+net.SelectedName+" (next)","Request to join selected demo","Invite selected demo here","Accept invite / join request","Decline invite / join request","Leave / end local session",net.PendingLabel,net.IsClient?"Connected to host":"Hosting  /  "+net.HumanCount+" humans","Open this page on both headsets","No restart needed to join","Back to demo controls"};
                for(int i=0;i<labels.Length;i++){labels[i].text=i<titles.Length?titles[i]:"";targets[i].enabled=i<titles.Length;}
                if(titles.Length<buttons.Length)buttons[titles.Length].gameObject.SetActive(false);
            }
            else
            {
                labels[0].text="Resume";labels[1].text=watching?"Spectate previous survivor":"Switch survivor / flying hand";labels[2].text=watching?"Spectate next survivor":"Respawn on top";labels[3].text="Rebuild castle (host)";
                labels[4].text="Add a survivor";labels[5].text="Fill to target";labels[6].text="Next survivor personality";labels[7].text="Clear survivors";
                labels[8].text="Add a flying hand";labels[9].text="Next hand personality";labels[10].text="Clear flying hands";labels[12].text="VR movement / voice";labels[13].text="Match / scenery / stamina";labels[14].text="Powerups / disasters";
                labels[11].text=net!=null&&net.IsFusion?"Multiplayer / leave":"Same-Wi-Fi invites"+(net!=null&&net.HasPending?"  /  NEW":"");
                for(int i=0;i<buttons.Length;i++)
                {
                    bool shown=(i!=11||net!=null)&&(i!=13||session.CanEditWorld||match!=null)&&(i!=14||DemoMode||CanCommand||match is IPowerupSpawnControls);
                    buttons[i].gameObject.SetActive(shown);
                    if(shown)targets[i].enabled=true;
                }
            }
            // Follow locomotion without following head rotation, so gaze can travel between buttons.
            transform.position = camera.transform.position + openingForward * 1.6f;
            Physics.SyncTransforms();
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            bool pointed=false;
            for (int i = 0; i < buttons.Length; i++)
            {
                if(!buttons[i].gameObject.activeSelf||!targets[i].enabled)continue;
                bool hover = targets[i].Raycast(ray, out _, 4);
                buttons[i].image.color = hover ? CandyHudVisuals.Mint : CandyHudVisuals.Card;
                labels[i].color=hover?CandyHudVisuals.Ink:Color.white;
                if(hover){pointed=true;help.text=networkPage||comfortPage||i>=explanations.Length?select:explanations[i]+"\n"+select;
                    }
                if (!hover || !pressed) continue;
                if(watching&&!networkPage&&!comfortPage&&!tuningPage&&(i==1||i==2)){match.NextSpectator(i==1?-1:1);session.SetMenu(false);break;}
                if(i==14){powerupPage=true;networkPage=comfortPage=tuningPage=false;break;}
                if(i==12){if(tuningPage){tuningPage=false;comfortPage=false;}else comfortPage=!comfortPage;networkPage=false;break;}
                if(tuningPage)
                {
                    if(i==0){session.SetMenu(false);break;}
                    if(i>=10)
                    {
                        var localAudio=CandyAudioEngine.Instance;
                        if(localAudio&&localAudio.bank)
                        {
                            if(i==10)localAudio.bank.master=localAudio.bank.master>=.99f?0:Mathf.Min(1,localAudio.bank.master+.1f);
                            if(i==11)localAudio.bank.music=localAudio.bank.music>=.99f?0:Mathf.Min(1,localAudio.bank.music+.1f);
                            if(i==13)localAudio.Play(CandySound.UiClick,session.Player.transform.position,local:true);
                        }
                        break;
                    }
                    if(!session.CanEditWorld||Settings==null)break;
                    var p=Settings;var rules=p.CurrentRules;string before=p.SaveStaminaRules();
                    if(i==1)rules.gripSeconds=Mathf.Min(20,rules.gripSeconds+.5f);if(i==2)rules.gripSeconds=Mathf.Max(.5f,rules.gripSeconds-.5f);
                    if(i==3)rules.drainRate=rules.drainRate>=4?0:rules.drainRate+.25f;
                    if(i==4)rules.recoveryRate=rules.recoveryRate>=2?0:rules.recoveryRate+.1f;
                    if(i==5)rules.recoveryDelay=rules.recoveryDelay>=5?0:rules.recoveryDelay+.25f;
                    if(i==6)rules.drainCurve=MatchOptions.Next(MatchOptions.Curves,rules.drainCurve);
                    if(i==7)rules.recoveryCurve=MatchOptions.Next(MatchOptions.Curves,rules.recoveryCurve);
                    if(i==8)rules.drainDegree=rules.drainDegree>=6?1:rules.drainDegree+.5f;
                    if(i==9)rules.recoveryDegree=rules.recoveryDegree>=6?1:rules.recoveryDegree+.5f;
                    if(i>=1&&i<=9){p.ApplyMatchRules(rules);string after=p.SaveStaminaRules();if(after!=before)Demo?.ApplyStaminaRules(after);}
                    break;
                }
                if(i==13){modePage=true;comfortPage=networkPage=false;break;}
                if(comfortPage&&Settings!=null)
                {
                    var p=Settings;var social=session.Social;
                    if(i==0)session.SetMenu(false);if(i==1)p.CycleMovement();if(i==2)p.CycleTurn();
                    if(i==3){if(p.StandingPlay)p.CycleJumpGesture();else p.RecalibrateSeatedHeight();}
                    if(i==4)p.CycleSmoothTurnSpeed();
                    if(i==5)p.UseStationaryPreset();if(i==6)p.UseStandingPreset();
                    if(social!=null)
                    {
                        if(i==7)social.SetMicrophone(!social.MicrophoneOn);
                        if(i==8)social.VoiceEnabled=!social.VoiceEnabled;
                        if(i==9){socialSelection++;socialMuted=false;}
                        if(i==10){var ids=social.AiPlayerIds;if(ids.Count>0){socialMuted=!socialMuted;social.SetMuted(ids[socialSelection%ids.Count],socialMuted);}}
                        if(i==11)social.BotVolume=social.BotVolume>.9f?0:Mathf.Min(1,social.BotVolume+.2f);
                    }
                    break;
                }
                if(i==11){networkPage=!networkPage;break;}
                if(networkPage)
                {
                    if(i==0)session.SetMenu(false);
                    if(net.IsFusion){if(i==1)net.ToggleSharedControls();if(i==2)net.InviteNearby();if(i==4)net.DismissInvitation();if(i==6)net.LeaveSession();break;}
                    if(i==1)net.NextNearby();
                    if(i==2)net.RequestSelected();
                    if(i==3)net.InviteSelected();
                    if(i==4)net.AcceptPending();
                    if(i==5)net.DeclinePending();
                    if(i==6)net.LeaveSession();
                    break;
                }
                if(i>=3&&!session.CanEditWorld&&!CanCommand)break;
                if (i == 1) session.SetRole(!session.PlayingHand);
                if (i == 2) session.SetRole(false);
                if (i == 3) session.ResetMap();
                if (i == 4) Demo?.Run(DemoCommand.AddSurvivor);
                if (i == 5) Demo?.Run(DemoCommand.FillSurvivors);
                if (i == 6) Demo?.Run(DemoCommand.NextSurvivor);
                if (i == 7) Demo?.Run(DemoCommand.ClearSurvivors);
                if (i == 8) Demo?.Run(DemoCommand.AddHand);
                if (i == 9) Demo?.Run(DemoCommand.NextHand);
                if (i == 10) Demo?.Run(DemoCommand.ClearHands);
                if(i>=4)break;
                session.SetMenu(false); break;
            }
            if(!pointed)help.text="LOOK to highlight  /  "+(trackedHands?"RIGHT pinch selects\nLeft thumb + middle opens / closes":"RIGHT trigger selects\nY  /  LEFT opens / closes");
        }
    }
}
