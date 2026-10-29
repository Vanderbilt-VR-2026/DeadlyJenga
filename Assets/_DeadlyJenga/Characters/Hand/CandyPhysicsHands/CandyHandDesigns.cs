using UnityEngine;
using DeadlyJenga.Core;
using DeadlyJenga.CandyPlayers;

namespace DeadlyJenga.CandyPhysicsHands
{
    // Every giant-hand design: a skin finish plus shapes, particles and a trail on the hand's 16 bones.
    // Finger bones point along their local up; the palm box sits around (0, 0, 0.02) in palm space.
    // All sizes are multiplied by the hand's size so designs work at any scale.
    public static class CandyHandDesigns
    {
        public static readonly string[] Names =
        {
            "Classic", "Robo Claw", "Skeleton", "Lava", "Frost", "Gummy", "Chocolate", "Ghost", "Golden Gauntlet"
        };
        public static int Count => Names.Length;
        const string SavedChoice = "DeadlyJenga.HandDesign";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            HandLooks.Names = Names;
            HandLooks.Chosen = PlayerPrefs.GetInt(SavedChoice, 0);
            HandLooks.Changed += () => PlayerPrefs.SetInt(SavedChoice, HandLooks.Chosen);
        }

        static readonly Color Steel = new Color(.55f, .58f, .63f), Dark = new Color(.18f, .19f, .22f), Cyan = new Color(.2f, .95f, 1),
            Bone = new Color(.95f, .93f, .85f), Toxic = new Color(.35f, 1, .45f), Ember = new Color(1, .45f, .05f), Basalt = new Color(.12f, .08f, .07f),
            Ice = new Color(.75f, .93f, 1), Choco = new Color(.33f, .17f, .07f), Gold = new Color(1, .76f, .18f), DeepGold = new Color(.72f, .5f, .08f),
            Ruby = new Color(1, .1f, .2f), Pink = new Color(1, .5f, .75f);

        public static void Build(int design, CandyHandParts p)
        {
            switch (design)
            {
                case 1: Robo(p); break;
                case 2: Skeleton(p); break;
                case 3: Lava(p); break;
                case 4: Frost(p); break;
                case 5: Gummy(p); break;
                case 6: Chocolate(p); break;
                case 7: Ghost(p); break;
                case 8: Gold_(p); break;
            }
        }

        static void Robo(CandyHandParts p)
        {
            float s = p.size;
            p.Skin(Steel, .9f, .75f);
            p.Add(p.Palm, PrimitiveType.Cube, new Vector3(0, 0, .02f) * s, Vector3.zero, new Vector3(.086f, .048f, .07f) * s, Dark);
            p.Add(p.Palm, PrimitiveType.Cube, new Vector3(0, 0, .02f) * s, Vector3.zero, new Vector3(.02f, .052f, .062f) * s, Cyan, CandyLook.Glow);
            for (int i = 1; i < 16; i++)
            {
                float r = p.Radius(i), l = p.Length(i);
                p.Add(p.Bone(i), PrimitiveType.Cube, new Vector3(0, l * .5f, 0), Vector3.zero, new Vector3(r * 2.3f, l * .7f, r * 2.3f), Steel);
                p.Add(p.Bone(i), PrimitiveType.Sphere, Vector3.zero, Vector3.zero, Vector3.one * r * 1.6f, Cyan, CandyLook.Glow);
            }
            p.Particles(new Color(1, .7f, .2f), 8, .35f, .06f * s, .004f * s, 1.5f);
        }

        static void Skeleton(CandyHandParts p)
        {
            float s = p.size;
            p.SkinMaterial(new Color(.3f, 1, .5f, .16f));
            for (int i = 1; i < 16; i++)
            {
                float r = p.Radius(i), l = p.Length(i);
                p.Add(p.Bone(i), PrimitiveType.Capsule, new Vector3(0, l * .5f, 0), Vector3.zero, new Vector3(r * 1.1f, l * .5f, r * 1.1f), Bone);
                p.Add(p.Bone(i), PrimitiveType.Sphere, Vector3.zero, Vector3.zero, Vector3.one * r * 1.3f, Bone);
            }
            for (int f = 0; f < 5; f++)
            {
                var root = p.Palm.InverseTransformPoint(p.Bone(p.Finger(f, 0)).position);
                p.Add(p.Palm, PrimitiveType.Capsule, root * .5f, Quaternion.FromToRotation(Vector3.up, root).eulerAngles,
                    new Vector3(.009f * s, root.magnitude * .5f, .009f * s), Bone);
            }
            p.Particles(Toxic, 6, 1.2f, .02f * s, .006f * s, -.05f);
        }

        static void Lava(CandyHandParts p)
        {
            float s = p.size;
            p.Skin(Basalt, 0, .15f);
            p.Add(p.Palm, PrimitiveType.Cube, new Vector3(0, 0, .02f) * s, new Vector3(0, 30, 0), new Vector3(.012f, .044f, .07f) * s, Ember, CandyLook.Glow);
            p.Add(p.Palm, PrimitiveType.Cube, new Vector3(0, 0, .02f) * s, new Vector3(0, -40, 0), new Vector3(.01f, .044f, .06f) * s, Ember, CandyLook.Glow);
            for (int i = 1; i < 16; i++)
            {
                float r = p.Radius(i), l = p.Length(i);
                p.Add(p.Bone(i), PrimitiveType.Cube, new Vector3(0, l * .5f, 0), new Vector3(0, i * 37, 0), new Vector3(r * .45f, l * .85f, r * 2.15f), Ember, CandyLook.Glow);
            }
            p.Particles(Ember, 25, .9f, .04f * s, .005f * s, -.15f);
            p.Trail(Ember, .35f, .03f * s);
        }

