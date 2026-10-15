# Terrain, maps, biomes and resources

Status: proposed full specification. Development 0.5.0 implements the restricted [radial map and biome contract](../modders/radial-maps.md); the general chart/volume pipeline below remains future work. Jakobian configures native spherical `PQS` through its own standalone loader; supported modifier semantics must be implemented and tested.

## Terrain pipeline

General bodies use `Terrain { Mods { ... } }`, preserving ordered modifier authoring without pretending that every stock `PQSMod` understands arbitrary topology. Compile a deterministic sequence: base shape -> chart/volume geometry detail -> material/biome fields -> contact mesh -> visual material/scatter. Order is explicit and stable; generator version, seed and input hashes enter the world signature.

Simple surfaces support displacement `P' = P + H n_empty`. Height is metres and its positive direction follows the region's configured empty-space normal. Reject displacements that fold a patch, self-intersect a thin shell or remove the minimum thickness. Under overhangs a single heightmap is insufficient; use imported geometry or a volumetric modifier. Seam neighbors use identical physical samples and derivative-aware stitches. Finite features at a seam must not be generated twice.

Stock `VertexHeightMap`/noise/color/land-control concepts can become explicit adapters with documented inputs. Support familiar names only where semantics are proven. Mods accessing PQS radial directions, quad internals, spherical latitude or stock scatter parents remain spherical-only until ported. An unsupported modifier is a diagnostic, not a silently ignored node.

## Maps and projections

| Map kind | Sampling / units | Rules |
| --- | --- | --- |
| Height | linear scalar, metres through `offset + scale * sample` | explicit channel and precision; no color-space conversion; normal-positive direction |
| Albedo/color | color-managed visual channels | ordinary textures; separate from biome identity |
| Normal | tangent or body/object space | declare convention/handedness; tangent frame follows chart orientation |
| Roughness/specular/emission | declared linear material channels | independent of collision geometry |
| Biome IDs | discrete integer/palette labels | nearest/discrete lookup; no bilinear blending of identifiers |
| Resources | abundance field and biome/surface rules | conservative range, units and saved scan/discovery identity |
| Masks | continuous region weight or discrete label | type declared; no guessing based on file extension |

Projection options: legacy equirectangular for the standalone spherical path; toroidal two-angle; cylindrical ribbon; cube-face; mesh UV atlas with named islands; triplanar/body-local procedural fields; bounded volume textures for implicit terrain. A map set references `surfaceId` and `chartId`, wrap/clamp behavior, seam mapping, resolution and asset path under `GameData`.

For a ring floor, longitude wraps `[0,1)`, width clamps `[0,1]`; physical texel dimensions differ strongly around versus across the ribbon. Define noise frequency in metres or cycles per chart explicitly. For a torus both angles wrap; tangential distance varies with tube angle, so UV frequency is not automatically uniform physical frequency. Use chart metric/tangents for metre-based noise and normal-map gradients.

New-path maps use explicit data/color semantics, exact IDs and documented axis orientation. Historical Kopernicus biome palettes, normalization and fallback behavior are preserved by explicit compatibility implementations on the standalone spherical path. Offer migration/export tooling for maps; do not resample legacy biome identity accidentally while converting a pack.

Maps can be virtual/tiled rather than a single texture spanning an astronomical circumference. Asset tooling bakes mip/tile pyramids and independent data min/max bounds. Cache identity includes projection version, seam rules, source hash, geometry/generator version and sampling parameters. Never reuse a mesh cache merely because a filename matches.

## Biomes and gameplay

Biome identity is `(bodyId, surfaceId, biomeId)`, independent of display name, texture color and live patch index. A deterministic priority system resolves explicit polygons/masks, procedural elevation/slope/latitude-like fields, material regions and default biome. Equal priority collisions must either have an explicit tiebreaker or be rejected. Shape-specific coordinates replace meaningless universal latitude; authors may choose an equatorial coordinate intentionally.

Material (rock/sand/metal/ice), gameplay biome and decorative scatter are separate. A metal hull can have multiple research biomes; cliffs and ceilings can inherit a region's biome without becoming terrain-height maps. Science uses native body identity where available and an adapter-provided stable region subject for habitat-anchor mode. Preserve stock experiment/situation multipliers; allow local overrides without issuing duplicate rewards on an upgrade.

Resources need location and substrate, not just parent body. Surface extraction uses the selected contacted boundary/material and resource rules; orbital abundance reporting may aggregate by body. Integrate scanning/save coverage through adapters. Define below-ground depth relative to the chosen region/solid volume; a drill on an interior floor must not search through the star-centred radial coordinate. Dust/scenery visual maps must not alter stock resource amounts silently.

## Streaming and LOD

Parametric patches use quadtree/tiled surface streaming; mesh imports use clusters with a BVH; volume terrain later uses octrees and a validated seam method such as [Transvoxel](https://transvoxel.org/) for its intended voxel resolutions. Backend topology is not forced into stock PQS quads.

Select LOD by projected error, distance to actual boundary, curvature and visibility. A huge bounding sphere alone can make a camera in a torus hole falsely appear underground. Maintain independent budgets for worker generation, main-thread mesh upload, collider cooking, material/scatter publication and GPU memory. A CPU coroutine time slice cannot preempt a single expensive native collider cook.

Prioritize terrain using conservative swept vessel/part bounds, fall distance and braking/streaming horizon. Nearby loaded craft, EVA and debris share or union required footprints. Generate enough contact coverage before an off-rails restore/arrival. At unsafe travel speeds stop warp/hold a transition before the boundary; preserve actual velocity and communicate the reason. Do not spawn collision after the craft has passed through the surface or grant automatic braking.

Publish coherent mesh/collider/material generations atomically. Retain older coverage until replacement is ready; bound retained footprints and cancel irrelevant unpublished work so work does not grow with path length. Freeze the job's local chart during generation, then transform completed chunks into the current chart. Deterministic generation must give the same occupied surface across camera motion and LOD changes.

## Colliders and scenery

Use local terrain contact chunks with correctly oriented nonconvex/static or compatible kinematic MeshColliders, respecting [Unity 2019.4 restrictions](https://docs.unity3d.com/2019.4/Documentation/Manual/class-MeshCollider.html). Do not make the entire concave planet a dynamic rigidbody or treat two-sided rendering as two-sided collision. Rotating habitats need frame-consistent contact geometry; moving a static collider every tick is a risk to contact energy and broad-phase cost that must be measured.

Scenery uses stable hash-derived instance identity from world/region/tile/seed and physical slope masks. Render instancing and collider visibility are independent. Buildings/anomalies have explicit local anchors and scale; place roads/rivers by chart graph/physical paths. Prototype rivers can be authored/baked masks and fluid volumes; hydraulic erosion and flowing currents are optional generators, not an implied consequence of naming a water texture.

## First acceptance scenes

Sphere with legacy maps; static solid torus with both inner and outer surfaces; cylindrical floor with inward-positive mountains; a closed irregular asteroid with UV islands; a later cave with distinct floor and roof. Check seams, biome/resource attribution, collider normals, scaled appearance and a landed reload in every implemented family.
