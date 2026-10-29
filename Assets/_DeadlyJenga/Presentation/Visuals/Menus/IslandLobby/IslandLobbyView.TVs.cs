using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        void BuildTVWall()
        {
            EnsureKeyboardArt();
            var wall=new GameObject("Retro spectator television stack · 3 x 3").transform;
            wall.SetParent(transform,false);wall.localPosition=new Vector3(-4.8f,.12f,-4.7f);wall.localRotation=Quaternion.Euler(0,-135,0);
            Box(wall,"Television plinth",new Vector3(0,.10f,.10f),new Vector3(3.8f,.22f,.65f),new Color(.08f,.12f,.18f));
            var displays=new GameObject("Television displays",typeof(RectTransform),typeof(Canvas)).GetComponent<RectTransform>();
            displays.SetParent(wall,false);displays.localPosition=new Vector3(0,0,-.245f);displays.localScale=Vector3.one*.001f;
            displays.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var feeds=wall.gameObject.AddComponent<LobbySpectatorTVWall>();
            Color[] colors={new Color(.98f,.43f,.28f),new Color(.20f,.72f,.64f),new Color(.66f,.43f,.88f)};
            for(int i=0;i<9;i++)
            {
                float x=(i%3-1)*1.24f,y=.69f+(2-i/3)*.89f;
                Box(wall,"Retro CRT cabinet "+(i+1),new Vector3(x,y,.02f),new Vector3(1.20f,.84f,.46f),colors[(i+i/3)%3]);
                Box(wall,"Charcoal screen bezel "+i,new Vector3(x-.055f,y+.045f,-.217f),new Vector3(1.03f,.67f,.035f),new Color(.025f,.035f,.055f));
                for(int v=0;v<4;v++)Box(wall,"Speaker grille",new Vector3(x+.51f,y-.10f+v*.06f,-.22f),new Vector3(.052f,.018f,.015f),new Color(.12f,.14f,.18f));
                Ellipsoid(wall,"Tuning knob",new Vector3(x+.51f,y+.23f,-.245f),new Vector3(.09f,.09f,.045f),new Color(.85f,.9f,.94f));
                var image=new GameObject("Channel "+(i+1),typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(displays,false);image.rectTransform.sizeDelta=new Vector2(940,530);image.rectTransform.anchoredPosition=new Vector2((x-.055f)*1000,(y+.045f)*1000);image.raycastTarget=false;
                var label=Label(displays,"CH "+(i+1)+" · STANDBY",(y-.335f)*1000,42,1100,55);label.rectTransform.anchoredPosition=new Vector2(x*1000,(y-.335f)*1000);label.color=new Color(.88f,1,.96f);
                feeds.SetChannel(i,image,label);
            }
            var heading=Label(displays,"LIVE MAYHEM  /  SPECTATOR TV",2920,51,3700,85);heading.fontStyle=FontStyle.Bold;heading.color=new Color(.76f,1,.9f);
            lobby.AddBlocker(wall, new Vector3(0,1.48f,.03f),new Vector3(3.76f,2.95f,.55f));
            foreach(var item in wall.GetComponentsInChildren<Transform>(true))item.gameObject.layer=2;
            feeds.Initialize(session);
        }
    }
}
