using UnityEngine;
using UnityEngine.Rendering;
using FormosaExpress.Core;
using FormosaExpress.Vehicle;

namespace FormosaExpress.Fx
{
    /// <summary>
    /// All the small reactive effects: exhaust, drift smoke, tyre marks, boost trails, impact
    /// sparks and delivery bursts. Everything is built at runtime from procedural textures.
    /// </summary>
    public sealed class FxDirector : MonoBehaviour
    {
        Material _additiveParticle;
        Material _smokeParticle;
        Material _skidMaterial;

        ParticleSystem _exhaust;
        ParticleSystem _driftSmoke;
        ParticleSystem _boostFlame;
        ParticleSystem _sparks;
        ParticleSystem _dust;
        ParticleSystem _celebration;
        ParticleSystem _speedLines;

        TrailRenderer _skidLeft;
        TrailRenderer _skidRight;

        ScooterController _player;
        Transform _exhaustPoint;
        float _skidFade;

        public void Initialise(ScooterController player, ScooterVisual visual)
        {
            _player = player;
            _exhaustPoint = visual != null ? visual.ExhaustPoint : player.transform;

            BuildMaterials();
            BuildEmitters();
            BuildSkidMarks();
        }

        void BuildMaterials()
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null) particleShader = Shader.Find("Sprites/Default");

            Texture2D glow = TextureFactory.SoftGlow(64, 2.4f).texture;

            _additiveParticle = new Material(particleShader) { name = "FE_ParticleAdditive" };
            ConfigureParticleMaterial(_additiveParticle, glow, BlendMode.One, BlendMode.One);

