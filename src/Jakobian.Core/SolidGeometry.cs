using System;
using System.Collections.Generic;

namespace Jakobian.Core
{
    public enum GeometryQueryStatus { Complete, InvalidInput, BudgetExceeded, Indeterminate }
    public enum SolidLocation { Outside, Inside, Boundary, Invalid }
    public enum BoundaryCrossing { Enter, Exit, Touch }

    public readonly struct SolidRayHit
    {
        public readonly double Distance;
        public readonly Vector3d Point, Normal;
        public readonly BoundaryCrossing Crossing;
        public readonly string SurfaceId;
        internal SolidRayHit(double distance, Vector3d point, Vector3d normal, BoundaryCrossing crossing)
        { Distance = distance; Point = point; Normal = normal; Crossing = crossing; SurfaceId = "exterior"; }
    }

    /// <summary>Closed solids in body-local metres. This contract does not assume a radial ground surface.</summary>
    public interface IClosedSolid
    {
        double BoundingRadius { get; }
        double SignedDistance(Vector3d point);
        SolidLocation Classify(Vector3d point, double tolerance);
        GeometryQueryStatus RaycastAll(Vector3d origin, Vector3d direction, double maxDistance, double tolerance,
            int evaluationBudget, out SolidRayHit[] hits);
    }

    public sealed class AnalyticSolid : IClosedSolid
    {
        public bool IsTorus { get; }
        public double MajorRadius { get; }
        public double MinorRadius { get; }
        public double BoundingRadius => IsTorus ? MajorRadius + MinorRadius : MinorRadius;
        public AnalyticSolid(double sphereRadius) : this(0, sphereRadius, false) { }
        public AnalyticSolid(double majorRadius, double minorRadius) : this(majorRadius, minorRadius, true) { }
        private AnalyticSolid(double major, double minor, bool torus)
        {
            if (!Vector3d.Finite(major) || !Vector3d.Finite(minor) || minor <= 0 || major + minor > 1e15
                || (torus && major <= minor)) throw new ArgumentOutOfRangeException(nameof(minor));
            MajorRadius = major; MinorRadius = minor; IsTorus = torus;
        }

        public double SignedDistance(Vector3d p)
        {
            if (!p.IsFinite || !Vector3d.Finite(p.Length)) return double.NaN;
            if (!IsTorus) return p.Length - MinorRadius;
            double radial = Math.Sqrt(p.X * p.X + p.Z * p.Z) - MajorRadius;
            return Math.Sqrt(radial * radial + p.Y * p.Y) - MinorRadius;
        }

        public SolidLocation Classify(Vector3d p, double tolerance)
        {
            if (!Vector3d.Finite(tolerance) || tolerance < 0) return SolidLocation.Invalid;
            double distance = SignedDistance(p);
            if (!Vector3d.Finite(distance)) return SolidLocation.Invalid;
            return Math.Abs(distance) <= tolerance ? SolidLocation.Boundary : distance < 0 ? SolidLocation.Inside : SolidLocation.Outside;
        }

