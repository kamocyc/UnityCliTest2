using System.Collections.Generic;
using UnityEngine;
using FormosaExpress.Core;

namespace FormosaExpress.City
{
    /// <summary>
    /// Builds one Taipei-style shophouse per lot: a lit shopfront with an awning and signage,
    /// a few residential floors with balconies, air-conditioners and laundry, then a cluttered
    /// roof. Signage glyphs are drawn as neon strokes rather than textures, which keeps the
    /// whole city on two materials and makes the bloom pass do the heavy lifting.
    /// </summary>
    public sealed class BuildingFactory
    {
        readonly Palette _pal;

        // Pre-registered palette slots. Registering up front (instead of per building) keeps
        // the atlas small enough to stay a single texture.
        struct WallScheme
        {
            public int Top, Side, Dark, Trim, Base;
        }

        readonly WallScheme[] _walls;
        readonly int[] _awnings, _awningDark, _neon, _neonDim, _cloth;
        readonly int _windowLit, _windowWarm, _windowDark, _frameDark, _frameLight;
        readonly int _concrete, _concreteDark, _metal, _metalDark, _tank, _tankDark;
        readonly int _shopInterior, _shopInteriorHot, _doorDark, _glassDark, _signDark;
        readonly int _railing, _pipe, _plantGreen, _plantDark, _lanternRed;
        readonly int _spillWarm;

        /// <summary>Additive sink for the light each shopfront spills onto the pavement.</summary>
        MeshBuilder _additive;

        public BuildingFactory(Palette palette)
        {
            _pal = palette;
            // Very dim on purpose: the spill is drawn as several nested rectangles, and being an
            // additive material they accumulate towards the shopfront into a soft gradient.
            _spillWarm = _pal.Add(new Color(0.048f, 0.033f, 0.016f));

            var rng = new Rng(90210);
            var schemes = new List<WallScheme>();
            foreach (Color baseColour in Art.BuildingWalls)
            {
                for (int v = 0; v < 4; v++)
                {
                    Color c = rng.Vary(baseColour, 0.015f, 0.06f, 0.09f);
                    schemes.Add(new WallScheme
                    {
                        Top = _pal.AddShaded(c, 1.06f),
                        Side = _pal.AddShaded(c, 0.78f),
                        Dark = _pal.AddShaded(c, 0.58f),
                        Trim = _pal.AddShaded(c, 1.22f),
                        Base = _pal.AddShaded(c, 0.66f)
                    });
                }
            }

            _walls = schemes.ToArray();

            _awnings = new int[Art.AwningColours.Length];
            _awningDark = new int[Art.AwningColours.Length];
            for (int i = 0; i < Art.AwningColours.Length; i++)
            {
                _awnings[i] = _pal.Add(Art.AwningColours[i]);
                _awningDark[i] = _pal.AddShaded(Art.AwningColours[i], 0.62f);
            }

            _neon = new int[Art.NeonColours.Length];
            _neonDim = new int[Art.NeonColours.Length];
            for (int i = 0; i < Art.NeonColours.Length; i++)
            {
                _neon[i] = _pal.Add(Art.NeonColours[i]);
                _neonDim[i] = _pal.AddShaded(Art.NeonColours[i], 0.35f);
            }

            _cloth = new int[Art.ClothColours.Length];
            for (int i = 0; i < Art.ClothColours.Length; i++) _cloth[i] = _pal.Add(Art.ClothColours[i]);

            _windowLit = _pal.Add(Art.WindowLit);
            _windowWarm = _pal.Add(new Color(1.00f, 0.72f, 0.42f));
            _windowDark = _pal.Add(Art.WindowDark);
            _frameDark = _pal.Add(new Color(0.13f, 0.13f, 0.15f));
            _frameLight = _pal.Add(new Color(0.80f, 0.79f, 0.75f));
            _concrete = _pal.Add(new Color(0.55f, 0.54f, 0.52f));
            _concreteDark = _pal.Add(new Color(0.34f, 0.34f, 0.33f));
            _metal = _pal.Add(new Color(0.62f, 0.64f, 0.66f));
            _metalDark = _pal.Add(new Color(0.36f, 0.38f, 0.40f));
            _tank = _pal.Add(new Color(0.72f, 0.74f, 0.76f));
            _tankDark = _pal.Add(new Color(0.46f, 0.48f, 0.50f));
            // The shopfront is a big panel: at full brightness it blows out and swallows all the
            // interior detail, so it sits well below the neon.
            _shopInterior = _pal.Add(new Color(0.62f, 0.44f, 0.24f));
            _shopInteriorHot = _pal.Add(new Color(0.72f, 0.56f, 0.34f));
            _doorDark = _pal.Add(new Color(0.10f, 0.10f, 0.12f));
            _glassDark = _pal.Add(new Color(0.18f, 0.22f, 0.26f));
            _signDark = _pal.Add(new Color(0.09f, 0.09f, 0.11f));
            _railing = _pal.Add(new Color(0.28f, 0.30f, 0.32f));
            _pipe = _pal.Add(new Color(0.44f, 0.42f, 0.40f));
            _plantGreen = _pal.Add(new Color(0.22f, 0.44f, 0.24f));
            _plantDark = _pal.Add(new Color(0.14f, 0.30f, 0.17f));
            _lanternRed = _pal.Add(new Color(0.92f, 0.22f, 0.18f));
        }

