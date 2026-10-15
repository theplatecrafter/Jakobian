using System;
using Jakobian.Core;
using UnityEngine;

namespace Jakobian.KSP
{
    internal static class ScaledShapeMesh
    {
        internal static Mesh Build(MappedRadialSurface surface, bool mapped)
        {
            const int rows = 64, columns = 128;
            var vertices = new Vector3[(rows + 1) * (columns + 1)];
            var uv = new Vector2[vertices.Length];
            var uv3 = new Vector2[vertices.Length]; var uv4 = new Vector2[vertices.Length];
            var indices = new int[rows * columns * 6];
            for (int i = 0; i <= rows; i++) for (int j = 0; j <= columns; j++)
            {
                double latitude = -Math.PI / 2 + Math.PI * i / rows, longitude = 2 * Math.PI * j / columns;
                var direction = new Jakobian.Core.Vector3d(Math.Cos(latitude) * Math.Cos(longitude), Math.Sin(latitude), Math.Cos(latitude) * Math.Sin(longitude));
                var point = surface.SurfacePoint(direction);
                int k = i * (columns + 1) + j;
                vertices[k] = new Vector3((float)(point.X / 6000), (float)(point.Y / 6000), (float)(point.Z / 6000));
                uv[k] = new Vector2((float)j / columns, (float)i / rows);
                uv3[k] = new Vector2((float)direction.X, (float)direction.Y); uv4[k] = new Vector2((float)direction.Z,0);
            }
            int n = 0;
            for (int i = 0; i < rows; i++) for (int j = 0; j < columns; j++)
            {
                int a = i * (columns + 1) + j, b = a + columns + 1;
                indices[n++] = a; indices[n++] = b; indices[n++] = a + 1;
                indices[n++] = a + 1; indices[n++] = b; indices[n++] = b + 1;
            }
            var mesh = new Mesh { name = "Jakobian " + surface.Shape.Kind };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices;
            if (mapped) { mesh.uv3 = uv3; mesh.uv4 = uv4; }
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
