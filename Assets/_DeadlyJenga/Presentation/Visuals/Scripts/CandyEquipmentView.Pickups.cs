using DeadlyJenga.Core;
using UnityEngine;
namespace DeadlyJenga.Presentation
{
    public sealed partial class CandyEquipmentView
    {
        static Material pickupCloud;static Mesh pickupCloudMesh;
        static readonly Color DroppedColor = new Color(1f,.65f,.19f);
        static readonly Color ReadyColor = new Color(.25f,.90f,1f);
        static readonly Color FocusColor = new Color(.65f,1f,.43f);
        Material pickupMarkerMaterial;

        PickupVisual CreatePickup(EquipmentKind kind,bool dropped)
        {
            var root=new GameObject("Visual - "+Name(kind)+" pickup").transform;
            root.SetParent(transform,false);
            if(kind==EquipmentKind.Speed||kind==EquipmentKind.Jump||kind==EquipmentKind.FlyingHand)
                BuildPickupCloud(root,kind==EquipmentKind.Jump?FocusColor:ReadyColor);
            var anchor=new GameObject("Pickup nameplate").transform;
            anchor.SetParent(root,false);
            anchor.localPosition=Vector3.up*.44f;
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var shadow=PickupLabel(anchor,font,"Pickup label shadow");
            shadow.transform.localPosition=new Vector3(.005f,-.006f,.005f);
            shadow.color=new Color(.035f,.045f,.07f,.96f);
            var label=PickupLabel(anchor,font,"Pickup label");

            if(!pickupMarkerMaterial)
            {
                var shader=Resources.Load<Shader>("CandyOrdnance");
                if(shader)
                {
                    pickupMarkerMaterial=new Material(shader){name="World loot markers"};
                    pickupMarkerMaterial.SetFloat("_Ribbon",1);
                }
            }
            var view=new PickupVisual{root=root,label=label,labelShadow=shadow,kind=kind,dropped=dropped};
            if(pickupMarkerMaterial)
            {
                view.ring=PickupLine(root,"Pickup ground ring",32,true);
                view.beam=PickupLine(root,"Pickup beacon",2,false);
            }
            return view;
        }

        static TextMesh PickupLabel(Transform parent,Font font,string name)
        {
            var label=new GameObject(name,typeof(TextMesh)).GetComponent<TextMesh>();
            label.transform.SetParent(parent,false);
            label.anchor=TextAnchor.LowerCenter;
            label.alignment=TextAlignment.Center;
            label.fontStyle=FontStyle.Bold;
            label.fontSize=48;
            label.characterSize=.007f;
            label.font=font;
            label.GetComponent<Renderer>().sharedMaterial=font.material;
            return label;
        }

        LineRenderer PickupLine(Transform root,string name,int count,bool loop)
        {
            var child=new GameObject(name).transform;
            child.SetParent(root,false);
            var line=child.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace=false;
            line.loop=loop;
            line.positionCount=count;
            line.sharedMaterial=pickupMarkerMaterial;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows=false;
            if(loop)
                for(int i=0;i<count;i++)
                {
                    float a=i*Mathf.PI*2/count;
                    line.SetPosition(i,new Vector3(Mathf.Cos(a)*.25f,-.20f,Mathf.Sin(a)*.25f));
                }
            else
            {
                line.SetPosition(0,new Vector3(0,-.20f,0));
                line.SetPosition(1,new Vector3(0,.88f,0));
            }
            return line;
        }

