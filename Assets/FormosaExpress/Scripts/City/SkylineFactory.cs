using UnityEngine;
using UnityEngine.Rendering;
using FormosaExpress.Core;

namespace FormosaExpress.City
{
    /// <summary>
    /// The city that the player never reaches: rings of tall, simple towers beyond the play
    /// area. Cheap silhouettes with lit window bands, they close off the horizon so the world
    /// reads as a district of a much larger city rather than an island.
    /// </summary>
    public static class SkylineFactory
    {
        public static void Build(CityModel model, MaterialLibrary mats, float nightFactor, Transform parent)
        {
            Palette pal = mats.Palette;
            var rng = new Rng(model.Seed * 4271 + 883);

            // Distance-hazed, desaturated masses. Fog does the rest of the work.
            var bodySlots = new int[6];
            var topSlots = new int[6];
            for (int i = 0; i < bodySlots.Length; i++)
            {
                Color c = Color.Lerp(new Color(0.34f, 0.33f, 0.38f), new Color(0.20f, 0.20f, 0.27f),
                    i / (float)(bodySlots.Length - 1));
                bodySlots[i] = pal.AddShaded(c, 0.85f);
                topSlots[i] = pal.AddShaded(c, 1.1f);
            }

            int windowSlot = pal.Add(Color.Lerp(new Color(0.75f, 0.66f, 0.48f), new Color(1.0f, 0.82f, 0.52f), nightFactor));
            int beaconSlot = pal.Add(new Color(1f, 0.25f, 0.22f));

            var root = new GameObject("Skyline");
            root.transform.SetParent(parent, false);

            var surface = new MeshBuilder(pal);
            var glow = new MeshBuilder(pal);

            float innerRadius = Mathf.Max(Tuning.WorldSizeX, Tuning.WorldSizeZ) * 0.62f + 40f;

            const int towers = 150;
            int flushed = 0;
            int chunk = 0;

            for (int i = 0; i < towers; i++)
            {
                // Spread across three rings; further rings are taller so the horizon steps up.
                int ring = i % 3;
                float radius = innerRadius + ring * 130f + rng.Range(-40f, 55f);
                float angle = (i / (float)towers) * Mathf.PI * 2f + rng.Range(-0.02f, 0.02f);

                var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                // Skip anything that would land inside the play area.
                if (Mathf.Abs(position.x) < Tuning.WorldSizeX * 0.5f + 40f &&
                    Mathf.Abs(position.z) < Tuning.WorldSizeZ * 0.5f + 40f)
                    continue;

                float width = rng.Range(18f, 46f);
                float depth = rng.Range(18f, 46f);
                float height = rng.Range(24f, 46f) + ring * rng.Range(14f, 40f);

                int palIndex = rng.Range(0, bodySlots.Length);
                var rotation = Quaternion.Euler(0f, rng.Range(0f, 90f), 0f);

                surface.AddBox(position + Vector3.up * (height * 0.5f), new Vector3(width, height, depth),
                    rotation, topSlots[palIndex], bodySlots[palIndex], bodySlots[palIndex]);

                // A stepped-back upper section on some of them.
                if (rng.Chance(0.45f))
                {
                    float upperHeight = rng.Range(8f, 26f);
                    surface.AddBox(position + Vector3.up * (height + upperHeight * 0.5f),
                        new Vector3(width * 0.62f, upperHeight, depth * 0.62f), rotation,
                        topSlots[palIndex], bodySlots[palIndex], bodySlots[palIndex]);
                }

                // Horizontal bands of lit windows, which is all you can resolve at this range.
                int bands = Mathf.Max(2, Mathf.FloorToInt(height / 9f));
                for (int b = 0; b < bands; b++)
                {
                    if (!rng.Chance(Mathf.Lerp(0.35f, 0.85f, nightFactor))) continue;

                    float y = (b + 0.8f) / bands * height;
                    glow.AddBox(position + Vector3.up * y, new Vector3(width * 1.005f, 1.6f, depth * 1.005f),
                        rotation, windowSlot, windowSlot, windowSlot);
                }

                // Aircraft warning light on the tallest ones.
                if (height > 70f)
                    glow.AddBox(position + Vector3.up * (height + 1.5f), new Vector3(2.2f, 2.2f, 2.2f),
                        rotation, beaconSlot, beaconSlot, beaconSlot);

                if (++flushed < 30) continue;

                flushed = 0;
                Flush(surface, glow, root.transform, mats, chunk++);
            }

            Flush(surface, glow, root.transform, mats, chunk);
        }

        static void Flush(MeshBuilder surface, MeshBuilder glow, Transform parent, MaterialLibrary mats, int index)
        {
            GameObject body = surface.Flush($"Skyline_{index}", parent, mats.Surface, false);
            NoShadows(body);
            NoShadows(glow.Flush($"Skyline_{index}_Glow", parent, mats.GlowSoft, false));
        }

        static void NoShadows(GameObject go)
        {
            if (go == null) return;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;

            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }
}
