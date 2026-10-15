# Gravity, reference frames and trajectories

Status: proposed full runtime design. Development 0.3.0 runs native central point gravity for radial bodies and exposes a shared geometry/navigation context, while the core also tests rotating-frame formulas. Custom fields and rotating-habitat force/cache adapters below remain future work. See [surface context](../architecture/surface-context.md) for the implemented vertical/normal/altitude distinctions.

## Geometry does not define gravity

Let a body specify a gravity model independently:

| Model | Meaning | Suitable use / limits |
| --- | --- | --- |
| `PointMass` | `a = -mu r / |r|^3` | maximum Keplerian compatibility; shape can still be nonspherical |
| `Mascon` | sum of bounded/regularized mass elements | irregular physical fields; near-source singularities require declared finite mass kernels |
| `Polyhedral` | acceleration/potential of a closed density mesh | later physical irregular solid backend; validate implementation and near-boundary behavior |
| `Potential` | `a = -grad Phi` | bounded conservative author/provider fields; smooth and correctly dimensioned |
| `VectorField` | declared acceleration with domain and transition policy | artificial/gameplay fields; need not be conservative, UI/predictor must disclose model |
| `Composite` | deliberate combination by coordinator | host tides plus body mass plus explicit artificial field; no accidental double counting |
| Rotation-only | apparent forces from selected local frame | spinning habitat; not an extra attractive mass field |

`geeASL` remains an unchanged spherical authoring concept on Jakobian's spherical bodies. For a non-sphere define a calibration point/region and reference radius if a convenience conversion to `mu` is desired. There is no unique global surface gravity on arbitrary geometry. Prefer explicit `gravParameter` and separate rotation parameters; if `mass`, `mu` and calibration inputs conflict, require a clear authoritative choice and diagnostic.

A physically gravitating torus need not pull normal to its surface. An artificially habitable shell may explicitly prescribe gravity toward selected walls. A physically rotating ring floor receives apparent outward acceleration while its habitable normal points inward. None of these models should be inferred from the word `Torus`.

## Rotating chart equation

Let `r` and relative velocity `v` be expressed in rotating axes. Let `Q` rotate body-local vectors into inertial axes; origin translation is `O(t)`, angular velocity is `omega`, and angular acceleration is `omegaDot`. The coherent equation is:

`a_local = Q^-1(a_inertial - O_ddot) - 2 omega×v - omega×(omega×r) - omegaDot×r`.

Position conversion is `x = O + Q r`; inertial velocity is `xDot = O_dot + Q(v + omega×r)`. Apply matching inverse conversions at chart/frame transitions. All terms must be expressed in the same axes before addition. Acceleration of the frame origin includes the chosen host orbit; physical host acceleration is not counted twice.

The scaffold's `ReferencePhysics.RotatingAcceleration` takes gravity and origin acceleration already expressed in the local axes. It independently checks centrifugal, Coriolis and Euler signs. It does not solve moving frames or apply game forces.

For a zero-mass cylindrical habitat at stationary relative surface position, apparent centrifugal magnitude is `omega² R`; desired apparent floor gravity `g` gives `omega = sqrt(g/R)` and `rotationPeriod = 2π/omega`. That relation excludes host/body gravity and tides, so a physically gravitating habitat needs a calibration solution using the full field. Motion introduces Coriolis effects. Changing spin without retaining Euler acceleration creates inconsistent flight.

Choose one canonical unloaded propagation frame and one loaded physics chart. Never add centrifugal/Coriolis acceleration to a solver already propagating inertial coordinates. Never apply rotation as both mesh motion and a fictitious force without the correct chart mapping.

## Force ownership

Loaded integration samples an immutable context per tick and applies acceleration once to each relevant rigidbody. Desired acceleration may replace or correct stock acceleration depending on verified ownership. Record `a_existing`, `a_desired` and frame convention in diagnostics. Do not use part mass twice: acceleration-force mode and force mode have different units.