        /// <summary>
        /// Emits one shophouse. <paramref name="surface"/> takes lit geometry and
        /// <paramref name="glow"/> takes everything that should bloom.
        /// </summary>
        public float Build(BuildingLot lot, MeshBuilder surface, MeshBuilder glow, MeshBuilder additive,
            ref Rng rng, float nightFactor)
        {
            _additive = additive;
            WallScheme wall = _walls[rng.Range(0, _walls.Length)];
            Vector3 fwd = lot.Forward;
            Vector3 right = lot.Right;
            Vector3 up = Vector3.up;
            Quaternion rot = Quaternion.LookRotation(fwd, up);

            float w = lot.Width;
            float depth = lot.Depth;
            int floors = rng.Range(2, 6);
            const float groundHeight = 3.9f;
            float floorHeight = rng.Range(2.95f, 3.25f);
            float height = groundHeight + floors * floorHeight;

            Vector3 frontBottom = lot.FrontCentre;                    // on the pavement, street side
            Vector3 bodyCentre = frontBottom - fwd * (depth * 0.5f) + up * (height * 0.5f);

            // ---------------------------------------------------------- main mass
            surface.AddBox(bodyCentre, new Vector3(w, height, depth), rot, wall.Top, wall.Side, wall.Dark);

            // Plinth: a slightly wider, darker base grounds the building.
            surface.AddBox(frontBottom - fwd * (depth * 0.5f) + up * 0.28f,
                new Vector3(w + 0.14f, 0.56f, depth + 0.10f), rot, wall.Base, wall.Base, wall.Dark);

            // A floor divider band between the shopfront and the flats above.
            surface.AddBox(frontBottom + fwd * 0.10f + up * (groundHeight + 0.14f),
                new Vector3(w + 0.10f, 0.28f, 0.28f), rot, wall.Trim, wall.Trim, wall.Dark);

            BuildShopfront(lot, surface, glow, ref rng, wall, groundHeight, nightFactor);
            BuildUpperFloors(lot, surface, glow, ref rng, wall, groundHeight, floorHeight, floors, nightFactor);
            BuildFlanks(lot, surface, glow, ref rng, wall, groundHeight, height);
            BuildRoof(lot, surface, glow, ref rng, wall, height);
            BuildSignage(lot, surface, glow, ref rng, groundHeight, height, nightFactor);

            return height;
        }

