using Jakobian.Core;

namespace Jakobian.KSP
{
    public enum BodyQueryStatus { Available, UnsupportedBody, InvalidPosition, UnavailableFrame, InvalidRequest }

    /// <summary>Development query ABI. World vectors use Unity Y-up; positions are current floating-origin doubles.</summary>
    public readonly struct BodyPointSample
    {
        public readonly string BodyId, SurfaceId;
        public readonly string BiomeName;
        public readonly double UniversalTime;
        public readonly int UnityFrame;
        public readonly RadialSurfaceSample LocalSurface;
        public readonly global::Vector3d GroundWorld, SurfaceNormalWorld, GravityWorld, GravityUpWorld, NorthWorld, EastWorld;
        public readonly global::Vector3d CorotationVelocityRelativeToBodyWorld;
        // Current provider is explicitly airless/dry. It does not synthesize resource abundance or biomes.
        public bool HasAtmosphere => false;
        public bool InFluid => false;
        public double PressurePa => 0;
        public double DensityKgM3 => 0;
        internal BodyPointSample(CelestialBody body, BodyDefinition definition, RadialSurfaceSample surface, global::Vector3d gravity, global::Vector3d position)
        {
            BodyId = definition.Id; SurfaceId = "exterior"; LocalSurface = surface;
            BiomeName = definition.Assets.BiomePixels == null ? ScienceUtil.GetExperimentBiome(body,surface.LatitudeDegrees,surface.LongitudeDegrees)
                : definition.Assets.Biomes[definition.Assets.BiomePixels.Nearest(surface.RadialUp)].name;
            UniversalTime = Planetarium.GetUniversalTime(); UnityFrame = UnityEngine.Time.frameCount;
            GroundWorld = body.position + BodyQueries.LocalToWorldVector(body, surface.Point);
            SurfaceNormalWorld = BodyQueries.LocalToWorldVector(body, surface.Normal);
            GravityUpWorld = BodyQueries.LocalToWorldVector(body, surface.RadialUp);
            NorthWorld = BodyQueries.LocalToWorldVector(body, surface.North);
            EastWorld = BodyQueries.LocalToWorldVector(body, surface.East);
            GravityWorld = gravity;
            CorotationVelocityRelativeToBodyWorld = body.getRFrmVel(position);
        }
    }

    /// <summary>One scoped geometry/physics context for owned radial bodies. No native caches are overwritten.</summary>
    public static class BodyQueries
    {
        public static BodyQueryStatus Sample(CelestialBody body, global::Vector3d worldPosition, out BodyPointSample sample)
        {
            sample = default(BodyPointSample);
            BodyDefinition definition;
            if (body == null) return BodyQueryStatus.UnsupportedBody;
            if (!SystemLoader.Bound) return BodyQueryStatus.UnavailableFrame;
            if (!SystemLoader.OwnedBodies.TryGetValue(body, out definition)) return BodyQueryStatus.UnsupportedBody;
            global::Vector3d local = WorldToLocalVector(body, worldPosition - body.position);
            RadialSurfaceSample surface;
            if (!definition.Surface.TrySample(Core(local), body.Radius, out surface)) return BodyQueryStatus.InvalidPosition;
            Jakobian.Core.Vector3d gravity;
            if (!ReferencePhysics.TryPointGravity(Core(worldPosition - body.position), definition.Mu, out gravity)) return BodyQueryStatus.InvalidPosition;
            sample = new BodyPointSample(body, definition, surface, Ksp(gravity), worldPosition);
            return BodyQueryStatus.Available;
        }

        public static global::Vector3d WorldToLocalVector(CelestialBody body, global::Vector3d vector) => body.BodyFrame.WorldToLocal(vector.xzy).xzy;
        public static global::Vector3d LocalToWorldVector(CelestialBody body, Jakobian.Core.Vector3d vector) => body.BodyFrame.LocalToWorld(Ksp(vector).xzy).xzy;

        /// <summary>Coordinate bridge only. Stock ResourceMap still owns abundance, locks and biome lookup.</summary>
        public static BodyQueryStatus CreatePlanetaryResourceRequest(CelestialBody body, global::Vector3d worldPosition,
            string resourceName, bool checkForLock, out AbundanceRequest request)
        {
            request = default(AbundanceRequest);
            if (string.IsNullOrWhiteSpace(resourceName)) return BodyQueryStatus.InvalidRequest;
            BodyPointSample sample;
            BodyQueryStatus status = Sample(body, worldPosition, out sample);
            if (status != BodyQueryStatus.Available) return status;
            request = new AbundanceRequest {
                BodyId = body.flightGlobalsIndex, Latitude = sample.LocalSurface.LatitudeDegrees,
                Longitude = sample.LocalSurface.LongitudeDegrees, Altitude = sample.LocalSurface.ReferenceAltitude,
                ResourceName = resourceName, ResourceType = HarvestTypes.Planetary, CheckForLock = checkForLock
            };
            return status;
        }
        internal static Jakobian.Core.Vector3d Core(global::Vector3d v) => new Jakobian.Core.Vector3d(v.x, v.y, v.z);
        internal static global::Vector3d Ksp(Jakobian.Core.Vector3d v) => new global::Vector3d(v.X, v.Y, v.Z);
    }
}
