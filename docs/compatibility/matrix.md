# Compatibility program and Kopernicus parity inventory

Status: architecture assessment. **No general-body compatibility is certified.** Boot coexistence of a diagnostic plugin is narrower than flight or save compatibility. Exact installed versions and test outcomes are recorded in [validation](../development/validation-record.json).

Development 0.10.0 requires the hash-locked HarmonyKSP 2.2.1.0 runtime for five early [load/start guards](../modders/load-admission.md). Their supported profile requires the exact game/library bytes and exclusive patch ownership of those five methods; conflicting ownership refuses admission, and loss of protection terminates the unsupported profile. Other mods may patch unrelated methods, but no new third-party flight compatibility is certified by this checkpoint. Kopernicus coexistence remains blocked pending single-loader coordination. See [current native evidence](../development/v0.10.0-validation.json), distinct from the original research bootstrap profile above.

## Compatibility levels

- **Native spherical path:** Jakobian configures valid stock spherical machinery through its own loader; supported Kopernicus-equivalent semantics need implementation and regression tests.
- **Shared-context candidate:** consumers of correctly published vessel/part caches may work without per-mod geometry changes; verify their actual read order and scene behavior.
- **Adapter required:** a mod reads radial geometry, scalar body environment or native conic trajectories directly.
- **Explicitly gated:** contradictory solver/geometry ownership or unsupported gameplay assumptions; no arbitrary-body support until a negotiated adapter exists.

No universal conversion can make `body.Radius` represent all surfaces of a torus. A direct caller of a radial API cannot recover which wall/chamber it meant. Some legacy mods require source changes or an adapter. The goal is broad coverage through stable interfaces and measured tests, not a promise that every binary can infer new topology.

## Kopernicus parity inventory

| Feature category | Jakobian standalone spherical responsibilities | Arbitrary body work | Parity gate |
| --- | --- | --- | --- |
| stock edits/new bodies/templates | own parser, template cloning and native publication | descriptor and stable binding | construction, cleanup and finalization with Kopernicus absent |
| body hierarchy/orbits/barycentres/post-spawn orbit | own hierarchy and native orbit setup/fixups | preserve native body/host orbit plus local field ownership | test all relevant hierarchy modes and save references |
| properties/rotation/tidal lock/homeworld | own supported property/rotation loaders | explicit spin/frame; arbitrary homeworld deferred | validate native homeworld/KSC/recovery assumptions |
| PQS terrain/noise/maps/materials/scatters/cities | own modifier loaders/native PQS setup | chart/mesh/volume terrain and explicit modifier adapters | inventory actual modifiers/providers, do not claim all by spelling |
| biomes/science/resources | own config/map/situation/resource integration | surface-aware context and stable identifiers | native archives, contracts and resource parity fixtures |
| atmosphere/AFG/curves/fog | own environment/material setup | local gas volumes and optical backend | physical and visual spherical parity |
| oceans/hazardous oceans | own ocean/buoyancy/hazard setup | reservoirs, buoyancy and local hazards | stock spherical ocean/underwater behavior |
| scaled mesh/materials/on-demand/cache | own mesh/material/cache pipeline | arbitrary mesh/bounds/picking/LOD | cache, texture and graphics parity fixtures |
| stars/light shifters/coronas/solar panels | own light/solar integration | separate illumination/geometry and occlusion | multi-star and stock consumers |
| decorative rings/particles/hazardous bodies | own supported visual/hazard providers | independently extend to bounded 3D volumes | feature-by-feature config/runtime coverage |
| asteroid/comet/discoverable objects | own supported native spawning/configuration | optional general-shape provider later | spawned-object/save/lifetime tests |
| SpaceCenter/debug/UI/settings/localization | own config/settings/lifecycle | region-aware diagnostics/anchors | arbitrary homeworld remains separate capability |
| external parsers and third-party PQSMods | own extension API and explicit interoperability adapters | explicit extension API/ported providers | provider inventory and binary/API adapter tests |

This checklist covers observed feature categories, not a declaration of complete enumeration. Before a standalone release, mechanically inventory parser targets and extensions in each supported Kopernicus release and associate each with implementation, fixture and known limitations.