        /// <summary>
        /// Dresses the side walls. Most are hidden by the neighbour, but the ones at the end of
        /// a row are fully exposed, and a bare five-storey slab is the single most obvious tell
        /// that a city was generated. Painted adverts, pipework and vents fix that cheaply.
        /// </summary>
        void BuildFlanks(BuildingLot lot, MeshBuilder surface, MeshBuilder glow, ref Rng rng,
            WallScheme wall, float groundHeight, float height)
        {
            Vector3 fwd = lot.Forward;
            Vector3 right = lot.Right;
            Vector3 centre = lot.FrontCentre - fwd * (lot.Depth * 0.5f);

            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 outward = right * s;
                Vector3 planeCentre = centre + outward * (lot.Width * 0.5f);
                Quaternion rot = Quaternion.LookRotation(outward, Vector3.up);

                // A large hand-painted advert, faded into the render.
                if (rng.Chance(0.55f))
                {
                    int adColour = rng.Chance(0.5f)
                        ? _pal.AddShaded(Art.AwningColours[rng.Range(0, Art.AwningColours.Length)], 0.55f)
                        : _pal.AddShaded(Art.NeonColours[rng.Range(0, Art.NeonColours.Length)], 0.32f);

                    float adHeight = Mathf.Min(height - groundHeight - 1.5f, rng.Range(4f, 8f));
                    float adWidth = lot.Depth * rng.Range(0.5f, 0.78f);
                    float adY = rng.Range(groundHeight + 1f, Mathf.Max(groundHeight + 1.2f, height - adHeight - 1f));
                    Vector3 adCentre = planeCentre + outward * 0.02f + Vector3.up * (adY + adHeight * 0.5f);

                    surface.AddBox(adCentre, new Vector3(adWidth, adHeight, 0.04f), rot,
                        adColour, adColour, adColour);

                    // Faux lettering across the advert.
                    AddGlyphColumn(surface, adCentre + outward * 0.04f,
                        Vector3.Cross(Vector3.up, outward), Vector3.up,
                        adWidth * 0.5f, adHeight * 0.82f, rng.Range(2, 5), wall.Dark, ref rng);
                }

                // Soil pipes running the full height.
                int pipes = rng.Range(1, 4);
                for (int p = 0; p < pipes; p++)
                {
                    float t = (p + 0.5f) / pipes - 0.5f;
                    Vector3 a = planeCentre + outward * 0.09f + Vector3.Cross(Vector3.up, outward) * (t * lot.Depth * 0.8f);
                    surface.AddBeam(a + Vector3.up * 0.3f, a + Vector3.up * (height - 0.4f), 0.11f, _pipe);
                }

                // Extract vents and a few condensers.
                int vents = rng.Range(0, 4);
                for (int v = 0; v < vents; v++)
                {
                    Vector3 p = planeCentre + outward * 0.22f
                                + Vector3.Cross(Vector3.up, outward) * rng.Range(-lot.Depth * 0.35f, lot.Depth * 0.35f)
                                + Vector3.up * rng.Range(groundHeight, height - 1f);
                    surface.AddBox(p, new Vector3(0.62f, 0.46f, 0.42f), rot, _metal, _metalDark, _metalDark);
                }

                // An external staircase on the odd one.
                if (rng.Chance(0.18f) && height > 12f)
                {
                    int steps = Mathf.FloorToInt((height - groundHeight) / 0.6f);
                    for (int k = 0; k < steps; k++)
                    {
                        float y = groundHeight + k * 0.6f;
                        float along = (k % 8) * 0.5f - 2f;
                        Vector3 p = planeCentre + outward * 0.45f
                                    + Vector3.Cross(Vector3.up, outward) * along + Vector3.up * y;
                        surface.AddBox(p, new Vector3(0.9f, 0.07f, 0.55f), rot, _railing, _railing, _railing);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ shopfront

        void BuildShopfront(BuildingLot lot, MeshBuilder surface, MeshBuilder glow, ref Rng rng,
            WallScheme wall, float groundHeight, float nightFactor)
        {
            Vector3 fwd = lot.Forward;
            Vector3 right = lot.Right;
            Vector3 front = lot.FrontCentre;
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

            float w = lot.Width;
            float openW = w * 0.80f;
            bool shuttered = rng.Chance(0.16f);

            // The glazed opening, pushed a few centimetres proud of the wall so it never z-fights.
            Vector3 glassCentre = front + fwd * 0.03f + Vector3.up * 1.62f;
            if (shuttered)
            {
                surface.AddBox(glassCentre, new Vector3(openW, 2.5f, 0.10f), rot, _metalDark, _metalDark, _metalDark);
                for (int i = 0; i < 7; i++)
                {
                    Vector3 p = glassCentre + fwd * 0.06f + Vector3.up * (-1.05f + i * 0.35f);
                    surface.AddBox(p, new Vector3(openW - 0.1f, 0.10f, 0.04f), rot, _metal, _metal, _metal);
                }
            }
            else
            {
                // Warm interior, brightest at eye level and dimmer towards the ceiling, so the
                // shopfront reads as a lit room rather than one flat glowing rectangle.
                int interior = rng.Chance(0.45f) ? _shopInteriorHot : _shopInterior;
                glow.AddBox(glassCentre - Vector3.up * 0.62f, new Vector3(openW, 1.26f, 0.08f), rot,
                    _shopInteriorHot, _shopInteriorHot, _shopInteriorHot);
                glow.AddBox(glassCentre + Vector3.up * 0.63f, new Vector3(openW, 1.24f, 0.08f), rot,
                    interior, interior, interior);

                // Dark soffit above the opening, and a shelf line across the middle.
                surface.AddBox(front + fwd * 0.10f + Vector3.up * 2.94f,
                    new Vector3(openW + 0.12f, 0.22f, 0.22f), rot, _signDark, _signDark, _signDark);
                surface.AddBox(front + fwd * 0.11f + Vector3.up * 2.16f,
                    new Vector3(openW * 0.96f, 0.07f, 0.10f), rot, _frameDark, _frameDark, _frameDark);

                // Goods on the shelf.
                int goods = Mathf.Max(2, Mathf.FloorToInt(openW / 0.6f));
                for (int i = 0; i < goods; i++)
                {
                    if (!rng.Chance(0.7f)) continue;
                    float t = (i + 0.5f) / goods - 0.5f;
                    int col = _cloth[rng.Range(0, _cloth.Length)];
                    surface.AddBox(front + fwd * 0.12f + right * (t * openW * 0.94f) + Vector3.up * 2.32f,
                        new Vector3(0.20f, 0.24f, 0.08f), rot, col, col, col);
                }

                // Counter and stools inside.
                surface.AddBox(front - fwd * 0.55f + Vector3.up * 0.48f,
                    new Vector3(openW * 0.9f, 0.96f, 0.5f), rot, _frameDark, _frameDark, _frameDark);
                surface.AddBox(front - fwd * 0.52f + Vector3.up * 0.99f,
                    new Vector3(openW * 0.94f, 0.06f, 0.58f), rot, _frameLight, _frameLight, _frameLight);

                int stools = Mathf.Max(1, Mathf.FloorToInt(openW / 1.5f));
                for (int i = 0; i < stools; i++)
                {
                    float t = (i + 0.5f) / stools - 0.5f;
                    Vector3 p = front - fwd * 0.20f + right * (t * openW * 0.82f);
                    surface.AddCylinder(p, 0.16f, 0.62f, 6, _frameDark);
                }

                // Mullions across the glass.
                int bays = Mathf.Max(2, Mathf.RoundToInt(openW / 1.6f));
                for (int i = 1; i < bays; i++)
                {
                    float t = i / (float)bays - 0.5f;
                    surface.AddBox(front + fwd * 0.08f + right * (t * openW) + Vector3.up * 1.62f,
                        new Vector3(0.09f, 2.5f, 0.12f), rot, _frameDark, _frameDark, _frameDark);
                }

                // The light the shop spills onto the pavement. Two overlapping rectangles give a
                // soft falloff without needing a real light per shopfront.
                if (_additive != null)
                {
                    // Nested rectangles, each pulled back towards the shop, so the pavement is
                    // brightest right against the glass and fades out towards the kerb.
                    const int layers = 4;
                    for (int i = 0; i < layers; i++)
                    {
                        float shrink = 1f - i * 0.22f;
                        float depth = 3.3f * shrink;
                        Vector3 spill = front + fwd * (depth * 0.5f + 0.05f)
                                        + Vector3.up * (Tuning.CurbHeight + 0.02f + i * 0.004f);
                        AddSpill(spill, fwd, right, openW * (1.12f * shrink), depth, _spillWarm);
                    }
                }
            }

            // Door pier on one side.
            float doorSide = rng.Chance(0.5f) ? 1f : -1f;
            Vector3 doorCentre = front + fwd * 0.04f + right * (doorSide * (w * 0.5f - 0.55f)) + Vector3.up * 1.05f;
            surface.AddBox(doorCentre, new Vector3(0.85f, 2.1f, 0.10f), rot, _doorDark, _doorDark, _doorDark);
            surface.AddBox(doorCentre + Vector3.up * 1.14f, new Vector3(0.95f, 0.18f, 0.16f), rot,
                wall.Trim, wall.Trim, wall.Dark);

            // Step up to the door.
            surface.AddBox(front + fwd * 0.30f + Vector3.up * 0.07f,
                new Vector3(w * 0.9f, 0.14f, 0.6f), rot, _concrete, _concreteDark, _concreteDark);

            // Corner piers framing the whole shopfront.
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = front + fwd * 0.05f + right * (s * (w * 0.5f - 0.14f)) + Vector3.up * (groundHeight * 0.5f);
                surface.AddBox(p, new Vector3(0.28f, groundHeight, 0.20f), rot, wall.Trim, wall.Base, wall.Dark);
            }

            BuildAwning(lot, surface, ref rng, groundHeight);

            // Plastic stools and crates spilling onto the pavement.
            if (rng.Chance(0.55f))
            {
                int count = rng.Range(2, 5);
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = front + fwd * rng.Range(0.9f, 1.7f) + right * rng.Range(-w * 0.4f, w * 0.4f);
                    int c = _cloth[rng.Range(0, _cloth.Length)];
                    surface.AddBox(p + Vector3.up * 0.21f, new Vector3(0.34f, 0.42f, 0.34f),
                        Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), c, c, c);
                }
            }

            // A hanging red lantern by the door, always lit.
            if (rng.Chance(0.30f))
            {
                Vector3 p = front + fwd * 0.45f + right * (doorSide * (lot.Width * 0.5f - 0.5f)) + Vector3.up * 2.65f;
                surface.AddBeam(p + Vector3.up * 0.45f, p + Vector3.up * 0.18f, 0.03f, _metalDark);
                glow.AddCylinder(p - Vector3.up * 0.18f, 0.17f, 0.17f, 0.36f, 8, Quaternion.identity,
                    _lanternRed, _lanternRed);
            }
        }

