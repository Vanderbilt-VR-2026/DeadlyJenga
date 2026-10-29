using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    // Same three-metre danger radius, indexed horizontally so distant debris is never scored.
    public sealed class FallingBlockThreats
    {
        const float CellSize = 3;
        readonly Dictionary<Vector2Int, List<Vector3>> cells = new Dictionary<Vector2Int, List<Vector3>>();
        readonly Stack<List<Vector3>> spare = new Stack<List<Vector3>>();
        static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
        public void Clear()
        {
            foreach (var list in cells.Values) { list.Clear(); spare.Push(list); }
            cells.Clear();
        }
        public void Add(Vector3 predicted)
        {
            var key = Cell(predicted);
            if (!cells.TryGetValue(key, out var list))
                cells[key] = list = spare.Count > 0 ? spare.Pop() : new List<Vector3>();
            list.Add(predicted);
        }
        public float Cost(Vector3 point)
        {
            float cost = 0; var cell = Cell(point);
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
            {
                if (!cells.TryGetValue(cell + new Vector2Int(x, z), out var list)) continue;
                foreach (var predicted in list)
                {
                    if (predicted.y < point.y - .5f || predicted.y > point.y + 8) continue;
                    float dx = predicted.x - point.x, dz = predicted.z - point.z, d2 = dx * dx + dz * dz;
                    if (d2 < 9) cost += (3 - Mathf.Sqrt(d2)) * .8f;
                }
            }
            return cost;
        }
    }
}
