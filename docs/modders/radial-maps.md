# Radial terrain maps and native biomes

Implemented in development 0.5.0 for the existing radial backend. See the enabled [Atlas configuration](../../GameData/JakobianTestBodies/Atlas.cfg). Atmosphere, water, overhangs, arbitrary expression functions and general solids remain separate future providers.

Add a `Terrain` child to a new body's `Body` node:

```text
Terrain
{
    heightMap = MyPack/Maps/height.png
    colorMap = MyPack/Maps/color.png
    offset = 0
    deformity = 600
    biomeMap = MyPack/Maps/biomes.png
    Biomes
    {
        Biome
        {
            name = Highlands
            displayName = Highlands
            red = 224
            green = 188
            blue = 124
            value = 1.2
        }
    }
}
```

The example shows one palette entry; **every** color in a real biome image must have a matching entry. Unknown or duplicate keys, duplicate biome names/colors, unassigned pixels, invalid dimensions or paths fail loading before system publication. Biome IDs use `name` (ASCII letters/digits/underscore, starting with a letter, at most 64 characters); keep them stable for saved science. Display labels may contain spaces. `value` populates the native map attribute, defaults to 1 and must be in [0,100]. Overall experiment/body science multipliers remain inherited from the current Minmus template.

## Map contract

- GameData-relative, case-consistent `.png` paths; no absolute paths, traversal or links. Source PNG size at most 16 MiB, non-indexed 8-bit grayscale/RGB/RGBA, width 4..2048 and height 3..1025. PNG16, DDS, JPEG, palette PNG and streaming maps are not implemented.
- Local axes match PQS: +Y north, longitude zero +X, positive longitude toward +Z. Columns sample longitude `360*x/width`; omit a duplicate final longitude column. Rows include both poles at latitude `-90 + 180*y/(height-1)`. Image editors display north at the top; Unity decodes south into row zero.
- Each pole row must have exactly one repeated RGBA value. There is no unique longitude at a pole. This rejects a real singularity instead of hiding a seam. Wrap occurs between the final column and the first; design that transition smoothly.
- Height PNG must be opaque grayscale, R=G=B. Bytes are linear data; no gamma conversion. Radius is `baseShapeRadius(direction) + offset + deformity * red/255`, with bilinear interpolation across the periodic column seam and clamped polar rows.
- Offset and deformity are metres, finite, with `deformity >= 0`, `abs(offset)+deformity <= baseMinimumRadius*0.25`, and final conservative minimum radius at least 100 m. Nonzero deformity requires a height map. These conservative restrictions keep this initial radial backend bounded; they are not a general topology limit.
- Color is an sRGB albedo map, sampled in the shader with the same chart and texel positioning. Encoded direction channels avoid interpolating longitude across the seam. Maps have no mipmaps in this first implementation, so distant aliasing and texture filtering need further rendering work. Scaled and contact materials are separate instances; a dither transition joins their ranges.
- Biomes use exact opaque RGB palette bytes and nearest-neighbor classification. No antialiasing or blended boundary colors. Supply `biomeMap` and `Biomes` together; 1..256 entries are supported. The native biome bridge implements `CBAttributeMapSO.GetAtt`, used by stock science and ResourceMap; it also provides a compiled map in the stock longitude convention for map consumers. Mods bypassing that method or depending on a concrete map subclass need separate compatibility testing.

Height maps, the scaled mesh and `BodyQueries` use the same immutable CPU field. Surface normals for displaced terrain use symmetric tangent derivatives of that field; actual triangle collider normals remain tessellation-dependent. Bilinear fields have derivative discontinuities at pixel boundaries. Geometry stays radial and cannot represent a cave, torus hole or interior cylinder floor. Native gravity vertical remains radial and differs from the terrain normal on slopes.

The mapped shader receives local unit direction through Mesh UV3/UV4. Native PQS's planet-relative modifier rewrites UV/UV2 after vertex callbacks, so those channels remain under native ownership. Finished contact meshes are checked separately from the generated scaled mesh. The current API does not promise free use of these channels to another terrain shader/modifier without an adapter.

## Development fixture and persistence

Atlas is a new ellipsoid, index 21 and stable ID `jakobian/tests/atlas-v1`, with 600 m maximum mapped displacement and four biomes: RustPlains, Highlands, CraterBasins and PolarCaps. It is airless and dry. Its maps are original deterministic fixtures from [Generate-TestMaps.py](../../tools/Generate-TestMaps.py), not external or extracted game textures. They include deliberately synthetic craters and polar caps; the caps do not imply water or ice physics.

The previous four fixtures are unchanged in name, indices, shape, spin and configuration bytes. Do not change Atlas geometry/maps or biome identity while reusing landed/science saves without a migration or new identity. Saved-world descriptor enforcement is still pending; the loader does not currently prevent a pack author from making an incompatible change.

The custom shader bundle targets Windows x64 with Unity 2019.4.18f1. Rebuild it with `tools/Build-Assets.ps1` after editing shader sources, then run `tools/Build.ps1`. The build manifest checks source and bundle hashes before deployment. Text source hashes use UTF-8 with LF-normalized line endings so Git's Windows checkout conversion does not force an unnecessary rebuild; binary hashes cover the exact bytes. Other graphics platforms, resource overlay rendering, shadows and third-party visual mods are not certified by the basic landing probe.
