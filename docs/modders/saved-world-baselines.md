# Saved-world baselines and reconciliation

Development 0.8.0 / P02 adds a per-game `WorldScenario` for Jakobian's implemented radial worlds. It records deterministic descriptor baselines and reports compatibility. **This is a diagnostic and preservation feature, not a pre-physics load blocker.** KSP's inspected `Game.Load` calls `onGameStateLoad` after `FlightState.Load`; scenario callbacks are not an admission barrier. Restore matching mod files before visiting or saving a world reported as incompatible. General-body flight admission remains a later gate.

Development **0.10.0 / P04a** adds a separate [enforced load/start guard](load-admission.md) before five native entrypoints. That guard consumes these records and refuses incompatible/unsupported loads; the scenario panel itself remains diagnostic. Neither component certifies active unpack/teleport/transfer or arbitrary general-body flight.

## Establishing a baseline

An unrecorded game is `LegacyUnverified`, including a new game that has not recorded its first baseline. The Flight/Space Center/Tracking Station panel offers **Record current world baseline**. This is an explicit decision to compare future loads with today's definitions. It cannot prove that an older save used the same world. The adoption is saved on the next normal game save; the button does not independently overwrite a save file or move craft.

The panel is visible for non-compatible states and does not provide an override for incompatible, malformed or future records. Explicit development probes hide the panel and invoke first adoption only in their own uniquely named private saves. No global snapshot is shared between games. Assessment is cached against the per-game snapshot and immutable published catalog; it does not rehash files every frame.

## What is recorded

Each supported body records a stable ID, native name/index and sorted property dictionary. Inputs cover:

- Provider/generator IDs, chart/surface conventions, pinned game/template identity and effective shape parameters/reference radius.
- Terrain offset/deformity/normal convention; height, color and biome source paths and SHA-256 hashes of the same PNG bytes read for decoding.
- Explicit biome IDs, palette colors and effective science values; material colors and mapped shader bundle identity.
- Point-gravity parameter, spherical influence radius, airless/dry model, uniform spin law/period/epoch, circular orbit parameters and parent native identity/index.

Descriptor identity comes from effective parsed inputs rather than raw CFG ordering. Numbers use invariant roundtrip formatting and normalize signed zero. Property keys and body IDs sort ordinally. Fingerprints use SHA-256 over the documented v1 binary encoding: BinaryWriter length-prefixed strict UTF-8 strings and little-endian Int32 fields. Body encoding contains `Jakobian.WorldBody/v1`, ID, native name/index, property count and sorted key/value pairs. Catalog encoding contains `Jakobian.WorldCatalog/v1`, body count and sorted body fingerprints. Digests detect accidental edits/omissions; they are not signatures or an authenticity/security boundary.

The schema freezes Jakobian's explicit compiled inputs. It does not snapshot arbitrary stock/third-party world geometry, inherited stock asset bytes, later runtime mutations by other mods, vessels, science progress, renderer settings or whole game binaries. Template/game/provider versions must be changed when relevant implicit generator behavior changes. Display labels/descriptions are outside the descriptor; explicit material/map changes are conservatively diagnosed even when purely visual.

## Compatibility policy

| Status | Meaning and save behavior |
| --- | --- |
| `Compatible` | Every recorded body matches its stable/native identity and properties. Preserve its records. |
| `Additive` | All recorded bodies match; extra current bodies have unique identities/bindings. Append only the new descriptors on the next save. |
| `LegacyUnverified` | No baseline exists. Saving alone never adopts it. Explicit first adoption is available. |
| `Incompatible` | A recorded body is absent, renamed/reindexed or has changed properties. Retain the saved baseline; no adoption or automatic migration. |
| `UnsupportedSchema` | World schema differs from v1. Preserve it; do not downgrade or adopt. |
| `UnsupportedRecord` | Unknown scenario/world/body/property fields may carry future semantics. Preserve them and withhold compatibility/adoption. |
| `InvalidRecord` | Duplicate identity/fields, malformed tokens/encoding, invalid bounds or mismatched body/catalog digest. Preserve the raw data and withhold compatibility/adoption. |
| `Unavailable` | A usable published catalog is unavailable. Preserve saved records and do not infer compatibility. |

Removing and re-adding a different ID at the same native slot is incompatible. Additional bodies cannot erase missing old bodies. Removing all definitions yields an empty current catalog and missing-body diagnostics for an established baseline. Merely re-saving a failed reconciliation cannot replace the old descriptors with new ones. Explicit first adoption applies only to genuinely unrecorded, recognized scenario data.

## Native storage and extension data

KSP writes `SCENARIO { name = WorldScenario; scene = ...; World { ... } }`. The world contains schema/provenance/time/catalog digest and `Body` records. Each body stores native identity, body fingerprint and repeated `Property { key = ...; valueBase64 = ... }` records. Values are strict UTF-8 encoded as Base64 to preserve Unicode, empty strings and CFG syntax characters without interpreting them as nodes/comments.

At scenario scope only KSP `name`/`scene`, `World` and `Extension` are recognized. Unknown data outside these envelopes is preserved but reported unsupported, even if no `World` exists. Repeated `Extension` nodes at scenario/world/body scope are defined as opaque nonsemantic metadata; they are retained but do not change world compatibility. Future behavior that affects geometry/dynamics must use a new property/provider/schema, not hide semantics in an ignored extension.

Records are defensively copied. Semantic descriptors allow at most 1,024 bodies, 1,024 unique properties per body, 4,096 UTF-16 characters per value and 64 KiB aggregate UTF-8 property text per body. This accommodates the loader's supported 256-biome palette. The codec checks duplicate fields, canonical tokens, Base64/UTF-8, bounds and both digest levels. Native names/scenes remain owned by KSP's scenario serializer. Comments/whitespace formatting are not a byte-preservation contract; unknown node/value content is preserved semantically. Opaque/native ConfigNode input is already in memory and is not covered by a streaming parser or adversarial input memory guarantee.

## Experimental API and checks

`WorldBodyDescriptor` and `WorldCatalog` are immutable core records. `WorldReconciliation.Compare(saved, current)` returns structured field differences. `WorldSnapshot.Read(scenario)` owns a copy, `Reconcile(current)` reports policy, `WriteScenario(current)` returns a copy and records only safe additions, and `TryAdoptCurrent` is restricted to first adoption. Callers must have explicit adoption intent; invoking the method is not historical compatibility proof.

Run `tools/Boot-Probe.ps1 -Instance baseline -Graphics` for native codec/mutation/disk checks. Run `tools/Flight-Probe.ps1 -Body JakobianAtlas -WorldIsolation` for two independent private games, each with actual landing, adoption, native save/reload and restored descriptor checks. `WORLD_CODEC_PASS`, two `WORLD_GAME_ISOLATION_PASS` and two `WORLD_SCENARIO_ROUNDTRIP_PASS` markers plus `FLIGHT_SUITE_PASS` are required. Process exit alone does not establish a pass. See [development evidence](../development/v0.8.0.md).
