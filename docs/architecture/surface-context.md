# Shared surface and navigation context

Development 0.3.0 introduces a first runtime query service for the supported radial bodies. It addresses the Ringworld lesson that camera, flight UI, physics caches and resource consumers must agree about position, reference frame and surface identity. General-topology and arbitrary-field adapters remain future work.

## Three distinct directions and distances

- **Gravity vertical:** the direction opposite physical gravitational acceleration. Current bodies use native central point gravity, so up is radial. The navball's level horizon and gravity-relative compass should use this vertical.
- **Geometric normal:** the direction away from solid terrain, perpendicular to its local boundary. This describes contact orientation and slope. It is generally different from gravity vertical on an ellipsoid, cube or peanut.
- **Ground below:** the first surface encountered along a declared downward query. The current radial backend can answer the centre-directed ray exactly. Its signed clearance is `length(position) - terrain.RadiusAt(direction)`. It is not Euclidean nearest distance or normal displacement.

Reference altitude remains centre distance minus native reference radius. It may be kilometres above zero while the craft is touching ground. Stock datum/terrain altimeter modes retain their existing meanings; the flight readout labels both values explicitly. Do not replace datum altitude globally with terrain clearance or point-gravity acceleration with the terrain normal.

Rotating habitats and custom fields will require an explicit apparent-vertical policy, including what happens near zero effective gravity. A torus hole may have no down-ray hit or several boundary intersections. The radial query must not be used for those bodies by pretending that a hit always exists.

## Implemented queries

`RadialSurfaceQuery.TrySample` is pure core code. Inputs use body-local Y-up metres. It returns the radial ground point, analytic outward normal, radial clearance, datum altitude, latitude/longitude, slope and compass basis. Normal derivatives are analytic for all four base shape families. Development 0.5.0 adds `MappedRadialSurface`: the same map-displaced radius drives PQS, the scaled mesh and shared queries; displaced normals use symmetric tangent derivatives. See [radial maps](../modders/radial-maps.md). The centre and nonfinite inputs fail. At a pole the result explicitly has `HasHeading = false`; north/east are zero instead of an invented compass direction.

`Jakobian.KSP.BodyQueries.Sample` adapts that result to a live body and current floating-origin world position. It accepts only the registered live Jakobian body instances, never a stock body or a same-named prefab. It returns an explicit status; default output fields are not usable unless the status is `Available`.

The result includes stable body/surface IDs, local geometry, world ground/normal/gravity/up/north/east, and the native corotation velocity evaluated at the query point relative to the body's translating origin. The latter is not the body's orbital velocity or a rigidbody velocity in the Krakensbane frame. `UniversalTime` and `UnityFrame` identify the snapshot; neither is a durable origin or surface-anchor identity. Re-query after a frame/origin/UT change. Calls touching game objects run on the Unity main thread. The flight panel and native-cache comparisons query at vessel centre of mass; camera queries use the actual camera endpoint.

The current environment provider explicitly reports airless/dry, zero pressure Pa and zero gas density kg/m³. This does not set ambient radiation/temperature to zero, provide water physics or invent resource abundance. The sample separately supplies a `BiomeName`, using the authored Atlas palette or the retained native template map on the older fixtures.

KSP's `CelestialFrame` uses Z-up while Unity and PQS use Y-up. Both vector transforms swizzle X/Z/Y before and after the native frame operation. Native checks compare positions/coordinates to independent `CelestialBody` APIs, including oblique latitudes/longitudes and a near-pole point.

## Consumer ownership

| Consumer | Current implementation | Later adapter work |
| --- | --- | --- |
| Navball / north / up | retain native central-gravity basis; compare actual navball rotation and vessel/global up to shared queries in flight | explicit apparent-vertical and tangent-heading provider for custom fields, poles and inward habitats |
| Altimeter | retain native datum and terrain modes; add labeled datum/ground readout | no-hit/multi-hit general queries, shape-aware native consumers where the radial path no longer applies |
| Camera | scoped endpoint clearance guard after native camera update; restore its presentation offset before the next native camera update | swept segment occlusion, all camera modes, thin walls, interior terrain and competing camera providers |
| Vessel caches | read back native up, altitude, terrain clearance and surface velocity; no replacement force or cache writes | inspect each actual final writer and consumer; publish coherent local state at proven boundaries |
| Air / fluids | native airless/dry behavior, explicit query capability | per-part volume/datum/flow, thermal/intake/aero/buoyancy adapters |
| Resources | create a native planetary abundance request using shared body/index/lat/lon and native datum altitude | custom chart biomes, resource maps, scanning and extraction on arbitrary surfaces |

`CreatePlanetaryResourceRequest` is a coordinate bridge. `ResourceMap` retains abundance, biome, scanning and lock behavior. The caller names the resource and chooses `CheckForLock`. No global harvesting/intake/resource patch is installed. Development 0.5.0 supplies an authored radial biome map on Atlas through native `CBAttributeMapSO.GetAtt`; older fixtures retain their template-derived biome data. Native science and resource lookup are checked against the shared biome sample. Calling the native abundance lookup does not certify drilling, scanners or resource balance.

## Camera constraints and scope

The guard runs only in external Flight camera mode on a loaded, unpacked active vessel whose live body is owned by Jakobian. Map, IVA/internal/external-provider modes and stock bodies are excluded. When the camera endpoint falls below its radial ground plus 2 m, it moves outside and looks back at the native pivot using gravity up.

The correction is a presentation offset. The original local camera pose is restored during the next Update, before native LateUpdate, only if the transform/parent/pose still match the offset Jakobian applied. Native pitch, heading and zoom state are not overwritten. This avoids feeding collision correction back into smoothing each frame and avoids undoing another owner's changed pose.

The guarantee is endpoint clearance for this radial family. It does not guarantee an unobstructed vessel-to-camera segment, a clear near plane on steep slopes or behavior under every camera mod. The flight readout exposes a guard toggle; F8 hides/shows the readout. F2 hides it with native UI.

## Validation scope

Pure-core checks compare analytic normals to independent finite-difference boundary tangents, verify signed clearance/datum separation, native-style coordinates, compass handedness, pole ambiguity and invalid inputs. An oblique ellipsoid case proves that gravity vertical and geometric normal differ by over 50°.

Native flight checks compare the actual navball rotation, vessel/global up, datum altitude, radar clearance, surface-velocity caches and vacuum values. They switch the actual native altimeter between ASL and AGL, inspect its value after native LateUpdate and restore its original mode. They submit a native Ore abundance request through the coordinate bridge. A deliberate underground camera pose exercises the production guard, checks unchanged pitch/heading/zoom and stock-body exclusion, then verifies endpoint clearance through ten native camera updates. Landing and save/reload tests continue separately. Exact results belong to the versioned development evidence, not this design description.

The camera offset rule and final-writer audit were informed by read-only inspection of the prior Ringworld implementation. [Provenance](../research/navigation-provenance.json) records the local reference hashes; no upstream patch source or binary is bundled.
