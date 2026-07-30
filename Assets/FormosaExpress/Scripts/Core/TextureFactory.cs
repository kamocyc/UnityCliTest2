using System.Collections.Generic;
using UnityEngine;

namespace FormosaExpress.Core
{
    /// <summary>
    /// Procedurally generated textures and sprites. The game ships no image files, so every
    /// panel, icon and the sky itself is drawn here at boot.
    /// </summary>
    public static class TextureFactory
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static void ClearCache() => Cache.Clear();

        // ---------------------------------------------------------------- UI sprites

        /// <summary>A white rounded rectangle set up for 9-slicing, tinted by the Image colour.</summary>
        public static Sprite RoundedRect(int radius = 16, float border = 0f)
        {
            string key = $"rr_{radius}_{border:0.##}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            int size = radius * 2 + 8;
            var tex = NewTexture(size, size, $"FE_RoundRect{radius}");
            var px = new Color[size * size];
            float r = radius;
            float cx = size * 0.5f, cy = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance to the rounded-rect boundary via the classic inset-box trick.
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - cx) - (cx - r), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - cy) - (cy - r), 0f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - r;

                    float alpha = Mathf.Clamp01(0.5f - dist);
                    if (border > 0f)
                    {
                        float inner = Mathf.Clamp01(0.5f - (dist + border));
                        alpha = Mathf.Clamp01(alpha - inner);
                    }

                    px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);

            float b = radius + 2f;
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        public static Sprite Circle(int size = 128, float innerRadius01 = 0f)
        {
            string key = $"circ_{size}_{innerRadius01:0.###}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = NewTexture(size, size, "FE_Circle");
            var px = new Color[size * size];
            float c = size * 0.5f;
            float outer = c - 1f;
            float inner = innerRadius01 * outer;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float a = Mathf.Clamp01(outer - d);
                if (inner > 0f) a = Mathf.Min(a, Mathf.Clamp01(d - inner));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Radial falloff used for glows, vignettes and soft shadows.</summary>
        public static Sprite SoftGlow(int size = 128, float power = 2f)
        {
            string key = $"glow_{size}_{power:0.##}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = NewTexture(size, size, "FE_Glow");
            var px = new Color[size * size];
            float c = size * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), power);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        public static Sprite Plain()
        {
            if (Cache.TryGetValue("plain", out Sprite cached)) return cached;
            var tex = NewTexture(4, 4, "FE_White");
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply(false, false);
            var s = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            s.name = "plain";
            Cache["plain"] = s;
            return s;
        }

        /// <summary>A solid triangle. <paramml name="down"/> points it at the ground.</summary>
        public static Sprite Triangle(int size = 64, bool pointDown = true)
        {
            string key = $"tri_{size}_{pointDown}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = NewTexture(size, size, "FE_Triangle");
            var px = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float ty = pointDown ? 1f - (y + 0.5f) / size : (y + 0.5f) / size;
                float halfWidth = ty * 0.5f * size;
                float dx = Mathf.Abs(x + 0.5f - size * 0.5f);
                float a = Mathf.Clamp01(halfWidth - dx);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Five-pointed star, used for bonus markers.</summary>
        public static Sprite Star(int size = 64)
        {
            string key = $"star_{size}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = NewTexture(size, size, "FE_Star");
            var px = new Color[size * size];
            float c = size * 0.5f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - c) / c;
                float dy = (y + 0.5f - c) / c;
                float ang = Mathf.Atan2(dy, dx);
                float rad = Mathf.Sqrt(dx * dx + dy * dy);
                // 5-lobe radial threshold gives a clean star silhouette.
                float lobe = 0.55f + 0.35f * Mathf.Cos(5f * (ang - Mathf.PI * 0.5f));
                float a = Mathf.Clamp01((lobe - rad) * size * 0.25f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Map-pin silhouette for minimap markers.</summary>
        public static Sprite Pin(int size = 64)
        {
            string key = $"pin_{size}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = NewTexture(size, size, "FE_Pin");
            var px = new Color[size * size];
            float c = size * 0.5f;
            float headR = size * 0.28f;
            var head = new Vector2(c, size * 0.66f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float a = Mathf.Clamp01(headR - Vector2.Distance(p, head));

                // Tail: a triangle narrowing towards the bottom.
                float t = Mathf.InverseLerp(head.y, size * 0.08f, p.y);
                if (t >= 0f && t <= 1f)
                {
                    float halfW = Mathf.Lerp(headR * 0.85f, 0f, t);
                    a = Mathf.Max(a, Mathf.Clamp01(halfW - Mathf.Abs(p.x - c)));
                }

                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.16f), 100f);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Simple scooter silhouette for the HUD badge and player map blip.</summary>
        public static Sprite ScooterGlyph(int size = 64)
        {
            string key = $"scoot_{size}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = NewTexture(size, size, "FE_Scooter");
            var px = new Color[size * size];
            float s = size / 64f;

            void Disc(Vector2 centre, float radius)
            {
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre);
                    float a = Mathf.Clamp01(radius - d);
                    int i = y * size + x;
                    px[i].a = Mathf.Max(px[i].a, a);
                    px[i].r = px[i].g = px[i].b = 1f;
                }
            }

            void Bar(Vector2 a2, Vector2 b2, float halfThick)
            {
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    Vector2 ab = b2 - a2;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a2, ab) / ab.sqrMagnitude);
                    float d = Vector2.Distance(p, a2 + ab * t);
                    float al = Mathf.Clamp01(halfThick - d);
                    int i = y * size + x;
                    px[i].a = Mathf.Max(px[i].a, al);
                    px[i].r = px[i].g = px[i].b = 1f;
                }
            }

            Disc(new Vector2(15f * s, 16f * s), 8f * s);
            Disc(new Vector2(49f * s, 16f * s), 8f * s);
            Bar(new Vector2(15f * s, 16f * s), new Vector2(49f * s, 16f * s), 3.0f * s);
            Bar(new Vector2(20f * s, 24f * s), new Vector2(42f * s, 26f * s), 5.0f * s);
            Bar(new Vector2(42f * s, 26f * s), new Vector2(50f * s, 40f * s), 3.0f * s);
            Bar(new Vector2(46f * s, 40f * s), new Vector2(56f * s, 42f * s), 2.5f * s);
            Bar(new Vector2(20f * s, 26f * s), new Vector2(14f * s, 34f * s), 4.0f * s);

            tex.SetPixels(px);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>A gift-box glyph, matching the delivery beacon icon.</summary>
        public static Sprite GiftGlyph(int size = 64)
        {
            string key = $"gift_{size}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var tex = NewTexture(size, size, "FE_Gift");
            var px = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                bool body = u > 0.16f && u < 0.84f && v > 0.10f && v < 0.66f;
                bool lid = u > 0.10f && u < 0.90f && v >= 0.66f && v < 0.82f;
                bool ribbonV = Mathf.Abs(u - 0.5f) < 0.075f && v > 0.10f && v < 0.82f;
                bool bowL = Vector2.Distance(new Vector2(u, v), new Vector2(0.40f, 0.89f)) < 0.10f;
                bool bowR = Vector2.Distance(new Vector2(u, v), new Vector2(0.60f, 0.89f)) < 0.10f;

                float a = (body || lid || ribbonV || bowL || bowR) ? 1f : 0f;
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }

            tex.SetPixels(px);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        // ---------------------------------------------------------------- sky

        /// <summary>
        /// An equirectangular sky for Skybox/Panoramic: day-to-dusk-to-night gradient, banded
        /// clouds, a low sun glow and (at night) stars. <paramref name="time"/> is -1 = bright
        /// midday, 0 = golden hour, 1 = night.
        /// </summary>
        public static Texture2D SkyPanorama(float time, int width = 1024, int height = 512)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBAHalf, true, true)
            {
                name = "FE_Sky",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            // Blends across three anchors (day / dusk / night) instead of one Lerp, since
            // Color.Lerp clamps its t to 0..1 and would otherwise flatten every negative
            // (daytime) value to the dusk look.
            Color Blend(Color day, Color dusk, Color night) =>
                time <= 0f ? Color.Lerp(dusk, day, -time) : Color.Lerp(dusk, night, time);

            Color zenith = Blend(Art.SkyNoonZenith, Art.SkyDayZenith, Art.SkyNightZenith);
            Color horizon = Blend(Art.SkyNoonHorizon, Art.SkyDayHorizon, Art.SkyNightHorizon);
            Color ground = Blend(new Color(0.42f, 0.40f, 0.38f), new Color(0.20f, 0.17f, 0.18f), new Color(0.05f, 0.05f, 0.07f));

            // How "night-like" the glow/star/cloud-mood terms should read; these were authored
            // over the dusk->night half and just hold at the dusk look through the day half.
            float nightLike = Mathf.Clamp01(time);

            // The horizon glow is a dusk/night phenomenon - a low, warm or neon-lit sun. At full
            // daylight the sun is high overhead, so the glow band fades out rather than tinting
            // warm the way it would at dusk.
            float glowStrength = time <= 0f ? Mathf.Lerp(0.08f, 1f, time + 1f) : 1f;

            var px = new Color[width * height];
            var rng = new Rng(4242);

            // Pre-roll cloud band offsets so the sky is deterministic but not obviously tiled.
            var cloudPhase = new float[6];
            var cloudScale = new float[6];
            for (int i = 0; i < 6; i++) { cloudPhase[i] = rng.Range(0f, 100f); cloudScale[i] = rng.Range(2.2f, 7.5f); }

            const float sunAzimuth = 0.30f;   // matches the directional light set up in Bootstrap
            const float sunElevation = 0.545f;

            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;      // 0 = bottom, 1 = top
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    Color c;

                    if (v < 0.5f)
                    {
                        float t = Mathf.Clamp01(v / 0.5f);
                        c = Color.Lerp(ground, horizon, Mathf.Pow(t, 0.55f));
                    }
                    else
                    {
                        float t = Mathf.Clamp01((v - 0.5f) / 0.5f);
                        c = Color.Lerp(horizon, zenith, Mathf.Pow(t, 0.62f));
                    }

                    // Warm glow around the sun's azimuth, strongest near the horizon.
                    float az = Mathf.Abs(Mathf.DeltaAngle(u * 360f, sunAzimuth * 360f)) / 180f;
                    float glow = Mathf.Pow(Mathf.Clamp01(1f - az * 2.4f), 2.2f)
                                 * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v - sunElevation) * 3.4f), 2.0f);
                    c += Color.Lerp(new Color(1.35f, 0.62f, 0.24f), new Color(0.34f, 0.20f, 0.42f), nightLike) * glow * glowStrength;

                    // Soft horizontal cloud bands.
                    if (v > 0.46f)
                    {
                        float cloud = 0f;
                        for (int i = 0; i < 6; i++)
                        {
                            float band = 0.50f + i * 0.075f;
                            float dv = Mathf.Abs(v - band);
                            if (dv > 0.055f) continue;
                            float n = Mathf.PerlinNoise(u * cloudScale[i] + cloudPhase[i], band * 9f + cloudPhase[i]);
                            float shape = Mathf.Clamp01(1f - dv / 0.055f);
                            cloud += Mathf.Clamp01(n - 0.44f) * 2.1f * shape;
                        }

                        cloud = Mathf.Clamp01(cloud);
                        Color cloudLit = Blend(new Color(1.0f, 1.0f, 1.0f), new Color(1.15f, 0.72f, 0.52f), new Color(0.26f, 0.22f, 0.34f));
                        Color cloudShade = Blend(new Color(0.70f, 0.74f, 0.80f), new Color(0.55f, 0.38f, 0.40f), new Color(0.12f, 0.11f, 0.18f));
                        float lit = Mathf.Clamp01(1f - az * 1.6f);
                        c = Color.Lerp(c, Color.Lerp(cloudShade, cloudLit, lit), cloud * 0.85f);
                    }

                    // Stars, only once it is dark enough for them to read.
                    if (time > 0.35f && v > 0.55f)
                    {
                        float star = Mathf.PerlinNoise(x * 3.7f, y * 3.7f);
                        if (star > 0.985f)
                        {
                            float twinkle = (time - 0.35f) / 0.65f;
                            c += Color.white * twinkle * 1.6f;
                        }
                    }

                    px[y * width + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply(true, false);
            return tex;
        }

        // ---------------------------------------------------------------- helpers

        static Texture2D NewTexture(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }
    }
}
