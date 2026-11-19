using System;
using System.Collections.Generic;
using DeadlyJenga.Core;
using UnityEngine;
using Builder = DeadlyJenga.CandyPlayers.CandyAccessoryMeshes.Builder;

namespace DeadlyJenga.CandyPlayers
{
    internal static class CandyHeadgearMeshes
    {
        internal enum Item
        {
            TopHat, Crown, CowboyHat, PirateHat, ChefHat, VikingHelmet, PartyHat, PropellerCap,
            FlowerCrown, PumpkinHelmet, SkeletonHead, CatEars, DevilHorns, Broccoli,
            HockeyHelmet, FootballHelmet, Visor, CyberEye, BowTie, StrongJaw
        }

        internal static readonly (AvatarAccessory Accessory, Item Design)[] Accessories =
        {
            (AvatarAccessory.TopHat, Item.TopHat), (AvatarAccessory.Crown, Item.Crown),
            (AvatarAccessory.CowboyHat, Item.CowboyHat), (AvatarAccessory.PirateHat, Item.PirateHat),
            (AvatarAccessory.ChefHat, Item.ChefHat), (AvatarAccessory.VikingHelmet, Item.VikingHelmet),
            (AvatarAccessory.PartyHat, Item.PartyHat), (AvatarAccessory.PropellerCap, Item.PropellerCap),
            (AvatarAccessory.FlowerCrown, Item.FlowerCrown), (AvatarAccessory.PumpkinHelmet, Item.PumpkinHelmet),
            (AvatarAccessory.SkeletonMask, Item.SkeletonHead), (AvatarAccessory.CatEars, Item.CatEars),
            (AvatarAccessory.DevilHorns, Item.DevilHorns), (AvatarAccessory.Broccoli, Item.Broccoli),
            (AvatarAccessory.StrongJaw, Item.StrongJaw)
        };

        internal const AvatarAccessory RemodeledAttachments = AvatarAccessory.TopHat | AvatarAccessory.Crown |
            AvatarAccessory.CowboyHat | AvatarAccessory.PirateHat | AvatarAccessory.ChefHat | AvatarAccessory.VikingHelmet |
            AvatarAccessory.PartyHat | AvatarAccessory.PropellerCap | AvatarAccessory.FlowerCrown | AvatarAccessory.PumpkinHelmet |
            AvatarAccessory.SkeletonMask | AvatarAccessory.CatEars | AvatarAccessory.DevilHorns | AvatarAccessory.Broccoli |
            AvatarAccessory.StrongJaw;
        internal const AvatarAccessory ReplacementHeads = AvatarAccessory.SkeletonMask | AvatarAccessory.PumpkinHelmet;
        internal const AvatarAccessory SculptedHeads = ReplacementHeads | AvatarAccessory.StrongJaw;

        static readonly Dictionary<Item, (Mesh Mesh, Color Color)[]> designs = new();
        static readonly Color ink = Color32(32, 24, 45), cream = Color32(255, 234, 184),
            pearl = Color32(255, 249, 235), gold = Color32(251, 165, 43),
            red = Color32(212, 29, 72), wine = Color32(130, 23, 62),
            steel = Color32(129, 168, 201), navy = Color32(34, 53, 91),
            cyan = Color32(43, 206, 240), leaf = Color32(60, 154, 56);
        static readonly Quaternion flat = Quaternion.Euler(90f, 0f, 0f);

