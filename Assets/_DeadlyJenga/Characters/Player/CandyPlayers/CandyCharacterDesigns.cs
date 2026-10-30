using System.Collections.Generic;
using UnityEngine;
using DeadlyJenga.Core;

namespace DeadlyJenga.CandyPlayers
{
    // Every character design, built from simple shapes on the candy player's head, torso and hands.
    // Head space: +Z is the face, the head is a ball about 0.22 wide. Torso space: +Z is the chest.
    public static class CandyCharacterDesigns
    {
        public static readonly string[] Names =
        {
            "Classic", "Gummy Bear", "Cupcake", "Lollipop Knight", "Astro Mint",
            "Licorice Ninja", "Choco King", "Angel Cake", "Bubble Bot", "Party Popper"
        };
        public static int Count => Names.Length;
        const string SavedChoice = "DeadlyJenga.CharacterDesign";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            PlayerLooks.Names = Names;
            PlayerLooks.Chosen = PlayerPrefs.GetInt(SavedChoice, 0);
            PlayerLooks.Changed += () => PlayerPrefs.SetInt(SavedChoice, PlayerLooks.Chosen);
        }

        static readonly Color Black = new Color(.06f, .05f, .07f), White = new Color(.97f, .95f, .93f),
            Silver = new Color(.75f, .78f, .82f), Gold = new Color(1, .78f, .2f), Pink = new Color(1, .45f, .7f),
            Red = new Color(.9f, .1f, .15f), Mint = new Color(.35f, 1, .8f), Brown = new Color(.4f, .22f, .12f),
            Purple = new Color(.45f, .15f, .65f), Cyan = new Color(.2f, .9f, 1), Orange = new Color(1, .5f, .1f);

        public static void Build(int design, CandyDesignParts p)
        {
            switch (design)
            {
                case 1: GummyBear(p); break;
                case 2: Cupcake(p); break;
                case 3: LollipopKnight(p); break;
                case 4: AstroMint(p); break;
                case 5: LicoriceNinja(p); break;
                case 6: ChocoKing(p); break;
                case 7: AngelCake(p); break;
                case 8: BubbleBot(p); break;
                case 9: PartyPopper(p); break;
            }
        }

