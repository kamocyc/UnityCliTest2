using UnityEngine;
using UnityEngine.Rendering.Universal;
using FormosaExpress.Audio;
using FormosaExpress.City;
using FormosaExpress.Fx;
using FormosaExpress.Gameplay;
using FormosaExpress.Traffic;
using FormosaExpress.UI;
using FormosaExpress.Vehicle;

namespace FormosaExpress.Core
{
    /// <summary>
    /// The single entry point. The scene contains only this component: the city, the scooter,
    /// the traffic, the HUD and every material and sound are generated here at start-up. That
    /// keeps the whole game reproducible from source with no binary assets to drift out of date.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("World")]
        [Tooltip("Change this for a completely different city layout.")]
        public int CitySeed = 20260730;

        [Header("Debug")]
        public bool SkipTitle;
        public bool LogBuildStats = true;

        void Awake()
        {
            Services.Reset();
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 1;

            Services.Save = SaveSystem.Load();
            Localization.SetLanguage((Language)Mathf.Clamp(Services.Save.language, 0, 3));
            Localization.Changed += () => { if (Services.Save != null) Services.Save.language = (int)Localization.Current; };

            // The loading screen is built and shown before anything else - a Screen Space Overlay
            // canvas needs no camera, so it can render for a frame while the coroutine below still
            // has the whole city, traffic and audio synthesis ahead of it.
            var loadingGo = new GameObject("_Loading");
            loadingGo.transform.SetParent(transform, false);
            LoadingScreen loading = loadingGo.AddComponent<LoadingScreen>();
            loading.Build();

            StartCoroutine(Boot(loading));
        }

        System.Collections.IEnumerator Boot(LoadingScreen loading)
        {
            yield return null;   // let the loading screen actually render before the freeze

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            ConfigurePhysics();

            Services.Input = new InputRouter();
            Services.Palette = new Palette();
            Services.Materials = new MaterialLibrary(Services.Palette);

            var systems = new GameObject("_Systems");
            systems.transform.SetParent(transform, false);

            EnvironmentDirector environment = Create<EnvironmentDirector>("Environment", systems.transform);
            environment.Initialise();

            loading.SetStatus(Localization.T("Laying out the streets..."));
            yield return null;

            CityModel city = BuildCity(out CityBuilder layout);
            Services.City = city;

            GetStartPose(city, out Vector3 startPosition, out float startYaw);

            ScooterController player = BuildPlayer(startPosition, startYaw, out ScooterVisual visual);
            Services.Player = player;

            Camera camera = BuildCamera(player);

            loading.SetStatus(Localization.T("Building traffic..."));
            yield return null;

            Services.Traffic = Create<TrafficSystem>("Traffic", systems.transform);
            Services.Traffic.Initialise(city, Services.Materials, CitySeed);

            Services.Pedestrians = Create<PedestrianSystem>("Pedestrians", systems.transform);
            Services.Pedestrians.Initialise(city, Services.Materials, CitySeed);

            Services.Routes = Create<RouteService>("Routes", systems.transform);
            Services.Routes.Initialise(Services.Materials);

            Services.Combo = Create<ComboSystem>("Combo", systems.transform);

            Services.Orders = Create<OrderManager>("Orders", systems.transform);
            Services.Orders.Initialise(city, Services.Materials, CitySeed);

            Services.Rival = RivalCourier.Create(systems.transform, Services.Materials);

            Services.Fx = Create<FxDirector>("Fx", systems.transform);
            Services.Fx.Initialise(player, visual);

            loading.SetStatus(Localization.T("Synthesising audio..."));
            yield return null;

            Services.Audio = Create<AudioDirector>("Audio", systems.transform);
            Services.Audio.Initialise();

            loading.SetStatus(Localization.T("Assembling the HUD..."));
            yield return null;

            var uiRoot = new GameObject("_UI");
            uiRoot.transform.SetParent(transform, false);

            Services.Hud = Create<HudRoot>("Hud", uiRoot.transform);
            Services.Hud.Build(city, layout);

            Services.Screens = Create<ScreenStack>("Screens", uiRoot.transform);
            Services.Screens.Build();

            Services.Director = Create<GameDirector>("Director", systems.transform);
            Services.Director.Initialise(environment, startPosition, startYaw);

            // Any colour registered while building the scooter, traffic or UI has to reach the
            // shared atlas, so commit once more now that everything exists.
            Services.Materials.CommitPalette();
            Services.Ready = true;

            Destroy(loading.gameObject);

            stopwatch.Stop();
            if (LogBuildStats)
                Debug.Log($"[FormosaExpress] Built city seed {CitySeed} in {stopwatch.ElapsedMilliseconds} ms: "
                          + $"{city.Nodes.Count} junctions, {city.Edges.Count} roads, {layout.Lots.Count} buildings, "
                          + $"{city.Sites.Count} venues, {city.Paths.Count} lanes, "
                          + $"{Services.Palette.Count} palette slots.");
        }

