using System.Collections.Generic;
using UnityEngine;
using FormosaExpress.Core;

namespace FormosaExpress.Traffic
{
    public enum VehicleKind
    {
        Sedan,
        Taxi,
        Van,
        Bus,
        Scooter,
        Truck
    }

    /// <summary>A pre-built mesh variant: shared by every instance of that colour and kind.</summary>
    public sealed class VehicleMeshVariant
    {
        public VehicleKind Kind;
        public Mesh Surface;
        public Mesh Glow;
        public Vector3 ColliderSize;
        public Vector3 ColliderCentre;
        public float DesiredSpeed;
        public float WheelBase;

        /// <summary>Relative odds of being picked by <see cref="TrafficSystem"/>'s weighted
        /// spawn roll. 1 = normal share; scooters are weighted heavily so the street reads as a
        /// real night-market swarm of mopeds rather than an even mix of car types.</summary>
        public float SpawnWeight = 1f;
    }

    /// <summary>
    /// Builds every traffic vehicle mesh once at boot. Instances share these meshes so the SRP
    /// batcher can draw the whole rush hour in a couple of dozen calls.
    /// </summary>
    public static class VehicleFactory
    {
        public static List<VehicleMeshVariant> BuildLibrary(Palette pal)
        {
            var variants = new List<VehicleMeshVariant>(48);

            int rubber = pal.Add(new Color(0.07f, 0.07f, 0.08f));
            int rim = pal.Add(new Color(0.70f, 0.72f, 0.74f));
            int glass = pal.Add(new Color(0.15f, 0.21f, 0.27f));
            int chrome = pal.Add(new Color(0.80f, 0.83f, 0.86f));
            int dark = pal.Add(new Color(0.13f, 0.13f, 0.15f));
            int taxiYellow = pal.Add(new Color(0.98f, 0.76f, 0.12f));
            int busBlue = pal.Add(new Color(0.16f, 0.34f, 0.62f));
            int white = pal.Add(new Color(0.92f, 0.92f, 0.90f));
            int headlight = pal.Add(new Color(1.00f, 0.95f, 0.82f));
            int taillight = pal.Add(new Color(1.00f, 0.20f, 0.14f));
            int signGreen = pal.Add(new Color(0.30f, 1.00f, 0.55f));

            var bodySlots = new int[Art.CarColours.Length];
            var bodyDarkSlots = new int[Art.CarColours.Length];
            for (int i = 0; i < Art.CarColours.Length; i++)
            {
                bodySlots[i] = pal.Add(Art.CarColours[i]);
                bodyDarkSlots[i] = pal.AddShaded(Art.CarColours[i], 0.70f);
            }

            var palette = new VehiclePalette
            {
                Rubber = rubber, Rim = rim, Glass = glass, Chrome = chrome, Dark = dark,
                White = white, Headlight = headlight, Taillight = taillight, SignGreen = signGreen
            };

            for (int c = 0; c < Art.CarColours.Length; c++)
            {
                palette.Body = bodySlots[c];
                palette.BodyDark = bodyDarkSlots[c];
                variants.Add(BuildSedan(pal, palette));
                variants.Add(BuildVan(pal, palette));
                variants.Add(BuildScooter(pal, palette));
            }

            // Taxis and buses have fixed liveries, so only a couple of each.
            palette.Body = taxiYellow;
            palette.BodyDark = pal.AddShaded(new Color(0.98f, 0.76f, 0.12f), 0.72f);
            variants.Add(BuildSedan(pal, palette, isTaxi: true));
            variants.Add(BuildSedan(pal, palette, isTaxi: true));

            palette.Body = busBlue;
            palette.BodyDark = pal.AddShaded(new Color(0.16f, 0.34f, 0.62f), 0.70f);
            variants.Add(BuildBus(pal, palette));

            palette.Body = pal.Add(new Color(0.86f, 0.88f, 0.86f));
            palette.BodyDark = pal.AddShaded(new Color(0.86f, 0.88f, 0.86f), 0.72f);
            variants.Add(BuildTruck(pal, palette));
            variants.Add(BuildBus(pal, palette));

            return variants;
        }

