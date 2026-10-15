# Human testing — Jakobian development 0.6.0

The latest successfully deployed build is in `C:\Users\Hans\Documents\programming\Jakobian\instances\baseline`. Launch it with [Play Jakobian.cmd](../../Play%20Jakobian.cmd), or run that instance's `KSP_x64.exe`. It uses the supplied KSP 1.12.5 installation and has no Kopernicus dependency.

This build implements a first restricted shape backend using native PQS terrain: one positive radius per direction. It is intended to exercise the independent loader, real terrain, native gravity and map representation. Torus holes, hollow bodies, arbitrary meshes, local atmosphere/water, rotating habitats and full Kopernicus feature parity remain future work.

Kopernicus coexistence is currently blocked with a loader ownership error; baseline runs Jakobian alone. Terrain tile color/lighting seams and camera behavior remain visual acceptance work. The automated probe re-aims its camera after touchdown, so those screenshots do not certify ordinary camera navigation across every slope.

## Included bodies

All five are additional airless moons of Kerbin with an ordinary six-hour native rotation. Existing stock bodies are retained. Native point gravity is intentional and does not claim a density-derived mass field for each shape.

| Body | Shape | Native reference radius | Test purpose |
| --- | --- | ---: | --- |
| JakobianControl | sphere | 5 km | control case |
| JakobianEllipsoid | axes 12 / 4 / 6 km | 4 km | unequal surface radius, silhouette and terrain |
| JakobianCube | rounded cube, exponent 8, semiaxes 6 km | 6 km | faces, corners and terrain slope |
| JakobianPeanut | two lobes, 10.5 km tips / 3.5 km waist | 3.5 km | strong radius variation and waist contacts |
| JakobianAtlas | axes 9 / 5.5 / 7 km, mapped displacement up to 600 m | 5.5 km | crater terrain, albedo textures and four native biomes |

Reference altitude is native centre-distance minus the listed reference radius. It is not clearance above local ground. Use terrain/radar altitude for descent; surfaces may lie several kilometres above the reference datum. Navball up follows the central gravity field, which can differ from the geometric surface normal on slopes.

## Ground, down and the camera

An F8 surface panel now shows ground clearance, datum altitude, terrain slope, gravity and biome. Its altitude values are sampled at the vessel centre of mass, matching the native altitude caches. F8 hides/shows the panel and F2 hides it with the rest of the UI. Switch the stock altimeter between ASL and AGL to compare its datum/ground modes.

The navball horizon remains aligned to gravity. On these point-gravity moons, gravity points toward the centre. A sloping face need not look level on the navball; the panel's slope describes that difference. Shape-aware/artificial gravity fields are later providers, not a change silently inferred from the body's shape.

The **Keep camera above ground** option enables a 2 m radial endpoint constraint in external Flight view on Jakobian bodies. Try orbiting/zooming the camera near a steep face. Map view, IVA/internal modes and stock bodies retain their existing camera behavior. This first guard does not guarantee an unobstructed camera-to-vessel line or a clear near plane on every steep surface.

The shared resource bridge supplies native body/latitude/longitude/datum coordinates for planetary abundance requests. Native Ore lookups are tested; Atlas also supplies native science/resource biome lookup. Actual drill/scanner operation, career science/archive UI and custom resource distributions remain separate acceptance work.

## Manual test

For the terrain fixture, choose **Resume Saved** and open **`JakobianProbe-JakobianAtlas-20261015-141117`**. It contains the stock capsule from the final 0.6.0 Atlas contact/GPU/save/reload run. The [0.6.0 evidence record](v0.6.0-validation.json) lists exact tested hashes and scope. Existing fixture saves remain available; all five worlds retain their 0.5.0 geometry, configuration and indices.

Atlas demonstrates macro albedo and terrain mapping with 256x129 images. Close-up ground can look smooth because fine detail/normal/splat textures are not implemented yet. Inspect larger terrain/color regions from altitude as well as contact at ground level. The isolated native overview render is recorded in the evidence file; it is a controlled material test, not a flight screenshot.

1. Start a new Sandbox save for initial development testing. Build a small stock lander with a command pod, landing legs, reaction control and a low-thrust engine. Launch it.
2. In map view, verify the new bodies orbit Kerbin and have their expected nonspherical silhouettes. Check the stock system as well.
3. For a quick visit, open the stock debug menu with **Alt+F12**, find **Cheats / Set Orbit**, select a Jakobian body and choose a safe initial orbit about **30 km above its reference radius**. This clears all current shape bounds. A normal transfer also works but is slower for repeated tests.
4. On Atlas, inspect crater bowls/rims, Rust Plains, Highlands, Crater Basins and Polar Caps. Compare the F8 biome label at different locations. Caps are a visual/biome fixture, not simulated ice or water. Descend at low speed. Test the ellipsoid's long/short-axis surfaces, the cube's faces/corners and the peanut's tips/waist. Inspect actual contact and camera behavior, rather than treating an altimeter value alone as proof of landing.
5. Once landed, test EVA, quicksave/reload, map/flight switching and vessel switching. These are human acceptance tasks; the evidence record distinguishes them from checks already executed by the agent.

