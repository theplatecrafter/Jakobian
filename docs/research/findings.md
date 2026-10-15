# Research findings and source ledger

## Method and evidence levels

Inspected the user's [beginner guide](https://forum.kerbalspaceprogram.com/topic/129540-a-beginners-guide-to-kopernicus-the-basics/), [examples repository](https://github.com/Kopernicus/KopernicusExamples/tree/master) and [Kopernicus source](https://github.com/Kopernicus/Kopernicus). Downloaded public source archives at pinned commits using GitHub's API; no upstream build scripts were run. Source filenames below resolve inside `.research/`. Full SHA-256 values and commit dates are in [upstream-lock.json](upstream-lock.json).

Read the referenced Codex chat **Create KSP Ringworld mod**, then inspected its local project at `C:\Users\Hans\Documents\programming\Ring World KSP mods\Ring World KSP`. Its source and saves were not modified. Its existing evidence is informative; its test claims have not been independently reproduced in Jakobian.

**Observed** means source/metadata inspection or a local check performed here. **Proposed** means a Jakobian design choice. **Unverified** means a runtime experiment is required. Metadata inspection establishes method existence and dependencies, not a reliable patch site by itself.

## Source pins and relevant files

| Project | Inspected commit | Useful source locations |
| --- | --- | --- |
| Kopernicus | `ed6b8e3bf03a7682894109a699431392d93b3882` | `src/Kopernicus/Injector.cs`, `Events.cs`, `Configuration/Body.cs`, `PQSLoader.cs`, `AtmosphereLoader.cs`, `OceanLoader.cs`, `ScaledVersionLoader.cs`, `Utility.cs`, `RuntimeUtility/RuntimeUtility.cs` |
| KopernicusExamples | `39c2542be71dd546c82732dcb5447a5c1b6ca015` | `KopernicusExamples/Creating New Bodies/PlanetWithMaps/KopernicusPlanet.cfg`, `OceanBody/Oceanus.cfg`, `ProceduralBody/Hodor.cfg`, `Star/Nemesis.cfg`, editing examples and `Rings Example/JoolRings.cfg` |
| ModularFlightIntegrator | `03f07cd6e498ed003f84ee97ce976430e6447256` | `ModularFlightIntegrator.cs`, `ModularVesselPrecalculate.cs`, `Example.cs` |
| KSPCommunityFixes | `bc2e03ad1d1b0dcdb1d7d91b8f0f2d3d21cdb63a` | `KSPCommunityFixes/Performance/FlightIntegratorPerf.cs`, patch framework and changelog |
| ModuleManager | `54b6de3d28175e0be3acd5bfc5454e232ed7dd78` | configuration processing and pass implementation; preserve its patch semantics |

Primary implementation references:

