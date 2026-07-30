using System.Collections.Generic;
using UnityEngine;
using FormosaExpress.Core;

namespace FormosaExpress.City
{
    /// <summary>A street-facing plot of land that one shophouse is built on.</summary>
    public struct BuildingLot
    {
        public Vector3 FrontCentre;   // middle of the street-facing edge, at pavement height
        public Vector3 Forward;       // outward, towards the road
        public Vector3 Right;         // along the street
        public float Width;
        public float Depth;
        public int BlockIndex;
        public int SiteIndex;
    }

    /// <summary>An alley cutting through the middle of a block: a player-only shortcut.</summary>
    public struct Alley
    {
        public Vector3 From;
        public Vector3 To;
        public bool Vertical;
        public int BlockIndex;
    }

    /// <summary>
    /// Generates the city layout: road graph, blocks, building lots, delivery sites, traffic
    /// lanes and sidewalk loops. Deterministic for a given seed.
    /// </summary>
    public sealed class CityBuilder
    {
        public readonly List<BuildingLot> Lots = new List<BuildingLot>(1024);

        /// <summary>Buildings on the outside of the boundary roads. They wall the play area in.</summary>
        public readonly List<BuildingLot> PerimeterLots = new List<BuildingLot>(256);
        public readonly List<Alley> Alleys = new List<Alley>(32);

        CityModel _model;
        Rng _rng;

        const float LotDepth = 13.5f;
        const float LotMinWidth = 6.5f;
        const float LotMaxWidth = 11.5f;

        public CityModel Build(int seed)
        {
            _model = new CityModel { Seed = seed };
            _rng = new Rng(seed * 7919 + 13);
            Lots.Clear();
            PerimeterLots.Clear();
            Alleys.Clear();

            BuildNodes();
            BuildEdges();
            BuildBlocks();
            BuildAlleys();
            BuildLotsAndSites();
            BuildPerimeter();
            BuildTrafficPaths();

            foreach (Site site in _model.Sites) site.NearestNode = _model.NearestNode(site.Position);

            float halfX = Tuning.WorldSizeX * 0.5f + Tuning.CellSize;
            float halfZ = Tuning.WorldSizeZ * 0.5f + Tuning.CellSize;
            _model.WorldBounds = new Bounds(Vector3.zero, new Vector3(halfX * 2f, 60f, halfZ * 2f));

            return _model;
        }

        // ---------------------------------------------------------------- graph

        void BuildNodes()
        {
            var grid = new int[Tuning.GridX, Tuning.GridZ];
            float ox = -Tuning.WorldSizeX * 0.5f;
            float oz = -Tuning.WorldSizeZ * 0.5f;

            for (int gx = 0; gx < Tuning.GridX; gx++)
            for (int gz = 0; gz < Tuning.GridZ; gz++)
            {
                var node = new RoadNode
                {
                    Index = _model.Nodes.Count,
                    Gx = gx,
                    Gz = gz,
                    Position = new Vector3(ox + gx * Tuning.CellSize, 0f, oz + gz * Tuning.CellSize)
                };
                grid[gx, gz] = node.Index;
                _model.Nodes.Add(node);
            }

            _model.SetNodeGrid(grid);
        }

        void BuildEdges()
        {
            for (int gx = 0; gx < Tuning.GridX; gx++)
            for (int gz = 0; gz < Tuning.GridZ; gz++)
            {
                int a = _model.NodeAt(gx, gz);
                int east = _model.NodeAt(gx + 1, gz);
                int north = _model.NodeAt(gx, gz + 1);

                if (east >= 0) AddEdge(a, east, gz % 3 == 0, false);
                if (north >= 0) AddEdge(a, north, gx % 3 == 0, false);
            }
        }

        RoadEdge AddEdge(int a, int b, bool avenue, bool alley)
        {
            Vector3 pa = _model.Nodes[a].Position;
            Vector3 pb = _model.Nodes[b].Position;
            Vector3 delta = pb - pa;

            var edge = new RoadEdge
            {
                Index = _model.Edges.Count,
                A = a,
                B = b,
                Length = delta.magnitude,
                Dir = delta.normalized,
                IsAvenue = avenue,
                IsAlley = alley
            };

            _model.Edges.Add(edge);
            _model.Nodes[a].Edges.Add(edge.Index);
            _model.Nodes[b].Edges.Add(edge.Index);
            return edge;
        }

        // ---------------------------------------------------------------- blocks

