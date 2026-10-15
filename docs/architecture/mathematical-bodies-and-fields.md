# Mathematical bodies, mass and influence volumes

Accepted direction, 15 October 2026. This is the target architecture, not the set of shipped providers. Current code has analytic radial surfaces, mapped radial displacement, sphere/torus closed-solid queries and rotating-frame formula tests. It does **not** yet run a torus celestial body, numerical gravity, centrifugal habitats or authored expression programs in flight.

## Four independent definitions

An authored object compiles into geometry, material motion, physical fields and influence policy. Each has its own stable ID, units, bounds, version and capabilities. An atmosphere or reservoir is another spatial domain, not a consequence of naming a body a ring. A single immutable descriptor must supply rendering, contact, navigation, resources, background propagation and persistence.

| Definition | Mathematical contract | Consumers |
| --- | --- | --- |
| Solid and boundary | Closed occupancy with signed inside/outside convention; oriented boundary regions and bounded intersection queries | Collision, landing, occlusion, terrain, volume integration |
| Surface parameterization | `P(u,v)` plus domain, adjacency, orientation and error bounds | Meshes, maps, biome/material coordinates and anchors |
| Density/mass | `rho(x,t)` over a declared solid/domain, or an explicit discrete mass model | Mass, centre of mass, inertia, gravity/potential |
| Motion | Origin `O(t)`, orientation `Q(t)`, their derivatives and any separate material motion | Surface velocity, frames, contacts, air and landed anchors |
| Physical field | Acceleration, optional potential and spatial/temporal validity | Loaded forces, unloaded propagation and trajectory prediction |
| Influence volume | Containment, entry/exit events, priority/composition and transition policy | Domain selection and solver handoff |

`Properties.gravParameter` remains a useful explicit point-mass model. It is not automatically recalculated from the visible mesh. A non-spherical solid can intentionally have point gravity, physical distributed gravity, prescribed artificial gravity or negligible local gravity.

## Defining ground mathematically

A closed implicit solid uses `F(x,y,z) < 0` for material, `F = 0` for its boundary and `F > 0` for empty space. On a regular boundary, `gradient(F)/|gradient(F)|` points into empty space. An arbitrary implicit function is **not** necessarily a signed distance; ray marching cannot safely step by its raw magnitude. Providers need a proven distance/Lipschitz bound or bounded root isolation. Singular gradients, unresolved roots and exhausted budgets must be explicit query failures, never fabricated ground.

A torus aligned to local Y can use `F = (sqrt(x*x+z*z)-R)^2 + y*y - r*r`, with `R > r > 0`. Its centre is empty and one ray can cross four surfaces. The existing analytic torus provider already tests that occupancy and multiple intersections. A thick ring habitat can instead be an annular finite cylinder, e.g. the intersection of `Ri <= sqrt(x*x+z*z) <= Ro` and `|y| <= halfWidth`, with explicit junction treatment. Its inner cylindrical wall faces inward and may be labeled `floor`; outer hull, rim faces and other walls remain real solid boundaries. A two-dimensional infinitely thin floor is not a closed solid with physical volume.

`P(u,v)` is an alternative surface authoring method. Require finite domains, seam adjacency and orientation; a local normal comes from oriented partial derivatives. A parametric surface alone does not establish enclosed material, distinguish a cave from an exterior, or prove freedom from self-intersections. Closed validated meshes and solid-composition providers can supply that missing occupancy contract. Multiple disconnected components and cavities need stable component/region IDs.

For a circular torus, `P(u,v) = ((R+r*cos(v))*cos(u), r*sin(v), (R+r*cos(v))*sin(u))`, with both angles periodic. For an annular cylinder's inner floor, `P(u,v) = (Ri*cos(u), v, Ri*sin(u))`, with periodic `u` and finite axial `v`; the oriented `dP/du cross dP/dv` points inward into the habitat. The rest of the finite wall volume must still be authored. Parameterization direction is explicit so a renderer and collision baker agree about which side is empty.

Landing is a vessel support/contact decision on a collision-enabled oriented boundary, using relative material velocity. It cannot be defined by the sign of a radius or the nearest surface alone. A floor label guides navigation and authoring; it does not remove collisions on walls or ceilings. Altitude needs an explicit query: nearest boundary, ray clearance along a declared down direction, or datum coordinate. Report no hit/ambiguous/inside-solid states distinctly.

## Safe function authoring

Plan a bounded expression language for scalar/vector functions with explicit variables (`x,y,z,u,v,t` as appropriate), units, parameters and declared domain. Compile a parsed AST into immutable instructions, never execute arbitrary C#, Python or shell text from a planet pack. Bound instructions, recursion/composition, spatial extent and query work. Reject nonfinite values, unsafe division, invalid roots and unknown names with source locations. Time-dependent geometry requires a separate moving/deforming collision capability, so it is not enabled merely because a function mentions `t`.

Expression providers require explicit accuracy and regularity contracts. Numerical derivatives need reproducible stencils and diagnostics; supplied gradients can be checked against finite differences. Non-distance implicit functions need a conservative bound provider. Adaptive sampling must either certify a patch/event to the declared tolerance or return incomplete status. A sampled grid is an approximation with resolution limits, not proof that an arbitrary function has no narrow features.

Version the function source, constants, compiler/math conventions and asset hashes in the world descriptor. Editing an established surface requires a migration or a new identity. Do not relocate a saved craft by silently reevaluating a changed mathematical function.