        void Update()
        {
            Services.Input?.Poll(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            if (Services.Save != null) SaveSystem.Save(Services.Save);
            Services.Reset();
        }

        // ------------------------------------------------------------------ construction

        static T Create<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        static void ConfigurePhysics()
        {
            Physics.gravity = new Vector3(0f, -Tuning.Gravity, 0f);
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;

            int ground = LayerMask.NameToLayer(Tuning.LayerGround);
            int building = LayerMask.NameToLayer(Tuning.LayerBuilding);
            int traffic = LayerMask.NameToLayer(Tuning.LayerTraffic);
            int pedestrian = LayerMask.NameToLayer(Tuning.LayerPedestrian);
            int prop = LayerMask.NameToLayer(Tuning.LayerProp);

            // Traffic and pedestrians are kinematic and steered by their own logic; only the
            // player needs to collide with them, so everything else is switched off.
            void Ignore(int a, int b)
            {
                if (a < 0 || b < 0) return;
                Physics.IgnoreLayerCollision(a, b, true);
            }

            Ignore(traffic, traffic);
            Ignore(traffic, pedestrian);
            Ignore(traffic, ground);
            Ignore(traffic, building);
            Ignore(traffic, prop);
            Ignore(pedestrian, pedestrian);
            Ignore(pedestrian, ground);
            Ignore(pedestrian, building);
            Ignore(pedestrian, prop);
        }

        CityModel BuildCity(out CityBuilder layout)
        {
            layout = new CityBuilder();
            CityModel model = layout.Build(CitySeed);

            var assembler = new CityAssembler();
            assembler.Assemble(model, layout, Services.Materials, Tuning.NightFactor(1));
            assembler.Root.transform.SetParent(transform, false);

            return model;
        }

        static void GetStartPose(CityModel city, out Vector3 position, out float yaw)
        {
            // Start in the middle of the map, in the correct lane, facing down the street.
            TrafficPath best = null;
            float bestDistance = float.MaxValue;

            foreach (TrafficPath path in city.Paths)
            {
                if (path.IsConnector || path.Length < 24f) continue;

                Vector3 mid = path.Sample(path.Length * 0.5f, out _);
                float distance = mid.sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = path;
            }

            if (best == null)
            {
                position = new Vector3(0f, 1f, 0f);
                yaw = 0f;
                return;
            }

            position = best.Sample(best.Length * 0.35f, out Vector3 tangent) + Vector3.up * 0.75f;
            yaw = MathX.SignedYawTo(Vector3.forward, tangent);
        }

        ScooterController BuildPlayer(Vector3 position, float yaw, out ScooterVisual visual)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.layer = LayerMask.NameToLayer(Tuning.LayerPlayer);
            go.tag = "Player";

            go.AddComponent<Rigidbody>();
            var controller = go.AddComponent<ScooterController>();
            controller.ApplyStats(VehicleStats.From(Services.Save));

            visual = ScooterVisual.Create(controller, Services.Materials);
            controller.Teleport(position, yaw);
            return controller;
        }

        Camera BuildCamera(ScooterController player)
        {
            var go = new GameObject("MainCamera");
            go.transform.SetParent(transform, false);
            go.tag = "MainCamera";

            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.18f;
            camera.farClipPlane = 1400f;   // far enough to reach the outer skyline rings
            camera.fieldOfView = Tuning.CamFovBase;
            camera.allowHDR = true;
            camera.allowMSAA = true;

            var urp = go.GetComponent<UniversalAdditionalCameraData>();
            if (urp == null) urp = go.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            urp.antialiasingQuality = AntialiasingQuality.High;
            urp.renderShadows = true;
            urp.requiresDepthOption = CameraOverrideOption.On;

            go.AddComponent<AudioListener>();

            var chase = go.AddComponent<ChaseCamera>();
            chase.Initialise(camera, player);
            Services.Camera = chase;

            return camera;
        }
    }
}
