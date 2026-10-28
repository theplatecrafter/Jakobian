using System;
using System.Collections.Generic;
using Jakobian.Core;
using UnityEngine;

namespace Jakobian.KSP
{
    internal static class TorusMeshChecks
    {
        internal static void Run()
        {
            var solid = new AnalyticSolid(1000, 200);
            SolidRayHit[] crossings;
            if (solid.Classify(new Jakobian.Core.Vector3d(), 1e-6) != SolidLocation.Outside
                || solid.RaycastAll(new Jakobian.Core.Vector3d(-2000, 0, 0), new Jakobian.Core.Vector3d(1, 0, 0), 4000, 1e-5, 2048, out crossings) != GeometryQueryStatus.Complete
                || crossings.Length != 4) throw new InvalidOperationException("Native Mono torus solid query failed.");
            for (int i = 0; i < 4; i++)
                if (Math.Abs(crossings[i].Distance - new[] { 800d, 1200, 2800, 3200 }[i]) > 1e-5)
                    throw new InvalidOperationException("Native Mono torus crossing distance mismatch.");
            if (solid.RaycastAll(new Jakobian.Core.Vector3d(0, -2000, 0), new Jakobian.Core.Vector3d(0, 1, 0), 4000, 1e-5, 2048, out crossings) != GeometryQueryStatus.Complete || crossings.Length != 0)
                throw new InvalidOperationException("Native Mono torus hole query failed.");
            Debug.Log("[Jakobian] TORUS_NATIVE_QUERY_PASS diameterCrossings=4 holeCrossings=0 runtime=gameMono");
            // Isolated native collider experiment, never an enabled celestial body or player terrain.
            var root = new GameObject("Jakobian torus collider probe");
            root.transform.position = new Vector3(20000, 20000, 20000);
            root.transform.rotation = Quaternion.Euler(20, 35, 10);
            var colliders = new List<MeshCollider>(); var meshes = new List<Mesh>();
            try
            {
                for (int u = 0; u < 8; u++) for (int v = 0; v < 4; v++)
                {
                    SurfaceMeshPatch patch = TorusMeshBaker.Build(1000, 200, u, v, 8, 4, 32);
                    var go = new GameObject("Patch " + u + ":" + v); go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = (Vector3)BodyQueries.Ksp(patch.Anchor);
                    var positions = new Vector3[patch.Vertices.Length]; var normals = new Vector3[positions.Length];
                    for (int i = 0; i < positions.Length; i++)
                    { positions[i] = (Vector3)BodyQueries.Ksp(patch.Vertices[i]); normals[i] = (Vector3)BodyQueries.Ksp(patch.Normals[i]); }
                    var mesh = new Mesh { name = go.name }; mesh.vertices = positions; mesh.normals = normals; mesh.triangles = patch.Triangles; mesh.RecalculateBounds();
                    meshes.Add(mesh);
                    var collider = go.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; colliders.Add(collider);
                }
                Physics.SyncTransforms();
                Check(new Vector3(2000, 0, 0), Vector3.left, 3000, 800, Vector3.right);
                Check(Vector3.zero, Vector3.right, 2000, 800, Vector3.left);
                Check(new Vector3(1000, 1000, 0), Vector3.down, 2000, 800, Vector3.up);
                Check(new Vector3(0, -2000, 0), Vector3.up, 4000, -1, Vector3.zero);
                GeneralContextNativeChecks.Run(root.transform, colliders);
                Debug.Log("[Jakobian] TORUS_NATIVE_MESH_PASS patches=32 inner=contact outer=contact top=contact hole=empty transform=rotated");
            }
            finally
            {
                root.SetActive(false); UnityEngine.Object.Destroy(root);
                foreach (Mesh mesh in meshes) UnityEngine.Object.Destroy(mesh);
            }

            void Check(Vector3 origin, Vector3 direction, float limit, float expected, Vector3 normal)
            {
                var ray = new Ray(root.transform.TransformPoint(origin), root.transform.TransformDirection(direction));
                float closest = float.PositiveInfinity; Vector3 actualNormal = Vector3.zero;
                foreach (MeshCollider collider in colliders)
                    if (collider.Raycast(ray, out RaycastHit hit, limit) && hit.distance < closest) { closest = hit.distance; actualNormal = hit.normal; }
                if (expected < 0) { if (!float.IsPositiveInfinity(closest)) throw new InvalidOperationException("Torus collider closes the hole."); }
                else if (Math.Abs(closest - expected) > 2 || Vector3.Angle(actualNormal, root.transform.TransformDirection(normal)) > 4)
                    throw new InvalidOperationException("Torus collider mismatch: distance=" + closest + " expected=" + expected);
            }
        }
    }
}
