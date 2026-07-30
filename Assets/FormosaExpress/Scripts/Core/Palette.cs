using System.Collections.Generic;
using UnityEngine;

namespace FormosaExpress.Core
{
    /// <summary>
    /// A runtime colour atlas. Geometry stores a UV that points at a single pixel of this
    /// texture instead of carrying vertex colours, which lets an entire city block collapse
    /// into one mesh sharing one material (and therefore one draw call).
    /// </summary>
    public sealed class Palette
    {
        public const int Dimension = 64;
        const int Capacity = Dimension * Dimension;
        const float Texel = 1f / Dimension;

        readonly Color32[] _pixels = new Color32[Capacity];
        readonly Dictionary<int, int> _lookup = new Dictionary<int, int>(512);
        readonly Vector2[] _uvs = new Vector2[Capacity];
        Texture2D _texture;
        int _count;
        bool _dirty;

        public Palette()
        {
            for (int i = 0; i < Capacity; i++)
            {
                int x = i % Dimension;
                int y = i / Dimension;
                _uvs[i] = new Vector2((x + 0.5f) * Texel, (y + 0.5f) * Texel);
            }

            // Slot 0 is always pure white so untinted geometry has a safe default.
            Add(Color.white);
        }

        public int Count => _count;

        public Texture2D Texture
        {
            get
            {
                if (_texture == null)
                {
                    _texture = new Texture2D(Dimension, Dimension, TextureFormat.RGBA32, false, false)
                    {
                        name = "FE_PaletteAtlas",
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp,
                        anisoLevel = 0
                    };
                    _dirty = true;
                }

                if (_dirty)
                {
                    _texture.SetPixels32(_pixels);
                    _texture.Apply(false, false);
                    _dirty = false;
                }

                return _texture;
            }
        }

        /// <summary>Registers a colour (de-duplicated) and returns its palette slot.</summary>
        public int Add(Color colour)
        {
            Color32 c = colour;
            int key = (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
            if (_lookup.TryGetValue(key, out int existing)) return existing;

            if (_count >= Capacity)
            {
                Debug.LogWarning("[FormosaExpress] Palette is full; reusing slot 0.");
                return 0;
            }

            int slot = _count++;
            _pixels[slot] = c;
            _lookup[key] = slot;
            _dirty = true;
            return slot;
        }

        /// <summary>Registers <paramref name="colour"/> scaled towards black; used for shaded faces.</summary>
        public int AddShaded(Color colour, float multiplier)
        {
            return Add(new Color(colour.r * multiplier, colour.g * multiplier, colour.b * multiplier, colour.a));
        }

        public Vector2 UV(int slot)
        {
            if (slot < 0 || slot >= Capacity) slot = 0;
            return _uvs[slot];
        }

        public Color ColorAt(int slot)
        {
            if (slot < 0 || slot >= Capacity) slot = 0;
            return _pixels[slot];
        }
    }
}
