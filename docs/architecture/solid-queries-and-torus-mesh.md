# Closed-solid queries and torus mesh foundation

Development 0.4.0 adds a core geometry contract that does not require one ground radius per direction. It is separate from the current native radial-body runtime. An enabled landable torus body is not yet implemented.

## Query semantics

`IClosedSolid` exposes a conservative bounding radius, signed Euclidean distance, explicit solid classification and all boundary contacts along a finite ray. The first `AnalyticSolid` implementations are a sphere and a circular torus aligned to local Y. A torus classifies its central hole as outside, independently of whether a unique nearest point exists there.

Distances are body-local metres. Ray directions are normalized internally, so returned distances are metres even if the supplied direction is not unit length. Hits are sorted, bounded to the forward segment and tagged `Enter`, `Exit` or `Touch` with an outward-from-solid normal and stable `exterior` surface ID. Inside-origin rays can begin with an exit. A complete empty hit array means no ground along that query, rather than a fabricated sphere or zero altitude.

`GeometryQueryStatus` distinguishes `Complete`, `InvalidInput`, `BudgetExceeded` and `Indeterminate`. Failed/incomplete queries expose no partial hit array. Every ray supplies a finite evaluation budget and spatial tolerance. The polynomial is restricted to the segment inside the bounding sphere; derivative roots split it into monotonic intervals and bounded bisection isolates crossings. No uniform surface sampling or unbounded Newton loop is used. Sphere intersections use a quadratic and torus intersections a quartic.

Ray parameters are scaled by the body bound and centred near the ray's closest point to the origin. Inputs that cannot support the requested precision are rejected. Current limits include tolerance/bound at least 1e-12, minor/bounding radius at least 1e-5, and a conservative origin-roundoff check. This is not arbitrary-precision geometry. Near-coincident/tangent contacts resolve only to the requested numerical tolerance; the residual guard rejects a contact more than four tolerances from the exact signed-distance boundary. Exact topological decisions below that numerical scale require a stronger provider, not a claim that floating-point roots are exact.

The existing nearest-point prototypes remain separate. In particular, the torus axis has a nonunique nearest point but still has valid occupancy and ray answers. The native `BodyQueries` service still owns only supported radial bodies; this core API does not silently reinterpret a torus as radial terrain.

## Mesh patches

`TorusMeshBaker.Build` creates one deterministic rectangular patch in periodic toroidal coordinates. Vertices are relative to a local double-precision anchor. Its integer sample grid wraps both periodic directions to the same canonical samples, so adjacent/seam patches reconstruct the same boundary points. Analytic vertex normals and triangle winding face away from solid material on both inner and outer walls.

A patch is bounded to at most 129 × 129 vertices. The caller chooses tile counts and tessellation and owns the returned arrays. This is a mesh bake primitive, not a complete streaming scheduler, tile cache, terrain modifier stack, UV/biome atlas or LOD stitcher. Terrain generation/version identity and resident anchors still need implementation before occupied worlds can depend on it.

## Native collider experiment

The explicitly enabled native test creates 32 mesh-collider patches, with a rotated root in an isolated local scene location. It checks outer, inner and upper-wall contact and verifies a ray through the empty hole has no collider hit. It disables/destroys the experiment after checking it; no collider or torus world is added to a player's save. This test executes from native startup probes, including flight and upgrade regression runs.

Core checks independently derive horizontal annulus and vertical tube crossings, tangent and inside-origin cases, budget/precision failures, periodic U/V seams, outward winding, two incident triangles per welded edge and torus Euler characteristic zero. Native contact validates Unity's mesh cooking, transforms and face orientation separately.

## Remaining integration gates

Before enabling a real torus fixture: build a general-body descriptor and renderer/contact lifecycle, stream/publish contact patches in the active native frame, adapt spherical kill/altitude/rails consumers for empty interiors, publish explicit no-hit/multi-hit navigation context, persist surface/material anchors, then exercise actual hole transit and inner/outer vessel contact through scene/warp/save changes. Stock point gravity and habitat apparent gravity remain independent choices. Passing isolated collider rays is not equivalent to landing a vessel on a torus celestial body.
