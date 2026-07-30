using System.Collections.Generic;
using UnityEngine;
using FormosaExpress.Core;

namespace FormosaExpress.City
{
    /// <summary>
    /// Builds the drivable surface: asphalt, intersections, road markings, crosswalks,
    /// sidewalks with curbs, and the alley shortcuts. Everything lands in a small number of
    /// spatial chunks so frustum culling still has something to work with.
    /// </summary>
    public sealed class GroundFactory
    {
        const float RoadY = 0.0f;
        const float PaintY = 0.012f;
        const float ChunkCells = 2f;

        readonly MaterialLibrary _mats;
        readonly Palette _pal;

        int _asphalt, _asphaltWorn, _paintWhite, _paintYellow;
        int _sidewalkTop, _sidewalkSide, _curbEdge, _alleyFloor, _drain, _basePlate;

        readonly Dictionary<Vector2Int, MeshBuilder> _chunks = new Dictionary<Vector2Int, MeshBuilder>();

        public GroundFactory(MaterialLibrary mats)
        {
            _mats = mats;
            _pal = mats.Palette;

            _asphalt = _pal.Add(Art.Asphalt);
            _asphaltWorn = _pal.Add(Art.AsphaltWorn);
            _paintWhite = _pal.Add(Art.RoadPaint);
            _paintYellow = _pal.Add(Art.RoadPaintYellow);
            _sidewalkTop = _pal.Add(Art.Sidewalk);
            _sidewalkSide = _pal.AddShaded(Art.Sidewalk, 0.62f);
            _curbEdge = _pal.Add(Art.SidewalkEdge);
            _alleyFloor = _pal.Add(new Color(0.30f, 0.29f, 0.30f));
            _drain = _pal.Add(new Color(0.11f, 0.11f, 0.12f));
            _basePlate = _pal.Add(new Color(0.09f, 0.09f, 0.10f));
        }

        public void Build(CityModel model, CityBuilder layout, Transform parent)
        {
            _chunks.Clear();

            BuildBasePlate(model, parent);

            foreach (RoadNode node in model.Nodes) BuildIntersection(model, node);
            foreach (RoadEdge edge in model.Edges)
            {
                if (edge.IsAlley) BuildAlleySurface(model, edge);
                else BuildRoadSegment(model, edge);
            }

            foreach (CityBlock block in model.Blocks) BuildSidewalk(model, layout, block);
            BuildPerimeterPavement(model);

            var root = new GameObject("Ground");
            root.transform.SetParent(parent, false);

            foreach (KeyValuePair<Vector2Int, MeshBuilder> kv in _chunks)
            {
                GameObject go = kv.Value.Flush($"GroundChunk_{kv.Key.x}_{kv.Key.y}", root.transform, _mats.Ground);
                if (go != null) go.layer = LayerMask.NameToLayer(Tuning.LayerGround);
            }

            BuildColliders(model, layout, root.transform);
        }

        // ---------------------------------------------------------------- chunking

        MeshBuilder Chunk(Vector3 worldPosition)
        {
            var key = new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / (Tuning.CellSize * ChunkCells)),
                Mathf.FloorToInt(worldPosition.z / (Tuning.CellSize * ChunkCells)));

            if (!_chunks.TryGetValue(key, out MeshBuilder mb))
            {
                mb = new MeshBuilder(_pal);
                _chunks[key] = mb;
            }

