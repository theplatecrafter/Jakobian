# Validation strategy and evidence scope

Original research results live in [validation-record.json](validation-record.json); versioned development records capture subsequent native fixtures. This document also defines future acceptance work; planned checks are not reported as passed.

## Current checks

Release build of core, KSP host, console checks and metadata inspector against the actual template. Locked restore, zero compiler warnings/errors. The current analytic harness checks **112,034 assertions**, with reproducible seeds: sphere/torus projection, chart reconstruction, solid versus empty torus hole, inward-facing hole normal, nonunique/invalid queries, point gravity and rotating-force signs, radial-shape bounds/symmetry/direction invariance and independent ellipsoid/superellipsoid implicit-surface checks, analytic normals versus finite-difference boundary tangents, compass/pole semantics, coordinates and datum/ground separation, plus finite solid-ray crossings/tangencies, budget/precision failures, torus seams/winding/edge incidence and Euler characteristic. Research 0.1.0 had 16,015 assertions, 0.2.0 had 38,021 and 0.3.0 had 86,039.

The randomized cases compare closed-form points/normals constructed from independent parametric coordinates; they do not establish general mesh topology, collision or physical gravity of a massive torus. No artificial-gravity game adapter exists. Console runtime and native Mono bootstrap are separate checks.

Metadata inspector reads four licensed game assemblies and records hashes, type/member signatures and IL call references. These files contain no decompiled method bodies or game DLLs. Source inventory collects parser targets from the pinned Kopernicus tree; it is an audit starting point rather than coverage certification.

The historical bootstrap baseline reached MainMenu and emitted `BOOT_PROBE_PASS`. That diagnostic-only scaffold also booted in the Kopernicus profile with optional KSPCF excluded; it logged one `SpaceCenter.Start` exception. The full KSPCF-enabled profile stalled in three launch modes. See the [startup investigation](startup-investigation.md). This does not certify coexistence for the current standalone loader, which deliberately reports an ownership error when Kopernicus is installed. Intentional stock-loader termination messages are not treated automatically as errors; absent bootstrap marker is not a pass.

Current development probes compare all four fixture bodies' native PQS heights and frame transforms to expected values, verify independent mutable terrain materials, and enter native flight on the three nonspherical bodies. The explicitly enabled flight harness uses velocity braking as an engine substitute, observes a native terrain collider and LANDED flag, then saves/reloads each vessel. Consult [development 0.2.0](v0.2.0.md) for exact evidence and limitations; this does not establish EVA, long-duration contact, every surface location, stock gameplay regression or third-party mod coverage.

The [0.3.0 probes](v0.3.0.md) additionally inspect actual native navball quaternions and both altimeter modes; compare vessel/global up, datum/radar and surface-velocity caches; query native Ore abundance through shared coordinates; and constrain a deliberately underground camera endpoint through ten native render updates. These are current radial/point-gravity fixtures, not validation of future arbitrary-field cache adapters or extraction/scanning.

The [0.4.0 probes](v0.4.0.md) cook isolated torus mesh-collider patches and check native inner/outer/top contact and a collider-free hole. A separate historical/stock runner loads private copies of six retained 0.2.0/0.3.0 Sandbox capsules, checks landed pose/navigation and re-saves/reloads them, then coasts/reloads a stock Kerbin orbit. It is not a complete old-save migration or stock-game regression matrix. Inputs are hashed and preserved.

## Future pure-core checks

- Exact primitive surface/occupancy oracles, boundary classification and query ambiguity.
- Multiple ray hits and sorted entry/exit, mesh BVH versus brute force, conservative swept bounds.
- Chart/inverse mapping, adjacency/seam orientation, disconnected/nested solids and thin shell bounds.
- Tangent/material frame roundtrips and native/body/inertial coordinate conversion.
- Deterministic terrain, stable biome/resource attribution, LOD boundaries and cache invalidation.
- Gravity/potential derivatives, independent field oracles and integrator convergence/invariants.
- Earliest event bracketing at large time steps, phase precision and field-transition continuity.
- Direct/skipped/idempotent world/save migrations using immutable historical fixtures.