        void AddSpill(Vector3 centre, Vector3 forward, Vector3 right, float width, float depth, int slot)
        {
            Vector3 f = forward * (depth * 0.5f);
            Vector3 s = right * (width * 0.5f);
            _additive.AddQuad(centre - f - s, centre + f - s, centre + f + s, centre - f + s, Vector3.up, slot);
        }

        void BuildAwning(BuildingLot lot, MeshBuilder surface, ref Rng rng, float groundHeight)
        {
            if (!rng.Chance(0.82f)) return;

            Vector3 fwd = lot.Forward;
            Vector3 right = lot.Right;
            Vector3 front = lot.FrontCentre;

            int colourIndex = rng.Range(0, _awnings.Length);
            int top = _awnings[colourIndex];
            int side = _awningDark[colourIndex];

            float project = rng.Range(1.35f, 2.15f);
            float y = groundHeight - rng.Range(0.55f, 0.95f);
            float w = lot.Width + 0.2f;

            // A tilted slab: high at the wall, low at the street edge.
            Vector3 inner = front + Vector3.up * (y + 0.42f);
            Vector3 outer = front + fwd * project + Vector3.up * y;

            Vector3 hw = right * (w * 0.5f);
            surface.AddQuad(inner - hw, outer - hw, outer + hw, inner + hw, top);
            surface.AddQuad(inner + hw, outer + hw, outer - hw, inner - hw, side);

            // Valance hanging off the front edge.
            Vector3 valanceTop = outer;
            Vector3 valanceBottom = outer - Vector3.up * 0.32f;
            surface.AddQuad(valanceTop - hw, valanceBottom - hw, valanceBottom + hw, valanceTop + hw, side);
            surface.AddQuad(valanceTop + hw, valanceBottom + hw, valanceBottom - hw, valanceTop - hw, top);

            // Support struts.
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 a = front + right * (s * w * 0.46f) + Vector3.up * (y + 0.42f);
                Vector3 b = a + fwd * project - Vector3.up * 0.42f;
                surface.AddBeam(a, b, 0.055f, _metalDark);
            }
        }

        // ------------------------------------------------------------------ upper floors