            return mb;
        }

        // ---------------------------------------------------------------- surfaces

        void BuildBasePlate(CityModel model, Transform parent)
        {
            // A single dark plane under everything so no gap ever shows the sky through.
            var mb = new MeshBuilder(_pal);
            Vector3 size = model.WorldBounds.size;
            // Large enough to sit under the distant skyline rings as well as the play area.
            mb.AddFloorRect(new Vector3(0f, -0.06f, 0f), size.x + 2600f, size.z + 2600f, _basePlate);
            GameObject go = mb.Flush("BasePlate", parent, _mats.Ground, false);
            if (go != null) go.layer = LayerMask.NameToLayer(Tuning.LayerGround);
        }

        void BuildIntersection(CityModel model, RoadNode node)
        {
            MeshBuilder mb = Chunk(node.Position);
            float r = Tuning.RoadHalfWidth;
            Vector3 c = node.Position + Vector3.up * RoadY;

            mb.AddFloorRect(c, r * 2f, r * 2f, _asphalt);

            // A subtly different patch breaks up the flatness of a pure grid.
            if ((node.Gx + node.Gz) % 3 == 0)
                mb.AddFloorRect(c + new Vector3(0f, PaintY * 0.4f, 0f), r * 1.1f, r * 1.1f, _asphaltWorn);

            // Stop lines and crosswalks on the approach to every full intersection.
            int drivable = 0;
            foreach (int e in node.Edges) if (!model.Edges[e].IsAlley) drivable++;
            if (drivable < 3) return;

            foreach (int edgeIndex in node.Edges)
            {
                RoadEdge edge = model.Edges[edgeIndex];
                if (edge.IsAlley) continue;

                Vector3 outward = edge.A == node.Index ? edge.Dir : -edge.Dir;
                Vector3 side = Vector3.Cross(Vector3.up, outward);

                // Zebra crossing just outside the box.
                Vector3 crossCentre = c + outward * (r + 1.9f) + Vector3.up * PaintY;
                for (int i = -3; i <= 3; i++)
                {
                    Vector3 stripeCentre = crossCentre + side * (i * 1.75f);
                    AddOrientedRect(mb, stripeCentre, outward, 2.6f, 0.85f, _paintWhite);
                }

                // Stop line for the lane arriving here.
                Vector3 stopCentre = c + outward * (r + 3.8f) + side * (-Tuning.LaneOffset) + Vector3.up * PaintY;
                AddOrientedRect(mb, stopCentre, outward, 0.42f, Tuning.RoadHalfWidth - 0.6f, _paintWhite);
            }
        }

        void BuildRoadSegment(CityModel model, RoadEdge edge)
        {
            Vector3 a = model.Nodes[edge.A].Position;
            Vector3 b = model.Nodes[edge.B].Position;
            Vector3 dir = edge.Dir;
            Vector3 side = Vector3.Cross(Vector3.up, dir);

            Vector3 start = a + dir * Tuning.RoadHalfWidth;
            Vector3 end = b - dir * Tuning.RoadHalfWidth;
            float length = Vector3.Distance(start, end);
            if (length <= 0.2f) return;

            Vector3 mid = (start + end) * 0.5f;
            MeshBuilder mb = Chunk(mid);

            AddOrientedRect(mb, mid + Vector3.up * RoadY, dir, length, Tuning.RoadHalfWidth * 2f, _asphalt);

            // Gutter strips: slightly lighter asphalt hugging the curb.
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 gutter = mid + side * (s * (Tuning.RoadHalfWidth - 0.55f)) + Vector3.up * (RoadY + 0.004f);
                AddOrientedRect(mb, gutter, dir, length, 1.1f, _asphaltWorn);
            }

            // Centre line: double yellow on avenues, dashes on side streets.
            if (edge.IsAvenue)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 p = mid + side * (s * 0.22f) + Vector3.up * PaintY;
                    AddOrientedRect(mb, p, dir, length - 5f, 0.16f, _paintYellow);
                }
            }
            else
            {
                int dashes = Mathf.Max(1, Mathf.FloorToInt(length / 5.5f));
                for (int i = 0; i < dashes; i++)
                {
                    float t = (i + 0.5f) / dashes;
                    Vector3 p = Vector3.Lerp(start, end, t) + Vector3.up * PaintY;
                    AddOrientedRect(mb, p, dir, 2.6f, 0.15f, _paintWhite);
                }
            }

            // Lane edge lines.
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = mid + side * (s * (Tuning.RoadHalfWidth - 1.35f)) + Vector3.up * PaintY;
                AddOrientedRect(mb, p, dir, length - 8f, 0.13f, _paintWhite);
            }

            // Direction arrows painted in each lane, pointing the way traffic flows.
            if (length > 24f)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 laneDir = s > 0 ? dir : -dir;
                    Vector3 laneSide = Vector3.Cross(Vector3.up, laneDir);
                    Vector3 laneCentre = mid + laneSide * Tuning.LaneOffset + Vector3.up * PaintY;
                    AddLaneArrow(mb, laneCentre - laneDir * (length * 0.22f), laneDir);
                }
            }

            // Drains and manholes for texture.
            int detailCount = Mathf.FloorToInt(length / 22f);
            for (int i = 0; i < detailCount; i++)
            {
                float t = (i + 0.7f) / (detailCount + 0.4f);
                Vector3 p = Vector3.Lerp(start, end, t) + side * (Tuning.RoadHalfWidth - 0.9f) + Vector3.up * (PaintY * 0.5f);
                mb.AddDisc(p, 0.42f, 8, _drain);
            }
        }

        void AddLaneArrow(MeshBuilder mb, Vector3 centre, Vector3 dir)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            AddOrientedRect(mb, centre, dir, 2.1f, 0.34f, _paintWhite);

            Vector3 tip = centre + dir * 1.55f;
            Vector3 baseL = centre + dir * 0.75f - side * 0.52f;
            Vector3 baseR = centre + dir * 0.75f + side * 0.52f;
            mb.AddTriangle(baseL, tip, baseR, _paintWhite);
        }

        void BuildAlleySurface(CityModel model, RoadEdge edge)
        {
            Vector3 a = model.Nodes[edge.A].Position;
            Vector3 b = model.Nodes[edge.B].Position;
            Vector3 dir = edge.Dir;

            // Trim the ends so the alley floor stops where the road asphalt begins.
            Vector3 start = a + dir * Tuning.RoadHalfWidth;
            Vector3 end = b - dir * Tuning.RoadHalfWidth;
            float length = Vector3.Distance(start, end);
            if (length < 1f) return;

            Vector3 mid = (start + end) * 0.5f;
            MeshBuilder mb = Chunk(mid);
            AddOrientedRect(mb, mid + Vector3.up * (RoadY + 0.006f), dir, length, Tuning.AlleyHalfWidth * 2f, _alleyFloor);

            // A drainage channel down the middle, the way real Taipei alleys look.
            AddOrientedRect(mb, mid + Vector3.up * (RoadY + 0.010f), dir, length, 0.5f, _drain);
        }

        void BuildSidewalk(CityModel model, CityBuilder layout, CityBlock block)
        {
            MeshBuilder mb = Chunk(block.Centre);
            float y = Tuning.CurbHeight;
            float hx = block.Size.x * 0.5f;
            float hz = block.Size.y * 0.5f;
            Vector3 c = block.Centre;

            // Top surface of the whole raised block. Buildings sit on top of this.
            mb.AddFloorRect(c + Vector3.up * y, block.Size.x, block.Size.y, _sidewalkTop);

            // Curb faces around the perimeter.
            AddCurbFace(mb, c + new Vector3(0f, 0f, -hz), Vector3.back, block.Size.x, y);
            AddCurbFace(mb, c + new Vector3(0f, 0f, hz), Vector3.forward, block.Size.x, y);
            AddCurbFace(mb, c + new Vector3(-hx, 0f, 0f), Vector3.left, block.Size.y, y);
            AddCurbFace(mb, c + new Vector3(hx, 0f, 0f), Vector3.right, block.Size.y, y);

            // Paving joints: a light line a little in from the curb, all the way round.
            float inset = Tuning.SidewalkWidth * 0.88f;
            float py = y + 0.006f;
            AddOrientedRect(mb, c + new Vector3(0f, py, -hz + inset), Vector3.right, block.Size.x - 0.4f, 0.10f, _curbEdge);
            AddOrientedRect(mb, c + new Vector3(0f, py, hz - inset), Vector3.right, block.Size.x - 0.4f, 0.10f, _curbEdge);
            AddOrientedRect(mb, c + new Vector3(-hx + inset, py, 0f), Vector3.forward, block.Size.y - 0.4f, 0.10f, _curbEdge);
            AddOrientedRect(mb, c + new Vector3(hx - inset, py, 0f), Vector3.forward, block.Size.y - 0.4f, 0.10f, _curbEdge);

            // Tactile strip right at the kerb edge.
            AddOrientedRect(mb, c + new Vector3(0f, py, -hz + 0.28f), Vector3.right, block.Size.x, 0.32f, _curbEdge);
            AddOrientedRect(mb, c + new Vector3(0f, py, hz - 0.28f), Vector3.right, block.Size.x, 0.32f, _curbEdge);
            AddOrientedRect(mb, c + new Vector3(-hx + 0.28f, py, 0f), Vector3.forward, block.Size.y, 0.32f, _curbEdge);
            AddOrientedRect(mb, c + new Vector3(hx - 0.28f, py, 0f), Vector3.forward, block.Size.y, 0.32f, _curbEdge);

            // Cut the alley back down to road level and ramp its mouth.
            for (int i = 0; i < layout.Alleys.Count; i++)
            {
                Alley alley = layout.Alleys[i];
                if (alley.BlockIndex != block.Index) continue;

                Vector3 dir = (alley.To - alley.From).normalized;
                float span = alley.Vertical ? block.Size.y : block.Size.x;
                Vector3 aCentre = block.Centre;
                float w = Tuning.AlleyHalfWidth * 2f;

                // The alley floor, drawn on top of the raised block, plus its side walls.
                AddOrientedRect(mb, aCentre + Vector3.up * (RoadY + 0.014f), dir, span + 1f, w, _alleyFloor);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, dir) * (s * Tuning.AlleyHalfWidth);
                    Vector3 wallCentre = aCentre + side + Vector3.up * (y * 0.5f);
                    Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
                    mb.AddBox(wallCentre, new Vector3(0.10f, y, span + 1f), rot, _curbEdge, _sidewalkSide, _sidewalkSide);
                }
            }
        }

        /// <summary>
        /// The pavement on the outside of the boundary roads, which the perimeter shophouses
        /// stand on. Built as four long strips rather than a ring of blocks.
        /// </summary>
        void BuildPerimeterPavement(CityModel model)
        {
            float halfX = Tuning.WorldSizeX * 0.5f;
            float halfZ = Tuning.WorldSizeZ * 0.5f;
            float r = Tuning.RoadHalfWidth;
            float depth = Tuning.SidewalkWidth + 16f;   // deep enough to carry the buildings
            float y = Tuning.CurbHeight;

            // Each strip runs the full side plus the corners.
            float spanX = Tuning.WorldSizeX + (r + depth) * 2f;
            float spanZ = Tuning.WorldSizeZ + (r + depth) * 2f;

            BuildStrip(new Vector3(0f, 0f, -halfZ - r - depth * 0.5f), spanX, depth, Vector3.forward);
            BuildStrip(new Vector3(0f, 0f, halfZ + r + depth * 0.5f), spanX, depth, Vector3.back);
            BuildStrip(new Vector3(-halfX - r - depth * 0.5f, 0f, 0f), depth, spanZ, Vector3.right);
            BuildStrip(new Vector3(halfX + r + depth * 0.5f, 0f, 0f), depth, spanZ, Vector3.left);

            void BuildStrip(Vector3 centre, float sizeX, float sizeZ, Vector3 inward)
            {
                MeshBuilder mb = Chunk(centre);
                mb.AddFloorRect(centre + Vector3.up * y, sizeX, sizeZ, _sidewalkTop);

                // Curb face on the road side.
                float length = Mathf.Abs(inward.x) > 0.5f ? sizeZ : sizeX;
                Vector3 edge = centre + inward * (Mathf.Abs(inward.x) > 0.5f ? sizeX * 0.5f : sizeZ * 0.5f);
                AddCurbFace(mb, edge, inward, length, y);
                AddOrientedRect(mb, edge - inward * 0.3f + Vector3.up * (y + 0.006f),
                    Vector3.Cross(Vector3.up, inward), length, 0.34f, _curbEdge);
            }
        }

        void AddCurbFace(MeshBuilder mb, Vector3 edgeCentre, Vector3 outward, float length, float height)
        {
            Vector3 along = Vector3.Cross(Vector3.up, outward) * (length * 0.5f);
            Vector3 o = outward * 0.001f;

            Vector3 bl = edgeCentre - along + o;
            Vector3 br = edgeCentre + along + o;
            Vector3 tl = bl + Vector3.up * height;
            Vector3 tr = br + Vector3.up * height;

            // Wound bl -> br -> tr -> tl so the face normal comes out along `outward`.
            mb.AddQuad(bl, br, tr, tl, outward, _sidewalkSide);
        }

        /// <summary>A flat rect on the ground, oriented so its length runs along <paramref name="dir"/>.</summary>
        static void AddOrientedRect(MeshBuilder mb, Vector3 centre, Vector3 dir, float length, float width, int slot)
        {
            Vector3 f = dir.normalized * (length * 0.5f);
            Vector3 s = Vector3.Cross(Vector3.up, dir.normalized) * (width * 0.5f);
            mb.AddQuad(centre - f - s, centre + f - s, centre + f + s, centre - f + s, Vector3.up, slot);
        }

        // ---------------------------------------------------------------- colliders

        void BuildColliders(CityModel model, CityBuilder layout, Transform parent)
        {
            int groundLayer = LayerMask.NameToLayer(Tuning.LayerGround);

            // One slab under the whole city gives the road surface.
            var slab = new GameObject("GroundCollider");
            slab.transform.SetParent(parent, false);
            slab.layer = groundLayer;
            var slabBox = slab.AddComponent<BoxCollider>();
            Vector3 worldSize = model.WorldBounds.size;
            slabBox.size = new Vector3(worldSize.x + 400f, 4f, worldSize.z + 400f);
            slabBox.center = new Vector3(0f, -2f, 0f);

            // Raised blocks, split around any alley so the shortcut stays at road level.
            var holder = new GameObject("SidewalkColliders");
            holder.transform.SetParent(parent, false);
            holder.layer = groundLayer;

            foreach (CityBlock block in model.Blocks)
            {
                Alley? alley = null;
                for (int i = 0; i < layout.Alleys.Count; i++)
                    if (layout.Alleys[i].BlockIndex == block.Index) { alley = layout.Alleys[i]; break; }

                if (alley == null)
                {
                    AddSlab(holder.transform, block.Centre, block.Size.x, block.Size.y, groundLayer);
                    continue;
                }

                if (alley.Value.Vertical)
                {
                    float half = (block.Size.x - Tuning.AlleyHalfWidth * 2f) * 0.5f;
                    float offset = Tuning.AlleyHalfWidth + half * 0.5f;
                    AddSlab(holder.transform, block.Centre + Vector3.left * offset, half, block.Size.y, groundLayer);
                    AddSlab(holder.transform, block.Centre + Vector3.right * offset, half, block.Size.y, groundLayer);
                }
                else
                {
                    float half = (block.Size.y - Tuning.AlleyHalfWidth * 2f) * 0.5f;
                    float offset = Tuning.AlleyHalfWidth + half * 0.5f;
                    AddSlab(holder.transform, block.Centre + Vector3.back * offset, block.Size.x, half, groundLayer);
                    AddSlab(holder.transform, block.Centre + Vector3.forward * offset, block.Size.x, half, groundLayer);
                }
            }

            // Perimeter pavement, plus a hard wall so the rider cannot leave the city.
            float halfX = Tuning.WorldSizeX * 0.5f;
            float halfZ = Tuning.WorldSizeZ * 0.5f;
            float r = Tuning.RoadHalfWidth;
            float depth = Tuning.SidewalkWidth + 16f;
            float spanX = Tuning.WorldSizeX + (r + depth) * 2f;
            float spanZ = Tuning.WorldSizeZ + (r + depth) * 2f;

            AddSlab(holder.transform, new Vector3(0f, 0f, -halfZ - r - depth * 0.5f), spanX, depth, groundLayer);
            AddSlab(holder.transform, new Vector3(0f, 0f, halfZ + r + depth * 0.5f), spanX, depth, groundLayer);
            AddSlab(holder.transform, new Vector3(-halfX - r - depth * 0.5f, 0f, 0f), depth, spanZ, groundLayer);
            AddSlab(holder.transform, new Vector3(halfX + r + depth * 0.5f, 0f, 0f), depth, spanZ, groundLayer);

            var walls = new GameObject("BoundaryWalls");
            walls.transform.SetParent(parent, false);
            int buildingLayer = LayerMask.NameToLayer(Tuning.LayerBuilding);

            float wallOffset = halfZ + r + Tuning.SidewalkWidth + 1f;
            AddWall(walls.transform, new Vector3(0f, 0f, -wallOffset), spanX, 1f, buildingLayer);
            AddWall(walls.transform, new Vector3(0f, 0f, wallOffset), spanX, 1f, buildingLayer);
            AddWall(walls.transform, new Vector3(-(halfX + r + Tuning.SidewalkWidth + 1f), 0f, 0f), 1f, spanZ, buildingLayer);
            AddWall(walls.transform, new Vector3(halfX + r + Tuning.SidewalkWidth + 1f, 0f, 0f), 1f, spanZ, buildingLayer);
        }

        static void AddWall(Transform parent, Vector3 centre, float sizeX, float sizeZ, int layer)
        {
            var go = new GameObject("BoundaryWall");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(centre.x, 0f, centre.z);
            go.layer = layer;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(sizeX, 24f, sizeZ);
            box.center = new Vector3(0f, 12f, 0f);
        }

        static void AddSlab(Transform parent, Vector3 centre, float sizeX, float sizeZ, int layer)
        {
            var go = new GameObject("BlockSlab");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(centre.x, 0f, centre.z);
            go.layer = layer;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(sizeX, Tuning.CurbHeight * 2f, sizeZ);
            box.center = Vector3.zero;   // half above ground, half below: gives a clean 16 cm step
        }
    }
}
