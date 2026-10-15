using System;
using System.Collections.Generic;
using System.IO;
using Jakobian.Core;
using UnityEngine;

namespace Jakobian.KSP
{
    internal static class MappedSurfaceChecks
    {
        internal static void ContactMeshes(CelestialBody body)
        {
            int checkedMeshes = 0;
            foreach (MeshFilter filter in body.pqsController.GetComponentsInChildren<MeshFilter>())
            {
                Renderer renderer = filter.GetComponent<Renderer>(); Mesh mesh = filter.sharedMesh;
                if (filter.GetComponent<PQ>() == null || renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || mesh == null || mesh.vertexCount < 3) continue;
                Vector2[] xy = mesh.uv3, z = mesh.uv4;
                var material = renderer.sharedMaterial;
                Debug.Log("[Jakobian] MAPPED_RENDER_TRACE mesh=" + mesh.name + " shader=" + material.shader.name
                    + " tex=" + material.GetTexture("_MainTex")?.name + " vertices=" + mesh.vertexCount + " xy=" + xy.Length + " z=" + z.Length
                    + " firstXY=" + (xy.Length == 0 ? "missing" : xy[0].ToString("F5")) + " firstZ=" + (z.Length == 0 ? "missing" : z[0].ToString("F5"))
                    + " shaderMaxLOD=" + Shader.globalMaximumLOD + " materialMaxLOD=" + material.shader.maximumLOD);
                if (material.shader.name != "Jakobian/MappedTerrain" || xy.Length != mesh.vertexCount || z.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Mapped contact mesh material/direction channels missing.");
                for (int i=0;i<xy.Length;i+=Math.Max(1,xy.Length/8))
                    if (Math.Abs(new Vector3(xy[i].x,xy[i].y,z[i].x).magnitude-1) > .001)
                        throw new InvalidOperationException("Mapped contact mesh direction is not a unit vector.");
                if (++checkedMeshes == 8) break;
            }
            if (checkedMeshes == 0) throw new InvalidOperationException("No mapped contact meshes available.");
            Debug.Log("[Jakobian] MAPPED_CONTACT_MESH_PASS meshes=" + checkedMeshes);
        }
        internal static void RenderAlbedo(CelestialBody body)
        {
            GameObject root = null; Material material = null; RenderTexture target = null; Texture2D capture = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                root = new GameObject("Jakobian isolated mapped render check"); root.transform.position = new Vector3(30000,40000,50000);
                var model = new GameObject("Mapped fixture"); model.transform.SetParent(root.transform,false); model.layer = 31;
                model.AddComponent<MeshFilter>().sharedMesh = body.scaledBody.GetComponent<MeshFilter>().sharedMesh;
                material = new Material(body.pqsController.surfaceMaterial); material.SetFloat("_Opacity",1); material.SetFloat("_PlanetOpacity",0);
                model.AddComponent<MeshRenderer>().sharedMaterial = material;
                var lamp = new GameObject("Fixture light"); lamp.transform.SetParent(root.transform,false); lamp.layer = 31;
                lamp.transform.rotation = Quaternion.Euler(45,-35,0);
                Light light = lamp.AddComponent<Light>(); light.type = LightType.Directional; light.color = Color.white; light.intensity = 1.2f; light.cullingMask = 1 << 31;
                var eye = new GameObject("Fixture camera"); eye.transform.SetParent(root.transform,false);
                eye.transform.localPosition = new Vector3(3,2,4); eye.transform.LookAt(model.transform);
                Camera camera = eye.AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.fieldOfView = 40; camera.nearClipPlane = .1f; camera.farClipPlane = 20;
                target = new RenderTexture(512,512,24,RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target; camera.Render();
                RenderTexture.active = target; capture = new Texture2D(512,512,TextureFormat.RGBA32,false);
                capture.ReadPixels(new Rect(0,0,512,512),0,0); capture.Apply();
                string directory = Path.Combine(KSPUtil.ApplicationRootPath,".dev","screenshots"); Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory,body.bodyName+"-overview.png"),ImageConversion.EncodeToPNG(capture));
                int warm=0,cool=0,neutral=0;
                foreach(Color32 color in capture.GetPixels32())
                {
                    if(color.a < 128) continue;
                    if(color.r > color.g*1.25 && color.g > color.b*1.2) warm++;
                    if(color.b > color.r*1.1 && color.g > color.r*1.05) cool++;
                    int max=Math.Max(color.r,Math.Max(color.g,color.b)), min=Math.Min(color.r,Math.Min(color.g,color.b));
                    if(max > 50 && max-min < max*.12) neutral++;
                }
                Debug.Log("[Jakobian] MAPPED_GPU_TRACE warm="+warm+" cool="+cool+" neutral="+neutral);
                if(warm < 50 || cool < 10 || neutral < 10) throw new InvalidOperationException("GPU albedo palette regions were not rendered.");
                Debug.Log("[Jakobian] MAPPED_GPU_PASS isolated=scaled-mesh albedo=palette-regions");
            }
            finally
            {
                RenderTexture.active = previous;
                if(root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
                if(material != null) UnityEngine.Object.Destroy(material);
                if(capture != null) UnityEngine.Object.Destroy(capture);
                if(target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            }
        }
        internal static void Run(CelestialBody body, BodyDefinition definition)
        {
            if (!(body.BiomeMap is RadialBiomeMap)) throw new InvalidOperationException("Biome map bridge lost during native cloning.");
            var reached = new HashSet<string>();
            for (int lat=-85; lat<=85; lat+=5) for (int lon=-180; lon<180; lon+=5)
            {
                global::Vector3d world = body.GetWorldSurfacePosition(lat,lon,20000);
                BodyPointSample context;
                if (BodyQueries.Sample(body,world,out context) != BodyQueryStatus.Available) throw new InvalidOperationException("Mapped context unavailable.");
                string native = ScienceUtil.GetExperimentBiome(body,lat,lon);
                if (context.BiomeName != native) throw new InvalidOperationException("Mapped science/context mismatch at " + lat + "," + lon);
                reached.Add(native);
                var local = BodyQueries.WorldToLocalVector(body,world-body.position);
                if (Math.Abs(body.pqsController.GetSurfaceHeight(local.normalized) - context.LocalSurface.Point.Length) > .01)
                    throw new InvalidOperationException("Mapped PQS/context mismatch.");
            }
            if (reached.Count != definition.Assets.Biomes.Count) throw new InvalidOperationException("Some configured biomes were never reached.");
            Mesh mesh = body.scaledBody.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices; Vector2[] xy = mesh.uv3, z = mesh.uv4;
            for (int i=0; i<vertices.Length; i+=37)
            {
                var direction = new Jakobian.Core.Vector3d(xy[i].x,xy[i].y,z[i].x);
                if (Math.Abs(vertices[i].magnitude*6000 - definition.Surface.RadiusAt(direction)) > .02)
                    throw new InvalidOperationException("Mapped scaled mesh radius mismatch.");
            }
            var material = body.pqsController.surfaceMaterial;
            if (material.shader.name != "Jakobian/MappedTerrain" || material.GetTexture("_MainTex") != definition.Assets.AlbedoTexture)
                throw new InvalidOperationException("Mapped terrain material not installed.");
            Debug.Log("[Jakobian] MAPPED_SURFACE_PASS " + body.bodyName + " samples=2520 biomes=" + reached.Count + " scaled=PQS=context shader=" + material.shader.name);
        }
    }
}