        void BuildBlocks()
        {
            for (int bx = 0; bx < Tuning.GridX - 1; bx++)
            for (int bz = 0; bz < Tuning.GridZ - 1; bz++)
            {
                Vector3 sw = _model.Nodes[_model.NodeAt(bx, bz)].Position;
                Vector3 ne = _model.Nodes[_model.NodeAt(bx + 1, bz + 1)].Position;

                var min = new Vector3(sw.x + Tuning.RoadHalfWidth, 0f, sw.z + Tuning.RoadHalfWidth);
                var max = new Vector3(ne.x - Tuning.RoadHalfWidth, 0f, ne.z - Tuning.RoadHalfWidth);

                var block = new CityBlock
                {
                    Index = _model.Blocks.Count,
                    Centre = (min + max) * 0.5f,
                    Size = new Vector2(max.x - min.x, max.z - min.z)
                };

                // Sidewalk centreline loop, half a sidewalk in from the block edge.
                float inset = Tuning.SidewalkWidth * 0.5f;
                float y = Tuning.CurbHeight + 0.02f;
                block.SidewalkLoop.Add(new Vector3(min.x + inset, y, min.z + inset));
                block.SidewalkLoop.Add(new Vector3(max.x - inset, y, min.z + inset));
                block.SidewalkLoop.Add(new Vector3(max.x - inset, y, max.z - inset));
                block.SidewalkLoop.Add(new Vector3(min.x + inset, y, max.z - inset));

                _model.Blocks.Add(block);
            }
        }

        Bounds BlockInner(CityBlock block)
        {
            return new Bounds(block.Centre, new Vector3(block.Size.x, 1f, block.Size.y));
        }

        // ---------------------------------------------------------------- alleys

        readonly Dictionary<int, int> _edgeMidNode = new Dictionary<int, int>();

        void BuildAlleys()
        {
            _edgeMidNode.Clear();

            for (int i = 0; i < _model.Blocks.Count; i++)
            {
                if (!_rng.Chance(0.30f)) continue;

                CityBlock block = _model.Blocks[i];
                int bx = i / (Tuning.GridZ - 1);
                int bz = i % (Tuning.GridZ - 1);
                bool vertical = _rng.Chance(0.5f);

                int edgeA, edgeB;
                if (vertical)
                {
                    edgeA = FindEdge(_model.NodeAt(bx, bz), _model.NodeAt(bx + 1, bz));           // south
                    edgeB = FindEdge(_model.NodeAt(bx, bz + 1), _model.NodeAt(bx + 1, bz + 1));   // north
                }
                else
                {
                    edgeA = FindEdge(_model.NodeAt(bx, bz), _model.NodeAt(bx, bz + 1));           // west
                    edgeB = FindEdge(_model.NodeAt(bx + 1, bz), _model.NodeAt(bx + 1, bz + 1));   // east
                }

                if (edgeA < 0 || edgeB < 0) continue;

                int midA = GetOrCreateMidNode(edgeA, block.Centre);
                int midB = GetOrCreateMidNode(edgeB, block.Centre);
                if (midA < 0 || midB < 0 || midA == midB) continue;

                AddEdge(midA, midB, false, true);

                Alleys.Add(new Alley
                {
                    From = _model.Nodes[midA].Position,
                    To = _model.Nodes[midB].Position,
                    Vertical = vertical,
                    BlockIndex = i
                });
            }
        }

        int FindEdge(int a, int b)
        {
            if (a < 0 || b < 0) return -1;
            foreach (int e in _model.Nodes[a].Edges)
                if (_model.Edges[e].Other(a) == b) return e;
            return -1;
        }

        /// <summary>
        /// Splits a road edge in half, inserting a node so the alley can join the graph.
        /// Idempotent: two neighbouring blocks alleying onto the same street share the node.
        /// </summary>
        int GetOrCreateMidNode(int edgeIndex, Vector3 towards)
        {
            if (_edgeMidNode.TryGetValue(edgeIndex, out int existing)) return existing;

            RoadEdge edge = _model.Edges[edgeIndex];
            if (edge.IsAlley) return -1;

            int originalB = edge.B;
            Vector3 pa = _model.Nodes[edge.A].Position;
            Vector3 pb = _model.Nodes[originalB].Position;
            Vector3 mid = (pa + pb) * 0.5f;

            var node = new RoadNode
            {
                Index = _model.Nodes.Count,
                Gx = -1,
                Gz = -1,
                Position = mid
            };
            _model.Nodes.Add(node);

            // Rewire: A-B becomes A-M plus M-B. Reusing the original edge slot for A-M keeps
            // every existing edge index (and therefore every traffic lane) valid.
            _model.Nodes[originalB].Edges.Remove(edgeIndex);
            edge.B = node.Index;
            edge.Length = Vector3.Distance(pa, mid);
            node.Edges.Add(edgeIndex);

            AddEdge(node.Index, originalB, edge.IsAvenue, false);

            _edgeMidNode[edgeIndex] = node.Index;
            return node.Index;
        }

