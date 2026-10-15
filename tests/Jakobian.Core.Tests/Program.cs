using System;
using Jakobian.Core;

internal static class Program
{
    private static int checks;
    private static void Assert(bool pass, string name) { checks++; if (!pass) throw new Exception(name); }
    private static void Near(double a, double b, double tolerance, string name) => Assert(Math.Abs(a - b) <= tolerance, name + ": " + a + " != " + b);
    private static void VectorNear(Vector3d a, Vector3d b, double tolerance, string name) => Near((a - b).Length, 0, tolerance, name);
    public static void Main()
    {
        var sphere = new SphereSurface(600000);
        Assert(!sphere.TryNearest(new Vector3d(), out _), "sphere centre has no unique normal");
        Assert(!sphere.TryNearest(new Vector3d(double.NaN, 0, 0), out _), "NaN rejected");
        var torus = new TorusSurface(1000000, 10000);
        Assert(!torus.TryNearest(new Vector3d(0, 5, 0), out _), "torus axis nearest point nonunique");
        Assert(!torus.TryNearest(new Vector3d(1000000, 0, 0), out _), "torus tube centre nonunique");
        Assert(torus.TryNearest(new Vector3d(500, 0, 0), out var hole), "hole sample");
        Assert(hole.SignedDistance > 0, "hole is empty space");
        VectorNear(hole.Normal, new Vector3d(-1, 0, 0), 1e-12, "inner torus normal faces hole");
        Assert(torus.TryNearest(new Vector3d(1000000, 100, 0), out var solid), "solid sample");
        Assert(solid.SignedDistance < 0, "tube interior is solid");
        var random = new Random(20261014);
        for (int i = 0; i < 2000; i++)
        {
            double u = random.NextDouble() * 2 * Math.PI;
            double v = (random.NextDouble() - .5) * Math.PI;
            double altitude = random.NextDouble() * 2000 - 1000;
            var n = new Vector3d(Math.Cos(u) * Math.Cos(v), Math.Sin(v), Math.Sin(u) * Math.Cos(v));
            var p = n * (sphere.Radius + altitude);
            Assert(sphere.TryNearest(p, out var sample), "sphere query");
            Near(sample.SignedDistance, altitude, 1e-8, "sphere signed altitude");
            VectorNear(sample.Point, n * sphere.Radius, 1e-8, "sphere projection");
            v = random.NextDouble() * 2 * Math.PI;
            var centre = new Vector3d(Math.Cos(u) * torus.MajorRadius, 0, Math.Sin(u) * torus.MajorRadius);
            n = new Vector3d(Math.Cos(u) * Math.Cos(v), Math.Sin(v), Math.Sin(u) * Math.Cos(v));
            p = centre + n * (torus.MinorRadius + altitude);
            Assert(torus.TryNearest(p, out sample), "torus query");
            Near(sample.SignedDistance, altitude, 1e-8, "torus signed altitude");
            VectorNear(sample.Point, centre + n * torus.MinorRadius, 1e-8, "torus projection");
            VectorNear(sample.Normal, n, 1e-10, "torus orientation");
            double major = torus.MajorRadius + torus.MinorRadius * Math.Cos(sample.V * 2 * Math.PI);
            var reconstructed = new Vector3d(major * Math.Cos(sample.U * 2 * Math.PI), torus.MinorRadius * Math.Sin(sample.V * 2 * Math.PI), major * Math.Sin(sample.U * 2 * Math.PI));
            VectorNear(reconstructed, sample.Point, 1e-8, "chart roundtrip");
        }
        var zero = new Vector3d();
        var omega = new Vector3d(0, .01, 0);
        var radius = new Vector3d(100000, 0, 0);
        VectorNear(ReferencePhysics.RotatingAcceleration(zero, zero, omega, zero, radius, zero), new Vector3d(10, 0, 0), 1e-12, "centrifugal outward, not inward");
        VectorNear(ReferencePhysics.RotatingAcceleration(zero, zero, omega, zero, radius, new Vector3d(0, 0, 100)), new Vector3d(8, 0, 0), 1e-12, "Coriolis sign");
        VectorNear(ReferencePhysics.RotatingAcceleration(zero, zero, zero, new Vector3d(0, .01, 0), radius, zero), new Vector3d(0, 0, 1000), 1e-12, "Euler sign");
        Assert(ReferencePhysics.TryPointGravity(new Vector3d(600000, 0, 0), 3.5316e12, out var gravity), "gravity finite");
        Near(gravity.X, -9.81, 1e-12, "Kerbin reference gravity");
        Assert(!ReferencePhysics.TryPointGravity(zero, 1, out _), "point singularity rejected");
        RadialChecks();
        SurfaceContextChecks();
        Console.WriteLine("PASS: " + checks + " analytic geometry and frame checks. No KSP flight validation implied.");
    }