## Mod families

| Mod / family | Assessment for arbitrary geometry | Required validation |
| --- | --- | --- |
| Kopernicus | source reference and optional config/provider/coexistence target; Jakobian owns loading | standalone baseline without Kopernicus, explicit single-injector coordination when installed, no duplicate services/bodies, packs and saves |
| HarmonyKSP | required pinned runtime from 0.10.0; supported cooperative patch profile | exact library/game hashes, unique installation, five exclusive guarded methods, refusal on changed ownership; no arbitrary-mod security guarantee |
| ModuleManager | keep actual parser/pass semantics | patched nodes, cache invalidation, `NEEDS`/body selectors, duplicate definitions |
| MFI | useful hooks, exclusive override slots can conflict | exact owner/base/post behavior, per-body scope and no double forces |
| KSPCommunityFixes | existing integrator replacements | final cache writers, aero/thermal/integration composition, stock parity |
| ordinary parts/resources/ModuleManager part patches | shared-context candidate | engines, intakes, drills, thermal, wheels, EVA and construction in each environment |
| FAR | adapter/owner negotiation required | local air velocity, density/sound/temperature and surface reference; avoid overriding FAR forces |
| RealFuels/AJE/SolverEngines | local input adapters likely | pressure/intake/oxygen and restart/thrust semantics; defer certification |
| MechJeb/kOS/autopilots | frame/trajectory adapters required for some modes | telemetry, heading, landing guidance and numerical maneuver prediction |
| Trajectories/maneuver/transfer planners | custom-field adapter required | authoritative field identity, noncentral prediction and visible truncation |
| Principia | conflicting dynamical ownership is explicitly gated | coordinate/force/ephemeris provider handshake; spherical compatibility alone does not imply local compatibility |
| SCANsat/mapping | atlas-aware adapter required | UV charts/multiple surfaces, stable coverage persistence, no sphere-only latitude inference |
| Contract Configurator/stock contracts | surface target and situation audit | waypoint coordinates, career rewards/recovery, body filters |
| Kerbalism/USI/life support/radiation | background/local environment adapters | atmosphere/oxygen/radiation, eclipses, unloaded numerical fleets |
| RemoteTech/CommNet | shape occlusion and distance audit | line-of-sight through holes, interior transmitters, relays and body hierarchy |
| EVE/Scatterer | adapters to Jakobian spherical setup; custom optical provider for general bodies | gas geometry, clouds, eclipses, water and local/scaled transitions |
| Parallax terrain/scatter | adapter to native spherical PQS configured by Jakobian; surface-backend adapter for arbitrary geometry | shader packed inputs, chart patches, instance/contact lifecycle |
| Kerbal Konstructs/cities/KSC relocation | surface anchor adapter | construction, orientation, facilities, recovery; defer arbitrary homeworld |
| boats/submarines/airships/hover systems | direct environment/force readers likely need adapters | immersion, ballast, buoyancy, airship lift and local gravity |
| Kopernicus Expansion and pack-specific PQSMods | explicit provider/binary adapters or source ports; no assumed loader dependency | every supported modifier's coordinates, threads and determinism |
| joint/physics-range/BDArmory mods | ownership/range/contact audit | multi-vessel, extended loaded footprints, debris and high-speed collision |

These assessments identify likely engineering requirements, not findings about uninspected current binaries. Pin versions/source and inspect each before building its adapter. Start with highest-value stock/parts/MFI/KSPCF coverage and common graphics/flight/environment mods; expand by real user demand and reproducible fixtures.

## Adapter contract and conflict reports

Adapters register supported mod/version and required provider roles, with exact query units/frames. Optional binaries must load lazily or reside in separate assemblies so a missing mod does not create type-load errors. Detect competing patch owners/signatures and report enabled/disabled capabilities per body. A part mod being installed is not enough to infer it owns a force calculation.

Regression matrix crosses stock sphere, opted-in sphere, torus, inward-floor habitat and irregular mesh with Flight/Map/Tracking, EVA, multiple craft, rails, reload and physics warp. A broad compatibility claim needs evidence across this matrix; a single bootstrap log proves only that these assemblies started together.