        void BuildUpperFloors(BuildingLot lot, MeshBuilder surface, MeshBuilder glow, ref Rng rng,
            WallScheme wall, float groundHeight, float floorHeight, int floors, float nightFactor)
        {
            Vector3 fwd = lot.Forward;
            Vector3 right = lot.Right;
            Vector3 front = lot.FrontCentre;
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

            float w = lot.Width;
            int windowsPerFloor = Mathf.Max(1, Mathf.FloorToInt(w / 2.3f));
            float litChance = Mathf.Lerp(0.30f, 0.78f, nightFactor);

            for (int f = 0; f < floors; f++)
            {
                float baseY = groundHeight + f * floorHeight;
                bool balcony = rng.Chance(0.45f);

                for (int i = 0; i < windowsPerFloor; i++)
                {
                    float t = (i + 0.5f) / windowsPerFloor - 0.5f;
                    Vector3 centre = front + right * (t * w * 0.92f) + Vector3.up * (baseY + floorHeight * 0.55f);

                    float ww = Mathf.Min(1.55f, w / windowsPerFloor * 0.72f);
                    float wh = floorHeight * 0.56f;

                    bool lit = rng.Chance(litChance);
                    int glassSlot = lit ? (rng.Chance(0.4f) ? _windowWarm : _windowLit) : _windowDark;

                    if (lit)
                        glow.AddBox(centre + fwd * 0.05f, new Vector3(ww, wh, 0.06f), rot, glassSlot, glassSlot, glassSlot);
                    else
                        surface.AddBox(centre + fwd * 0.05f, new Vector3(ww, wh, 0.06f), rot, _glassDark, _glassDark, _glassDark);

                    // Frame.
                    surface.AddBox(centre + fwd * 0.02f, new Vector3(ww + 0.16f, wh + 0.16f, 0.07f), rot,
                        _frameLight, _frameLight, _frameLight);

                    // Window bars, very common on Taipei facades.
                    if (rng.Chance(0.55f))
                    {
                        int bars = 3;
                        for (int b = 0; b < bars; b++)
                        {
                            float bt = (b + 1) / (float)(bars + 1) - 0.5f;
                            surface.AddBox(centre + fwd * 0.10f + right * (bt * ww),
                                new Vector3(0.045f, wh, 0.045f), rot, _metalDark, _metalDark, _metalDark);
                        }
                    }

                    // Air-conditioner hung under the sill.
                    if (rng.Chance(0.42f))
                    {
                        Vector3 ac = centre + fwd * 0.30f - Vector3.up * (wh * 0.5f + 0.34f);
                        surface.AddBox(ac, new Vector3(0.72f, 0.46f, 0.44f), rot, _metal, _metalDark, _metalDark);
                        surface.AddBox(ac + fwd * 0.24f, new Vector3(0.52f, 0.34f, 0.05f), rot,
                            _metalDark, _metalDark, _metalDark);
                    }
                }

                if (balcony)
                {
                    float by = baseY + 0.12f;
                    float project = 0.72f;
                    Vector3 ledgeCentre = front + fwd * (project * 0.5f) + Vector3.up * by;
                    surface.AddBox(ledgeCentre, new Vector3(w * 0.94f, 0.14f, project), rot,
                        _concrete, _concreteDark, _concreteDark);

                    // Railing.
                    Vector3 railTop = front + fwd * project + Vector3.up * (by + 0.92f);
                    surface.AddBox(railTop, new Vector3(w * 0.94f, 0.06f, 0.06f), rot, _railing, _railing, _railing);
                    int posts = Mathf.Max(3, Mathf.FloorToInt(w / 0.55f));
                    for (int p = 0; p < posts; p++)
                    {
                        float pt = (p + 0.5f) / posts - 0.5f;
                        surface.AddBox(front + fwd * project + right * (pt * w * 0.94f) + Vector3.up * (by + 0.48f),
                            new Vector3(0.035f, 0.9f, 0.035f), rot, _railing, _railing, _railing);
                    }

                    // Laundry pole with a couple of shirts.
                    if (rng.Chance(0.55f))
                    {
                        Vector3 poleA = front + fwd * (project + 0.2f) + right * (-w * 0.42f) + Vector3.up * (by + 1.35f);
                        Vector3 poleB = poleA + right * (w * 0.84f);
                        surface.AddBeam(poleA, poleB, 0.035f, _metal);

                        int shirts = rng.Range(1, 4);
                        for (int s = 0; s < shirts; s++)
                        {
                            float st = (s + 0.5f) / shirts;
                            Vector3 hang = Vector3.Lerp(poleA, poleB, st) - Vector3.up * 0.36f;
                            int c = _cloth[rng.Range(0, _cloth.Length)];
                            surface.AddDoubleSidedQuad(hang, right * 0.26f, Vector3.up * 0.34f, c);
                        }
                    }

                    // A potted plant.
                    if (rng.Chance(0.4f))
                    {
                        Vector3 pot = front + fwd * (project * 0.7f) + right * rng.Range(-w * 0.35f, w * 0.35f)
                                      + Vector3.up * (by + 0.2f);
                        surface.AddCylinder(pot, 0.17f, 0.14f, 0.24f, 6, Quaternion.identity, _pipe, _pipe);
                        surface.AddBox(pot + Vector3.up * 0.42f, new Vector3(0.34f, 0.42f, 0.34f),
                            Quaternion.Euler(0f, rng.Range(0f, 90f), 0f), _plantGreen, _plantDark, _plantDark);
                    }
                }

                // Drain pipe running down one side of the facade.
                if (f == 0 && rng.Chance(0.5f))
                {
                    float s = rng.Chance(0.5f) ? 1f : -1f;
                    Vector3 a = front + fwd * 0.14f + right * (s * (w * 0.5f - 0.18f)) + Vector3.up * groundHeight;
                    Vector3 b = a + Vector3.up * (floors * floorHeight);
                    surface.AddBeam(a, b, 0.13f, _pipe);
                }
            }
        }

        // ------------------------------------------------------------------ roof