    private static void SurfaceContextChecks()
    {
        var shapes = new[] { new RadialShape(RadialShapeKind.Sphere, 5000, 5000, 5000),
            new RadialShape(RadialShapeKind.Ellipsoid, 12000, 4000, 6000),
            new RadialShape(RadialShapeKind.Superellipsoid, 6000, 6000, 6000, 8),
            new RadialShape(RadialShapeKind.Peanut, 7000, 7000, 7000, 2, .5) };
        var random = new Random(80317);
        foreach (var shape in shapes)
        {
            for (int i = 0; i < 1000; i++)
            {
                double latitude = (random.NextDouble() - .5) * Math.PI * .99, longitude = (random.NextDouble() * 2 - 1) * Math.PI;
                var d = new Vector3d(Math.Cos(latitude) * Math.Cos(longitude), Math.Sin(latitude), Math.Cos(latitude) * Math.Sin(longitude));
                var point = shape.SurfacePoint(d);
                double clearance = random.NextDouble() * 1000 - 500;
                Assert(RadialSurfaceQuery.TrySample(shape, point + d * clearance, shape.MinimumRadius, out var sample), "surface context available");
                Near(sample.Clearance, clearance, 1e-8, "signed radial clearance");
                Near(sample.ReferenceAltitude - sample.Clearance, point.Length - shape.MinimumRadius, 1e-8, "datum and ground are distinct");
                Near(sample.LatitudeDegrees, latitude * 180 / Math.PI, 1e-9, "latitude coordinate oracle");
                Near(sample.LongitudeDegrees, longitude * 180 / Math.PI, 1e-9, "longitude coordinate oracle");
                Assert(sample.HasHeading, "heading away from poles");
                Near(sample.Normal.Length, 1, 1e-12, "unit surface normal");
                VectorNear(Vector3d.Cross(sample.RadialUp, sample.North), sample.East, 1e-12, "right handed compass");
                Near(Vector3d.Dot(sample.North, sample.RadialUp), 0, 1e-12, "north tangent to gravity horizontal");
                // Independent finite-difference tangents to the actual geometric boundary.
                const double h = 1e-5;
                var t1 = shape.SurfacePoint(d + sample.North * h) - shape.SurfacePoint(d - sample.North * h);
                var t2 = shape.SurfacePoint(d + sample.East * h) - shape.SurfacePoint(d - sample.East * h);
                Near(Vector3d.Dot(t1 / t1.Length, sample.Normal), 0, 1e-7, "normal orthogonal to meridian boundary tangent");
                Near(Vector3d.Dot(t2 / t2.Length, sample.Normal), 0, 1e-7, "normal orthogonal to longitude boundary tangent");
                Assert(Vector3d.Dot(sample.Normal, sample.RadialUp) > 0, "normal faces exterior");
            }
            Assert(!RadialSurfaceQuery.TrySample(shape, new Vector3d(), 5000, out _), "centre has no radial projection");
            Assert(!RadialSurfaceQuery.TrySample(shape, new Vector3d(double.NaN, 1, 1), 5000, out _), "nonfinite context rejected");
            Assert(RadialSurfaceQuery.TrySample(shape, new Vector3d(0, 20000, 0), shape.MinimumRadius, out var pole) && !pole.HasHeading, "pole explicitly lacks compass heading");
            VectorNear(pole.North, new Vector3d(), 0, "no invented north at pole");
        }
        var ellipsoid = shapes[1];
        var oblique = new Vector3d(12000 / Math.Sqrt(2), 4000 / Math.Sqrt(2), 0);
        Assert(RadialSurfaceQuery.TrySample(ellipsoid, oblique, 4000, out var slope), "oblique ellipsoid context");
        Assert(slope.SlopeDegrees > 50, "gravity up differs substantially from geometric normal");
    }

    private static void RadialChecks()
    {
        var ellipsoid = new RadialShape(RadialShapeKind.Ellipsoid, 12000, 4000, 6000);
        var cube = new RadialShape(RadialShapeKind.Superellipsoid, 6000, 6000, 6000, 8);
        var peanut = new RadialShape(RadialShapeKind.Peanut, 7000, 7000, 7000, 2, .5);
        Near(ellipsoid.RadiusAt(new Vector3d(1, 0, 0)), 12000, 1e-8, "ellipsoid long axis");
        Near(ellipsoid.RadiusAt(new Vector3d(0, 1, 0)), 4000, 1e-8, "ellipsoid short axis");
        Near(peanut.RadiusAt(new Vector3d(1, 0, 0)), 10500, 1e-8, "peanut tip");
        Near(peanut.RadiusAt(new Vector3d(0, 1, 0)), 3500, 1e-8, "peanut waist");
        var random = new Random(87032);
        for (int i = 0; i < 2000; i++)
        {
            var d = new Vector3d(random.NextDouble() - .5, random.NextDouble() - .5, random.NextDouble() - .5);
            var p = ellipsoid.SurfacePoint(d);
            Near(p.X * p.X / (12000d * 12000) + p.Y * p.Y / (4000d * 4000) + p.Z * p.Z / (6000d * 6000), 1, 1e-10, "ellipsoid implicit oracle");
            p = cube.SurfacePoint(d);
            Near(Math.Pow(Math.Abs(p.X) / 6000, 8) + Math.Pow(Math.Abs(p.Y) / 6000, 8) + Math.Pow(Math.Abs(p.Z) / 6000, 8), 1, 1e-10, "superellipsoid implicit oracle");
            foreach (var shape in new[] { ellipsoid, cube, peanut })
            {
                double radius = shape.RadiusAt(d);
                Assert(radius >= shape.MinimumRadius - 1e-8 && radius <= shape.BoundingRadius + 1e-8, "conservative radial bounds");
                Near(shape.RadiusAt(d * 15), radius, 1e-8, "direction scale invariance");
                Near(shape.RadiusAt(-d), radius, 1e-8, "radial inversion symmetry");
            }
        }
        bool failed = false;
        try { ellipsoid.RadiusAt(new Vector3d()); } catch (ArgumentException) { failed = true; }
        Assert(failed, "zero radial direction rejected");
        failed = false;
        try { new RadialShape(RadialShapeKind.Peanut, 5000, 5000, 5000, 2, 1); } catch (ArgumentException) { failed = true; }
        Assert(failed, "zero-thickness peanut rejected");
    }
}