        struct VehiclePalette
        {
            public int Body, BodyDark, Rubber, Rim, Glass, Chrome, Dark, White, Headlight, Taillight, SignGreen;
        }

        // ------------------------------------------------------------------ shared parts

        static void AddWheels(MeshBuilder mb, VehiclePalette p, float halfBase, float halfTrack,
            float radius, float width)
        {
            Quaternion axis = Quaternion.Euler(0f, 0f, 90f);
            for (int fx = -1; fx <= 1; fx += 2)
            for (int fz = -1; fz <= 1; fz += 2)
            {
                var centre = new Vector3(fx * halfTrack - width * 0.5f, radius, fz * halfBase);
                mb.AddCylinder(centre, radius, radius, width, 10, axis, p.Rubber, p.Rubber);
                mb.AddCylinder(centre + new Vector3(fx > 0 ? width * 0.55f : width * 0.45f, 0f, 0f),
                    radius * 0.55f, radius * 0.55f, width * 0.2f, 8, axis, p.Rim, p.Rim);
            }
        }

        static void AddLights(MeshBuilder glow, VehiclePalette p, float frontZ, float rearZ, float halfWidth, float y)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                glow.AddBox(new Vector3(s * halfWidth * 0.72f, y, frontZ), new Vector3(0.30f, 0.16f, 0.05f), p.Headlight);
                glow.AddBox(new Vector3(s * halfWidth * 0.74f, y, rearZ), new Vector3(0.28f, 0.14f, 0.05f), p.Taillight);
            }
        }

        // ------------------------------------------------------------------ sedan

        static VehicleMeshVariant BuildSedan(Palette pal, VehiclePalette p, bool isTaxi = false)
        {
            var mb = new MeshBuilder(pal);
            var glow = new MeshBuilder(pal);

            const float length = 4.35f, width = 1.78f, wheelR = 0.32f;

            AddWheels(mb, p, 1.30f, width * 0.5f - 0.06f, wheelR, 0.22f);

            // Lower body.
            mb.AddTaperedBox(new Vector3(0f, 0.62f, 0f), new Vector3(width, 0.62f, length), 0.97f, 0.95f,
                Quaternion.identity, p.Body, p.BodyDark);

            // Cabin.
            mb.AddTaperedBox(new Vector3(0f, 1.12f, -0.16f), new Vector3(width * 0.90f, 0.56f, length * 0.52f),
                0.80f, 0.78f, Quaternion.identity, p.Body, p.BodyDark);

            // Glass: windscreen, rear window, side windows.
            mb.AddBox(new Vector3(0f, 1.14f, length * 0.16f), new Vector3(width * 0.80f, 0.40f, 0.06f),
                Quaternion.Euler(-24f, 0f, 0f), p.Glass, p.Glass, p.Glass);
            mb.AddBox(new Vector3(0f, 1.14f, -length * 0.24f), new Vector3(width * 0.78f, 0.38f, 0.06f),
                Quaternion.Euler(26f, 0f, 0f), p.Glass, p.Glass, p.Glass);
            for (int s = -1; s <= 1; s += 2)
                mb.AddBox(new Vector3(s * width * 0.44f, 1.14f, -0.16f), new Vector3(0.05f, 0.34f, length * 0.34f),
                    p.Glass);

            // Bumpers, grille and mirrors.
            mb.AddBox(new Vector3(0f, 0.48f, length * 0.5f), new Vector3(width * 0.94f, 0.22f, 0.14f), p.Dark);
            mb.AddBox(new Vector3(0f, 0.48f, -length * 0.5f), new Vector3(width * 0.94f, 0.22f, 0.14f), p.Dark);
            mb.AddBox(new Vector3(0f, 0.72f, length * 0.5f), new Vector3(width * 0.55f, 0.16f, 0.06f), p.Chrome);
            for (int s = -1; s <= 1; s += 2)
                mb.AddBox(new Vector3(s * (width * 0.5f + 0.09f), 1.06f, length * 0.13f),
                    new Vector3(0.16f, 0.09f, 0.07f), p.Dark);

            if (isTaxi)
            {
                mb.AddBox(new Vector3(0f, 1.44f, -0.10f), new Vector3(0.62f, 0.16f, 0.24f), p.Dark);
                glow.AddBox(new Vector3(0f, 1.44f, -0.10f), new Vector3(0.56f, 0.11f, 0.20f), p.Headlight);
                for (int s = -1; s <= 1; s += 2)
                    mb.AddBox(new Vector3(s * (width * 0.5f + 0.01f), 0.78f, -0.1f),
                        new Vector3(0.02f, 0.16f, 1.2f), p.White);
            }

            AddLights(glow, p, length * 0.5f + 0.02f, -length * 0.5f - 0.02f, width * 0.5f, 0.72f);

            return Finish(VehicleKind.Sedan, mb, glow, width, 1.42f, length, 12.5f, 2.6f);
        }

        // ------------------------------------------------------------------ van

        static VehicleMeshVariant BuildVan(Palette pal, VehiclePalette p)
        {
            var mb = new MeshBuilder(pal);
            var glow = new MeshBuilder(pal);

            const float length = 4.9f, width = 1.92f, wheelR = 0.34f;

            AddWheels(mb, p, 1.45f, width * 0.5f - 0.05f, wheelR, 0.24f);

            mb.AddBox(new Vector3(0f, 1.10f, -0.25f), new Vector3(width, 1.52f, length * 0.78f),
                p.Body, p.BodyDark, p.Dark);
            mb.AddTaperedBox(new Vector3(0f, 0.86f, length * 0.35f), new Vector3(width, 1.06f, length * 0.30f),
                0.96f, 0.9f, Quaternion.identity, p.Body, p.BodyDark);

            mb.AddBox(new Vector3(0f, 1.20f, length * 0.44f), new Vector3(width * 0.84f, 0.48f, 0.06f),
                Quaternion.Euler(-14f, 0f, 0f), p.Glass, p.Glass, p.Glass);
            for (int s = -1; s <= 1; s += 2)
                mb.AddBox(new Vector3(s * width * 0.48f, 1.24f, length * 0.24f),
                    new Vector3(0.05f, 0.40f, 0.62f), p.Glass);

            // A cargo stripe and roof rack.
            mb.AddBox(new Vector3(0f, 1.05f, -0.25f), new Vector3(width + 0.02f, 0.14f, length * 0.78f), p.White);
            mb.AddBox(new Vector3(0f, 1.90f, -0.3f), new Vector3(width * 0.82f, 0.08f, length * 0.5f), p.Dark);

            mb.AddBox(new Vector3(0f, 0.5f, length * 0.5f), new Vector3(width * 0.94f, 0.24f, 0.14f), p.Dark);
            AddLights(glow, p, length * 0.5f + 0.02f, -length * 0.39f - 0.02f, width * 0.5f, 0.76f);

            return Finish(VehicleKind.Van, mb, glow, width, 1.95f, length, 11f, 2.9f);
        }

        // ------------------------------------------------------------------ bus

        static VehicleMeshVariant BuildBus(Palette pal, VehiclePalette p)
        {
            var mb = new MeshBuilder(pal);
            var glow = new MeshBuilder(pal);

            const float length = 9.4f, width = 2.42f, wheelR = 0.46f;

            Quaternion axis = Quaternion.Euler(0f, 0f, 90f);
            for (int s = -1; s <= 1; s += 2)
            {
                foreach (float z in new[] { length * 0.34f, -length * 0.26f, -length * 0.38f })
                {
                    var centre = new Vector3(s * (width * 0.5f - 0.10f) - 0.14f, wheelR, z);
                    mb.AddCylinder(centre, wheelR, wheelR, 0.28f, 10, axis, p.Rubber, p.Rubber);
                }
            }

            mb.AddBox(new Vector3(0f, 1.72f, 0f), new Vector3(width, 2.32f, length), p.Body, p.BodyDark, p.Dark);

            // Window band down both sides plus the windscreen.
            for (int s = -1; s <= 1; s += 2)
                mb.AddBox(new Vector3(s * (width * 0.5f + 0.01f), 2.10f, -0.2f),
                    new Vector3(0.05f, 0.86f, length * 0.82f), p.Glass);

            mb.AddBox(new Vector3(0f, 2.10f, length * 0.5f + 0.01f), new Vector3(width * 0.88f, 1.00f, 0.05f), p.Glass);
            mb.AddBox(new Vector3(0f, 2.10f, -length * 0.5f - 0.01f), new Vector3(width * 0.88f, 0.86f, 0.05f), p.Glass);

            // Livery stripes, doors, roof units.
            mb.AddBox(new Vector3(0f, 1.30f, 0f), new Vector3(width + 0.02f, 0.20f, length + 0.02f), p.White);
            for (int s = -1; s <= 1; s += 2)
                mb.AddBox(new Vector3(s * (width * 0.5f + 0.015f), 1.30f, length * 0.28f),
                    new Vector3(0.03f, 1.7f, 1.0f), p.Dark);

            mb.AddBox(new Vector3(0f, 2.94f, -length * 0.2f), new Vector3(width * 0.7f, 0.24f, 2.2f), p.White);

            // Illuminated destination sign.
            glow.AddBox(new Vector3(0f, 2.72f, length * 0.5f + 0.02f), new Vector3(width * 0.62f, 0.26f, 0.04f),
                p.SignGreen);

            AddLights(glow, p, length * 0.5f + 0.02f, -length * 0.5f - 0.02f, width * 0.5f, 0.86f);

            return Finish(VehicleKind.Bus, mb, glow, width, 3.1f, length, 9f, 6.0f);
        }

        // ------------------------------------------------------------------ truck

        static VehicleMeshVariant BuildTruck(Palette pal, VehiclePalette p)
        {
            var mb = new MeshBuilder(pal);
            var glow = new MeshBuilder(pal);

            const float length = 6.6f, width = 2.20f, wheelR = 0.42f;

            AddWheels(mb, p, 2.0f, width * 0.5f - 0.08f, wheelR, 0.28f);

            // Cab.
            mb.AddBox(new Vector3(0f, 1.32f, length * 0.30f), new Vector3(width, 1.76f, length * 0.36f),
                p.Body, p.BodyDark, p.Dark);
            mb.AddBox(new Vector3(0f, 1.62f, length * 0.47f), new Vector3(width * 0.86f, 0.72f, 0.05f), p.Glass);

            // Flatbed with a canvas cover.
            mb.AddBox(new Vector3(0f, 0.92f, -length * 0.16f), new Vector3(width, 0.28f, length * 0.62f), p.Dark);
            mb.AddTaperedBox(new Vector3(0f, 1.62f, -length * 0.16f), new Vector3(width * 0.98f, 1.32f, length * 0.60f),
                0.9f, 1f, Quaternion.identity, p.White, p.BodyDark);

            for (int s = -1; s <= 1; s += 2)
                mb.AddBox(new Vector3(s * (width * 0.5f + 0.06f), 1.86f, length * 0.24f),
                    new Vector3(0.18f, 0.10f, 0.08f), p.Dark);

            AddLights(glow, p, length * 0.5f + 0.02f, -length * 0.5f - 0.02f, width * 0.5f, 0.84f);

            return Finish(VehicleKind.Truck, mb, glow, width, 2.6f, length, 9.5f, 3.6f);
        }

        // ------------------------------------------------------------------ traffic scooter

        static VehicleMeshVariant BuildScooter(Palette pal, VehiclePalette p)
        {
            var mb = new MeshBuilder(pal);
            var glow = new MeshBuilder(pal);

            const float wheelR = 0.26f;
            Quaternion axis = Quaternion.Euler(0f, 0f, 90f);
            mb.AddCylinder(new Vector3(-0.055f, wheelR, 0.55f), wheelR, wheelR, 0.11f, 10, axis, p.Rubber, p.Rubber);
            mb.AddCylinder(new Vector3(-0.055f, wheelR, -0.55f), wheelR, wheelR, 0.11f, 10, axis, p.Rubber, p.Rubber);

            mb.AddTaperedBox(new Vector3(0f, 0.52f, -0.10f), new Vector3(0.34f, 0.36f, 1.02f), 0.8f, 0.9f,
                Quaternion.identity, p.Body, p.BodyDark);
            mb.AddTaperedBox(new Vector3(0f, 0.62f, 0.44f), new Vector3(0.38f, 0.66f, 0.26f), 0.7f, 0.85f,
                Quaternion.Euler(-12f, 0f, 0f), p.Body, p.BodyDark);
            mb.AddBox(new Vector3(0f, 0.78f, -0.26f), new Vector3(0.30f, 0.13f, 0.52f), p.Dark);
            mb.AddBeam(new Vector3(-0.30f, 1.00f, 0.50f), new Vector3(0.30f, 1.00f, 0.50f), 0.045f, p.Chrome);

            // A rider so it does not look driverless.
            int jacket = pal.Add(Art.ClothColours[(p.Body * 7) % Art.ClothColours.Length]);
            int jacketDark = pal.AddShaded(Art.ClothColours[(p.Body * 7) % Art.ClothColours.Length], 0.7f);
            int helmet = pal.Add(new Color(0.16f, 0.17f, 0.20f));
            int skin = pal.Add(Art.SkinTones[(p.Body * 3) % Art.SkinTones.Length]);

            mb.AddTaperedBox(new Vector3(0f, 1.12f, -0.20f), new Vector3(0.40f, 0.52f, 0.28f), 1.0f, 1.0f,
                Quaternion.Euler(13f, 0f, 0f), jacket, jacketDark);
            for (int s = -1; s <= 1; s += 2)
            {
                mb.AddBox(new Vector3(s * 0.20f, 1.18f, 0.08f), new Vector3(0.10f, 0.10f, 0.46f),
                    Quaternion.Euler(-26f, 0f, 0f), jacket, jacketDark, jacketDark);
                mb.AddBox(new Vector3(s * 0.13f, 0.62f, 0.04f), new Vector3(0.13f, 0.12f, 0.42f),
                    Quaternion.Euler(-70f, 0f, 0f), p.Dark, p.Dark, p.Dark);
            }

            mb.AddBox(new Vector3(0f, 1.44f, -0.16f), new Vector3(0.16f, 0.13f, 0.16f), skin);
            mb.AddTaperedBox(new Vector3(0f, 1.56f, -0.18f), new Vector3(0.27f, 0.25f, 0.29f), 0.72f, 0.72f,
                Quaternion.identity, helmet, helmet);

            glow.AddBox(new Vector3(0f, 0.86f, 0.58f), new Vector3(0.18f, 0.12f, 0.05f), p.Headlight);
            glow.AddBox(new Vector3(0f, 0.66f, -0.66f), new Vector3(0.16f, 0.09f, 0.04f), p.Taillight);

            return Finish(VehicleKind.Scooter, mb, glow, 0.62f, 1.75f, 1.72f, 13.5f, 1.1f);
        }

        static VehicleMeshVariant Finish(VehicleKind kind, MeshBuilder surface, MeshBuilder glow,
            float width, float height, float length, float speed, float wheelBase)
        {
            return new VehicleMeshVariant
            {
                Kind = kind,
                Surface = surface.ToMesh($"FE_{kind}_Surface"),
                Glow = glow.IsEmpty ? null : glow.ToMesh($"FE_{kind}_Glow"),
                ColliderSize = new Vector3(width, height, length),
                ColliderCentre = new Vector3(0f, height * 0.5f, 0f),
                DesiredSpeed = speed,
                WheelBase = wheelBase,
                // Scooters swarm a real night market street; every other kind keeps its normal
                // share of the pick.
                SpawnWeight = kind == VehicleKind.Scooter ? 5f : 1f
            };
        }
    }
}