        void BuildRoof(BuildingLot lot, MeshBuilder surface, MeshBuilder glow, ref Rng rng,
            WallScheme wall, float height)
        {
            Vector3 fwd = lot.Forward;
            Vector3 right = lot.Right;
            Vector3 roofCentre = lot.FrontCentre - fwd * (lot.Depth * 0.5f) + Vector3.up * height;
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

            float w = lot.Width;
            float d = lot.Depth;

            // Parapet all the way round.
            surface.AddBox(roofCentre + fwd * (d * 0.5f) + Vector3.up * 0.28f,
                new Vector3(w, 0.56f, 0.18f), rot, wall.Trim, wall.Side, wall.Dark);
            surface.AddBox(roofCentre - fwd * (d * 0.5f) + Vector3.up * 0.28f,
                new Vector3(w, 0.56f, 0.18f), rot, wall.Trim, wall.Side, wall.Dark);
            for (int s = -1; s <= 1; s += 2)
                surface.AddBox(roofCentre + right * (s * w * 0.5f) + Vector3.up * 0.28f,
                    new Vector3(0.18f, 0.56f, d), rot, wall.Trim, wall.Side, wall.Dark);

            // Stainless water tanks: the signature Taipei rooftop silhouette.
            int tanks = rng.Range(1, 4);
            for (int i = 0; i < tanks; i++)
            {
                Vector3 p = roofCentre
                            + right * rng.Range(-w * 0.3f, w * 0.3f)
                            - fwd * rng.Range(-d * 0.25f, d * 0.3f)
                            + Vector3.up * 0.06f;
                float r = rng.Range(0.42f, 0.62f);
                surface.AddCylinder(p, r, r, rng.Range(0.9f, 1.4f), 10, Quaternion.identity, _tank, _tankDark);
                surface.AddCylinder(p + Vector3.up * 0.06f, r * 0.35f, 0.16f, 8, _tankDark);
            }

            // Stair head / utility shed.
            if (rng.Chance(0.55f))
            {
                Vector3 p = roofCentre - fwd * (d * 0.28f) + right * rng.Range(-w * 0.2f, w * 0.2f);
                float sh = rng.Range(1.7f, 2.4f);
                surface.AddBox(p + Vector3.up * (sh * 0.5f), new Vector3(rng.Range(1.8f, 2.6f), sh, rng.Range(1.8f, 2.4f)),
                    rot, wall.Base, wall.Dark, wall.Dark);
            }

            // Aerials and a satellite dish.
            int masts = rng.Range(0, 3);
            for (int i = 0; i < masts; i++)
            {
                Vector3 p = roofCentre + right * rng.Range(-w * 0.4f, w * 0.4f) - fwd * rng.Range(-d * 0.3f, d * 0.3f);
                float h = rng.Range(1.6f, 4.2f);
                surface.AddBeam(p, p + Vector3.up * h, 0.05f, _metalDark);
                for (int c = 0; c < 3; c++)
                {
                    float cy = h * (0.4f + c * 0.2f);
                    surface.AddBeam(p + Vector3.up * cy - right * 0.4f, p + Vector3.up * cy + right * 0.4f, 0.03f, _metalDark);
                }
            }

            if (rng.Chance(0.3f))
            {
                Vector3 p = roofCentre + right * rng.Range(-w * 0.3f, w * 0.3f) + fwd * (d * 0.3f) + Vector3.up * 0.7f;
                surface.AddCylinder(p, 0.55f, 0.12f, 0.30f, 10,
                    Quaternion.Euler(52f, rng.Range(0f, 360f), 0f), _frameLight, _frameLight);
            }

            // Rooftop water tower on a frame, on the tallest buildings.
            if (height > 16f && rng.Chance(0.35f))
            {
                Vector3 p = roofCentre - fwd * (d * 0.15f);
                for (int lx = -1; lx <= 1; lx += 2)
                for (int lz = -1; lz <= 1; lz += 2)
                    surface.AddBeam(p + right * (lx * 0.7f) + fwd * (lz * 0.7f),
                        p + right * (lx * 0.7f) + fwd * (lz * 0.7f) + Vector3.up * 2.2f, 0.09f, _metalDark);

                surface.AddCylinder(p + Vector3.up * 2.2f, 1.05f, 1.05f, 1.4f, 12, Quaternion.identity, _tank, _tankDark);
            }
        }

        // ------------------------------------------------------------------ signage

