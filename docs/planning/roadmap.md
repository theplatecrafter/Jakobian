# Implementation roadmap and acceptance gates

This is a sequence of testable deliverables, not a schedule or completed-framework claim. Each milestone produces runnable fixtures and updates the compatibility matrix.

## M0 — research and environment foundation (this pass)

Source/assembly pins, build/test/inspect tools, isolated game copies, diagnostic bootstrap, analytic math probes, architecture/specification and reviewable examples. Gate: zero-warning build, geometry/frame probes, baseline startup marker and accurately recorded upstream-profile result. No arbitrary-body gameplay yet.

## M1 — standalone system loader and canonical query service

Implement Jakobian's own final-node parser, stock template/system construction, pre-instantiation publication, post-spawn binding, lifecycle trace, immutable shape registry and descriptor validation. Add query availability/ambiguity/bounds and coordinate conversion tests. Gate with Kopernicus absent: untouched stock system works, one new spherical body is created by Jakobian, body IDs and save references remain stable, and system publication happens once. Define explicit competing-loader handling. Expand spherical config parity throughout later milestones rather than treating it as inherited from Kopernicus.

## M2 — complete airless static torus slice

Analytic shape/BVH/charts, scaled mesh, local collider streaming, geometry-aware kill/altitude paths, point gravity, surface caches/navball/camera, contact landing/EVA/wheels, material anchors and first scenario save schema. This tests empty hole transit plus both inner and outer surfaces while keeping gravity initially central. Gate: land -> EVA -> save -> reload -> switch vessel -> Map/Tracking -> warp on the actual supported shape; multiple vessels/debris do not lose contact.

## M3 — terrain, biomes, science and resources

Ordered modifiers, inward-positive chart displacement, maps/noise, seams, region materials/scatter, science archive/situation adapters, extraction/scanning, deterministic cache and generator versioning. Gate: byte/numerical terrain identity across LOD/reload, correct inner/outer biome/resource attribution, no duplicate science on upgrade.

## M4 — rotating habitats and noncentral flight

RingHabitat finite shell/regions, frame manager, spin/reference forces, field providers and numerical trajectory/warp event service. Gate: independently validated centrifugal/Coriolis cases, inertial-state continuity, swept impact/warp guard, anchored/flying reload, long UT and fleets; no misleading conic line in custom fields.

## M5 — local air, water and thermal behavior

Gas volume/datum/curves/flow, per-part environment, reservoirs/free surfaces/buoyancy, stock engine/intake/parachute/EVA/thermal consumers, custom optical providers. Gate: outside-rim vacuum, interior pressure, ascent/descent, waterline/sinking, dry/wet support, no double drag/buoyancy; physical and visual boundaries agree to declared tolerances.

## M6 — arbitrary mesh and implicit topology

Offline import/bake, closed-solid/atlas validation, clustered contact/LOD and BVH; later bounded AST/CSG/volumetric terrain with validated stitching. Gate: irregular closed asteroid, hollow chamber and cave floor/ceiling with correct multi-hit queries, nearest ambiguity and stable saved anchors. Reject unsupported/self-intersecting/non-orientable geometry precisely.

## M7 — compatibility expansion

Pin and implement high-value FAR/engine/autopilot/mapping/graphics/life-support/contract adapters from source inspections and demand. Extend patch-owner reports and fixtures. Gate: exact mod/version/shape/scene evidence; explicitly gate competing dynamical solvers until ownership handshake exists. This work overlaps later stages as needed rather than waiting to discover conflicts after implementation.

## M8 — full spherical parity and public framework

Complete the standalone loader's remaining spherical feature parity against the Kopernicus parser/provider inventory. Add system parity fixtures, packaging/install/dependency metadata, owner-selected license/provenance, versioned public API and historical save fixtures. Gate: checklist category coverage plus explicit unsupported provider list; single injector ownership in every supported installation. Standalone loading is already required by M1, not introduced here.

## Later optional modules

Arbitrary homeworld/KSC, richer background life support/communications, potential/polyhedral gravity, erosion/weather/currents, dynamic/deformable topology and interacting loaded habitats. Do not advertise these until their invariants and migrations are tested.

## Development status

Development 0.2.0 implements the first standalone parser/publication/binding path and four additional stock-parent moons, using native PQS with a radial-shape modifier for three nonspherical fixtures. The spherical control and geometry prototypes are initial coverage, not full M1 acceptance or general topology. See [human testing](../development/human-testing.md) for the enabled subset and executed evidence.

Development 0.3.0 adds the first shared radial surface context, analytic normals, explicit datum/down-ray distances, a native resource-coordinate bridge, an external-camera endpoint guard and native navigation/UI/cache validation. Gravity vertical and surface normal are independent. Full M1 canonical general-query semantics and saved-world reconciliation are still incomplete; no arbitrary-field cache injection or general-topology claim follows from this radial slice.

Next close M1's native terrain/contact, stock regression and save/reload gaps, then implement M2's general query/collision/anchor path for a true torus. The radial backend remains a supported restricted family; it does not become the general topology model.
