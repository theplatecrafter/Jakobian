using System;
using Jakobian.Core;
using UnityEngine;
using CoreVector = Jakobian.Core.Vector3d;
using WorldVector = global::Vector3d;

namespace Jakobian.KSP
{
    public enum GeneralBodyQueryStatus { InvalidRequest, Available, InvalidFrame }

    public readonly struct GeneralBodyPointSample
    {
        public readonly GeneralSurfaceSample Local;
        public readonly WorldVector WorldPosition, GravityWorld;
        public readonly WorldVector? UpWorld, NorthWorld, EastWorld;
        public readonly WorldVector? NearestPointWorld, NearestNormalWorld, GroundPointWorld, GroundNormalWorld;
        public readonly int UnityFrame;
        internal GeneralBodyPointSample(GeneralSurfaceSample local, RigidQueryFrame frame, WorldVector position, WorldVector gravity)
        {
            Local = local; WorldPosition = position; GravityWorld = gravity; UnityFrame = Time.frameCount;
            UpWorld = frame.OptionalVector(local.Up); NorthWorld = frame.OptionalVector(local.North); EastWorld = frame.OptionalVector(local.East);
            NearestPointWorld = local.Nearest.HasValue ? frame.Point(local.Nearest.Value.Point) : (WorldVector?)null;
            NearestNormalWorld = local.Nearest.HasValue ? frame.Vector(local.Nearest.Value.Normal) : (WorldVector?)null;
            GroundPointWorld = local.Ground.HasValue ? frame.Point(local.Ground.Value.Point) : (WorldVector?)null;
            GroundNormalWorld = local.Ground.HasValue ? frame.Vector(local.Ground.Value.Normal) : (WorldVector?)null;
        }
    }

    /// <summary>Experimental owner-thread bridge in the caller's current Unity physics chart.
    /// Does not register a celestial body or inject native vessel/UI caches.</summary>
    public static class GeneralBodyQueries
    {
        public static GeneralBodyQueryStatus Sample(Transform root, IGeneralSurfaceGeometry geometry,
            WorldVector worldPosition, WorldVector gravityWorld, WorldVector northAxisWorld,
            GeneralQuerySettings settings, out GeneralBodyPointSample sample)
        {
            sample = default(GeneralBodyPointSample);
            if (!RigidQueryFrame.TryCapture(root, out var frame)) return GeneralBodyQueryStatus.InvalidFrame;
            if (!GeneralSurfaceQuery.TrySample(geometry, frame.LocalPoint(worldPosition), frame.LocalVector(gravityWorld),
                frame.LocalVector(northAxisWorld), settings, out var local)) return GeneralBodyQueryStatus.InvalidRequest;
            sample = new GeneralBodyPointSample(local, frame, worldPosition, gravityWorld);
            return GeneralBodyQueryStatus.Available;
        }
    }

    internal readonly struct RigidQueryFrame
    {
        private readonly CoreVector origin, x, y, z;
        private RigidQueryFrame(CoreVector origin, CoreVector x, CoreVector y, CoreVector z)
        { this.origin = origin; this.x = x; this.y = y; this.z = z; }
        internal static bool TryCapture(Transform root, out RigidQueryFrame frame)
        {
            frame = default(RigidQueryFrame);
            if (root == null) return false;
            Matrix4x4 matrix = root.localToWorldMatrix;
            CoreVector Column(int i) { Vector4 v = matrix.GetColumn(i); return new CoreVector(v.x, v.y, v.z); }
            CoreVector origin = Column(3), x = Column(0), y = Column(1), z = Column(2);
            // Inspect the actual matrix, including parent scaling, shear and reflection.
            if (!origin.IsFinite || !x.IsFinite || !y.IsFinite || !z.IsFinite
                || Math.Abs(x.Length - 1) > 1e-6 || Math.Abs(y.Length - 1) > 1e-6 || Math.Abs(z.Length - 1) > 1e-6
                || Math.Abs(CoreVector.Dot(x, y)) > 1e-6 || Math.Abs(CoreVector.Dot(x, z)) > 1e-6
                || Math.Abs(CoreVector.Dot(y, z)) > 1e-6 || Math.Abs(CoreVector.Dot(CoreVector.Cross(x, y), z) - 1) > 1e-6) return false;
            frame = new RigidQueryFrame(origin, x, y, z); return true;
        }
        internal CoreVector LocalVector(WorldVector v)
        {
            CoreVector value = BodyQueries.Core(v);
            return new CoreVector(CoreVector.Dot(value, x), CoreVector.Dot(value, y), CoreVector.Dot(value, z));
        }
        internal CoreVector LocalPoint(WorldVector p) => LocalVector(p - BodyQueries.Ksp(origin));
        internal WorldVector Vector(CoreVector v) => BodyQueries.Ksp(x * v.X + y * v.Y + z * v.Z);
        internal WorldVector Point(CoreVector p) => BodyQueries.Ksp(origin) + Vector(p);
        internal WorldVector? OptionalVector(CoreVector? v) => v.HasValue ? Vector(v.Value) : (WorldVector?)null;
    }
}