        // ---------------------------------------------------------------- lots and sites

        void BuildLotsAndSites()
        {
            for (int i = 0; i < _model.Blocks.Count; i++)
            {
                CityBlock block = _model.Blocks[i];
                Bounds inner = BlockInner(block);

                float bMinX = inner.min.x + Tuning.SidewalkWidth;
                float bMaxX = inner.max.x - Tuning.SidewalkWidth;
                float bMinZ = inner.min.z + Tuning.SidewalkWidth;
                float bMaxZ = inner.max.z - Tuning.SidewalkWidth;

                // Which pair of sides gets the full-length rows varies per block for variety.
                bool xMajor = ((i * 31) % 7) < 4;

                float xStart = xMajor ? bMinX : bMinX + LotDepth;
                float xEnd = xMajor ? bMaxX : bMaxX - LotDepth;
                float zStart = xMajor ? bMinZ + LotDepth : bMinZ;
                float zEnd = xMajor ? bMaxZ - LotDepth : bMaxZ;

                // South row (faces -Z), north row (faces +Z)
                AddLotRow(Lots, i, new Vector3(xStart, 0f, bMinZ), new Vector3(xEnd, 0f, bMinZ),
                    new Vector3(0f, 0f, -1f), Vector3.right);
                AddLotRow(Lots, i, new Vector3(xEnd, 0f, bMaxZ), new Vector3(xStart, 0f, bMaxZ),
                    new Vector3(0f, 0f, 1f), Vector3.left);

                // West row (faces -X), east row (faces +X)
                AddLotRow(Lots, i, new Vector3(bMinX, 0f, zEnd), new Vector3(bMinX, 0f, zStart),
                    new Vector3(-1f, 0f, 0f), Vector3.back);
                AddLotRow(Lots, i, new Vector3(bMaxX, 0f, zStart), new Vector3(bMaxX, 0f, zEnd),
                    new Vector3(1f, 0f, 0f), Vector3.forward);
            }
        }

        /// <summary>
        /// Lines the outside of the boundary roads with shophouses facing inwards. Without this
        /// the outermost street has open ground on one side and the rider can leave the city.
        /// </summary>
        void BuildPerimeter()
        {
            float halfX = Tuning.WorldSizeX * 0.5f;
            float halfZ = Tuning.WorldSizeZ * 0.5f;
            float out2 = Tuning.RoadHalfWidth + Tuning.SidewalkWidth;

            float minX = -halfX - out2;
            float maxX = halfX + out2;
            float minZ = -halfZ - out2;
            float maxZ = halfZ + out2;

            // South side: buildings sit below the road and face +Z.
            AddLotRow(PerimeterLots, -1, new Vector3(maxX, 0f, minZ), new Vector3(minX, 0f, minZ),
                new Vector3(0f, 0f, 1f), Vector3.left);

            // North side.
            AddLotRow(PerimeterLots, -1, new Vector3(minX, 0f, maxZ), new Vector3(maxX, 0f, maxZ),
                new Vector3(0f, 0f, -1f), Vector3.right);

            // West side.
            AddLotRow(PerimeterLots, -1, new Vector3(minX, 0f, minZ + Tuning.CellSize * 0.1f),
                new Vector3(minX, 0f, maxZ - Tuning.CellSize * 0.1f),
                new Vector3(1f, 0f, 0f), Vector3.forward);

            // East side.
            AddLotRow(PerimeterLots, -1, new Vector3(maxX, 0f, maxZ - Tuning.CellSize * 0.1f),
                new Vector3(maxX, 0f, minZ + Tuning.CellSize * 0.1f),
                new Vector3(-1f, 0f, 0f), Vector3.back);
        }

