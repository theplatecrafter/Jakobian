# Jakobian development

Research baseline: **14 October 2026**; development updated **15 October 2026**. Target game: **KSP 1.12.5, Windows x64**. Workspace: `C:\Users\Hans\Documents\programming\Jakobian`.

Jakobian is a **standalone mod framework under development**, with its own system loader and no required Kopernicus installation. The full framework targets the familiar Kopernicus authoring model and landable bodies with arbitrary *supported* geometry: irregular solids, rings, tori, cylinders, hollow shells, disconnected assemblies and locally volumetric terrain. Geometry, gravity, breathable air and water are separate definitions. An interior floor must work as well as an exterior surface.

Development **0.2.0** adds Jakobian's own system loader and a first restricted radial terrain backend for sphere, ellipsoid, rounded cube and peanut shapes. The baseline instance receives the current build; the original four fixtures are retained alongside the new Atlas moon. See [human testing](development/human-testing.md) and [development workflow](development/workflow.md). General topology and full framework parity remain in development; the final public API/schema is not frozen. Files under `examples/` still describe disabled future-language proposals; the active test pack is under `GameData/JakobianTestBodies`.

Development **0.3.0** adds a shared surface/navigation query, analytic normals, explicit ground-versus-datum altitude, a flight readout and a scoped camera clearance guard. It preserves the current point-gravity navball vertical and supplies a native resource-coordinate bridge. See [surface context](architecture/surface-context.md) and the [0.3.0 development record](development/v0.3.0.md).

Development **0.4.0** adds closed-solid sphere/torus queries, bounded multiple-intersection rays, deterministic torus mesh patches and an isolated native collider experiment. It adds retained-save upgrade and stock-orbit regression probes. See [solid queries and meshes](architecture/solid-queries-and-torus-mesh.md) and [0.4.0](development/v0.4.0.md). The enabled celestial bodies remain the radial fixtures; a landable torus world is not yet available.

Development **0.5.0** adds height/albedo maps, a shared displaced radial field, a custom terrain shader and native biome integration on the new Atlas fixture. See the [map authoring contract](modders/radial-maps.md) and [0.5.0 development record](development/v0.5.0.md). The broader [mathematical bodies and fields design](architecture/mathematical-bodies-and-fields.md) covers function-defined solids, mass distribution, rotating habitats and non-spherical influence domains; these remain implementation milestones.

Development **0.6.0** adds bounded torus contact streaming, swept coverage, transactional native patch publication and analytic material-coordinate records. The [isolated native contact fixture](architecture/torus-contact-streaming.md) tests real rigidbodies and hole transit; [the provider API](modders/experimental-torus-provider.md) remains separate from enabled celestial-body configuration. See [0.6.0](development/v0.6.0.md).

## Read in this order

1. [Research findings and source ledger](research/findings.md): what was inspected and what can be concluded.
2. [Engine constraints and integration inventory](architecture/ksp-integration.md): why this needs more than custom meshes.
3. [Architecture and ownership](architecture/system.md) and [standalone loader](architecture/standalone-loader.md): Jakobian's own system construction and optional Kopernicus coexistence.
4. [Shape, topology and ground specification](spec/geometry.md): solids, surface regions, charts, distances and query semantics.
5. [Terrain, maps, biomes and resources](spec/terrain-and-maps.md): authoring familiar maps on unfamiliar topology.
6. [Gravity, reference frames and trajectories](spec/physics.md): physical and artificial fields, rails and warp.
7. [Atmosphere, fluids, lighting and hazards](spec/environment.md): spatial volumes rather than spherical altitude alone.
8. [Configuration and migration guide](modders/configuration.md): familiar nodes and proposed new nodes.
9. [Compatibility matrix and adapter policy](compatibility/matrix.md): explicit coverage rather than a blanket guarantee.
10. [Persistence and stable identities](spec/persistence.md): upgrading worlds without moving landed craft.
11. [Development environment](development/environment.md) and [validation plan](development/validation.md).
12. [Implementation roadmap](planning/roadmap.md), [decisions](planning/decisions.md) and [open questions](planning/open-questions.md).

The [source lock](research/upstream-lock.json) pins five upstream repositories, and the [release source lock](research/release-source-lock.json) separately pins three exact release revisions. The [assembly report](research/ksp-assembly-report.json) records hashes, method signatures and IL call references from the actual template. The [environment report](development/environment-report.json) and [validation record](development/validation-record.json) capture this setup's evidence.

## Design commitments

- Preserve Kopernicus `Body`, `Template`, `Properties`, `Orbit`, `ScaledVersion`, `Atmosphere`, `Ocean`, `PQS`, biome and science concepts wherever their meaning remains valid.
- Own spherical and nonspherical body loading from the first loader milestone; use native KSP machinery where valid. Kopernicus is a reference and optional compatibility target.
- Give every game subsystem one coherent surface/environment query service. Geometry normals, apparent vertical and orbital radius are distinct values.
- Use explicit stable body/surface/chart/biome identifiers, deterministic terrain and versioned migrations.
- Schedule bounded work, stream contact terrain ahead of travel and expose meaningful diagnostics.
- Treat wide mod compatibility as a test program and adapter contract. It cannot be established by API similarity alone.

All authored Markdown lives under `docs/`. Source and DLL snapshots under ignored `.research/` are inspection inputs, not part of Jakobian's distribution. Game installations, saves and proprietary game DLLs are ignored by version control.

## Setup result

The original research bootstrap evidence is retained in [validation-record.json](development/validation-record.json) and the [startup investigation](development/startup-investigation.md). Development 0.6.0 has a zero-warning build and 165,348 core assertions. Three final native runs passed isolated torus streaming/contact/material-record checks, Atlas mapped terrain/contact/GPU/navigation/reload checks, six historical-save upgrades and a stock orbit coast/reload. All three logs are exception-free through shutdown. The [current evidence](development/v0.6.0-validation.json) records exact tested hashes and limitations. [0.5.0](development/v0.5.0-validation.json), [0.4.0](development/v0.4.0-validation.json), [0.3.0](development/v0.3.0-validation.json) and [0.2.0](development/v0.2.0-validation.json) remain historical evidence. Kopernicus coexistence remains unsupported until single-loader coordination is implemented. The main development branch is `main`.
