using System.Collections.Generic;
using UnityEngine;
using FormosaExpress.Core;

namespace FormosaExpress.City
{
    /// <summary>
    /// Street furniture: utility poles and overhead cables, ranks of parked scooters, market
    /// stalls, traffic lights, trees, bins and cones. This is the layer that makes the streets
    /// feel lived in rather than like a grid of boxes.
    /// </summary>
    public sealed class PropFactory
    {
        readonly Palette _pal;

        readonly int _poleGrey, _poleDark, _cable, _metal, _metalDark, _rubber, _chrome;
        readonly int _concrete, _concreteDark, _trunk, _leaf, _leafDark, _binGreen, _binDark;
        readonly int _coneOrange, _coneWhite, _plastic, _canvasRed, _canvasWhite;
        readonly int _lampRed, _lampAmber, _lampGreen, _lanternRed, _lanternWarm, _vendingGlow;
        readonly int[] _scooterBody, _cloth, _neon;

        /// <summary>
        /// Additive geometry sink for light pools. Set per block by the caller; kept as a field
        /// rather than threaded through every prop method, which would be all noise.
        /// </summary>
        MeshBuilder _additive;

        readonly int _lightPoolWarm, _lightPoolAmber;

        public PropFactory(Palette palette)
        {
            _pal = palette;

            // A single flat disc reads as paint on the road. Light pools are instead built from
            // concentric discs of this one very dim colour: because the material is additive the
            // overlaps accumulate towards the middle, which gives a soft falloff for free.
            _lightPoolWarm = _pal.Add(new Color(0.052f, 0.037f, 0.019f));
            _lightPoolAmber = _pal.Add(new Color(0.058f, 0.031f, 0.010f));

            _poleGrey = _pal.Add(new Color(0.58f, 0.57f, 0.55f));
            _poleDark = _pal.Add(new Color(0.34f, 0.34f, 0.33f));
            _cable = _pal.Add(new Color(0.11f, 0.11f, 0.12f));
            _metal = _pal.Add(new Color(0.64f, 0.66f, 0.68f));
            _metalDark = _pal.Add(new Color(0.34f, 0.36f, 0.38f));
            _rubber = _pal.Add(new Color(0.10f, 0.10f, 0.11f));
            _chrome = _pal.Add(new Color(0.80f, 0.83f, 0.86f));
            _concrete = _pal.Add(new Color(0.55f, 0.54f, 0.52f));
            _concreteDark = _pal.Add(new Color(0.33f, 0.33f, 0.32f));
            _trunk = _pal.Add(new Color(0.32f, 0.24f, 0.18f));
            _leaf = _pal.Add(new Color(0.20f, 0.42f, 0.22f));
            _leafDark = _pal.Add(new Color(0.13f, 0.29f, 0.16f));
            _binGreen = _pal.Add(new Color(0.20f, 0.38f, 0.26f));
            _binDark = _pal.Add(new Color(0.12f, 0.22f, 0.16f));
            _coneOrange = _pal.Add(new Color(0.94f, 0.42f, 0.12f));
            _coneWhite = _pal.Add(new Color(0.92f, 0.92f, 0.90f));
            _plastic = _pal.Add(new Color(0.72f, 0.72f, 0.70f));
            _canvasRed = _pal.Add(new Color(0.78f, 0.22f, 0.20f));
            _canvasWhite = _pal.Add(new Color(0.90f, 0.88f, 0.82f));

            _lampRed = _pal.Add(new Color(1.00f, 0.20f, 0.16f));
            _lampAmber = _pal.Add(new Color(1.00f, 0.72f, 0.14f));
            _lampGreen = _pal.Add(new Color(0.24f, 1.00f, 0.44f));
            _lanternRed = _pal.Add(new Color(1.00f, 0.26f, 0.20f));
            _lanternWarm = _pal.Add(new Color(1.00f, 0.80f, 0.48f));
            _vendingGlow = _pal.Add(new Color(0.72f, 0.92f, 1.00f));

            _scooterBody = new int[Art.CarColours.Length];
            for (int i = 0; i < Art.CarColours.Length; i++) _scooterBody[i] = _pal.Add(Art.CarColours[i]);

            _cloth = new int[Art.ClothColours.Length];
            for (int i = 0; i < Art.ClothColours.Length; i++) _cloth[i] = _pal.Add(Art.ClothColours[i]);

            _neon = new int[Art.NeonColours.Length];
            for (int i = 0; i < Art.NeonColours.Length; i++) _neon[i] = _pal.Add(Art.NeonColours[i]);
        }

