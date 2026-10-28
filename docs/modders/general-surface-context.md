# Experimental general-solid surface context

Development 0.7.0 adds a query layer for nonradial solids. It accepts a coherent geometry provider and an explicitly supplied acceleration. This is a prerequisite for a future KSP torus runtime. The enabled celestial-body parser, radial `BodyQueries`, altimeter, navball and camera retain their existing behavior.

## Local query

```csharp
var geometry = new Jakobian.Core.AnalyticSolid(1000, 200); // Torus, +Y axis, metres.
var settings = new Jakobian.Core.GeneralQuerySettings(
    referenceRadius: 1000, maxGroundDistance: 5000,
    tolerance: 1e-5, evaluationBudget: 4096);
bool valid = Jakobian.Core.GeneralSurfaceQuery.TrySample(
    geometry,
    new Jakobian.Core.Vector3d(750, 0, 0),
    new Jakobian.Core.Vector3d(-1, 0, 0), // Supplied local acceleration in m/s².
    new Jakobian.Core.Vector3d(0, 1, 0), // Reference pole for heading.
    settings, out var context);
if (valid && context.Ground.HasValue)
{
    double clearance = context.Ground.Value.Distance;
    // Use only for this ray request; it does not guarantee actor/collider admission.
}
```

`IGeneralSurfaceGeometry` combines `IClosedSolid` and `IAnalyticSurface` under one provider. Classification, signed distance, nearest projection and ordered ray crossings must describe the same solid, coordinate frame and generator. `AnalyticSolid` implements this for spheres and circular ring tori. It outlines the contract for future providers, but third-party provider authoring is incomplete: `SolidRayHit` still has an internal constructor, so an external assembly cannot create ordinary hit records through a supported public factory. This increment exposes consumption of the built-in providers, not a complete geometry plug-in SDK. A later ABI task must add validated hit construction and provider conformance tests; imported meshes and function programs are not automatically validated by implementing an interface.

| Result | Meaning |
| --- | --- |
| `Location` | Inside, outside or on the solid boundary within the requested tolerance. |
| `SignedDistance` | Euclidean signed distance; negative means inside material. For the implemented analytic providers it is exact apart from numeric error. |
| `Nearest` | Nullable nearest surface projection with point, outward normal and chart coordinates. Missing at nonunique projections. |
| `ReferenceAltitude` | Distance from local origin minus the explicitly supplied reference-sphere radius. It is a datum, independent of solid occupancy or ground. |
| `Up` | Nullable unit vector opposite the supplied acceleration. It is independent of the nearest/ground normals. |
| `North` / `East` | Nullable heading axes from the reference pole projected perpendicular to up. East is `up cross north`. |
| `Ground` / `GroundClearance` | Nullable first entering boundary along gravity within the finite ray range. Only present with `GroundStatus.Contact`. |

In the example, nearest clearance is 50 m, nearest point is `(800,0,0)` and its normal faces into the hole (`-X`). Navigation up is `+X`. Gravity down travels through the hole and reaches the opposite inner wall at `(-800,0,0)`, 1,550 m away. Datum altitude is -250 m, while the actor is outside solid material. These four results must not be substituted for each other. A nearest surface can be overhead relative to gravity.

`AnalyticSolid` torus projections use `torus-uv/v1`, matching the [material anchor API](experimental-torus-provider.md). The older standalone `TorusSurface` prototype retains its historical `toroidal` chart label. Identical U/V values do not authorize automatic migration of arbitrary saved vessel state.

## Availability and failure

`TrySample` returns false for invalid common inputs (including null geometry, nonfinite vectors, undefined reference pole and default settings) or invalid geometry classification/distance. Do not use its default output on false. Settings require finite positive radius/tolerance, nonnegative finite ray range and an evaluation budget from 1 to 65,536.

A valid context may still have no ground:

| `GroundStatus` | Ground policy |
| --- | --- |
| `Contact` | Complete ray query, first crossing enters solid; ground is available. |
| `Miss` | No boundary within this finite ray. This does not prove an infinite path is clear. |
| `NoGravity` | Supplied acceleration has zero representable magnitude; up/heading and ground are absent. Nearest geometry can remain available. |
| `InsideSolid` | Actor is buried. An outgoing boundary is not accepted as supporting ground. |
| `OnBoundary` | Actor starts within the classification tolerance; a contact/recovery owner must resolve support. No invented zero clearance. |
| `Tangent` | First boundary is a graze, not an entering support contact. No later wall is silently selected. |
| `BudgetExceeded` | Geometry work did not complete. Any returned partial hits are withheld. |
| `Indeterminate` | Geometry solver cannot certify the result at this scale/tolerance, or the first crossing is inconsistent with an outside start. |
| `InvalidInput` | Geometry provider rejected the ray settings; other context fields can remain available. |

Interior/boundary status takes precedence over zero-gravity status. Heading is absent when the projected reference pole has length at most `1e-10` after normalization. Nearest ambiguity does not suppress a separately unique ray contact. The torus axis, for example, can have no unique nearest point while a horizontal ray reaches an inner wall. No fallback vertical, nearest floor or positive radius is invented.

This point query is not actor sweep/contact coverage, collision recovery, an influence-volume test or a force integrator. Pass the acceleration owned by the active physics coordinator; supplying a surface-normal attraction merely to obtain a convenient floor does not implement physical gravity or a rotating habitat.

## Unity physics-chart bridge

`Jakobian.KSP.GeneralBodyQueries.Sample(root, geometry, worldPosition, gravityWorld, northAxisWorld, settings, out result)` accepts KSP double vectors expressed in the current Unity physics chart. Call it on the Unity owner thread with a caller-owned rigid root. It returns `Available`, `InvalidFrame` or `InvalidRequest`. `Available` means the context is valid; callers must still check optional ground/nearest/heading values.

The bridge captures the root's matrix once, translates positions and rotates directions in double arithmetic, then exposes `Local` plus nullable world-space point/normal/heading results and the sampling Unity frame. Translation never applies to acceleration, normals or headings. It inspects the actual matrix, including parents: nonfinite, scaled, singular, reflected or sheared frames are rejected. Unit lengths, orthogonality and positive handedness are checked to `1e-6`, allowing native float rotation roundoff. Native transform precision still limits correspondence; the fixture accepts analytic transform distances within 2 mm at its 20 km translated origin and collider distance within 2 m at its mesh tessellation.

The root origin is Unity's float transform position. World input doubles avoid an extra point-to-float conversion, but do not recover precision already lost by the root. This is a snapshot in the current floating-origin chart, not an inertial frame or persistent frame ID. Origin shifts, spin, body identity, reference epoch and surface velocity remain a coordinator's responsibilities. Do not cache the snapshot across chart changes.

The API does not bind `CelestialBody`, populate KSP native caches, set vessel situation, alter stock down, infer forces or admit a vessel to incomplete collider coverage. Those integration steps have separate acceptance gates in the [prompt roadmap](../planning/prompt-roadmap.md).

## Verification

Core tests cover exact sphere/torus oracles, 1,200 deterministic normal-ray samples, zero gravity, heading poles, ambiguous projections, buried/boundary states, grazing/missing/out-of-range rays, invalid inputs and partial/budget/conditioning failures. The native startup checks compare six transformed torus contacts in two frames and one sphere contact against actual colliders, verify the hole and reject invalid frames including parent shear. The [0.7.0 record](../development/v0.7.0.md) distinguishes executed checks from pending/full-game integration.
