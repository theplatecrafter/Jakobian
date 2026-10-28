# Load/start admission — development 0.10.0 / P04a

Jakobian enforces supported load/start checks before guarded native work. This closes the earlier P02 diagnostic-only boundary for the entrypoints below. It does **not** admit a general body to live physics, intercept active-vessel unpack/teleport/transfers, or register the detached torus. Those remain P04b. A valid load decision is not general physics/terrain certification.

## Policy

| Input | Decision |
| --- | --- |
| Compatible or additive supported radial baseline, valid native references | Allow; preserve recorded identity. |
| Unrecorded legacy radial/native state, valid references | Allow **LegacyUnverified**; no automatic adoption and no historical geometry proof. |
| Changed/missing recorded geometry, assets, provider or native bindings | Refuse. Restore matching files; no migration/override is invented. |
| Future/unknown/malformed world data or duplicate owned modules | Refuse and retain original data. |
| Detached preview or unsupported owned provider/capability | Refuse, including an empty-vessel save carrying that world record. |
| Invalid/unknown native vessel body reference | Refuse before the guarded native load. |
| Required current catalog or hook ownership unavailable | Refuse; startup cannot continue if required hooks cannot be established. |

Legacy permission preserves earlier saves and stock/native behavior, but cannot determine whether unrecorded historical geometry changed. Other mods' unowned native bodies are not independently physics-certified. The per-game baseline panel still reports diagnostics; it does not stop active flight. Explicit first adoption records today's inputs only.

## Guarded boundaries

| Native target | Guard boundary |
| --- | --- |
| `GamePersistence.LoadGameCfg(ConfigNode,string,bool,bool)` | Before native persistent-ID clearing and Game construction. |
| `Game.Start()` | Before guarded native start/scene work. |
| `Game.Load()` | Before native CurrentGame/ScenarioRunner/FlightState loading. |
| `FlightDriver.StartAndFocusVessel(Game,int)` | Before caching game/focus state or requesting Flight. |
| `FlightDriver.StartAndFocusVessel(string,int)` | Read-only inspection of the SFS before native flight-start field/scene work. |

Input nodes are never adopted or rewritten by admission. Serialized, compiled proto and live scenario sources are independently checked; a compatible live copy cannot hide a future/incompatible stored copy. Proto vessel references are checked again rather than trusting an earlier decision. A recorded baseline reset to legacy or removed from compiled state is refused. Newly adopted live records can coexist with genuinely legacy stored state without relabeling legacy history.

Bounds are 100,000 vessel references, 4,096 unique native indices and 10,000 scenarios, plus existing descriptor limits. Invalid/exhausted inputs never become permission. `GameAdmission.Inspect` is read-only; default/unknown results deny.

A refused LoadGameCfg returns null as native failed-load paths already can. Void methods skip their guarded body. A close-only diagnostic explains refusal; there is no continue-anyway action. Save-browser UX depends partly on the native caller. This is not a guard against arbitrary file upgrades/writes performed before these methods.

## Dependency and ownership

P04a uses community [HarmonyKSP](https://github.com/KSPModdingLibs/HarmonyKSP), pinned to **2.2.1.0** by [runtime-dependency-lock.json](../development/runtime-dependency-lock.json). Build materializes its verified package in ignored `.research/runtime`; deployment installs `000_Harmony` files and both upstream MIT notices. No upstream DLL is committed or renamed into Jakobian. Kopernicus is not required.

`tools/Prepare-Runtime.py` verifies archive, extracted files and notice hashes. Deployment preflights before copying Jakobian DLLs, rejects different existing bytes, duplicate installations and linked/junction paths, and never replaces a different user library. Reconcile a conflict deliberately; the installer does not select another version automatically.

Runtime verifies loaded Harmony version/bytes and single installation, the supplied KSP 1.12.5/03190 Assembly-CSharp SHA-256, exact signatures and each expected prefix. Other assembly builds require validation. Five prefixes install once under `Jakobian.LoadAdmission.v1` at highest normal Harmony priority. Partial installation rolls back only this owner's patches and refuses startup. Missing/replaced hooks and foreign prefixes/postfixes/transpilers/finalizers are detected; foreign patches are preserved.

The supported profile has **no competing patches on these targets**. Returning false skips the original but cannot prevent arbitrary foreign postfix/finalizer code or hostile same-frame unpatch-and-call. An unsupported late patch triggers refusal/integrity shutdown when detected; this is cooperative ownership enforcement, not a security barrier against arbitrary mods. Broad profiles need explicit adapters/ordering evidence. Removing Jakobian or its dependency removes its protection.

## Verification

`tools/Admission-Probe.ps1` invokes actual patched methods with private fixtures. Temporary observers count constructor, persistent-ID clear, FlightState.Load and scene calls. Denials require all counters zero and unchanged live game/vessel/scene/save-folder references. A valid load must hit constructor and ID-clear observers. Mutations cover descriptors, schemas, indices, missing/preview bodies, raw/proto/live sources, foreign ownership and removed hooks. Observer patches/data are separate from player saves.

Also run Atlas and historical/stock regressions. Require `ADMISSION_ORDER_PASS`, `ADMISSION_SUITE_PASS` and the watcher's admissionMarker; exit alone is not success. See [0.10.0](../development/v0.10.0.md) for executed evidence and grade.