        /// <summary>
        /// Dresses the pavements of one block. Poles and their cables are emitted here too;
        /// the block owns the ones on its south and west edges so no pole is built twice.
        /// </summary>
        public void BuildBlockProps(CityModel model, CityBuilder layout, CityBlock block,
            MeshBuilder surface, MeshBuilder glow, MeshBuilder additive, ref Rng rng, float nightFactor)
        {
            _additive = additive;
            float y = Tuning.CurbHeight;
            float hx = block.Size.x * 0.5f;
            float hz = block.Size.y * 0.5f;
            Vector3 c = block.Centre;

            // Each of the four pavements gets dressed independently.
            DressPavement(c + new Vector3(0f, y, -hz), Vector3.right, Vector3.back, block.Size.x, surface, glow, ref rng, nightFactor);
            DressPavement(c + new Vector3(0f, y, hz), Vector3.left, Vector3.forward, block.Size.x, surface, glow, ref rng, nightFactor);
            DressPavement(c + new Vector3(-hx, y, 0f), Vector3.back, Vector3.left, block.Size.y, surface, glow, ref rng, nightFactor);
            DressPavement(c + new Vector3(hx, y, 0f), Vector3.forward, Vector3.right, block.Size.y, surface, glow, ref rng, nightFactor);

            BuildCourtyard(block, surface, glow, ref rng);
            BuildAlleyDressing(layout, block, surface, glow, ref rng);
        }

        // ------------------------------------------------------------------ pavements

        /// <summary>
        /// <paramref name="edgeCentre"/> is the middle of one pavement edge (on top of the curb),
        /// <paramref name="along"/> runs down it and <paramref name="outward"/> points at the road.
        /// </summary>
        void DressPavement(Vector3 edgeCentre, Vector3 along, Vector3 outward, float length,
            MeshBuilder surface, MeshBuilder glow, ref Rng rng, float nightFactor)
        {
            Vector3 curbLine = edgeCentre;                                    // curb edge
            Vector3 inner = edgeCentre - outward * Tuning.SidewalkWidth;       // building line

            // A rank of parked scooters: the single most Taipei thing on the street.
            if (rng.Chance(0.78f))
            {
                int count = rng.Range(3, 8);
                float spacing = 0.78f;
                float startT = rng.Range(-length * 0.5f + 3f, length * 0.5f - count * spacing - 3f);
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = curbLine + along * (startT + i * spacing) - outward * 0.95f;
                    float yaw = MathX.SignedYawTo(Vector3.forward, -outward) + rng.Range(-14f, 14f);
                    BuildParkedScooter(surface, glow, p, Quaternion.Euler(0f, yaw, 0f), ref rng);
                }
            }

            // Utility poles along the kerb, with cables strung between them.
            int poleCount = Mathf.Max(2, Mathf.FloorToInt(length / 22f));
            var polesTops = new List<Vector3>(poleCount);
            for (int i = 0; i < poleCount; i++)
            {
                float t = (i + 0.5f) / poleCount - 0.5f;
                Vector3 p = curbLine + along * (t * (length - 4f)) - outward * 0.45f;
                float h = rng.Range(7.5f, 9.2f);
                BuildUtilityPole(surface, glow, p, outward, along, h, ref rng, nightFactor);
                polesTops.Add(p + Vector3.up * (h - 0.6f));
            }

            for (int i = 1; i < polesTops.Count; i++)
            {
                // Three cables at slightly different heights, all sagging.
                for (int k = 0; k < 3; k++)
                {
                    Vector3 a = polesTops[i - 1] + Vector3.up * (k * 0.30f) + along * 0f;
                    Vector3 b = polesTops[i] + Vector3.up * (k * 0.30f);
                    surface.AddSaggingCable(a, b, 0.55f + k * 0.12f, 0.035f, 5, _cable);
                }
            }

