# Proposed configuration language and Kopernicus migration

Status: **development 0.5.0 has a restricted implemented standalone subset**. Mapped radial terrain and native biomes are described in [radial maps](radial-maps.md). The general language below remains a design proposal. Files under `examples/` use `.cfg.disabled` to keep future features out of the game database. The enabled pack is [Bodies.cfg](../../GameData/JakobianTestBodies/Bodies.cfg).

## Implemented subset through 0.5.0

The loader reads `Jakobian` roots with `schemaVersion = 1` and repeated `Body` nodes. Unknown/duplicate fields and unsupported child nodes are rejected. Current keys are:

| Node | Supported values |
| --- | --- |
| `Body` | `name`, `id`, `flightGlobalsIndex`, `red`, `green`, `blue` |
| `Template` | `name = Minmus` only |
| `Properties` | `radius`, `gravParameter`, `sphereOfInfluence`, `description`, `rotates`, `rotationPeriod` |
| `Orbit` | stock `referenceBody`, `semiMajorAxis`, `inclination`, `meanAnomalyAtEpoch`, `eccentricity = 0` |
| `Shape` | `type`, `axisX`, `axisY`, `axisZ`, `exponent`, `lobes` |
| `Terrain` | `heightMap`, `colorMap`, `offset`, `deformity`, `biomeMap`, repeated `Biomes/Biome` definitions; see [map contract](radial-maps.md) |

`name`, stable `id` and an unused integer `flightGlobalsIndex >= 17` are required. Keep them stable in saves. The supplied fixtures reserve 17–21. Dimensions are metres; `gravParameter` is m³/s²; orbit inclination is degrees, mean anomaly radians, rotation period seconds. Colors are scalar channels in [0, 1]. Parents must be existing stock bodies. There is no current support for replacing a stock body, making a homeworld, elliptical orbits, atmosphere, ocean, arbitrary expression geometry or general-solid flight. Kopernicus installed alongside this build produces an ownership error rather than two competing publications.

Omitting `Shape` selects a sphere. Supported case-sensitive types are `Sphere`, `Ellipsoid`, `Superellipsoid` and `Peanut`. Axes default to `Properties.radius` (default 5000 m); a sphere uses `axisX`. Ellipsoid axes are semiaxes. Superellipsoid uses the Lp surface with `exponent` in [2, 16]; exponent 8 gives rounded cube faces. Peanut uses `axisX * (1 + lobes * (2 * direction.x² - 1))`, with `lobes` in [0, 0.65]; its Y/Z axes are unused. Axes must be between 100 and 100,000,000 m, although extreme scales have not been validated in native flight. The native reference radius is currently computed as the shape's minimum radial bound; authored `Properties.radius` supplies axis defaults rather than an independent datum in this subset.

Gravity is native central point gravity. All current bodies are airless and dry. `rotates` defaults true and `rotationPeriod` defaults 21600 seconds; positive spin periods must be at least 60 seconds. The human fixtures use six-hour rotation. Static frames exist experimentally, but zero-spin landed save persistence is not certified. Shape geometry and rotation must remain unchanged while reusing test saves; descriptor migration is future work.

Shapes are sampled in native PQS local axes, with exactly one positive radius per direction. This cannot represent a torus hole or a cave. All stock template height/color noise is replaced by the analytic radius plus optional mapped displacement and albedo; native material/UV, terrain lifecycle and collider modifiers are retained. Source validation and the latest evidence are described in [development 0.2.0](../development/v0.2.0.md).

## Familiar structure

The standalone root is `Jakobian { schemaVersion = 1; Body { ... } }` in ordinary ConfigNode syntax. Each `Body` has a native `name` and stable `id`; familiar `Template`, `Properties`, `Orbit`, `ScaledVersion`, `Atmosphere`, `Ocean`, `Rings`, `PQS`, science and biome nodes retain supported meanings. New `Shape`, `Rotation`, `Surfaces`, `Terrain`, `Gravity`, `Environment`, `Navigation` and `Compatibility` nodes are direct children of `Body`. Jakobian parses and constructs the system itself.

For a spherical pack: migrate/import supported configs into Jakobian's own spherical pipeline, with no Kopernicus installation required. Conversion must inspect root selectors, ModuleManager passes/dependency tests and external providers; unchanged legacy text is not automatically compatible. For a nonspherical body: preserve orbit/basic description/science settings, explicitly choose shape and surface regions, replace unsupported PQS geometry with chart/volume terrain, define local environment/gravity and declare compatibility policy. Changing shape in a released save needs a migration; this guide is for new authored worlds.

Use ModuleManager's real `:NEEDS`, `:AFTER` and body selectors. After a base Jakobian root exists, a pack can patch `@Jakobian:AFTER[Jakobian]:NEEDS[Jakobian]`. A pack must not use `:FOR[Kopernicus]` or `:FOR[Jakobian]` just to request dependency ordering; `FOR` names a provider. Do not duplicate the same body under Jakobian and legacy Kopernicus input roots. Optional coexistence must coordinate a single system loader; Jakobian keeps loading ownership.