        static void Frost(CandyHandParts p)
        {
            float s = p.size;
            p.Skin(Ice, .1f, .97f);
            for (int f = 0; f < 5; f++)
            {
                int knuckle = p.Finger(f, 0), tip = p.Finger(f, 2);
                p.Add(p.Bone(knuckle), PrimitiveType.Cube, Vector3.zero, new Vector3(45, 20 * f, 45), Vector3.one * p.Radius(knuckle) * 1.8f, new Color(.8f, .97f, 1, .55f), CandyLook.Glass);
                p.Add(p.Bone(tip), PrimitiveType.Cube, new Vector3(0, p.Length(tip), 0), new Vector3(0, 45, 0), new Vector3(p.Radius(tip) * 1.2f, p.Radius(tip) * 3, p.Radius(tip) * 1.2f), new Color(.8f, .97f, 1, .6f), CandyLook.Glass);
            }
            for (int i = 0; i < 3; i++)
                p.Add(p.Palm, PrimitiveType.Cube, new Vector3(-.025f + i * .025f, 0, .02f + (i % 2) * .015f) * s, new Vector3(45, i * 30, 45), Vector3.one * .02f * s, new Color(.8f, .97f, 1, .55f), CandyLook.Glass);
            p.Particles(new Color(.9f, .97f, 1, .7f), 12, 1.5f, .015f * s, .012f * s, .02f);
            p.Trail(new Color(.7f, .95f, 1, .6f), .5f, .025f * s);
        }

        static void Gummy(CandyHandParts p)
        {
            float s = p.size;
            p.SkinMaterial(new Color(.35f, 1, .45f, .55f));
            for (int i = 0; i < 7; i++)
                p.Add(p.Palm, PrimitiveType.Sphere, new Vector3(Mathf.Sin(i * 1.7f) * .03f, (i % 2 == 0 ? .021f : -.021f), .02f + Mathf.Cos(i * 2.3f) * .025f) * s,
                    Vector3.zero, Vector3.one * .007f * s, Color.white, CandyLook.Glow);
            for (int f = 0; f < 5; f++)
            {
                int tip = p.Finger(f, 2);
                p.Add(p.Bone(tip), PrimitiveType.Sphere, new Vector3(0, p.Length(tip) * .7f, 0), Vector3.zero, Vector3.one * p.Radius(tip) * .7f, Color.white, CandyLook.Glow);
            }
        }

        static void Chocolate(CandyHandParts p)
        {
            float s = p.size;
            p.Skin(Choco, 0, .65f);
            for (int f = 0; f < 5; f++)
            {
                int tip = p.Finger(f, 2); float r = p.Radius(tip);
                p.Add(p.Bone(tip), PrimitiveType.Sphere, new Vector3(0, p.Length(tip) * .9f, r * .6f), Vector3.zero, new Vector3(r * 1.1f, r * 2.2f, r * 1.1f), Choco);
            }
            p.Add(p.Palm, PrimitiveType.Cube, new Vector3(0, 0, .02f) * s, new Vector3(0, 20, 0), new Vector3(.01f, .043f, .075f) * s, Pink);
            p.Add(p.Palm, PrimitiveType.Cube, new Vector3(0, 0, .02f) * s, new Vector3(0, -25, 0), new Vector3(.01f, .043f, .07f) * s, Pink);
            p.Particles(Choco, 6, 1.2f, 0, .008f * s, 2);
        }

        static void Ghost(CandyHandParts p)
        {
            float s = p.size;
            p.SkinMaterial(new Color(.85f, .92f, 1, .32f));
            for (int f = 0; f < 5; f++)
            {
                int tip = p.Finger(f, 2);
                p.Add(p.Bone(tip), PrimitiveType.Sphere, new Vector3(0, p.Length(tip), 0), Vector3.zero, Vector3.one * p.Radius(tip) * 1.3f, new Color(.7f, .85f, 1), CandyLook.Glow);
            }
            p.Particles(new Color(.85f, .92f, 1, .5f), 10, 2, .01f * s, .015f * s, -.03f);
            p.Trail(new Color(.85f, .92f, 1, .45f), 1.1f, .06f * s);
        }

        static void Gold_(CandyHandParts p)
        {
            float s = p.size;
            p.Skin(Gold, 1, .85f);
            p.Add(p.Palm, PrimitiveType.Cube, new Vector3(0, 0, .02f) * s, Vector3.zero, new Vector3(.084f, .046f, .068f) * s, DeepGold);
            p.Add(p.Palm, PrimitiveType.Sphere, new Vector3(0, 0, .02f) * s, Vector3.zero, new Vector3(.03f, .056f, .03f) * s, Ruby, CandyLook.Glow);
            for (int i = 1; i < 16; i++)
            {
                float r = p.Radius(i), l = p.Length(i);
                p.Add(p.Bone(i), PrimitiveType.Cylinder, new Vector3(0, l * .5f, 0), Vector3.zero, new Vector3(r * 2.4f, l * .12f, r * 2.4f), DeepGold);
                if ((i - 1) % 3 == 0) p.Add(p.Bone(i), PrimitiveType.Sphere, Vector3.zero, Vector3.zero, Vector3.one * r * 1.25f, Ruby, CandyLook.Glow);
            }
            p.Particles(new Color(1, .9f, .4f), 5, .8f, .02f * s, .004f * s, -.05f);
        }
    }
}
