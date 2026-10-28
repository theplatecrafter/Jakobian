using System;
using Jakobian.Core;

internal static partial class Program
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
        SolidChecks();
        MapChecks();
        StreamingChecks();
        GeneralContextChecks();
        WorldDescriptorChecks();
        TorusPreviewChecks();
        AdmissionChecks();
        Console.WriteLine("PASS: " + checks + " analytic geometry and frame checks. No KSP flight validation implied.");
    }

    private static void MapChecks()
    {
        var bytes = new byte[4*3*4];
        for (int y=0;y<3;y++) for (int x=0;x<4;x++)
        {
            byte value = (byte)(y==0 ? 0 : y==2 ? 255 : 10+x*40);
            int i=(y*4+x)*4; bytes[i]=bytes[i+1]=bytes[i+2]=value; bytes[i+3]=255;
        }
        var map = new RadialMap(4,3,bytes);
        Vector3d Direction(double u, double v) => new Vector3d(Math.Cos((v-.5)*Math.PI)*Math.Cos(u*2*Math.PI),Math.Sin((v-.5)*Math.PI),Math.Cos((v-.5)*Math.PI)*Math.Sin(u*2*Math.PI));
        Near(map.Bilinear(Direction(.125,.5)).R,30.0/255,1e-12,"map independent interpolation oracle");
        Near(map.Bilinear(Direction(.875,.5)).R,70.0/255,1e-12,"map interpolates across periodic seam");
        Near(map.Bilinear(new Vector3d(1,0,-1e-20)).R,10.0/255,1e-12,"rounded negative seam wraps to first column");
        for (int y=0;y<=64;y++) for(int x=0;x<=128;x++)
        {
            MapColor color=map.Bilinear(Direction((double)x/128,(double)y/64));
            Assert(color.R>=0 && color.R<=1,"complete scaled grid stays within sampler bounds");
        }
        Assert(map.Nearest(Direction(.20,.5))==0x323232ff,"biomes use nearest palette, never blended colors");
        Near(map.Bilinear(Direction(0,.25)).R,5.0/255,1e-12,"latitude interpolation includes pole endpoints");
        bytes[0]=200;
        Near(map.Bilinear(new Vector3d(0,-1,0)).R,0,0,"map owns a defensive copy");
        bool rejected=false;
        try { new RadialMap(4,3,bytes); } catch(ArgumentException) { rejected=true; }
        Assert(rejected,"conflicting pole longitudes rejected");
        var shape = new RadialShape(RadialShapeKind.Ellipsoid,9000,5500,7000);
        var terrain = new MappedRadialSurface(shape,map,-50,600);
        rejected=false;
        try { new MappedRadialSurface(shape,null,0,1); } catch(ArgumentException) { rejected=true; }
        Assert(rejected,"nonzero displacement needs map");
        rejected=false;
        try { new MappedRadialSurface(shape,map,0,2000); } catch(ArgumentException) { rejected=true; }
        Assert(rejected,"displacement cannot exceed conservative radial bounds");
        for (int i=0;i<360;i++)
        {
            double u=(i+.123)/360, v=.05+.9*((i*37)%359)/359;
            Vector3d direction=Direction(u,v);
            double radius=shape.RadiusAt(direction)-50+600*map.Bilinear(direction).R;
            Assert(terrain.TrySample(direction*(radius+42),shape.MinimumRadius,out var sample),"mapped radial context");
            Near(sample.Clearance,42,1e-8,"mapped ground includes height displacement");
            Near(sample.ReferenceAltitude,radius+42-shape.MinimumRadius,1e-8,"terrain does not move native datum");
            Assert(radius>=terrain.MinimumRadius && radius<=terrain.BoundingRadius,"mapped bounds contain terrain");
            Vector3d lonTangent=terrain.SurfacePoint(Direction(u+1e-6,v))-terrain.SurfacePoint(Direction(u-1e-6,v));
            Vector3d latTangent=terrain.SurfacePoint(Direction(u,v+1e-6))-terrain.SurfacePoint(Direction(u,v-1e-6));
            Vector3d expected=Vector3d.Cross(latTangent,lonTangent); expected/=expected.Length;
            VectorNear(sample.Normal,expected,2e-5,"mapped normal independent longitude/latitude stencil");
            Near(map.Bilinear(Direction(u+1,v)).R,map.Bilinear(direction).R,1e-12,"periodic longitude wrap");
        }
        Assert(!terrain.TrySample(new Vector3d(),5500,out _),"mapped centre remains undefined");
    }

    private static void SolidChecks()
    {
        var torus = new AnalyticSolid(1000, 200);
        var sphere = new AnalyticSolid(500);
        Assert(torus.Classify(new Vector3d(), 1e-6) == SolidLocation.Outside, "torus centre is empty");
        Assert(torus.Classify(new Vector3d(1000, 0, 0), 1e-6) == SolidLocation.Inside, "torus tube is solid");
        SolidRayHit[] hits;
        Assert(torus.RaycastAll(new Vector3d(-2000, 0, 0), new Vector3d(1, 0, 0), 4000, 1e-5, 2048, out hits) == GeometryQueryStatus.Complete && hits.Length == 4, "four torus crossings");
        for (int i = 0; i < 4; i++)
        {
            Near(hits[i].Distance, new[] { 800d, 1200, 2800, 3200 }[i], 1e-5, "torus crossing oracle");
            Assert(hits[i].Crossing == (i % 2 == 0 ? BoundaryCrossing.Enter : BoundaryCrossing.Exit), "entry exit orientation");
        }
        Assert(torus.RaycastAll(new Vector3d(0, -2000, 0), new Vector3d(0, 1, 0), 4000, 1e-5, 2048, out hits) == GeometryQueryStatus.Complete && hits.Length == 0, "hole transit has no ground hit");
        Assert(torus.RaycastAll(new Vector3d(1200, -500, 0), new Vector3d(0, 1, 0), 1000, 1e-5, 2048, out hits) == GeometryQueryStatus.Complete && hits.Length == 1 && hits[0].Crossing == BoundaryCrossing.Touch, "torus tangent touch");
        Assert(torus.RaycastAll(new Vector3d(-2000, 0, 0), new Vector3d(1, 0, 0), 4000, 1e-5, 1, out hits) == GeometryQueryStatus.BudgetExceeded && hits.Length == 0, "budget failure exposes no partial hit set");
        Assert(sphere.RaycastAll(new Vector3d(), new Vector3d(4, 0, 0), 1000, 1e-5, 512, out hits) == GeometryQueryStatus.Complete && hits.Length == 1, "inside sphere ray");
        Near(hits[0].Distance, 500, 1e-5, "direction normalized into metre distances");
        Assert(hits[0].Crossing == BoundaryCrossing.Exit, "inside sphere exit");
        Assert(torus.RaycastAll(new Vector3d(), new Vector3d(), 1000, 1e-5, 512, out hits) == GeometryQueryStatus.InvalidInput, "invalid ray direction");
        Assert(torus.RaycastAll(new Vector3d(-1e11, 0, 0), new Vector3d(1, 0, 0), 2e11, 1e-5, 2048, out hits) == GeometryQueryStatus.Indeterminate && hits.Length == 0, "unresolvable coordinate precision explicitly rejected");
        var tinyHole = new AnalyticSolid(1000, 999.999999);
        Assert(tinyHole.RaycastAll(new Vector3d(-3000, 0, 0), new Vector3d(1, 0, 0), 6000, 1e-5, 2048, out hits) == GeometryQueryStatus.Indeterminate && hits.Length == 0, "unresolvable hole is not silently closed by merging crossings");
        var random = new Random(441023);
        for (int i = 0; i < 2000; i++)
        {
            double y = random.NextDouble() * 600 - 300, z = random.NextDouble() * 3000 - 1500;
            var expected = new System.Collections.Generic.List<double>();
            if (Math.Abs(y) < 200)
            {
                double tube = Math.Sqrt(200 * 200 - y * y), outer = 1000 + tube, inner = 1000 - tube;
                if (Math.Abs(z) < outer)
                {
                    double x = Math.Sqrt(outer * outer - z * z); expected.Add(2000 - x); expected.Add(2000 + x);
                    if (Math.Abs(z) < inner) { x = Math.Sqrt(inner * inner - z * z); expected.Add(2000 - x); expected.Add(2000 + x); }
                }
            }
            expected.Sort();
            Assert(torus.RaycastAll(new Vector3d(-2000, y, z), new Vector3d(1, 0, 0), 4000, 1e-5, 2048, out hits) == GeometryQueryStatus.Complete && hits.Length == expected.Count, "independent annular cross-section crossing count");
            for (int j = 0; j < hits.Length; j++) Near(hits[j].Distance, expected[j], 1e-5, "annular cross-section intersection oracle");
        }
        for (int i = 0; i < 1000; i++)
        {
            double u = random.NextDouble() * Math.PI * 2, v = .1 + random.NextDouble() * (Math.PI - .2);
            double radial = 1000 + 200 * Math.Cos(v), y = 200 * Math.Sin(v);
            var origin = new Vector3d(radial * Math.Cos(u), -500, radial * Math.Sin(u));
            Assert(torus.RaycastAll(origin, new Vector3d(0, 7, 0), 1000, 1e-5, 2048, out hits) == GeometryQueryStatus.Complete && hits.Length == 2, "parametric vertical tube ray");
            Near(hits[0].Distance, 500 - y, 1e-5, "parametric tube lower crossing");
            Near(hits[1].Distance, 500 + y, 1e-5, "parametric tube upper crossing");
            foreach (var hit in hits)
            {
                Near(torus.SignedDistance(hit.Point), 0, 1e-5, "ray boundary oracle");
                VectorNear(hit.Point, origin + new Vector3d(0, 1, 0) * hit.Distance, 1e-7, "ray distance reconstructs point");
            }
        }
        const int tu = 8, tv = 4, n = 8;
        var edges = new System.Collections.Generic.Dictionary<string, int>();
        for (int u = 0; u < tu; u++) for (int v = 0; v < tv; v++)
        {
            var patch = TorusMeshBaker.Build(1000, 200, u, v, tu, tv, n);
            var next = TorusMeshBaker.Build(1000, 200, (u + 1) % tu, v, tu, tv, n);
            for (int j = 0; j <= n; j++) VectorNear(patch.Vertices[n * (n + 1) + j] + patch.Anchor, next.Vertices[j] + next.Anchor, 1e-10, "periodic patch U seam");
            next = TorusMeshBaker.Build(1000, 200, u, (v + 1) % tv, tu, tv, n);
            for (int j = 0; j <= n; j++) VectorNear(patch.Vertices[j * (n + 1) + n] + patch.Anchor, next.Vertices[j * (n + 1)] + next.Anchor, 1e-10, "periodic patch V seam");
            for (int k = 0; k < patch.Vertices.Length; k++) Near(torus.SignedDistance(patch.Vertices[k] + patch.Anchor), 0, 1e-10, "baked vertex on exact boundary");
            for (int k = 0; k < patch.Triangles.Length; k += 3)
            {
                int a = patch.Triangles[k], b = patch.Triangles[k + 1], c = patch.Triangles[k + 2];
                Vector3d normal = Vector3d.Cross(patch.Vertices[b] - patch.Vertices[a], patch.Vertices[c] - patch.Vertices[a]);
                Assert(Vector3d.Dot(normal, patch.Normals[a]) > 0, "torus triangles face out of solid, including inner wall");
                int[] corners = { a, b, c };
                for (int e = 0; e < 3; e++)
                {
                    int id1 = Weld(corners[e]), id2 = Weld(corners[(e + 1) % 3]);
                    string key = Math.Min(id1, id2) + ":" + Math.Max(id1, id2);
                    edges.TryGetValue(key, out int count); edges[key] = count + 1;
                }
                int Weld(int index) => ((u * n + index / (n + 1)) % (tu * n)) * (tv * n) + ((v * n + index % (n + 1)) % (tv * n));
            }
        }
        foreach (int count in edges.Values) Assert(count == 2, "watertight welded edge has two incident triangles");
        Assert(tu * tv * n * n - edges.Count + tu * tv * n * n * 2 == 0, "closed torus Euler characteristic zero");
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
