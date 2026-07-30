using UnityEngine;
using UnityEngine.Rendering;
using FormosaExpress.Core;

namespace FormosaExpress.Vehicle
{
    /// <summary>Paint scheme for a scooter, so the rival is legible at a glance.</summary>
    public struct ScooterLivery
    {
        public Color Body;
        public Color Bag;
        public Color Rider;

        public static ScooterLivery Player => new ScooterLivery
        {
            Body = Art.PlayerOrange,
            Bag = Art.PlayerBagBlue,
            Rider = Art.RiderGreen
        };

        public static ScooterLivery Rival => new ScooterLivery
        {
            Body = Art.RivalPurple,
            Bag = Art.RivalBagRed,
            Rider = Art.RivalRider
        };
    }

    /// <summary>
    /// Builds and animates the player's scooter, rider and delivery box. The chassis collider
    /// stays upright; everything visual leans, pitches and squats on this rig instead.
    /// </summary>
    public sealed class ScooterVisual : MonoBehaviour
    {
        ScooterController _controller;
        Transform _lean;
        Transform _frontWheel;
        Transform _rearWheel;
        Transform _handlebar;
        Transform _riderTorso;
        Transform _box;
        Transform _headlight;
        MeshRenderer _brakeLight;
        Material _brakeLightMaterial;
        Light _headlightLight;
        Light _riderFill;

        float _boxWobble;
        float _boxWobbleVelocity;

        public Transform ExhaustPoint { get; private set; }
        public Transform BoxAnchor => _box;

        public static ScooterVisual Create(ScooterController controller, MaterialLibrary mats,
            ScooterLivery? livery = null)
        {
            var root = new GameObject("Visual");
            root.transform.SetParent(controller.transform, false);
            var visual = root.AddComponent<ScooterVisual>();
            visual.Build(controller, mats, livery ?? ScooterLivery.Player);
            controller.VisualRoot = root.transform;
            return visual;
        }

