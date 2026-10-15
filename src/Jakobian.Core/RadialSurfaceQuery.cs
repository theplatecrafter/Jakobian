using System;

namespace Jakobian.Core
{
    /// <summary>A radial/down-ray projection, never a Euclidean nearest-point result.</summary>
    public readonly struct RadialSurfaceSample
    {
        public readonly Vector3d Point, Normal, RadialUp, North, East;
        public readonly double Clearance, ReferenceAltitude, LatitudeDegrees, LongitudeDegrees, SlopeDegrees;
        public readonly bool HasHeading;
        internal RadialSurfaceSample(Vector3d point, Vector3d normal, Vector3d up, Vector3d north, Vector3d east,
            double clearance, double altitude, double latitude, double longitude, bool hasHeading)
        {
            Point = point; Normal = normal; RadialUp = up; North = north; East = east;
            Clearance = clearance; ReferenceAltitude = altitude; LatitudeDegrees = latitude; LongitudeDegrees = longitude;
            HasHeading = hasHeading;
            SlopeDegrees = Math.Acos(Math.Max(-1, Math.Min(1, Vector3d.Dot(normal, up)))) * 180 / Math.PI;
        }
    }

    public static class RadialSurfaceQuery
    {
        public static bool TrySample(RadialShape shape, Vector3d position, double referenceRadius, out RadialSurfaceSample sample)
        {
            sample = default(RadialSurfaceSample);
            double radius = position.Length;
            if (shape == null || !position.IsFinite || !Vector3d.Finite(radius) || radius < 1e-9
                || !Vector3d.Finite(referenceRadius) || referenceRadius <= 0) return false;
            Vector3d up = position / radius;
            double floorRadius = shape.RadiusAt(up);
            Vector3d point = up * floorRadius, normal;
            if (shape.Kind == RadialShapeKind.Sphere) normal = up;
            else if (shape.Kind == RadialShapeKind.Peanut)
            {
                Vector3d derivative = new Vector3d(4 * shape.X * shape.Lobes * up.X, 0, 0);
                normal = up - (derivative - up * Vector3d.Dot(up, derivative)) / floorRadius;
            }
            else
            {
                double p = shape.Kind == RadialShapeKind.Ellipsoid ? 2 : shape.Exponent;
                normal = new Vector3d(Gradient(point.X, shape.X, p), Gradient(point.Y, shape.Y, p), Gradient(point.Z, shape.Z, p));
            }
            normal = normal / normal.Length;
            Vector3d north = new Vector3d(0, 1, 0) - up * up.Y;
            bool heading = north.Length > 1e-10;
            north = heading ? north / north.Length : new Vector3d();
            Vector3d east = heading ? Vector3d.Cross(up, north) : new Vector3d();
            sample = new RadialSurfaceSample(point, normal, up, north, east, radius - floorRadius, radius - referenceRadius,
                Math.Asin(Math.Max(-1, Math.Min(1, up.Y))) * 180 / Math.PI, Math.Atan2(up.Z, up.X) * 180 / Math.PI, heading);
            return normal.IsFinite;
        }

        private static double Gradient(double value, double axis, double exponent) => Math.Sign(value) * Math.Pow(Math.Abs(value) / axis, exponent - 1) / axis;
    }
}
