using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FormosaExpress.Core;

namespace FormosaExpress.Fx
{
    /// <summary>
    /// Sky, sun, fog and post-processing. A single "time of day" dial drives the whole look, so
    /// a shift can open in bright midday and later shifts slide through golden hour into a
    /// neon-lit night without any authored assets.
    /// </summary>
    public sealed class EnvironmentDirector : MonoBehaviour
    {
        Light _sun;
        Light _fill;
        Material _skybox;
        Volume _volume;
        VolumeProfile _profile;

        Bloom _bloom;
        Tonemapping _tonemapping;
        ColorAdjustments _colour;
        Vignette _vignette;
        FilmGrain _grain;
        ChromaticAberration _aberration;
        MotionBlur _motionBlur;

        readonly Texture2D[] _skyCache = new Texture2D[7];
        float _night = float.NaN;   // sentinel: guarantees the first ApplyNight actually applies
        float _targetNight;
        float _speedBlur;

        public float NightFactor => Mathf.Max(0f, _night);

        public void Initialise()
        {
            BuildLights();
            BuildSky();
            BuildPostProcessing();
            SetNightFactor(0f, immediate: true);
        }

        void BuildLights()
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.72f;
            _sun.shadowBias = 0.05f;
            _sun.shadowNormalBias = 0.35f;

            // Low western sun: long shadows down the street, matching the reference framing.
            sunGo.transform.rotation = Quaternion.Euler(16f, 108f, 0f);

            // A cool fill from the opposite side keeps shadowed facades from going black.
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(transform, false);
            _fill = fillGo.AddComponent<Light>();
            _fill.type = LightType.Directional;
            _fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(34f, -76f, 0f);
        }

        void BuildSky()
        {
            Shader panoramic = Shader.Find("Skybox/Panoramic");
            if (panoramic != null)
            {
                _skybox = new Material(panoramic) { name = "FE_Sky" };
                _skybox.SetFloat("_Mapping", 1f);         // latitude-longitude layout
                _skybox.SetFloat("_ImageType", 0f);       // 360 degrees
                _skybox.SetFloat("_MirrorOnBack", 0f);
                _skybox.SetFloat("_Exposure", 1.15f);
            }
            else
            {
                Shader procedural = Shader.Find("Skybox/Procedural");
                if (procedural != null) _skybox = new Material(procedural) { name = "FE_SkyProcedural" };
            }

            if (_skybox != null) RenderSettings.skybox = _skybox;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
        }

        void BuildPostProcessing()
        {
            var go = new GameObject("PostProcessing");
            go.transform.SetParent(transform, false);

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "FE_VolumeProfile";

            _bloom = _profile.Add<Bloom>(true);
            _bloom.intensity.Override(1.35f);
            _bloom.threshold.Override(0.95f);
            _bloom.scatter.Override(0.68f);
            _bloom.clamp.Override(48f);
            _bloom.highQualityFiltering.Override(true);
            _bloom.tint.Override(new Color(1f, 0.95f, 0.9f));

            // Neutral rather than ACES: ACES crushes the midtones, and this game lives in the
            // midtones - dusk facades lit by neon.
            _tonemapping = _profile.Add<Tonemapping>(true);
            _tonemapping.mode.Override(TonemappingMode.Neutral);

            _colour = _profile.Add<ColorAdjustments>(true);
            _colour.postExposure.Override(0.42f);
            _colour.contrast.Override(10f);
            _colour.saturation.Override(14f);
            // A warm filter pushes the whole frame towards the golden-hour look.
            _colour.colorFilter.Override(new Color(1f, 0.955f, 0.885f));

            _vignette = _profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.28f);
            _vignette.smoothness.Override(0.42f);

            _grain = _profile.Add<FilmGrain>(true);
            _grain.type.Override(FilmGrainLookup.Thin1);
            _grain.intensity.Override(0.18f);
            _grain.response.Override(0.75f);

            _aberration = _profile.Add<ChromaticAberration>(true);
            _aberration.intensity.Override(0.04f);

