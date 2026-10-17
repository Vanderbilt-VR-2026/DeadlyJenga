using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    // World-space menu is visible in both eyes; gaze + trigger requires no mouse in VR.
    public sealed partial class CastleWorldMenu : MonoBehaviour
    {
        CastlePlaySession session;
        Vector3 openingForward;
        readonly Button[] buttons = new Button[15];
        readonly BoxCollider[] targets = new BoxCollider[15];
        readonly Text[] labels = new Text[15];
        bool networkPage,comfortPage,tuningPage,disasterPage;
        string disasterStatus=string.Empty;
        int disasterOffset;
        int socialSelection;bool socialMuted;
        Text heading,help;
        readonly CandyHudIcon[] icons={CandyHudIcon.Play,CandyHudIcon.Hand,CandyHudIcon.Home,CandyHudIcon.Home,CandyHudIcon.Survivor,CandyHudIcon.Friends,CandyHudIcon.Settings,CandyHudIcon.Survivor,CandyHudIcon.Hand,CandyHudIcon.Settings,CandyHudIcon.Hand,CandyHudIcon.Friends};
        readonly string[] explanations={"Return to the action.","Swap between surviving and flying as a giant hand.","Return to the highest upright block as a survivor.","Restore the castle. Only the host can rebuild.","Add a survivor with the selected personality.","Fill empty survivor slots, including human players.","Choose the personality used by the next survivor.","Remove survivor bots and stop automatic filling.","Add an AI giant hand using the current hand prefab.","Choose the next giant hand's personality.","Remove AI hands; keep the human players.","Find nearby players and manage invitations."};
        public void Build(CastlePlaySession owner)
        {
            session = owner;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rect = GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(600, 1040); transform.localScale = Vector3.one * .0018f;
            var bg = gameObject.AddComponent<Image>();bg.sprite=CandyHudVisuals.Pill;bg.type=Image.Type.Sliced;bg.color = CandyHudVisuals.Ink;
            heading=Text("DEADLY JENGA / DEMO\nLook at a button · RIGHT trigger to select", new Vector2(0, 355), new Vector2(570, 95), 24);
            help=Text("Left Y closes · Hand tracking: left thumb + middle opens / closes",new Vector2(0,-480),new Vector2(550,65),19);help.color=CandyHudVisuals.Muted;
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
            help.text="Next launch only · look along the slider and hold trigger / pinch to adjust";return true;
        }
        Text Text(string value, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(value, typeof(RectTransform), typeof(Text)); go.transform.SetParent(transform, false);
            var text = go.GetComponent<Text>(); text.text = value; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
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
            if(disasterPage){PointDisasterPage(camera,pressed,held);return;}
            HideDisasterSliders();
            var net=session.Network;
            var tracking=(session.PlayingHand?session.Hand.gameObject:session.Player.gameObject).GetComponent<DeadlyJenga.CandyPlayers.CandyTrackingSource>();
            string select=tracking&&tracking.HasHandTracking?"Look at a button · RIGHT pinch to select":"Look at a button · RIGHT trigger to select";
            heading.text=networkPage?"MULTIPLAYER\n"+(net?net.Status:"Finding nearby demos..."):"DEADLY JENGA\n"+select;
            for(int j=0;j<buttons.Length;j++){var r=(RectTransform)buttons[j].transform;r.anchoredPosition=new Vector2(0,277-j*50);r.sizeDelta=new Vector2(540,46);}
            if (networkPage && net && net.IsFusion)
            {
                for (int j = 0; j < buttons.Length; j++) buttons[j].gameObject.SetActive(j == 0 || j == 6 || j == 11 || j==1 || j==2);
                var room=IslandRoomConnection.Instance;
                labels[1].text="Demo controls: "+(room&&room.SharedDemoControls?"EVERYONE":"HOST ONLY");targets[1].enabled=room&&room.IsHost&&MatchSetup.ModeId=="demo";
                labels[2].text="Invite everyone on Wi-Fi";targets[2].enabled=room&&room.IsHost;
                labels[0].text = "Resume"; labels[6].text = net.IsClient ? "Leave game" : "Leave / transfer host"; labels[11].text = "Back";
                var invitation=room?room.Discovery?.Invitation:null;
                buttons[3].gameObject.SetActive(invitation!=null);buttons[4].gameObject.SetActive(invitation!=null);
                labels[3].text=invitation!=null?"Invite: "+invitation.message.name:"";targets[3].enabled=false;
                labels[4].text="Dismiss invite · leave room to accept";targets[4].enabled=invitation!=null;
                targets[0].enabled = targets[6].enabled = targets[11].enabled = true;
            }
            else if(tuningPage)
            {
                var p=session.Player;var audio=DeadlyJenga.CandyAudio.CandyAudioEngine.Instance;
                string[] titles={"Resume","Grip capacity: "+p.climbGripSeconds.ToString("F1")+" s  (+)","Grip capacity  (-)","Drain: "+p.climbDrainPerSecond.ToString("F2")+" / s  (cycle)","Recovery: "+p.climbRecoveryPerSecond.ToString("F2")+" / s  (cycle)","Recovery delay: "+p.climbRecoveryDelay.ToString("F2")+" s  (cycle)","Drain curve: "+p.climbDrainCurve,"Recovery curve: "+p.climbRecoveryCurve,"Drain degree: "+p.climbDrainDegree.ToString("F1"),"Recovery degree: "+p.climbRecoveryDegree.ToString("F1"),"Master volume: "+(audio&&audio.bank?audio.bank.master.ToString("P0"):"unavailable"),"Music volume: "+(audio&&audio.bank?audio.bank.music.ToString("P0"):"unavailable"),"Back to demo controls","Preview sound"};
                for(int i=0;i<buttons.Length;i++){buttons[i].gameObject.SetActive(i<titles.Length);if(i<titles.Length){labels[i].text=titles[i];targets[i].enabled=!net||net.CanEditWorld;}}
            }
            else if(comfortPage)
            {
                var p=session.Player;
                string[] titles={"Resume","Movement: "+p.vrMovement,"Right stick: "+p.vrTurn,p.vrPlayStyle==SurvivorVRPlayStyle.Standing?"Jump gesture: "+p.jumpGesture:"Recalibrate seated eye height","Smooth speed: "+p.vrSmoothDegreesPerSecond+"° / sec","Seated / smooth turn"+(p.vrPlayStyle==SurvivorVRPlayStyle.Seated?" (active)":""),"Standing / physical turn"+(p.vrPlayStyle==SurvivorVRPlayStyle.Standing?" (active)":""),"Left stick moves in both modes","Physical head turns always look around","Seated: A jumps; hold to climb","Standing jump: "+p.jumpGesture,"Tap to jump; hold to climb; release to drop","Back to demo controls"};
                var social=session.GetComponent<DeadlyJenga.CastleSocial.SocialWorld>();
                if(social)
                {
                    titles[7]=social.MicrophoneInput.Capturing?"Mute microphone":"Enable microphone (permission)";
                    titles[8]="Bot voices: "+(social.Config.enableVoice?"on":"off");
                    var names=new System.Collections.Generic.List<DeadlyJenga.CastleSocial.SocialNotice>(social.Participants.Values);
                    titles[9]=names.Count>0?"AI player: "+names[socialSelection%names.Count].name+" (next)":"No AI players yet";
                    titles[10]="Toggle mute selected AI";titles[11]="Bot volume: "+social.Settings.botVolume.ToString("P0")+" (cycle)";
                }
                for(int i=0;i<labels.Length;i++){buttons[i].gameObject.SetActive(i<titles.Length);if(i<titles.Length){labels[i].text=titles[i];targets[i].enabled=social||i<7||i==12;}}
            }
            else if(networkPage&&net)
            {
                foreach(var button in buttons)button.gameObject.SetActive(true);
                string[] titles={"Resume","Nearby: "+net.SelectedName+" (next)","Request to join selected demo","Invite selected demo here","Accept invite / join request","Decline invite / join request","Leave / end local session",net.PendingLabel,net.IsClient?"Connected to host":"Hosting · "+net.HumanCount+" humans","Open this page on both headsets","No restart needed to join","Back to demo controls"};
                for(int i=0;i<labels.Length;i++){labels[i].text=i<titles.Length?titles[i]:"";targets[i].enabled=i<titles.Length;}
                if(titles.Length<buttons.Length)buttons[titles.Length].gameObject.SetActive(false);
            }
            else
            {
                labels[0].text="Resume";labels[1].text="Switch survivor / flying hand";labels[2].text="Respawn on top";labels[3].text="Rebuild castle (host)";
                labels[4].text="Add a survivor";labels[5].text="Fill to target";labels[6].text="Next survivor personality";labels[7].text="Clear survivors";
                labels[8].text="Add a flying hand";labels[9].text="Next hand personality";labels[10].text="Clear flying hands";labels[12].text="VR movement / voice";labels[13].text="Host stamina / audio";labels[14].text="Natural disasters";
                labels[11].text=net&&net.IsFusion?"Multiplayer / leave":"Same-Wi-Fi invites"+(net&&net.HasPending?" · NEW":"");
                for(int i=0;i<buttons.Length;i++)
                {
                    bool shown=(i!=11||net)&&(i!=13||!net||net.CanEditWorld)&&(i!=14||MatchSetup.ModeId=="demo");
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
                if(hover){pointed=true;help.text=networkPage||comfortPage||i>=explanations.Length?select:explanations[i]+"\n"+select;}
                if (!hover || !pressed) continue;
                if(i==14){disasterSelection=-1;disasterPage=true;networkPage=comfortPage=tuningPage=false;break;}
                if(i==12){if(tuningPage){tuningPage=false;comfortPage=false;}else comfortPage=!comfortPage;networkPage=false;break;}
                if(tuningPage)
                {
                    if(net&&!net.CanEditWorld)break;
                    var p=session.Player;string before=MatchSetup.CaptureRules(p);var audio=DeadlyJenga.CandyAudio.CandyAudioEngine.Instance;
                    if(i==0)session.SetMenu(false);
                    if(i==1)p.climbGripSeconds=Mathf.Min(20,p.climbGripSeconds+.5f);if(i==2)p.climbGripSeconds=Mathf.Max(.5f,p.climbGripSeconds-.5f);
                    if(i==3)p.climbDrainPerSecond=p.climbDrainPerSecond>=4?0:p.climbDrainPerSecond+.25f;
                    if(i==4)p.climbRecoveryPerSecond=p.climbRecoveryPerSecond>=2?0:p.climbRecoveryPerSecond+.1f;
                    if(i==5)p.climbRecoveryDelay=p.climbRecoveryDelay>=5?0:p.climbRecoveryDelay+.25f;
                    if(i==6)p.climbDrainCurve=(StaminaCurve)(((int)p.climbDrainCurve+1)%4);
                    if(i==7)p.climbRecoveryCurve=(StaminaCurve)(((int)p.climbRecoveryCurve+1)%4);
                    if(i==8)p.climbDrainDegree=p.climbDrainDegree>=6?1:p.climbDrainDegree+.5f;
                    if(i==9)p.climbRecoveryDegree=p.climbRecoveryDegree>=6?1:p.climbRecoveryDegree+.5f;
                    if(audio&&audio.bank){if(i==10)audio.bank.master=audio.bank.master>=.99f?0:Mathf.Min(1,audio.bank.master+.1f);if(i==11)audio.bank.music=audio.bank.music>=.99f?0:Mathf.Min(1,audio.bank.music+.1f);if(i==13)audio.Play(DeadlyJenga.CandyAudio.CandySound.UiClick,p.transform.position,local:true);}
                    string after=MatchSetup.CaptureRules(p);if(after!=before)CastleDemoCommands.Run(session,CastleDemoAction.Stamina,after);
                    break;
                }
                if(i==13){tuningPage=true;comfortPage=networkPage=false;break;}
                if(comfortPage)
                {
                    var p=session.Player;
                    if(i==0)session.SetMenu(false);if(i==1)p.CycleVRMovement();if(i==2)p.CycleVRTurn();
                    if(i==3){if(p.vrPlayStyle==SurvivorVRPlayStyle.Standing)p.CycleJumpGesture();else p.CalibrateSeatedHeight();}
                    if(i==4)p.vrSmoothDegreesPerSecond=p.vrSmoothDegreesPerSecond<60?60:p.vrSmoothDegreesPerSecond<90?90:45;
                    if(i==5)p.UseStationaryVR();if(i==6)p.UseStandingVR();
                    var social=session.GetComponent<DeadlyJenga.CastleSocial.SocialWorld>();
                    if(social)
                    {
                        if(i==7)social.MicrophoneInput.EnableCapture(!social.MicrophoneInput.Capturing);
                        if(i==8)social.Config.enableVoice=!social.Config.enableVoice;
                        if(i==9){socialSelection++;socialMuted=false;}
                        if(i==10){var names=new System.Collections.Generic.List<DeadlyJenga.CastleSocial.SocialNotice>(social.Participants.Values);if(names.Count>0){socialMuted=!socialMuted;social.Mute(names[socialSelection%names.Count].id,socialMuted);}}
                        if(i==11)social.Settings.botVolume=social.Settings.botVolume>.9f?0:Mathf.Min(1,social.Settings.botVolume+.2f);
                    }
                    break;
                }
                if(i==11){networkPage=!networkPage;break;}
                if(networkPage)
                {
                    if(i==0)session.SetMenu(false);
                    if(net.IsFusion){var room=IslandRoomConnection.Instance;if(i==1&&room)room.SetSharedControls(!room.SharedDemoControls);if(i==2&&room)room.InviteNearby();if(i==4&&room)room.Discovery?.Dismiss();if(i==6)net.LeaveSession();break;}
                    if(i==1)net.NextNearby();
                    if(i==2)net.RequestSelected();
                    if(i==3)net.InviteSelected();
                    if(i==4)net.AcceptPending();
                    if(i==5)net.DeclinePending();
                    if(i==6)net.LeaveSession();
                    break;
                }
                if(i>=3&&net&&!net.CanEditWorld)break;
                if (i == 1) session.SetRole(!session.PlayingHand);
                if (i == 2) session.SetRole(false);
                if (i == 3) session.ResetCastle();
                if (i == 4) CastleDemoCommands.Run(session,CastleDemoAction.AddSurvivor);
                if (i == 5) CastleDemoCommands.Run(session,CastleDemoAction.FillSurvivors);
                if (i == 6) CastleDemoCommands.Run(session,CastleDemoAction.NextSurvivor);
                if (i == 7) CastleDemoCommands.Run(session,CastleDemoAction.ClearSurvivors);
                if (i == 8) CastleDemoCommands.Run(session,CastleDemoAction.AddHand);
                if (i == 9) CastleDemoCommands.Run(session,CastleDemoAction.NextHand);
                if (i == 10) CastleDemoCommands.Run(session,CastleDemoAction.ClearHands);
                if(i>=4)break;
                session.SetMenu(false); break;
            }
            if(!pointed)help.text="LOOK to highlight · "+(tracking&&tracking.HasHandTracking?"RIGHT pinch selects\nLeft thumb + middle opens / closes":"RIGHT trigger selects\nY · LEFT opens / closes");
        }
    }
}