        /// <summary>
        /// Fills the run from <paramref name="from"/> to <paramref name="to"/> with shophouse
        /// lots, leaving a gap wherever an alley opens onto this side of the block.
        /// </summary>
        void AddLotRow(List<BuildingLot> target, int blockIndex, Vector3 from, Vector3 to,
            Vector3 outward, Vector3 along)
        {
            float runLength = Vector3.Distance(from, to);
            if (runLength < LotMinWidth) return;

            float cursor = 0f;
            int guard = 0;

            while (cursor < runLength - LotMinWidth * 0.6f && guard++ < 120)
            {
                float width = Mathf.Min(_rng.Range(LotMinWidth, LotMaxWidth), runLength - cursor);
                Vector3 centre = from + along * (cursor + width * 0.5f);

                if (blockIndex >= 0 && IntersectsAlley(blockIndex, centre, width * 0.5f + 1.2f))
                {
                    cursor += 3.0f;
                    continue;
                }

                var lot = new BuildingLot
                {
                    FrontCentre = new Vector3(centre.x, Tuning.CurbHeight, centre.z),
                    Forward = outward,
                    Right = along,
                    Width = width - 0.35f,   // a hairline gap between neighbours reads as separate buildings
                    Depth = LotDepth,
                    BlockIndex = blockIndex,
                    SiteIndex = -1
                };

                lot.SiteIndex = CreateSite(lot);
                target.Add(lot);
                if (blockIndex >= 0) _model.Blocks[blockIndex].Sites.Add(lot.SiteIndex);

                cursor += width;
            }
        }

        bool IntersectsAlley(int blockIndex, Vector3 point, float radius)
        {
            for (int i = 0; i < Alleys.Count; i++)
            {
                Alley a = Alleys[i];
                if (a.BlockIndex != blockIndex) continue;
                Vector3 p = MathX.ClosestPointOnSegment(a.From, a.To, point, out _);
                if ((p - point).sqrMagnitude < (radius + Tuning.AlleyHalfWidth) * (radius + Tuning.AlleyHalfWidth))
                    return true;
            }

            return false;
        }

        int CreateSite(BuildingLot lot)
        {
            float roll = _rng.Value;
            SiteKind kind;
            string name;

            if (roll < 0.42f)
            {
                kind = SiteKind.FoodShop;
                name = _rng.Pick(CityNames.FoodShops);
            }
            else if (roll < 0.78f)
            {
                kind = SiteKind.Residence;
                name = _rng.Pick(CityNames.Residences);
            }
            else if (roll < 0.94f)
            {
                kind = SiteKind.Office;
                name = _rng.Pick(CityNames.Offices);
            }
            else
            {
                kind = SiteKind.Landmark;
                name = _rng.Pick(CityNames.Landmarks);
            }

            Vector3 door = lot.FrontCentre;

            // The pickup/drop-off point sits out in the near lane rather than on the pavement.
            // A zone tucked against the shopfront is almost impossible to hit from the road, and
            // putting the beacon in the street is also what makes it readable while riding.
            Vector3 stand = door + lot.Forward * (Tuning.SidewalkWidth + 2.0f);

            var site = new Site
            {
                Index = _model.Sites.Count,
                Kind = kind,
                Name = name,
                DoorPosition = door,
                Position = new Vector3(stand.x, 0f, stand.z),
                Facing = lot.Forward,
                BlockIndex = lot.BlockIndex,
                Tint = _rng.Pick(Art.NeonColours)
            };

            site.NearestNode = -1;   // filled in after all sites exist
            _model.Sites.Add(site);
            return site.Index;
        }

        // ---------------------------------------------------------------- traffic lanes