Use property/random tests where they expose geometry edge cases; avoid testing implementation spelling or only comparing a function to itself. Keep reproducible seeds/tolerances and a small set of hand-derived anchor cases.

## Native gameplay acceptance

| Fixture | Required behaviors |
| --- | --- |
| untouched stock sphere | no force/environment/cache/save changes from plugin presence |
| standalone spherical body | body loaded by Jakobian with Kopernicus absent; numerical/query parity before generalization |
| airless static torus | hole transit, inner/outer contact, correct normal/UI, land/EVA/reload |
| rotating ring floor | centrifugal/Coriolis state, contact stability, rim/hull behavior, no frame-induced energy |
| chart terrain/biome | seams, LOD, deterministic occupied surface and science/resource identity |
| gas volume | local pressure/flow/heat, engine/intake/parachute/EVA, outside-volume vacuum |
| reservoir | partial immersion, afloat/sink, dry/wet situation, underwater state |
| irregular mesh/cave | multi-sheet ray/nearest query, floor/roof labels, empty cavity and BVH accuracy |
| multi-vessel | debris, EVA, wheels, robotics, docking, switching and shared footprints |
| unloaded numerical flight | warp event handling, reload, background vessels and map/trajectory truth |

Every applicable fixture includes Flight, Map, Tracking Station, scene re-entry, on/off rails, quicksave/reload and a supported-mod profile. Use private copies and hash original save fixtures. Record exact binary versions/provider signatures and active patch owners.

## Performance methodology

Measure terrain generation, upload/cook/publication, field/environment queries, predictor, visual updates, allocations/GC, retained tiles, GPU draw/memory and total frame time separately. Instrument relevant synchronous boundaries. Use paired fixed-UT/location/craft/settings runs, multiple repetitions and warm/cold/cache status, reporting distributions rather than a single FPS number as a universal claim.

Stress trajectories that continually outrun generation, huge UT/coordinate offsets, large contact craft and many local participants. Work/memory must stay bounded with travel duration; critical collider coverage must not depend on the whole planet being generated. A time slice is not a hard bound on native Unity collider cooking. Distinguish CPU scheduling improvement from reduced terrain/visual fidelity.

## Release readiness

No arbitrary-body release is ready at this stage. A release requires its milestone's actual native fixtures, compatibility table, migration fixtures, dependency/license/installation review and reproducible packaged source/assets. Keep technical evidence under `docs/development` and future player-facing notes under `docs/releases`. Do not present earlier Ringworld results or a boot-only coexistence test as Jakobian gameplay evidence.

## Mapped radial backend in 0.5.0

The core map checks cover independent interpolation values, longitude wrap including a roundoff endpoint, complete scaled-mesh sample bounds, immutable input ownership, pole agreement, palette classification, displacement limits, radial clearance/datum separation and an independent normal stencil. Native Atlas checks compare 2,520 PQS heights and science biome lookups to shared queries, verify all four biomes occur and compare sampled scaled vertices. Flight adds ResourceMap biome agreement to the existing navigation/contact/reload suite. See [0.5.0](v0.5.0.md) for executed scope and limits.

## Torus contact backend in 0.6.0

Core checks cover material coordinate inverses/seams/ambiguity, matching mesh tessellations, conservative patch bounds, swept contact membership, invalid/budget/capacity requests, transactional readiness, cache limits, stale/duplicate/cross-owner tickets and build retries. The native isolated scene traverses 128 two-participant footprints, checks a 33-sample seam corridor, settles three rigidbodies against the actual mesh colliders, transits the hole and round-trips material records with identity/shape/schema validation. Default physics settings remain unchanged and the local scene is unloaded. See [0.6.0](v0.6.0.md) for exact evidence and the distinction from KSP vessel gameplay.