        public GeometryQueryStatus RaycastAll(Vector3d origin, Vector3d direction, double maxDistance, double tolerance,
            int evaluationBudget, out SolidRayHit[] hits)
        {
            hits = new SolidRayHit[0];
            double length = direction.Length;
            if (!origin.IsFinite || !direction.IsFinite || !Vector3d.Finite(length) || length < 1e-12
                || !Vector3d.Finite(maxDistance) || maxDistance < 0 || !Vector3d.Finite(tolerance) || tolerance <= 0
                || tolerance >= MinorRadius / 100 || evaluationBudget < 1) return GeometryQueryStatus.InvalidInput;
            double scale = BoundingRadius;
            if (origin.Length / scale > 1e8 || origin.Length * 1e-14 > tolerance || tolerance / scale < 1e-12 || MinorRadius / scale < 1e-5)
                return GeometryQueryStatus.Indeterminate;
            Vector3d d = direction / length, o = origin / scale;
            double centre = -Vector3d.Dot(o, d);
            Vector3d q = o + d * centre;
            double discriminant = 1 - Vector3d.Dot(q, q);
            if (discriminant < -1e-14) return GeometryQueryStatus.Complete;
            double half = Math.Sqrt(Math.Max(0, discriminant));
            double lo = Math.Max(-half, -centre), hi = Math.Min(half, maxDistance / scale - centre);
            if (lo > hi) return GeometryQueryStatus.Complete;
            double[] coefficients;
            double a = Vector3d.Dot(q, q), b = 2 * Vector3d.Dot(q, d);
            if (!IsTorus) coefficients = new[] { a - 1, b, 1d };
            else
            {
                double r = MajorRadius / scale, tube = MinorRadius / scale, c = a + r * r - tube * tube;
                coefficients = new[] { c * c - 4 * r * r * (q.X * q.X + q.Z * q.Z),
                    2 * b * c - 8 * r * r * (q.X * d.X + q.Z * d.Z),
                    b * b + 2 * c - 4 * r * r * (d.X * d.X + d.Z * d.Z), 2 * b, 1d };
            }
            var roots = new List<double>();
            if (!PolynomialRoots.Isolate(coefficients, lo, hi, tolerance / scale / 8, ref evaluationBudget, roots))
                return GeometryQueryStatus.BudgetExceeded;
            var result = new List<SolidRayHit>();
            foreach (double root in roots)
            {
                double distance = Math.Max(0, (centre + root) * scale);
                Vector3d point = (q + d * root) * scale;
                if (Math.Abs(SignedDistance(point)) > tolerance * 4) return GeometryQueryStatus.Indeterminate;
                Vector3d normal;
                if (IsTorus)
                {
                    double radial = Math.Sqrt(point.X * point.X + point.Z * point.Z);
                    if (radial < 1e-12) return GeometryQueryStatus.Indeterminate;
                    normal = point - new Vector3d(point.X / radial * MajorRadius, 0, point.Z / radial * MajorRadius);
                }
                else normal = point;
                normal = normal / normal.Length;
                double cosine = Vector3d.Dot(normal, d);
                BoundaryCrossing crossing = Math.Abs(cosine) < 1e-7 ? BoundaryCrossing.Touch : cosine < 0 ? BoundaryCrossing.Enter : BoundaryCrossing.Exit;
                // Two distinct crossings below the requested resolution cannot safely
                // be reduced to one Enter/Exit and then used as a solid interval.
                if (result.Count > 0 && distance - result[result.Count - 1].Distance <= tolerance)
                    return GeometryQueryStatus.Indeterminate;
                result.Add(new SolidRayHit(distance, point, normal, crossing));
            }
            hits = result.ToArray();
            return GeometryQueryStatus.Complete;
        }
    }

    // Derivative roots partition a polynomial into monotonic intervals. No uniform ray sampling or
    // unbounded Newton iteration is used. Near-coincident contacts resolve only to caller tolerance.
    internal static class PolynomialRoots
    {
        internal static bool Isolate(double[] c, double lo, double hi, double tolerance, ref int budget, List<double> roots)
        {
            int degree = c.Length - 1;
            if (degree == 1)
            {
                if (--budget < 0) return false;
                double root = -c[0] / c[1];
                if (root >= lo && root <= hi) roots.Add(root);
                return true;
            }
            var derivative = new double[degree];
            for (int i = 1; i <= degree; i++) derivative[i - 1] = i * c[i];
            var critical = new List<double>();
            if (!Isolate(derivative, lo, hi, tolerance / 4, ref budget, critical)) return false;
            var knots = new List<double> { lo };
            foreach (double root in critical) if (root > lo && root < hi) knots.Add(root);
            knots.Add(hi); knots.Sort();
            double norm = 0; foreach (double coefficient in c) norm += Math.Abs(coefficient);
            double residualTolerance = Math.Max(1, norm) * 2e-14;
            for (int i = 0; i < knots.Count; i++)
            {
                if (--budget < 0) return false;
                double left = knots[i], value = Evaluate(c, left);
                if (Math.Abs(value) <= residualTolerance) Add(roots, left, tolerance * 4);
                if (i + 1 == knots.Count) break;
                double right = knots[i + 1];
                if (--budget < 0) return false;
                double endValue = Evaluate(c, right);
                if (Math.Abs(value) <= residualTolerance || Math.Abs(endValue) <= residualTolerance || Math.Sign(value) == Math.Sign(endValue)) continue;
                while (right - left > tolerance)
                {
                    if (--budget < 0) return false;
                    double middle = (left + right) / 2, f = Evaluate(c, middle);
                    if (f == 0) { left = right = middle; break; }
                    if (Math.Sign(f) == Math.Sign(value)) { left = middle; value = f; } else right = middle;
                }
                Add(roots, (left + right) / 2, tolerance * 4);
            }
            roots.Sort(); return true;
        }
        private static void Add(List<double> roots, double root, double tolerance)
        { foreach (double existing in roots) if (Math.Abs(existing - root) <= tolerance) return; roots.Add(root); }
        private static double Evaluate(double[] c, double x)
        { double result = c[c.Length - 1]; for (int i = c.Length - 2; i >= 0; i--) result = result * x + c[i]; return result; }
    }
}