        void Build(ScooterController controller, MaterialLibrary mats, ScooterLivery livery)
        {
            _controller = controller;
            Palette pal = mats.Palette;

            int orange = pal.Add(livery.Body);
            int orangeDark = pal.AddShaded(livery.Body, 0.68f);
            int blue = pal.Add(livery.Bag);
            int blueDark = pal.AddShaded(livery.Bag, 0.66f);
            int green = pal.Add(livery.Rider);
            int greenDark = pal.AddShaded(livery.Rider, 0.7f);
            int denim = pal.Add(new Color(0.22f, 0.28f, 0.42f));
            int denimDark = pal.AddShaded(new Color(0.22f, 0.28f, 0.42f), 0.72f);
            int skin = pal.Add(Art.SkinTones[1]);
            int rubber = pal.Add(new Color(0.08f, 0.08f, 0.09f));
            int chrome = pal.Add(new Color(0.82f, 0.85f, 0.88f));
            int dark = pal.Add(new Color(0.14f, 0.14f, 0.16f));
            int white = pal.Add(new Color(0.94f, 0.94f, 0.92f));
            int rimSlot = pal.Add(new Color(0.70f, 0.72f, 0.74f));

            _lean = new GameObject("Lean").transform;
            _lean.SetParent(transform, false);

            var body = new MeshBuilder(pal);

            // -------------------------------------------------------------- chassis
            // Floorboard and step-through frame.
            body.AddBox(new Vector3(0f, 0.30f, -0.05f), new Vector3(0.40f, 0.10f, 0.86f), dark);
            body.AddBox(new Vector3(0f, 0.36f, -0.05f), new Vector3(0.34f, 0.06f, 0.80f), rubber);

            // Front leg shield.
            body.AddTaperedBox(new Vector3(0f, 0.62f, 0.44f), new Vector3(0.42f, 0.72f, 0.30f), 0.72f, 0.85f,
                Quaternion.Euler(-12f, 0f, 0f), orange, orangeDark);

            // Rear body / engine cover.
            body.AddTaperedBox(new Vector3(0f, 0.55f, -0.48f), new Vector3(0.44f, 0.48f, 0.72f), 0.82f, 0.86f,
                Quaternion.identity, orange, orangeDark);
            body.AddBox(new Vector3(0f, 0.34f, -0.62f), new Vector3(0.38f, 0.26f, 0.42f), dark);

            // Seat.
            body.AddTaperedBox(new Vector3(0f, 0.83f, -0.30f), new Vector3(0.34f, 0.14f, 0.70f), 0.85f, 0.9f,
                Quaternion.Euler(-4f, 0f, 0f), dark, rubber);

            // A racing stripe down the side panels.
            for (int s = -1; s <= 1; s += 2)
                body.AddBox(new Vector3(s * 0.225f, 0.55f, -0.48f), new Vector3(0.02f, 0.09f, 0.62f), white);

            // Exhaust.
            body.AddCylinder(new Vector3(0.20f, 0.28f, -0.35f), 0.055f, 0.055f, 0.48f, 6,
                Quaternion.Euler(90f, 0f, 0f), chrome, chrome);

            // Rear rack for the delivery box.
            body.AddBox(new Vector3(0f, 0.94f, -0.66f), new Vector3(0.38f, 0.05f, 0.34f), chrome);
            for (int s = -1; s <= 1; s += 2)
                body.AddBeam(new Vector3(s * 0.16f, 0.80f, -0.60f), new Vector3(s * 0.16f, 0.94f, -0.66f), 0.035f, chrome);

            // Kickstand and footpegs.
            body.AddBeam(new Vector3(-0.20f, 0.30f, -0.20f), new Vector3(-0.30f, 0.06f, -0.28f), 0.035f, chrome);
            for (int s = -1; s <= 1; s += 2)
                body.AddBox(new Vector3(s * 0.24f, 0.32f, -0.22f), new Vector3(0.14f, 0.04f, 0.10f), rubber);

            AttachMesh(body, "Chassis", _lean, mats.Surface);

            // -------------------------------------------------------------- rider
            var rider = new MeshBuilder(pal);

            // Legs: bent, feet on the board.
            for (int s = -1; s <= 1; s += 2)
            {
                rider.AddBox(new Vector3(s * 0.11f, 0.60f, 0.10f), new Vector3(0.15f, 0.14f, 0.46f),
                    Quaternion.Euler(-72f, 0f, 0f), denim, denimDark, denimDark);
                rider.AddBox(new Vector3(s * 0.12f, 0.44f, -0.02f), new Vector3(0.14f, 0.36f, 0.15f),
                    Quaternion.Euler(18f, 0f, 0f), denim, denimDark, denimDark);
                rider.AddBox(new Vector3(s * 0.13f, 0.30f, 0.06f), new Vector3(0.13f, 0.09f, 0.26f), dark);
            }

            AttachMesh(rider, "Legs", _lean, mats.Surface);

            // Torso, arms and head ride on their own pivot so they can bob.
            _riderTorso = new GameObject("Torso").transform;
            _riderTorso.SetParent(_lean, false);
            _riderTorso.localPosition = new Vector3(0f, 0.86f, -0.16f);

            var torso = new MeshBuilder(pal);
            torso.AddTaperedBox(new Vector3(0f, 0.26f, 0.02f), new Vector3(0.40f, 0.54f, 0.28f), 1.05f, 1.0f,
                Quaternion.Euler(14f, 0f, 0f), green, greenDark);

            // Arms reaching for the bars.
            for (int s = -1; s <= 1; s += 2)
            {
                torso.AddBox(new Vector3(s * 0.20f, 0.34f, 0.20f), new Vector3(0.11f, 0.11f, 0.44f),
                    Quaternion.Euler(-24f, s * -7f, 0f), green, greenDark, greenDark);
                torso.AddBox(new Vector3(s * 0.24f, 0.24f, 0.44f), new Vector3(0.09f, 0.09f, 0.12f), skin);
            }

            // Head and helmet.
            torso.AddBox(new Vector3(0f, 0.60f, 0.04f), new Vector3(0.17f, 0.14f, 0.17f), skin);
            torso.AddTaperedBox(new Vector3(0f, 0.72f, 0.02f), new Vector3(0.28f, 0.26f, 0.30f), 0.72f, 0.72f,
                Quaternion.identity, green, greenDark);
            torso.AddBox(new Vector3(0f, 0.70f, 0.16f), new Vector3(0.19f, 0.09f, 0.03f), dark);
            torso.AddBox(new Vector3(0f, 0.79f, 0.15f), new Vector3(0.26f, 0.04f, 0.08f), greenDark);

            AttachMesh(torso, "Rider", _riderTorso, mats.Surface);

            // -------------------------------------------------------------- delivery box
            _box = new GameObject("DeliveryBox").transform;
            _box.SetParent(_lean, false);
            _box.localPosition = new Vector3(0f, 0.97f, -0.68f);

            var boxMesh = new MeshBuilder(pal);

            // Narrow and low enough that the rider's shoulders and helmet stay visible from
            // the chase camera - the box should read as cargo, not as the whole vehicle.
            const float bw = 0.52f, bh = 0.50f, bd = 0.44f;
            boxMesh.AddBox(new Vector3(0f, bh * 0.5f, 0f), new Vector3(bw, bh, bd), blue, blueDark, blueDark);
            boxMesh.AddBox(new Vector3(0f, bh + 0.025f, 0f), new Vector3(bw + 0.03f, 0.05f, bd + 0.03f), blueDark);

            // Corner ribs.
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                boxMesh.AddBox(new Vector3(sx * bw * 0.5f, bh * 0.5f, sz * bd * 0.5f),
                    new Vector3(0.04f, bh, 0.04f), blueDark);

            // Livery: a white label with a courier stripe, on both sides and the back.
            var liveryRng = new Rng(4242);
            AddLivery(boxMesh, new Vector3(-bw * 0.5f - 0.006f, bh * 0.56f, 0f), Vector3.left, Vector3.forward,
                0.30f, 0.20f, white, orange, dark, ref liveryRng);
            AddLivery(boxMesh, new Vector3(bw * 0.5f + 0.006f, bh * 0.56f, 0f), Vector3.right, Vector3.back,
                0.30f, 0.20f, white, orange, dark, ref liveryRng);
            AddLivery(boxMesh, new Vector3(0f, bh * 0.56f, -bd * 0.5f - 0.006f), Vector3.back, Vector3.right,
                0.34f, 0.20f, white, orange, dark, ref liveryRng);

            AttachMesh(boxMesh, "Box", _box, mats.Surface);

            // -------------------------------------------------------------- wheels
            _frontWheel = BuildWheel("FrontWheel", new Vector3(0f, 0.27f, 0.62f), pal, mats, rubber, rimSlot);
            _rearWheel = BuildWheel("RearWheel", new Vector3(0f, 0.27f, -0.62f), pal, mats, rubber, rimSlot);

            // Forks and swingarm bridge the wheels to the frame.
            var forks = new MeshBuilder(pal);
            for (int s = -1; s <= 1; s += 2)
                forks.AddBeam(new Vector3(s * 0.10f, 0.82f, 0.50f), new Vector3(s * 0.10f, 0.30f, 0.62f), 0.05f, chrome);
            forks.AddBeam(new Vector3(0f, 0.34f, -0.30f), new Vector3(0f, 0.28f, -0.62f), 0.07f, dark);
            AttachMesh(forks, "Forks", _lean, mats.Surface);

            // -------------------------------------------------------------- handlebar
            _handlebar = new GameObject("Handlebar").transform;
            _handlebar.SetParent(_lean, false);
            _handlebar.localPosition = new Vector3(0f, 1.02f, 0.52f);

            var bars = new MeshBuilder(pal);
            bars.AddBeam(new Vector3(-0.34f, 0f, 0f), new Vector3(0.34f, 0f, 0f), 0.045f, chrome);
            for (int s = -1; s <= 1; s += 2)
            {
                bars.AddCylinder(new Vector3(s * 0.24f, -0.03f, 0f), 0.032f, 0.032f, 0.12f, 6,
                    Quaternion.Euler(0f, 0f, 90f), dark, dark);
                bars.AddBox(new Vector3(s * 0.30f, 0.08f, 0.03f), new Vector3(0.09f, 0.06f, 0.04f), chrome);
            }

            bars.AddBox(new Vector3(0f, 0.02f, 0.06f), new Vector3(0.20f, 0.12f, 0.06f), dark);
            AttachMesh(bars, "Bars", _handlebar, mats.Surface);

            // -------------------------------------------------------------- lights
            var glowMesh = new MeshBuilder(pal);
            int headlightSlot = pal.Add(new Color(1.00f, 0.94f, 0.78f));
            glowMesh.AddBox(new Vector3(0f, 0.88f, 0.60f), new Vector3(0.26f, 0.16f, 0.05f), headlightSlot);
            GameObject headlightGo = AttachMesh(glowMesh, "Headlight", _lean, mats.GlowHot);
            _headlight = headlightGo != null ? headlightGo.transform : null;

            var brake = new MeshBuilder(pal);
            int brakeSlot = pal.Add(new Color(1.0f, 0.16f, 0.12f));
            brake.AddBox(new Vector3(0f, 0.66f, -0.83f), new Vector3(0.30f, 0.10f, 0.04f), brakeSlot);
            GameObject brakeGo = AttachMesh(brake, "BrakeLight", _lean, null);
            if (brakeGo != null)
            {
                _brakeLightMaterial = new Material(mats.GlowSoft) { name = "FE_BrakeLight" };
                _brakeLight = brakeGo.GetComponent<MeshRenderer>();
                _brakeLight.sharedMaterial = _brakeLightMaterial;
            }

            // A real light so the scooter throws illumination onto the road at night.
            var lightGo = new GameObject("HeadlightLight");
            lightGo.transform.SetParent(_lean, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.88f, 0.66f);
            _headlightLight = lightGo.AddComponent<Light>();
            _headlightLight.type = LightType.Spot;
            _headlightLight.color = new Color(1f, 0.93f, 0.78f);
            _headlightLight.intensity = 3.2f;
            _headlightLight.range = 26f;
            _headlightLight.spotAngle = 62f;
            _headlightLight.innerSpotAngle = 26f;
            _headlightLight.shadows = LightShadows.None;

            // A close, dim fill light so the hero reads as more than a silhouette on a dark
            // street. Without it the rider disappears completely on the night shifts.
            var fillGo = new GameObject("RiderFill");
            fillGo.transform.SetParent(_lean, false);
            fillGo.transform.localPosition = new Vector3(0f, 1.55f, -0.1f);
            _riderFill = fillGo.AddComponent<Light>();
            _riderFill.type = LightType.Point;
            _riderFill.color = new Color(1f, 0.88f, 0.74f);
            _riderFill.range = 6.2f;
            _riderFill.intensity = 0.6f;
            _riderFill.shadows = LightShadows.None;

            ExhaustPoint = new GameObject("ExhaustPoint").transform;
            ExhaustPoint.SetParent(_lean, false);
            ExhaustPoint.localPosition = new Vector3(0.20f, 0.28f, -0.62f);
            ExhaustPoint.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        /// <summary>
        /// A courier decal: white panel, orange stripe and a row of small marks that read as a
        /// wordmark at gameplay distance. Flat geometry rather than a texture, so it shares the
        /// city's single atlas material.
        /// </summary>
        static void AddLivery(MeshBuilder mb, Vector3 centre, Vector3 outward, Vector3 right,
            float width, float height, int panelSlot, int stripeSlot, int glyphSlot, ref Rng rng)
        {
            Vector3 up = Vector3.up;
            Vector3 offset = outward * 0.004f;

            mb.AddDoubleSidedQuad(centre + offset, right * (width * 0.5f), up * (height * 0.5f), panelSlot);

            // Courier stripe below the panel.
            mb.AddDoubleSidedQuad(centre + offset * 2f - up * (height * 0.5f + 0.045f),
                right * (width * 0.5f), up * 0.026f, stripeSlot);

            // A row of narrow marks with a baseline: enough to read as lettering, small enough
            // that it never resolves into a face.
            const int marks = 7;
            float span = width * 0.72f;
            float markHeight = height * 0.22f;

            for (int i = 0; i < marks; i++)
            {
                float t = (i + 0.5f) / marks - 0.5f;
                float h = markHeight * rng.Range(0.62f, 1f);
                Vector3 p = centre + offset * 3f + right * (t * span) + up * (height * 0.08f);
                mb.AddDoubleSidedQuad(p, right * (span / marks * 0.28f), up * (h * 0.5f), glyphSlot);
            }

            mb.AddDoubleSidedQuad(centre + offset * 3f - up * (height * 0.22f),
                right * (span * 0.5f), up * (height * 0.022f), glyphSlot);
        }

        Transform BuildWheel(string name, Vector3 localPosition, Palette pal, MaterialLibrary mats,
            int rubberSlot, int rimSlot)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(_lean, false);
            pivot.localPosition = localPosition;

            var mb = new MeshBuilder(pal);
            Quaternion axis = Quaternion.Euler(0f, 0f, 90f);
            mb.AddCylinder(new Vector3(-0.06f, 0f, 0f), 0.27f, 0.27f, 0.12f, 14, axis, rubberSlot, rubberSlot);
            mb.AddCylinder(new Vector3(-0.065f, 0f, 0f), 0.16f, 0.16f, 0.13f, 12, axis, rimSlot, rimSlot);

            // Spokes, so the wheel visibly spins.
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                var dir = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
                mb.AddBeam(-dir * 0.05f, dir * 0.23f, 0.035f, rimSlot);
            }