- [Kopernicus Injector](https://github.com/Kopernicus/Kopernicus/blob/ed6b8e3bf03a7682894109a699431392d93b3882/src/Kopernicus/Injector.cs#L155): rewrites the system prefab before native instantiation and registers post-spawn fixups.
- [Body loader](https://github.com/Kopernicus/Kopernicus/blob/ed6b8e3bf03a7682894109a699431392d93b3882/src/Kopernicus/Configuration/Body.cs#L421): external parser targets are loaded before `OnBodyPostApply` and subsequent scaled mesh generation. Integration must distinguish prefab objects from spawned bodies.
- [Events](https://github.com/Kopernicus/Kopernicus/blob/ed6b8e3bf03a7682894109a699431392d93b3882/src/Kopernicus/Events.cs#L221): load/finalization and body events provide candidate extension boundaries. Their availability does not prove Jakobian can safely change every aspect at that boundary.
- [Scaled-space generation](https://github.com/Kopernicus/Kopernicus/blob/ed6b8e3bf03a7682894109a699431392d93b3882/src/Kopernicus/Utility.cs#L412): the PQS-derived scaled mesh path is a specific pipeline to retain for spherical bodies and replace for general geometry.
- [MFI gravity override](https://github.com/sarbian/ModularFlightIntegrator/blob/03f07cd6e498ed003f84ee97ce976430e6447256/ModularVesselPrecalculate.cs#L180): one override slot, not an unlimited composition mechanism. Registering a competing override can fail.
- [MFI pressure override](https://github.com/sarbian/ModularFlightIntegrator/blob/03f07cd6e498ed003f84ee97ce976430e6447256/ModularFlightIntegrator.cs#L471): a replaceable calculation with an explicit base-call helper. Need ownership detection rather than unconditional registration.
- [KSPCF flight integrator](https://github.com/KSPModdingLibs/KSPCommunityFixes/blob/bc2e03ad1d1b0dcdb1d7d91b8f0f2d3d21cdb63a/KSPCommunityFixes/Performance/FlightIntegratorPerf.cs#L27): already replaces several stock calculations, including physics statistics and integration. Global Jakobian replacements would be a likely conflict.

Release sources were also fetched separately to distinguish current master from the binaries actually installed. [release-source-lock.json](release-source-lock.json) pins Kopernicus release-251 at `bab156c666d6ea96801dfdac29b7326368fc9bd9`, KSPCF 1.41.1 at `ca89fd3a154c69c05c09a0edd98c7b2ce4271503` and KSPTextureLoader 1.0.38 at `299e4bb238bc82fc3b609a88db32d8513f21168a`. Inspected release copies confirm the body event and KSPCF integrator replacement behavior above; their line numbers differ from master.

The [release parser implementation](https://github.com/Kopernicus/Kopernicus/blob/bab156c666d6ea96801dfdac29b7326368fc9bd9/src/Kopernicus.Parser/Parser.cs#L835) scans tagged external classes, matches parent node/mod/node name and creates a parameterless parser object. This is a real extension mechanism but does not itself pass the `Body` instance to that new parser. `OnBodyPostApply` does pass the body plus ConfigNode. These are references for a familiar provider/event API and optional interoperability; Jakobian's standalone loader must supply its own lifecycle and binding. The `.gitmodules` file retains an external-parser path, but this release archive already includes `src/Kopernicus.Parser`; the code was inspected there rather than assuming a missing submodule.

A static [parser-target inventory](parser-target-inventory.json) records **1,944** attribute occurrences from the pinned master tree. Multiple occurrences can represent related/aliased implementations; this count is not 1,944 independent features or evidence that Jakobian supports them.

## Findings

**Observed: familiar authoring can be retained.** The guide and examples use text `ConfigNode` trees and ModuleManager patches. New shape settings can be placed beneath a body, while orbit/science/basic appearance remain recognizable. The 2016 guide includes explanations and example values that should not override current source behavior; normalization flags, pressure units and load ordering need current verification.

**Observed: Kopernicus is more than its historical README description.** The README describes prefab modification, but the inspected tree includes runtime utilities, texture/on-demand infrastructure, events and flight/render integrations. Jakobian must audit those consumers, not assume Kopernicus stops affecting the system after injection.

**Observed: native terrain and scaled-generation interfaces assume a sphere.** `PQS.GetSurfaceHeight(Vector3d)` returns a radial height; it cannot return both the near and far faces of a torus or a cave roof/floor. Kopernicus's PQS pipeline and spherical scaled proxy use this model. Very large radial displacements may produce unusual silhouettes but do not supply general topology or consistent physics.

**Required direction: standalone framework.** The owner's clarification supersedes the initial extension-first recommendation. Jakobian supplies its own parser, native system construction, spherical feature implementation and general-body runtime. Kopernicus source can be referenced or adapted with provenance; installing Kopernicus is not required. Optional coexistence/config/provider interoperability must maintain single ownership of system creation.

**Unverified: Jakobian's standalone loading boundary.** A controlled prototype must confirm final ModuleManager nodes, pre-instantiation publication, descriptor attachment, scaled mesh construction, suppression of incompatible spherical contact paths and final body identity with Kopernicus absent. Separately establish safe ownership coordination when Kopernicus is installed. Source event availability does not establish either integration automatically.

## Ringworld lessons

Inspected `Geometry.cs`, `StockIntegration.cs`, `AtmosphereIntegration.cs`, `EvaIntegration.cs`, `RingResidence.cs`, `RingSurfaceWarp.cs`, `RingTrajectory.cs`, `RingWaterPhysics.cs` and `docs/developers/ISSUE-7-PERFORMANCE.md`.

- Its inner floor uses inward geometry normals and a rotating frame. Gravity includes centrifugal/Coriolis terms separately from physical mass gravity. A surface orientation must not be inferred from centre-to-position alone.
- It patches atmosphere, landing, navball, altimeter, camera, EVA, intakes, parachutes, buoyancy, resources and vessel caches. The required framework abstraction spans these consumers.
- Its central star remains the native main body; its own limitations document notes resulting science/orbit assumptions. Jakobian should represent independent bodies with native body identities when possible, with a distinct habitat-anchor mode for star-centred rings.
- `CalculatePhysicsStats`, `UpdateCaches` and `UpdatePosVel` can successively write surface values. Publishing the correct value too early can leave downstream consumers seeing spherical values again.
- Saved local pose and world identity matter across rails transitions. A fake Keplerian orbit is insufficient as the sole source of truth for a landed vessel on an arbitrary surface.
- The recent performance investigation found repeated equivalent cloud lighting, unbounded obsolete terrain retention and final conic-slot dereferences. Generalize the lessons: cache identical inputs, bound live work, validate orbit transitions and separate loaded/scaled/trajectory representations.

The chat reports an improvement from 1.7 to 12.1 FPS in a derived fast-flight workload. This is **prior-project evidence**, not a Jakobian benchmark and not certification of the original reporter's full mod stack. The ringworld's one-local-frame and overlapping-envelope limitations motivate explicit multi-vessel/frame tests here.

No Ringworld code was copied into this scaffold. If future code is reused, retain its MIT notice and review each vendor's separate license. Kopernicus's source header declares LGPL-3.0-or-later, while its README identifies separate GPL scatterer-derived shader code; a root license alone does not cover every vendor asset. Licensing/provenance must be recorded per reused component before distribution. The project owner's Jakobian license selection remains open.

## Geometry and numerical references

- [Unity 2019.4 MeshCollider manual](https://docs.unity3d.com/2019.4/Documentation/Manual/class-MeshCollider.html): contact mesh construction/cooking restrictions; runtime collision publication belongs on the main thread. Verify concave moving geometry behavior in the actual game's PhysX integration.
- [Transvoxel, Eric Lengyel](https://transvoxel.org/): transition cells for differing voxel resolutions. Candidate for a later volumetric backend, not a plug-in seam fix for arbitrary parametric charts.
- [Dual contouring publications, Joe Warren](https://www.cs.rice.edu/~jwarren/research/index.html): candidate for feature-preserving implicit surfaces; topology and QEF degeneracy need validation before adoption.
- Werner and Scheeres, *Exterior Gravitation of a Polyhedron…*, [DOI 10.1007/BF00053511](https://doi.org/10.1007/BF00053511): candidate physical gravity backend. The paper text was not retrieved in this pass; do not claim its implementation or formulas were validated. Start with independently tested point-mass/mascon models; investigate polyhedral acceleration and singular cases later.

## Limits of this pass

No exhaustive source audit of every Kopernicus PQSMod, graphics mod, autopilot, contract or life-support system. No full Kopernicus replacement, numerical orbit solver, volumetric renderer or compatibility adapter has been implemented. Tests listed in the validation record establish the scaffold and analytic prototype only. Questions requiring native flight experiments are tracked in [open questions](../planning/open-questions.md).
