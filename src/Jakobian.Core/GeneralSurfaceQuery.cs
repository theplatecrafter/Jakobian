using System;

namespace Jakobian.Core
{
    /// <summary>One provider owns classification, nearest projection and ray boundaries in local metres.</summary>
    public interface IGeneralSurfaceGeometry : IClosedSolid, IAnalyticSurface { }

    public enum GroundQueryStatus
    {
        InvalidInput, Contact, Miss, NoGravity, InsideSolid, OnBoundary, Tangent, BudgetExceeded, Indeterminate
    }

    public readonly struct GeneralQuerySettings
    {
        public readonly double ReferenceRadius, MaxGroundDistance, Tolerance;
        public readonly int EvaluationBudget;
        public GeneralQuerySettings(double referenceRadius, double maxGroundDistance, double tolerance, int evaluationBudget)
        {
            if (!Valid(referenceRadius, maxGroundDistance, tolerance, evaluationBudget))
                throw new ArgumentOutOfRangeException(nameof(referenceRadius), "Finite positive datum/tolerance, nonnegative ray length and budget 1..65536 required.");
            ReferenceRadius = referenceRadius; MaxGroundDistance = maxGroundDistance;
            Tolerance = tolerance; EvaluationBudget = evaluationBudget;
        }
        internal bool IsValid => Valid(ReferenceRadius, MaxGroundDistance, Tolerance, EvaluationBudget);
        private static bool Valid(double radius, double distance, double tolerance, int budget) =>
            Vector3d.Finite(radius) && radius > 0 && Vector3d.Finite(distance) && distance >= 0
            && Vector3d.Finite(tolerance) && tolerance > 0 && budget >= 1 && budget <= 65536;
    }

    /// <summary>Optional values are nullable. A miss, ambiguous nearest point or absent vertical is never zero ground.</summary>
    public readonly struct GeneralSurfaceSample
    {
        public readonly SolidLocation Location;
        public readonly double SignedDistance, ReferenceAltitude;
        public readonly Vector3d Gravity;
        public readonly SurfaceSample? Nearest;
        public readonly Vector3d? Up, North, East;
        public readonly GroundQueryStatus GroundStatus;
        public readonly SolidRayHit? Ground;
        public double? GroundClearance => Ground.HasValue ? Ground.Value.Distance : (double?)null;
        internal GeneralSurfaceSample(SolidLocation location, double signedDistance, double altitude, Vector3d gravity,
            SurfaceSample? nearest, Vector3d? up, Vector3d? north, Vector3d? east, GroundQueryStatus status, SolidRayHit? ground)
        {
            Location = location; SignedDistance = signedDistance; ReferenceAltitude = altitude; Gravity = gravity;
            Nearest = nearest; Up = up; North = north; East = east; GroundStatus = status; Ground = ground;
        }
    }

    /// <summary>Experimental context; supplied acceleration defines down, never a geometry-derived force.</summary>
    public static class GeneralSurfaceQuery
    {
        public static bool TrySample(IGeneralSurfaceGeometry geometry, Vector3d position, Vector3d gravity,
            Vector3d northAxis, GeneralQuerySettings settings, out GeneralSurfaceSample sample)
        {
            sample = default(GeneralSurfaceSample);
            double radius = position.Length, magnitude = gravity.Length, axisLength = northAxis.Length;
            if (geometry == null || !settings.IsValid || !position.IsFinite || !Vector3d.Finite(radius)
                || !gravity.IsFinite || !Vector3d.Finite(magnitude) || !northAxis.IsFinite
                || !Vector3d.Finite(axisLength) || axisLength < 1e-12) return false;
            double distance = geometry.SignedDistance(position);
            SolidLocation location = geometry.Classify(position, settings.Tolerance);
            if (!Vector3d.Finite(distance) || location == SolidLocation.Invalid) return false;
            SurfaceSample? nearest = geometry.TryNearest(position, out var projection) ? projection : (SurfaceSample?)null;
            Vector3d? up = null, north = null, east = null;
            if (magnitude > 0)
            {
                up = -gravity / magnitude;
                Vector3d axis = northAxis / axisLength;
                Vector3d tangent = axis - up.Value * Vector3d.Dot(axis, up.Value);
                if (tangent.Length > 1e-10)
                {
                    north = tangent / tangent.Length;
                    east = Vector3d.Cross(up.Value, north.Value);
                }
            }
            GroundQueryStatus status;
            SolidRayHit? ground = null;
            // Buried/boundary actors require contact recovery; an exit is not supporting ground.
            if (location == SolidLocation.Inside) status = GroundQueryStatus.InsideSolid;
            else if (location == SolidLocation.Boundary) status = GroundQueryStatus.OnBoundary;
            else if (!up.HasValue) status = GroundQueryStatus.NoGravity;
            else
            {
                GeometryQueryStatus ray = geometry.RaycastAll(position, -up.Value, settings.MaxGroundDistance,
                    settings.Tolerance, settings.EvaluationBudget, out var hits);
                switch (ray)
                {
                    case GeometryQueryStatus.BudgetExceeded: status = GroundQueryStatus.BudgetExceeded; break;
                    case GeometryQueryStatus.InvalidInput: status = GroundQueryStatus.InvalidInput; break;
                    case GeometryQueryStatus.Indeterminate: status = GroundQueryStatus.Indeterminate; break;
                    case GeometryQueryStatus.Complete:
                        if (hits.Length == 0) status = GroundQueryStatus.Miss;
                        else if (hits[0].Crossing == BoundaryCrossing.Touch) status = GroundQueryStatus.Tangent;
                        else if (hits[0].Crossing != BoundaryCrossing.Enter) status = GroundQueryStatus.Indeterminate;
                        else { status = GroundQueryStatus.Contact; ground = hits[0]; }
                        break;
                    default: status = GroundQueryStatus.Indeterminate; break;
                }
            }
            sample = new GeneralSurfaceSample(location, distance, radius - settings.ReferenceRadius, gravity,
                nearest, up, north, east, status, ground);
            return true;
        }
    }
}