            _smokeParticle = new Material(particleShader) { name = "FE_ParticleSmoke" };
            ConfigureParticleMaterial(_smokeParticle, glow, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Sprites/Default");
            _skidMaterial = new Material(unlit) { name = "FE_SkidMark" };
            ConfigureParticleMaterial(_skidMaterial, glow, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);
            if (_skidMaterial.HasProperty("_BaseColor")) _skidMaterial.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.06f, 0.8f));
        }

        static void ConfigureParticleMaterial(Material m, Texture texture, BlendMode src, BlendMode dst)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", src == BlendMode.One ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)src);
            m.SetInt("_DstBlend", (int)dst);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        // ------------------------------------------------------------------ emitters

        ParticleSystem MakeEmitter(string name, Transform parent, Material material, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;

            var main = ps.main;
            main.maxParticles = maxParticles;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ps.Stop();
            return ps;
        }

        void BuildEmitters()
        {
            // --- exhaust: thin grey puffs, always trickling while the engine runs.
            _exhaust = MakeEmitter("Exhaust", _exhaustPoint, _smokeParticle, 90);
            {
                var main = _exhaust.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 2.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.55f, 0.55f, 0.30f));
                main.gravityModifier = -0.08f;

                var emission = _exhaust.emission;
                emission.rateOverTime = 0f;

                var shape = _exhaust.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 12f;
                shape.radius = 0.04f;

                var sol = _exhaust.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 2.4f));

                var col = _exhaust.colorOverLifetime;
                col.enabled = true;
                col.color = Fade(new Color(0.6f, 0.6f, 0.6f), 0.35f);

                _exhaust.Play();
            }

            // --- drift smoke: thicker, from the rear tyre.
            _driftSmoke = MakeEmitter("DriftSmoke", _player.transform, _smokeParticle, 220);
            {
                _driftSmoke.transform.localPosition = new Vector3(0f, 0.12f, -0.62f);

                var main = _driftSmoke.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.15f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.72f, 0.70f, 0.68f, 0.40f));
                main.gravityModifier = -0.05f;

                var emission = _driftSmoke.emission;
                emission.rateOverTime = 0f;

                var shape = _driftSmoke.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.18f;

                var sol = _driftSmoke.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.6f));

                var col = _driftSmoke.colorOverLifetime;
                col.enabled = true;
                col.color = Fade(new Color(0.8f, 0.78f, 0.76f), 0.45f);

                _driftSmoke.Play();
            }

            // --- boost flame: hot additive jets.
            _boostFlame = MakeEmitter("BoostFlame", _exhaustPoint, _additiveParticle, 160);
            {
                var main = _boostFlame.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.30f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1.6f, 0.85f, 0.30f, 1f), new Color(1.2f, 0.35f, 0.95f, 1f));

                var emission = _boostFlame.emission;
                emission.rateOverTime = 0f;

                var shape = _boostFlame.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 8f;
                shape.radius = 0.05f;

                var col = _boostFlame.colorOverLifetime;
                col.enabled = true;
                col.color = Fade(Color.white, 1f);

                _boostFlame.Play();
            }

            // --- impact sparks.
            _sparks = MakeEmitter("Sparks", transform, _additiveParticle, 200);
            {
                var main = _sparks.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 13f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(2.2f, 1.5f, 0.4f, 1f), new Color(2.4f, 0.7f, 0.2f, 1f));
                main.gravityModifier = 1.4f;

                var emission = _sparks.emission;
                emission.rateOverTime = 0f;

                var shape = _sparks.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.12f;

                var trails = _sparks.trails;
                trails.enabled = true;
                trails.ratio = 0.7f;
                trails.lifetime = new ParticleSystem.MinMaxCurve(0.18f);
                trails.dieWithParticles = true;

                var renderer = _sparks.GetComponent<ParticleSystemRenderer>();
                renderer.trailMaterial = _additiveParticle;
            }

            // --- landing dust.
            _dust = MakeEmitter("Dust", transform, _smokeParticle, 160);
            {
                var main = _dust.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.95f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.68f, 0.64f, 0.58f, 0.5f));

                var emission = _dust.emission;
                emission.rateOverTime = 0f;

                var shape = _dust.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.5f;
                shape.radiusThickness = 0f;

                var col = _dust.colorOverLifetime;
                col.enabled = true;
                col.color = Fade(new Color(0.75f, 0.72f, 0.66f), 0.55f);
            }

            // --- delivery celebration.
            _celebration = MakeEmitter("Celebration", transform, _additiveParticle, 260);
            {
                var main = _celebration.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.34f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1.8f, 1.5f, 0.4f, 1f), new Color(0.5f, 1.9f, 1.1f, 1f));
                main.gravityModifier = 0.85f;

                var emission = _celebration.emission;
                emission.rateOverTime = 0f;

                var shape = _celebration.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.4f;

                var col = _celebration.colorOverLifetime;
                col.enabled = true;
                col.color = Fade(Color.white, 1f);
            }

            // --- speed lines streaming past the camera while boosting.
            _speedLines = MakeEmitter("SpeedLines", transform, _additiveParticle, 120);
            {
                var main = _speedLines.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.55f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.95f, 1.2f, 0.55f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = _speedLines.emission;
                emission.rateOverTime = 0f;

                var shape = _speedLines.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 5.5f;
                shape.radiusThickness = 0.35f;

                var renderer = _speedLines.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0f;
                renderer.lengthScale = 5.5f;
                renderer.cameraVelocityScale = 0.55f;

                var col = _speedLines.colorOverLifetime;
                col.enabled = true;
                col.color = Fade(Color.white, 1f);

                _speedLines.Play();
            }
        }

        static ParticleSystem.MinMaxGradient Fade(Color colour, float peakAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(colour, 0f), new GradientColorKey(colour, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peakAlpha, 0.18f),
                    new GradientAlphaKey(0f, 1f)
                });

            return new ParticleSystem.MinMaxGradient(gradient);
        }

        void BuildSkidMarks()
        {
            _skidLeft = MakeSkid("SkidLeft", new Vector3(-0.06f, 0.035f, -0.62f));
            _skidRight = MakeSkid("SkidRight", new Vector3(0.06f, 0.035f, -0.62f));
        }

        TrailRenderer MakeSkid(string name, Vector3 localOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_player.transform, false);
            go.transform.localPosition = localOffset;

            var trail = go.AddComponent<TrailRenderer>();
            trail.material = _skidMaterial;
            trail.time = 5.5f;
            trail.minVertexDistance = 0.22f;
            trail.widthMultiplier = 0.22f;
            trail.alignment = LineAlignment.TransformZ;
            trail.autodestruct = false;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            trail.numCapVertices = 2;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;

            // Lay it flat against the road.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return trail;
        }

        // ------------------------------------------------------------------ tick

        void Update()
        {
            if (_player == null) return;
            float dt = Time.deltaTime;

            float speed01 = _player.Speed01;

            // Exhaust puffs harder under load and when the engine is cold-idling.
            var exhaustEmission = _exhaust.emission;
            exhaustEmission.rateOverTime = _player.ControlEnabled
                ? Mathf.Lerp(6f, 30f, _player.ThrottleInput) + speed01 * 12f
                : 3f;

            // Drift smoke and tyre marks.
            bool marking = _player.IsGrounded &&
                           (_player.IsDrifting || (_player.BrakeInput > 0.55f && speed01 > 0.28f) ||
                            (_player.ThrottleInput > 0.7f && speed01 < 0.22f && _player.ForwardSpeed > 0.4f));

            var driftEmission = _driftSmoke.emission;
            driftEmission.rateOverTime = marking ? Mathf.Lerp(30f, 130f, speed01) : 0f;

            _skidFade = MathX.ExpSmooth(_skidFade, marking ? 1f : 0f, 14f, dt);
            _skidLeft.emitting = marking;
            _skidRight.emitting = marking;

            // Boost jets and speed lines.
            var boostEmission = _boostFlame.emission;
            boostEmission.rateOverTime = _player.IsBoosting ? 150f : 0f;

            var linesEmission = _speedLines.emission;
            float intensity = _player.IsBoosting ? 1f : Mathf.InverseLerp(0.72f, 1f, speed01);
            linesEmission.rateOverTime = intensity * 70f;

            if (Services.Camera != null && Services.Camera.Camera != null)
                _speedLines.transform.SetPositionAndRotation(
                    Services.Camera.Camera.transform.position + Services.Camera.Camera.transform.forward * 6f,
                    Services.Camera.Camera.transform.rotation);
        }

        // ------------------------------------------------------------------ one-shots

        public void PlayImpact(Vector3 position, Vector3 normal, float severity)
        {
            _sparks.transform.position = position;
            _sparks.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.01f ? normal : Vector3.up);
            _sparks.Emit(Mathf.RoundToInt(Mathf.Lerp(6f, 42f, severity)));

            _dust.transform.position = position;
            _dust.Emit(Mathf.RoundToInt(Mathf.Lerp(3f, 18f, severity)));

            Services.Camera?.Shake(Mathf.Lerp(0.18f, 1.1f, severity));
        }

        public void PlayLanding(Vector3 position, float severity)
        {
            _dust.transform.position = position;
            _dust.Emit(Mathf.RoundToInt(Mathf.Lerp(6f, 30f, severity)));
            Services.Camera?.Shake(Mathf.Lerp(0.10f, 0.55f, severity));
        }

        public void PlayDelivery(Vector3 position)
        {
            _celebration.transform.position = position + Vector3.up * 1.2f;
            _celebration.Emit(70);
        }

        public void ClearSkids()
        {
            _skidLeft.Clear();
            _skidRight.Clear();
        }
    }
}