        void BuildSignage(BuildingLot lot, MeshBuilder surface, MeshBuilder glow, ref Rng rng,
            float groundHeight, float height, float nightFactor)
        {
            Vector3 fwd = lot.Forward;
            Vector3 right = lot.Right;
            Vector3 front = lot.FrontCentre;
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
            float w = lot.Width;

            int neonIndex = rng.Range(0, _neon.Length);
            int neonSlot = _neon[neonIndex];

            // 1. Fascia sign above the shopfront: a backlit box with dark glyphs.
            {
                float y = groundHeight + 0.62f;
                float signH = 0.86f;
                Vector3 centre = front + fwd * 0.16f + Vector3.up * y;
                bool backlit = rng.Chance(0.62f);

                if (backlit)
                {
                    glow.AddBox(centre, new Vector3(w * 0.92f, signH, 0.14f), rot, neonSlot, neonSlot, neonSlot);
                    AddGlyphRow(surface, centre + fwd * 0.10f, right, Vector3.up, w * 0.86f, signH * 0.66f,
                        rng.Range(2, 5), _signDark, ref rng);
                }
                else
                {
                    surface.AddBox(centre, new Vector3(w * 0.92f, signH, 0.14f), rot, _signDark, _signDark, _signDark);
                    AddGlyphRow(glow, centre + fwd * 0.10f, right, Vector3.up, w * 0.86f, signH * 0.66f,
                        rng.Range(2, 5), neonSlot, ref rng);
                }
            }

            // 2. Vertical projecting sign: the thing that makes these streets read as Taipei.
            if (rng.Chance(0.55f) && height > 9f)
            {
                int vIndex = rng.Range(0, _neon.Length);
                int vSlot = _neon[vIndex];
                float signW = rng.Range(0.85f, 1.15f);
                float signH = rng.Range(2.8f, 5.2f);
                float baseY = rng.Range(groundHeight + 1.9f, Mathf.Max(groundHeight + 2.2f, height - signH - 1.2f));
                float project = rng.Range(1.1f, 1.9f);
                float side = rng.Chance(0.5f) ? 1f : -1f;

                Vector3 anchor = front + right * (side * (w * 0.5f - signW * 0.6f)) + Vector3.up * (baseY + signH * 0.5f);
                Vector3 centre = anchor + fwd * project;

                // Support arm back to the wall.
                surface.AddBeam(anchor, centre, 0.07f, _metalDark);
                surface.AddBeam(anchor + Vector3.up * (signH * 0.35f), centre + Vector3.up * (signH * 0.35f), 0.05f, _metalDark);

                // The sign is a thin slab whose broad faces point along the street.
                Quaternion signRot = Quaternion.LookRotation(right * side, Vector3.up);
                bool backlit = rng.Chance(0.7f);
                int bodySlot = backlit ? vSlot : _signDark;
                MeshBuilder bodyBuilder = backlit ? glow : surface;
                bodyBuilder.AddBox(centre, new Vector3(signW, signH, 0.16f), signRot, bodySlot, bodySlot, bodySlot);

                // Frame edge.
                surface.AddBox(centre, new Vector3(signW + 0.10f, signH + 0.10f, 0.10f), signRot,
                    _signDark, _signDark, _signDark);

                int glyphCount = Mathf.Clamp(Mathf.FloorToInt(signH / 0.95f), 2, 5);
                MeshBuilder glyphBuilder = backlit ? surface : glow;
                int glyphSlot = backlit ? _signDark : vSlot;

                // Glyphs stack vertically and appear on both broad faces.
                for (int face = -1; face <= 1; face += 2)
                {
                    Vector3 outward = right * side * face;
                    Vector3 planeCentre = centre + outward * 0.11f;
                    Vector3 glyphRight = Vector3.Cross(Vector3.up, outward);
                    AddGlyphColumn(glyphBuilder, planeCentre, glyphRight, Vector3.up, signW * 0.72f, signH * 0.88f,
                        glyphCount, glyphSlot, ref rng);
                }
            }

            // 3. Horizontal projecting blade sign.
            if (rng.Chance(0.34f))
            {
                int hIndex = rng.Range(0, _neon.Length);
                int hSlot = _neon[hIndex];
                float signH = rng.Range(0.7f, 1.0f);
                float signW = rng.Range(2.0f, 3.2f);
                float y = rng.Range(groundHeight + 1.6f, Mathf.Max(groundHeight + 2.0f, height - 2f));
                float project = rng.Range(1.4f, 2.3f);

                Vector3 centre = front + fwd * project + Vector3.up * y;
                surface.AddBeam(front + Vector3.up * y, centre, 0.06f, _metalDark);

                Quaternion signRot = Quaternion.LookRotation(right, Vector3.up);
                glow.AddBox(centre, new Vector3(signW, signH, 0.12f), signRot, hSlot, hSlot, hSlot);
                surface.AddBox(centre, new Vector3(signW + 0.08f, signH + 0.08f, 0.08f), signRot,
                    _signDark, _signDark, _signDark);

                for (int face = -1; face <= 1; face += 2)
                {
                    Vector3 outward = right * face;
                    Vector3 planeCentre = centre + outward * 0.09f;
                    Vector3 glyphRight = Vector3.Cross(Vector3.up, outward);
                    AddGlyphRow(surface, planeCentre, glyphRight, Vector3.up, signW * 0.84f, signH * 0.62f,
                        rng.Range(2, 5), _signDark, ref rng);
                }
            }

            // 4. Neon tube outline traced around the shopfront opening.
            if (rng.Chance(0.30f))
            {
                int tubeSlot = _neon[rng.Range(0, _neon.Length)];
                float y0 = 0.5f, y1 = groundHeight - 0.7f;
                float hw = w * 0.44f;
                Vector3 o = front + fwd * 0.13f;

                glow.AddBox(o + Vector3.up * y1, new Vector3(hw * 2f, 0.075f, 0.075f), rot, tubeSlot, tubeSlot, tubeSlot);
                glow.AddBox(o + Vector3.up * y0, new Vector3(hw * 2f, 0.075f, 0.075f), rot, tubeSlot, tubeSlot, tubeSlot);
                for (int s = -1; s <= 1; s += 2)
                    glow.AddBox(o + right * (s * hw) + Vector3.up * ((y0 + y1) * 0.5f),
                        new Vector3(0.075f, y1 - y0, 0.075f), rot, tubeSlot, tubeSlot, tubeSlot);
            }

            // 5. A hanging cloth banner: cheap, and it breaks up the neon.
            if (rng.Chance(0.22f))
            {
                int c = _cloth[rng.Range(0, _cloth.Length)];
                float y = groundHeight + rng.Range(1.4f, 3.0f);
                Vector3 centre = front + fwd * 0.22f + Vector3.up * y;
                surface.AddDoubleSidedQuad(centre, right * (w * 0.36f), Vector3.up * rng.Range(0.5f, 0.9f), c);
            }
        }

        // ------------------------------------------------------------------ faux glyphs

