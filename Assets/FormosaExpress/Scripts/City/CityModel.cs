using System.Collections.Generic;
using UnityEngine;
using FormosaExpress.Core;

namespace FormosaExpress.City
{
    public enum SiteKind
    {
        FoodShop,     // valid order pickup
        Residence,    // valid order dropoff
        Office,       // valid order dropoff
        Landmark      // flavour only
    }

    public sealed class RoadNode
    {
        public int Index;
        public int Gx, Gz;
        public Vector3 Position;
        public readonly List<int> Edges = new List<int>(4);
    }

    public sealed class RoadEdge
    {
        public int Index;
        public int A, B;
        public Vector3 Dir;      // A -> B, normalised, flat
        public float Length;
        public bool IsAvenue;
        public bool IsAlley;

        public int Other(int node) => node == A ? B : A;
    }

    /// <summary>
    /// One drivable strip for traffic: either a straight lane between two intersections or a
    /// turn connector through one. Agents walk a polyline and hand off to a successor.
    /// </summary>
    public sealed class TrafficPath
    {
        public int Index;
        public Vector3[] Points;
        public float[] Cumulative;   // arc length at each point
        public float Length;
        public bool IsConnector;
        public int EdgeIndex = -1;
        public int FromNode = -1;
        public int ToNode = -1;
        public readonly List<int> Next = new List<int>(3);

        public void Finalise()
        {
            Cumulative = new float[Points.Length];
            float total = 0f;
            for (int i = 1; i < Points.Length; i++)
            {
                total += Vector3.Distance(Points[i - 1], Points[i]);
                Cumulative[i] = total;
            }

            Length = total;
        }

        /// <summary>Position at <paramref name="distance"/> along the path, with its tangent.</summary>
        public Vector3 Sample(float distance, out Vector3 tangent)
        {
            if (Points.Length < 2)
            {
                tangent = Vector3.forward;
                return Points.Length > 0 ? Points[0] : Vector3.zero;
            }

            distance = Mathf.Clamp(distance, 0f, Length);

            int i = 1;
            while (i < Cumulative.Length - 1 && Cumulative[i] < distance) i++;

            float segStart = Cumulative[i - 1];
            float segLen = Cumulative[i] - segStart;
            float t = segLen > 0.0001f ? (distance - segStart) / segLen : 0f;

            Vector3 a = Points[i - 1], b = Points[i];
            tangent = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : Vector3.forward;
            return Vector3.Lerp(a, b, t);
        }
    }

    /// <summary>A place in the city that can generate or receive an order.</summary>
    public sealed class Site
    {
        public int Index;
        public SiteKind Kind;
        public string Name;
        public Vector3 Position;     // standing point on the sidewalk
        public Vector3 Facing;       // outward, towards the street
        public Vector3 DoorPosition; // for the beacon
        public int NearestNode;
        public Color Tint = Color.white;
        public int BlockIndex;
    }

    public sealed class CityBlock
    {
        public int Index;
        public Vector3 Centre;
        public Vector2 Size;
        public readonly List<int> Sites = new List<int>(16);
        public readonly List<Vector3> SidewalkLoop = new List<Vector3>(16);
        public Transform Root;
    }

    /// <summary>
    /// The generated city: a road graph for routing, a lane graph for traffic, sidewalk loops
    /// for pedestrians and a list of delivery sites. Purely data — no GameObjects.
    /// </summary>
    public sealed class CityModel
    {
        public readonly List<RoadNode> Nodes = new List<RoadNode>();
        public readonly List<RoadEdge> Edges = new List<RoadEdge>();
        public readonly List<TrafficPath> Paths = new List<TrafficPath>();
        public readonly List<Site> Sites = new List<Site>();
        public readonly List<CityBlock> Blocks = new List<CityBlock>();

        public int Seed;
        public Bounds WorldBounds;
        public Transform Root;

        int[,] _nodeGrid;   // [gx, gz] -> node index, or -1

