# Experimental torus provider API

Development 0.6.0 exposes analytic material coordinates and a contact-mesh publisher. These are building blocks for a future general-body backend. They do not make the current body parser accept `Torus`, and do not supply KSP altitude, gravity, vessel, rails or scene adapters.

## Resolve a material position

```csharp
var chart = new Jakobian.Core.TorusChart(1000, 200);
var anchor = new Jakobian.Core.TorusAnchor(u: 0.25, v: 0.5, normalOffset: 1);
if (!chart.TryResolve(anchor, out var localFrame))
    throw new InvalidOperationException("Folded or invalid offset chart");
```

The result contains local position, outward-from-solid normal and unit directions of increasing U/V. Coordinates are periodic turns; lengths are metres. The inner wall's normal points into the hole. `TryProject` finds material coordinates for nearby points, with explicit axis/medial ambiguity. Normals are not a gravity or navigation-up policy.

Use these coordinates for persistent material identity instead of tile/vertex indices. To serialize a position record, add the codec's node to an enclosing ConfigNode before saving:

```csharp
var records = new ConfigNode();
records.AddNode(Jakobian.KSP.TorusAnchorCodec.Save("my-pack/torus", chart, anchor));
records.Save(privatePath);

var loaded = ConfigNode.Load(privatePath).GetNode("JakobianTorusAnchor");
bool ok = Jakobian.KSP.TorusAnchorCodec.TryLoad(
    loaded, "my-pack/torus", chart, out var restored, out var error);
```

IDs follow the implemented stable-body identifier character rules. The codec rejects changed dimensions and owner/chart/schema mismatches. Handle a failed load as unavailable; do not apply default output coordinates. A complete saved-vessel anchor still needs GUID, orientation, contact state, epoch, world descriptor and integration with the vessel lifecycle. This codec does not modify player saves or perform those operations.

## Request contact coverage

On the Unity owner thread, create `TorusContactStream` with a unit-world-scale root transform and a `TorusContactGrid`. The frame must be explicitly owned by the caller. A normal gameplay provider must publish colliders in the same physics scene as its vessels; the isolated scene used by the development probe is only for testing.

```csharp
var grid = new Jakobian.Core.TorusContactGrid(1000, 200, 64, 32);
var stream = new Jakobian.KSP.TorusContactStream(
    surfaceRoot, grid, segments: 8, capacity: 96, maxRequired: 48);

// from/to are already in the same local metre frame as the analytic torus.
bool accepted = stream.Request(new[] {
    new Jakobian.Core.ContactSweep(from, to, actorRadiusPlusMargin)
}, evaluationBudget: 2048);
if (accepted) stream.Pump(maxBuilds: 2);
bool ready = accepted && stream.CoverageReady;
```

`ready` is an admission result for the requesting coordinator, **not a command that stops KSP physics**. Until a vessel/frame adapter exists, this snippet cannot safely admit a flying spacecraft to a new area. A production coordinator must prepare coverage before unpacking/travel, hold an appropriate safe state while coverage is incomplete, or reduce its requested step/prediction span. Never silently truncate coverage, freeze only one part or alter global simulation time as a substitute.

Up to eight participants/sweeps can be supplied. For multiple sweeps, supply a sufficient evaluation budget; a 64×32 grid needs at most 2,048 distance evaluations per sweep. The old active set stays in place during replacement, and the cache must accommodate old plus desired coverage. Inspect `LastPlanStatus` for capacity/budget/input failure. `Pump` limits build attempts per call and per Unity frame; it does not bound native cooking latency in milliseconds.

The root must remain a valid unit-scale rigid frame. Frame motion, origin shifts and material velocity require a coordinator; rotating this static-collider hierarchy is not a centrifuge implementation. Dispose the stream on its owner thread when the provider is removed, and yield frames so Unity can consume deferred mesh/object destruction.

See the [architecture and native fixture](../architecture/torus-contact-streaming.md) for exact semantics, tested scope and remaining gates. The current API is experimental and has no frozen public ABI promise.