The full loader must parse **final ModuleManager-patched nodes**, regardless of how a pack constructed them. The current parser runs at `PSystemSpawn`; its supplied baseline has no ModuleManager dependency. Ordering with patched databases remains a separate acceptance task. Namespace ownership, unknown fields and cross-provider adapters need predictable diagnostics. A `:NEEDS[Kopernicus]` node suppressed before parsing needs explicit migration; Jakobian must not spoof an installed Kopernicus binary to recover it.

## New nodes

| Node | Core fields / purpose |
| --- | --- |
| `Jakobian` | top-level system configuration and `schemaVersion`; contains `Body` definitions |
| `Body` | native `name`, stable `id`, familiar metadata nodes and direct shape/runtime nodes |
| `Shape` | `type`, named dimensional parameters, orientation, bounded extent; one selected provider |
| `Rotation` | body/material spin axis, period or law and epoch; distinct from a physical gravity field |
| `Surfaces/Surface` | stable `id`, provider region selector, material/chart mapping, landing/collision/navigation roles |
| `Charts/Chart` | stable projection/domain/adjacency definitions; can be supplied by a built-in shape |
| `Terrain/Mods` | deterministic ordered geometry/material modifiers, version, seed |
| `Maps/Map` | asset path, semantic type, channel, units, projection/chart/surface, seam/wrap rules |
| `Gravity` | model, `mu`/density/potential/field parameters, domain, approximation/transition policy |
| `Environment/Atmosphere/Volume` | bounded gas region, explicit datum/curves, composition and gas velocity |
| `Environment/Fluids/Fluid` | reservoir/free surface/solid clipping, density, flow and visual provider |
| `Navigation` | gravity/reference vertical, chart directions, altitude-display policy |
| `Compatibility` | native radius/ownership mode, required adapters, optional providers |

A built-in `RingHabitat` exports `floor`, `outerHull`, `rimNorth`, `rimSouth` and cap/junction regions as specified by its thickness/wall dimensions. A solid `Torus` exports `exterior` with inner/outer named subsets available by tube angle. Region selectors identify boundary pieces; they do not change solid occupancy. Imported mesh selectors come from validated face labels/sidecars.

If two declarations use the same stable ID, reject or apply explicit ModuleManager edit semantics before compilation. Surface-role selection cannot suppress collisions on a solid wall merely because that wall is not a preferred floor. Separate `collision = true` from `role = HabitatFloor` and landing-policy fields.

## Units and conventions

Dimensions/height/position are metres; UT/period seconds; mass kilograms; `mu` m³/s²; internal pressure Pa, authored pressure curves explicitly kPa or Pa; temperature K; density kg/m³; authored orientation angles explicitly degrees. Function-provider math uses declared radians. UV maps use normalized coordinates; region/material coordinates retain their chart units and domain.

For nonspherical bodies `Properties.radius` is a stable native reference radius. It does not define the solid shape, collision hull or local surface altitude. `boundingRadius` is computed/validated from the shape, and visit/envelope bounds are separately derived. Choose exactly one authoritative mass/`mu` input; convenience `geeASL` is valid only with a defined calibration.

Paths are GameData-relative and case-consistent; no absolute texture paths. Imported assets declare source hash/format/units/axis conversion. Reusable presets/macros must compile into explicit descriptors recorded in the save signature, so changing a preset cannot silently move an existing world.

## Migration mapping

| Kopernicus concept | Jakobian standalone spherical path | General shape |
| --- | --- | --- |
| `Body`, `Template`, native name | own template/parser implementation, preserving supported semantics | retained metadata; suppress incompatible spherical surface during own construction |
| `Properties`, science values | own configuration of native values | retained common values; local context adapter for surfaces/situations |
| `Orbit` | own hierarchy/configuration of native conics | body/host orbit remains familiar; local noncentral vessel dynamics are separate |
| `PQS/Mods` | own supported modifier loaders and native PQS setup | ported modifiers only; new `Terrain` chart/volume backend |
| `biomeMap`, `Biomes` | own palette/map/biome implementation | explicit chart/surface biome mappings and stable IDs |
| `Atmosphere` | own curve/normalization/native environment setup | explicit gas volumes and datum; imported curve adapter where supported |
| `Ocean` | own spherical ocean setup | convenience conversion into a declared region reservoir, only if unambiguous |
| `ScaledVersion` | own mesh/material/cache pipeline | general mesh/material backend; bypass stock sphere generation safely |
| `Rings` | own decorative ring implementation | retain decoration independently of a landable RingHabitat body |
| PQS cities/scatters/SpaceCenter | own supported loaders and native anchors | local anchors/region scatters; arbitrary KSC/homeworld deferred |

## Author diagnostics

Linter output includes config source path/node path, stable body/region ID, violated rule and a precise fix. Reject unknown required providers, nonfinite dimensions, invalid topology/seams, unsupported modifiers, overlapping volumes without policy, missing assets and incompatible saved-world hashes. Expose a compiled summary with surface labels, units, coordinate conventions, compatibility level and runtime capabilities.

The intended beginner experience is: copy a minimal sphere/torus/ring example -> set dimensions -> choose regions -> add familiar maps/noise -> select gravity/air/fluid presets -> inspect solid/empty and normal preview -> run a local land/save/reload validation. Advanced math and mesh import extend this workflow without becoming mandatory.