            // Stalls, trees, bins and vending machines spread along the pavement. Dense on
            // purpose: an empty pavement is what makes a procedural city look procedural.
            int slots = Mathf.Max(3, Mathf.FloorToInt(length / 6.5f));
            for (int i = 0; i < slots; i++)
            {
                float t = (i + 0.5f) / slots - 0.5f;
                Vector3 basePos = Vector3.Lerp(curbLine, inner, 0.42f) + along * (t * (length - 6f));
                float roll = rng.Value;

                if (roll < 0.24f)
                {
                    BuildMarketStall(surface, glow, basePos, -outward, along, ref rng, nightFactor);
                }
                else if (roll < 0.40f)
                {
                    BuildTree(surface, basePos + outward * 0.6f, ref rng);
                }
                else if (roll < 0.52f)
                {
                    BuildVendingMachine(surface, glow, basePos, -outward, ref rng);
                }
                else if (roll < 0.66f)
                {
                    BuildBins(surface, basePos, along, ref rng);
                }
                else if (roll < 0.78f)
                {
                    BuildBollards(surface, curbLine + along * (t * (length - 6f)) - outward * 0.35f, along, ref rng);
                }
                else if (roll < 0.88f)
                {
                    BuildConesAndBarrier(surface, glow, basePos, along, ref rng);
                }
                else
                {
                    BuildStoolCluster(surface, basePos, along, ref rng);
                }
            }
        }

        /// <summary>
        /// Stacks concentric additive discs so the brightness ramps towards the centre. Cheaper
        /// and sharper than a textured quad, and it keeps everything on the shared atlas.
        /// </summary>
        void AddSoftPool(Vector3 centre, float radius, int layers, int slot)
        {
            for (int i = 0; i < layers; i++)
            {
                float r = radius * (1f - i / (float)layers);
                _additive.AddDisc(centre + Vector3.up * (i * 0.004f), r, 18, slot);
            }
        }

        void BuildUtilityPole(MeshBuilder surface, MeshBuilder glow, Vector3 basePos,
            Vector3 outward, Vector3 along, float height, ref Rng rng, float nightFactor)
        {
            surface.AddCylinder(basePos, 0.16f, 0.12f, height, 7, Quaternion.identity, _poleGrey, _poleDark);

            // Cross-arms.
            for (int k = 0; k < 2; k++)
            {
                float y = height - 0.6f - k * 0.75f;
                Vector3 a = basePos + Vector3.up * y - along * 0.7f;
                Vector3 b = basePos + Vector3.up * y + along * 0.7f;
                surface.AddBeam(a, b, 0.08f, _poleDark);
                for (int s = -1; s <= 1; s += 2)
                    surface.AddCylinder(basePos + Vector3.up * y + along * (s * 0.55f), 0.07f, 0.18f, 5, _plastic);
            }

            // Transformer can.
            if (rng.Chance(0.35f))
                surface.AddCylinder(basePos + Vector3.up * (height - 2.4f), 0.30f, 0.30f, 0.85f, 8,
                    Quaternion.identity, _metalDark, _metalDark);

            // Street lamp on an arm reaching over the road; always on.
            if (rng.Chance(0.55f))
            {
                float lampY = height - 1.5f;
                Vector3 armA = basePos + Vector3.up * lampY;
                Vector3 armB = armA + outward * 1.7f + Vector3.up * 0.35f;
                surface.AddBeam(armA, armB, 0.07f, _poleDark);
                surface.AddBox(armB + Vector3.up * 0.02f, new Vector3(0.72f, 0.14f, 0.34f),
                    Quaternion.LookRotation(outward, Vector3.up), _metal, _metalDark, _metalDark);
                glow.AddBox(armB - Vector3.up * 0.08f, new Vector3(0.62f, 0.06f, 0.26f),
                    Quaternion.LookRotation(outward, Vector3.up), _lanternWarm, _lanternWarm, _lanternWarm);

                // The pool of light it casts on the road, plus a soft halo at the lamp itself.
                if (_additive != null)
                {
                    AddSoftPool(new Vector3(armB.x, 0.02f, armB.z), 3.7f, 5, _lightPoolWarm);
                    for (int h = 0; h < 3; h++)
                        _additive.AddDoubleSidedQuad(armB - Vector3.up * 0.1f,
                            Vector3.Cross(Vector3.up, outward) * (0.7f - h * 0.18f),
                            Vector3.up * (0.45f - h * 0.12f), _lightPoolWarm);
                }
            }

            // Bundle of fly-posted notices at eye height.
            if (rng.Chance(0.4f))
            {
                int c = _cloth[rng.Range(0, _cloth.Length)];
                surface.AddBox(basePos + Vector3.up * 1.5f - outward * 0.14f,
                    new Vector3(0.30f, 0.42f, 0.03f), Quaternion.LookRotation(-outward, Vector3.up), c, c, c);
            }
        }