            AttachMesh(mb, name + "Mesh", pivot, mats.Surface);
            return pivot;
        }

        GameObject AttachMesh(MeshBuilder mb, string name, Transform parent, Material material)
        {
            if (mb.IsEmpty) return null;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name + "_Mesh");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mb.Clear();
            return go;
        }

        void LateUpdate()
        {
            if (_controller == null) return;
            float dt = Time.deltaTime;

            // Body roll and pitch, plus a suspension squat.
            float squat = Mathf.Lerp(0f, -0.085f, _controller.SuspensionCompression);
            _lean.localPosition = new Vector3(0f, squat, 0f);
            _lean.localRotation = Quaternion.Euler(_controller.VisualPitch, 0f, _controller.VisualLean);

            // Wheels spin with road speed; the front one also turns with the bars.
            var spin = Quaternion.Euler(_controller.WheelSpin, 0f, 0f);
            _rearWheel.localRotation = spin;
            float steerAngle = _controller.SteerInput * 26f;
            _frontWheel.localRotation = Quaternion.Euler(0f, steerAngle, 0f) * spin;
            _handlebar.localRotation = Quaternion.Euler(0f, steerAngle * 0.85f, 0f);

            // The rider tucks forward at speed and sits up while braking.
            float tuck = _controller.Speed01 * 9f - _controller.BrakeInput * 7f;
            _riderTorso.localRotation = Quaternion.Euler(tuck, -_controller.SteerInput * 5f, 0f);

            // The delivery box rocks on its rack: a spring driven by lateral force.
            float drive = -_controller.SteerInput * _controller.Speed01 * 26f;
            float stiffness = 190f, damping = 17f;
            _boxWobbleVelocity += (drive - _boxWobble) * stiffness * dt;
            _boxWobbleVelocity -= _boxWobbleVelocity * damping * dt;
            _boxWobble += _boxWobbleVelocity * dt;
            _boxWobble = Mathf.Clamp(_boxWobble, -14f, 14f);
            _box.localRotation = Quaternion.Euler(-_boxWobble * 0.25f, 0f, _boxWobble);

            // Brake light and headlight react to input.
            if (_brakeLightMaterial != null)
            {
                float glow = Mathf.Lerp(0.6f, 6.5f, _controller.BrakeInput);
                _brakeLightMaterial.SetColor("_BaseColor", new Color(glow, glow, glow, 1f));
            }

            float night = Services.Director != null ? Services.Director.NightFactor : 0.5f;

            if (_headlightLight != null)
            {
                _headlightLight.intensity = Mathf.Lerp(1.1f, 4.4f, night);
                _headlightLight.enabled = night > 0.05f;
            }

            if (_riderFill != null)
            {
                _riderFill.intensity = Mathf.Lerp(0.3f, 2.8f, night);
                _riderFill.enabled = night > 0.1f;
            }
        }
    }
}