## Mass volume and gravity

For a volume density, `M = integral_V rho dV`, centre of mass `c = integral_V rho*x dV / M`, and inertia about `c` follows the usual integral of `rho*(|r|^2 I - r*r^T)`. Density is kg/m³ and the volume must be finite and integrable. A thin shell needs finite thickness or an explicit surface density in kg/m², with a different integration contract. A visual material/color is not density.

Volume quadrature is an offline compilation task with convergence/error estimates. Preserve cavities and disconnected components. Require one authoritative mass model; reject conflicting `mass`, `mu`, density and mass-element inputs unless an explicit normalization policy is selected. Zero density is useful for an engineered habitat whose own attraction is negligible. Gravity from a physically massive torus may pull at an angle to its floor, so gravity direction and surface normal remain separate.

Constant-density primitives provide independent integration oracles: a circular torus has volume `2*pi^2*R*r^2`; a finite annular cylinder has volume `2*pi*halfWidth*(Ro^2-Ri^2)`. Multiplying by constant density gives total mass, but a point attraction using that mass still approximates the near field. Those formulas must not be applied to a clipped, hollowed, displaced or composite object without accounting for its changed occupancy.

Provider progression: verified point mass; finite-kernel mass elements; conservative potential functions; later validated polyhedral/distributed-density gravity. Far-field monopole approximations need an error criterion and a smooth declared transition, not an undocumented sphere radius. In the potential blend `Phi = w*PhiNear + (1-w)*PhiFar`, acceleration must include the derivative of `w`.

## Rotation and KSP rigidbodies

For a rigidly moving surface, material velocity is `O_dot + Q*(omega cross r)` plus any independent material flow. A free craft keeps its inertial velocity when it loses contact; friction/collision with a rotating floor produces support forces. Use KSP/Unity's rigidbody contacts where verified, while providing consistent moving collider velocity and floating-origin transforms. Merely rotating a huge transform each frame does not prove reliable contacts.

In a rotating physics chart the same motion contains centrifugal, Coriolis and Euler terms. Use the equation in [physics](../spec/physics.md) exactly once. In an inertial solver, add physical acceleration only; apparent forces arise from frame conversion and moving contacts. Do not add an outward `omega²R` force to an inertially integrated craft as a shortcut. Compare rotating and inertial runs of the same experiment.

KSP's loaded physics chart, Krakensbane velocity, body frame and native cached gravity must be audited together. Keep native host acceleration or replace it with a scoped correction, with a single owner. Test all parts, EVA, wheels, debris, joints, docking and multiple craft. Anchored landed craft on rails follow material motion, then recover that velocity on unpack. Save anchors by stable region/chart/local pose, not only latitude/longitude/radial altitude.

Read-only inspection of the earlier Ringworld project's geometry/flight/settings code provides useful concrete precedents: explicit inertial/rotating velocity transforms, per-rigidbody deduplication, desired-minus-stock acceleration and full pose/angular-velocity handoff. The [inspection provenance](../research/physics-provenance.json) records exact local source hashes. Those single-ring implementations and their runtime claims are reference context; they are not Jakobian support or substitutes for general-domain tests. No source was copied in this slice.

## Influence is a volume and a policy

Separate physical field support, local high-accuracy solver domain, terrain activation envelope, environment occupancy and native orbital parent. Gravity itself need not vanish at a domain boundary. A stock SOI is an orbital approximation and body-selection rule, not the shape of a body's material or the universal limit of its gravity.

An influence domain can be a sphere, ellipsoid, toroidal tube neighborhood, CSG volume or bounded implicit domain. Store separate entry and exit thresholds (hysteresis), priority and overlap rules. Domain selection and acceleration composition are different operations: overlapping field contributions may add, while one solver/frame must own a vessel's integration at a time. Host gravity continues where required; crossing a habitat domain does not automatically change the celestial parent.

Detect crossings along the complete propagated path with conservative swept bounds and bounded root refinement. Endpoint containment can miss entry and exit through a thin annulus in one step. Preserve inertial position/velocity and UT at every frame/solver handoff. Blend conservative fields through potentials where that is the chosen physics model. A budget failure reduces the step or warp, rather than skipping the boundary.

KSP's native `Orbit` and patched-conic parent transitions cannot exactly encode these domains or noncentral fields. Keep native conics where valid; inside a numerical domain, authoritative state vectors and the predictor must use the same field/event provider. Osculating conics are a compatibility view. Map lines, maneuvers, background fleets, warp and save/reload need their own acceptance tests before the capability is enabled.

## Implementation gates

1. Complete the nonradial query/contact/anchor backend on a static airless torus; verify empty hole and inner/outer landing through scene changes.
2. Add bounded expression geometry with regularity/bounds diagnostics and reproducible compiled descriptors.
3. Add influence-domain event detection and numerical flight with a point field first; prove unchanged state at domain/frame boundaries.
4. Add rigidly rotating habitats and centrifugal/Coriolis parity; validate multiple craft, EVA, rails, warp and reload.
5. Add mass compilation and finite-kernel/distributed fields, predictor convergence and far-field transitions.
6. Add spatial gas/fluid volumes and adapters using the same geometry/motion/field contexts.

These are required development areas. The current PNG terrain feature remains a useful restricted provider and must not become a radial assumption in the general contracts.
