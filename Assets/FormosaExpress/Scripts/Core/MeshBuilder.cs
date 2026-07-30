using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FormosaExpress.Core
{
    /// <summary>
    /// Accumulates hard-edged, flat-shaded geometry and bakes it into a single mesh.
    /// All colour comes from <see cref="Palette"/> slots, so any amount of geometry built
    /// through one builder shares a single material.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _verts = new List<Vector3>(1024);
        readonly List<Vector3> _normals = new List<Vector3>(1024);
        readonly List<Vector2> _uvs = new List<Vector2>(1024);
        readonly List<int> _tris = new List<int>(2048);
        readonly Palette _palette;

        public MeshBuilder(Palette palette)
        {
            _palette = palette;
        }

        public int VertexCount => _verts.Count;
        public bool IsEmpty => _tris.Count == 0;
        public Palette Palette => _palette;

        public void Clear()
        {
            _verts.Clear();
            _normals.Clear();
            _uvs.Clear();
            _tris.Clear();
        }

        // ---------------------------------------------------------------- primitives

        /// <summary>Adds a quad wound a-b-c-d. The normal is derived from the winding.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int slot)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            AddQuad(a, b, c, d, n, slot);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int slot)
        {
            int baseIndex = _verts.Count;
            Vector2 uv = _palette.UV(slot);

            _verts.Add(a); _verts.Add(b); _verts.Add(c); _verts.Add(d);
            _normals.Add(normal); _normals.Add(normal); _normals.Add(normal); _normals.Add(normal);
            _uvs.Add(uv); _uvs.Add(uv); _uvs.Add(uv); _uvs.Add(uv);

            _tris.Add(baseIndex); _tris.Add(baseIndex + 1); _tris.Add(baseIndex + 2);
            _tris.Add(baseIndex); _tris.Add(baseIndex + 2); _tris.Add(baseIndex + 3);
        }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, int slot)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int baseIndex = _verts.Count;
            Vector2 uv = _palette.UV(slot);

            _verts.Add(a); _verts.Add(b); _verts.Add(c);
            _normals.Add(n); _normals.Add(n); _normals.Add(n);
            _uvs.Add(uv); _uvs.Add(uv); _uvs.Add(uv);

            _tris.Add(baseIndex); _tris.Add(baseIndex + 1); _tris.Add(baseIndex + 2);
        }

        /// <summary>A flat, upward-facing rectangle centred on <paramref name="centre"/>.</summary>
        public void AddFloorRect(Vector3 centre, float sizeX, float sizeZ, int slot)
        {
            float hx = sizeX * 0.5f, hz = sizeZ * 0.5f;
            AddQuad(
                centre + new Vector3(-hx, 0f, -hz),
                centre + new Vector3(-hx, 0f, hz),
                centre + new Vector3(hx, 0f, hz),
                centre + new Vector3(hx, 0f, -hz),
                Vector3.up, slot);
        }

        public void AddBox(Vector3 centre, Vector3 size, int slot)
        {
            AddBox(centre, size, Quaternion.identity, slot, slot, slot);
        }

        public void AddBox(Vector3 centre, Vector3 size, Quaternion rotation, int slot)
        {
            AddBox(centre, size, rotation, slot, slot, slot);
        }

        /// <summary>Axis-aligned box with separate top/side/bottom slots.</summary>
        public void AddBox(Vector3 centre, Vector3 size, int topSlot, int sideSlot, int bottomSlot)
        {
            AddBox(centre, size, Quaternion.identity, topSlot, sideSlot, bottomSlot);
        }

        /// <summary>
        /// A box with separate palette slots for the top, the four sides and the bottom.
        /// Using a darker slot for sides fakes ambient occlusion without vertex colours.
        /// </summary>
        public void AddBox(Vector3 centre, Vector3 size, Quaternion rotation, int topSlot, int sideSlot, int bottomSlot)
        {
            Vector3 h = size * 0.5f;
            Vector3 rx = rotation * Vector3.right * h.x;
            Vector3 ry = rotation * Vector3.up * h.y;
            Vector3 rz = rotation * Vector3.forward * h.z;

            Vector3 p000 = centre - rx - ry - rz;
            Vector3 p100 = centre + rx - ry - rz;
            Vector3 p110 = centre + rx + ry - rz;
            Vector3 p010 = centre - rx + ry - rz;
            Vector3 p001 = centre - rx - ry + rz;
            Vector3 p101 = centre + rx - ry + rz;
            Vector3 p111 = centre + rx + ry + rz;
            Vector3 p011 = centre - rx + ry + rz;

            AddQuad(p010, p011, p111, p110, topSlot);      // +Y
            AddQuad(p000, p100, p101, p001, bottomSlot);   // -Y
            AddQuad(p001, p101, p111, p011, sideSlot);     // +Z
            AddQuad(p100, p000, p010, p110, sideSlot);     // -Z
            AddQuad(p101, p100, p110, p111, sideSlot);     // +X
            AddQuad(p000, p001, p011, p010, sideSlot);     // -X
        }

        /// <summary>An axis-aligned box defined by opposite corners.</summary>
        public void AddBounds(Vector3 min, Vector3 max, int topSlot, int sideSlot)
        {
            Vector3 centre = (min + max) * 0.5f;
            AddBox(centre, max - min, Quaternion.identity, topSlot, sideSlot, sideSlot);
        }

        /// <summary>A prism-like tapered box, handy for roofs, awnings and vehicle bodies.</summary>
        public void AddTaperedBox(Vector3 centre, Vector3 size, float topScaleX, float topScaleZ,
            Quaternion rotation, int topSlot, int sideSlot)
        {
            Vector3 h = size * 0.5f;
            Vector3 up = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;
            Vector3 fwd = rotation * Vector3.forward;

            Vector3 bC = centre - up * h.y;
            Vector3 tC = centre + up * h.y;
            Vector3 br = right * h.x, bf = fwd * h.z;
            Vector3 tr = right * (h.x * topScaleX), tf = fwd * (h.z * topScaleZ);

            Vector3 b00 = bC - br - bf, b10 = bC + br - bf, b11 = bC + br + bf, b01 = bC - br + bf;
            Vector3 t00 = tC - tr - tf, t10 = tC + tr - tf, t11 = tC + tr + tf, t01 = tC - tr + tf;

            AddQuad(t00, t01, t11, t10, topSlot);
            AddQuad(b00, b10, b11, b01, sideSlot);
            AddQuad(b01, b11, t11, t01, sideSlot);
            AddQuad(b10, b00, t00, t10, sideSlot);
            AddQuad(b11, b10, t10, t11, sideSlot);
            AddQuad(b00, b01, t01, t00, sideSlot);
        }

        public void AddCylinder(Vector3 baseCentre, float radius, float height, int sides, int slot)
        {
            AddCylinder(baseCentre, radius, radius, height, sides, Quaternion.identity, slot, slot);
        }

        public void AddCylinder(Vector3 baseCentre, float bottomRadius, float topRadius, float height,
            int sides, Quaternion rotation, int capSlot, int sideSlot)
        {
            sides = Mathf.Max(3, sides);
            Vector3 up = rotation * Vector3.up * height;
            Vector3 topCentre = baseCentre + up;

            // Winding note: this builder treats Cross(b - a, c - a) as the outward normal, which
            // matches Unity's front-face convention. Radial geometry has to be wound
            // "up the wall then round" to come out facing outwards rather than inwards.
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;

                Vector3 d0 = rotation * new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = rotation * new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                Vector3 b0 = baseCentre + d0 * bottomRadius;
                Vector3 b1 = baseCentre + d1 * bottomRadius;
                Vector3 t0 = topCentre + d0 * topRadius;
                Vector3 t1 = topCentre + d1 * topRadius;

                if (topRadius > 0.0001f) AddQuad(b0, t0, t1, b1, sideSlot);
                else AddTriangle(b0, topCentre, b1, sideSlot);

                AddTriangle(topCentre, t1, t0, capSlot);
                AddTriangle(baseCentre, b0, b1, capSlot);
            }
        }

        /// <summary>
        /// An open-ended tube: side walls only, no caps. Additive geometry needs this, because
        /// end caps stack another layer of brightness exactly where you least want it.
        /// </summary>
        public void AddTube(Vector3 baseCentre, float bottomRadius, float topRadius, float height,
            int sides, int slot)
        {
            sides = Mathf.Max(3, sides);
            Vector3 topCentre = baseCentre + Vector3.up * height;

            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;

                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                AddQuad(baseCentre + d0 * bottomRadius, topCentre + d0 * topRadius,
                    topCentre + d1 * topRadius, baseCentre + d1 * bottomRadius, slot);
            }
        }

        /// <summary>A disc facing +Y, used for glow pads and shadow blobs.</summary>
        public void AddDisc(Vector3 centre, float radius, int sides, int slot)
        {
            sides = Mathf.Max(3, sides);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 p0 = centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                AddTriangle(centre, p1, p0, slot);
            }
        }

        /// <summary>A ring/annulus facing +Y, used for the beacon ground marker.</summary>
        public void AddRing(Vector3 centre, float innerRadius, float outerRadius, int sides, int slot)
        {
            sides = Mathf.Max(3, sides);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                AddQuad(
                    centre + d0 * innerRadius,
                    centre + d1 * innerRadius,
                    centre + d1 * outerRadius,
                    centre + d0 * outerRadius,
                    Vector3.up, slot);
            }
        }

        /// <summary>A double-sided vertical billboard, for signs, banners and flags.</summary>
        public void AddDoubleSidedQuad(Vector3 centre, Vector3 right, Vector3 up, int slot)
        {
            Vector3 a = centre - right - up;
            Vector3 b = centre - right + up;
            Vector3 c = centre + right + up;
            Vector3 d = centre + right - up;
            AddQuad(a, b, c, d, slot);
            AddQuad(d, c, b, a, slot);
        }

        /// <summary>A thin tube between two points, for cables, wires and railings.</summary>
        public void AddBeam(Vector3 from, Vector3 to, float thickness, int slot)
        {
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.0001f) return;
            Quaternion rot = Quaternion.LookRotation(dir / len, Vector3.up);
            AddBox((from + to) * 0.5f, new Vector3(thickness, thickness, len), rot, slot);
        }

        /// <summary>A slack catenary-ish cable, drawn as a chain of beams.</summary>
        public void AddSaggingCable(Vector3 from, Vector3 to, float sag, float thickness, int segments, int slot)
        {
            segments = Mathf.Max(2, segments);
            Vector3 prev = from;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * sag;
                AddBeam(prev, p, thickness, slot);
                prev = p;
            }
        }

        // ---------------------------------------------------------------- output

        public Mesh ToMesh(string name, bool markNoLongerReadable = true)
        {
            var mesh = new Mesh { name = name };
            if (_verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_tris, 0, true);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(markNoLongerReadable);
            return mesh;
        }

        /// <summary>Bakes into a child GameObject with a renderer, then clears the builder.</summary>
        public GameObject Flush(string name, Transform parent, Material material, bool castShadows = true)
        {
            if (IsEmpty) return null;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = ToMesh(name + "_Mesh");

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = castShadows;

            Clear();
            return go;
        }
    }
}