        void BuildParkedScooter(MeshBuilder surface, MeshBuilder glow, Vector3 basePos, Quaternion rot, ref Rng rng)
        {
            int body = _scooterBody[rng.Range(0, _scooterBody.Length)];
            Vector3 fwd = rot * Vector3.forward;
            Vector3 right = rot * Vector3.right;

            // Wheels.
            Quaternion wheelRot = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, 0f, 90f);
            surface.AddCylinder(basePos + fwd * 0.55f + Vector3.up * 0.26f - right * 0.055f, 0.26f, 0.26f, 0.11f,
                10, wheelRot, _rubber, _rubber);
            surface.AddCylinder(basePos - fwd * 0.55f + Vector3.up * 0.26f - right * 0.055f, 0.26f, 0.26f, 0.11f,
                10, wheelRot, _rubber, _rubber);

            // Body, seat, floorboard.
            surface.AddTaperedBox(basePos + Vector3.up * 0.52f, new Vector3(0.34f, 0.34f, 1.05f), 0.8f, 0.9f, rot, body, body);
            surface.AddBox(basePos - fwd * 0.28f + Vector3.up * 0.74f, new Vector3(0.32f, 0.14f, 0.55f), rot,
                _rubber, _rubber, _rubber);
            surface.AddBox(basePos + fwd * 0.12f + Vector3.up * 0.36f, new Vector3(0.30f, 0.08f, 0.5f), rot,
                _metalDark, _metalDark, _metalDark);

            // Front column and handlebar.
            surface.AddBox(basePos + fwd * 0.52f + Vector3.up * 0.72f, new Vector3(0.22f, 0.62f, 0.20f), rot, body, body, body);
            surface.AddBeam(basePos + fwd * 0.52f + Vector3.up * 1.02f - right * 0.32f,
                basePos + fwd * 0.52f + Vector3.up * 1.02f + right * 0.32f, 0.045f, _chrome);

            // Headlight and mirrors.
            glow.AddBox(basePos + fwd * 0.66f + Vector3.up * 0.86f, new Vector3(0.18f, 0.12f, 0.05f), rot,
                _lanternWarm, _lanternWarm, _lanternWarm);
            for (int s = -1; s <= 1; s += 2)
                surface.AddBox(basePos + fwd * 0.5f + Vector3.up * 1.18f + right * (s * 0.28f),
                    new Vector3(0.11f, 0.07f, 0.04f), rot, _chrome, _chrome, _chrome);