The shape fixture definitions are [Bodies.cfg](../../GameData/JakobianTestBodies/Bodies.cfg). This initial parser accepts only a small documented subset; unsupported shape/environment/terrain settings fail rather than becoming an unadvertised spherical fallback.

Development 0.5.0 keeps the original four fixture definitions unchanged and adds [Atlas.cfg](../../GameData/JakobianTestBodies/Atlas.cfg). See [map authoring](../modders/radial-maps.md) for the supported PNG8 schema. Its torus geometry and native collider checks are isolated development experiments, not an enabled torus celestial body. The next general-body slice must handle hole/interior flight, spherical-consumer assumptions and stable surface anchors before that fixture is enabled.

## Torus contact development fixture

Development 0.6.0 retains the same five playable radial moons. The new torus contact provider is an experimental API and isolated native test; there is no enabled torus celestial body yet. With baseline closed, run `./tools/Torus-Probe.ps1`. It tests bounded patch streaming, two participants, a swept chart seam, real rigidbody contact on inner/outer/top walls, hole transit and local material-record roundtrip. Its prescribed forces are contact fixtures, not a torus gravity model or centrifuge. The separate physics scene is destroyed before exit and no player craft/save is modified. Look for `TORUS_STREAM_SUITE_PASS` and the watcher's `torusStreamingMarker`; a process exit alone does not prove success. See [architecture](../architecture/torus-contact-streaming.md), [API guide](../modders/experimental-torus-provider.md) and [0.6.0 evidence](v0.6.0-validation.json).

## Automated development probes

From the repository root, with the baseline game closed:

```powershell
./tools/Build.ps1
./tools/Boot-Probe.ps1 -Instance baseline -Graphics
./tools/Torus-Probe.ps1
./tools/Flight-Probe.ps1 -Body all
# Or only the new terrain fixture:
./tools/Flight-Probe.ps1 -Body JakobianAtlas
./tools/Regression-Probe.ps1
```

Boot verifies system publication/binding, stock Kerbin identity, analytic/displaced versus native PQS radii, coordinate transforms and scaled-mesh presence. Atlas adds 2,520 mapped height/science-biome comparisons plus scaled-vertex checks. Flight probe checks native navball/up/altitude/velocity/vacuum caches, both actual altimeter modes and native Ore request coordinates. It deliberately puts the camera endpoint underground, exercises the production guard and follows native camera updates for ten frames. It then uses test-only velocity braking to descend at about 3 m/s, checks real terrain contact/landing, writes a private save and reloads it. The braking substitutes for an engine; it never assigns the landed flag or creates a substitute collider. It is enabled only by the explicit launch argument. The watchdog stops a stalled probe; a process exit alone is not a pass. Inspect `BOOT_PROBE_PASS`, the navigation/altimeter/camera pass markers, the per-body `FLIGHT_LANDING_PASS` / `FLIGHT_RELOAD_PASS`, and the final `FLIGHT_SUITE_PASS` in the returned log.

Probe-created saves use `JakobianProbe-<body>-<time>` names and can be opened for further manual testing. Screenshots, where produced, are under the instance's `.dev/screenshots`. No tutorial seed or proprietary game files are stored in source control.

The regression probe stages six retained landed saves from 0.2.0 and 0.3.0, verifies each in a new private `JakobianUpgrade-...` save and reloads it. It also coasts/reloads a stock Kerbin orbit under `JakobianStock-...`. Original fixtures are never edited; input hashes are stored in `.dev/regression-inputs/manifest.json`. It returns through MainMenu between worlds. These Sandbox capsule cases do not certify every craft, career save, scene or mod. Both probe suites now return through MainMenu before process exit. This pass's seven completed private upgrade/stock copies were retained under `.dev/regression-results`; no player or original input saves were moved.

Probes use normal graphics by default. Earlier 0.3/0.4 headless attempts reached startup/shape/frame checks but crashed in native RenderTexture creation during flight loading on this host. The mapped shader/GPU checks in 0.5.0 require a graphics device. Headless mode remains an explicit unsupported experiment, not the current validation path.

## Report observations

Send the build version, body, location, craft, reproduction steps and the relevant `KSP.log`, plus a screenshot where useful. Note whether the problem concerns visual shape, terrain collision, local altitude, gravity, EVA or reload. The project owner handles GitHub/repository activity; development reports can stay in this chat.

Static-body frames are implemented, but zero-spin landed persistence is not yet certified. The human fixture pack uses native uniform rotation while dedicated surface-anchor persistence is developed. Early failed probe saves are experimental evidence; use the latest successful fixture saves or a fresh Sandbox for testing.

The older 0.3.0 probe logged a native KnowledgeBase/MapView null reference when quitting directly from Flight. The 0.4.0 probes use normal MainMenu teardown and both final logs are exception-free. This does not install a global UI fix or certify forced desktop exit from every scene; include scene/mode context when reporting exit errors.
