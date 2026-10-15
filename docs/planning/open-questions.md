# Open questions and experiments

These are engineering tasks that can proceed without asking the user to make routine implementation choices. Resolve them with source inspection and the smallest useful runtime fixture.

| Question | Next experiment | Completion evidence |
| --- | --- | --- |
| Does standalone publication consume finalized patched configs in supported profiles? | `PSystemSpawn` publication/binding is proven for the unpatched 0.2.0 baseline; next test ModuleManager ordering/cache fixtures | final MM input, one publication and descriptor binding in final bodies |
| How does Jakobian construct general geometry without unsafe spherical consumers? | own airless torus construction, general scaled mesh and scoped collision path | hole transit, real collision on both sides, no null-PQS exceptions |
| Which native kill-radius/altitude paths must be bypassed for empty holes/interiors? | trace loaded/rails vessel at torus centre and several radial distances | no destruction or incorrect unpack behavior from reference radius |
| Can we compose with MFI and KSPCF across their active overrides? | patch-owner report plus desired/existing acceleration trace | correct once-only acceleration and unchanged stock spherical case |
| Which surface caches are read after the final writer? | stock engines/intakes/wheels/EVA execution-order fixture | local values at consumer boundary, no last-writer regressions |
| How should a huge host-centred habitat be represented natively? | compare independent body and parent-host region prototypes | clean map/science/CommNet/warp behavior with documented limitations |
| Which numerical propagator gives bounded event/error work? | high-accuracy offline oracle then native packed-flight harness | convergence, conservative-field invariants, swept earliest event |
| What moving/static collider arrangement is stable in a rotating local chart? | zero-spin then spinning pod/wheel/EVA surface fixture | no induced velocity/contact energy over long holds and frame shifts |
| How does camera placement/collision track nonspherical ground? | 0.3.0 implements a scoped radial endpoint guard; next test swept view segments, steep near-plane cases and interactive mode/scene transitions | camera remains outside terrain; actual terrain visible across orbit/flight/scene transitions |
| How should zero-spin landed states avoid degenerate stock conics? | isolated static-body anchor/rails fixture after native BodyFrame initialization | finite stored state, stable packed/unpacked reload and no NaN orbital state |
| How do multiple vessels/EVA/debris share one loaded frame? | local fleet and decoupling/docking fixtures | no participant falls through or receives incompatible transforms |
| How should chart seams and zero-gravity navigation be handled? | closed-loop traversal and transported-frame oracle | stable heading/up with explicit seam rules |
| Which shader/data layouts can common visual mods adapt? | version-pinned Parallax/EVE/Scatterer source inventory | adapter requirements and minimal scene comparison, no unsupported claim |
| How can an optional Kopernicus coexistence adapter prevent competing system injection/runtime ownership? | exact-version ownership trace and config/provider interoperability prototype | Jakobian still loads independently; one system publication and no duplicate services |
| What target/runtime APIs do reused source and optional provider adapters require? | compile/import minimal reviewed components and test actual Mono APIs | own-loader startup and optional adapter tests, without relying on target name alone |
| What are practical world dimension/feature/time limits? | sweep huge radius/UT/phase with contact-scale offsets | declared validated ranges and precision rejection diagnostics |
| What should an author mesh format contain? | simple OBJ/import-sidecar bake prototype before richer interchange | closed-solid validation, BVH, region labels, atlas and hash manifest |
| Which physical gravity models are worth implementing beyond mascons? | retrieve/read original polyhedral paper and compare reference fields | verified boundary behavior and performance/error study |

Product decisions that remain open: owner-selected project license, eventual public API stability policy, minimum supported hardware/OS and distribution channels. They do not block local research or the first airless-body slice. Broad mod compatibility and arbitrary homeworld are long-term gates, not properties established by bootstrap.
