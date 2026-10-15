using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    // World-space menu is visible in both eyes; gaze + trigger requires no mouse in VR.
    public sealed class CastleWorldMenu : MonoBehaviour
    {
        CastlePlaySession session;
        Vector3 openingForward;
        readonly Button[] buttons = new Button[13];
        readonly BoxCollider[] targets = new BoxCollider[13];
        readonly Text[] labels = new Text[13];
        bool networkPage,comfortPage;
        Text heading,help;
        readonly CandyHudIcon[] icons={CandyHudIcon.Play,CandyHudIcon.Hand,CandyHudIcon.Home,CandyHudIcon.Home,CandyHudIcon.Survivor,CandyHudIcon.Friends,CandyHudIcon.Settings,CandyHudIcon.Survivor,CandyHudIcon.Hand,CandyHudIcon.Settings,CandyHudIcon.Hand,CandyHudIcon.Friends};
        readonly string[] explanations={"Return to the action.","Swap between surviving and flying as a giant hand.","Return to the highest upright block as a survivor.","Restore the castle. Only the host can rebuild.","Add a survivor with the selected personality.","Fill empty survivor slots, including human players.","Choose the personality used by the next survivor.","Remove survivor bots and stop automatic filling.","Add an AI giant hand using the current hand prefab.","Choose the next giant hand's personality.","Remove AI hands; keep the human players.","Find nearby players and manage invitations."};
        public void Build(CastlePlaySession owner)
        {
            session = owner;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rect = GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(600, 920); transform.localScale = Vector3.one * .0018f;
            var bg = gameObject.AddComponent<Image>();bg.sprite=CandyHudVisuals.Pill;bg.type=Image.Type.Sliced;bg.color = CandyHudVisuals.Ink;
            heading=Text("DEADLY JENGA / DEMO\nLook at a button · RIGHT trigger to select", new Vector2(0, 355), new Vector2(570, 95), 24);
            help=Text("Left Y closes · Hand tracking: left thumb + middle opens / closes",new Vector2(0,-393),new Vector2(550,65),19);help.color=CandyHudVisuals.Muted;
            string[] titles = { "Resume", "Switch survivor / flying hand", "Respawn on top", "Rebuild castle", "Spawn AI survivor", "Fill to 10 players", "NPC personality: Balanced", "Clear survivors / stop auto-fill", "Spawn AI giant hand", "Hand personality: Playful", "Clear AI hands", "Local multiplayer / invites", "VR movement / turning" };
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
        public void Point(Camera camera, bool pressed)
        {
            var net=session.Network;
            var tracking=(session.PlayingHand?session.Hand.gameObject:session.Player.gameObject).GetComponent<DeadlyJenga.CandyPlayers.CandyTrackingSource>();
            string select=tracking&&tracking.HasHandTracking?"Look at a button · RIGHT pinch to select":"Look at a button · RIGHT trigger to select";
            heading.text=networkPage?"MULTIPLAYER\n"+(net?net.Status:"Finding nearby demos..."):"DEADLY JENGA\n"+select;
            if (networkPage && net && net.IsFusion)
            {
                for (int j = 0; j < buttons.Length; j++) buttons[j].gameObject.SetActive(j == 0 || j == 6 || j == 11);
                labels[0].text = "Resume"; labels[6].text = net.IsClient ? "Leave game" : "Close game for everyone"; labels[11].text = "Back";
                targets[0].enabled = targets[6].enabled = targets[11].enabled = true;
            }
            else if(comfortPage)
            {
                var p=session.Player;
                string[] titles={"Resume","Movement: "+p.vrMovement,"Right stick: "+p.vrTurn,"Snap amount: "+p.vrSnapAngle+"°","Smooth speed: "+p.vrSmoothDegreesPerSecond+"° / sec","Stationary / seated preset","Fluid / head-directed preset","Left stick moves · right stick turns","Physical head turns always look around","Stationary: movement follows rig facing","Fluid: movement follows head direction","Snap is the default comfort option","Back to demo controls"};
                for(int i=0;i<labels.Length;i++){labels[i].text=titles[i];targets[i].enabled=i<7||i==12;}
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
                labels[11].text=net&&net.IsFusion?"Multiplayer / leave":"Same-Wi-Fi invites"+(net&&net.HasPending?" · NEW":"");
                for(int i=0;i<buttons.Length;i++)
                {
                    bool shown=i<4||(i==11&&net);
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
                if(!buttons[i].gameObject.activeSelf)continue;
                bool hover = targets[i].Raycast(ray, out _, 4);
                buttons[i].image.color = hover ? CandyHudVisuals.Mint : CandyHudVisuals.Card;
                labels[i].color=hover?CandyHudVisuals.Ink:Color.white;
                if(hover){pointed=true;help.text=networkPage||comfortPage||i>=explanations.Length?select:explanations[i]+"\n"+select;}
                if (!hover || !pressed) continue;
                if(i==12){comfortPage=false;networkPage=false;break;}
                if(comfortPage)
                {
                    var p=session.Player;
                    if(i==0)session.SetMenu(false);if(i==1)p.CycleVRMovement();if(i==2)p.CycleVRTurn();
                    if(i==3)p.vrSnapAngle=p.vrSnapAngle<30?30:p.vrSnapAngle<45?45:p.vrSnapAngle<60?60:15;
                    if(i==4)p.vrSmoothDegreesPerSecond=p.vrSmoothDegreesPerSecond<60?60:p.vrSmoothDegreesPerSecond<90?90:45;
                    if(i==5)p.UseStationaryVR();if(i==6)p.UseFluidVR();break;
                }
                if(i==11){networkPage=!networkPage;break;}
                if(networkPage)
                {
                    if(i==0)session.SetMenu(false);
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
                if (i == 4) session.Bots.SpawnBot();
                if (i == 5) session.Bots.FillToTarget();
                if (i == 6) session.Bots.CyclePersonality();
                if (i == 7) session.Bots.ClearBots();
                if (i == 8) session.HandBots.SpawnHand();
                if (i == 9) session.HandBots.CyclePersonality();
                if (i == 10) session.HandBots.ClearHands();
                if(i>=4)break;
                session.SetMenu(false); break;
            }
            if(!pointed)help.text="LOOK to highlight · "+(tracking&&tracking.HasHandTracking?"RIGHT pinch selects\nLeft thumb + middle opens / closes":"RIGHT trigger selects\nY · LEFT opens / closes");
        }
    }
}
