using UnityEngine;

namespace FormosaExpress.Core
{
    public static class MathX
    {
        /// <summary>Frame-rate independent exponential smoothing. <paramref name="speed"/> is 1/seconds.</summary>
        public static float ExpSmooth(float current, float target, float speed, float dt)
        {
            if (speed <= 0f) return target;
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
        }

        public static Vector3 ExpSmooth(Vector3 current, Vector3 target, float speed, float dt)
        {
            if (speed <= 0f) return target;
            return Vector3.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
        }

        public static Quaternion ExpSmooth(Quaternion current, Quaternion target, float speed, float dt)
        {
            if (speed <= 0f) return target;
            return Quaternion.Slerp(current, target, 1f - Mathf.Exp(-speed * dt));
        }

        public static float Remap(float v, float inMin, float inMax, float outMin, float outMax)
        {
            if (Mathf.Approximately(inMax, inMin)) return outMin;
            return outMin + (outMax - outMin) * ((v - inMin) / (inMax - inMin));
        }

        public static float Remap01(float v, float inMin, float inMax)
        {
            return Mathf.Clamp01(Remap(v, inMin, inMax, 0f, 1f));
        }

        public static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        public static float EaseInCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        /// <summary>Overshooting ease, good for UI pops.</summary>
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.70158f;
            float u = t - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        public static Vector3 Flatten(Vector3 v) { v.y = 0f; return v; }

        public static Vector2 ToXZ(Vector3 v) => new Vector2(v.x, v.z);
        public static Vector3 FromXZ(Vector2 v, float y = 0f) => new Vector3(v.x, y, v.y);

        /// <summary>Signed angle in degrees between two directions around +Y.</summary>
        public static float SignedYawTo(Vector3 from, Vector3 to)
        {
            from.y = 0f; to.y = 0f;
            if (from.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(from.normalized, to.normalized, Vector3.up);
        }

        /// <summary>Closest point on segment ab to p, plus the parametric position.</summary>
        public static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p, out float t)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) { t = 0f; return a; }
            t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            return a + ab * t;
        }

        /// <summary>Metres/second to the km/h shown on the HUD.</summary>
        public static float ToKmh(float metresPerSecond) => metresPerSecond * 3.6f;

        public static string FormatClock(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = Mathf.CeilToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }

        public static string FormatMoney(int amount) => "$" + amount.ToString("N0");

        public static string FormatDistance(float metres)
        {
            if (metres >= 1000f) return (metres / 1000f).ToString("0.0") + "km";
            return Mathf.RoundToInt(metres) + "m";
        }
    }

    /// <summary>
    /// A tiny deterministic PRNG so a given city seed always produces the same city,
    /// independent of Unity's global random state.
    /// </summary>
    public struct Rng
    {
        uint _state;

        public Rng(int seed)
        {
            _state = seed == 0 ? 0x9E3779B9u : (uint)seed;
        }

        public uint NextUInt()
        {
            // xorshift32
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        public float Value => (NextUInt() & 0xFFFFFF) / 16777216f;

        public float Range(float min, float max) => min + (max - min) * Value;

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public bool Chance(float probability) => Value < probability;

        public T Pick<T>(T[] items) => items[Range(0, items.Length)];

        /// <summary>A hue-shifted variation of a colour, for cheap visual variety.</summary>
        public Color Vary(Color c, float hueJitter = 0.02f, float satJitter = 0.08f, float valJitter = 0.10f)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(h + Range(-hueJitter, hueJitter), 1f);
            s = Mathf.Clamp01(s + Range(-satJitter, satJitter));
            v = Mathf.Clamp01(v + Range(-valJitter, valJitter));
            Color outC = Color.HSVToRGB(h, s, v);
            outC.a = c.a;
            return outC;
        }

        public Vector3 OnUnitCircleXZ()
        {
            float a = Value * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }
    }
}
