using UnityEngine;
using UnityEngine.Rendering;
using FormosaExpress.Core;

namespace FormosaExpress.Gameplay
{
    /// <summary>
    /// The pillar of light that marks a pickup or a drop-off. Built from additive geometry with
    /// a per-segment falloff, a spinning ground ring and a floating parcel.
    /// </summary>
    public sealed class DeliveryBeacon : MonoBehaviour
    {
        const float Height = 13f;
        const int Segments = 9;

        Transform _pillar;
        Transform _ring;
        Transform _innerRing;
        Transform _parcel;
        Light _light;
        Material _pillarMaterial;
        Material _ringMaterial;
        Material _parcelMaterial;
        Color _tint = Art.BeaconGreen;
        float _phase;
        float _urgency;
        bool _visible = true;

        public bool IsPickup { get; private set; }

        public static DeliveryBeacon Create(Transform parent, MaterialLibrary mats)
        {
            var go = new GameObject("DeliveryBeacon");
            go.transform.SetParent(parent, false);
            var beacon = go.AddComponent<DeliveryBeacon>();
            beacon.Build(mats);
            return beacon;
        }

        void Build(MaterialLibrary mats)
        {
            Palette pal = mats.Palette;

            // --- pillar: an open tube in stacked segments that fade out towards the top. Open
            // ended and slim, because additive geometry stacks up fast and a solid cylinder of
            // light just reads as a white smear once bloom gets hold of it.
            var pillarMesh = new MeshBuilder(pal);
            for (int i = 0; i < Segments; i++)
            {
                float t0 = i / (float)Segments;
                float t1 = (i + 1) / (float)Segments;
                float fade = Mathf.Pow(1f - t0, 2.1f);
                int slot = pal.Add(new Color(fade, fade, fade, 1f));
                float r0 = Mathf.Lerp(1.05f, 0.45f, t0);
                float r1 = Mathf.Lerp(1.05f, 0.45f, t1);

                pillarMesh.AddTube(new Vector3(0f, t0 * Height, 0f), r0, r1, (t1 - t0) * Height, 20, slot);
            }

            _pillarMaterial = new Material(mats.Additive) { name = "FE_BeaconPillar" };
            _pillar = Attach(pillarMesh, "Pillar", transform, _pillarMaterial);

            // --- ground rings.
            var ringMesh = new MeshBuilder(pal);
            int bright = pal.Add(Color.white);
            ringMesh.AddRing(Vector3.zero, 2.4f, 3.1f, 40, bright);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                ringMesh.AddQuad(
                    dir * 3.2f - Vector3.Cross(Vector3.up, dir) * 0.12f,
                    dir * 4.0f - Vector3.Cross(Vector3.up, dir) * 0.12f,
                    dir * 4.0f + Vector3.Cross(Vector3.up, dir) * 0.12f,
                    dir * 3.2f + Vector3.Cross(Vector3.up, dir) * 0.12f,
                    Vector3.up, bright);
            }

            _ringMaterial = new Material(mats.Additive) { name = "FE_BeaconRing" };
            _ring = Attach(ringMesh, "Ring", transform, _ringMaterial);
            _ring.localPosition = new Vector3(0f, 0.08f, 0f);

            var innerMesh = new MeshBuilder(pal);
            innerMesh.AddRing(Vector3.zero, 1.1f, 1.5f, 30, bright);
            _innerRing = Attach(innerMesh, "InnerRing", transform, _ringMaterial);
            _innerRing.localPosition = new Vector3(0f, 0.10f, 0f);

            // --- floating parcel, mirroring the icon in the HUD.
            var parcelMesh = new MeshBuilder(pal);
            int box = pal.Add(new Color(1.0f, 1.0f, 1.0f, 1f));
            int ribbon = pal.Add(new Color(0.4f, 0.4f, 0.4f, 1f));
            parcelMesh.AddBox(Vector3.zero, new Vector3(0.78f, 0.66f, 0.78f), box);
            parcelMesh.AddBox(new Vector3(0f, 0.37f, 0f), new Vector3(0.88f, 0.16f, 0.88f), box);
            parcelMesh.AddBox(Vector3.zero, new Vector3(0.20f, 0.70f, 0.82f), ribbon);
            parcelMesh.AddBox(Vector3.zero, new Vector3(0.82f, 0.70f, 0.20f), ribbon);
            parcelMesh.AddBox(new Vector3(0f, 0.50f, 0f), new Vector3(0.34f, 0.20f, 0.16f), ribbon);

            _parcelMaterial = new Material(mats.GlowHot) { name = "FE_BeaconParcel" };
            _parcel = Attach(parcelMesh, "Parcel", transform, _parcelMaterial);
            _parcel.localPosition = new Vector3(0f, 4.6f, 0f);

            // --- a real point light so the beacon spills colour onto the street.
            var lightGo = new GameObject("BeaconLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 16f;
            _light.intensity = 3.4f;
            _light.shadows = LightShadows.None;

            SetVisible(false);
        }

        Transform Attach(MeshBuilder mb, string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name + "_Mesh");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        public void Configure(Vector3 position, bool isPickup, Color tint)
        {
            transform.position = new Vector3(position.x, Tuning.CurbHeight + 0.02f, position.z);
            IsPickup = isPickup;
            _tint = tint;
            _phase = Random.value * 6f;
            SetVisible(true);
        }

        /// <summary>0 = plenty of time, 1 = about to expire. Drives the pulse rate and colour.</summary>
        public void SetUrgency(float urgency) => _urgency = Mathf.Clamp01(urgency);

        public void SetVisible(bool value)
        {
            _visible = value;
            if (gameObject.activeSelf != value) gameObject.SetActive(value);
        }

        void Update()
        {
            if (!_visible) return;

            float dt = Time.deltaTime;
            _phase += dt * Mathf.Lerp(1.6f, 5.2f, _urgency);

            // Colour shifts towards red as the clock runs down.
            Color urgent = Color.Lerp(_tint, Art.HudRed, _urgency * 0.85f);
            float pulse = 0.72f + 0.28f * Mathf.Sin(_phase * 2.4f);

            // Tuned against an open tube (one layer of additive geometry, not four): bright
            // enough to spot down a sunlit street, not enough to clip to white.
            SetColour(_pillarMaterial, urgent * (1.55f * pulse));
            SetColour(_ringMaterial, urgent * (2.30f * pulse));
            SetColour(_parcelMaterial, urgent * (2.6f + pulse * 0.6f));

            if (_light != null)
            {
                _light.color = urgent;
                _light.intensity = Mathf.Lerp(1.5f, 3.0f, pulse) * Mathf.Lerp(1f, 1.4f, _urgency);
            }

            // The outer ring spins, the inner one breathes.
            _ring.localRotation = Quaternion.Euler(0f, _phase * 34f, 0f);
            float breathe = 1f + 0.16f * Mathf.Sin(_phase * 3.1f);
            _innerRing.localScale = new Vector3(breathe, 1f, breathe);

            // Parcel bobs and turns.
            _parcel.localPosition = new Vector3(0f, 4.6f + Mathf.Sin(_phase * 1.7f) * 0.32f, 0f);
            _parcel.localRotation = Quaternion.Euler(0f, _phase * 40f, Mathf.Sin(_phase * 1.4f) * 6f);
        }

        static void SetColour(Material material, Color colour)
        {
            if (material == null) return;
            colour.a = 1f;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
        }
    }
}
