# Torus contact streaming and material coordinates

Development 0.6.0 adds a bounded contact publisher and periodic material chart alongside the existing radial backend. This is an experimental provider API and isolated native physics fixture. It does not register a torus celestial body, change player craft or enable a `Shape { type = Torus }` configuration.

## Material coordinates

`TorusChart` resolves `TorusAnchor(u,v,normalOffset)` in local metres. Both coordinates wrap in [0,1); `u` goes around the major circle in X/Z and `v` around the tube, with +Y at v=0.25. The chart/surface identities are `torus-uv/v1` and `exterior`. Positions and tangent frames depend on the analytic surface and parameters, never a resident patch ID or render vertex.

The normal points out of solid material. On the inner wall it points into the hole. `VDirection cross UDirection = Normal`, matching the baker's outward triangle winding. Offset is measured along that normal. The offset tube radius must remain strictly between zero and the major radius; otherwise offset charts fold or reach ambiguous medial sets. Projection fails explicitly on the symmetry axis and tube centreline. This is nearest material projection, not a down-ray or a gravity model.

`TorusAnchorCodec` writes versioned ConfigNode material records containing body/surface/chart identities, analytic dimensions and coordinates. It rejects the wrong owner, changed dimensions, unknown schema/fields and folded offsets. Exact dimensions use invariant roundtrip formatting. This is a local position record; vessel GUID, orientation, epoch, contact evidence, body motion, world-descriptor reconciliation and scenario integration are still required before it can restore an actual KSP vessel. No player-save migration is installed.

## Conservative contact requests

`TorusContactGrid` divides the periodic surface into fixed parameter tiles. A request contains up to eight `ContactSweep` values: a local straight segment with a swept-ball radius covering the actor's extent and declared margin. It can represent multiple actors or a linear prediction corridor. A curved/numerically propagated trajectory needs a conservative segmented enclosure from its owner; a straight chord is not automatically safe for curved motion.

Every patch is enclosed by a sphere around its analytic parameter midpoint. With U/V tile counts `Nu`, `Nv`, its radius is bounded by:

`B = 2*(R+r)*sin(pi/(2*Nu)) + 2*r*sin(pi/(2*Nv))`.

The two circular-arc chord bounds and the triangle inequality enclose the complete analytic patch, including interior points. The enclosing sphere is convex, so it also contains the baked triangles. Segment-to-sphere broad phase is conservative: false positives are allowed, omitted intersecting triangles are not. It does not approximate the hole as a spherical ground surface.

Planning returns explicit `Complete`, `InvalidInput`, `BudgetExceeded` or `CapacityExceeded` status. An incomplete result contains no accepted patch set. Work counts segment/patch distance evaluations and stops at the supplied budget. The grid and maximum participant count bound input work and memory. A huge corridor can exceed the tile budget; splitting motion or reducing the step is a caller responsibility.

## Residency and publication

`TorusContactResidency` maintains an active set, a desired set, a bounded LRU resident cache and build tickets. The old active set remains protected while replacement patches are built. The desired set activates only when **every** requested patch is resident. The union of old active and new desired coverage must fit the cache; a rejected request leaves old geometry intact but reports the new coverage as not ready.

Each ticket carries cache-owner identity, target revision and a unique reservation serial. Cross-world tickets, stale work and duplicate publication are rejected. An aborted build releases its reservation for retry. Repeating an unchanged target does not invalidate useful work. Eviction can remove only cached tiles outside both protected sets. An empty successful request removes active colliders while retaining bounded warm cache entries.

The mutable residency coordinator is owned by one thread. Workers may build data for a ticket, but state transitions/publication must return to the owner; this class is not a concurrent dictionary or an asynchronous task scheduler.

`TorusContactStream` creates inactive native meshes/colliders in a caller-supplied unit-scale rigid frame, commits valid tickets, disables evicted objects and synchronizes active coverage. It bounds native cooking attempts per call and to 32 per Unity frame per stream. Meshes are anchored locally as in the baker. Unity consumes deferred destruction at frame boundaries; callers must yield rather than run an unbounded loop of updates in one frame. A mesh-count quota is not a millisecond guarantee for synchronous native cooking.

The current provider uses one tessellation level, no height modifiers, LOD transitions, renderer, biome atlas or asset cache. Root motion, floating-origin ownership and physical material velocity remain integration responsibilities. Moving/rotating static colliders is not certified centrifuge physics.

## Native probe

Run `tools/Torus-Probe.ps1` with baseline closed. It starts KSP with an explicit development argument and creates a separate Unity `PhysicsScene` using `LocalPhysicsMode.Physics3D`. It never turns off default-scene simulation or changes global fixed-step settings. The standard startup checks run first.

The probe requests 128 changing footprints with two participants, tests streamed rays and normals, and checks resident/build limits and eviction. A straight swept corridor crossing the periodic U seam must supply 33 uninterrupted surface ray contacts. It then drops real native rigidbodies on outer, inner and upper walls using a deliberately prescribed force toward each test surface. That force is a contact-test fixture, not torus gravity. A separate body travels 500 m through the empty axial hole while remote terrain remains active. An oversized request must preserve old contact while withholding readiness for the new corridor. Three analytic material records are written/read only under baseline's `.dev` directory.

The final success marker is `TORUS_STREAM_SUITE_PASS`; process exit alone is not success. The watcher records `torusStreamingMarker` and a deployment snapshot accompanies the log. See the [0.6.0 record](../development/v0.6.0.md) for executed results and limits. Native rigidbody contact in an isolated scene does not certify KSP vessel landing, EVA, rails, warp or persistence.

## Next integration gates

Construct a general-body descriptor and scaled view; bind a contact frame to the active KSP physics chart; adapt native spherical kill/altitude/terrain consumers; reconcile saved descriptors and install real material anchors; then test loaded/unloaded vessels through hole transit, inner/outer contact, scene changes, EVA and warp. Gravity, apparent vertical and surface orientation remain separate providers. A rotating habitat also needs a validated inertial/rotating force-owner handshake and matching surface velocities.