        internal static (Mesh Mesh, Color Color)[] Get(Item item)
        {
            if (designs.TryGetValue(item, out var parts)) return parts;
            var colors = new Dictionary<Color, Builder>();
            Builder Paint(Color color)
            {
                if (!colors.TryGetValue(color, out var mesh)) colors[color] = mesh = new Builder { Soft = true };
                return mesh;
            }

            switch (item)
            {
                case Item.TopHat: TopHat(Paint); break;
                case Item.Crown: Crown(Paint); break;
                case Item.CowboyHat: Cowboy(Paint); break;
                case Item.PirateHat: Pirate(Paint); break;
                case Item.ChefHat: Chef(Paint); break;
                case Item.VikingHelmet: Viking(Paint); break;
                case Item.PartyHat: Party(Paint); break;
                case Item.PropellerCap: Propeller(Paint); break;
                case Item.FlowerCrown: FlowerCrown(Paint); break;
                case Item.PumpkinHelmet: Pumpkin(Paint); break;
                case Item.SkeletonHead: Skull(Paint); break;
                case Item.CatEars: Ears(Paint); break;
                case Item.DevilHorns: Horns(Paint); break;
                case Item.Broccoli: Broccoli(Paint); break;
                case Item.HockeyHelmet: SportsHelmet(Paint, false); break;
                case Item.FootballHelmet: SportsHelmet(Paint, true); break;
                case Item.Visor: Visor(Paint); break;
                case Item.CyberEye: CyberEye(Paint); break;
                case Item.BowTie: BowTie(Paint); break;
                case Item.StrongJaw: Jaw(Paint); break;
            }

            parts = new (Mesh, Color)[colors.Count];
            int i = 0;
            foreach (var entry in colors)
                parts[i++] = (entry.Value.Mesh(item.ToString()), entry.Key);
            return designs[item] = parts;
        }

        internal static bool UsesCosmeticShade(Item item, Color finish)
        {
            switch (item)
            {
                case Item.TopHat:
                case Item.PirateHat: return finish == ink;
                case Item.Crown: return finish == gold;
                case Item.CowboyHat: return finish == Color32(147, 69, 29) || finish == Color32(188, 104, 51);
                case Item.ChefHat: return finish == pearl;
                case Item.VikingHelmet:
                case Item.CyberEye: return finish == steel;
                case Item.PartyHat: return finish == Color32(129, 49, 222);
                case Item.PropellerCap: return finish == red || finish == gold || finish == cyan || finish == leaf;
                case Item.FlowerCrown: return finish != leaf && finish != gold;
                case Item.PumpkinHelmet: return finish == Color32(246, 103, 20);
                case Item.SkeletonHead: return finish == pearl || finish == cream;
                case Item.CatEars: return finish == Color32(99, 60, 135);
                case Item.DevilHorns:
                case Item.BowTie: return finish == red;
                case Item.Broccoli: return finish == Color32(28, 146, 63) || finish == leaf;
                case Item.Visor: return finish == Color32(48, 141, 241);
                case Item.StrongJaw:
                case Item.HockeyHelmet:
                case Item.FootballHelmet: return finish == Color.clear;
                default: return false;
            }
        }

        internal static void ApplyFinish(Renderer renderer, MaterialPropertyBlock block, Color color)
        {
            block.Clear();
            block.SetColor("_BaseColor", color);
            block.SetFloat("_Smoothness", .89f);
            block.SetColor("_EmissionColor", color.linear * .035f);
            renderer.SetPropertyBlock(block);
        }