        void BuildTrafficPaths()
        {
            // Two directed lanes per drivable edge. Alleys stay traffic-free on purpose:
            // they are the player's shortcut.
            var laneForward = new int[_model.Edges.Count];
            var laneBackward = new int[_model.Edges.Count];
            for (int i = 0; i < _model.Edges.Count; i++) { laneForward[i] = -1; laneBackward[i] = -1; }

            for (int i = 0; i < _model.Edges.Count; i++)
            {
                RoadEdge edge = _model.Edges[i];
                if (edge.IsAlley) continue;
                if (edge.Length < Tuning.RoadHalfWidth * 2f + 4f) continue;

                laneForward[i] = AddLane(edge, true);
                laneBackward[i] = AddLane(edge, false);
            }

            // Link each incoming lane to every legal outgoing lane through the intersection.
            for (int n = 0; n < _model.Nodes.Count; n++)
            {
                RoadNode node = _model.Nodes[n];

                for (int ei = 0; ei < node.Edges.Count; ei++)
                {
                    int inEdgeIndex = node.Edges[ei];
                    RoadEdge inEdge = _model.Edges[inEdgeIndex];
                    if (inEdge.IsAlley) continue;

                    // The lane arriving at this node.
                    int incoming = inEdge.B == n ? laneForward[inEdgeIndex] : laneBackward[inEdgeIndex];
                    if (incoming < 0) continue;

                    bool deadEnd = CountDrivableEdges(node) <= 1;

                    for (int ej = 0; ej < node.Edges.Count; ej++)
                    {
                        int outEdgeIndex = node.Edges[ej];
                        if (!deadEnd && outEdgeIndex == inEdgeIndex) continue;
                        RoadEdge outEdge = _model.Edges[outEdgeIndex];
                        if (outEdge.IsAlley) continue;

                        // The lane leaving this node.
                        int outgoing = outEdge.A == n ? laneForward[outEdgeIndex] : laneBackward[outEdgeIndex];
                        if (outgoing < 0 || outgoing == incoming) continue;

                        int connector = AddConnector(_model.Paths[incoming], _model.Paths[outgoing], n);
                        _model.Paths[incoming].Next.Add(connector);
                    }
                }
            }

            // Drop any lane that leads nowhere so agents never dead-stop mid-street.
            foreach (TrafficPath p in _model.Paths)
                if (!p.IsConnector && p.Next.Count == 0)
                    p.Next.Add(p.Index);   // loop back on itself; the agent will be recycled
        }

        int CountDrivableEdges(RoadNode node)
        {
            int count = 0;
            foreach (int e in node.Edges)
                if (!_model.Edges[e].IsAlley) count++;
            return count;
        }

        int AddLane(RoadEdge edge, bool forward)
        {
            Vector3 dir = forward ? edge.Dir : -edge.Dir;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 startNode = forward ? _model.Nodes[edge.A].Position : _model.Nodes[edge.B].Position;
            Vector3 endNode = forward ? _model.Nodes[edge.B].Position : _model.Nodes[edge.A].Position;

            float inset = Tuning.RoadHalfWidth;
            Vector3 offset = right * Tuning.LaneOffset;
            Vector3 start = startNode + dir * inset + offset;
            Vector3 end = endNode - dir * inset + offset;

            var path = new TrafficPath
            {
                Index = _model.Paths.Count,
                Points = new[] { start, end },
                EdgeIndex = edge.Index,
                FromNode = forward ? edge.A : edge.B,
                ToNode = forward ? edge.B : edge.A,
                IsConnector = false
            };
            path.Finalise();
            _model.Paths.Add(path);
            return path.Index;
        }

        int AddConnector(TrafficPath from, TrafficPath to, int throughNode)
        {
            Vector3 a = from.Points[from.Points.Length - 1];
            Vector3 b = to.Points[0];
            Vector3 aDir = (a - from.Points[from.Points.Length - 2]).normalized;
            Vector3 bDir = (to.Points[1] - b).normalized;

            Vector3 control;
            if (!LineIntersectXZ(a, aDir, b, bDir, out control))
                control = (a + b) * 0.5f;

            // Keep the control point sane for U-turns and near-collinear cases.
            if ((control - a).magnitude > Tuning.CellSize * 0.6f || (control - b).magnitude > Tuning.CellSize * 0.6f)
                control = (a + b) * 0.5f + (aDir + bDir).normalized * 2f;

            const int steps = 6;
            var points = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                points[i] = Bezier(a, control, b, t);
            }

            var path = new TrafficPath
            {
                Index = _model.Paths.Count,
                Points = points,
                IsConnector = true,
                FromNode = throughNode,
                ToNode = to.ToNode
            };
            path.Finalise();
            path.Next.Add(to.Index);
            _model.Paths.Add(path);
            return path.Index;
        }

        static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        static bool LineIntersectXZ(Vector3 p1, Vector3 d1, Vector3 p2, Vector3 d2, out Vector3 hit)
        {
            float cross = d1.x * d2.z - d1.z * d2.x;
            if (Mathf.Abs(cross) < 0.001f)
            {
                hit = Vector3.zero;
                return false;
            }

            float dx = p2.x - p1.x;
            float dz = p2.z - p1.z;
            float t = (dx * d2.z - dz * d2.x) / cross;
            hit = p1 + d1 * t;
            hit.y = 0f;
            return true;
        }
    }
}
