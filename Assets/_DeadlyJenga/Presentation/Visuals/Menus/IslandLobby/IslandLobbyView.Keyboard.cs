using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // The giant piano keyboard for typing room codes (point, click or press keys with a fingertip).
    public sealed partial class IslandLobbyView
    {
        RectTransform piano;
        Material keyboardMaterial;
        Mesh keyboardMesh;
        readonly Choice[] touching=new Choice[2];
        public Transform GiantKeyboard=>piano;
        void BuildKeyboard(RectTransform panel)
        {
            EnsureKeyboardArt();
            var go=new GameObject("Giant piano keyboard",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);
            var yaw=panel.rotation;
            go.transform.SetPositionAndRotation(panel.position+yaw*new Vector3(0,-.88f,-.95f),yaw*Quaternion.Euler(65,0,0));go.transform.localScale=Vector3.one*.0024f;
            piano=go.GetComponent<RectTransform>();piano.sizeDelta=new Vector2(1680,740);var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.overrideSorting=true;canvas.sortingOrder=101;
            var body=Box(piano,"Glossy piano case",new Vector3(0,0,40),new Vector3(1680,740,120),new Color(.18f,.08f,.32f));
            lobby.AddBlocker(go.transform,new Vector3(0,0,40),new Vector3(1680,740,120));   // the lobby makes the piano solid
            string[] rows={"1234567890","QWERTYUIOP","ASDFGHJKL","ZXCVBNM-"};
            for(int row=0;row<rows.Length;row++)
            for(int col=0;col<rows[row].Length;col++)
            {
                char c=rows[row][col];float x=(col-(rows[row].Length-1)*.5f)*150;
                PhysicalKey("key-"+c,c.ToString(),x,225-row*120,134,()=>{lobby.FocusCode();lobby.TypeCode(c);},()=>lobby.JoinEnabled,new Color(.79f,.68f,1));
            }
            PhysicalKey("backspace","ERASE",-560,-275,370,lobby.EraseCode,()=>lobby.JoinEnabled,new Color(1,.72f,.63f));
            PhysicalKey("clear-code","CLEAR",-140,-275,370,()=>lobby.EnteredCode="",()=>lobby.JoinEnabled,new Color(1,.72f,.63f));
            PhysicalKey("keyboard-enter","ENTER / JOIN",480,-275,700,lobby.Join,()=>lobby.JoinEnabled&&lobby.CodeValid,CandyHudVisuals.Mint);
        }
        void PhysicalKey(string id,string title,float x,float y,float width,Action action,Func<bool> enabled,Color color)
        {
            var key=Add(piano,id,()=>title,y,action,enabled,x,width,100);key.physical=true;key.keyColor=color;key.button.image.color=Color.clear;
            key.collider.size=new Vector3(width,100,68);key.collider.center=new Vector3(0,0,-35);
            key.keyRenderer=Box(key.button.transform,"Rounded keycap",new Vector3(0,0,-35),new Vector3(width,100,60),color);key.keyVisual=key.keyRenderer.transform;
            key.keyProperties=new MaterialPropertyBlock();key.label.fontSize=44;key.label.resizeTextMaxSize=44;key.label.fontStyle=FontStyle.Bold;key.label.color=CandyHudVisuals.Ink;key.label.rectTransform.anchoredPosition3D=new Vector3(0,0,-67);
        }
        void UpdatePhysicalKey(Choice key,bool hover)
        {
            bool enabled=key.enabled==null||key.enabled();float depression=Time.unscaledTime<key.pressedUntil?13:0;
            key.keyVisual.localPosition=new Vector3(0,0,-35+depression);key.label.rectTransform.anchoredPosition3D=new Vector3(0,0,-67+depression);
            key.keyProperties ??= new MaterialPropertyBlock();
            Color color=!enabled?new Color(.16f,.13f,.22f):hover||depression>0?CandyHudVisuals.Mint:key.keyColor;
            key.keyProperties.SetColor("_BaseColor",color);key.keyRenderer.SetPropertyBlock(key.keyProperties);
        }
        void UpdateKeyboardTouches()
        {
            var fingers=Player.gameObject.GetComponent<IFingerTips>();
            if(!Player.IsVR||fingers==null||!lobby.JoinEnabled){touching[0]=touching[1]=null;return;}
            for(int side=0;side<2;side++)
            {
                if(!fingers.TryIndexTip(side==0,out var tip)){touching[side]=null;continue;}
                TouchKeyboard(tip,side);
            }
        }
        public bool TouchKeyboard(Vector3 point,int hand)
        {
            if(hand<0||hand>1)return false;
            Choice hit=null;float distance=.018f*.018f;
            if(lobby.JoinEnabled)
            foreach(var choice in choices)
            {
                if(!choice.physical||!choice.collider.enabled||choice.enabled!=null&&!choice.enabled())continue;
                if(choice.collider.transform.InverseTransformPoint(point).z>-10)continue;
                float d=(choice.collider.ClosestPoint(point)-point).sqrMagnitude;
                if(d<=distance){hit=choice;distance=d;}
            }
            if(hit==touching[hand])return false;
            touching[hand]=hit;
            if(hit==null||Time.unscaledTime<hit.pressedUntil)return false;
            return InvokeChoice(hit.id);
        }
        void EnsureKeyboardArt()
        {
            if(!keyboardMesh)keyboardMesh=RoundedBox();
            if(!keyboardMaterial){keyboardMaterial=new Material(localArt.purple);keyboardMaterial.name="Lobby candy stations (runtime)";keyboardMaterial.SetFloat("_Smoothness",.85f);}
        }
        Renderer Box(Transform parent,string name,Vector3 position,Vector3 scale,Color color)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<MeshFilter>().sharedMesh=keyboardMesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=keyboardMaterial;
            var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",color);renderer.SetPropertyBlock(properties);return renderer;
        }
        static Mesh RoundedBox()
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            float[] grid={-.5f,-.4f,0,.4f,.5f};
            void Face(Vector3 normal,Vector3 u,Vector3 v)
            {
                int start=vertices.Count;
                for(int y=0;y<5;y++)for(int x=0;x<5;x++)
                {var p=normal*.5f+u*grid[x]+v*grid[y];var inner=new Vector3(Mathf.Clamp(p.x,-.4f,.4f),Mathf.Clamp(p.y,-.4f,.4f),Mathf.Clamp(p.z,-.4f,.4f));var n=(p-inner).normalized;vertices.Add(inner+n*.1f);normals.Add(n);}
                for(int y=0;y<4;y++)for(int x=0;x<4;x++){int a=start+y*5+x;triangles.AddRange(new[]{a,a+1,a+5,a+1,a+6,a+5});}
            }
            Face(Vector3.forward,Vector3.right,Vector3.up);Face(Vector3.back,Vector3.left,Vector3.up);Face(Vector3.up,Vector3.right,Vector3.back);Face(Vector3.down,Vector3.right,Vector3.forward);Face(Vector3.right,Vector3.back,Vector3.up);Face(Vector3.left,Vector3.forward,Vector3.up);
            var mesh=new Mesh{name="Rounded candy station box"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
    }
}