        static Color Color32(byte r, byte g, byte b) => new Color32(r, g, b, 255);
        static Vector3 V(float x, float y, float z) => new(x, y, z);
        static Vector3 Radial(float angle) => V(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        static float Front(float angle) => Mathf.Max(0f, Mathf.Cos(angle));

        // Profile runs from lower/back face to upper/front face. The arc is
        // sampled across authored sections so bends stay smooth.
        static void Turn(Builder b, Func<float, float, Vector3> surface, int rows = 12, int columns = 48) =>
            b.Surface(surface, rows, columns);

        static void Lathe(Builder b, float depth, params (float Y, float R)[] profile)
        {
            Turn(b, (a, t) =>
            {
                float index = t * (profile.Length - 1);
                int i = Mathf.Min(profile.Length - 2, Mathf.FloorToInt(index));
                float blend = index - i;
                float r = Mathf.Lerp(profile[i].R, profile[i + 1].R, blend);
                return V(Mathf.Sin(a) * r, Mathf.Lerp(profile[i].Y, profile[i + 1].Y, blend), Mathf.Cos(a) * r * depth);
            }, profile.Length - 1);
        }

        static void Brim(Builder b, float x, float z, Func<float, float> edge)
        {
            Turn(b, (a, t) =>
            {
                float f = 1f - Mathf.Abs(t * 2f - 1f);
                float r = Mathf.Lerp(.51f, 1f, f);
                return V(Mathf.Sin(a) * x * r, .173f + edge(a) * f - (t < .5f ? .014f : 0f) * (1f - f),
                    Mathf.Cos(a) * z * r);
            }, 12, 64);
        }

        static void TopHat(Func<Color, Builder> paint)
        {
            Brim(paint(ink), .319f, .287f, a => .022f * Mathf.Cos(a * 2f));
            Lathe(paint(ink), .91f, (.16f,.167f), (.20f,.190f), (.24f,.184f), (.34f,.179f),
                (.462f,.202f), (.487f,.194f), (.499f,.170f), (.502f,0f));
            Lathe(paint(wine), .92f, (.211f,.192f), (.222f,.198f), (.273f,.190f), (.282f,.182f));
            paint(gold).Pillow(V(-.035f,.267f,.175f), V(.035f,.267f,.175f), V(-.031f,.224f,.185f),
                V(.031f,.224f,.185f), .014f, 0f, 0f, 4, 4);
            paint(ink).Sphere(V(0,.246f,.197f), V(.020f,.012f,.010f));
        }

        static void Crown(Func<Color, Builder> paint)
        {
            float Edge(float a)
            {
                float teeth = Mathf.Pow(.5f + .5f * Mathf.Cos(a * 6f), 2f);
                return .264f + .118f * teeth + .043f * Mathf.Pow(Front(a), 14f);
            }
            Turn(paint(gold), (a, t) =>
            {
                float rim = .189f + .034f * Mathf.Sin(t * Mathf.PI * .54f);
                return V(Mathf.Sin(a) * rim, Mathf.Lerp(.147f, Edge(a), t), Mathf.Cos(a) * rim * .94f);
            }, 13, 72);
            Turn(paint(gold), (a, t) =>
            {
                float rim = .174f + .035f * Mathf.Sin((1f - t) * Mathf.PI * .54f);
                return V(Mathf.Sin(a) * rim, Mathf.Lerp(Edge(a) - .004f, .153f, t), Mathf.Cos(a) * rim * .94f);
            }, 13, 72);
            foreach (float y in new[] { .170f, .209f })
                paint(cream).Ring(V(0,y,0), .196f + (y - .17f) * .20f, .009f, flat, new Vector2(1f,.94f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Vector3 n = Radial(a), p = n * .218f + V(0,.266f,0);
                paint(cream).Sphere(p, V(.032f,.047f,.016f), Quaternion.FromToRotation(Vector3.forward,n));
                paint(i == 0 ? red : cyan).Sphere(p + n * .014f, V(.023f,.034f,.012f),
                    Quaternion.FromToRotation(Vector3.forward,n));
                paint(gold).Sphere(n * .223f + V(0,Edge(a),0), Vector3.one * .018f);
            }
        }

        static void Cowboy(Func<Color, Builder> paint)
        {
            Color leather = Color32(147, 69, 29), toffee = Color32(188, 104, 51);
            Brim(paint(leather), .344f, .306f, a =>
                .106f * Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 3.4f) - .036f * Front(a));
            Turn(paint(toffee), (a, t) =>
            {
                float r = Mathf.Lerp(.197f, .160f, Mathf.SmoothStep(0f, 1f, t));
                float cap = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.78f, 1f, t));
                r *= 1f - cap;
                return V(Mathf.Sin(a) * r * (.89f + .11f * Mathf.Cos(a * 2f)),
                    .166f + .252f * t - .040f * cap - .031f * t * Mathf.Pow(Front(a), 4f), Mathf.Cos(a) * r);
            }, 18);
            Lathe(paint(ink), 1.06f, (.198f,.185f), (.202f,.192f), (.233f,.189f), (.239f,.184f));
            paint(gold).Sphere(V(0,.222f,.209f), V(.036f,.029f,.010f));
        }

