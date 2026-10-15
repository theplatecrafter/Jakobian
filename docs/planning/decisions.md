# Design decisions

All decisions are revisable when a native experiment disproves their assumptions.

| ID | Decision | Reason / consequence |
| --- | --- | --- |
| D01 | Jakobian is standalone from the first loader milestone | owner requirement; its own parser/system construction runs with Kopernicus absent |
| D02 | Kopernicus is a reference and optional interoperability target | source reuse can accelerate implementation; coexistence requires single-loader ownership, not an upstream dependency |
| D03 | Geometry, gravity, air and water are separate providers | a surface normal cannot determine physical gravity or chamber pressure |
| D04 | Use solids plus named oriented boundary regions | distinguishes interior floor, hull, wall and ceiling and preserves holes/cavities |
| D05 | Primitives and chart maps first; mesh/implicit backends later | keeps beginner authoring simple and isolates topology/collision risks |
| D06 | Configure native spherical PQS through Jakobian's own loader | preserves valid native machinery without forcing nonspheres through radial queries; feature parity needs implementation and fixtures |
| D07 | General provider query results carry identity/availability/error | nearest surface can be ambiguous; unresolved data must not become fabricated ground |
| D08 | Canonical doubles and local chart rebasing | contact precision must survive astronomical dimensions and huge UT |
| D09 | Numerical trajectories only where required | preserve conics where valid; keep noncentral dynamics truthful and bounded |
| D10 | Landed state comes from support contact and persists as material anchor | distance alone does not establish landing; native sphere pose is insufficient |
| D11 | Freeze world generation and version migrations | packs/updates must not silently move occupied terrain or change biome/research identities |
| D12 | Patch by exact boundary and explicit ownership | MFI/KSPCF and other physics mods already own overlapping calculations |
| D13 | Independent physics, visual, map and prediction acceptance | a convincing rendered body does not establish a usable flight framework |
| D14 | Initial KSP scaffold targets net472/C#7.3; tools target net10 | conservative Mono-compatible bootstrap already compiled; imported source/provider APIs require runtime-target validation |
| D15 | Match Unity 2019.4.18f1 for later assets | local editor and supplied game match; shader/asset projects are deferred until a renderer needs them |
| D16 | No release/license/public API promise at research stage | avoid implying the scaffold is a distributable replacement; owner license and provenance remain explicit decisions |

## Alternatives considered

**Extreme PQS displacement:** can approximate some star-shaped surfaces and reuses terrain mods, but radial height cannot represent general multi-sheet topology. Retain as a supported restricted geometry family, not the universal backend.

**One imported planet-sized Unity mesh:** useful as a small-body/author input, but full-resolution contact geometry at astronomical scale is not bounded or precise. Bake acceleration structures and stream local clusters instead.

**A globally patched `CelestialBody.Radius`:** cannot express local ground/pressure/biome identity, breaks unrelated consumers and cannot resolve a hole. Keep a stable documented reference value and targeted context APIs.

**A global custom flight integrator:** expands compatibility conflicts and duplicates existing work. Start with scoped ownership-aware boundaries and only expand when exact runtime evidence requires it.

**Artificial gravity derived from nearest normal everywhere:** convenient for fictional worlds but generally nonphysical, may jump on medial axes and changes orbital behavior. Offer an explicit bounded vector-field provider when requested; physical mass and rotating frames are distinct models.

**Automatic migration of every Kopernicus config to nonspherical topology:** many modifiers assume radial coordinates and have no meaningful universal translation. Keep spherical configs untouched and provide explicit diagnosed conversion for supported concepts.
