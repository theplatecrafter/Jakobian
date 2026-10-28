using System;
using System.Collections.Generic;
using System.Linq;
using Jakobian.Core;

internal static partial class Program
{
    private static void TorusPreviewChecks()
    {
        TorusBodyDefinition Definition(double major = 9000) => new TorusBodyDefinition("test/torus", "Torus", 22, major, 1500, 5e7, 500000, "Kerbin", 1, 60000000, 4, 0);
        var definition = Definition();
        Near(definition.Geometry.BoundingRadius, 10500, 0, "preview bound is not reference radius");
        Assert(definition.World.Properties.Any(p => p.Key == "capability" && p.Value == "preview-only"), "preview capability fingerprinted");
        var prior = new WorldCatalog(new[] { definition.World });
        Assert(WorldReconciliation.Compare(prior, new WorldCatalog(new[] { Definition(9100).World })).Status == WorldCompatibility.Incompatible, "torus dimension change incompatible");
        foreach (var quality in new[] { new[] { 8, 8 }, new[] { 32, 16 }, new[] { 128, 64 }, new[] { 256, 128 } })
        {
            int uCount = quality[0], vCount = quality[1];
            var mesh = TorusMeshBaker.BuildClosed(9000, 1500, uCount, vCount);
            Assert(mesh.Vertices.Length == uCount * vCount && mesh.Vertices.Length <= 32768 && mesh.Triangles.Length == uCount * vCount * 6, "bounded closed mesh size");
            Near(mesh.Anchor.Length, 0, 0, "whole mesh uses body centre anchor");
            var edges = new Dictionary<long, int>(); var orientations = new Dictionary<long, int>();
            void Edge(int a, int b)
            {
                long key = ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
                edges[key] = edges.TryGetValue(key, out var count) ? count + 1 : 1;
                orientations[key] = (orientations.TryGetValue(key, out var winding) ? winding : 0) + (a < b ? 1 : -1);
            }
            double signedVolume = 0;
            for (int i = 0; i < mesh.Triangles.Length; i += 3)
            {
                int a = mesh.Triangles[i], b = mesh.Triangles[i + 1], c = mesh.Triangles[i + 2];
                Assert(a >= 0 && a < mesh.Vertices.Length && b >= 0 && b < mesh.Vertices.Length && c >= 0 && c < mesh.Vertices.Length, "indices in range");
                Edge(a, b); Edge(b, c); Edge(c, a);
                var cross = Vector3d.Cross(mesh.Vertices[b] - mesh.Vertices[a], mesh.Vertices[c] - mesh.Vertices[a]);
                Assert(cross.Length > 0 && Vector3d.Dot(cross, mesh.Normals[a] + mesh.Normals[b] + mesh.Normals[c]) > 0, "outward nondegenerate triangle");
                signedVolume += Vector3d.Dot(mesh.Vertices[a], Vector3d.Cross(mesh.Vertices[b], mesh.Vertices[c])) / 6;
            }
            Assert(edges.Values.All(n => n == 2) && orientations.Values.All(n => n == 0), "every seam edge has two oppositely directed incident triangles");
            Assert(mesh.Vertices.Length - edges.Count + mesh.Triangles.Length / 3 == 0, "torus Euler characteristic zero");
            Assert(signedVolume > 0, "closed volume outward");
            for (int u = 0; u < uCount; u++) for (int v = 0; v < vCount; v++)
            {
                int i = u * vCount + v; double a = 2 * Math.PI * u / uCount, b = 2 * Math.PI * v / vCount;
                var expected = new Vector3d((9000 + 1500 * Math.Cos(b)) * Math.Cos(a), 1500 * Math.Sin(b), (9000 + 1500 * Math.Cos(b)) * Math.Sin(a));
                VectorNear(mesh.Vertices[i], expected, 1e-9, "independent circular-tube position oracle");
                Near(definition.Geometry.SignedDistance(mesh.Vertices[i]), 0, 1e-9, "mesh vertices on analytic boundary");
                Near(mesh.Normals[i].Length, 1, 1e-12, "analytic normals unit");
                // Every triangle projects into one U cell with radius >= this positive chord bound.
                double rho = Math.Sqrt(mesh.Vertices[i].X * mesh.Vertices[i].X + mesh.Vertices[i].Z * mesh.Vertices[i].Z);
                Assert(rho >= 7500 - 1e-9 && 7500 * Math.Cos(Math.PI / uCount) > 0, "hole remains open at all mesh qualities");
            }
            Assert(definition.World.Fingerprint == Definition().World.Fingerprint, "retessellation does not change world descriptor");
        }
        bool rejected;
        void Reject(Action action, string name) { rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Assert(rejected, name); }
        Reject(() => TorusMeshBaker.BuildClosed(9000, 1500, 7, 64), "too few preview segments rejected");
        Reject(() => TorusMeshBaker.BuildClosed(9000, 1500, 257, 64), "excessive preview segments rejected");
        Reject(() => Definition(1400), "self-intersecting torus rejected");
        Reject(() => new TorusBodyDefinition("test/torus", "Torus", 22, 1e10, 1500, 5e7, 5e14, "Kerbin", 1, 6e14, 4, 0), "precision/extent limits rejected");
        Reject(() => new TorusBodyDefinition("test/torus", "Torus", 22, 9000, 1500, double.NaN, 500000, "Kerbin", 1, 60000000, 4, 0), "nonfinite dynamics rejected");
    }
}