        public void SetNodeGrid(int[,] grid) => _nodeGrid = grid;

        public int NodeAt(int gx, int gz)
        {
            if (_nodeGrid == null) return -1;
            if (gx < 0 || gz < 0 || gx >= _nodeGrid.GetLength(0) || gz >= _nodeGrid.GetLength(1)) return -1;
            return _nodeGrid[gx, gz];
        }

        // ---------------------------------------------------------------- lookups

        public int NearestNode(Vector3 position)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Nodes.Count; i++)
            {
                float d = (Nodes[i].Position - position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = i; }
            }

            return best;
        }

        /// <summary>The point on the nearest road centreline, used to snap the player back on track.</summary>
        public Vector3 NearestRoadPoint(Vector3 position)
        {
            Vector3 best = position;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Edges.Count; i++)
            {
                RoadEdge e = Edges[i];
                Vector3 p = MathX.ClosestPointOnSegment(Nodes[e.A].Position, Nodes[e.B].Position, position, out _);
                float d = (p - position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = p; }
            }

            return best;
        }

        // ---------------------------------------------------------------- routing (A*)

        readonly List<int> _openHeap = new List<int>(128);
        float[] _gScore, _fScore;
        int[] _cameFrom;
        bool[] _closed;

        void EnsureRoutingBuffers()
        {
            int n = Nodes.Count;
            if (_gScore == null || _gScore.Length < n)
            {
                _gScore = new float[n];
                _fScore = new float[n];
                _cameFrom = new int[n];
                _closed = new bool[n];
            }
        }

        /// <summary>A* over intersections. Returns node indices from start to goal, or null.</summary>
        public List<int> FindNodePath(int start, int goal)
        {
            if (start < 0 || goal < 0 || start >= Nodes.Count || goal >= Nodes.Count) return null;
            EnsureRoutingBuffers();

            int n = Nodes.Count;
            for (int i = 0; i < n; i++)
            {
                _gScore[i] = float.MaxValue;
                _fScore[i] = float.MaxValue;
                _cameFrom[i] = -1;
                _closed[i] = false;
            }

            _openHeap.Clear();
            _gScore[start] = 0f;
            _fScore[start] = Heuristic(start, goal);
            _openHeap.Add(start);

            while (_openHeap.Count > 0)
            {
                // Small graph (a few hundred nodes): a linear scan beats heap bookkeeping.
                int bestIdx = 0;
                for (int i = 1; i < _openHeap.Count; i++)
                    if (_fScore[_openHeap[i]] < _fScore[_openHeap[bestIdx]]) bestIdx = i;

                int current = _openHeap[bestIdx];
                _openHeap.RemoveAt(bestIdx);

                if (current == goal) return Reconstruct(current);

                _closed[current] = true;

                foreach (int edgeIndex in Nodes[current].Edges)
                {
                    RoadEdge e = Edges[edgeIndex];
                    int next = e.Other(current);
                    if (_closed[next]) continue;

                    // Alleys are shortcuts: cheap in distance but slightly penalised so the
                    // suggested route prefers real streets unless the alley genuinely wins.
                    float cost = e.Length * (e.IsAlley ? 1.25f : 1f);

                    // A real turn costs a few extra "metres" too, so a route that is a wash on
                    // distance still breaks toward the straighter option instead of zig-zagging
                    // between parallel streets. `current`'s incoming edge is already fixed once
                    // it has been popped from the open set, so this stays stable per expansion.
                    int from = _cameFrom[current];
                    if (from != -1)
                    {
                        Vector3 dirIn = Nodes[current].Position - Nodes[from].Position;
                        Vector3 dirOut = Nodes[next].Position - Nodes[current].Position;
                        dirIn.y = 0f;
                        dirOut.y = 0f;

                        if (dirIn.sqrMagnitude > 0.01f && dirOut.sqrMagnitude > 0.01f)
                            cost += Vector3.Angle(dirIn, dirOut) * Tuning.RouteTurnPenaltyPerDegree;
                    }

                    float tentative = _gScore[current] + cost;
                    if (tentative >= _gScore[next]) continue;

                    _cameFrom[next] = current;
                    _gScore[next] = tentative;
                    _fScore[next] = tentative + Heuristic(next, goal);
                    if (!_openHeap.Contains(next)) _openHeap.Add(next);
                }
            }

            return null;
        }

        float Heuristic(int a, int b) => Vector3.Distance(Nodes[a].Position, Nodes[b].Position);

        List<int> Reconstruct(int current)
        {
            var path = new List<int>(32) { current };
            while (_cameFrom[current] != -1)
            {
                current = _cameFrom[current];
                path.Add(current);
            }

            path.Reverse();
            return path;
        }

        /// <summary>
        /// Builds a drivable polyline from <paramref name="from"/> to <paramref name="to"/>,
        /// snapping both ends to the road graph. Also reports the total length.
        /// </summary>
        public bool BuildRoute(Vector3 from, Vector3 to, List<Vector3> output, out float length)
        {
            output.Clear();
            length = 0f;

            int a = NearestNode(from);
            int b = NearestNode(to);
            if (a < 0 || b < 0) return false;

            List<int> nodePath = FindNodePath(a, b);
            if (nodePath == null) return false;

            output.Add(from);
            foreach (int nodeIndex in nodePath) output.Add(Nodes[nodeIndex].Position);
            output.Add(to);

            // Drop the first/last graph node when the direct hop is clearly shorter, which
            // stops the route line from doubling back at the start and finish.
            if (output.Count > 3 && Vector3.Distance(output[0], output[2]) < Vector3.Distance(output[0], output[1]))
                output.RemoveAt(1);
            if (output.Count > 3 && Vector3.Distance(output[output.Count - 1], output[output.Count - 3])
                < Vector3.Distance(output[output.Count - 1], output[output.Count - 2]))
                output.RemoveAt(output.Count - 2);

            // Nudge the interior waypoints into the correct lane. The graph runs down the middle
            // of each street, and a route drawn there sits on the centre line.
            for (int i = 1; i < output.Count - 1; i++)
            {
                Vector3 dir = output[i + 1] - output[i - 1];
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) continue;

                Vector3 right = Vector3.Cross(Vector3.up, dir.normalized);
                output[i] += right * Tuning.LaneOffset;
            }

            for (int i = 1; i < output.Count; i++) length += Vector3.Distance(output[i - 1], output[i]);
            return true;
        }

        /// <summary>Cheap route length estimate without allocating a polyline.</summary>
        public float EstimateRouteDistance(Vector3 from, Vector3 to)
        {
            int a = NearestNode(from);
            int b = NearestNode(to);
            if (a < 0 || b < 0) return Vector3.Distance(from, to);

            List<int> path = FindNodePath(a, b);
            if (path == null) return Vector3.Distance(from, to);

            float len = Vector3.Distance(from, Nodes[path[0]].Position);
            for (int i = 1; i < path.Count; i++)
                len += Vector3.Distance(Nodes[path[i - 1]].Position, Nodes[path[i]].Position);
            len += Vector3.Distance(Nodes[path[path.Count - 1]].Position, to);
            return len;
        }

        // ---------------------------------------------------------------- site selection

        public Site PickSite(ref Rng rng, SiteKind kind, Vector3 awayFrom, float minDistance)
        {
            Site fallback = null;
            for (int attempt = 0; attempt < 48; attempt++)
            {
                Site s = Sites[rng.Range(0, Sites.Count)];
                if (s.Kind != kind) continue;
                fallback ??= s;
                if ((s.Position - awayFrom).sqrMagnitude >= minDistance * minDistance) return s;
            }

            return fallback ?? Sites[0];
        }

        public TrafficPath RandomStraightPath(ref Rng rng)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                TrafficPath p = Paths[rng.Range(0, Paths.Count)];
                if (!p.IsConnector && p.Length > 12f) return p;
            }

            return Paths[0];
        }
    }
}
