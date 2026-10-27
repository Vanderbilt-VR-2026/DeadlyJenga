using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        void BuildControlCabinet(RectTransform screen,bool game)
        {
            EnsureKeyboardArt();
            var root=new GameObject(game?"Mode controller computer":"Host controller computer");
            root.transform.SetParent(transform,false);
            root.transform.SetPositionAndRotation(new Vector3(screen.position.x,0,screen.position.z),screen.rotation);
            Color accent=game?new Color(.85f,.35f,.18f):new Color(.08f,.57f,.46f);
            float width=screen.rect.width*Mathf.Abs(screen.lossyScale.x);
            float top=screen.position.y-screen.rect.height*Mathf.Abs(screen.lossyScale.y)*.5f+.10f;
            float height=top+.08f;
            Box(root.transform,"Flat grounded terminal wall",new Vector3(0,(top-.08f)*.5f,.09f),new Vector3(width,height,.18f),accent);
            Box(root.transform,"Flush control panel",new Vector3(0,top*.52f,-.008f),new Vector3(width-.14f,top*.73f,.016f),new Color(.025f,.035f,.065f));
            Box(root.transform,"Panel divider",new Vector3(0,top*.52f,-.02f),new Vector3(.018f,top*.52f,.008f),accent*.7f);
            lobby.AddBlocker(root.transform,new Vector3(0,(top-.08f)*.5f,.09f),new Vector3(width,height,.18f));
            var controls=new GameObject("Vertical controller face").transform;
            controls.SetParent(root.transform,false);controls.localPosition=new Vector3(0,top*.52f,-.05f);
            float fit=Mathf.Min((width-.28f)/1.8f,top*.70f/.72f);
            controls.localScale=Vector3.one*Mathf.Max(.25f,fit);
            BuildControllerArtwork(controls,game,game?CandyPeach:CandyMint);
            ApplyControllerFinish(root.transform.Find("Flush control panel").gameObject,false);
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