            // Camera-only motion blur smears the entire frame when the chase camera swings, so
            // it stays subtle even flat out: just enough to feel the speed.
            _motionBlur = _profile.Add<MotionBlur>(true);
            _motionBlur.mode.Override(MotionBlurMode.CameraOnly);
            _motionBlur.quality.Override(MotionBlurQuality.Medium);
            _motionBlur.intensity.Override(0.02f);

            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1f;
            _volume.sharedProfile = _profile;
        }

        /// <summary>-1 = bright midday, 0 = golden hour, 1 = full night.</summary>
        public void SetNightFactor(float night, bool immediate = false)
        {
            _targetNight = Mathf.Clamp(night, -1f, 1f);
            if (immediate) ApplyNight(_targetNight);
        }

        // Blends across three anchors instead of one Lerp: day at t=-1, golden-hour dusk at
        // t=0, night at t=1. Mathf/Color.Lerp both clamp their t to 0..1, so feeding a negative
        // "night" straight into the old two-anchor lerps would just freeze everything at dusk.
        static float Blend(float day, float dusk, float night, float t) =>
            t <= 0f ? Mathf.Lerp(dusk, day, -t) : Mathf.Lerp(dusk, night, t);

        static Color Blend(Color day, Color dusk, Color night, float t) =>
            t <= 0f ? Color.Lerp(dusk, day, -t) : Color.Lerp(dusk, night, t);

        void ApplyNight(float night)
        {
            if (Mathf.Approximately(_night, night)) return;
            _night = night;

            Color sunColour = Blend(Art.SunNoon, Art.SunDay, Art.SunNight, night);
            _sun.color = sunColour;
            _sun.intensity = Blend(2.35f, 2.05f, 0.35f, night);

            // Overhead at noon, low and warm at dusk, low and cool at night.
            float pitch = Blend(58f, 16f, 8f, night);
            float yaw = Blend(140f, 108f, 122f, night);
            _sun.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            _fill.color = Blend(new Color(0.78f, 0.85f, 0.96f), new Color(0.62f, 0.74f, 0.98f),
                new Color(0.32f, 0.38f, 0.70f), night);
            _fill.intensity = Blend(1.05f, 0.85f, 0.34f, night);

            Color horizon = Blend(Art.SkyNoonHorizon, Art.SkyDayHorizon, Art.SkyNightHorizon, night);
            Color zenith = Blend(Art.SkyNoonZenith, Art.SkyDayZenith, Art.SkyNightZenith, night);
            Color ambient = Blend(Art.AmbientNoon, Art.AmbientDay, Art.AmbientNight, night);

            // Shaded facades on a narrow street get almost no direct light, so the ambient rig
            // is doing most of the work here.
            RenderSettings.ambientSkyColor = zenith * Blend(1.35f, 1.15f, 0.55f, night);
            RenderSettings.ambientEquatorColor = ambient * Blend(1.7f, 1.5f, 1.0f, night);
            RenderSettings.ambientGroundColor = ambient * 0.55f;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fogColor = Color.Lerp(horizon * 0.92f, horizon * 0.5f, Mathf.Clamp01(night));
            RenderSettings.fogDensity = Blend(0.0016f, 0.0026f, 0.0044f, night);

            // Swap in the matching sky, cached by quantised time-of-day bucket.
            if (_skybox != null && _skybox.HasProperty("_MainTex"))
            {
                float u = (night + 1f) * 0.5f;
                int bucket = Mathf.Clamp(Mathf.RoundToInt(u * (_skyCache.Length - 1)), 0, _skyCache.Length - 1);
                if (_skyCache[bucket] == null)
                {
                    float bucketTime = bucket / (float)(_skyCache.Length - 1) * 2f - 1f;
                    _skyCache[bucket] = TextureFactory.SkyPanorama(bucketTime);
                }

                _skybox.SetTexture("_MainTex", _skyCache[bucket]);
                _skybox.SetFloat("_Exposure", Blend(1.30f, 1.15f, 0.95f, night));
            }
            else if (_skybox != null && _skybox.HasProperty("_AtmosphereThickness"))
            {
                _skybox.SetFloat("_AtmosphereThickness", Blend(3.0f, 2.4f, 0.6f, night));
                _skybox.SetColor("_SkyTint", zenith);
                _skybox.SetColor("_GroundColor", ambient);
                _skybox.SetFloat("_Exposure", Blend(1.40f, 1.25f, 0.55f, night));
            }

            // Neon should read stronger against a darker sky, and barely read at all at noon.
            if (_bloom != null)
            {
                _bloom.intensity.Override(Blend(0.85f, 1.15f, 1.85f, night));
                _bloom.threshold.Override(Blend(1.25f, 1.05f, 0.78f, night));
            }

            if (_colour != null)
            {
                _colour.postExposure.Override(Blend(0.10f, 0.18f, 0.34f, night));
                _colour.saturation.Override(Blend(18f, 14f, 22f, night));
            }

            DynamicGI.UpdateEnvironment();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (!Mathf.Approximately(_night, _targetNight))
                ApplyNight(MathX.ExpSmooth(_night, _targetNight, 2.5f, dt));

            // Camera effects that respond to how fast the rider is going.
            var player = Services.Player;
            if (player == null) return;

            float intensity = player.IsBoosting ? 1f : Mathf.InverseLerp(0.65f, 1f, player.Speed01);
            _speedBlur = MathX.ExpSmooth(_speedBlur, intensity, 5f, dt);

            if (_motionBlur != null) _motionBlur.intensity.Override(Mathf.Lerp(0.015f, 0.085f, _speedBlur));
            if (_aberration != null) _aberration.intensity.Override(Mathf.Lerp(0.03f, 0.16f, _speedBlur));
            if (_vignette != null) _vignette.intensity.Override(Mathf.Lerp(0.26f, 0.38f, _speedBlur));
        }
    }
}
