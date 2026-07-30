# Formosa Express

An arcade scooter-delivery game set in a Taiwanese night-market district, built in Unity 6
(URP). Ride a delivery scooter through dense traffic, collect food from shops and get it to
customers before the clock runs out — without shaking the food to pieces on the way.

## How to run it

Open `Assets/FormosaExpress/Scenes/FormosaExpress.unity` and press Play. That scene contains a
single GameObject.

## The one architectural rule

**Everything is generated at runtime. There are no art, audio or prefab assets.**

The city, the scooter, the traffic, every material, every UI sprite, the sky and every sound are
built in code during `GameBootstrap.Awake()`. This was a deliberate constraint and it shapes the
whole codebase:

- Geometry is assembled through `Core/MeshBuilder`, which bakes flat-shaded triangles into
  combined meshes.
- Colour never lives in a material. `Core/Palette` is a 64x64 texture atlas where each pixel is
  one colour; geometry stores a UV pointing at its pixel. That is why an entire city block —
  buildings, signage, market stalls, parked scooters — collapses into **one mesh with one
  material**, and why the whole city renders in a few hundred draw calls.
- Consequently, any new colour must be registered with `Palette.Add()` **before**
  `MaterialLibrary.CommitPalette()` runs, or it will not reach the GPU. Bootstrap commits twice:
  once after the city, once after everything else.
- Audio is synthesised offline into `AudioClip`s by `Audio/AudioSynth` — engine tone, horn,
  crash, coins, and a four-bar music loop.
- Signage is not textured. Faux CJK glyphs are drawn as neon **stroke geometry**
  (`BuildingFactory.AddGlyph`), which is why the signs bloom properly.

### Winding convention

`MeshBuilder` treats `Cross(b - a, c - a)` as the outward normal, matching Unity's front-face
winding. Radial helpers (`AddCylinder`, `AddTube`, `AddDisc`, `AddRing`) have to be wound "up the
wall, then round" to face outwards. Getting this backwards silently back-face-culls the geometry;
it is the single easiest mistake to make in this codebase.

## Layout

```
Assets/FormosaExpress/Scripts/
  Core/       MeshBuilder, Palette, MaterialLibrary, TextureFactory, InputRouter,
              SaveSystem, Services (locator), Tuning + Art (all balance and art direction),
              GameBootstrap (the entry point)
  City/       CityBuilder (road graph, blocks, lots, sites, traffic lanes), CityModel (data +
              A* routing), GroundFactory (roads, kerbs, markings), BuildingFactory,
              PropFactory, SkylineFactory, CityAssembler (orchestrates)
  Vehicle/    ScooterController (handling), ScooterVisual (rig + animation)
  Traffic/    VehicleFactory (shared meshes), TrafficAgent, TrafficSystem, PedestrianSystem
  Gameplay/   GameDirector (state machine), OrderManager, ComboSystem, RouteService,
              DeliveryBeacon
  Fx/         ChaseCamera, FxDirector (particles, skids), EnvironmentDirector (sky, sun, post)
  Audio/      AudioSynth, AudioDirector
  UI/         UiKit, HudRoot, Minimap, ScreenStack
  Dev/        AutoRider — a soak-test autopilot, not a game feature
```

`Core/Tuning.cs` holds every gameplay number and `Core/Art.cs` (same file) every colour. Balance
changes belong there, not scattered through systems.

## Design notes worth knowing

- **Handling is a deliberate split.** Translation is dynamic (so kerbs, jumps and collisions feel
  physical) but rotation is authored — `ScooterController` drives yaw directly with
  `FreezeRotation` on the rigidbody. That is what stops an arcade scooter from spinning out.
- **Risk is the economy.** Near-misses, drifts and airtime feed a decaying combo multiplier and
  an adrenaline tank that pays for boost. Crashes wipe the combo *and* damage the food, which
  cuts the payout tier. The fastest line and the most profitable line are different questions.
- **Pickup and drop-off zones sit ~2 m into the near lane**, not on the pavement. A zone tucked
  against the shopfront is nearly impossible to hit from the road.
- **Offers do not tick down while the bag is full**, so you can never lose money to a job you had
  no way of collecting.
- **Navigation uses pure pursuit** (`RouteService.UpdateHeading`): project the rider onto the
  route polyline, then aim a fixed arc length further along it. Aiming at "the first waypoint more
  than N metres away" sends riders in circles when the nearest junction is behind them.
- **Lighting is one dial.** `EnvironmentDirector.SetNightFactor` slides sun, ambient, fog, sky and
  bloom from golden hour to full night; shift level drives it, so the game visibly gets darker and
  more neon-lit as you progress. Street lamps and shopfronts cast their light as stacked additive
  geometry rather than real lights — free at runtime, and it is what makes the night shifts read.

## Driving the editor from the CLI

The `unity` CLI can build, inspect and script the running editor. Notes that cost time to
discover:

- `unity command eval` / `eval_file` take a **method body**, not a file with `using` directives.
  `UnityEngine` and `UnityEditor` are already imported; `Object` is ambiguous, so qualify it. A
  static class cannot be aliased with `var` — write `FormosaExpress.Core.Services.Orders` in full.
- Nested-object arguments (e.g. `set_tags_layers settings={...}`) do not parse from the CLI. Use
  `eval_file` for project settings instead.
- `unity command screenshot` renders the camera, so it **excludes** screen-space UI. For a capture
  including the HUD, call `ScreenCapture.CaptureScreenshot(path, 2)` through `eval` and wait for
  the file.
- `InputRouter` has `Scripted*` fields for exactly this: set `ScriptedActive = true` and you can
  drive the game from `eval` with no device attached. `Dev/AutoRider` builds on that for unattended
  soak tests.
