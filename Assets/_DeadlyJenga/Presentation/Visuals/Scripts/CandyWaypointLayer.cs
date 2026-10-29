using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    // A bounded pool; fixed HUD panels and other markers reserve their screen space.
    public sealed class CandyWaypointLayer
    {
        readonly RectTransform root;
        readonly List<Text> labels=new List<Text>();
        readonly List<Rect> occupied=new List<Rect>();
        readonly Vector2 size=new Vector2(238,68);
        int used;
        public CandyWaypointLayer(RectTransform root){this.root=root;}
        public void Begin(bool mystery,bool choices=false)
        {
            used=0;occupied.Clear();
            occupied.Add(new Rect(255,-145,375,450));
            if(!mystery)occupied.Add(new Rect(-630,90,420,245));
            if(mystery){occupied.Add(new Rect(-630,-200,420,550));occupied.Add(new Rect(-235,-70,470,110));if(choices)occupied.Add(new Rect(-235,40,470,120));}
        }
        public void End(){for(int i=used;i<labels.Count;i++)CandyHudCards.Visible(labels[i],false);}
        public bool Show(Camera eye,Vector3 world,string title,Color accent)
        {
            if(!eye||used>=8)return false;
            var p=eye.WorldToViewportPoint(world);var delta=eye.transform.InverseTransformPoint(world);
            Vector2 projected;
            var canvas=root.GetComponent<Canvas>();
            if(p.z>0&&RectTransformUtility.ScreenPointToLocalPointInRectangle(root,eye.WorldToScreenPoint(world),canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:eye,out var local))projected=local;
            else projected=new Vector2(delta.x>=0?580:-580,-105);
            var bounds=new Rect(-600,-145,1200,325);Rect placed=default;bool found=false;
            for(int attempt=0;attempt<11;attempt++)
            {
                var point=projected+new Vector2(0,attempt==0?0:((attempt+1)/2)*(attempt%2==1?74:-74));
                point.x=Mathf.Clamp(point.x,bounds.xMin+size.x/2,bounds.xMax-size.x/2);point.y=Mathf.Clamp(point.y,bounds.yMin+size.y/2,bounds.yMax-size.y/2);
                var candidate=new Rect(point-size/2,size);bool overlaps=false;foreach(var reserved in occupied)if(reserved.Overlaps(candidate)){overlaps=true;break;}
                if(overlaps)
                {
                    point.x=Mathf.Clamp(point.x, -90,120);candidate=new Rect(point-size/2,size);overlaps=false;
                    foreach(var reserved in occupied)if(reserved.Overlaps(candidate)){overlaps=true;break;}
                }
                if(!overlaps){placed=candidate;found=true;break;}
            }
            if(!found)return false;
            Text label;if(used==labels.Count){label=CandyHudCards.Create(root,"Objective waypoint",Vector2.zero,size,18,TextAnchor.MiddleCenter);labels.Add(label);}else label=labels[used];used++;
            CandyHudCards.Place(label,placed.center,size);label.text=title+"\n<size=17>"+CandyHudContent.Direction(eye,world)+"</size>";label.color=Color.white;
            label.transform.parent.GetComponent<Image>().color=Color.Lerp(CandyHudCards.Background,accent,.12f);
            CandyHudCards.Visible(label,true);occupied.Add(placed);return true;
        }
    }
}
