using System;

namespace Jakobian.Core
{
    public sealed class SphereSurface : IAnalyticSurface
    {
        public double Radius { get; }
        public SphereSurface(double radius)
        {
            if (!Vector3d.Finite(radius) || radius <= 0 || radius > 1e15) throw new ArgumentOutOfRangeException(nameof(radius));
            Radius = radius;
        }
        public bool TryNearest(Vector3d p, out SurfaceSample sample)
        {
            sample = default(SurfaceSample);
            double length = p.Length;
            if (!p.IsFinite || !Vector3d.Finite(length) || length < 1e-12) return false;
            Vector3d n = p / length;
            double u = Chart.WrapUnit(Math.Atan2(n.Z, n.X) / (2 * Math.PI));
            double v = Math.Asin(Math.Max(-1, Math.Min(1, n.Y))) / Math.PI + .5;
            sample = new SurfaceSample("exterior", "spherical", n * Radius, n, length - Radius, u, v);
            return true;
        }
    }

    /// <summary>Solid circular torus: major radius R, tube radius r; axis +Y. No artificial gravity is implied.</summary>
    public sealed class TorusSurface : IAnalyticSurface
    {
        public double MajorRadius { get; }
        public double MinorRadius { get; }
        public TorusSurface(double majorRadius, double minorRadius)
        {
            if (!Vector3d.Finite(majorRadius) || !Vector3d.Finite(minorRadius) || minorRadius <= 0 || majorRadius <= minorRadius || majorRadius > 1e15)
                throw new ArgumentOutOfRangeException(nameof(majorRadius));
            MajorRadius = majorRadius; MinorRadius = minorRadius;
        }
        public bool TryNearest(Vector3d p, out SurfaceSample sample)
        {
            sample = default(SurfaceSample);
            if (!p.IsFinite) return false;
            double radial = Math.Sqrt(p.X * p.X + p.Z * p.Z);
            if (!Vector3d.Finite(radial) || radial < 1e-12) return false;
            Vector3d centre = new Vector3d(p.X / radial * MajorRadius, 0, p.Z / radial * MajorRadius);
            Vector3d offset = p - centre;
            double tubeDistance = offset.Length;
            if (!Vector3d.Finite(tubeDistance) || tubeDistance < 1e-12) return false;
            Vector3d normal = offset / tubeDistance;
            double u = Chart.WrapUnit(Math.Atan2(p.Z, p.X) / (2 * Math.PI));
            double v = Chart.WrapUnit(Math.Atan2(p.Y, radial - MajorRadius) / (2 * Math.PI));
            sample = new SurfaceSample("exterior", "toroidal", centre + normal * MinorRadius, normal, tubeDistance - MinorRadius, u, v);
            return true;
        }
    }

    internal static class Chart
    {
        internal static double WrapUnit(double x) => x - Math.Floor(x);
    }
}