Preserve KSPCF/MFI processing and third-party forces. Audit all rigidbodies associated with parts, servo mechanisms, debris, EVA/ragdolls and physical objects. Deduplicate per tick with reused storage. A COM approximation is allowed for weak field gradients; near small/thin geometry use per-part sampling or a declared tidal correction. G-meter/crew limits measure physical support/proper acceleration, not merely the magnitude of a coordinate fictitious force.

## Vertical, orientation and motion

`normalEmpty` is the geometry orientation. `apparentDown` is derived from the selected acceleration model and reference velocity. `navigationUp` uses a declared policy: opposite effective gravity where well-defined, transported previous frame near zero gravity or a region-authored reference. `north/east` are chart/material tangents with continuous transport and seam rules; on a torus there is no global stock pole convention that all regions must share.

Surface-relative speed subtracts material motion; airspeed subtracts local gas velocity; fluid-relative speed subtracts local current; orbital velocity remains inertial relative to native parent. Preserve these distinct values. Autopilots and cameras need a consistent reference frame, particularly when traversing a seam or crossing from inner to outer surfaces.

## Far-field and native conics

Retain stock conics where the physical field is exactly a configured point mass plus native dominant-body policy. A noncentral field cannot be represented exactly by one Keplerian `Orbit`. Two implementation modes are possible:

1. Numerical propagation throughout the relevant native SOI, with stock conics only outside a proven transition. This is the most truthful but expands integration and compatibility work.
2. A declared approximate far-field transition to a monopole. Choose error bounds and a transition shell, record model in the descriptor, and derive a smooth potential blend `Phi = w Phi_near + (1-w) Phi_far`. Its acceleration includes `-grad(w)(Phi_near-Phi_far)`; simply lerping accelerations is generally not conservative.

Do not switch force by nearest-surface distance each frame without hysteresis/state continuity. A habitat around the parent star has separate local region entry while host orbital motion remains defined by the star. There may be no valid native child-SOI hierarchy for an astronomical ring enclosing its host.

## Numerical flight, prediction and warp

Free flight inside noncentral fields needs persistent state vectors and a deterministic integrator with adaptive error and event handling. Start with a reference high-accuracy offline integrator; choose production method after comparing conservative-field energy/angular momentum and time-dependent cases. Atmosphere adds drag/heating and requires stronger warp restrictions. Arbitrary thrust during packed numerical flight is a separate capability.

The map predictor uses the same field/environment service as authoritative flight but runs with a bounded budget and explicit truncated/approximate status. Native `Orbit` can provide a compatibility/osculating view; it cannot become the truth for local numerical propagation. Maneuver node prediction, autopilots and patched-conic mods require adapters.

Before a large warp step, conservatively detect crossings of terrain, atmosphere, fluid, field boundaries and SOI transitions along the entire path. Check the complete swept craft bounds, not just vessel COM. Use bracketing/refinement or conservative spatial bounds; signed endpoint values alone can miss two crossings through a torus or a thin wall. Stop at the earliest event and prepare collision before unpacking. Clamp the requested step by error/event budget instead of blocking the UI in an enormous catch-up loop.

Keep landed/splashed anchored vessels on the material surface while on rails; do not integrate them as free particles. Store local pose and restore velocity from origin/spin/current motion plus allowed relative motion. Background fleets require deterministic advancement, saved epochs, reload validation and event detection. Do not call `nextPatch` after a native FINAL conic or follow null/cyclic chains.

## Validation gates

Zero-spin spherical parity; orbit energy convergence; known point-gravity cases; torus hole transit; same inertial state before/after chart transition; centrifugal equilibrium and Coriolis deflection; zero-gravity orientation; high-speed swept impact; many-hour warp/reload; two loaded craft plus debris/EVA; docking/undocking and scene changes; force-owner composition with MFI/KSPCF. Numerical tolerances and model approximation error are separate from visual quality.