        /// <summary>
        /// Draws <paramref name="count"/> abstract characters in a row. They are not real
        /// hanzi, but the stroke density and grid reads correctly as East Asian signage at
        /// gameplay distance, and it costs no font atlas.
        /// </summary>
        void AddGlyphRow(MeshBuilder mb, Vector3 centre, Vector3 right, Vector3 up,
            float totalWidth, float glyphHeight, int count, int slot, ref Rng rng)
        {
            count = Mathf.Clamp(count, 1, 6);
            float cell = Mathf.Min(totalWidth / count, glyphHeight);
            float usedWidth = cell * count;

            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count - 0.5f;
                Vector3 c = centre + right * (t * usedWidth);
                AddGlyph(mb, c, right, up, cell * 0.82f, slot, ref rng);
            }
        }

        void AddGlyphColumn(MeshBuilder mb, Vector3 centre, Vector3 right, Vector3 up,
            float glyphWidth, float totalHeight, int count, int slot, ref Rng rng)
        {
            count = Mathf.Clamp(count, 1, 6);
            float cell = Mathf.Min(totalHeight / count, glyphWidth);
            float usedHeight = cell * count;

            for (int i = 0; i < count; i++)
            {
                // Top to bottom, the way vertical signage reads.
                float t = 0.5f - (i + 0.5f) / count;
                Vector3 c = centre + up * (t * usedHeight);
                AddGlyph(mb, c, right, up, cell * 0.82f, slot, ref rng);
            }
        }

        void AddGlyph(MeshBuilder mb, Vector3 centre, Vector3 right, Vector3 up, float size, int slot, ref Rng rng)
        {
            float thick = Mathf.Max(0.035f, size * 0.115f);
            float half = size * 0.5f;
            Vector3 normal = Vector3.Cross(right, up).normalized;

            void Stroke(float x0, float y0, float x1, float y1)
            {
                Vector3 a = centre + right * (x0 * half) + up * (y0 * half);
                Vector3 b = centre + right * (x1 * half) + up * (y1 * half);
                Vector3 dir = b - a;
                float len = dir.magnitude;
                if (len < 0.001f) return;

                Vector3 axis = dir / len;
                // Cross(normal, axis) keeps the quad's winding facing `normal`.
                Vector3 perp = Vector3.Cross(normal, axis).normalized * (thick * 0.5f);
                if (perp.sqrMagnitude < 1e-8f) perp = up * (thick * 0.5f);

                Vector3 e = axis * (len * 0.5f + thick * 0.5f);
                Vector3 mid = (a + b) * 0.5f;

                mb.AddQuad(mid - e - perp, mid + e - perp, mid + e + perp, mid - e + perp, normal, slot);
            }

            // Every glyph gets a horizontal top or bottom bar plus a vertical spine, then a
            // couple of random interior strokes. That is enough structure to read as a
            // character rather than noise.
            int pattern = rng.Range(0, 8);

            switch (pattern)
            {
                case 0:  // 目-like
                    Stroke(-0.8f, 0.85f, 0.8f, 0.85f);
                    Stroke(-0.8f, -0.85f, 0.8f, -0.85f);
                    Stroke(-0.8f, 0.85f, -0.8f, -0.85f);
                    Stroke(0.8f, 0.85f, 0.8f, -0.85f);
                    Stroke(-0.8f, 0f, 0.8f, 0f);
                    break;
                case 1:  // 王-like
                    Stroke(-0.8f, 0.85f, 0.8f, 0.85f);
                    Stroke(-0.55f, 0.1f, 0.55f, 0.1f);
                    Stroke(-0.9f, -0.85f, 0.9f, -0.85f);
                    Stroke(0f, 0.85f, 0f, -0.85f);
                    break;
                case 2:  // 大-like
                    Stroke(-0.85f, 0.45f, 0.85f, 0.45f);
                    Stroke(0f, 0.9f, 0f, -0.1f);
                    Stroke(-0.1f, 0.2f, -0.85f, -0.9f);
                    Stroke(0.1f, 0.2f, 0.85f, -0.9f);
                    break;
                case 3:  // 口-like with a lid
                    Stroke(-0.9f, 0.95f, 0.9f, 0.95f);
                    Stroke(-0.65f, 0.45f, 0.65f, 0.45f);
                    Stroke(-0.65f, 0.45f, -0.65f, -0.8f);
                    Stroke(0.65f, 0.45f, 0.65f, -0.8f);
                    Stroke(-0.65f, -0.8f, 0.65f, -0.8f);
                    break;
                case 4:  // 中-like
                    Stroke(-0.6f, 0.6f, 0.6f, 0.6f);
                    Stroke(-0.6f, -0.35f, 0.6f, -0.35f);
                    Stroke(-0.6f, 0.6f, -0.6f, -0.35f);
                    Stroke(0.6f, 0.6f, 0.6f, -0.35f);
                    Stroke(0f, 0.95f, 0f, -0.95f);
                    break;
                case 5:  // 食-like
                    Stroke(-0.5f, 0.95f, 0.5f, 0.6f);
                    Stroke(0.5f, 0.6f, -0.5f, 0.6f);
                    Stroke(-0.85f, 0.2f, 0.85f, 0.2f);
                    Stroke(-0.6f, -0.2f, 0.6f, -0.2f);
                    Stroke(-0.6f, -0.7f, 0.6f, -0.7f);
                    Stroke(0f, 0.2f, 0f, -0.7f);
                    break;
                case 6:  // 川-like
                    Stroke(-0.7f, 0.9f, -0.7f, -0.9f);
                    Stroke(0f, 0.7f, 0f, -0.9f);
                    Stroke(0.7f, 0.9f, 0.7f, -0.6f);
                    Stroke(-0.9f, 0.95f, 0.9f, 0.95f);
                    break;
                default: // 茶-like
                    Stroke(-0.85f, 0.9f, 0.85f, 0.9f);
                    Stroke(-0.45f, 0.9f, -0.45f, 0.5f);
                    Stroke(0.45f, 0.9f, 0.45f, 0.5f);
                    Stroke(-0.75f, 0.3f, 0.75f, 0.3f);
                    Stroke(0f, 0.5f, 0f, -0.9f);
                    Stroke(-0.6f, -0.35f, 0.6f, -0.35f);
                    break;
            }
        }
    }
}