        static void Eyes(CandyDesignParts p, float size = .045f)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                p.Head(PrimitiveType.Sphere, new Vector3(s * .075f, .035f, .19f), Vector3.zero, new Vector3(size, size * 1.25f, size * .6f), Black);
                p.Head(PrimitiveType.Sphere, new Vector3(s * .075f + .012f, .05f, .205f), Vector3.zero, Vector3.one * size * .3f, White, CandyLook.Glow);
            }
        }

        static void GummyBear(CandyDesignParts p)
        {
            var gummy = new Color(.95f, .3f, .2f);
            for (int s = -1; s <= 1; s += 2)
            {
                p.Head(PrimitiveType.Sphere, new Vector3(s * .15f, .17f, -.02f), Vector3.zero, new Vector3(.13f, .13f, .07f), gummy);
                p.Head(PrimitiveType.Sphere, new Vector3(s * .15f, .17f, .01f), Vector3.zero, new Vector3(.07f, .07f, .04f), Pink);
            }
            p.Head(PrimitiveType.Sphere, new Vector3(0, -.06f, .17f), Vector3.zero, new Vector3(.14f, .1f, .09f), new Color(1, .85f, .7f));
            p.Head(PrimitiveType.Sphere, new Vector3(0, -.035f, .215f), Vector3.zero, new Vector3(.05f, .035f, .03f), Black);
            Eyes(p);
            p.Torso(PrimitiveType.Sphere, new Vector3(0, .8f, .1f), Vector3.zero, new Vector3(.24f, .3f, .06f), new Color(1, .85f, .7f));
        }

        static void Cupcake(CandyDesignParts p)
        {
            p.Head(PrimitiveType.Sphere, new Vector3(0, .19f, 0), Vector3.zero, new Vector3(.44f, .13f, .44f), Pink);
            p.Head(PrimitiveType.Sphere, new Vector3(0, .26f, 0), Vector3.zero, new Vector3(.33f, .11f, .33f), White);
            p.Head(PrimitiveType.Sphere, new Vector3(0, .32f, 0), Vector3.zero, new Vector3(.21f, .1f, .21f), Pink);
            p.Head(PrimitiveType.Sphere, new Vector3(0, .4f, 0), Vector3.zero, Vector3.one * .08f, Red);
            p.Head(PrimitiveType.Cylinder, new Vector3(.01f, .46f, 0), new Vector3(0, 0, -15), new Vector3(.012f, .03f, .012f), new Color(.2f, .55f, .2f));
            Color[] sprinkles = { Cyan, Gold, Mint, Purple, Orange, White };
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36 * Mathf.Deg2Rad;
                p.Head(PrimitiveType.Capsule, new Vector3(Mathf.Cos(a) * .17f, .23f + (i % 2) * .04f, Mathf.Sin(a) * .17f),
                    new Vector3(90, i * 47, 0), new Vector3(.015f, .02f, .015f), sprinkles[i % sprinkles.Length], CandyLook.Glow);
            }
            Eyes(p);
        }

        static void LollipopKnight(CandyDesignParts p)
        {
            p.Head(PrimitiveType.Sphere, Vector3.zero, Vector3.zero, new Vector3(.48f, .48f, .47f), Silver);
            p.Head(PrimitiveType.Cube, new Vector3(0, .03f, .225f), Vector3.zero, new Vector3(.3f, .045f, .03f), Black);
            for (int i = 0; i < 4; i++)
                p.Head(PrimitiveType.Capsule, new Vector3(0, .25f - i * .015f, .08f - i * .08f), new Vector3(-30 - i * 20, 0, 0), new Vector3(.07f, .1f, .07f), Red);
            p.Torso(PrimitiveType.Cylinder, new Vector3(0, .95f, -.2f), new Vector3(90, 0, 0), new Vector3(.5f, .02f, .5f), Pink);
            p.Torso(PrimitiveType.Cylinder, new Vector3(0, .95f, -.215f), new Vector3(90, 0, 0), new Vector3(.34f, .02f, .34f), White);
            p.Torso(PrimitiveType.Cylinder, new Vector3(0, .95f, -.23f), new Vector3(90, 0, 0), new Vector3(.18f, .02f, .18f), Pink);
            p.Torso(PrimitiveType.Cylinder, new Vector3(0, .5f, -.19f), Vector3.zero, new Vector3(.035f, .25f, .035f), White);
        }

        static void AstroMint(CandyDesignParts p)
        {
            p.Head(PrimitiveType.Sphere, Vector3.zero, Vector3.zero, Vector3.one * .6f, new Color(.6f, 1, .9f, .22f), CandyLook.Glass);
            p.Head(PrimitiveType.Cylinder, new Vector3(0, .33f, 0), Vector3.zero, new Vector3(.015f, .08f, .015f), Silver);
            p.Head(PrimitiveType.Sphere, new Vector3(0, .43f, 0), Vector3.zero, Vector3.one * .06f, Mint, CandyLook.Glow);
            Eyes(p);
            for (int s = -1; s <= 1; s += 2)
            {
                p.Torso(PrimitiveType.Cylinder, new Vector3(s * .09f, .95f, -.21f), Vector3.zero, new Vector3(.12f, .17f, .12f), Silver);
                p.Torso(PrimitiveType.Cylinder, new Vector3(s * .09f, .74f, -.21f), Vector3.zero, new Vector3(.08f, .04f, .08f), Orange, CandyLook.Glow);
            }
            p.Torso(PrimitiveType.Cube, new Vector3(0, 1f, -.16f), Vector3.zero, new Vector3(.3f, .25f, .06f), Mint);
        }

        static void LicoriceNinja(CandyDesignParts p)
        {
            p.Head(PrimitiveType.Cylinder, new Vector3(0, .08f, 0), Vector3.zero, new Vector3(.47f, .035f, .47f), Red);
            for (int s = -1; s <= 1; s += 2)
                p.Head(PrimitiveType.Cube, new Vector3(s * .05f, .06f, -.3f), new Vector3(25, s * 18, 0), new Vector3(.05f, .015f, .22f), Red);
            p.Head(PrimitiveType.Sphere, new Vector3(0, -.08f, .06f), Vector3.zero, new Vector3(.46f, .2f, .4f), Black);
            p.Head(PrimitiveType.Sphere, new Vector3(0, .16f, -.01f), Vector3.zero, new Vector3(.46f, .2f, .45f), Black);
            for (int s = -1; s <= 1; s += 2)
                p.Head(PrimitiveType.Cube, new Vector3(s * .07f, .03f, .205f), new Vector3(0, 0, s * -8), new Vector3(.08f, .025f, .02f), White, CandyLook.Glow);
            p.Torso(PrimitiveType.Cube, new Vector3(0, 1f, -.17f), new Vector3(0, 0, 40), new Vector3(.05f, .6f, .04f), Silver);
        }

        static void ChocoKing(CandyDesignParts p)
        {
            p.Head(PrimitiveType.Cylinder, new Vector3(0, .19f, 0), Vector3.zero, new Vector3(.32f, .04f, .32f), Gold);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60 * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Cos(a) * .15f, .26f, Mathf.Sin(a) * .15f);
                p.Head(PrimitiveType.Cube, at, new Vector3(45, i * 60, 45), Vector3.one * .065f, Gold);
                p.Head(PrimitiveType.Sphere, at + Vector3.up * .055f, Vector3.zero, Vector3.one * .03f, i % 2 == 0 ? Red : Cyan, CandyLook.Glow);
            }
            Eyes(p);
            for (int s = -1; s <= 1; s += 2)
                p.Head(PrimitiveType.Capsule, new Vector3(s * .045f, -.06f, .2f), new Vector3(0, 0, s * 70), new Vector3(.035f, .05f, .035f), Brown);
            p.Torso(PrimitiveType.Cube, new Vector3(0, .78f, -.18f), new Vector3(8, 0, 0), new Vector3(.46f, .9f, .025f), Purple);
            p.Torso(PrimitiveType.Capsule, new Vector3(0, 1.22f, -.06f), new Vector3(0, 0, 90), new Vector3(.12f, .26f, .12f), White);
        }

        static void AngelCake(CandyDesignParts p)
        {
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f * Mathf.Deg2Rad;
                p.Head(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * .16f, .36f, Mathf.Sin(a) * .16f), Vector3.zero, Vector3.one * .045f, Gold, CandyLook.Glow);
            }
            Eyes(p, .035f);
            for (int s = -1; s <= 1; s += 2)
            {
                p.Head(PrimitiveType.Sphere, new Vector3(s * .12f, -.04f, .165f), Vector3.zero, new Vector3(.06f, .035f, .02f), Pink);
                p.Torso(PrimitiveType.Sphere, new Vector3(s * .2f, 1.02f, -.2f), new Vector3(0, s * 35, s * 25), new Vector3(.36f, .48f, .05f), White);
                p.Torso(PrimitiveType.Sphere, new Vector3(s * .3f, .85f, -.22f), new Vector3(0, s * 35, s * 40), new Vector3(.25f, .32f, .04f), White);
            }
        }

        static void BubbleBot(CandyDesignParts p)
        {
            p.Head(PrimitiveType.Cylinder, new Vector3(0, .3f, 0), Vector3.zero, new Vector3(.015f, .08f, .015f), Silver);
            p.Head(PrimitiveType.Sphere, new Vector3(0, .4f, 0), Vector3.zero, Vector3.one * .06f, Red, CandyLook.Glow);
            for (int s = -1; s <= 1; s += 2)
            {
                p.Head(PrimitiveType.Cube, new Vector3(s * .08f, .04f, .2f), Vector3.zero, new Vector3(.075f, .075f, .02f), Cyan, CandyLook.Glow);
                p.Head(PrimitiveType.Cylinder, new Vector3(s * .22f, 0, 0), new Vector3(0, 0, 90), new Vector3(.09f, .03f, .09f), Silver);
            }
            p.Head(PrimitiveType.Cube, new Vector3(0, -.08f, .2f), Vector3.zero, new Vector3(.16f, .03f, .02f), Silver);
            p.Torso(PrimitiveType.Cube, new Vector3(0, .95f, .13f), Vector3.zero, new Vector3(.22f, .18f, .04f), Silver);
            Color[] lights = { Red, Gold, Mint };
            for (int i = 0; i < 3; i++)
                p.Torso(PrimitiveType.Sphere, new Vector3(-.06f + i * .06f, .95f, .155f), Vector3.zero, Vector3.one * .035f, lights[i], CandyLook.Glow);
        }

        static void PartyPopper(CandyDesignParts p)
        {
            Color[] stripes = { Pink, Cyan, Gold, Mint, Purple };
            for (int i = 0; i < 5; i++)
            {
                float r = Mathf.Lerp(.2f, .04f, i / 4f);
                p.Head(PrimitiveType.Cylinder, new Vector3(0, .2f + i * .05f, 0), Vector3.zero, new Vector3(r, .025f, r), stripes[i]);
            }
            p.Head(PrimitiveType.Sphere, new Vector3(0, .47f, 0), Vector3.zero, Vector3.one * .07f, White);
            for (int s = -1; s <= 1; s += 2)
                p.Head(PrimitiveType.Cube, new Vector3(s * .075f, .04f, .205f), Vector3.zero, new Vector3(.12f, .07f, .02f), Black);
            p.Head(PrimitiveType.Cube, new Vector3(0, .055f, .21f), Vector3.zero, new Vector3(.05f, .015f, .02f), Black);
            p.Head(PrimitiveType.Capsule, new Vector3(0, -.08f, .19f), new Vector3(0, 0, 90), new Vector3(.04f, .07f, .04f), Red);
        }
    }

    public enum CandyLook { Solid, Glow, Glass }

    // Makes the shapes for one design and remembers them so the design can be swapped.
    public sealed class CandyDesignParts
    {
        public readonly Transform head, torso;
        readonly Material candy;
        readonly List<GameObject> made;
        readonly List<Renderer> headRenderers;
        static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static MaterialPropertyBlock block;

        public CandyDesignParts(Transform head, Transform torso, Material candy, List<GameObject> made, List<Renderer> headRenderers)
        { this.head = head; this.torso = torso; this.candy = candy; this.made = made; this.headRenderers = headRenderers; }

        public void Head(PrimitiveType shape, Vector3 position, Vector3 euler, Vector3 size, Color color, CandyLook look = CandyLook.Solid)
            => Add(head, shape, position, euler, size, color, look);
        public void Torso(PrimitiveType shape, Vector3 position, Vector3 euler, Vector3 size, Color color, CandyLook look = CandyLook.Solid)
            => Add(torso, shape, position, euler, size, color, look);

        void Add(Transform bone, PrimitiveType shape, Vector3 position, Vector3 euler, Vector3 size, Color color, CandyLook look)
        {
            if (!bone) return;
            var go = new GameObject(shape.ToString());
            go.transform.SetParent(bone, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = Mesh(shape);
            var r = go.AddComponent<MeshRenderer>();
            if (look == CandyLook.Solid && candy)
            {
                r.sharedMaterial = candy;
                block ??= new MaterialPropertyBlock();
                block.Clear(); block.SetColor("_BaseColor", color); block.SetFloat("_Smoothness", .8f);
                r.SetPropertyBlock(block);
            }
            else r.sharedMaterial = Material(color, look);
            made.Add(go);
            if (bone == head) headRenderers.Add(r);
        }

        public static Mesh Mesh(PrimitiveType shape)
        {
            if (meshes.TryGetValue(shape, out var mesh) && mesh) return mesh;
            var temp = GameObject.CreatePrimitive(shape);
            mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(temp); else Object.DestroyImmediate(temp);
            return meshes[shape] = mesh;
        }

        public static Material Material(Color color, CandyLook look)
        {
            string key = look + ColorUtility.ToHtmlStringRGBA(color);
            if (materials.TryGetValue(key, out var m) && m) return m;
            var shader = Shader.Find(look == CandyLook.Solid ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            m = new Material(shader) { name = "Candy design " + key, enableInstancing = true };
            if (look == CandyLook.Glass)
            {
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return materials[key] = m;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { materials.Clear(); }
    }
}
