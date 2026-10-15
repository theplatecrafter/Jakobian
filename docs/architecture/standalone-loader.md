# Standalone loader specification

Requirement confirmed by the owner on **14 October 2026**: Jakobian is a standalone full mod framework. The earlier extension-first proposal is superseded. Development 0.2.0 implements an initial standalone loader and radial terrain subset without Kopernicus assembly references. The complete lifecycle/parity contract below remains broader than that initial implementation.

## Ownership and dependencies

Jakobian owns parsing, stock template cloning, system construction, native publication, post-spawn binding, rendering/provider lifecycle and saved-world reconciliation. Its baseline installation must work with Kopernicus absent, including new spherical and nonspherical bodies. ModuleManager and selected shared libraries may be explicit dependencies; they do not make Kopernicus a required system constructor.

Reuse native KSP PQS, conics and environment features where their semantics fit spherical bodies. Implement their configuration/build pipeline inside Jakobian. General surfaces use the same body descriptor model with shape-specific backends. Avoid separate incompatible body identities or save models for spheres and nonspheres.

## Loading phases

1. **Discover capabilities and loader ownership.** Identify provider binaries/versions and competing injectors before publishing a system. Required providers must be available; optional adapters must not prevent startup when absent.
2. **Wait for finalized configuration.** Consume the final ModuleManager database, not raw files or partly patched nodes. Establish the actual KSP loading boundary through a controlled lifecycle trace; do not assume callback ordering from another loader.
3. **Normalize and parse.** Read the Jakobian root, familiar body nodes and explicit shape/runtime nodes. Normalize supported legacy inputs through Jakobian's own parser. Preserve source/node context for diagnostics and require explicit conflict/duplicate policy.
4. **Compile and validate.** Build the body hierarchy and immutable geometry/gravity/environment descriptors. Validate identities, references, templates, assets, bounds, topology, units, provider capabilities and saved-world versions before any partial system becomes visible.
5. **Construct the native prefab.** Clone the supplied stock system/templates as needed, create/modify bodies, wire hierarchy and native metadata, select spherical/general terrain and scaled representations, and attach stable descriptor identity. Avoid mutating the stock template irreversibly before validation succeeds.
6. **Publish once.** Assign the validated system to `PSystemManager.Instance.systemPrefab` at the proven pre-instantiation boundary. One loader owns this assignment; a second injector must not overwrite the result later.
7. **Bind and fix up after native instantiation.** Map stable IDs to final native bodies, reconcile indices/names, finish orbit/scaled/provider setup and initialize runtime query services. Exactly-once behavior matters across scene changes.
8. **Reconcile a loaded save.** Preserve established descriptor/generator/anchor identity and verify native references before visits or physics restoration. Loading a new config is not permission to move existing worlds.

Kopernicus's injector/parser/fixup source is a useful reference for these responsibilities. Jakobian's lifecycle and event API remain its own. The first experiment must prove these phases in the supplied KSP 1.12.5 installation with Kopernicus removed.

## Familiar authoring and parity

The primary language is `Jakobian { schemaVersion = 1; Body { name; id; Template; Properties; Orbit; Shape; ... } }`, expressed as ordinary ConfigNodes rather than literal semicolon syntax. Missing `Shape` selects the supported spherical/default template path. General geometry nodes are direct children of `Body`, not a nested extension node consumed by Kopernicus.

Keep familiar node names and compatible meanings where possible. Full spherical behavior must be earned through a parser-target inventory, independent construction code and fixtures. Implement the first small spherical subset in M1, then expand parity throughout development; do not postpone ownership of spherical bodies until a late replacement project.

Legacy config support and binary compatibility are distinct. A pack containing `:NEEDS[Kopernicus]` can be filtered out by ModuleManager when Kopernicus is absent; a parser cannot recover nodes already removed. Migration tooling must explicitly handle root names, passes, dependency tests and referenced providers. Do not impersonate Kopernicus globally or advertise transparent support for all existing packs.

## Kopernicus installed alongside Jakobian

Both mods affect one native planetary system, so coexistence requires an explicit, version-tested coordinator. It must resolve input/body ownership and prevent Kopernicus's system injector from independently publishing a conflicting prefab. It must also account for overlapping runtime utilities, textures, solar panels, caches and external parser/PQS providers. Suppressing one prefab assignment alone is insufficient.

The coordinator is optional compatibility code; it never transfers Jakobian's required loading responsibilities to Kopernicus. Any interception/ownership negotiation needs exact signatures, supported versions, diagnostics and tests. If it cannot establish safe single ownership, fail before publishing a mixed system and preserve saves. Supporting both installations remains a compatibility goal, not a reason to make Jakobian an extension.

## Source reuse

The existing pinned source archives are sufficient reference checkouts; a clone is optional. Source-derived implementations can be adapted into Jakobian where useful. Record the upstream file/commit, nature of changes and notices for every copied or derived component. Review parser, rendering/shader and bundled dependency provenance separately. No upstream code has been copied into the current scaffold.

Do not retain an upstream assembly's name or spoof its API merely to satisfy dependency checks. Where external binaries require a specific API, provide an explicitly tested optional interoperability layer. Source reuse does not create a runtime requirement to install the upstream mod.

## M1 acceptance

With Kopernicus absent: finalized config is parsed once; an unchanged stock system remains usable; one new spherical body loads through Jakobian; final body IDs/hierarchy are stable; scene/save/reload behavior is checked; load errors do not leave a partly published system. Report the implemented config subset precisely. A competing-loader test must produce deliberate ownership handling rather than an injection race. The later torus slice builds on this same standalone loader.
