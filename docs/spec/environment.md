# Atmosphere, fluids, illumination and hazards

Status: proposed specification. None of these arbitrary-shape environment providers exists in the diagnostic scaffold.

## Atmosphere is a spatial volume

Retain familiar `Atmosphere` settings for spherical bodies loaded by Jakobian. General bodies define one or more `Volume` nodes with stable ID, containment, datum coordinate, pressure/temperature/composition fields, motion, optional terrain-occlusion masks and overlap policy.

Useful containment models: finite height band above selected chart regions; bounded implicit region; closed mesh volume; connected interior chamber; conservative-potential band. A ring's floor height alone cannot tell whether a point beyond its rim is in air. A hollow body's exterior atmosphere and interior air are different volumes and may have different gas/pressure.

`AirSample(p,t)` returns pressure (Pa in core), temperature (K), density (kg/m³), composition/oxygen fraction, gamma/molar mass or sound speed, gas velocity, volume identity, boundary distance and validity. Convert Pa to **kPa** where the KSP API requires it. Preserve legacy curve normalization semantics on the standalone spherical path; new curves declare axis units/datum and normalization explicitly. Never import an old tutorial's pressure values by assuming its units.

Recommended new authoring curve: `pressureUnit = kPa`, `heightCoordinate = SurfaceDatum`, unnormalized heights in metres, `pressureCurve` keys carrying explicitly defined pressure units. Runtime internal Pa avoids mixed-unit calculations. Use `rho = p M / (R_universal T)` only for an explicitly ideal-gas model; require positive temperature/molar mass and coherent composition. Oxygen flags describe gameplay capability; an adapter may need actual oxygen partial pressure rather than a global body flag.

## Selecting an atmospheric height

For an exterior sphere, radial/reference height is familiar. For an interior cylinder, datum height increases inward from the floor. For a lumpy asteroid, nearest-surface height is an artistic surface-following atmosphere, not an automatically hydrostatic equilibrium. For physically conservative gravity one can use potential differences: isothermal `p = p0 exp[-(Phi_eff - Phi0)/(R_specific T)]`. This requires connected gas domains and valid potential/temperature assumptions; it is not appropriate for every arbitrary vector field.

Declare a volume's height policy rather than reusing whichever surface happens to be nearest on that frame. Volume selection uses containment and explicit priority/composition policy. Two overlapping air definitions cannot simply add pressure/oxygen or jump to the nearest ceiling. Smooth defined boundaries where physically/model-wise appropriate; interior doors/partitions are optional explicit chamber features.

Atmosphere pressure is zero outside its defined region; gradual edge masks may approximate open edges and escape. A rotating open ring retaining breathable air through artificial rim walls is a prescribed atmosphere model, not a computational fluid simulation. Warn in author diagnostics if chosen dimensions/field cannot support the intended layer; allow intentional fictional models with explicit declarations.

## Motion and game consumers

Gas may corotate, have a fixed inertial flow or add a body-local wind field. Drag uses vessel/part velocity relative to the gas. Rotation does not mean velocity zero in inertial coordinates. Publish local pressure/density/sound/temperature at verified stock/MFI boundaries and audit FAR separately. Thermal flux needs local relative speed and illumination, not just corrected pressure.

Adapters cover engines, intakes, parachutes, aero/heating effects, thermal integrator, EVA helmet safety, life support, atmospheric science, cameras and telemetry. Stock `CelestialBody.GetPressure(altitude)` lacks a position/region argument; patching it globally cannot disambiguate two chambers at the same altitude. Prefer coherent vessel/part cache publication and position-aware API adapters. Unknown direct scalar callers get a documented reference profile and capability limitation.

## Water and other fluids

Separate visual water surface from fluid occupancy. Define `Fluid { id; type; density; Volume; FreeSurface; Motion; Properties }` scoped to a body/region. Properties include viscosity/drag approximation, temperature, hazards, sound/underwater optics and a resource identity. A body can have disconnected lakes, interior oceans and exterior reservoirs; `Ocean` alone remains a spherical configuration/convenience alias implemented by Jakobian.

Free-surface choices:

- A chart datum level with a solid-clipped bounded reservoir, convenient for ring-floor lakes.
- A closed arbitrary volume with prescribed surface, useful for intentional fictional fluid shapes.
- An effective equipotential with reservoir/connectivity constraints for a conservative stationary model.

For a constant rotating frame, `Phi_eff = Phi_gravity - 1/2 |omega×r|²`; free surfaces of resting fluid can follow this potential within a reservoir. Arbitrary acceleration fields may not admit a hydrostatic equipotential. Zero-gravity open water is outside the first hydrostatic backend; a sealed prescribed reservoir can still be authored without claiming natural equilibrium.

Fluid sample supplies depth/immersion fraction, fluid velocity, density, free-surface normal and reservoir ID. Geometric depth follows the defined free surface; vessel COM below a plane does not imply all its parts are immersed. Approximate displaced volume per part from verified stock bounds/volume behavior, apply buoyancy opposite the relevant effective acceleration and fluid-relative drag, and preserve third-party hull/ballast providers. Avoid adding the custom buoyancy on top of stock buoyancy. Test sinking, asymmetric immersion, waves and zero-gravity fallback separately.

`SPLASHED` requires real fluid intersection/support policy. Contact with dry rock remains `LANDED` even if a distant region on the same body contains ocean. Underwater state, intakes and camera effects sample the local reservoir. Water shaders/reflections are replaceable visual providers; using Scatterer for spherical oceans does not make it understand an arbitrary curved reservoir.

## Lighting and rendering

Generate scaled and contact views from the same descriptor. Use correct bounds, true geometry silhouettes, normals and named surface material assignments. Cull occluded patches by geometry, not spherical horizon alone. The torus hole and hollow interior require two-sided visibility decisions with correctly oriented surface faces.

Stars/light emitters retain familiar color/intensity/flare/corona concepts. Treat luminosity and landable solid geometry separately: a luminous mesh can be landable fictionally, while physically stellar gas can be a hazard volume with no support surface. Multiple stars, ring shadow panels, self-shadowing and artificial light sources are provider capabilities. Physical solar flux consumers, panels and visibility should share the same occlusion result used by visual effects where accuracy is promised.

Use finite-star ray sampling or other bounded approximation with declared error/quality. Planet/ring/structure eclipses need shape-aware ray intersections. Cache light transforms and identical sample inputs once per query batch/tick; do not repeat expensive eclipse/terrain calculations for every cloud layer sharing the same point. Keep optical atmospheres/clouds/water separate from physical environment providers and expose their mismatch/quality status.

EVE/Scatterer/Parallax support requires explicit adapters to Jakobian's spherical/general rendering backends; their spherical shader constants cannot be filled with an arbitrary bounding radius and called compatible. Custom shaders and asset bundles use the verified Unity 2019.4.18f1 editor; start with minimal shaders, then backend/platform-specific QA.

## Hazards and optional systems

Bounded fields can define temperature/radiation, pressure damage, corrosive fluids, luminous plasma, tidal stresses and force hazards. Define units, sampling frequency, applicability and overlap behavior. Existing stock heat/crew G and science effects should continue when valid; life-support/radiation mods retain ownership unless a negotiated adapter supplies local inputs.

Weather, currents, erosion, geological simulation and dynamic doors are opt-in modules, not required to land on an arbitrary shape. Initial framework supplies deterministic fields and static reservoirs; advanced simulations need save schema, background integration and performance budgets of their own.