        static void Pirate(Func<Color, Builder> paint)
        {
            Lathe(paint(ink), .92f, (.138f,.20f), (.169f,.221f), (.243f,.192f),
                (.326f,.168f), (.342f,.123f), (.344f,0f));
            Vector3 Fold(float a, float t)
            {
                float flap = .5f + .5f * Mathf.Cos(a * 3f);
                float radius = Mathf.Lerp(.197f, .273f + .047f * (1f - flap), t);
                return Radial(a) * radius + V(0, .157f + t * (.092f + .165f * flap), -.023f);
            }
            Turn(paint(ink), Fold, 12, 72);
            Turn(paint(ink), (a,t) => Fold(a,1f - t) - Radial(a) * .010f, 10, 72);
            paint(gold).Tube(t => Fold(t * Mathf.PI * 2f, 1f), t => .011f, 72, 8);
            var mark = paint(cream);
            mark.Sphere(V(0,.315f,.244f), V(.043f,.041f,.018f));
            mark.Box(V(0,.280f,.251f), V(.054f,.031f,.019f));
            mark.Beam(V(-.055f,.278f,.238f), V(.055f,.250f,.249f), .007f);
            mark.Beam(V(.055f,.278f,.238f), V(-.055f,.250f,.249f), .007f);
            for (int s = -1; s <= 1; s += 2)
                paint(ink).Sphere(V(s*.016f,.315f,.261f), Vector3.one * .010f);
        }

