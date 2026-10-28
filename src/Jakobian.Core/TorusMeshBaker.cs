using System;

namespace Jakobian.Core
{
    public sealed class SurfaceMeshPatch
    {
        public Vector3d Anchor { get; }
        public Vector3d[] Vertices { get; }
        public Vector3d[] Normals { get; }
        public int[] Triangles { get; }
        internal SurfaceMeshPatch(Vector3d anchor, Vector3d[] vertices, Vector3d[] normals, int[] triangles)
        { Anchor = anchor; Vertices = vertices; Normals = normals; Triangles = triangles; }
    }

    public static class TorusMeshBaker
    {
        /// <summary>Watertight shared-index whole surface in local metres; no UV texture-atlas contract.</summary>
        public static SurfaceMeshPatch BuildClosed(double major, double minor, int segmentsU = 128, int segmentsV = 64)
        {
            var solid = new AnalyticSolid(major, minor);
            if (segmentsU < 8 || segmentsU > 256 || segmentsV < 8 || segmentsV > 128)
                throw new ArgumentOutOfRangeException(nameof(segmentsU));
            var vertices = new Vector3d[segmentsU * segmentsV]; var normals = new Vector3d[vertices.Length];
            for (int u = 0; u < segmentsU; u++) for (int v = 0; v < segmentsV; v++)
            {
                double a = 2 * Math.PI * u / segmentsU, b = 2 * Math.PI * v / segmentsV;
                vertices[u * segmentsV + v] = Position(major, minor, a, b);
                normals[u * segmentsV + v] = new Vector3d(Math.Cos(a) * Math.Cos(b), Math.Sin(b), Math.Sin(a) * Math.Cos(b));
            }
            var triangles = new int[vertices.Length * 6]; int k = 0;
            for (int u = 0; u < segmentsU; u++) for (int v = 0; v < segmentsV; v++)
            {
                int a = u * segmentsV + v, b = ((u + 1) % segmentsU) * segmentsV + v;
                int c = u * segmentsV + (v + 1) % segmentsV, d = ((u + 1) % segmentsU) * segmentsV + (v + 1) % segmentsV;
                triangles[k++] = a; triangles[k++] = c; triangles[k++] = b;
                triangles[k++] = b; triangles[k++] = c; triangles[k++] = d;
            }
            return new SurfaceMeshPatch(new Vector3d(), vertices, normals, triangles);
        }
        public static SurfaceMeshPatch Build(double major, double minor, int tileU, int tileV, int tilesU, int tilesV, int segments)
        {
            var solid = new AnalyticSolid(major, minor);
            if (tilesU < 3 || tilesU > 256 || tilesV < 3 || tilesV > 256 || segments < 2 || segments > 128
                || tileU < 0 || tileU >= tilesU || tileV < 0 || tileV >= tilesV) throw new ArgumentOutOfRangeException(nameof(segments));
            Vector3d anchor = Position(major, minor, 2 * Math.PI * (tileU + .5) / tilesU, 2 * Math.PI * (tileV + .5) / tilesV);
            int count = (segments + 1) * (segments + 1);
            var vertices = new Vector3d[count]; var normals = new Vector3d[count];
            for (int i = 0; i <= segments; i++) for (int j = 0; j <= segments; j++)
            {
                // Shared integer grid wraps at exactly the same sample on either side of a seam.
                double u = 2 * Math.PI * ((tileU * segments + i) % (tilesU * segments)) / (tilesU * segments);
                double v = 2 * Math.PI * ((tileV * segments + j) % (tilesV * segments)) / (tilesV * segments);
                int k = i * (segments + 1) + j;
                vertices[k] = Position(major, minor, u, v) - anchor;
                normals[k] = new Vector3d(Math.Cos(u) * Math.Cos(v), Math.Sin(v), Math.Sin(u) * Math.Cos(v));
            }
            var triangles = new int[segments * segments * 6]; int n = 0;
            for (int i = 0; i < segments; i++) for (int j = 0; j < segments; j++)
            {
                int a = i * (segments + 1) + j, b = a + segments + 1;
                triangles[n++] = a; triangles[n++] = a + 1; triangles[n++] = b;
                triangles[n++] = b; triangles[n++] = a + 1; triangles[n++] = b + 1;
            }
            return new SurfaceMeshPatch(anchor, vertices, normals, triangles);
        }
        private static Vector3d Position(double major, double minor, double u, double v) => new Vector3d(
            (major + minor * Math.Cos(v)) * Math.Cos(u), minor * Math.Sin(v), (major + minor * Math.Cos(v)) * Math.Sin(u));
    }
}
