using System;
using Jakobian.Core;

internal static partial class Program
{
    private static void GeneralContextChecks()
    {
        var torus = new AnalyticSolid(1000, 200);
        var sphere = new AnalyticSolid(200);
        var settings = new GeneralQuerySettings(1000, 5000, 1e-5, 4096);
        var y = new Vector3d(0, 1, 0);
        GeneralSurfaceSample Sample(IGeneralSurfaceGeometry geometry, Vector3d p, Vector3d g) {
            Assert(GeneralSurfaceQuery.TrySample(geometry, p, g, y, settings, out var result), "general context available");
            return result;
        }
        var sample = Sample(torus, new Vector3d(1250, 0, 0), new Vector3d(-3, 0, 0));
        Assert(sample.GroundStatus == GroundQueryStatus.Contact && sample.Ground.HasValue, "outer supported contact");
        Near(sample.SignedDistance, 50, 1e-10, "outer nearest distance");
        Near(sample.ReferenceAltitude, 250, 1e-10, "datum independent of nearest/ground");
        Near(sample.GroundClearance.Value, 50, 1e-5, "outer down distance");
        Assert(sample.Nearest.Value.ChartId == TorusAnchor.ChartId, "context uses versioned material chart");
        Assert(torus.TryNearest(new Vector3d(1250, -1e-20, -1e-20), out var seam)
            && seam.U >= 0 && seam.U < 1 && seam.V >= 0 && seam.V < 1, "versioned chart canonicalizes rounded negative seams");
        VectorNear(sample.Up.Value, new Vector3d(1, 0, 0), 1e-12, "up from supplied gravity");
        VectorNear(sample.North.Value, y, 1e-12, "reference-axis heading");
        VectorNear(sample.East.Value, new Vector3d(0, 0, 1), 1e-12, "heading handedness");

        sample = Sample(torus, new Vector3d(750, 0, 0), new Vector3d(-3, 0, 0));
        Near(sample.Nearest.Value.Point.X, 800, 1e-10, "inner nearest wall on positive side");
        Near(sample.SignedDistance, 50, 1e-10, "inner nearest clearance");
        Near(sample.GroundClearance.Value, 1550, 1e-5, "gravity down crosses hole to far wall");
        Near(sample.Ground.Value.Point.X, -800, 1e-5, "ground is far inner wall");
        Near(Vector3d.Dot(sample.Nearest.Value.Normal, sample.Up.Value), -1, 1e-12, "nearest normal is opposite navigation up");
        Near(sample.ReferenceAltitude, -250, 1e-12, "negative datum does not imply underground");
        Assert(sample.Location == SolidLocation.Outside, "inner hole outside despite negative datum");

        sample = Sample(torus, new Vector3d(1000, 250, 0), -y * 2);
        Near(sample.GroundClearance.Value, 50, 1e-5, "top wall down-ray");
        Assert(sample.Up.HasValue && !sample.North.HasValue && !sample.East.HasValue, "pole heading explicitly unavailable");
        sample = Sample(torus, new Vector3d(0, 500, 0), -y);
        Assert(sample.Location == SolidLocation.Outside && !sample.Nearest.HasValue, "axis nearest ambiguous but empty");
        Assert(sample.GroundStatus == GroundQueryStatus.Miss && !sample.GroundClearance.HasValue, "axial hole has no invented ground");
        sample = Sample(torus, new Vector3d(), new Vector3d(1, 0, 0));
        Assert(!sample.Nearest.HasValue && sample.Ground.HasValue, "ambiguous nearest does not suppress unique ray contact");
        Near(sample.GroundClearance.Value, 800, 1e-5, "axis horizontal ground contact");
        sample = Sample(torus, new Vector3d(1250, 0, 0), new Vector3d());
        Assert(sample.GroundStatus == GroundQueryStatus.NoGravity && !sample.Up.HasValue
            && !sample.North.HasValue && !sample.GroundClearance.HasValue && sample.Nearest.HasValue, "zero gravity preserves geometry only");
        sample = Sample(torus, new Vector3d(1000, 0, 0), new Vector3d(-1, 0, 0));
        Assert(sample.Location == SolidLocation.Inside && sample.GroundStatus == GroundQueryStatus.InsideSolid && !sample.Ground.HasValue, "buried query never labels an exit as ground");
        sample = Sample(torus, new Vector3d(1200, 0, 0), new Vector3d(-1, 0, 0));
        Assert(sample.GroundStatus == GroundQueryStatus.OnBoundary && !sample.GroundClearance.HasValue, "boundary requires contact handling");

        sample = Sample(sphere, new Vector3d(250, 0, 0), new Vector3d(-.8, -.6, 0));
        Near(sample.GroundClearance.Value, 200 - Math.Sqrt(17500), 1e-5, "independent sphere quadratic oracle");
        Near(sample.SignedDistance, 50, 1e-12, "oblique distance differs from nearest");
        sample = Sample(sphere, new Vector3d(200, -100, 0), y);
        Assert(sample.GroundStatus == GroundQueryStatus.Tangent && !sample.Ground.HasValue, "grazing ray is not support");
        sample = Sample(sphere, new Vector3d(250, 0, 0), y);
        Assert(sample.GroundStatus == GroundQueryStatus.Miss && sample.Nearest.HasValue, "nearest cannot substitute for missing down contact");

        Assert(GeneralSurfaceQuery.TrySample(torus, new Vector3d(1250, 0, 0), new Vector3d(-1, 0, 0), y,
            new GeneralQuerySettings(1000, 40, 1e-5, 4096), out sample), "bounded range valid");
        Assert(sample.GroundStatus == GroundQueryStatus.Miss && !sample.GroundClearance.HasValue, "out of range contact absent");
        Assert(GeneralSurfaceQuery.TrySample(torus, new Vector3d(1250, 0, 0), new Vector3d(-1, 0, 0), y,
            new GeneralQuerySettings(1000, 5000, 1e-5, 1), out sample), "partial context under exhausted ray budget");
        Assert(sample.GroundStatus == GroundQueryStatus.BudgetExceeded && !sample.Ground.HasValue && sample.Nearest.HasValue, "budget failure has no partial ground");
        sample = Sample(new PartialGeneralGeometry(), new Vector3d(1250, 0, 0), new Vector3d(-1, 0, 0));
        Assert(sample.GroundStatus == GroundQueryStatus.BudgetExceeded && !sample.Ground.HasValue, "even returned partial provider hits are withheld");
        sample = Sample(new PartialGeneralGeometry((GeometryQueryStatus)99), new Vector3d(1250, 0, 0), new Vector3d(-1, 0, 0));
        Assert(sample.GroundStatus == GroundQueryStatus.Indeterminate && !sample.Ground.HasValue, "unknown provider status fails closed");
        Assert(GeneralSurfaceQuery.TrySample(new AnalyticSolid(1000, .001), new Vector3d(1001, 0, 0), new Vector3d(-1, 0, 0), y,
            new GeneralQuerySettings(1000, 5000, 1e-8, 4096), out sample), "thin geometry partial context");
        Assert(sample.GroundStatus == GroundQueryStatus.Indeterminate && !sample.Ground.HasValue, "unsupported ray conditioning explicit");
        Assert(GeneralSurfaceQuery.TrySample(torus, new Vector3d(1250, 0, 0), new Vector3d(-1, 0, 0), y,
            new GeneralQuerySettings(1000, 5000, 3, 4096), out sample), "provider tolerance failure retains partial context");
        Assert(sample.GroundStatus == GroundQueryStatus.InvalidInput && !sample.Ground.HasValue, "provider rejects excessive tolerance");
        Assert(!GeneralSurfaceQuery.TrySample(null, new Vector3d(), y, y, settings, out _), "null provider rejected");
        Assert(!GeneralSurfaceQuery.TrySample(torus, new Vector3d(double.NaN, 0, 0), y, y, settings, out _), "invalid position rejected");
        Assert(!GeneralSurfaceQuery.TrySample(torus, new Vector3d(), new Vector3d(double.PositiveInfinity, 0, 0), y, settings, out _), "invalid force rejected");
        Assert(!GeneralSurfaceQuery.TrySample(torus, new Vector3d(), y, new Vector3d(), settings, out _), "undefined heading axis rejected");
        Assert(!GeneralSurfaceQuery.TrySample(torus, new Vector3d(), y, y, default(GeneralQuerySettings), out _), "default settings rejected");

        var random = new Random(20261026);
        for (int i = 0; i < 1200; i++)
        {
            // Independent circular-tube oracle, not projection/raycast output reused as expected data.
            double u = random.NextDouble() * 2 * Math.PI, v = random.NextDouble() * 2 * Math.PI;
            double offset = 5 + random.NextDouble() * 75;
            var normal = new Vector3d(Math.Cos(u) * Math.Cos(v), Math.Sin(v), Math.Sin(u) * Math.Cos(v));
            var point = new Vector3d((1000 + 200 * Math.Cos(v)) * Math.Cos(u), 200 * Math.Sin(v), (1000 + 200 * Math.Cos(v)) * Math.Sin(u));
            sample = Sample(torus, point + normal * offset, normal * -(1 + random.NextDouble() * 10));
            Assert(sample.GroundStatus == GroundQueryStatus.Contact, "random normal ray has first boundary");
            Near(sample.GroundClearance.Value, offset, 4e-5, "random ray independent clearance");
            Near(sample.SignedDistance, offset, 1e-9, "random signed distance");
            VectorNear(sample.Ground.Value.Point, point, 4e-5, "random first point");
            VectorNear(sample.Nearest.Value.Point, point, 1e-9, "random nearest point");
            VectorNear(sample.Up.Value, normal, 1e-12, "field magnitude does not affect vertical");
            VectorNear(sample.Ground.Value.Normal, normal, 3e-7, "random ground normal");
        }
    }

    private sealed class PartialGeneralGeometry : IGeneralSurfaceGeometry
    {
        private readonly AnalyticSolid solid = new AnalyticSolid(1000, 200);
        private readonly GeometryQueryStatus status;
        internal PartialGeneralGeometry(GeometryQueryStatus status = GeometryQueryStatus.BudgetExceeded) { this.status = status; }
        public double BoundingRadius => solid.BoundingRadius;
        public double SignedDistance(Vector3d p) => solid.SignedDistance(p);
        public SolidLocation Classify(Vector3d p, double t) => solid.Classify(p, t);
        public bool TryNearest(Vector3d p, out SurfaceSample s) => solid.TryNearest(p, out s);
        public GeometryQueryStatus RaycastAll(Vector3d p, Vector3d d, double max, double tolerance, int budget, out SolidRayHit[] hits)
        {
            solid.RaycastAll(p, d, max, tolerance, 4096, out hits);
            return status;
        }
    }
}
