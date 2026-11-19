using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    internal static class CandyHairMeshes
    {
        static readonly Dictionary<AvatarHair, Mesh> hair = new Dictionary<AvatarHair, Mesh>();
        static readonly Dictionary<AvatarHair, Mesh> ties = new Dictionary<AvatarHair, Mesh>();
        static Mesh mohawk;
        static Material glaze, tieGlaze;

        public static Material Glaze(Material fallback) =>
            glaze ? glaze : glaze = CreateGlaze("Hard candy hair", new Color(.245f, .113f, .073f), fallback);
        public static Material TieGlaze(Material fallback) =>
            tieGlaze ? tieGlaze : tieGlaze = CreateGlaze("Strawberry candy ties", new Color(.70f, .20f, .28f), fallback);
        public static bool IsHairMaterial(Material material) => glaze && material == glaze;
        public static bool IsTieMaterial(Material material) => tieGlaze && material == tieGlaze;

        public static Color PlayerShade(Color player, AvatarHairShade shade)
        {
            if (shade == AvatarHairShade.Match) return player;
            var basis = ToLab(player);
            if (shade == AvatarHairShade.Dark)
            {
                var firstPass = ToLab(player * .23f);
                var secondPass = new Vector3(Mathf.Max(0, basis.x - .12f), basis.y * .8f, basis.z * .8f);
                secondPass = InGamut(secondPass);
                return ToRgb(InGamut((firstPass + secondPass) * .5f));
            }
            var oldLight = ToLab(Color.Lerp(player, Color.white, .42f));
            var newLight = InGamut(new Vector3(.89f, basis.y * .57f, basis.z * .57f));
            var light = new Vector3(Mathf.Max(.8535533f, Mathf.Lerp(basis.x, 1f, .35f)),
                (oldLight.y + newLight.y) * .5f, (oldLight.z + newLight.z) * .5f);
            return ToRgb(InGamut(light));
        }

        static Vector3 ToLab(Color input)
        {
            var c = input.linear;
            var l = Mathf.Pow(.41222147f*c.r + .53633254f*c.g + .05144599f*c.b, 1f/3);
            var m = Mathf.Pow(.21190350f*c.r + .68069955f*c.g + .10739696f*c.b, 1f/3);
            var s = Mathf.Pow(.08830246f*c.r + .28171884f*c.g + .62997870f*c.b, 1f/3);
            return new Vector3(.21045426f*l + .79361779f*m - .00407205f*s,
                1.97799850f*l - 2.42859221f*m + .45059371f*s,
                .02590404f*l + .78277177f*m - .80867577f*s);
        }

        static Color LinearRgb(Vector3 lab)
        {
            float l = lab.x + .39633778f*lab.y + .21580376f*lab.z;
            float m = lab.x - .10556135f*lab.y - .06385417f*lab.z;
            float s = lab.x - .08948418f*lab.y - 1.29148555f*lab.z;
            l *= l*l; m *= m*m; s *= s*s;
            return new Color(4.07674166f*l - 3.30771159f*m + .23096993f*s,
                -1.26843800f*l + 2.60975740f*m - .34131940f*s,
                -.00419609f*l - .70341861f*m + 1.70761470f*s);
        }

        static Vector3 InGamut(Vector3 lab)
        {
            float low = 0f, high = 1f;
            for (int step = 0; step < 16; step++)
            {
                float mid = (low + high)*.5f;
                var rgb = LinearRgb(new Vector3(lab.x, lab.y*mid, lab.z*mid));
                if (rgb.r >= 0f && rgb.r <= 1f && rgb.g >= 0f && rgb.g <= 1f && rgb.b >= 0f && rgb.b <= 1f)
                    low = mid;
                else
                    high = mid;
            }
            return new Vector3(lab.x, lab.y*low, lab.z*low);
        }

        static Color ToRgb(Vector3 lab)
        {
            var rgb = LinearRgb(lab);
            return new Color(Mathf.Clamp01(rgb.r), Mathf.Clamp01(rgb.g), Mathf.Clamp01(rgb.b)).gamma;
        }

        public static void ApplyShade(Renderer renderer, MaterialPropertyBlock block, Color shade)
        {
            block.Clear();
            block.SetColor("_BaseColor", shade);
            block.SetColor("_EmissionColor", shade.linear * .035f);
            renderer.SetPropertyBlock(block);
        }

        static Material CreateGlaze(string name, Color color, Material fallback)
        {
            var shader = Shader.Find("Universal Render Pipeline/Complex Lit");
            var material = shader && shader.isSupported ? new Material(shader) : new Material(fallback);
            material.name = name;
            material.enableInstancing = true;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .93f);
            material.SetFloat("_Metallic", 0f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(.028f, .012f, .008f));
            if (material.HasProperty("_ClearCoatMask"))
            {
                material.SetFloat("_ClearCoat", 1);
                material.SetFloat("_ClearCoatMask", .87f);
                material.SetFloat("_ClearCoatSmoothness", .96f);
                material.EnableKeyword("_CLEARCOAT");
            }
            return material;
        }

        public static Mesh Get(AvatarHair style)
        {
            if (hair.TryGetValue(style, out var mesh)) return mesh;
            var shape = new Shape();
            var bands = new Shape();
            switch (style)
            {
                case AvatarHair.Buzz:
                case AvatarHair.CrewCut:
                    Crop(shape, style == AvatarHair.CrewCut);
                    break;
                case AvatarHair.Bob:
                case AvatarHair.LongWaves:
                    Curtain(shape, style == AvatarHair.LongWaves);
                    break;
                case AvatarHair.TwinTails:
                    shape.Cap(1.62f, 2.08f);
                    PartedFront(shape);
                    TwinTails(shape, bands);
                    break;
                case AvatarHair.Ponytail:
                case AvatarHair.Bun:
                    Gathered(shape, style == AvatarHair.Bun);
                    break;
                case AvatarHair.Braids:
                    Braided(shape, bands);
                    break;
                case AvatarHair.Dreadlocks:
                    Locs(shape);
                    break;
                case AvatarHair.Curls:
                case AvatarHair.Afro:
                    Coils(shape, style == AvatarHair.Afro);
                    break;
                case AvatarHair.Spikes:
                case AvatarHair.LibertySpikes:
                    Spiked(shape, style == AvatarHair.LibertySpikes);
                    break;
                case AvatarHair.SidePart:
                case AvatarHair.Pompadour:
                    Combed(shape, style == AvatarHair.Pompadour);
                    break;
            }
            mesh = shape.Mesh(style.ToString());
            hair[style] = mesh;
            if (bands.HasGeometry) ties[style] = bands.Mesh(style + " ties");
            return mesh;
        }

        public static Mesh Ties(AvatarHair style) => ties.TryGetValue(style, out var mesh) ? mesh : null;

        public static Mesh Mohawk()
        {
            if (mohawk) return mohawk;
            var shape = new Shape();
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float z = Mathf.Lerp(.183f, -.185f, t);
                float height = .332f + .115f * Mathf.Sin(t * Mathf.PI);
                shape.Sweep(V(0,.148f-Mathf.Abs(z)*.22f,z),V(0,.233f,z+.016f),
                    V(0,height-.042f,z-.012f),V(0,height,z-.042f),.051f,.056f,.005f,.044f);
            }
            return mohawk = shape.Mesh("Mohawk");
        }

        static void PartedFront(Shape shape)
        {
            shape.Sweep(V(-.065f,.203f,.059f),V(.037f,.197f,.161f),V(.144f,.147f,.205f),V(.183f,.061f,.139f),.034f,.059f,.009f,.021f);
            shape.Sweep(V(-.065f,.204f,.069f),V(-.136f,.187f,.168f),V(-.175f,.108f,.186f),V(-.209f,.060f,.103f),.032f,.043f,.010f,.017f);
        }

        static void TwinTails(Shape shape, Shape bands)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var knot = V(side*.234f,.091f,-.108f);
                var along = V(side*.75f,.12f,-.18f).normalized;
                shape.Sweep(V(side*.096f,.126f,-.157f),V(side*.136f,.127f,-.179f),V(side*.186f,.070f,-.087f),knot,
                    .046f,.057f,.040f,.027f);
                bands.Band(knot,along,.053f);
                var peak = V(side*.398f,.089f,-.112f);
                var bend = V(side*.426f,-.216f,-.103f);
                var tip = V(side*.296f,-.285f,-.074f);
                shape.Sweep(knot+along*.018f,peak,bend,tip,.039f,.083f,.008f,.059f);
                shape.Sweep(knot+along*.033f+V(0,.018f,.027f),peak+V(side*.019f,.019f,.041f),
                    bend+V(side*.023f,0,.055f),tip+V(side*.054f,.042f,.041f),.025f,.051f,.006f,.037f);
            }
        }


        // Short cuts follow the head closely and use the hairline to distinguish the shapes.
        static void Crop(Shape shape, bool crew)
        {
            shape.Scalp((angle, t) =>
            {
                float front = Mathf.Cos(angle);
                float temple = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle)), 7f);
                float edge = 1.48f - .25f * Mathf.Max(0, front) + .37f * Mathf.Max(0, -front) - .30f * temple;
                float phi = edge * t;
                float lift = crew ? .084f * Mathf.Pow(Mathf.Max(0, front), 2f) *
                    Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 1.5f) : .003f * Mathf.Sin(angle * 23f) * t;
                Vector3 point = HeadSurface(angle, phi, .235f, crew ? .252f : .235f);
                point.y += lift;
                return point;
            });
        }

        static void Combed(Shape shape, bool pompadour)
        {
            shape.Scalp((angle, t) =>
            {
                float front = Mathf.Cos(angle);
                float side = Mathf.Sin(angle);
                float frontWeight = Mathf.SmoothStep(0f, 1f, Mathf.Max(0f, front));
                float edge = 1.64f -.44f * frontWeight + .30f * Mathf.Max(0f, -front);
                edge += frontWeight * .25f * Mathf.Sin(angle * 2f + .35f);
                float lean = pompadour ? .24f : .35f;
                float comb = angle + lean * t * t;
                float groove = Flow(comb * (pompadour ? 7f : 8f), t) * .008f;
                Vector3 point = HeadSurface(angle, edge * t, .241f + groove, .251f + groove);
                float crest = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 1.35f) * frontWeight;
                point.y += crest * (pompadour ? .146f : .050f) * (1f + .22f * side);
                point.z += crest * (pompadour ? .045f : .008f);
                point.x += crest * (pompadour ? .025f : .044f);
                return point;
            });
        }

        // Continuous curtain from the crown to the hem. The two shallow sides
        // of the front opening run into the fringe without separate plates.
        static void Curtain(Shape shape, bool longWaves)
        {
            shape.Scalp((angle, t) =>
            {
                float front = Mathf.Cos(angle);
                float side = Mathf.Sin(angle);
                float curtain = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.92f, .32f, front + .056f * side));
                float phi = Mathf.Lerp(1.29f + .16f * Mathf.Sin(angle * 2f), 1.74f, curtain) * t;
                float run = Mathf.Pow(t, 3.9f) * curtain;
                float wave = Mathf.Sin(t * 10f + angle * 3f);
                float strand = angle * (longWaves ? 7f : 6f) + t * (longWaves ? 1.8f + wave * .9f : .75f);
                float section = Flow(strand, t);
                float radius = .239f + .018f * Mathf.Sin(phi) + section * (longWaves ? .025f : .013f);
                Vector3 point = HeadSurface(angle, phi, radius, .260f);
                point.y -= run * (longWaves ? .388f + .064f * Mathf.Cos(strand) : .144f + .030f * side);
                point.z += run * (longWaves ? -.065f : .032f);
                point.x += run * side * (longWaves ? .083f + .035f * wave : -.029f);
                return point;
            });
        }

        static void Gathered(Shape shape, bool bun)
        {
            shape.Scalp((angle, t) =>
            {
                float front = Mathf.Cos(angle);
                float edge = 1.55f - .40f * Mathf.Max(0, front) + .54f * Mathf.Max(0, -front);
                float phi = edge * t;
                float groove = .010f * Flow(angle * 9f + .50f * Mathf.Sin(phi * 2f), t);
                Vector3 point = HeadSurface(angle, phi, .242f + groove, .251f + groove);
                float lift = Mathf.Max(0f, -front) * Mathf.Sin(Mathf.PI * t);
                point.y += .037f * lift;
                point.z -= .044f * lift;
                return point;
            });
            if (bun)
            {
                var center = V(0, .282f, -.232f);
                shape.Scalp((angle, t) =>
                {
                    float phi = t * 3.125f;
                    float twist = .011f * Mathf.Cos(angle * 4f + phi * 3f) * Mathf.Sin(phi);
                    Vector3 direction = Polar(angle, phi);
                    return center + V(direction.x * (.103f + twist), direction.y * (.108f + twist),
                        direction.z * (.109f + twist));
                });
                return;
            }

            shape.Lock(t =>
            {
                float turn = Mathf.Sin(t * Mathf.PI);
                return V(.15f * t * t, .192f + .091f * Mathf.Sin(t * Mathf.PI * 1.36f) - .49f * t * t,
                    -.203f - .223f * turn + .035f * t);
            }, t => Mathf.Lerp(.049f, .005f, t) + .063f * Mathf.Sin(Mathf.PI * t),
                .73f, 4f);
        }

        static void Braided(Shape shape, Shape bands)
        {
            shape.Scalp((angle, t) =>
            {
                float front = Mathf.Cos(angle);
                float edge = 1.75f - .51f * Mathf.Max(0f, front) + .38f * Mathf.Max(0f, -front);
                float crease = -.017f * Mathf.Pow(Mathf.Abs(front), 30f) * Mathf.Sin(t * Mathf.PI * .9f);
                float ridge = .008f * Flow(angle * 7f + .32f * t * Mathf.Sin(angle), t);
                return HeadSurface(angle, edge * t, .250f + crease + ridge, .257f + crease + ridge);
            });

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 Center(float t) => V(side * (.185f + .072f * Mathf.Sin(t * 1.97f)), .068f - .497f * t,
                    -.088f + .245f * t - .044f * Mathf.Sin(t * Mathf.PI));
                for (int strand = 0; strand < 3; strand++)
                {
                    float phase = strand * Mathf.PI * 2f / 3f;
                    shape.Lock(t =>
                    {
                        float theta = t * 39f + phase;
                        float radius = Mathf.Lerp(.028f, .012f, t);
                        return Center(t) + V(Mathf.Cos(theta) * radius, 0f, Mathf.Sin(theta) * radius);
                    }, t => Mathf.Lerp(.036f, .015f, t), .89f, 0f, 42);
                }
                bands.Band(Center(.994f), Vector3.down, .031f);
                shape.Lock(t => Center(1f + t * .15f) + V(.025f * side * t * t, 0f, 0f),
                    t => Mathf.Lerp(.029f, .004f, t), .73f, 3f);
            }
        }

        static void Locs(Shape shape)
        {
            shape.Scalp((angle, t) =>
            {
                float front = Mathf.Cos(angle);
                float edge = 1.64f - .43f * Mathf.Max(0f, front) + .35f * Mathf.Max(0f, -front);
                float ridge = .009f * Flow(angle * 11f + .8f * t, t);
                return HeadSurface(angle, edge * t, .239f + ridge, .251f + ridge);
            });

            for (int i = 0; i < 13; i++)
            {
                float angle = Mathf.Lerp(.68f, 5.49f, i / 12f);
                float length = .24f + .18f * Mathf.Pow(Mathf.Sin(i * 1.75f + .65f), 2f);
                float swing = .14f * Mathf.Sin(i * 2.32f);
                float rootPhase = .70f + .19f * Mathf.Sin(i * 2f);
                shape.Lock(t =>
                {
                    float flow = angle + swing * Mathf.Sin(Mathf.PI * t) + .17f * t;
                    float phi = rootPhase + Mathf.Sin(t * Mathf.PI * .73f) * 1.07f;
                    Vector3 point = HeadSurface(flow, phi, .231f + .057f * Mathf.Sin(t * 2.4f), .246f);
                    point.y -= length * t * t;
                    return point;
                }, t => Mathf.Lerp(.028f, .010f, t) + .014f * Mathf.Sin(Mathf.PI * t), .91f, 3f);
            }
        }

        static void Coils(Shape shape, bool afro)
        {
            int count = afro ? 62 : 19;
            var centers = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float y = Mathf.Lerp(.98f, afro ? -.14f : .14f, (i + .5f) / count);
                float angle = i * 2.399963f;
                float radius = Mathf.Sqrt(1f - y * y);
                centers[i] = V(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius);
            }

            shape.Scalp((angle, t) =>
            {
                float front = Mathf.Cos(angle);
                float phi = (afro ? 1.65f : 1.56f) * t + t * (.14f * Mathf.Max(0f, -front) - .08f * Mathf.Max(0f, front));
                Vector3 direction = Polar(angle, phi);
                float bump = 0;
                for (int i = 0; i < centers.Length; i++)
                {
                    float distance = Mathf.Max(0f, 1f - Vector3.Dot(direction, centers[i]));
                    float crown = Mathf.Exp(-distance * (afro ? 58f : 39f));
                    float curl = .80f + .20f * Mathf.Sin(angle * 8f + phi * 13f + i * .61f);
                    bump += crown * crown * (afro ? 1f : curl);
                }
                float profile = Mathf.Min(1f, Mathf.Sqrt(bump));
                float rim = Mathf.SmoothStep(.41f, 1f, t);
                float radius = (afro ? .280f : .245f) + (afro ? .053f : .034f) * profile - rim * .026f;
                float height = (afro ? .321f : .268f) + (afro ? .050f : .043f) * profile;
                return V(direction.x * radius, direction.y * height + .021f, direction.z * radius - .014f);
            }, 46, 96);
        }

        static void Spiked(Shape shape, bool liberty)
        {
            shape.Scalp((angle, t) =>
            {
                float front = Mathf.Cos(angle);
                float edge = 1.64f - .43f * Mathf.Max(0f, front) + .26f * Mathf.Max(0f, -front);
                float ridge = .008f * Flow(angle * 7f + t, t);
                var point = HeadSurface(angle, edge * t, .240f + ridge, .251f + ridge);
                if (!liberty)
                {
                    float crown = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(angle * 5f + t * 2.3f)), 3f);
                    float raise = crown * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 4f);
                    point += V(.048f * raise, .111f * raise, -.078f * raise);
                }
                return point;
            });
            if (!liberty) return;

            const int count = 7;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.Lerp(-1.66f, 1.66f, i / (count - 1f));
                var outward = V(Mathf.Sin(angle), 0f, Mathf.Cos(angle) * .39f);
                float height = Mathf.Lerp(.390f, .536f, Mathf.Pow(Mathf.Cos(angle * .72f), 2f));
                Vector3 root = outward * .137f + V(0f, .170f, -.049f);
                Vector3 tip = outward * .333f + V(.031f, height, -.062f);
                shape.Lock(t => Vector3.Lerp(root, tip, t) + outward * (.045f * Mathf.Sin(Mathf.PI * t)),
                    t => .070f * Mathf.Pow(1f - t, .87f) + .002f, .68f, 3f);
            }
        }

        static Vector3 Polar(float angle, float phi) =>
            V(Mathf.Sin(angle) * Mathf.Sin(phi), Mathf.Cos(phi), Mathf.Cos(angle) * Mathf.Sin(phi));

        static Vector3 HeadSurface(float angle, float phi, float radius, float height)
        {
            Vector3 direction = Polar(angle, phi);
            return V(direction.x * radius, direction.y * height + .010f, direction.z * (radius + .006f) - .014f);
        }

        static float Flow(float phase, float t) =>
            Mathf.Sin(t * Mathf.PI * .86f) * (1f - Mathf.Pow(.5f + .5f * Mathf.Cos(phase), 6f));

        static Vector3 V(float x,float y,float z) => new Vector3(x,y,z);

        sealed class Shape
        {
            readonly List<Vector3> points=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<int> triangles=new List<int>();
            readonly List<(int First,int Last)> seams=new List<(int,int)>();
            public bool HasGeometry=>points.Count>0;

            public void Band(Vector3 center,Vector3 direction,float size)
            {
                var along=direction.normalized*.030f;
                Sweep(center-along,center-along*.42f,center+along*.42f,center+along,size*.88f,size,size*.88f,size);
            }

            public void Cap(float side,float back,float front=1.22f,float height=1f,float radius=.239f)
            {
                const int rows = 14;
                const int columns = 36;
                int start=points.Count;
                for(int row=0;row<=rows;row++)
                {
                    int first=points.Count;
                    for(int col=0;col<=columns;col++)
                    {
                        float u=col/(float)columns,v=row/(float)rows;
                        float a=u*Mathf.PI*2f,c=Mathf.Cos(a);
                        float extent=c>0?Mathf.Lerp(side,front,Mathf.Pow(c,.65f)):Mathf.Lerp(side,back,-c);
                        float phi=Mathf.Lerp(.002f,extent,v);
                        points.Add(V(Mathf.Sin(a)*Mathf.Sin(phi)*radius,Mathf.Cos(phi)*.230f*height+.003f,
                            c*Mathf.Sin(phi)*(radius+.003f)-.014f));
                        uv.Add(new Vector2(u,v));
                        if(row<rows&&col<columns) Quad(start+row*(columns+1)+col,columns+1);
                    }
                    seams.Add((first,points.Count-1));
                }
            }


            public void Scalp(System.Func<float, float, Vector3> surface, int rows = 28, int columns = 72)
            {
                int origin = points.Count;
                for (int row = 0; row <= rows + 1; row++)
                {
                    int first = points.Count;
                    float t = Mathf.Clamp(row / (float)rows, .001f, 1f);
                    for (int col = 0; col <= columns; col++)
                    {
                        float u = col / (float)columns;
                        Vector3 position = surface(u * Mathf.PI * 2f, t);
                        if (row > rows)
                        {
                            position.x *= .91f;
                            position.z *= .91f;
                            position.y += .006f;
                        }
                        points.Add(position);
                        uv.Add(new Vector2(u, t));
                        if (row <= rows && col < columns) Quad(origin + row * (columns + 1) + col, columns + 1);
                    }
                    seams.Add((first, points.Count - 1));
                }
            }

            public void Lock(System.Func<float, Vector3> path, System.Func<float, float> radius,
                float depth, float ridges, int rings = 24)
            {
                const int sides = 12;
                int first = points.Count;
                for (int ring = 0; ring <= rings; ring++)
                {
                    int ringStart = points.Count;
                    float t = ring / (float)rings;
                    Vector3 center = path(t);
                    Vector3 axis = (path(Mathf.Min(1f, t + .002f)) - path(Mathf.Max(0f, t - .002f))).normalized;
                    Vector3 normal = Vector3.ProjectOnPlane(Vector3.forward, axis).normalized;
                    if (normal.sqrMagnitude < .01f) normal = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
                    Vector3 width = Vector3.Cross(axis, normal).normalized;
                    for (int side = 0; side <= sides; side++)
                    {
                        float u = side / (float)sides;
                        float angle = u * Mathf.PI * 2f;
                        float section = radius(t);
                        if (ridges > 0f) section *= 1f + .10f * Mathf.Cos(angle * ridges + t * 2.1f);
                        int index = points.Count;
                        points.Add(center + section * (Mathf.Cos(angle) * width + Mathf.Sin(angle) * depth * normal));
                        uv.Add(new Vector2(u, t));
                        if (ring < rings && side < sides) Quad(index, sides + 1);
                    }
                    seams.Add((ringStart, points.Count - 1));
                }

                int last = first + rings * (sides + 1);
                int startCap = points.Count;
                points.Add(path(0f));
                uv.Add(new Vector2(.5f, 0f));
                int endCap = points.Count;
                points.Add(path(1f));
                uv.Add(new Vector2(.5f, 1f));
                for (int side = 0; side < sides; side++)
                {
                    Triangle(startCap, first + side, first + side + 1);
                    Triangle(endCap, last + side + 1, last + side);
                }
            }
            public void Sweep(Vector3 start,Vector3 control1,Vector3 control2,Vector3 end,
                float startWidth,float middleWidth,float endWidth,float depth,float wave=0)
            {
                const int rings=16,sides=12;
                int first=points.Count;
                Vector3 Point(float t)
                {
                    float s=1-t;
                    return s*s*s*start+3*s*s*t*control1+3*s*t*t*control2+t*t*t*end+
                        V(Mathf.Sin(t*Mathf.PI*2)*Mathf.Sin(t*Mathf.PI)*wave,0,0);
                }
                for(int ring=0;ring<=rings;ring++)
                {
                    int ringStart=points.Count;
                    float t=ring/(float)rings;
                    var p=Point(t);
                    var axis=(Point(Mathf.Min(1,t+.006f))-Point(Mathf.Max(0,t-.006f))).normalized;
                    var normal=Vector3.ProjectOnPlane(Vector3.forward,axis).normalized;
                    if(normal.sqrMagnitude<.01f) normal=Vector3.ProjectOnPlane(Vector3.up,axis).normalized;
                    var sideAxis=Vector3.Cross(axis,normal).normalized;
                    float width=Mathf.Lerp(startWidth,endWidth,t)+(middleWidth-(startWidth+endWidth)*.5f)*Mathf.Sin(t*Mathf.PI);
                    float thickness=depth*width/middleWidth;
                    for(int side=0;side<=sides;side++)
                    {
                        float u=side/(float)sides,a=u*Mathf.PI*2f;
                        int vertex=points.Count;
                        points.Add(p+sideAxis*(Mathf.Cos(a)*width)+normal*(Mathf.Sin(a)*thickness));
                        uv.Add(new Vector2(u,t));
                        if(ring<rings&&side<sides) Quad(vertex,sides+1);
                    }
                    seams.Add((ringStart,points.Count-1));
                }
                int last=first+rings*(sides+1);
                int startCap=points.Count;
                points.Add(start); uv.Add(new Vector2(.5f,0));
                int endCap=points.Count;
                points.Add(end); uv.Add(new Vector2(.5f,1));
                for(int side=0;side<sides;side++)
                {
                    Triangle(startCap,first+side,first+side+1);
                    Triangle(endCap,last+side+1,last+side);
                }
            }

            void Quad(int p,int stride)
            {
                Triangle(p,p+stride,p+1);
                Triangle(p+1,p+stride,p+stride+1);
            }

            void Triangle(int a,int b,int c)
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }

            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name="Candy hair - "+name};
                mesh.SetVertices(points);
                mesh.SetUVs(0,uv);
                mesh.SetTriangles(triangles,0);
                mesh.RecalculateNormals();
                var normals=mesh.normals;
                foreach(var seam in seams)
                {
                    Vector3 joined=(normals[seam.First]+normals[seam.Last]).normalized;
                    normals[seam.First]=joined;
                    normals[seam.Last]=joined;
                }
                mesh.normals=normals;
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
