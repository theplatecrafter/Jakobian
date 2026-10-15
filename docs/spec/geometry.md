# Shapes, topology and the meaning of ground

Status: proposed specification v0.1. The scaffold implements only analytic sphere/solid-torus nearest samples, not this full contract.

## A body is a collection of regions

Separate **solid occupancy** from **boundary surfaces** and **gameplay semantics**:

- A solid provider answers whether a point is material, empty, on a boundary or indeterminate. Hollow interiors and torus holes are empty.
- A surface provider supplies oriented boundary geometry, closest/ray/sweep queries and stable region/chart identity.
- A surface region supplies material, collision, landing, map, biome and environment references. A wall can collide without being a designated habitat floor. A ceiling can be landable with a suitable craft/gravity configuration.
- Gravity and air are independent spatial fields. Defining a torus never automatically supplies attractive gravity toward its tube or artificial gravity toward its inner rim.

**Ground is the contacted boundary of solid material, with a selected surface identity.** For clearance/navigation it is a candidate boundary selected by query direction and context. For `LANDED`, it is sustained real support contact meeting the situation policy. It is never simply the first point on a ray from the body's centre, a UV map's lowest pixel, or a collider closest to the vessel regardless of side.

A ring habitat is not a mathematical zero-thickness annulus. Its floor, outer hull, rims/walls and any caps enclose material with explicit thickness. An optional thin-shell provider must declare two oriented faces and a collision thickness, rather than rely on a two-sided shader to supply physical ground.

## Supported shape families

| Family | Definition | Terrain backend | Important restriction |
| --- | --- | --- | --- |
| Sphere | radius or native PQS configured by Jakobian | stock quad sphere | spherical compatibility path |
| Star-shaped radial solid | positive `r(direction)` | radial patches | exactly one boundary radius per direction; cannot represent torus holes or caves |
| Ellipsoid / primitive solid | bounded analytic function and transforms | analytic chart patches or baked mesh | nearest point is not generally obtained by radial scaling |
| Solid torus | major/minor radii with `R > r > 0` | two periodic charts/patch hierarchy | axis and tube-centre nearest queries can be nonunique |
| Cylinder / RingHabitat | dimensioned shell with named floor/hull/caps/rims | analytic parametric charts | finite bounds, explicit interior/exterior orientation and thickness |
| Parametric atlas | `P_i(u,v)` and chart adjacency | chart quadtree | finite regular derivatives locally; inverse query acceleration must be supplied/baked |
| Imported mesh | baked triangles, BVH, charts, region labels | mesh clusters; optional normal displacement | closed oriented manifold for volume/inside claims; units/transforms required |
| Implicit / CSG solid | bounded zero set `F(x,y,z)=0` | voxel/octree contouring | regularity, feature size and evaluation limits; a general scalar field is not necessarily a distance |
| Composite | transformed child regions with stable IDs | shared spatial index | overlapping material resolved by declared CSG rules; disjoint components remain distinct |

Choose primitives first, heightmaps for detail, mesh import for artist-defined topology and a bounded expression language for advanced authors. Do not require every beginner to write an implicit equation. Arbitrary shape means extensible finite geometry within supported numerical/physics bounds; no promise that every mathematical set is landable.

## Coordinate convention

Canonical Jakobian coordinates: metres, seconds, kilograms; right-handed body-local axes; `+Y` is the nominal spin axis; at the identity orientation the zero-longitude reference points along `+X`, with increasing chart longitude toward `+Z`. All rotations include an epoch. Convert explicitly at KSP/Unity/orbit boundaries: KSP vectors, Unity transforms and orbit state vectors must not be assumed to share a permutation/sign convention.

For a solid torus:

`P(u,v) = ((R + r cos v) cos u, r sin v, (R + r cos v) sin u)`

Here mathematical `u,v` are radians in `[0,2π)`. Texture coordinates use normalized `u/(2π), v/(2π)`. The analytic outward normal is `(cos v cos u, sin v, cos v sin u)`. At `v=π` it faces the central hole. These are geometric normals, regardless of the chosen gravity.

For a cylindrical interior floor with radius `R`, physical width `W` along `Y`:

`P(u,v) = (R cos u, (v - 1/2) W, R sin u)` with mathematical `u` in radians and normalized `v` in `[0,1]`.

Its empty-space normal is `(-cos u,0,-sin u)`. If terrain grows into the habitable interior, use `P_terrain = P + H(u,v) n_empty`, so positive terrain height reduces the radial coordinate. Outer-hull terrain uses its own outward normal and chart. Do not invert a copied exterior map silently.

The collider triangle winding must agree with this normal after all coordinate conversions and negative-scale rejection. At creases retain per-face contact normals and region identity; a smoothed shader normal is not a collision normal.

## Topology and charts

A chart has `chartId`, `surfaceId`, domain, periodic axes, mapping, oriented normal convention and adjacency edges. An adjacency edge defines neighbor chart, edge mapping, direction reversal, stitch rule and material continuity. UV seams may duplicate rendering vertices but must not split physical continuity. Patch keys use integer level/tile coordinates, not rounded float UV values.

