using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeadlyJenga.Presentation;
using DeadlyJenga.Core;

namespace DeadlyJenga.CastlePrototype
{
    // The giant piano keyboard for typing room codes (point, click or press keys with a fingertip).
    public sealed partial class IslandLobbyView
    {
        RectTransform piano;
        Text keyboardCode;
        RectTransform keyboardGrabBar;
        Image keyboardGrabFace;
        bool draggingKeyboard;
        int keyboardDragDevice;
        float keyboardDragDepth;
        Vector3 keyboardDragOffset;
        Material keyboardMaterial;
        Mesh keyboardMesh;
        readonly Choice[] touching=new Choice[2];
        public Transform GiantKeyboard=>piano;
        void BuildKeyboard(RectTransform panel)
        {
            EnsureKeyboardArt();
            var go=new GameObject("Giant piano keyboard",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);
            var yaw=panel.rotation;
            go.transform.SetPositionAndRotation(new Vector3(panel.position.x,.94f,.55f),yaw*Quaternion.Euler(40,0,0));
            go.transform.localScale=Vector3.one*(1000*StationScale/1680);
            piano=go.GetComponent<RectTransform>();piano.sizeDelta=new Vector2(1680,740);var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.overrideSorting=true;canvas.sortingOrder=101;
            var scaler=go.AddComponent<CanvasScaler>();scaler.dynamicPixelsPerUnit=3.5f;scaler.referencePixelsPerUnit=100;
            Box(piano,"Glossy piano case",new Vector3(0,0,40),new Vector3(1680,740,120),new Color(.85f,.018f,.07f));
            if (!UsesMayhemMarina)
                lobby.AddBlocker(go.transform,new Vector3(0,0,40),new Vector3(1680,740,120));
            BuildArcadeCabinet();
            keyboardCode = Label(piano, "", 322, 42, 1160, 76);
            keyboardCode.fontStyle = FontStyle.Bold;
            var close = Add(piano, "keyboard-close", () => "×", 322, CloseRoomCodeKeyboard,
                () => lobby.CodeFocused, 722, 134, 74, () => lobby.CodeFocused);
            close.label.fontSize = close.label.resizeTextMaxSize = 50;
            keyboardCode.rectTransform.localPosition += Vector3.back * 68;
            close.button.transform.localPosition += Vector3.back * 68;
            string[] rows={"1234567890","QWERTYUIOP","ASDFGHJKL","ZXCVBNM-"};
            for(int row=0;row<rows.Length;row++)
            for(int col=0;col<rows[row].Length;col++)
            {
                char c=rows[row][col];float x=(col-(rows[row].Length-1)*.5f)*150;
                PhysicalKey("key-"+c,c.ToString(),x,225-row*120,134,()=>{lobby.FocusCode();lobby.TypeCode(c);},()=>lobby.JoinEnabled,new Color(.92f,.04f,.12f));
            }
            var back=PhysicalKey("backspace","\u232b",-560,-275,370,lobby.EraseCode,()=>lobby.JoinEnabled,new Color(1,.25f,.32f));
            back.label.enabled=false;
            var symbol=new GameObject("Backspace symbol",typeof(RectTransform),typeof(CanvasRenderer),typeof(LobbyBackspaceIcon));symbol.transform.SetParent(back.button.transform,false);symbol.GetComponent<RectTransform>().sizeDelta=new Vector2(100,58);symbol.GetComponent<RectTransform>().anchoredPosition3D=new Vector3(0,0,-68);back.keyIcon=symbol.GetComponent<LobbyBackspaceIcon>();back.keyIcon.raycastTarget=false;
            PhysicalKey("clear-code","CLEAR",-140,-275,370,()=>lobby.EnteredCode="",()=>lobby.JoinEnabled&&!ConfirmingJoin,new Color(1,.25f,.32f));
            PhysicalKey("keyboard-enter","ENTER / JOIN",480,-275,700,RequestJoinCodeChoice,()=>lobby.JoinEnabled&&lobby.CodeValid&&!ConfirmingJoin,new Color(1,.25f,.32f));
        }
        void BuildKeyboardGrabBar()
        {
            var grab = new GameObject("Keyboard grab bar", typeof(RectTransform), typeof(Image));
            keyboardGrabBar = (RectTransform)grab.transform;
            keyboardGrabBar.SetParent(piano, false);
            keyboardGrabBar.anchoredPosition3D = new Vector3(0, -389, -67);
            keyboardGrabBar.sizeDelta = new Vector2(510, 46);
            keyboardGrabFace = grab.GetComponent<Image>();
            keyboardGrabFace.sprite = CandyHudVisuals.Pill;
            keyboardGrabFace.type = Image.Type.Sliced;
            keyboardGrabFace.color = CandyHudVisuals.Muted;
            keyboardGrabFace.raycastTarget = false;
        }
        void PlaceFloatingKeyboard(Camera eye)
        {
            var forward = Vector3.ProjectOnPlane(eye.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) forward = Vector3.ProjectOnPlane(eye.transform.up, Vector3.up).normalized;
            piano.localScale = Vector3.one * (.79f / 1680f);
            piano.SetPositionAndRotation(eye.transform.position + forward * .91f - Vector3.up * .24f,
                Quaternion.LookRotation(forward) * Quaternion.Euler(18, 0, 0));
        }
        bool DragRoomCodeKeyboard(Ray ray, bool pressed, bool held, int device, float range, out float depth)
        {
            depth = 0;
            if (!keyboardGrabBar || !piano.gameObject.activeInHierarchy)
            {
                draggingKeyboard = false;
                return false;
            }
            if (draggingKeyboard && (!held || device != keyboardDragDevice))
                draggingKeyboard = false;
            bool hit = MenuAim.TryPoint(ray, keyboardGrabBar, range, out _, out var point);
            keyboardGrabFace.color = hit || draggingKeyboard ? CandyHudVisuals.Mint : CandyHudVisuals.Muted;
            if (draggingKeyboard)
            {
                var eye = Player.ViewCamera.transform.position;
                var offset = ray.GetPoint(keyboardDragDepth) + keyboardDragOffset - eye;
                float distance = offset.magnitude;
                if (distance > .15f)
                {
                    var forward = Vector3.ProjectOnPlane(offset, Vector3.up);
                    if (forward.sqrMagnitude > .04f)
                        piano.SetPositionAndRotation(eye + offset / distance * Mathf.Clamp(distance, .55f, 1.6f),
                            Quaternion.LookRotation(forward) * Quaternion.Euler(18, 0, 0));
                }
                depth = keyboardDragDepth;
                return true;
            }
            if (!hit) return false;
            depth = Vector3.Distance(ray.origin, point);
            if (pressed && held)
            {
                draggingKeyboard = true;
                keyboardDragDevice = device;
                keyboardDragDepth = depth;
                keyboardDragOffset = piano.position - point;
            }
            return true;
        }
        void CloseRoomCodeKeyboard()
        {
            lobby.UnfocusCode();
            UpdateMarinaKeyboard();
        }

