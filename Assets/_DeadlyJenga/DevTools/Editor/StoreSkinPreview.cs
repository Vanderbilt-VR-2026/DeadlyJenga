using System;
using System.Collections.Generic;
using DeadlyJenga.CandyPlayers;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    sealed class StoreSkinPreview : IDisposable
    {
        public static readonly HashSet<AvatarSkin> Catalog = new HashSet<AvatarSkin>
        {
            AvatarSkin.Fox, AvatarSkin.Penguin, AvatarSkin.Axolotl, AvatarSkin.Dragon,
            AvatarSkin.Clownfish, AvatarSkin.CandyCorn, AvatarSkin.Gingerbread, AvatarSkin.Astronaut
        };

        readonly List<Object> owned = new List<Object>();
        CandyPlayerRig rig;
        readonly Color? teamColor;

        public StoreSkinPreview(Color? teamColor = null) => this.teamColor = teamColor;

        Material ivory, dark;

        public void Apply(CandyPlayerRig avatar, AvatarSkin skin)
        {
            rig = avatar;
            foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = renderer == rig.bodyRenderer || renderer == rig.headRenderer;
            ivory = Finish("Vanilla", "FFF0D5", .63f, .08f);
            dark = Finish("Dark candy", "251923", .76f, .035f);
            switch (skin)
            {
                case AvatarSkin.Fox: Fox(); break;
                case AvatarSkin.Penguin: Penguin(); break;
                case AvatarSkin.Axolotl: Axolotl(); break;
                case AvatarSkin.Dragon: Dragon(); break;
                case AvatarSkin.Clownfish: Clownfish(); break;
                case AvatarSkin.CandyCorn: CandyCorn(); break;
                case AvatarSkin.Gingerbread: Gingerbread(); break;
                case AvatarSkin.Astronaut: Astronaut(); break;
                default: throw new ArgumentOutOfRangeException(nameof(skin));
            }
        }

        void Fox()
        {
            var amber = Finish("Burnt orange glaze", "D96127");
            var toast = Finish("Toasted caramel", "79402C");
            Body(amber, new Vector3(1.13f, 1f, 1.04f), .08f);
            Face(.19f, .04f, .087f);
            for (int side = -1; side <= 1; side += 2)
            {
                var earBase = new Vector3(side * .125f, .14f, -.022f);
                var earTip = new Vector3(side * .252f, .375f, -.045f);
                Taper("Fox ear", rig.HeadAnchor, amber, earBase, earTip, .092f, .68f);
                Taper("Toast ear insert", rig.HeadAnchor, toast, earBase + new Vector3(0,.043f,.048f),
                    earTip + new Vector3(-side*.018f,-.025f,.027f), .045f, .48f);
                Ball("Vanilla cheek", rig.HeadAnchor, ivory, new Vector3(side*.097f,-.073f,.152f),
                    new Vector3(.119f,.079f,.078f), Quaternion.Euler(0,side*16,side*13));
                Foot(side, toast, .097f, .146f);
            }
            Ball("Fox nose", rig.HeadAnchor, dark, new Vector3(0,-.058f,.258f), new Vector3(.052f,.031f,.025f));
            Belly(ivory, .137f, .218f, .98f, true);
            var tail = rig.BodyAnchor;
            Petal("Caramel brush tail", tail, amber, new Vector3(0,.68f,-.10f), new Vector3(.37f,.91f,-.44f), .117f,.083f);
            Petal("Dipped tail tip", tail, ivory, new Vector3(.26f,.82f,-.36f), new Vector3(.43f,.975f,-.49f), .079f,.066f);
        }

        void Penguin()
        {
            var blueberry = Finish("Blueberry shell", "243D62", .78f, .075f);
            var citrus = Finish("Tangerine candy", "F68E35");
            Body(blueberry, new Vector3(1.07f,.96f,1.03f), .28f);
            Belly(ivory, .169f,.249f,.97f);
            for (int side = -1; side <= 1; side += 2)
            {
                Ball("Ivory eye patch", rig.HeadAnchor, ivory, new Vector3(side*.076f,.041f,.174f),new Vector3(.089f,.111f,.051f));
                Foot(side, citrus, .125f,.182f);
            }
            Face(.213f, .046f, .077f);
            Petal("Rounded tangerine beak", rig.HeadAnchor, citrus,
                new Vector3(0,-.059f,.17f),new Vector3(0,-.074f,.343f),.067f,.038f);
        }

        void Axolotl()
        {
            var rose = Finish("Strawberry rose", "EC87AC");
            var berry = Finish("Raspberry gills", "CB477B", .70f);
            var tip = Finish("Pink sugar tips", "FBB4C9", .68f);
            Body(rose,new Vector3(1.24f,.91f,1.04f),.13f);
            Belly(tip,.139f,.19f,.957f);
            Face(.191f,.028f,.105f);
            Smile(rig.HeadAnchor, new Vector3(0,-.068f,.215f), dark, .046f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    var start = new Vector3(side*.166f, .088f-i*.064f, -.018f);
                    var end = new Vector3(side*(.366f+i*.017f), .225f-i*.135f, -.038f-i*.034f);
                    Petal("Raspberry gill",rig.HeadAnchor,berry,start,end,.047f,.029f);
                    Petal("Pale gill tip",rig.HeadAnchor,tip,Vector3.Lerp(start,end,.55f),end,.032f,.023f);
                }
            }
            Petal("Axolotl tail",rig.BodyAnchor,rose,new Vector3(0,.67f,-.10f),new Vector3(.15f,.78f,-.40f),.088f,.057f);
        }

        void Dragon()
        {
            var jade = Finish("Jade candy glaze", "328864", .82f);
            var mint = Finish("Lime belly candy", "B5CA79", .66f);
            var amber = Finish("Amber horn sugar", "F1B555", .73f);
            var cherry = Finish("Burnt cherry wings", "9F423A", .58f, .09f);
            cherry.SetFloat("_Cull", (float)CullMode.Off);
            Body(jade, new Vector3(1.13f, .98f, 1.10f), .16f);
            Belly(mint, .139f, .215f, .97f, true);
            Ball("Dragon lower jaw", rig.HeadAnchor, mint, new Vector3(0, -.12f, .16f),
                new Vector3(.142f, .058f, .126f));
            Ball("Jade snout", rig.HeadAnchor, jade, new Vector3(0, -.066f, .186f),
                new Vector3(.151f, .084f, .14f));

            for (int side = -1; side <= 1; side += 2)
            {
                var horn = new Vector3(side * .142f, .127f, -.025f);
                Taper("Swept amber horn", rig.HeadAnchor, amber, horn,
                    new Vector3(side * .271f, .411f, -.177f), .083f, .86f);
                Taper("Secondary cheek horn", rig.HeadAnchor, amber,
                    new Vector3(side * .167f, -.055f, -.022f),
                    new Vector3(side * .266f, .057f, -.137f), .046f, .76f);

                var eye = new Vector3(side * .101f, .065f, .176f);
                Ball("Dragon eye socket", rig.HeadAnchor, dark, eye, new Vector3(.048f, .036f, .025f),
                    Quaternion.Euler(0, 0, side * 12f));
                Ball("Amber dragon iris", rig.HeadAnchor, amber, eye + Vector3.forward * .019f,
                    new Vector3(.026f, .023f, .012f));
                Ball("Dragon pupil", rig.HeadAnchor, dark, eye + new Vector3(0, 0, .028f),
                    new Vector3(.010f, .018f, .006f));
                Petal("Dragon brow", rig.HeadAnchor, jade, eye + new Vector3(-side * .040f, .018f, .018f),
                    eye + new Vector3(side * .046f, .045f + (side < 0 ? .013f : 0f), .007f), .017f, .019f);
                Ball("Dragon nostril", rig.HeadAnchor, dark, new Vector3(side * .063f, -.039f, .306f),
                    new Vector3(.017f, .012f, .011f));
                Taper("Vanilla fang", rig.HeadAnchor, ivory, new Vector3(side * .107f, -.105f, .243f),
                    new Vector3(side * .109f, -.17f, .235f), .022f, .84f);
                Wing(side, cherry, jade);
            }

            for (int i = 0; i < 3; i++)
                Taper("Back-swept crown spike", rig.HeadAnchor, jade,
                    new Vector3(0, .188f - i * .034f, -.061f - i * .045f),
                    new Vector3(0, .32f - i * .045f, -.147f - i * .06f), .048f - i * .007f, .50f);

            var start = new Vector3(0, .683f, -.112f);
            var control = new Vector3(.012f, .625f, -.44f);
            var end = new Vector3(.432f, .893f, -.497f);
            SweepTail(rig.BodyAnchor, jade, start, control, end);
            Taper("Amber tail point", rig.BodyAnchor, amber, Vector3.Lerp(control, end, .91f),
                end + new Vector3(.064f, .061f, -.010f), .035f, .80f);
        }

        void Clownfish()
        {
            var orange = Finish("Clownfish orange candy", "FB6D26", .81f);
            var darkFin = Finish("Licorice fin rim", "67303A", .65f);
            var fin = Finish("Juicy tangerine fin", "FF9536", .74f);
            Body(orange, new Vector3(1.13f, .97f, 1.10f), 0f);
            var stripe = Stripe("Three broad clownfish bands", "FA6A24", "FFF4DC",
                new Vector2(.18f, .30f), new Vector2(.44f, .60f), "582634", new Vector2(.72f, .85f), .085f);
            Revolve("Rounded banded clownfish suit", rig.BodyAnchor, stripe, .53f, 1.40f,
                t => .336f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), .57f), .86f);
            Face(.193f, .042f, .088f);
            Ball("Tangerine cheek muzzle", rig.HeadAnchor, fin, new Vector3(0, -.056f, .182f),
                new Vector3(.095f, .056f, .081f));
            Smile(rig.HeadAnchor, new Vector3(0, -.067f, .259f), darkFin, .046f);

            Petal("Clownfish dorsal fin", rig.BodyAnchor, darkFin,
                new Vector3(0, 1.245f, -.125f), new Vector3(0, 1.407f, -.251f), .068f, .065f);
            Petal("Clownfish orange dorsal inset", rig.BodyAnchor, fin,
                new Vector3(0, 1.258f, -.129f), new Vector3(0, 1.385f, -.226f), .070f, .048f);

            for (int side = -1; side <= 1; side += 2)
            {
                var finRoot = new Vector3(side * .225f, 1.049f, -.087f);
                var finTip = new Vector3(side * .463f, .985f, -.206f);
                Petal("Licorice-edged side fin", rig.BodyAnchor, darkFin, finRoot, finTip, .097f, .052f);
                Petal("Orange side fin face", rig.BodyAnchor, fin, finRoot + Vector3.forward * .024f,
                    Vector3.Lerp(finRoot, finTip, .93f) + Vector3.forward * .022f, .080f, .042f);
                var tailRoot = new Vector3(0, .978f, -.207f);
                var tailTip = new Vector3(0, .981f + side * .190f, -.525f);
                Petal("Black-rimmed tail fan", rig.BodyAnchor, darkFin, tailRoot, tailTip, .151f, .080f);
                Petal("Tangerine tail fan face", rig.BodyAnchor, fin,
                    tailRoot + Vector3.right * .012f, Vector3.Lerp(tailRoot, tailTip, .91f) + Vector3.right * .012f,
                    .135f, .067f);
            }
        }

        void CandyCorn()
        {
            var orange = Finish("Candy corn orange", "F78A37");
            var yellow = Finish("Candy corn golden base", "FBC839");
            var vanilla = Finish("Candy corn ivory tip", "FFF3DA", .68f, .105f);
            Body(orange, Vector3.one, 0f);
            rig.headRenderer.enabled = false;
            var banded = Stripe("Orange middle and golden base", "FB791F", "FFB92D",
                new Vector2(0, .53f), null, shadowLift: .22f);
            Revolve("Wide rounded candy corn body", rig.BodyAnchor, banded, .47f, 1.315f,
                t => Mathf.Lerp(.372f, .179f, t) * Mathf.SmoothStep(.75f, 1f, Mathf.Clamp01(t / .155f)),
                .74f);
            Revolve("Integrated vanilla candy tip", rig.HeadAnchor, vanilla, -.214f, .442f, t =>
            {
                float taper = Mathf.Clamp01((t - .22f) / .78f);
                float lower = Mathf.SmoothStep(.82f, 1f, Mathf.Clamp01(t / .17f));
                return .205f * lower * Mathf.Pow(1f - taper * taper, .76f);
            }, .90f);
            Face(.174f, .020f, .076f);
            Smile(rig.HeadAnchor, new Vector3(0, -.064f, .186f), dark, .056f);
            for (int side = -1; side <= 1; side += 2) Foot(side, yellow, .116f, .161f);
        }

        void Gingerbread()
        {
            var biscuit = Finish("Baked ginger", "AB662F", .45f,.092f);
            var icing = Finish("Piped vanilla icing", "FFF2DA", .58f,.13f);
            Body(biscuit,new Vector3(1.12f,1.015f,.92f),.15f);
            Face(.175f,.043f,.086f);
            Smile(rig.HeadAnchor,new Vector3(0,-.062f,.202f),icing,.070f);
            for(int side=-1;side<=1;side+=2)
            {
                Petal("Icing eyebrow",rig.HeadAnchor,icing,new Vector3(side*.113f,.103f,.16f),
                    new Vector3(side*.061f,.112f,.174f),.009f,.008f);
                int hand = side<0 ? 3 : 6;
                Ring("Piped wrist cuff",rig.AppearanceBone(hand),icing,new Vector3(0,.251f,0),.048f,.010f);
                Ball("Icing toe",rig.AppearanceBone(side<0 ? 10 : 13),icing,new Vector3(0,.021f,.181f),new Vector3(.065f,.022f,.018f));
            }
            string[] gum = {"C94954","EDD078","65AB77"};
            for(int i=0;i<gum.Length;i++)
            {
                var button=Finish("Glazed gumdrop",gum[i],.82f,.10f);
                Ball("Gumdrop button",rig.BodyAnchor,button,new Vector3(0,1.125f-i*.128f,.172f),
                    new Vector3(.033f,.040f,.025f));
            }
        }

        void Astronaut()
        {
            var pearl = Finish("Vanilla ceramic shell", "E9DFD2", .69f,.10f);
            var rubber = Finish("Blueberry suit joints", "24344A", .44f,.06f);
            var orange = Finish("Tangerine enamel trim", "E37B36");
            var glass = Finish("Indigo visor", "314E71", .89f,.09f);
            Body(rubber,new Vector3(1.22f,1.19f,1.17f),.12f);
            Use(rig.headRenderer,pearl);
            Ball("Panoramic visor rim",rig.HeadAnchor,orange,new Vector3(0,.008f,.137f),new Vector3(.213f,.148f,.106f));
            Ball("Panoramic visor",rig.HeadAnchor,glass,new Vector3(0,.013f,.164f),new Vector3(.193f,.126f,.100f));
            Ball("Pressure vest",rig.BodyAnchor,pearl,new Vector3(0,1.01f,.045f),new Vector3(.220f,.267f,.169f));
            Ball("Trimmed chest badge",rig.BodyAnchor,orange,new Vector3(.008f,1.049f,.212f),new Vector3(.073f,.063f,.013f));
            Ball("Chest badge center",rig.BodyAnchor,glass,new Vector3(.008f,1.049f,.224f),new Vector3(.048f,.042f,.007f));
            Ball("Life support pack",rig.BodyAnchor,pearl,new Vector3(0,1.022f,-.157f),new Vector3(.173f,.235f,.158f));
            for(int side=-1;side<=1;side+=2)
            {
                var arm=rig.AppearanceBone(side<0 ? 2 : 5);
                Ball("Rounded shoulder shell",arm,pearl,new Vector3(0,.062f,.005f),new Vector3(.096f,.136f,.099f));
                var forearm=rig.AppearanceBone(side<0 ? 3 : 6);
                Ring("Enamel cuff",forearm,orange,new Vector3(0,.232f,0),.049f,.014f);
                var ear=new Vector3(side*.194f,-.014f,-.015f);
                Ball("Helmet comms pod",rig.HeadAnchor,orange,ear,new Vector3(.045f,.072f,.070f));
                Ball("Backpack enamel spine",rig.BodyAnchor,orange,new Vector3(side*.091f,1.019f,-.277f),new Vector3(.031f,.155f,.037f));
                Foot(side,pearl,.118f,.165f);
            }
        }

        void Body(Material glaze, Vector3 headScale, float fullness)
        {
            Use(rig.bodyRenderer,glaze);
            Use(rig.headRenderer,glaze);
            rig.HeadAnchor.localScale=Vector3.Scale(rig.HeadAnchor.localScale,headScale);
            if(fullness <= 0f)return;
            var mesh = Keep(Object.Instantiate(rig.bodyRenderer.sharedMesh));
            var vertices=mesh.vertices;
            var normals=mesh.normals;
            var weights=mesh.boneWeights;
            for(int i=0;i<vertices.Length;i++)
            {
                if(!Torso(weights[i]))continue;
                var point=vertices[i];
                float y=(point.y-.94f)/.23f;
                float x=1+fullness*Mathf.Exp(-y*y);
                float z=1+(x-1)*.62f;
                vertices[i]=new Vector3(point.x*x,point.y,point.z*z);
                normals[i]=new Vector3(normals[i].x/x,normals[i].y,normals[i].z/z).normalized;
            }
            mesh.vertices=vertices;
            mesh.normals=normals;
            mesh.RecalculateBounds();
            rig.bodyRenderer.sharedMesh=mesh;
        }

        static bool Torso(BoneWeight w) =>
            TorsoWeight(w.boneIndex0,w.weight0)+TorsoWeight(w.boneIndex1,w.weight1)+
            TorsoWeight(w.boneIndex2,w.weight2)+TorsoWeight(w.boneIndex3,w.weight3)>.97f;

        static float TorsoWeight(int bone,float weight) => bone==0 || bone==44 || bone==45 ? weight : 0f;

        void Belly(Material finish, float halfWidth, float halfHeight, float center, bool bib=false)
        {
            var original=rig.bodyRenderer;
            var mesh=Keep(Object.Instantiate(original.sharedMesh));
            var vertices=mesh.vertices;
            var normals=mesh.normals;
            var weights=mesh.boneWeights;
            var uv=new Vector2[vertices.Length];
            var selected=new bool[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i];
                selected[i]=Torso(weights[i]) && p.z>.035f && normals[i].z>0f;
                uv[i]=new Vector2((p.x/halfWidth+1)*.5f,((p.y-center)/halfHeight+1)*.5f);
                vertices[i]+=normals[i]*.0025f;
            }
            var tri=mesh.triangles;
            var front=new List<int>();
            for(int i=0;i<tri.Length;i+=3)
                if(selected[tri[i]]&&selected[tri[i+1]]&&selected[tri[i+2]])
                { front.Add(tri[i]); front.Add(tri[i+1]); front.Add(tri[i+2]); }
            mesh.vertices=vertices;
            mesh.uv=uv;
            mesh.subMeshCount=1;
            mesh.SetTriangles(front,0);
            var decal=Keep(new Material(finish));
            decal.SetTexture("_BaseMap",Texture(96,96,(x,y)=>
            {
                float u=x*2f-1f,v=y*2f-1f;
                float width=bib?Mathf.Lerp(.68f,1f,y):1f;
                float oval=Mathf.Sqrt((u/width)*(u/width)+v*v);
                return new Color(1,1,1,Mathf.Clamp01((.975f-oval)*44f));
            }));
            decal.SetFloat("_AlphaClip",1f);
            decal.SetFloat("_Cutoff",.50f);
            decal.EnableKeyword("_ALPHATEST_ON");
            var part=new GameObject("Fitted candy inlay");
            part.transform.SetParent(original.transform,false);
            var fitted=part.AddComponent<SkinnedMeshRenderer>();
            fitted.sharedMesh=mesh;
            fitted.bones=original.bones;
            fitted.rootBone=original.rootBone;
            fitted.localBounds=original.localBounds;
            fitted.updateWhenOffscreen=true;
            fitted.sharedMaterial=decal;
        }

        void Face(float z,float y,float spacing)
        {
            for(int side=-1;side<=1;side+=2)
            {
                var point=new Vector3(side*spacing,y,z);
                Ball("Candy eye",rig.HeadAnchor,dark,point,new Vector3(.027f,.035f,.025f));
                Ball("Eye catchlight",rig.HeadAnchor,ivory,point+new Vector3(-.006f,.010f,.023f),new Vector3(.006f,.007f,.004f));
            }
        }

        void Smile(Transform anchor,Vector3 center,Material color,float width)
        {
            const int samples=13;
            for(int i=0;i<samples;i++)
            {
                float t=i/(samples-1f)*2f-1f;
                var point=center+new Vector3(width*t,.019f*t*t,-.011f*t*t);
                Ball("Piped smile",anchor,color,point,new Vector3(.008f,.006f,.007f));
            }
        }

        void Foot(int side,Material glaze,float width,float length) =>
            Ball("Rounded candy foot",rig.AppearanceBone(side<0?10:13),glaze,new Vector3(0f,-.006f,.068f),new Vector3(width,.078f,length));

        void Wing(int sign, Material membrane, Material edging)
        {
            Vector3 P(float x, float y, float z) => new Vector3(sign * x, y, z);
            var outline = new[]
            {
                P(.11f, 1.13f, -.105f), P(.29f, 1.33f, -.215f), P(.57f, 1.44f, -.311f),
                P(.69f, 1.38f, -.348f), P(.605f, 1.238f, -.345f), P(.556f, 1.195f, -.343f),
                P(.526f, 1.052f, -.327f), P(.434f, 1.117f, -.287f), P(.356f, .948f, -.266f),
                P(.266f, 1.028f, -.202f), P(.132f, .922f, -.151f)
            };
            var center = P(.320f, 1.170f, -.285f);
            var vertices = new List<Vector3> { center };
            vertices.AddRange(outline);
            var triangles = new List<int>();
            for (int i = 0; i < outline.Length; i++)
            {
                triangles.Add(0);
                triangles.Add(i + 1);
                triangles.Add((i + 1) % outline.Length + 1);
            }
            MeshPart("Cherry wing membrane", rig.BodyAnchor, membrane, vertices.ToArray(), triangles.ToArray());
            for (int edge = 0; edge < outline.Length; edge++)
                Petal("Jade wing edge", rig.BodyAnchor, edging, outline[edge],
                    outline[(edge + 1) % outline.Length], edge < 3 ? .028f : .017f, .023f);
            foreach (int tip in new[] { 3, 6, 8 })
                Taper("Tapered jade wing rib", rig.BodyAnchor, edging, outline[0], outline[tip], .025f, .73f);
        }

        void SweepTail(Transform anchor, Material finish, Vector3 start, Vector3 control, Vector3 end)
        {
            const int rings = 22, sides = 24;
            var vertices = new Vector3[(rings + 1) * (sides + 1)];
            var triangles = new int[rings * sides * 6];
            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                var a = Vector3.Lerp(start, control, t);
                var b = Vector3.Lerp(control, end, t);
                var center = Vector3.Lerp(a, b, t);
                var axis = (b - a).normalized;
                var cross = Vector3.Cross(axis, Vector3.up).normalized;
                var normal = Vector3.Cross(cross, axis);
                float radius = Mathf.Lerp(.073f, .019f, t);
                for (int side = 0; side <= sides; side++)
                {
                    float angle = side * 2f * Mathf.PI / sides;
                    int j = ring * (sides + 1) + side;
                    vertices[j] = center + radius * (Mathf.Cos(angle) * cross + Mathf.Sin(angle) * normal);
                    if (ring == rings || side == sides) continue;
                    int next = j + sides + 1, k = (ring * sides + side) * 6;
                    triangles[k] = j;
                    triangles[k + 1] = next;
                    triangles[k + 2] = j + 1;
                    triangles[k + 3] = j + 1;
                    triangles[k + 4] = next;
                    triangles[k + 5] = next + 1;
                }
            }
            MeshPart("Curled jade tail", anchor, finish, vertices, triangles);
        }

        void Petal(string name,Transform anchor,Material glaze,Vector3 from,Vector3 to,float width,float depth)
        {
            var along=to-from;
            Ball(name,anchor,glaze,(from+to)*.5f,new Vector3(width,along.magnitude*.59f,depth),
                Quaternion.FromToRotation(Vector3.up,along.normalized));
        }

        static void Ball(string name,Transform anchor,Material glaze,Vector3 center,Vector3 radii,Quaternion? rotation=null)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name=name;
            var collider=part.GetComponent<Collider>();
            collider.enabled=false;
            Object.Destroy(collider);
            part.transform.SetParent(anchor,false);
            part.transform.SetLocalPositionAndRotation(center,rotation??Quaternion.identity);
            part.transform.localScale=radii*2f;
            part.GetComponent<MeshRenderer>().sharedMaterial=glaze;
        }

        void Ring(string name,Transform anchor,Material glaze,Vector3 center,float radius,float thickness)
        {
            const int segments=24, cross=8;
            var vertices=new Vector3[segments*cross];
            var triangles=new int[segments*cross*6];
            for(int i=0;i<segments;i++)
            {
                float angle=i*Mathf.PI*2f/segments;
                var radial=new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
                for(int j=0;j<cross;j++)
                {
                    float angle2=j*Mathf.PI*2f/cross;
                    int a=i*cross+j,b=i*cross+(j+1)%cross,c=((i+1)%segments)*cross+j,d=((i+1)%segments)*cross+(j+1)%cross;
                    vertices[a]=center+radial*(radius+Mathf.Cos(angle2)*thickness)+Vector3.up*(Mathf.Sin(angle2)*thickness);
                    int k=a*6;
                    triangles[k]=a;triangles[k+1]=b;triangles[k+2]=c;
                    triangles[k+3]=b;triangles[k+4]=d;triangles[k+5]=c;
                }
            }
            MeshPart(name,anchor,glaze,vertices,triangles);
        }

        void Taper(string name, Transform anchor, Material glaze, Vector3 from, Vector3 to, float width, float depth)
        {
            var pivot = new GameObject(name + " mount").transform;
            pivot.SetParent(anchor, false);
            var axis = to - from;
            pivot.SetLocalPositionAndRotation(from, Quaternion.FromToRotation(Vector3.up, axis.normalized));
            Revolve(name, pivot, glaze, 0f, axis.magnitude,
                t => width * Mathf.SmoothStep(.61f, 1f, Mathf.Clamp01(t / .18f)) * Mathf.Pow(1f - t, .83f),
                depth);
        }

        void Revolve(string name,Transform anchor,Material glaze,float low,float high,Func<float,float> radius,float depth)
        {
            const int rows=40,around=48;
            var vertices=new Vector3[(rows+1)*(around+1)];
            var normals=new Vector3[vertices.Length];
            var uv=new Vector2[vertices.Length];
            var tri=new int[rows*around*6];
            for(int row=0;row<=rows;row++)
            {
                float t=row/(float)rows,r=Mathf.Max(.006f,radius(t));
                float slope=(radius(Mathf.Min(1f,t+.002f))-radius(Mathf.Max(0f,t-.002f)))/(.004f*(high-low));
                for(int side=0;side<=around;side++)
                {
                    float u=side/(float)around,a=2f*Mathf.PI*u;
                    float x=Mathf.Cos(a),z=Mathf.Sin(a);
                    int j=row*(around+1)+side;
                    vertices[j]=new Vector3(x*r,Mathf.Lerp(low,high,t),z*r*depth);
                    normals[j]=new Vector3(x,-slope,z/depth).normalized;
                    uv[j]=new Vector2(u,t);
                    if(row==rows||side==around)continue;
                    int next=j+around+1,k=(row*around+side)*6;
                    tri[k]=j;tri[k+1]=next;tri[k+2]=j+1;
                    tri[k+3]=j+1;tri[k+4]=next;tri[k+5]=next+1;
                }
            }
            var mesh=MeshPart(name,anchor,glaze,vertices,tri);
            mesh.normals=normals;
            mesh.uv=uv;
        }

        Mesh MeshPart(string name,Transform anchor,Material glaze,Vector3[] vertices,int[] triangles)
        {
            var mesh=Keep(new Mesh { name=name,vertices=vertices,triangles=triangles });
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var part=new GameObject(name);
            part.transform.SetParent(anchor,false);
            part.AddComponent<MeshFilter>().sharedMesh=mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial=glaze;
            return mesh;
        }

        Material Stripe(string name,string body,string stripe,Vector2 first,Vector2? second,string border=null,Vector2? third=null,float shadowLift=.022f)
        {
            var glaze=Finish(name,"FFFFFF",.79f,.022f);
            var baseTint=teamColor ?? Tint(body);
            var stripeTint=Tint(stripe);
            var borderTint=border==null?stripeTint:Tint(border);
            var bands = Texture(4,256,(_,y)=>
            {
                float Gap(Vector2 range)=>Mathf.Max(range.x-y,y-range.y);
                float gap=Gap(first);
                if(second.HasValue)gap=Mathf.Min(gap,Gap(second.Value));
                if(third.HasValue)gap=Mathf.Min(gap,Gap(third.Value));
                return gap<=0?stripeTint:gap<.016f?borderTint:baseTint;
            });
            glaze.SetTexture("_BaseMap", bands);
            glaze.SetTexture("_EmissionMap", bands);
            glaze.SetColor("_EmissionColor", Color.white * shadowLift);
            return glaze;
        }

        Texture2D Texture(int width,int height,Func<float,float,Color> color)
        {
            var texture=Keep(new Texture2D(width,height,TextureFormat.RGBA32,true));
            texture.wrapMode=TextureWrapMode.Clamp;
            var values=new Color[width*height];
            for(int y=0;y<height;y++)
                for(int x=0;x<width;x++)values[y*width+x]=color((x+.5f)/width,(y+.5f)/height);
            texture.SetPixels(values);
            texture.Apply(true,true);
            return texture;
        }

        Material Finish(string name,string hex,float smoothness=.77f,float shadowLift=.12f)
        {
            var shader=Shader.Find("Universal Render Pipeline/Complex Lit");
            if(!shader||!shader.isSupported)shader=Shader.Find("Universal Render Pipeline/Lit");
            var material=Keep(new Material(shader){name=name});
            var color=TeamFinish(name, Tint(hex));
            material.SetColor("_BaseColor",color);
            material.SetFloat("_Smoothness",smoothness);
            material.SetFloat("_Metallic",0f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor",color.linear*shadowLift);
            if(material.HasProperty("_ClearCoatMask"))
            {
                material.SetFloat("_ClearCoat",1f);
                material.SetFloat("_ClearCoatMask",.89f);
                material.SetFloat("_ClearCoatSmoothness",.94f);
                material.EnableKeyword("_CLEARCOAT");
            }
            return material;
        }

        Color TeamFinish(string name, Color original)
        {
            if (!teamColor.HasValue) return original;
            var color = teamColor.Value;
            switch (name)
            {
                case "Jade candy glaze":
                case "Clownfish orange candy":
                case "Candy corn orange":
                    return color;
                case "Burnt cherry wings":
                    return Color.Lerp(color, new Color(.10f,.075f,.10f), .35f);
                case "Juicy tangerine fin":
                    return Color.Lerp(color, Color.white, .26f);
                default:
                    return original;
            }
        }

        static Color Tint(string hex)
        {
            if(ColorUtility.TryParseHtmlString("#"+hex,out var value))return value;
            throw new ArgumentException("Expected an RGB tint.",nameof(hex));
        }

        static void Use(Renderer renderer,Material glaze)
        {
            renderer.SetPropertyBlock(null);
            renderer.sharedMaterial=glaze;
        }

        T Keep<T>(T asset) where T:Object
        {
            owned.Add(asset);
            return asset;
        }

        public void Dispose()
        {
            foreach(var asset in owned)if(asset)Object.Destroy(asset);
            owned.Clear();
        }
    }
}
