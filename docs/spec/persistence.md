# Persistence, identities and migration

Status: proposed schema. No Jakobian world/scenario serializer is implemented yet.

## Identity rules

Each body has a namespace-qualified stable ID, native internal body name, display name and provider identity. Every surface, chart, terrain generator, biome, reservoir, chamber and anchor has a stable ID within that body. Avoid serializing `flightGlobalsIndex`, instance IDs, loaded mesh indices, list order or localized display names as the only authoritative identifier.

Native KSP still saves native body references and part/module data. Jakobian's supplemental scenario must map stable IDs to current native bodies while preserving native names and supported indexing behavior. Test body insertion/reordering against saves; a new registry alone cannot repair all native reference assumptions. Never change stock/native planet identities casually to support a differently shaped homeworld.

## Persisted world record

Freeze the world-generating configuration used when a save first adopts a body:

- Schema version, stable body ID/name, provider and ABI/generator versions.
- Shape dimensions/topology, orientation/spin law/epoch and host/orbit ownership.
- Geometry/maps/material hashes and imported asset manifests.
- Terrain seed, generator version and immutable parameters.
- Gravity, atmosphere/fluid model versions and relevant parameters.
- Surface/chart/biome identities, anchors and discovered/scanned/research state.
- User settings distinguished from immutable world parameters.

A new mod pack can add new bodies, but cannot silently overwrite established geometry, seeds or occupied terrain. An author can opt into a versioned migration that has a defined occupied-surface mapping; otherwise preserve the old generator/configuration or report the changed body unavailable. Store enough descriptors to preserve generated worlds, but do not embed third-party copyrighted texture files into ordinary saves.

## Vessel records

Anchor state includes vessel GUID, body/surface/chart ID, material coordinate, physical tangent/normal offsets, local orientation, pose epoch and contact/situation state. Multiple support contacts may matter for a large craft bridging patches; the chosen anchor is stable and accompanied by contact evidence. Render mesh vertex IDs are not anchors.

Free numerical flight state includes position/velocity, authoritative reference frame ID, epoch, propagation model/version and event-state information. Preserve native vessel orbit as a compatibility view when required. Also record transitions under way so a save/reload cannot apply a velocity transform twice. Docking/undocking and EVA change vessel identity: migrate anchors from native events with explicit rules, not nearest-body guesses.

Store material coordinate plus enough body-local fallback information for validation, but do not choose whichever coordinate restores into a different surface after an upgrade. Verify restored solid/empty classification, contact normal and clearance before enabling physics. If assets/providers are missing, retain records and offer a recovery diagnostic; do not delete craft or relabel an interior floor as an exterior sphere.

## Migration contract

Version-aware migrations must be deterministic, idempotent, preserve unknown future/extension records and support direct upgrades skipping releases. Defaults for missing legacy fields come from the legacy schema/model, not today's preset. Back up the local save before a destructive user-authorized conversion; routine migrations should preserve prior records until successfully written.

Shape/topology edits require an explicit correspondence from old surface/chart/material coordinates to new ones plus occupied-terrain checks. If a floor disappears, hold that body unavailable until an authorized recovery/migration resolves each vessel. Provider removal is not permission to invent a new world. Saved science subjects/biome IDs and discoveries must not reset or grant duplicate rewards.

Asset hash mismatch is a diagnostic with policy: retain compatible archived assets/generator, use a validated migration, or block that body's contact/visit capability. Do not silently accept a changed mesh under the same filename. Keep mod settings/preferences independent so renderer-quality changes can be applied without changing geography.

## Fixtures and acceptance

Retain real save fixtures for every released schema, plus synthetic edge cases. Run direct and sequential upgrades, repeated load/save, Flight/Map/Tracking transitions, distant numerical fleets, landed/splashed anchors, docking/EVA identity changes, removed providers, absent assets and reordered bodies. Verify terrain/biome/resource identity, pose/velocity, science/discovery progress and settings. Hash original fixtures before/after tests; do not claim backward compatibility when historical binaries or fixtures are absent.

Ringworld's existing saves remain owned by its mod. Jakobian will not automatically take them over. A future Ringworld-to-Jakobian importer needs both projects' world versions, native anchor semantics and explicit migration fixtures; merely renaming a scenario module is not a migration.