        void ShowPickup(PickupVisual view,EquipmentPickup pickup,Camera eye,bool focused)
        {
            bool ready=pickup.availableIn<=0;
            bool resting=pickup.station&&!ready;
            if(!pickup.station&&!ready)view.dropped=true;
            view.root.position=pickup.position;
            float distance=eye?(eye.transform.position-pickup.position).sqrMagnitude:float.PositiveInfinity;
            var tint=focused?FocusColor:view.dropped?DroppedColor:ReadyColor;
            if(view.ring)
            {
                view.ring.enabled=!resting&&distance<18*18;
                view.ring.transform.localScale=new Vector3(focused?1.24f:1,1,focused?1.24f:1);
                float width=focused?.041f:view.dropped?.029f:.022f;
                view.ring.startWidth=view.ring.endWidth=width;
                view.ring.startColor=view.ring.endColor=tint;
                view.beam.enabled=!resting&&distance<16*16;
                view.beam.startWidth=focused?.12f:view.dropped?.095f:.055f;
                view.beam.endWidth=.014f;
                view.beam.startColor=new Color(tint.r,tint.g,tint.b,focused?.70f:view.dropped?.52f:.30f);
                view.beam.endColor=new Color(tint.r,tint.g,tint.b,0);
            }
            if(view.model)
            {
                view.model.gameObject.SetActive(!resting);
                float settle=view.dropped&&!ready?Mathf.Clamp01((pickup.availableIn-.72f)/.28f):0;
                float bob=.025f*Mathf.Sin(Time.unscaledTime*2.4f+pickup.id*.27f);
                view.model.localPosition=Vector3.up*(bob+.38f*settle*settle);
                view.model.localRotation=Quaternion.Euler(9+settle*45,Time.unscaledTime*19+pickup.id*41,settle*25);
            }
            bool showLabel=eye&&(focused||distance<3.5f*3.5f);
            var anchor=view.label.transform.parent;
            anchor.gameObject.SetActive(showLabel);
            if(!showLabel)return;
            var facing=anchor.position-eye.transform.position;
            if(facing.sqrMagnitude>.0001f)anchor.rotation=Quaternion.LookRotation(facing);
            string detail=resting?"Ready in "+Mathf.CeilToInt(pickup.availableIn)+"s":
                !ready?"Dropped · ready shortly":view.dropped?"Dropped gear":pickup.station?"Weapon station":"";
            if(focused&&pickup.rounds>=0)detail=pickup.rounds+" rounds";
            string label=Name(pickup.kind)+(detail.Length>0?"\n"+detail:"");
            if(view.label.text!=label)view.label.text=view.labelShadow.text=label;
            view.label.color=resting?new Color(.70f,.74f,.79f):Color.Lerp(tint,Color.white,.30f);
        }

        void BuildPickupCloud(Transform root,Color color)
        {
            if(!pickupCloud){pickupCloud=new Material(Shader.Find("DeadlyJenga/Pickup Cloud"));pickupCloud.SetColor("_BaseColor",Color.white);}
            if(!pickupCloudMesh)
            {
                var source=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");pickupCloudMesh=Instantiate(source);var v=pickupCloudMesh.vertices;var bounds=source.bounds;
                for(int i=0;i<v.Length;i++){var p=(v[i]-bounds.center)/bounds.extents.magnitude;float noise=1+.13f*Mathf.Sin(p.x*17+p.z*11)*Mathf.Cos(p.y*13-p.x*8);v[i]=(v[i]-bounds.center)*noise/Mathf.Max(.001f,bounds.size.x);}
                pickupCloudMesh.vertices=v;pickupCloudMesh.RecalculateNormals();pickupCloudMesh.RecalculateBounds();
            }
            for(int i=0;i<2;i++)
            {
                var cloud=new GameObject("Translucent static pickup cloud",typeof(MeshFilter),typeof(MeshRenderer));cloud.transform.SetParent(root,false);cloud.transform.localScale=Vector3.one*(i==0?.63f:.57f);cloud.transform.localRotation=Quaternion.Euler(i*31,i*67,i*19);
                cloud.GetComponent<MeshFilter>().sharedMesh=pickupCloudMesh;var renderer=cloud.GetComponent<MeshRenderer>();renderer.sharedMaterial=pickupCloud;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",new Color(color.r,color.g,color.b,.12f));renderer.SetPropertyBlock(block);
            }
        }
        void UpdatePickupModel(PickupVisual view)
        {
            if(!view.model&&EquipmentModelData.Parts.TryGetValue(view.kind,out var parts))
            {
                view.model=new GameObject("Live equipment model").transform;view.model.SetParent(view.root,false);
                Bounds bounds=default;bool first=true;
                foreach(var part in parts)
                {
                    var go=new GameObject("Equipment geometry",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(view.model,false);go.transform.localPosition=part.local.GetColumn(3);go.transform.localRotation=part.local.rotation;go.transform.localScale=part.local.lossyScale;go.GetComponent<MeshFilter>().sharedMesh=part.mesh;
                    var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=part.material;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",part.color);renderer.SetPropertyBlock(block);renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    var b=renderer.bounds;if(first){bounds=b;first=false;}else bounds.Encapsulate(b);
                }
                if(!first){float scale=.52f/Mathf.Max(.1f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)));var center=view.model.InverseTransformPoint(bounds.center);var content=new GameObject("Centered model geometry").transform;var children=new System.Collections.Generic.List<Transform>();foreach(Transform child in view.model)children.Add(child);content.SetParent(view.model,false);foreach(var child in children)child.SetParent(content,false);content.localPosition=-center;view.model.localScale=Vector3.one*scale;view.model.localPosition=Vector3.zero;}
            }
        }
    }
}
