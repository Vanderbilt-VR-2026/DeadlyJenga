using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        LobbySpectatorTVWall spectatorWall;
        void BuildTVWall()
        {
            EnsureKeyboardArt();
            var wall=new GameObject("Retro spectator television stack").transform;
            wall.SetParent(transform,false);
            bool marina = lobby is IWorldQuery world && world.Radius >= 30;
            if(marina)
            {
                wall.SetLocalPositionAndRotation(new Vector3(5.10f,1.31f,-.65f),Quaternion.Euler(0,90,0));
                wall.localScale=Vector3.one*.88f;
            }
            else
                wall.SetLocalPositionAndRotation(new Vector3(-4.8f,.12f,-4.7f),Quaternion.Euler(0,-135,0));
            Box(wall,"Television plinth",new Vector3(0,.10f,.10f),new Vector3(3.8f,.22f,.65f),new Color(.08f,.12f,.18f));
            var displays=new GameObject("Television displays",typeof(RectTransform),typeof(Canvas)).GetComponent<RectTransform>();
            displays.SetParent(wall,false);displays.localPosition=new Vector3(0,0,-.253f);displays.localScale=Vector3.one*.001f;
            var canvas=displays.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var scaler=displays.gameObject.AddComponent<CanvasScaler>();scaler.dynamicPixelsPerUnit=3.5f;scaler.referencePixelsPerUnit=100;
            var feeds=spectatorWall=wall.gameObject.AddComponent<LobbySpectatorTVWall>();
            Color[] colors={new Color(.98f,.43f,.28f),new Color(.20f,.72f,.64f),new Color(.66f,.43f,.88f)};
            for(int i=0;i<LobbySpectatorTVWall.ChannelCount;i++)
            {
                int column=i%LobbySpectatorTVWall.Columns,row=i/LobbySpectatorTVWall.Columns;
                float x=(column-1)*1.24f,y=.69f+(LobbySpectatorTVWall.Rows-1-row)*.89f;
                Box(wall,"Retro CRT cabinet "+(i+1),new Vector3(x,y,.02f),new Vector3(1.20f,.84f,.46f),colors[(column+row+1)%colors.Length]);
                Box(wall,"Charcoal screen bezel "+i,new Vector3(x-.055f,y+.045f,-.217f),new Vector3(1.03f,.67f,.035f),new Color(.025f,.035f,.055f));
                for(int v=0;v<4;v++)Box(wall,"Speaker grille",new Vector3(x+.51f,y-.10f+v*.06f,-.22f),new Vector3(.052f,.018f,.015f),new Color(.12f,.14f,.18f));
                Ellipsoid(wall,"Tuning knob",new Vector3(x+.51f,y+.23f,-.245f),new Vector3(.09f,.09f,.045f),new Color(.85f,.9f,.94f));
                var image=new GameObject("Channel "+(i+1),typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(displays,false);image.rectTransform.sizeDelta=new Vector2(940,530);image.rectTransform.anchoredPosition=new Vector2((x-.055f)*1000,(y+.045f)*1000);image.raycastTarget=false;
                var label=Label(displays,"CH "+(i+1)+" · STANDBY",(y-.335f)*1000,42,1100,55);label.rectTransform.anchoredPosition=new Vector2(x*1000,(y-.335f)*1000);label.color=new Color(.88f,1,.96f);
                feeds.SetChannel(i,image,label);
            }
            var heading=Label(displays,"LIVE MAYHEM  /  SPECTATOR TV",2030,51,3700,85);heading.fontStyle=FontStyle.Bold;heading.color=new Color(.76f,1,.9f);
            lobby.AddBlocker(wall, new Vector3(0,1.03f,.03f),new Vector3(3.76f,2.05f,.55f));
            BuildBroadcastTV(wall, feeds, marina);
            foreach(var item in wall.GetComponentsInChildren<Transform>(true))item.gameObject.layer=2;
            var timer=Label(displays,"",2230,55,3700,90);
            timer.color=new Color(1,.87f,.46f);
            feeds.Initialize(session);
            feeds.SetMatchTimer(timer);
        }
        void BuildBroadcastTV(Transform wall, LobbySpectatorTVWall feeds, bool marina)
        {
            var cabinet = new GameObject("Live match broadcast TV").transform;
            cabinet.SetParent(wall, false);
            // Both displays face the open side of the plaza.
            var position = marina ? new Vector3(3.25f, 0, -1.45f) : new Vector3(3.25f, 0, -.12f);
            cabinet.SetLocalPositionAndRotation(position, Quaternion.Euler(0, 12, 0));

            var charcoal = new Color(.045f, .06f, .085f);
            var mint = new Color(.66f, 1, .87f);
            Box(cabinet, "Broadcast TV stand", new Vector3(0, .34f, .05f), new Vector3(2.45f, .68f, .72f), charcoal);
            Box(cabinet, "Oversized CRT cabinet", new Vector3(0, 1.55f, .05f),
                new Vector3(2.75f, 1.78f, .73f), new Color(.31f, .71f, .65f));
            Box(cabinet, "Broadcast screen bezel", new Vector3(-.19f, 1.60f, -.329f),
                new Vector3(2.25f, 1.35f, .04f), charcoal);
            for (int i = 0; i < 7; i++)
                Box(cabinet, "Broadcast speaker grille", new Vector3(1.14f, 1.36f + i * .072f, -.33f),
                    new Vector3(.23f, .024f, .015f), charcoal);
            Ellipsoid(cabinet, "Broadcast tuning knob", new Vector3(1.14f, 1.13f, -.35f),
                new Vector3(.19f, .19f, .085f), new Color(.93f, .88f, .71f));
            Ellipsoid(cabinet, "Broadcast power light", new Vector3(1.14f, 2, -.35f),
                new Vector3(.085f, .085f, .035f), mint);

            var display = new GameObject("Broadcast display", typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
            display.SetParent(cabinet, false);
            display.localPosition = new Vector3(0, 0, -.352f);
            display.localScale = Vector3.one * .001f;
            display.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var screen = new GameObject("Live rotating cameras", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            screen.transform.SetParent(display, false);
            screen.rectTransform.anchoredPosition = new Vector2(-190, 1600);
            screen.rectTransform.sizeDelta = new Vector2(2112, 1188);
            screen.raycastTarget = false;
            var title = Label(display, "MATCH BROADCAST", 2310, 72, 2400, 90);
            title.fontStyle = FontStyle.Bold;
            title.color = mint;
            var status = Label(display, "STANDBY", 800, 48, 2300, 70);
            status.color = mint;
            feeds.SetBroadcast(screen, status);
            lobby.AddBlocker(cabinet, new Vector3(0, 1.20f, .05f), new Vector3(2.75f, 2.4f, .73f));
        }
    }
}
