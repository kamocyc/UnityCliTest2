using UnityEngine;
using UnityEngine.Rendering;

namespace FormosaExpress.Core
{
    /// <summary>
    /// Every material used by the game, built at runtime around a single palette atlas.
    /// Keeping the count this low is what lets the whole city render in a handful of
    /// SRP-batched draw calls.
    /// </summary>
    public sealed class MaterialLibrary
    {
        public readonly Palette Palette;

        /// <summary>Lit, slightly glossy. Buildings, vehicles, props.</summary>
        public Material Surface { get; private set; }

        /// <summary>Lit and matte. Roads, sidewalks, ground.</summary>
        public Material Ground { get; private set; }

        /// <summary>Unlit at moderate HDR intensity. Windows, lit shop interiors.</summary>
        public Material GlowSoft { get; private set; }

        /// <summary>Unlit at high HDR intensity. Neon signs, headlights, beacons.</summary>
        public Material GlowHot { get; private set; }

        /// <summary>Additive transparent. Light shafts, beacon pillars, speed lines.</summary>
        public Material Additive { get; private set; }

        /// <summary>Alpha-blended unlit. Decals, skid marks, ground shadows.</summary>
        public Material Decal { get; private set; }

        public MaterialLibrary(Palette palette)
        {
            Palette = palette;
            Build();
        }

        void Build()
        {
            Shader lit = FindShader("Universal Render Pipeline/Lit", "Standard");
            Shader unlit = FindShader("Universal Render Pipeline/Unlit", "Unlit/Texture");

            Surface = new Material(lit) { name = "FE_Surface" };
            ConfigureLit(Surface, smoothness: 0.22f, metallic: 0f);

            Ground = new Material(lit) { name = "FE_Ground" };
            ConfigureLit(Ground, smoothness: 0.42f, metallic: 0f);
            Ground.enableInstancing = true;

            GlowSoft = new Material(unlit) { name = "FE_GlowSoft" };
            ConfigureUnlit(GlowSoft, 1.9f);

            GlowHot = new Material(unlit) { name = "FE_GlowHot" };
            ConfigureUnlit(GlowHot, 5.5f);

            Additive = new Material(unlit) { name = "FE_Additive" };
            ConfigureUnlit(Additive, 1.5f);
            MakeTransparent(Additive, BlendMode.One, BlendMode.One, RenderQueue.Transparent);

            Decal = new Material(unlit) { name = "FE_Decal" };
            ConfigureUnlit(Decal, 1f);
            MakeTransparent(Decal, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, RenderQueue.Transparent - 50);
        }

        /// <summary>Re-uploads the atlas; call once after all geometry has registered its colours.</summary>
        public void CommitPalette()
        {
            Texture2D tex = Palette.Texture;
            AssignAtlas(Surface, tex);
            AssignAtlas(Ground, tex);
            AssignAtlas(GlowSoft, tex);
            AssignAtlas(GlowHot, tex);
            AssignAtlas(Additive, tex);
            AssignAtlas(Decal, tex);
        }

        static void AssignAtlas(Material m, Texture2D tex)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        }

        static void ConfigureLit(Material m, float smoothness, float metallic)
        {
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            m.enableInstancing = true;
        }

        static void ConfigureUnlit(Material m, float intensity)
        {
            var c = new Color(intensity, intensity, intensity, 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            m.enableInstancing = true;
        }

        static void MakeTransparent(Material m, BlendMode src, BlendMode dst, RenderQueue queue)
        {
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", src == BlendMode.One ? 2f : 0f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)src);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)dst);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            m.SetInt("_SrcBlend", (int)src);
            m.SetInt("_DstBlend", (int)dst);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)queue;
        }

        static Shader FindShader(params string[] candidates)
        {
            foreach (string name in candidates)
            {
                Shader s = Shader.Find(name);
                if (s != null) return s;
            }

            Debug.LogError($"[FormosaExpress] None of the shaders [{string.Join(", ", candidates)}] could be found.");
            return Shader.Find("Sprites/Default");
        }

        /// <summary>A one-off unlit material with an explicit HDR colour (no atlas lookup).</summary>
        public static Material MakeFlatUnlit(string name, Color hdrColour, bool additive)
        {
            Shader unlit = FindShader("Universal Render Pipeline/Unlit", "Unlit/Color");
            var m = new Material(unlit) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdrColour);
            if (m.HasProperty("_Color")) m.SetColor("_Color", hdrColour);
            if (additive) MakeTransparent(m, BlendMode.One, BlendMode.One, RenderQueue.Transparent);
            return m;
        }
    }
}