        static void Chef(Func<Color, Builder> paint)
        {
            Lathe(paint(cream), .93f, (.145f,.190f), (.156f,.204f), (.214f,.201f), (.224f,.194f));
            Turn(paint(pearl), (a, t) =>
            {
                float scallop = .5f + .5f * Mathf.Cos(a * 10f);
                float dome = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.72f, 1f, t));
                float r = (.185f + .038f * Mathf.Sin(t * Mathf.PI * .82f) +
                    .013f * scallop * Mathf.Sin(t * Mathf.PI)) * (1f - dome);
                return Radial(a) * r + V(0,.195f + .302f * t - .021f * dome,0);
            }, 22, 72);
        }

        static void Viking(Func<Color, Builder> paint)
        {
            HelmetShell(paint(steel), a => 1.48f - .28f * Front(a) + .19f * Front(a + Mathf.PI), .260f, .276f, 0f);
            paint(gold).Ring(V(0,.112f,0), .225f, .015f, flat, new Vector2(1f,.96f));
            paint(gold).Tube(t => V(0,.026f + .258f * Mathf.Sin(t * Mathf.PI), .252f * Mathf.Cos(t * Mathf.PI)),
                t => .022f, 24, 8);
            paint(gold).Pillow(V(-.028f,.13f,.239f), V(.028f,.13f,.239f),
                V(-.012f,-.047f,.234f), V(.012f,-.047f,.234f), .017f, .005f, 0f, 4, 6);
            for (int s = -1; s <= 1; s += 2)
            {
                paint(gold).Sphere(V(s*.222f,.149f,0), V(.046f,.081f,.075f));
                paint(cream).Tube(t => V(s * (.225f + .160f*t), .143f + .25f*t*t, .029f*t),
                    t => .065f * (1f - t) + .003f);
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = Radial(s * (i + 1) * .50f) * .234f + V(0,.112f,0);
                    paint(navy).Sphere(p, Vector3.one*.012f);
                }
            }
        }

        static void Party(Func<Color, Builder> paint)
        {
            Color violet = Color32(129, 49, 222);
            paint(violet).Cone(V(0,.155f,0), V(.072f,.554f,0), .173f, .006f, 24);
            paint(cyan).Ring(V(0,.168f,0), .167f, .018f, flat);
            paint(gold).Sphere(V(.071f,.554f,0), Vector3.one*.043f);
            for (int i = 0; i < 23; i++)
            {
                float a = i * 2.399963f, t = .18f + (i % 5) * .145f;
                Vector3 n = Radial(a);
                Vector3 p = n * (.167f * (1f - t)) + V(.072f*t,.155f + .399f*t,0);
                paint(i % 2 == 0 ? gold : cyan).Sphere(p, V(.016f,.021f,.008f),
                    Quaternion.FromToRotation(Vector3.forward,n));
            }
        }

        static void Propeller(Func<Color, Builder> paint)
        {
            float edge = 1.16f;
            for (int sector = 0; sector < 4; sector++)
            {
                float start = sector * Mathf.PI / 2f;
                Color color = sector switch { 0 => red, 1 => gold, 2 => cyan, _ => leaf };
                paint(color).Surface((a,t) =>
                {
                    float angle = start + a / (Mathf.PI * 2f) * Mathf.PI / 2f;
                    float phi = Mathf.Lerp(edge,.001f,t);
                    return Radial(angle) * (Mathf.Sin(phi)*.235f) + V(0,.278f * Mathf.Cos(phi),-.012f);
                }, 12, 14);
            }
            paint(red).Sphere(V(0,.108f,.182f), V(.167f,.026f,.167f), Quaternion.Euler(7f,0f,0f));
            paint(navy).Ring(V(0,.118f,-.012f), .217f, .012f, flat);
            paint(steel).Beam(V(0,.265f,-.012f), V(0,.350f,-.012f), .016f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f + 20f;
                Vector3 p = Quaternion.Euler(0f,a,0f) * V(0,.346f,.115f) - V(0,0,.012f);
                paint(cream).Sphere(p, V(.036f,.014f,.122f), Quaternion.Euler(0f,a,10f));
            }
            paint(red).Sphere(V(0,.354f,-.012f), V(.038f,.023f,.038f));
        }

        static void FlowerCrown(Func<Color, Builder> paint)
        {
            Color blush = Color32(246, 105, 155), lilac = Color32(174, 104, 244);
            paint(leaf).Ring(V(0,.135f,-.009f), .206f, .017f, flat);
            for (int i = 0; i < 9; i++)
            {
                float a = i * Mathf.PI * 2f / 9f;
                Vector3 n = Radial(a);
                Vector3 p = n * .212f + V(0,.145f,-.009f);
                Vector3 tangent = Vector3.Cross(n, Vector3.up);
                paint(leaf).Sphere(p - tangent*.032f + Vector3.up*.013f, V(.015f,.027f,.050f),
                    Quaternion.FromToRotation(Vector3.forward,tangent+Vector3.up*.53f));
                float size = 1f - .25f * Mathf.Abs(Mathf.Sin(a*.5f));
                Color petals = i % 3 == 0 ? pearl : i % 3 == 1 ? blush : lilac;
                Quaternion facing = Quaternion.FromToRotation(Vector3.forward, n + Vector3.up*.58f);
                for (int j = 0; j < 5; j++)
                {
                    float offset = j * 72f;
                    Quaternion q = facing * Quaternion.Euler(0,0,offset);
                    paint(petals).Sphere(p + q * V(0,.033f*size,.010f),
                        V(.024f,.034f,.016f)*size,q);
                }
                paint(gold).Sphere(p + n*.024f + Vector3.up*.013f, Vector3.one*.020f*size);
            }
        }

        static void Pumpkin(Func<Color, Builder> paint)
        {
            paint(Color32(246,103,20)).CarvedPumpkin(true);
            paint(wine).Sphere(V(0,0,.026f), V(.236f,.206f,.180f));
            paint(leaf).Tube(t => V(.053f*t*t,.245f + .136f*t,-.029f*t), t => .042f - .023f*t);
            paint(leaf).Sphere(V(.081f,.283f,.031f), V(.088f,.017f,.037f),Quaternion.Euler(0f,-24f,13f));
            paint(leaf).Tube(t => V(-.040f - .050f*t, .270f + .032f*Mathf.Sin(t*5f),
                .027f + .065f*t), t => Mathf.Lerp(.010f,.004f,t), 18, 8);
        }

        static void Skull(Func<Color, Builder> paint)
        {
            // Broad cranium with lower cheekbones and a separate jaw silhouette.
            Turn(paint(pearl), (a,t) =>
            {
                float phi = Mathf.Lerp(2.40f,.001f,t);
                Vector3 direction = Radial(a);
                float y = .068f + .204f*Mathf.Cos(phi);
                float taper = Mathf.Lerp(.79f,1f,Mathf.InverseLerp(-.125f,.065f,y));
                return V(direction.x * .226f * Mathf.Sin(phi)*taper, y,
                    direction.z * .211f * Mathf.Sin(phi) - .022f);
            }, 24, 64);
            paint(cream).Sphere(V(0,-.137f,.024f), V(.154f,.096f,.154f));
            paint(ink).Sphere(V(0,-.128f,.157f), V(.125f,.045f,.033f));
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 c = V(s*.083f,.057f,.185f);
                Quaternion face = Quaternion.Euler(0f,s*15f,s*8f);
                paint(cream).Sphere(c + V(0,0,-.017f), V(.091f,.086f,.037f),face);
                paint(ink).Sphere(c + V(0,0,.010f), V(.069f,.065f,.020f),face);
                paint(pearl).Sphere(V(s*.148f,-.047f,.127f),V(.054f,.036f,.062f),face);
            }
            paint(ink).Pillow(V(-.004f,.013f,.206f), V(.004f,.013f,.206f),
                V(-.028f,-.053f,.208f), V(.028f,-.053f,.208f), .014f, 0f, 0f, 4, 4);
            for (int i = -3; i <= 3; i++)
            {
                float x = i * .032f, z = .186f - .037f * Mathf.Pow(Mathf.Abs(i)/3f, 2f);
                paint(pearl).Box(V(x,-.101f,z), V(.026f,.062f,.031f));
                paint(pearl).Box(V(x,-.166f,z-.005f), V(.026f,.039f,.028f));
            }
        }

        static void Ears(Func<Color, Builder> paint)
        {
            Color lilac = Color32(99,60,135), pink = Color32(247,126,170);
            for (int s = -1; s <= 1; s += 2)
            {
                var outer = V(s*.249f,.131f,.038f); var inner = V(s*.069f,.161f,.034f);
                var tip = V(s*.199f,.382f,.015f);
                paint(lilac).Pillow(tip - V(.009f,0,0),tip + V(.009f,0,0),
                    s > 0 ? inner : outer,s > 0 ? outer : inner,.075f,.009f,0f,7,10);
                var p = tip + V(0,-.056f,.045f);
                var left = Vector3.Lerp(s > 0 ? inner : outer,tip,.24f)+V(0,0,.045f);
                var right = Vector3.Lerp(s > 0 ? outer : inner,tip,.24f)+V(0,0,.045f);
                paint(pink).Pillow(p-V(.006f,0,0),p+V(.006f,0,0),left,right,.026f,.009f,0f,5,8);
            }
        }

        static void Horns(Func<Color, Builder> paint)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 Path(float t) => V(s * (.131f + .095f * Mathf.Sin(t * 2.30f)),
                    .139f + .283f*t, .010f + .143f*t*t);
                paint(red).Tube(Path, t => .061f*Mathf.Pow(1f-t,.83f) + .002f);
                paint(wine).Sphere(Path(.07f),V(.074f,.040f,.073f),Quaternion.Euler(0,0,s*-17f));
            }
        }

        static void Broccoli(Func<Color, Builder> paint)
        {
            Color stem = Color32(128, 195, 65), floret = Color32(28, 146, 63);
            paint(stem).Tube(t => V(0,.141f + .206f*t,-.035f*t),
                t => .088f - .021f*Mathf.Sin(t*Mathf.PI));
            for (int i = 0; i < 7; i++)
            {
                Vector3 n = Radial(i * 2.399963f);
                float r = i == 0 ? 0f : .13f + .026f*(i % 3);
                Vector3 p = n * r + V(0, i == 0 ? .434f : .333f + .025f * (i%3),-.022f);
                paint(stem).Tube(t => Vector3.Lerp(V(0,.247f,-.02f),p,t), t => .037f-.009f*t, 10, 8);
                paint(floret).Sphere(p,V(.088f,.071f,.084f));
                for (int j = 0; j < 6; j++)
                {
                    Vector3 frill = Radial(j * 2.399963f + i) * .051f + V(0,.026f + .020f*(j%3),0);
                    paint((i+j)%4 == 0 ? leaf : floret).Sphere(p+frill,Vector3.one*(.038f + .004f*(j%3)));
                }
            }
        }

        static Vector3 HelmetPoint(float a, float phi, float radius, float height, float back) =>
            Radial(a) * (radius * Mathf.Sin(phi)) + V(0,height * Mathf.Cos(phi)+.022f,back);

        static void HelmetShell(Builder b, Func<float,float> extent, float radius, float height, float back)
        {
            Turn(b, (a,t) => HelmetPoint(a,Mathf.Lerp(extent(a),.001f,t),radius,height,back), 24, 64);
            Turn(b, (a,t) => HelmetPoint(a,Mathf.Lerp(extent(a)-.10f,extent(a),t),
                Mathf.Lerp(radius-.022f,radius,t),height,back), 4, 64);
        }

        static void SportsHelmet(Func<Color, Builder> paint, bool football)
        {
            Color shell = Color.clear;
            float Extent(float a) => 1.75f - (football?.67f:.56f)*Front(a) + .33f*Front(a+Mathf.PI);
            HelmetShell(paint(shell), Extent, football?.264f:.252f, football?.282f:.261f, -.014f);
            paint(navy).Tube(t => HelmetPoint(t*Mathf.PI*2f,Extent(t*Mathf.PI*2f),football?.263f:.252f,
                football?.282f:.261f,-.014f), t => .014f, 56, 8);
            if (football)
            {
                // Stripe follows the shell surface and finishes before the rim.
                paint(pearl).Surface((a,t) =>
                {
                    float phi = Mathf.Lerp(1.04f,-1.91f,t);
                    return V(Mathf.Lerp(-.035f,.035f,a/(Mathf.PI*2f)), .022f+.287f*Mathf.Cos(phi),
                        -.014f+.270f*Mathf.Sin(phi));
                }, 32, 6);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                if (football)
                    paint(shell).Sphere(V(s*.223f,-.015f,-.039f),V(.059f,.129f,.121f));
                else
                    for (int i = 0; i < 3; i++)
                        paint(navy).Sphere(V(s*(.083f+.040f*i), .253f-.016f*i, -.033f),
                            V(.013f,.014f,.064f),Quaternion.Euler(0,0,s*-19f));
                paint(navy).Sphere(V(s*.252f,-.015f,.015f),V(.021f,.044f,.033f));
                paint(steel).Sphere(V(s*.235f,.029f,.101f),Vector3.one*.022f);
            }

            Vector3 Bar(float angle, float y) => V(Mathf.Sin(angle)*.235f,y,Mathf.Cos(angle)*.266f+.027f);
            var bars = paint(steel);
            int rows = football ? 3 : 4;
            for (int i = 0; i < rows; i++)
            {
                float y = (football ? -.047f : .050f) - i * (football?.055f:.068f);
                bars.Tube(t => Bar(Mathf.Lerp(-1.15f,1.15f,t),y+Mathf.Abs(t-.5f)*.070f),
                    t => football?.012f:.007f, 26, 8);
            }

            int posts = football ? 4 : 6;
            for (int i = 0; i < posts; i++)
            {
                float angle = Mathf.Lerp(-1.09f,1.09f,i/(posts-1f));
                bars.Tube(t => Bar(angle*(1f-.17f*t),Mathf.Lerp(football?-.023f:.083f,-.161f,t)),
                    t => football?.012f:.007f, 14, 8);
            }
            paint(navy).Sphere(V(0,-.173f,.247f),V(.092f,.032f,.035f));
        }

        static void Visor(Func<Color, Builder> paint)
        {
            Vector3 Surface(float u, float v, float radius)
            {
                float a = Mathf.Lerp(-1.25f,1.25f,u);
                float tip = Mathf.Pow(Mathf.Abs(u*2f-1f),3f);
                float bridge = .031f*Mathf.Pow(1f-Mathf.Abs(u*2f-1f),10f);
                return Radial(a)*radius + V(0,Mathf.Lerp(-.066f+.063f*tip+bridge,.092f-.020f*tip,v),.012f);
            }
            paint(navy).Surface((a,t) => Surface(a/(Mathf.PI*2f),t,.252f),15,48);
            paint(Color32(48, 141, 241)).Surface((a,t) => Surface(Mathf.Lerp(.03f,.97f,a/(Mathf.PI*2f)),
                Mathf.Lerp(.10f,.90f,t),.261f), 15,48);
            paint(cyan).Tube(t => Surface(Mathf.Lerp(.038f,.962f,t),.91f,.265f), t => .008f,36,8);
            for (int s = -1; s <= 1; s += 2)
            {
                paint(navy).Pillow(V(s*.228f,.062f,.086f),V(s*.235f,.060f,-.086f),
                    V(s*.228f,.007f,.086f),V(s*.235f,.007f,-.086f),.030f,0f,0f,4,5);
                paint(steel).Sphere(V(s*.254f,.035f,.045f),V(.012f,.026f,.039f));
            }
        }

        static void CyberEye(Func<Color, Builder> paint)
        {
            var center = V(.095f,.031f,.204f);
            var angle = Quaternion.Euler(0f,19f,0f);
            var axis = angle*Vector3.forward;
            paint(navy).Sphere(center-axis*.009f,V(.095f,.094f,.060f),angle);
            paint(steel).Ring(center+axis*.049f,.060f,.017f,angle,new Vector2(1f,.96f));
            paint(ink).Sphere(center+axis*.054f,V(.050f,.049f,.026f),angle);
            paint(cyan).Ring(center+axis*.074f,.028f,.009f,angle);
            paint(cyan).Sphere(center+axis*.078f,V(.011f,.011f,.011f),angle);
            paint(navy).Tube(t => V(.151f+.077f*Mathf.Sin(t*1.8f),.061f-.04f*t,.189f-.210f*t),
                t => .024f-.006f*t);
            paint(steel).Sphere(V(.228f,.019f,-.001f),V(.028f,.046f,.052f));
            paint(cyan).Sphere(V(.252f,.020f,.014f),V(.008f,.011f,.014f));
        }

        static void BowTie(Func<Color, Builder> paint)
        {
            Vector3 Center(float t) => V(0,1.301f + .007f*Mathf.Sin(t*Mathf.PI*4f),.012f) +
                Radial(t*Mathf.PI*2f)*.127f;
            paint(wine).Tube(Center,t => .020f,32,8);
            for (int s = -1; s <= 1; s += 2)
            {
                var a = V(s*.019f,1.281f,.161f);
                var b = V(s*.137f,1.312f,.153f);
                var c = V(s*.018f,1.241f,.173f);
                var d = V(s*.139f,1.218f,.155f);
                if (s < 0) { (a,b)=(b,a); (c,d)=(d,c); }
                paint(red).Pillow(a,b,c,d,.047f,.011f,.012f,8,8);
                paint(wine).Tube(t => V(s*(.025f+.062f*t),1.262f + .011f*t,.190f+.006f*Mathf.Sin(t*Mathf.PI)),
                    t => Mathf.Lerp(.005f,.002f,t),14,8);
            }
            paint(wine).Sphere(V(0,1.261f,.190f),V(.039f,.034f,.026f));
            paint(red).Sphere(V(.006f,1.264f,.206f),V(.024f,.030f,.017f));
        }

        static void Jaw(Func<Color, Builder> paint)
        {
            // Shape the whole head so the cheek, jaw angle and chin share one surface.
            Turn(paint(Color.clear), (a,t) =>
            {
                float h = Mathf.Lerp(-.233f,.229f,t);
                float s = Mathf.Sin(a), c = Mathf.Cos(a);
                float dome = Mathf.Sqrt(Mathf.Max(0f,1f - Mathf.Pow(t * 2f - 1f, 2f)));
                float jaw = Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.00f,.22f,t)) *
                    (1f - Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.37f,.64f,t)));
                float cheek = Mathf.Exp(-Mathf.Pow((t-.55f)*7f,2f));
                float square = Mathf.Lerp(1f,.62f,jaw);
                float x = Mathf.Sign(s)*Mathf.Pow(Mathf.Abs(s),square);
                float z = Mathf.Sign(c)*Mathf.Pow(Mathf.Abs(c),square);
                float width = .210f*dome + .016f*jaw + .013f*cheek;
                float forward = Mathf.Max(0f,c);
                float depth = .203f*dome + .009f*jaw;
                float chin = .055f * Mathf.Exp(-Mathf.Pow((t-.235f)*8f,2f)) * Mathf.Pow(forward,2f);
                float mouth = -.012f * Mathf.Exp(-Mathf.Pow((t-.35f)*18f,2f)) * Mathf.Pow(forward,3f);
                float nose = .048f * Mathf.Exp(-Mathf.Pow((t-.48f)*13f,2f)) * Mathf.Pow(forward,23f);
                float brow = .012f * Mathf.Exp(-Mathf.Pow((t-.62f)*13f,2f)) * Mathf.Pow(forward,3f);
                return V(x*width,h - .01f*jaw*forward,z*depth + .012f*jaw + chin + mouth + nose + brow);
            }, 36, 72);
        }
    }
}
