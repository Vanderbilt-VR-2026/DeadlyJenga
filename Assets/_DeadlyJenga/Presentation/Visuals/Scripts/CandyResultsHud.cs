using System;
using System.Linq;
using System.Text;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    public sealed class CandyResultsHud : MonoBehaviour
    {
        Canvas canvas; Text title, rows, secondaryRows, hint;GUISkin theme;
        readonly BoxCollider[] targets = new BoxCollider[4];
        readonly Text[] labels = new Text[4];
        MatchPlayerFrame[] sorted = Array.Empty<MatchPlayerFrame>(), infectedSorted=Array.Empty<MatchPlayerFrame>();
        RoundPhase previous; int page,killPage; bool visible; Camera camera; Vector3 openingForward;
        readonly StringBuilder text = new StringBuilder();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => GameEvents.SessionStarted += go => {if(!go.GetComponent<CandyResultsHud>())go.AddComponent<CandyResultsHud>();};
        void Update()
        {
            var match = MatchRuntime.Current; var session = Services.Session;
            if (match == null || session == null) return;
            var frame = match.Frame;
            visible = frame.phase == RoundPhase.Results;
            if (visible && previous != RoundPhase.Results)
            {
                sorted = frame.players.OrderByDescending(p => p.metric).ThenBy(p => p.name).ToArray();infectedSorted=frame.players.OrderByDescending(p=>p.kills).ThenBy(p=>p.name).ToArray();
                page = killPage = 0; session.SetMenu(true);
                openingForward=session.ActiveCamera?Vector3.ProjectOnPlane(session.ActiveCamera.transform.forward,Vector3.up).normalized:Vector3.forward;
                if(openingForward.sqrMagnitude<.1f)openingForward=Vector3.forward;
            }
            previous = frame.phase;
            if (!visible) { if (canvas) canvas.gameObject.SetActive(false); return; }
            if (!canvas) Build();
            camera=session.ActiveCamera;
            title.text = (string.IsNullOrEmpty(frame.winner) ? "ROUND COMPLETE" : frame.winner.ToUpperInvariant() + " WINS") + "\n" + frame.metricLabel;
            bool infection=frame.rules.mode==GameModeKind.Infection;secondaryRows.gameObject.SetActive(infection);rows.rectTransform.sizeDelta=new Vector2(820,infection?170:350);rows.rectTransform.anchoredPosition=new Vector2(0,infection?115:25);rows.fontSize=infection?21:23;
            text.Clear();if(infection)text.Append("LONGEST SURVIVORS\n");
            int pageSize=infection?5:10;int first = page * pageSize;
            for (int i = first; i < Mathf.Min(first + pageSize, sorted.Length); i++)
            {
                var p = sorted[i]; string color = ColorUtility.ToHtmlStringRGB(Color.Lerp(p.color,Color.white,.55f));
                text.Append(i + 1).Append(". <color=#").Append(color).Append('>').Append(p.name).Append("</color>    ").Append(p.metric.ToString("0.0"));
                if (infection) text.Append(" sec");
                text.Append('\n');
            }
            rows.text = text.ToString();
            if(infection){text.Clear();text.Append("INFECTION KILLS\n");for(int i=killPage*5;i<Mathf.Min(killPage*5+5,infectedSorted.Length);i++){var p=infectedSorted[i];text.Append(i+1).Append(". ").Append(p.name).Append("    ").Append(p.kills).Append('\n');}secondaryRows.text=text.ToString();}
            hint.text=infection?"Survivors "+(page+1)+" / "+Mathf.Max(1,Mathf.CeilToInt(sorted.Length/5f))+"   ·   Kills "+(killPage+1)+" / "+Mathf.Max(1,Mathf.CeilToInt(infectedSorted.Length/5f)):"Page "+(page+1)+" / "+Mathf.Max(1,Mathf.CeilToInt(sorted.Length/10f))+"  ·  Look and select";labels[2].text=infection?"NEXT SURVIVORS":"PREVIOUS";labels[3].text=infection?"NEXT KILLS":"NEXT";
            labels[0].text = match.Authority ? "NEXT ROUND" : "WAITING FOR HOST";
            if(!session.IsVR){canvas.gameObject.SetActive(false);return;}
            canvas.gameObject.SetActive(true);if(!camera)return;
            canvas.transform.SetParent(transform,false);canvas.transform.position=camera.transform.position+openingForward*1.8f;
            canvas.transform.rotation=Quaternion.LookRotation(openingForward,Vector3.up);canvas.transform.localScale=Vector3.one*.002f;
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            for (int i = 0; i < targets.Length; i++)
                if (targets[i].Raycast(ray, out _, 4) && session.MenuSelectPressed) Act(i);
        }
        void Act(int index)
        {
            var match = MatchRuntime.Current; if (match == null) return;
            if (index == 0 && match.Authority) { match.StartRound(); Services.Session.SetMenu(false); }
            if (index == 1) match.ReturnToLobby();
            if(index==2){if(match.Rules.mode==GameModeKind.Infection)page=(page+1)%Mathf.Max(1,Mathf.CeilToInt(sorted.Length/5f));else page=Mathf.Max(0,page-1);}
            if(index==3){if(match.Rules.mode==GameModeKind.Infection)killPage=(killPage+1)%Mathf.Max(1,Mathf.CeilToInt(infectedSorted.Length/5f));else page=Mathf.Min(Mathf.Max(0,(sorted.Length-1)/10),page+1);}
        }
        void Build()
        {
            var root = new GameObject("Visual - round results", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(transform, false); canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(900, 640);
            var pane = new GameObject("Candy panel", typeof(RectTransform), typeof(Image)); pane.transform.SetParent(root.transform, false);
            var background = pane.GetComponent<Image>(); background.color = new Color(.055f, .025f, .12f, .98f); background.rectTransform.sizeDelta = rect.sizeDelta;
            title = Label("Result", new Vector2(820, 80), new Vector2(0, 260), 28);
            rows = Label("Scores", new Vector2(820, 350), new Vector2(0, 25), 23); rows.alignment = TextAnchor.UpperLeft;
            secondaryRows=Label("Infected kills",new Vector2(820,170),new Vector2(0,-70),21);secondaryRows.alignment=TextAnchor.UpperLeft;
            hint = Label("Paging", new Vector2(820, 40), new Vector2(0, -185), 18);
            string[] names = { "NEXT ROUND", "RETURN TO LOBBY", "PREVIOUS", "NEXT" };
            for (int i = 0; i < 4; i++)
            {
                labels[i] = Label(names[i], new Vector2(195, 52), new Vector2(-315 + i * 210, -265), 19);
                labels[i].color = new Color(.2f, 1, .78f); targets[i] = labels[i].gameObject.AddComponent<BoxCollider>(); targets[i].isTrigger = true; targets[i].size = new Vector3(195, 52, 1);
            }
        }
        Text Label(string name, Vector2 size, Vector2 position, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(canvas.transform, false);
            var label = go.GetComponent<Text>();label.text=name; label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = fontSize; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.sizeDelta = size; label.rectTransform.anchoredPosition = position; return label;
        }
        void OnGUI()
        {
            if (!visible || Services.Session?.IsVR == true || MatchRuntime.Current == null) return;
            var oldSkin=GUI.skin;int oldDepth=GUI.depth;
            if(!theme){theme=Instantiate(GUI.skin);theme.label.fontSize=20;theme.label.normal.textColor=Color.white;theme.button.fontSize=19;theme.button.normal.textColor=Color.white;theme.button.hover.textColor=Color.white;theme.button.active.textColor=Color.white;theme.button.normal.background=DeadlyJenga.CastlePrototype.CandyHudVisuals.Rounded;theme.box.normal.background=DeadlyJenga.CastlePrototype.CandyHudVisuals.Rounded;theme.box.padding=new RectOffset(24,24,20,20);}
            GUI.skin=theme;GUI.depth = -50;
            var frame = MatchRuntime.Current.Frame;
            float width = Mathf.Min(Screen.width - 30, 860), height = Mathf.Min(Screen.height - 30, 590);
            var old = GUI.backgroundColor; GUI.backgroundColor = new Color(.18f, .07f, .32f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height), GUI.skin.box);
            GUILayout.Label((string.IsNullOrEmpty(frame.winner) ? "ROUND COMPLETE" : frame.winner + " WINS") + "  ·  " + frame.metricLabel);
            bool infection=frame.rules.mode==GameModeKind.Infection;int pageSize=infection?5:10;
            if(infection)GUILayout.Label("LONGEST SURVIVORS");
            for(int i=page*pageSize;i<Mathf.Min(page*pageSize+pageSize,sorted.Length);i++){var p=sorted[i];GUILayout.Label((i+1)+". "+p.name+"    "+p.metric.ToString("0.0")+(infection?" sec":""));}
            if(infection){GUILayout.Space(12);GUILayout.Label("INFECTION KILLS");for(int i=killPage*5;i<Mathf.Min(killPage*5+5,infectedSorted.Length);i++){var p=infectedSorted[i];GUILayout.Label((i+1)+". "+p.name+"    "+p.kills);}}
            GUILayout.FlexibleSpace(); GUILayout.BeginHorizontal();
            bool enabled = GUI.enabled; GUI.enabled = MatchRuntime.Current.Authority; if (GUILayout.Button("NEXT ROUND", GUILayout.Height(48))) Act(0); GUI.enabled = enabled;
            if (GUILayout.Button("RETURN TO LOBBY", GUILayout.Height(48))) Act(1); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); if (GUILayout.Button(infection?"NEXT SURVIVORS":"PREVIOUS")) Act(2); GUILayout.Label(infection?"Survivors "+(page+1)+" · Kills "+(killPage+1):"Page "+(page+1)); if (GUILayout.Button(infection?"NEXT KILLS":"NEXT")) Act(3); GUILayout.EndHorizontal();
            GUILayout.EndArea(); GUI.backgroundColor = old;GUI.depth=oldDepth;GUI.skin=oldSkin;
        }
        void OnDestroy() { if(theme)Destroy(theme);if (canvas) Destroy(canvas.gameObject); }
    }
}