Spheres need pole handling and commonly six cube-face patches internally even if maps are latitude/longitude. A torus wraps both axes. A floor ribbon wraps longitude and has finite across-width edges joined to named rims. Imported meshes can use multiple UV islands; inverse mapping returns an island/chart ID as well as UV. Texture UV alone cannot identify a unique world point.

Closed mesh checks: finite vertices, legal indices, no degenerates, paired edges with consistent orientation, self-intersection checks, consistent scale and connected components. Each connected boundary is two-manifold for the first mesh backend. Hollow solids can have nested oriented boundaries; genus is metadata/validation output, not the renderer's surface selector. Open/nonmanifold meshes require an explicit shell-only capability and cannot support assumed interior pressure/fluid volumes. Non-orientable surfaces cannot enclose an ordinary signed solid; defer or reject them for full landable-body mode.

## Query contract

Proposed surface result fields:

| Field | Meaning |
| --- | --- |
| `bodyId`, `surfaceId`, `chartId`, `primitiveId` | stable ownership and precise boundary identity |
| `pointBody`, `normalEmpty`, `tangentU`, `tangentV` | body-local position and oriented geometric frame |
| `materialCoordinate` | chart coordinate that persists across rendering LOD changes |
| `distance`, `signClassification`, `errorBound` | distance with explicit semantics and uncertainty |
| `materialId`, `biomeId`, `terrainVersion` | coherent environment/generation attribution |
| `surfaceVelocity`, `normalValidity`, `availability` | motion, uniqueness and streamed-data status |

Queries are distinct operations:

- `ClassifySolid(p)`: material occupancy; no nearest normal required.
- `NearestBoundary(p, filters, preferredSurface)`: nearest allowed boundary, possibly ambiguous. Hysteresis preserves a previous surface when candidates are nearly tied.
- `RaycastAll(origin, direction, range, filters)`: sorted hits, including entry/exit and region IDs; caller-provided output buffer and overflow status. A centre ray can cross a torus four times.
- `SupportClearance(p, vertical, preferredSurface)`: ray/sweep along local down, separate from nearest Euclidean distance.
- `ResolveMaterialCoordinate(anchor)`: restore a persisted point/normal without depending on live mesh LOD.
- `SweepBounds(pose, velocity, timeRange)`: conservative collision/warp/streaming bounds, not just a sample at the endpoint.

At a sphere centre, torus axis, tube centre or medial-axis ambiguity, return nonunique/indeterminate normal. Keep a previous transported navigation frame when appropriate; zero gravity cannot define a fresh down vector. Never divide by a zero gradient or fabricate a global up and call the result physical.

## Distance and sign are not universal altitude

For a sphere, signed distance is `|p|-R`. For a solid torus with axis `Y`, it is `sqrt((sqrt(x²+z²)-R)²+y²)-r`. Negative means solid material; the central hole is positive. A habitat floor's local positive height goes into its habitable empty region. These signs do not automatically give a global atmosphere coordinate or ocean depth.

Expose at least: native radial/reference altitude, Euclidean nearest-boundary distance, support clearance along selected down, chart terrain elevation, atmosphere datum coordinate and fluid depth. Use explicit UI labels. Stock-compatible scalar fields must have a documented choice and an adapter-specific validity region.

## Mathematics/expression authoring

An optional expression node defines `field`, bounded extent, minimum feature size, analytic/numerical gradient policy, coordinate units and evaluation budget. Example torus field: `(sqrt(x*x+z*z)-R)^2 + y*y - r*r`. This polynomial-like field has units of square metres and is **not** a signed distance in metres.

The expression parser is an offline/compiled deterministic AST with an allowlist of arithmetic, trigonometric and bounded noise primitives. No arbitrary C# execution, filesystem operations or unbounded loops. Reject NaN/Inf, invalid parameters, degenerate gradients on the required boundary and unresolved features below the configured collision resolution. CSG `min/max` distance combinations have sharp gradients and may only be conservative distance bounds, depending on operands and transformations.

Do not sphere-trace arbitrary `F` by treating its magnitude as a step length. Use an actual distance bound/Lipschitz estimate, conservative bracketing/root refinement or mesh BVH queries. Large bodies can have a coarse whole-body occupancy oracle and locally refined contact surface, with uncertainty explicitly reported.

## Editing, validation and diagnostics

Author tooling should show solid/empty slices, oriented surface names, chart seams, gravity arrows, air/fluid volumes, scaled proxy and collider mismatch heatmaps. Compile a descriptor manifest and deterministic cache key. Author can select floor regions visually and assign stable labels in an import sidecar. A linter must identify an unlabeled interior, reversed hull, invalid atlas seam, impossible fluid volume or occupied surface change before the game loads it.

Initial topology is static. Runtime terrain deformation/digging and topology changes require versioned occupancy migration, contact stabilization and cache invalidation; keep these outside the first framework release.
