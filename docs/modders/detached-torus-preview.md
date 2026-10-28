# Detached torus definition and scaled preview

Development 0.9.0 / P03 adds an experimental torus definition, closed whole-surface mesh and isolated native prefab preview. **It does not add a live celestial body or flight destination.** P04 must establish admission and spherical-consumer adapters before a torus enters the native system. The existing five moons retain their gameplay and identities.

## Human preview

With baseline closed, run [Preview Jakobian Torus.cmd](../../Preview%20Jakobian%20Torus.cmd). It invokes the guarded interactive PowerShell 7 launcher; it refuses a second baseline process. The preview appears over MainMenu with top/tilted views and rotation buttons. An owned native `MAIN_MENU` input lock prevents clicks from reaching stock 3D menu buttons underneath. **Close preview** releases its resources and removes only its own lock, restoring the normal menu. Leaving MainMenu also releases it. This menu lock is not a game/flight admission barrier. The renderer has no flight collider or force ownership, and no player save is loaded or adopted by opening the preview. The ordinary [Play Jakobian.cmd](../../Play%20Jakobian.cmd) remains the normal game launcher.

The displayed torus is a uniform-color geometry fixture, not textured terrain or biomes. It is rendered at KSP's 1:6,000 scale into a 512×512 target; thin features below a pixel are not visually certified. Scaled coordinates, body-local metres and the inactive native prefab are separate representations. The preview's reserved native index is metadata, not a consumed live slot.

## Experimental input

The shipped [Torus.cfg.disabled](../../GameData/Jakobian/Preview/Torus.cfg.disabled) is copied into baseline by deployment and read only by the opt-in preview app. Keeping the `.disabled` suffix excludes it from ordinary GameDatabase CFG loading. It is separate from the enabled `Jakobian` celestial parser; renaming it to `.cfg` does not enable torus gameplay.

The `JakobianPreview` node requires `schemaVersion = 1`, `capability = preview-only`, `name`, `id`, `flightGlobalsIndex`, and exactly one of each child:

| Child | Required values |
| --- | --- |
| `Shape` | `type = Torus`, `majorRadius`, `minorRadius` in metres |
| `Properties` | `gravParameter` in m³/s², `sphereOfInfluence` in metres |
| `Orbit` | Native `referenceBody`, `semiMajorAxis`, `inclination` in degrees, `meanAnomalyAtEpoch` in radians |
| `Preview` | `segmentsU` 8–256 and `segmentsV` 8–128 |

Unknown/duplicate fields and unsupported capability, shape or child nodes fail. The native parent must exist. Stable/native identities are validated and checked against installed bodies before detached publication. A registry holds one publication; replacement requires disposal and a new registry.

The ring torus must satisfy `R > r`, `r >= 100 m`, `R+r <= 10¹⁰ m`, and `r/R >= 10⁻⁴`. Gravity metadata must be finite and positive, no greater than 10³⁰; SOI is at least five times the outer extent and at most 10¹⁵ m; orbit radius exceeds twenty times the outer extent and is at most 10¹⁵ m. Inclination is bounded to ±180° and phase magnitude to 10⁹ radians. These are the current experimental profile limits, not a universal KSP precision guarantee. No atmosphere, water, spin, terrain function or density program is inferred.

The native reference radius is the tube radius, while the bounding radius is `R+r`; neither is a generic ground-altitude substitute. Gravity/orbit values populate inactive metadata only. The root is never activated: native `CelestialBody.Awake` would otherwise add it to its parent's orbiting list.

## API and ownership

`TorusBodyDefinition` owns a coherent `AnalyticSolid`, `TorusChart` and versioned `WorldBodyDescriptor`. Identity includes geometry, reference/dynamics metadata, chart/provider and **preview-only capability**. `Preview` tessellation is excluded from world identity. A future landable provider/capability requires explicit reconciliation, not silent promotion of a saved preview.

`TorusMeshBaker.BuildClosed(R,r,U,V)` returns body-local metre vertices, unit outward normals and consistently wound triangles. Both periodic axes share indices; every closed mesh edge has two incident faces. Maximum size is 32,768 vertices/65,536 triangles. This mesh has no texture-atlas UV contract; the preview uses a constant albedo with analytic normals. The existing tile baker/contact streamer remains separate.

On the Unity owner thread, create `TorusPreviewRegistry`, call `Publish(config)` and use the returned `TorusPreviewBody`. Its inactive native prefab binds body/index/orbit metadata and a separate scaled renderer; `Image` is a RenderTexture for the UI. The registry owns an isolated additive scene/physics scene. Do not activate the prefab or insert it into native body lists. `GeneralBodyQueries` can still use the explicit geometry/frame contract; radial `BodyQueries` excludes the detached body.

`Render(tilt, azimuth, zoom)` supports tilt 0–85°, bounded finite azimuth and zoom 1.05–2. `Capture()` returns a Texture2D owned by the caller; destroy it after use. Dispose the registry on its owner thread and yield frames for deferred destruction and asynchronous scene unload. The registry owns the prefab, mesh, material, color texture, camera, light, target and scene. RenderTexture active state is restored after rendering/capture. The mesh renderer stays disabled except during the synchronous preview render, so ordinary native cameras cannot render it. Layer 31 must be unused by another renderer on an active GameObject; the preview refuses that conflict instead of mixing foreign geometry into its image. Registry/body operations reject cross-thread mutation before changing ownership state.

Publication never changes `FlightGlobals.Bodies`, native system/local-body lists, parent orbiting lists, radial query ownership or `SystemLoader.CurrentWorld`. Preview catalog roundtrips use private `.dev` files; they do not append a torus to a player's `WorldScenario`.

## Validation and limits

Run `tools/Preview-Probe.ps1` with baseline closed. It executes normal startup checks, strict input/collision tests, two create/render/pick/dispose cycles, per-resource destruction assertions, GPU hole/surface samples, private P02 catalog roundtrip/mutation and a native Settings/MainMenu roundtrip. Native triangle-ray tests use an isolated temporary picking collider removed before disposal. No Game.Start, vessel, default-scene physics simulation or live-body publication occurs.

Require `TORUS_PREVIEW_CONFIG_PASS`, two each of GPU/bind/dispose markers, the scene marker, `TORUS_PREVIEW_SUITE_PASS` and the watcher's `torusPreviewMarker`. Process exit alone is not success. Exact executed scope, capture paths and independent assessment are in [0.9.0](../development/v0.9.0.md).

Actual Map/Tracking integration, celestial-system registration, orbit prediction, vessel admission, ground/UI caches, contact landing, saved anchors, EVA/warp, rotating frames and centrifuge physics remain later work. A watertight scaled mesh is not certification of those systems.
