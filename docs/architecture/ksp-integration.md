# KSP constraints and integration inventory

Observed using the supplied KSP **build 03190** and four managed assemblies. [Machine-readable metadata](../research/ksp-assembly-report.json) includes their SHA-256 hashes, signatures, visibility and selected IL calls. UnityPlayer identifies **2019.4.18**, matching installed editor **2019.4.18f1**. Managed metadata alone does not verify native execution order.

Jakobian's own standalone loader is responsible for native system construction and post-spawn binding. Kopernicus interfaces below are research references and optional interoperability concerns, not required entry points. Validate the baseline with Kopernicus absent; see the [standalone loader specification](standalone-loader.md).

## Native assumptions and necessary adapters

| Area | Observed native interface / prior-project evidence | Required Jakobian behavior |
| --- | --- | --- |
| Identity/system | Kopernicus `PSystemManager.systemPrefab`, spawned `CelestialBody` | Native body identity for independent bodies; explicit anchored-habitat mode for co-centred structures |
| Radial geometry | `CelestialBody.GetAltitude`, `GetWorldSurfacePosition`, `GetSurfaceNVector`; `PQS.GetSurfaceHeight` | Position/surface queries that can express inward normals, multiple sheets and empty holes |
| Body properties | `Radius`, `gravParameter`, PQS/atmosphere/ocean fields | Retain reference values for legacy consumers; publish local environment through explicit adapters |
| Loaded vessel | `VesselPrecalculate.CalculateGravity`, `CalculatePhysicsStats` | Correct field and vertical; preserve stock/KSPCF work and compose at verified boundaries |
| Cache ordering | `Vessel.UpdateCaches`, `UpdatePosVel`; Ringworld final-writer corrections | One coherent local velocity/up/clearance environment remains valid for downstream consumers |
| Atmosphere | `FlightIntegrator.CalculatePressure`, `CalculateConstantsAtmosphere`; body pressure API is scalar | Position-dependent air sample; correct drag, thermal, sound, engine/intake and parachute inputs |
| Contact | `Vessel.checkLanded`, `GetHeightFromSurface`, wheels and EVA contacts | Contact-confirmed landed/splashed situations; candidate distance alone never establishes landing |
| Fluids | `PartBuoyancy.FixedUpdate`; native oceans are radial | Per-part immersion in explicit fluid volumes, free-surface normals and local relative velocity |
| Navigation/UI | navball, north/east/up, heading, altimeter, camera | Distinguish gravity vertical, surface normal, reference altitude and nearest/down-ray clearance |
| Rails | `GoOnRails`, `GoOffRails`, `OrbitDriver.UpdateOrbit` | Persisted material anchor or numerical flight state; lossless position/velocity transitions |
| Keplerian prediction | native `Orbit`, conic patches and SOI ownership | Stock conics outside custom fields; numerical trajectory/event detection inside them |
| Floating origin | `FloatingOrigin`, `Krakensbane.SafeToEngage` | Double-to-local conversion and coherent velocity-frame shifts for bodies, terrain and all participants |
| Science/resources | body/biome/situation-based stock queries | Surface-aware biome, situation and resource adapters; stable archive identities |
| Map/tracking | scaled movements, map collider/picking and orbit rendering | General scaled mesh, true extent, stable far-distance projection, truthful trajectories |
| Recovery/contracts/KSC | native body/name/index and latitude/longitude assumptions | Keep spherical homeworld first; defer arbitrary homeworld and provide explicit career adapters |
| Lighting | spherical occultation assumptions and stock solar consumers | Shape-aware occlusion where required; shared physical illumination and visual results |

