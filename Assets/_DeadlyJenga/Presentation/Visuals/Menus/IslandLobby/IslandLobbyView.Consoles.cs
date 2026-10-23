using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        void BuildControlCabinet(RectTransform screen,bool game)
        {
            EnsureKeyboardArt();
            var root=new GameObject(game?"Mode controller computer":"Host controller computer");
            root.transform.SetParent(transform,false);root.transform.position=new Vector3(screen.position.x,0,.70f);
            Color accent=game?new Color(.85f,.35f,.18f):new Color(.08f,.57f,.46f);
            Box(root.transform,"Grounded computer pedestal",new Vector3(0,.43f,.14f),new Vector3(1.55f,1.16f,.75f),accent);
            Box(root.transform,"Dark control deck",new Vector3(0,1.03f,-.04f),new Vector3(1.62f,.12f,.88f),new Color(.025f,.035f,.065f));
            Box(root.transform,"Monitor support",new Vector3(0,1.39f,.55f),new Vector3(.25f,.70f,.18f),accent);
            Box(root.transform,"Candy deck rim",new Vector3(0,1,-.49f),new Vector3(1.6f,.05f,.045f),accent*1.3f);
            lobby.AddBlocker(root.transform,new Vector3(0,.48f,.07f),new Vector3(1.62f,1.22f,.95f));
            // These oversized controls are scenery, not extra UI or input targets.
            if(game)
            {
                Ellipsoid(root.transform,"Joystick socket",new Vector3(-.41f,1.10f,-.06f),new Vector3(.46f,.06f,.46f),Color.black);
                Box(root.transform,"Joystick stem",new Vector3(-.41f,1.27f,-.06f),new Vector3(.075f,.32f,.075f),new Color(.28f,.32f,.37f));
                Ellipsoid(root.transform,"Joystick candy grip",new Vector3(-.41f,1.44f,-.06f),new Vector3(.29f,.29f,.29f),accent);
                string[] names={"A","B","X","Y"};Color[] colors={new Color(.12f,.9f,.35f),new Color(1,.12f,.18f),new Color(.15f,.48f,1),new Color(1,.8f,.08f)};
                Vector3[] offsets={new Vector3(0,0,-.19f),new Vector3(.19f,0,0),new Vector3(-.19f,0,0),new Vector3(0,0,.19f)};
                for(int i=0;i<4;i++)
                {
                    var position=new Vector3(.40f,1.14f,-.06f)+offsets[i];
                    Ellipsoid(root.transform,"Decorative "+names[i]+" button",position,new Vector3(.19f,.09f,.19f),colors[i]);
                    var label=new GameObject(names[i]+" legend");label.transform.SetParent(root.transform,false);label.transform.localPosition=position+Vector3.up*.055f;label.transform.localRotation=Quaternion.Euler(90,0,0);
                    var text=label.AddComponent<TextMesh>();text.text=names[i];text.fontSize=48;text.characterSize=.045f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
                }
            }
            else
            {
                Box(root.transform,"D-pad horizontal",new Vector3(-.4f,1.15f,-.06f),new Vector3(.54f,.13f,.18f),new Color(.065f,.08f,.12f));
                Box(root.transform,"D-pad vertical",new Vector3(-.4f,1.15f,-.06f),new Vector3(.18f,.13f,.54f),new Color(.065f,.08f,.12f));
                Ellipsoid(root.transform,"Scroll wheel",new Vector3(.40f,1.18f,-.06f),new Vector3(.24f,.29f,.56f),new Color(.25f,.28f,.35f));
                for(int i=0;i<11;i++)Box(root.transform,"Scroll wheel grip "+i,new Vector3(.4f,1.327f,-.29f+i*.046f),new Vector3(.25f,.018f,.018f),accent);
            }
        }
        void Ellipsoid(Transform parent,string name,Vector3 position,Vector3 size,Color color)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            if(!consoleSphere)consoleSphere=BuildConsoleSphere();go.GetComponent<MeshFilter>().sharedMesh=consoleSphere;go.GetComponent<MeshRenderer>().sharedMaterial=keyboardMaterial;
            var properties=new MaterialPropertyBlock();properties.SetColor("_BaseColor",color);go.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
        }
        Mesh consoleSphere;
        static Mesh BuildConsoleSphere()
        {
            var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
            const int slices=24,rings=12;
            for(int y=0;y<=rings;y++)for(int x=0;x<=slices;x++){float a=x*Mathf.PI*2/slices,b=y*Mathf.PI/rings;vertices.Add(new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a))*.5f);}
            for(int y=0;y<rings;y++)for(int x=0;x<slices;x++){int a=y*(slices+1)+x;triangles.AddRange(new[]{a,a+1,a+slices+1,a+1,a+slices+2,a+slices+1});}
            var mesh=new Mesh{name="Candy console rounded control"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