            // Top box or crate, on some of them.
            if (rng.Chance(0.4f))
            {
                int c = _cloth[rng.Range(0, _cloth.Length)];
                surface.AddBox(basePos - fwd * 0.52f + Vector3.up * 0.92f, new Vector3(0.34f, 0.28f, 0.32f), rot, c, c, c);
            }
        }

        void BuildMarketStall(MeshBuilder surface, MeshBuilder glow, Vector3 basePos, Vector3 facing,
            Vector3 along, ref Rng rng, float nightFactor)
        {
            Quaternion rot = Quaternion.LookRotation(facing, Vector3.up);
            float w = rng.Range(2.0f, 3.0f);
            float d = 1.25f;

            // Trestle table with a cloth.
            surface.AddBox(basePos + Vector3.up * 0.86f, new Vector3(w, 0.10f, d), rot, _canvasWhite, _canvasWhite, _plastic);
            int clothSlot = _cloth[rng.Range(0, _cloth.Length)];
            surface.AddBox(basePos + Vector3.up * 0.44f, new Vector3(w - 0.06f, 0.80f, d - 0.06f), rot,
                clothSlot, clothSlot, clothSlot);

            // Goods piled on top.
            int items = rng.Range(4, 9);
            for (int i = 0; i < items; i++)
            {
                Vector3 p = basePos + Vector3.up * 0.98f
                            + along * rng.Range(-w * 0.42f, w * 0.42f)
                            + facing * rng.Range(-d * 0.3f, d * 0.3f);
                int col = rng.Chance(0.4f) ? _coneOrange : _cloth[rng.Range(0, _cloth.Length)];
                if (rng.Chance(0.5f))
                    surface.AddBox(p + Vector3.up * 0.09f, new Vector3(0.22f, 0.18f, 0.22f),
                        Quaternion.Euler(0f, rng.Range(0f, 90f), 0f), col, col, col);
                else
                    surface.AddCylinder(p, 0.13f, 0.13f, 0.22f, 7, Quaternion.identity, col, col);
            }

            // Umbrella or canopy.
            float poleH = 2.35f;
            surface.AddCylinder(basePos, 0.05f, 0.05f, poleH, 6, Quaternion.identity, _metal, _metal);
            bool striped = rng.Chance(0.5f);
            int canopyA = striped ? _canvasRed : _cloth[rng.Range(0, _cloth.Length)];
            int canopyB = striped ? _canvasWhite : _pal.AddShaded(Color.white, 0.85f);

            int segs = 8;
            float radius = rng.Range(1.5f, 2.0f);
            Vector3 apex = basePos + Vector3.up * (poleH + 0.28f);
            for (int i = 0; i < segs; i++)
            {
                float a0 = i / (float)segs * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
                Vector3 p0 = basePos + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius + Vector3.up * poleH;
                Vector3 p1 = basePos + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius + Vector3.up * poleH;
                int slot = i % 2 == 0 ? canopyA : canopyB;
                surface.AddTriangle(apex, p0, p1, slot);
                surface.AddTriangle(apex, p1, p0, slot);
            }

            // Work lamp under the canopy.
            glow.AddCylinder(basePos + Vector3.up * (poleH - 0.22f), 0.10f, 0.10f, 0.14f, 6,
                Quaternion.identity, _lanternWarm, _lanternWarm);

            // Stools in front.
            int stools = rng.Range(0, 4);
            for (int i = 0; i < stools; i++)
            {
                Vector3 p = basePos + facing * rng.Range(0.9f, 1.5f) + along * rng.Range(-w * 0.5f, w * 0.5f);
                int col = _cloth[rng.Range(0, _cloth.Length)];
                surface.AddBox(p + Vector3.up * 0.20f, new Vector3(0.32f, 0.40f, 0.32f),
                    Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), col, col, col);
            }
        }

        void BuildTree(MeshBuilder surface, Vector3 basePos, ref Rng rng)
        {
            float h = rng.Range(3.4f, 5.6f);
            surface.AddCylinder(basePos, 0.20f, 0.14f, h * 0.55f, 6, Quaternion.identity, _trunk, _trunk);

            // Planter ring.
            surface.AddRing(basePos + Vector3.up * 0.02f, 0.35f, 0.85f, 10, _concreteDark);
            surface.AddCylinder(basePos, 0.88f, 0.88f, 0.22f, 10, Quaternion.identity, _concrete, _concreteDark);

            // Canopy: overlapping wide, shallow clumps at scattered angles. Boxes that taper too
            // hard end up reading as lampshades rather than foliage.
            int blobs = rng.Range(4, 8);
            for (int i = 0; i < blobs; i++)
            {
                Vector3 p = basePos + Vector3.up * (h * rng.Range(0.52f, 0.92f))
                            + rng.OnUnitCircleXZ() * rng.Range(0.1f, 1.05f);
                float r = rng.Range(0.75f, 1.35f);
                int slot = i % 3 == 0 ? _leafDark : _leaf;
                surface.AddTaperedBox(p, new Vector3(r * 2f, r * 1.15f, r * 1.8f), 0.82f, 0.86f,
                    Quaternion.Euler(rng.Range(-14f, 14f), rng.Range(0f, 360f), rng.Range(-14f, 14f)), slot, slot);
            }

            // A couple of branches poking out of the mass.
            int branches = rng.Range(1, 4);
            for (int i = 0; i < branches; i++)
            {
                Vector3 from = basePos + Vector3.up * (h * 0.5f);
                Vector3 to = from + rng.OnUnitCircleXZ() * rng.Range(0.6f, 1.1f) + Vector3.up * rng.Range(0.3f, 0.9f);
                surface.AddBeam(from, to, 0.07f, _trunk);
            }
        }

        void BuildVendingMachine(MeshBuilder surface, MeshBuilder glow, Vector3 basePos, Vector3 facing, ref Rng rng)
        {
            Quaternion rot = Quaternion.LookRotation(facing, Vector3.up);
            surface.AddBox(basePos + Vector3.up * 0.92f, new Vector3(1.05f, 1.84f, 0.62f), rot, _metal, _metalDark, _metalDark);

            // Illuminated display panel.
            glow.AddBox(basePos + facing * 0.33f + Vector3.up * 1.20f, new Vector3(0.82f, 1.10f, 0.05f), rot,
                _vendingGlow, _vendingGlow, _vendingGlow);

            // Product rows as dark slats over the glow.
            for (int i = 0; i < 4; i++)
                surface.AddBox(basePos + facing * 0.36f + Vector3.up * (0.78f + i * 0.28f),
                    new Vector3(0.80f, 0.05f, 0.04f), rot, _metalDark, _metalDark, _metalDark);

            surface.AddBox(basePos + facing * 0.34f + Vector3.up * 0.42f, new Vector3(0.70f, 0.28f, 0.06f), rot,
                _metalDark, _metalDark, _metalDark);
        }

        void BuildBins(MeshBuilder surface, Vector3 basePos, Vector3 along, ref Rng rng)
        {
            int count = rng.Range(1, 4);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = basePos + along * (i * 0.75f);
                surface.AddCylinder(p, 0.32f, 0.28f, 0.86f, 8, Quaternion.identity, _binGreen, _binDark);
                surface.AddCylinder(p + Vector3.up * 0.86f, 0.34f, 0.30f, 0.10f, 8, Quaternion.identity, _binDark, _binDark);
            }

            // Bin bags slumped beside them.
            int bags = rng.Range(0, 4);
            for (int i = 0; i < bags; i++)
            {
                Vector3 p = basePos + along * rng.Range(-0.8f, 1.8f) + rng.OnUnitCircleXZ() * 0.35f;
                surface.AddTaperedBox(p + Vector3.up * 0.24f, new Vector3(0.52f, 0.48f, 0.52f), 0.55f, 0.55f,
                    Quaternion.Euler(0f, rng.Range(0f, 90f), 0f), _binDark, _binDark);
            }
        }

        /// <summary>A knot of plastic stools and a folding table: pavement dining.</summary>
        void BuildStoolCluster(MeshBuilder surface, Vector3 basePos, Vector3 along, ref Rng rng)
        {
            Vector3 side = Vector3.Cross(Vector3.up, along);

            int col = _cloth[rng.Range(0, _cloth.Length)];
            surface.AddBox(basePos + Vector3.up * 0.62f, new Vector3(0.72f, 0.06f, 0.72f),
                Quaternion.Euler(0f, rng.Range(0f, 45f), 0f), _canvasWhite, _canvasWhite, _plastic);
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2f + 0.78f;
                Vector3 leg = basePos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.28f;
                surface.AddBox(leg + Vector3.up * 0.31f, new Vector3(0.05f, 0.62f, 0.05f), _plastic);
            }

            int stools = rng.Range(2, 6);
            for (int i = 0; i < stools; i++)
            {
                float a = i / (float)stools * Mathf.PI * 2f;
                Vector3 p = basePos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rng.Range(0.7f, 1.05f);
                int c = i % 2 == 0 ? col : _cloth[rng.Range(0, _cloth.Length)];
                surface.AddBox(p + Vector3.up * 0.20f, new Vector3(0.30f, 0.40f, 0.30f),
                    Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), c, c, c);
            }
        }

        void BuildBollards(MeshBuilder surface, Vector3 basePos, Vector3 along, ref Rng rng)
        {
            int count = rng.Range(3, 7);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = basePos + along * (i * 1.25f);
                surface.AddCylinder(p, 0.09f, 0.07f, 0.72f, 6, Quaternion.identity, _coneWhite, _poleDark);
                surface.AddCylinder(p + Vector3.up * 0.5f, 0.10f, 0.10f, 0.10f, 6, Quaternion.identity,
                    _coneOrange, _coneOrange);
            }
        }

        void BuildConesAndBarrier(MeshBuilder surface, MeshBuilder glow, Vector3 basePos, Vector3 along, ref Rng rng)
        {
            int cones = rng.Range(2, 5);
            for (int i = 0; i < cones; i++)
            {
                Vector3 p = basePos + along * (i * 0.9f) + rng.OnUnitCircleXZ() * 0.2f;
                surface.AddBox(p + Vector3.up * 0.03f, new Vector3(0.36f, 0.06f, 0.36f), _coneOrange);
                surface.AddCylinder(p + Vector3.up * 0.06f, 0.15f, 0.02f, 0.52f, 7, Quaternion.identity,
                    _coneOrange, _coneOrange);
                surface.AddCylinder(p + Vector3.up * 0.26f, 0.10f, 0.10f, 0.08f, 7, Quaternion.identity,
                    _coneWhite, _coneWhite);
            }

            if (rng.Chance(0.4f))
            {
                Vector3 p = basePos + along * (cones * 0.9f + 0.6f);
                surface.AddBox(p + Vector3.up * 0.55f, new Vector3(1.6f, 0.10f, 0.08f),
                    Quaternion.LookRotation(along, Vector3.up), _coneWhite, _coneWhite, _coneWhite);
                for (int s = -1; s <= 1; s += 2)
                    surface.AddCylinder(p + along * (s * 0.7f), 0.05f, 0.05f, 0.6f, 5, Quaternion.identity,
                        _coneOrange, _coneOrange);
                glow.AddCylinder(p + Vector3.up * 0.68f, 0.06f, 0.06f, 0.08f, 6, Quaternion.identity,
                    _lampAmber, _lampAmber);
            }
        }

        // ------------------------------------------------------------------ block interiors

        void BuildCourtyard(CityBlock block, MeshBuilder surface, MeshBuilder glow, ref Rng rng)
        {
            // The middle of the block: rear yards, AC condensers, stacked crates, a lone tree.
            float y = Tuning.CurbHeight;
            float span = Mathf.Min(block.Size.x, block.Size.y) * 0.5f - 14f;
            if (span < 2f) return;

            int clutter = rng.Range(4, 10);
            for (int i = 0; i < clutter; i++)
            {
                Vector3 p = block.Centre + Vector3.up * y
                            + new Vector3(rng.Range(-span, span), 0f, rng.Range(-span, span));
                float roll = rng.Value;

                if (roll < 0.35f)
                {
                    int layers = rng.Range(1, 4);
                    for (int k = 0; k < layers; k++)
                    {
                        int col = _cloth[rng.Range(0, _cloth.Length)];
                        surface.AddBox(p + Vector3.up * (0.2f + k * 0.36f), new Vector3(0.6f, 0.34f, 0.44f),
                            Quaternion.Euler(0f, rng.Range(0f, 60f), 0f), col, col, col);
                    }
                }
                else if (roll < 0.6f)
                {
                    surface.AddBox(p + Vector3.up * 0.35f, new Vector3(0.9f, 0.7f, 0.5f),
                        Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), _metal, _metalDark, _metalDark);
                }
                else if (roll < 0.78f)
                {
                    BuildBins(surface, p, Vector3.right, ref rng);
                }
                else
                {
                    BuildTree(surface, p, ref rng);
                }
            }
        }

        void BuildAlleyDressing(CityBuilder layout, CityBlock block, MeshBuilder surface, MeshBuilder glow, ref Rng rng)
        {
            for (int i = 0; i < layout.Alleys.Count; i++)
            {
                Alley alley = layout.Alleys[i];
                if (alley.BlockIndex != block.Index) continue;

                Vector3 dir = (alley.To - alley.From).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float span = alley.Vertical ? block.Size.y : block.Size.x;

                // Strings of lanterns down the alley: the shortcut should look inviting.
                int strings = Mathf.Max(2, Mathf.FloorToInt(span / 8f));
                for (int s = 0; s < strings; s++)
                {
                    float t = (s + 0.5f) / strings - 0.5f;
                    Vector3 a = block.Centre + dir * (t * span) - side * Tuning.AlleyHalfWidth + Vector3.up * 3.6f;
                    Vector3 b = a + side * (Tuning.AlleyHalfWidth * 2f);
                    surface.AddSaggingCable(a, b, 0.35f, 0.025f, 4, _cable);

                    int lanterns = 3;
                    for (int k = 0; k < lanterns; k++)
                    {
                        float lt = (k + 0.5f) / lanterns;
                        Vector3 p = Vector3.Lerp(a, b, lt) - Vector3.up * (Mathf.Sin(lt * Mathf.PI) * 0.35f + 0.16f);
                        int slot = rng.Chance(0.6f) ? _lanternRed : _lanternWarm;
                        glow.AddCylinder(p - Vector3.up * 0.14f, 0.14f, 0.14f, 0.28f, 8,
                            Quaternion.identity, slot, slot);
                    }
                }

                // Clutter pushed against the alley walls.
                int items = Mathf.FloorToInt(span / 5f);
                for (int k = 0; k < items; k++)
                {
                    float t = (k + 0.5f) / items - 0.5f;
                    float s2 = rng.Chance(0.5f) ? 1f : -1f;
                    Vector3 p = block.Centre + dir * (t * span) + side * (s2 * (Tuning.AlleyHalfWidth - 0.45f));
                    if (rng.Chance(0.5f)) BuildBins(surface, p, dir, ref rng);
                    else
                    {
                        int col = _cloth[rng.Range(0, _cloth.Length)];
                        surface.AddBox(p + Vector3.up * 0.25f, new Vector3(0.5f, 0.5f, 0.7f),
                            Quaternion.LookRotation(dir, Vector3.up), col, col, col);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ intersections

        /// <summary>Traffic lights and signage at one intersection.</summary>
        public void BuildIntersectionProps(CityModel model, RoadNode node, MeshBuilder surface, MeshBuilder glow,
            ref Rng rng, float nightFactor)
        {
            int drivable = 0;
            foreach (int e in node.Edges) if (!model.Edges[e].IsAlley) drivable++;
            if (drivable < 3) return;

            float r = Tuning.RoadHalfWidth;

            foreach (int edgeIndex in node.Edges)
            {
                RoadEdge edge = model.Edges[edgeIndex];
                if (edge.IsAlley) continue;

                Vector3 outward = edge.A == node.Index ? edge.Dir : -edge.Dir;
                Vector3 side = Vector3.Cross(Vector3.up, outward);

                // Post on the near-side corner, arm reaching over the approaching lane.
                Vector3 postBase = node.Position + outward * (r + 1.2f) - side * (r + 1.0f)
                                   + Vector3.up * Tuning.CurbHeight;
                float h = 5.4f;
                surface.AddCylinder(postBase, 0.13f, 0.10f, h, 7, Quaternion.identity, _poleDark, _poleDark);

                Vector3 armA = postBase + Vector3.up * (h - 0.3f);
                Vector3 armB = armA + side * 4.2f;
                surface.AddBeam(armA, armB, 0.09f, _poleDark);

                Vector3 headPos = armB - Vector3.up * 0.55f;
                Quaternion headRot = Quaternion.LookRotation(-outward, Vector3.up);
                surface.AddBox(headPos, new Vector3(0.34f, 1.02f, 0.30f), headRot, _poleDark, _poleDark, _poleDark);

                // Lamps. Which one is lit alternates by axis so the city looks synchronised.
                bool greenAxis = Mathf.Abs(outward.x) > 0.5f;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 lampPos = headPos + Vector3.up * (0.32f - k * 0.32f) - outward * 0.17f;
                    int slot = k == 0 ? _lampRed : k == 1 ? _lampAmber : _lampGreen;
                    bool lit = greenAxis ? k == 2 : k == 0;
                    if (lit)
                        glow.AddCylinder(lampPos, 0.11f, 0.11f, 0.05f, 8,
                            Quaternion.LookRotation(-outward, Vector3.up) * Quaternion.Euler(90f, 0f, 0f), slot, slot);
                    else
                        surface.AddCylinder(lampPos, 0.11f, 0.11f, 0.05f, 8,
                            Quaternion.LookRotation(-outward, Vector3.up) * Quaternion.Euler(90f, 0f, 0f),
                            _poleDark, _poleDark);
                }

                // Street name plate.
                if (rng.Chance(0.5f))
                {
                    Vector3 plate = postBase + Vector3.up * (h - 1.4f) + side * 0.4f;
                    int col = _neon[rng.Range(0, _neon.Length)];
                    surface.AddBox(plate, new Vector3(1.15f, 0.30f, 0.05f),
                        Quaternion.LookRotation(-outward, Vector3.up), col, col, col);
                }
            }
        }
    }
}