        Choice PhysicalKey(string id,string title,float x,float y,float width,Action action,Func<bool> enabled,Color color)
        {
            var key=Add(piano,id,()=>title,y,action,enabled,x,width,100);key.physical=true;key.keyColor=color;key.button.image.color=Color.clear;
            key.collider.size=new Vector3(width,100,68);key.collider.center=new Vector3(0,0,-35);
            key.keyRenderer=Box(key.button.transform,"Rounded keycap",new Vector3(0,0,-35),new Vector3(width,100,60),color);key.keyVisual=key.keyRenderer.transform;
            key.keyProperties=new MaterialPropertyBlock();key.label.fontSize=44;key.label.resizeTextMaxSize=44;key.label.fontStyle=FontStyle.Bold;key.label.color=Color.white;key.label.rectTransform.anchoredPosition3D=new Vector3(0,0,-67);
            Image Glow(string name,float padding,float alpha)
            {
                var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(key.button.transform,false);var glow=go.GetComponent<Image>();glow.sprite=CandyHudVisuals.Pill;glow.type=Image.Type.Sliced;glow.fillCenter=false;glow.raycastTarget=false;glow.color=new Color(1,.62f,.68f,alpha);glow.rectTransform.sizeDelta=new Vector2(width+padding,100+padding);glow.rectTransform.anchoredPosition3D=new Vector3(0,0,-68);glow.enabled=false;return glow;
            }
            key.keyGlowSoft=Glow("Soft selection glow",44,.3f);key.keyGlow=Glow("Bright selection rim",22,1);
            return key;
        }
        void UpdatePhysicalKey(Choice key,bool hover)
        {
            bool enabled=key.enabled==null||key.enabled();float depression=Time.unscaledTime<key.pressedUntil?13:0;
            key.keyVisual.localPosition=new Vector3(0,0,-35+depression);key.label.rectTransform.anchoredPosition3D=new Vector3(0,0,-67+depression);
            key.keyProperties ??= new MaterialPropertyBlock();
            bool lit=enabled&&(hover||depression>0);
            Color color=!enabled?new Color(.18f,.14f,.25f):key.keyColor;
            key.keyProperties.SetColor("_BaseColor",color);
            key.keyProperties.SetColor("_EmissionColor",lit?CandyMint*1.2f:Color.black);
            key.keyRenderer.SetPropertyBlock(key.keyProperties);
            key.keyGlow.enabled=key.keyGlowSoft.enabled=lit;
            key.keyGlow.rectTransform.anchoredPosition3D=key.keyGlowSoft.rectTransform.anchoredPosition3D=new Vector3(0,0,-68+depression);
            if(key.keyIcon){key.keyIcon.color=enabled?CandyForeground(key.keyColor):CandyMuted;key.keyIcon.rectTransform.anchoredPosition3D=new Vector3(0,0,-68+depression);}
        }
        void UpdateKeyboardTouches()
        {
            var fingers=Player.gameObject.GetComponent<IFingerTips>();
            if(!Player.IsVR||fingers==null||!lobby.JoinEnabled||ConfirmingJoin){touching[0]=touching[1]=null;return;}
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
            if(lobby.JoinEnabled&&!ConfirmingJoin)
            foreach(var choice in choices)
            {
                if(!choice.physical||!choice.collider.gameObject.activeInHierarchy||!choice.collider.enabled||choice.enabled!=null&&!choice.enabled())continue;
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
            if(!keyboardMaterial){keyboardMaterial=new Material(localArt.purple);keyboardMaterial.name="Lobby candy stations (runtime)";keyboardMaterial.SetFloat("_Smoothness",.85f);keyboardMaterial.EnableKeyword("_EMISSION");keyboardMaterial.SetColor("_EmissionColor",Color.black);}
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
