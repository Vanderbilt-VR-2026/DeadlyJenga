using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    // The loose-block "submaps" that can sit on the Deck Ship. Each one is a list of plain physics blocks.
    public enum DeckShipCargoKind { TowerAndForts, ContainerYard, DominoRun, StepPyramid }

    // One loose block, in deck coordinates: x = starboard, y = up from the main deck, z = toward the bow.
    // size is the block's own length (x), height (y) and width (z) before it is turned by yaw degrees.
    public struct DeckShipPiece
    {
        public Vector3 position, size;
        public float yaw;
        public int color;
        public string role;
    }

    public static class DeckShipCargo
    {
        // One Jenga block: length is three widths, height is .6 widths.
        public const float Length = 4.5f, Height = .9f, Width = 1.5f;
        // The open middle deck the cargo may use (inside the rails, between the ramps).
        public const float FieldHalfWidth = 9.4f, FieldMinZ = -15f, FieldMaxZ = 15f;
        // The fixed cargo hatch in the middle of the deck (part of the ship, not cargo).
        public const float HatchHalf = 3f, HatchHeight = 1.5f;
        // A hair of space so blocks never start inside each other.
        const float Gap = .03f;

        public static int Count => 4;
        public static readonly string[] Titles = { "Tower and Forts", "Container Yard", "Domino Run", "Step Pyramid" };

        // hatch: height of the ship's raised hatch (0 = none). Tower and Forts' bridge planks are sized for 1.5 m.
        public static List<DeckShipPiece> Build(DeckShipCargoKind kind, int seed, float hatch = HatchHeight)
        {
            var list = new List<DeckShipPiece>();
            var random = new System.Random(seed);
            switch (kind)
            {
                case DeckShipCargoKind.TowerAndForts: TowerAndForts(list, random, hatch); break;
                case DeckShipCargoKind.ContainerYard: ContainerYard(list, random, hatch); break;
                case DeckShipCargoKind.DominoRun: DominoRun(list, random, hatch); break;
                default: StepPyramid(list, random); break;
            }
            return list;
        }

        // A tall Jenga tower on the hatch, a gate fort fore and aft, and long planks bridging them to the tower.
        static void TowerAndForts(List<DeckShipPiece> list, System.Random random, float hatch)
        {
            // Layer 5 runs along the ship; its middle block is left out so the bridge planks slot in from both ends.
            Tower(list, new Vector3(0, hatch, 0), 11, 0, (layer, i) => layer == 5 && i == 0 || layer == 8 && i == 0 || layer == 2 && i == 1, "Center tower");
            Tower(list, new Vector3(0, 0, -11), 7, 1, null, "Aft fort");
            Tower(list, new Vector3(0, 0, 11), 7, 2, null, "Bow fort");
            // Each plank rests on a fort's top and reaches into the tower's open slot, then settles onto the slot floor.
            float fortTop = 6 * (Height + Gap) + Gap + Height;
            float y = fortTop + .25f + Gap;
            Add(list, new Vector3(0, y, -6.25f), new Vector3(10.5f, .5f, 1.4f), 90, 3, "Aft bridge plank");
            Add(list, new Vector3(0, y, 6.25f), new Vector3(10.5f, .5f, 1.4f), 90, 3, "Bow bridge plank");
            // Low cover walls along the rails: two blocks high, end to end.
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 2; k++)
                    Wall(list, new Vector3(side * 7.6f, 0, -6 + k * 12), 2, 2, (k + (side > 0 ? 1 : 0)) % 3, "Rail wall");
            Scatter(list, random, 10, "Loose block");
        }

        // Block-built shipping containers in two rows, stacked one to three high, with an aisle over the hatch.
        static void ContainerYard(List<DeckShipPiece> list, System.Random random, float hatch)
        {
            float[] rows = { -6.2f, 6.2f };
            float[] stations = { -11.5f, -6.8f, 6.8f, 11.5f };
            float tierHeight = 4 * (Height + Gap) + Gap;
            for (int r = 0; r < rows.Length; r++)
                for (int s = 0; s < stations.Length; s++)
                {
                    // The two middle stations stay one high: the aisle planks rest on their roofs.
                    int tiers = s == 1 || s == 2 ? 1 : 1 + random.Next(3);
                    for (int t = 0; t < tiers; t++) Container(list, new Vector3(rows[r], t * tierHeight, stations[s]), (r + s + t) % 3, "Container");
                }
            // One container sits on the hatch, open along the ship.
            Container(list, new Vector3(0, hatch, 0), 3, "Hatch container");
            // A plank across the aisle between the two middle stacks, at roof height of a single container.
            float roof = tierHeight;
            Add(list, new Vector3(0, roof + .3f + Gap, -6.8f), new Vector3(13, .6f, 1.4f), 0, 3, "Aisle plank");
            Add(list, new Vector3(0, roof + .3f + Gap, 6.8f), new Vector3(13, .6f, 1.4f), 0, 3, "Aisle plank");
            Scatter(list, random, 6, "Loose block");
        }

        // Tall standing slabs in snaking lines: the first big roll knocks them down like dominoes.
        static void DominoRun(List<DeckShipPiece> list, System.Random random, float hatch)
        {
            var slab = new Vector3(2.4f, 3.6f, .6f);
            // Two S-shaped lines down the port and starboard halves of the deck.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 14; i++)
                {
                    float z = FieldMinZ + 1 + i * 2.1f;
                    if (Mathf.Abs(z) < HatchHalf + 1) continue;
                    float x = side * (6 + 2.2f * Mathf.Sin(i * .7f + (side > 0 ? 1.5f : 0)));
                    float yaw = Mathf.Cos(i * .7f + (side > 0 ? 1.5f : 0)) * 25 * side;
                    Add(list, new Vector3(x, slab.y * .5f + Gap, z), slab, yaw, i % 3, "Domino");
                }
            }
            // Short cross rows on the hatch's fore and aft sides.
            for (int k = -1; k <= 1; k++)
            {
                Add(list, new Vector3(k * 1.4f, slab.y * .5f + Gap, -5.2f), slab, 90, 1, "Domino");
                Add(list, new Vector3(k * 1.4f, slab.y * .5f + Gap, 5.2f), slab, 90, 2, "Domino");
            }
            // A short Jenga tower on the hatch as high ground.
            Tower(list, new Vector3(0, hatch, 0), 6, 3, null, "Hatch tower");
            Scatter(list, random, 6, "Loose block");
        }

        // A stepped pyramid aft of the hatch and a hollow log crib forward of it.
        static void StepPyramid(List<DeckShipPiece> list, System.Random random)
        {
            // Four tiers, each two block layers thick; every tier is 3 m narrower than the one below.
            float baseY = 0;
            for (int tier = 0; tier < 4; tier++)
            {
                float side = 12 - tier * 3;
                for (int layer = 0; layer < 2; layer++)
                {
                    bool alongZ = layer % 2 == 1;
                    float w = side / 3;
                    for (int i = -1; i <= 1; i++)
                    {
                        var offset = alongZ ? new Vector3(i * w, 0, 0) : new Vector3(0, 0, i * w);
                        Add(list, new Vector3(0, baseY + Height * .5f + Gap, -9) + offset, new Vector3(side - Gap * 2, Height, w - Gap * 2), alongZ ? 90 : 0, tier % 3, "Pyramid tier " + (tier + 1));
                    }
                    baseY += Height + Gap;
                }
            }
            // Log crib: a hollow square of blocks, only the two edge blocks in every layer.
            for (int layer = 0; layer < 7; layer++)
            {
                bool alongX = layer % 2 == 0;
                for (int i = -1; i <= 1; i += 2)
                {
                    var offset = alongX ? new Vector3(0, 0, i * Width) : new Vector3(i * Width, 0, 0);
                    Add(list, new Vector3(0, layer * (Height + Gap) + Height * .5f + Gap, 9.5f) + offset, Block, alongX ? 0 : 90, 2, "Log crib");
                }
            }
            Tower(list, new Vector3(-6.2f, 0, 9.5f), 4, 0, null, "Bow stack");
            Tower(list, new Vector3(6.2f, 0, 9.5f), 4, 1, null, "Bow stack");
            Scatter(list, random, 8, "Loose block");
        }

        static Vector3 Block => new Vector3(Length - Gap, Height, Width - Gap);

        // A Jenga tower: three blocks per layer, every layer turned 90 degrees. skip(layer, -1|0|1) leaves a block out.
        static void Tower(List<DeckShipPiece> list, Vector3 baseCenter, int layers, int color, System.Func<int, int, bool> skip, string role)
        {
            for (int layer = 0; layer < layers; layer++)
            {
                bool alongX = layer % 2 == 0;
                for (int i = -1; i <= 1; i++)
                {
                    if (skip != null && skip(layer, i)) continue;
                    var offset = alongX ? new Vector3(0, 0, i * Width) : new Vector3(i * Width, 0, 0);
                    Add(list, baseCenter + new Vector3(0, layer * (Height + Gap) + Height * .5f + Gap, 0) + offset, Block, alongX ? 0 : 90, (color + layer) % 3, role);
                }
            }
        }

        // A straight wall of blocks laid end to end along the ship.
        static void Wall(List<DeckShipPiece> list, Vector3 baseCenter, int layers, int blocksLong, int color, string role)
        {
            for (int layer = 0; layer < layers; layer++)
                for (int b = 0; b < blocksLong; b++)
                {
                    float z = (b - (blocksLong - 1) * .5f) * Length + (layer % 2 == 1 ? Length * .5f : 0);
                    if (layer % 2 == 1 && b == blocksLong - 1) continue;
                    Add(list, baseCenter + new Vector3(0, layer * (Height + Gap) + Height * .5f + Gap, z), Block, 90, color, role);
                }
        }

        // A container: two walls three blocks high along the ship, roofed with three blocks across it.
        // The tunnel inside is 2.4 m wide and 2.7 m high.
        static void Container(List<DeckShipPiece> list, Vector3 baseCenter, int color, string role)
        {
            for (int layer = 0; layer < 3; layer++)
                for (int side = -1; side <= 1; side += 2)
                    Add(list, baseCenter + new Vector3(side * 1.95f, layer * (Height + Gap) + Height * .5f + Gap, 0), Block, 90, color, role);
            float roofY = 3 * (Height + Gap) + Height * .5f + Gap;
            for (int i = -1; i <= 1; i++)
                Add(list, baseCenter + new Vector3(0, roofY, i * Width), Block, 0, color, role + " roof");
        }

        // Single loose blocks and crates in open deck spots.
        static void Scatter(List<DeckShipPiece> list, System.Random random, int count, string role)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 30 && placed < count; attempt++)
            {
                bool crate = random.Next(3) == 0;
                var size = crate ? new Vector3(1.5f, 1.5f, 1.5f) : Block;
                float yaw = random.Next(4) * 45;
                // r covers the piece at any turn, so it never pokes past the edge of the cargo deck.
                float r = crate ? 1.1f : 2.4f;
                var p = new Vector3((float)(random.NextDouble() * 2 - 1) * (FieldHalfWidth - r), 0, Mathf.Lerp(FieldMinZ + r, FieldMaxZ - r, (float)random.NextDouble()));
                if (Mathf.Abs(p.x) < HatchHalf + r && Mathf.Abs(p.z) < HatchHalf + r) continue;
                bool clear = true;
                foreach (var other in list)
                {
                    float reach = Mathf.Max(other.size.x, other.size.z) * .5f + r;
                    if (other.position.y - other.size.y * .5f < 2 && Vector2.Distance(new Vector2(other.position.x, other.position.z), new Vector2(p.x, p.z)) < reach) { clear = false; break; }
                }
                if (!clear) continue;
                p.y = size.y * .5f + Gap;
                Add(list, p, size, yaw, random.Next(4), crate ? "Crate" : role);
                placed++;
            }
        }

        static void Add(List<DeckShipPiece> list, Vector3 position, Vector3 size, float yaw, int color, string role)
            => list.Add(new DeckShipPiece { position = position, size = size, yaw = yaw, color = color, role = role });
    }
}
