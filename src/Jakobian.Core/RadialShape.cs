using System;

namespace Jakobian.Core
{
    // Restricted star-shaped backend: exactly one positive radius per direction.
    // This is not the general topology backend used for tori or hollow bodies.
    public enum RadialShapeKind { Sphere, Ellipsoid, Superellipsoid, Peanut }

    public sealed class RadialShape
    {
        public RadialShapeKind Kind { get; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double Exponent { get; }
        public double Lobes { get; }
        public double MinimumRadius { get; }
        public double BoundingRadius { get; }

        public RadialShape(RadialShapeKind kind, double x, double y, double z, double exponent = 2, double lobes = .4)
        {
            if (!Enum.IsDefined(typeof(RadialShapeKind), kind) || !ValidAxis(x) || !ValidAxis(y) || !ValidAxis(z)
                || !Vector3d.Finite(exponent) || exponent < 2 || exponent > 16 || !Vector3d.Finite(lobes) || lobes < 0 || lobes > .65)
                throw new ArgumentException("Invalid radial shape dimensions, exponent or lobes.");
            Kind = kind; X = x; Y = y; Z = z; Exponent = exponent; Lobes = lobes;
            MinimumRadius = kind == RadialShapeKind.Peanut ? x * (1 - lobes) : kind == RadialShapeKind.Sphere ? x : Math.Min(x, Math.Min(y, z));
            BoundingRadius = kind == RadialShapeKind.Peanut ? x * (1 + lobes) : kind == RadialShapeKind.Superellipsoid
                ? Math.Sqrt(x * x + y * y + z * z) : kind == RadialShapeKind.Sphere ? x : Math.Max(x, Math.Max(y, z));
        }

        private static bool ValidAxis(double x) => Vector3d.Finite(x) && x >= 100 && x <= 1e8;

        public double RadiusAt(Vector3d direction)
        {
            double length = direction.Length;
            if (!direction.IsFinite || !Vector3d.Finite(length) || length < 1e-12) throw new ArgumentException("Direction must be finite and nonzero.");
            Vector3d d = direction / length;
            if (Kind == RadialShapeKind.Sphere) return X;
            if (Kind == RadialShapeKind.Peanut) return X * (1 + Lobes * (2 * d.X * d.X - 1));
            double power = Kind == RadialShapeKind.Ellipsoid ? 2 : Exponent;
            return 1 / Math.Pow(Math.Pow(Math.Abs(d.X) / X, power) + Math.Pow(Math.Abs(d.Y) / Y, power) + Math.Pow(Math.Abs(d.Z) / Z, power), 1 / power);
        }

        public Vector3d SurfacePoint(Vector3d direction) => direction / direction.Length * RadiusAt(direction);
    }
}