The [KSP modding documentation](https://kspmoddinglibs.github.io/KSPDocsSite/class_flight_globals.html) also documents dominant-body gravity behavior. Local assembly inspection is the reference for this game installation; online API docs may differ in visibility or version.

## Four representations must agree

1. **Native metadata:** body name, hierarchy, mass parameter and legacy radius.
2. **Canonical geometry/environment:** full descriptor and query service.
3. **Loaded contact/render chunks:** local meshes, colliders and scenery at active participants.
4. **Scaled/map/prediction:** distant shape, bounded representation and custom trajectory where needed.

A visually correct map mesh does not establish a flight collider. A collider does not establish atmosphere or rails persistence. An environment adapter that affects only the altimeter still leaves engines and EVA reading spherical altitude. Track these as separate acceptance gates.

## Radius and sphere-of-influence policy

An arbitrary body does not have a single physical surface radius. Define `referenceRadius` for legacy altitude/monopole conventions and `boundingRadius` for broad-phase culling; define entry volumes independently. Never increase native radius to the outer bounding sphere without checking kill-radius, ocean, camera, scaled and SOI consequences. In particular, the centre of a torus is navigable empty space.

Keep native spherical SOI hierarchy where it makes sense for a compact independent body. A habitat around its parent star can instead retain the star as orbital owner and use an explicit local shape region. This reduces native hierarchy distortion but needs separate surface identity adapters, and cannot be claimed fully compatible with body-based mods. Decide per body at load time; do not change a vessel's `mainBody` each tick to fix an altimeter.

## Patch policy

Prefer public loader/event/provider APIs. MFI's gravity and pressure override hooks are exclusive slots in the inspected source; some hooks have pre/post facilities and others do not. Choose composition per hook after inspecting the running owner. KSPCF already replaces several integrator methods. A postfix that republishes selected caches is a candidate, not proof that all physical acceleration paths are covered.

Each patch requires: exact target signature, supported version range, ownership/ordering report, applicability predicate, state restoration rules, test with no Jakobian body, test with competing owner, and one named reason it is needed. Patches skip unrelated bodies. Preserve stock acceleration before computing a desired-minus-existing correction; adding custom gravity blindly can double it. Loose rigidbodies, servo rigidbodies, wheels, ragdolls and EVA need explicit coverage.

## Scale and precision

Body-local doubles are necessary but insufficient. At astronomical ring radii, subtracting two large float positions loses contact-scale information. Rebase canonical doubles before float conversion; phase reduction at huge UT must preserve small motion. Use local material charts/tile-relative coordinates so tangential distances do not depend on whole-world float coordinates. Reject dimensions whose required contact accuracy cannot be represented. Frame changes, timewarp and rotating colliders must be tested for injected energy rather than assessed solely by screen appearance.

## Features intentionally gated

Development 0.2.0 first implements radial nonspherical bodies through native PQS. The first general-topology slice remains an airless static torus with stock spherical homeworld. Arbitrary homeworld/KSC, non-orientable landable surfaces, changing topology under occupied terrain, arbitrary articulated body motion and interacting loaded frames are future capability gates. They must either be supported by a dedicated provider or rejected with specific diagnostics, not silently flattened into a sphere.

## Observations from the 0.2.0 native implementation

- `PSystemSpawn` permits independent publication before native construction in the tested baseline. Cloned native metadata, stock-parent orbit references and separate new-body PQS/scaled representations require post-spawn binding to the live bodies. Final ModuleManager database ordering remains untested.
- A custom `PQSMod` on the sphere root was ignored by native height queries; direct-child registration works. Preserve the native terrain transform, mesh-collider, material-direction and relative-UV infrastructure while replacing height/color modifiers. Copied material references need explicit ownership because Unity component instantiation does not duplicate material assets.
- Native `CelestialBody.CBUpdate` skips body-frame initialization for a nonrotating body. Setting its quaternion alone does not initialize the `BodyFrame` matrix. Static-frame publication fixes coordinate collapse, but does not solve degenerate zero-spin landed stock conics/persistence.
- `CelestialFrame` uses orbital Z-up, while Unity/PQS use Y-up. Native `GetWorldSurfacePosition` swizzles X/Z/Y before and after `BodyFrame.LocalToWorld`. The inverse conversion is `BodyFrame.WorldToLocal(relativeWorld.xzy).xzy`; omitting either conversion produces misleading local-surface/clearance values.
- `Vessel.SetWorldVelocity` directly sets part rigidbody velocities in the floating physics frame. An inertial desired velocity must account for `Krakensbane.GetFrameVelocity()`. The development braking fixture exposed this through an apparently stopped capsule retaining orbital motion.
- Native collider contact and LANDED flags, not an analytic height sample, establish the current flight checks. Save/reload and rendered-ground inspection are separate evidence. Choosing a camera pose at orbital entry did not keep a useful ground view through the descent; the probe now re-aims after touchdown.

These findings apply to the supplied build and tested radial fixtures. They do not establish general-topology, EVA, long-duration contact or third-party compatibility. See [development evidence](../development/v0.2.0.md).
