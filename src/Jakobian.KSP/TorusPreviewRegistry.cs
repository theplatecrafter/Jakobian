using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Jakobian.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jakobian.KSP
{
    /// <summary>Owner-scoped detached prefab registry. Never publishes into KSP's live system.</summary>
    public sealed class TorusPreviewRegistry : IDisposable
    {
        private readonly int owner = Thread.CurrentThread.ManagedThreadId;
        private bool disposed;
        private TorusPreviewBody body;
        public Scene Scene { get; }
        public int Publications { get; private set; }
        public int Count => body == null ? 0 : 1;
        public TorusPreviewRegistry()
        {
            Scene = SceneManager.CreateScene("Jakobian detached torus " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        }
        public TorusPreviewBody Publish(TorusPreviewConfig config)
        {
            Check(); if (config == null) throw new ArgumentNullException(nameof(config));
            if (!SystemLoader.Bound || SystemLoader.CurrentWorld == null) throw new InvalidOperationException("Native baseline is not ready.");
            if (body != null) throw new InvalidOperationException("A preview registry holds one publication; dispose before replacement.");
            if (UnityEngine.Object.FindObjectsOfType<Renderer>().Any(r => r.gameObject.layer == 31)) throw new InvalidOperationException("Preview render layer 31 is already in use.");
            var d = config.Definition;
            if (FlightGlobals.Bodies.Any(b => b.bodyName == d.Name || b.flightGlobalsIndex == d.NativeIndex)
                || SystemLoader.CurrentWorld.Bodies.Any(b => b.Id == d.Id)) throw new ArgumentException("Preview identity collides with an installed body.");
            CelestialBody parent = FlightGlobals.Bodies.SingleOrDefault(b => b.bodyName == d.Parent && b.flightGlobalsIndex == d.ParentIndex);
            if (parent == null) throw new ArgumentException("Preview parent changed or is missing.");
            body = new TorusPreviewBody(Scene, config, parent); Publications++; return body;
        }
        private void Check()
        {
            if (Thread.CurrentThread.ManagedThreadId != owner) throw new InvalidOperationException("Preview registry belongs to its Unity owner thread.");
            if (disposed) throw new ObjectDisposedException(nameof(TorusPreviewRegistry));
        }
        public void Dispose()
        {
            if (disposed) return; Check(); disposed = true;
            if (body != null) { body.Dispose(); body = null; }
            if (Scene.IsValid() && Scene.isLoaded) SceneManager.UnloadSceneAsync(Scene);
        }
    }

    /// <summary>Inactive native prefab plus an independently owned scaled renderer. Do not activate or register the prefab.</summary>
    public sealed class TorusPreviewBody : IDisposable
    {
        public const double ScaledFactor = 6000;
        private bool disposed;
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private GameObject prefabRoot, renderRoot;
        private Material material;
        private Texture2D color;
        private Camera camera;
        private RenderTexture target;
        public TorusBodyDefinition Definition { get; }
        public PSystemBody Prefab { get; private set; }
        public Mesh Mesh { get; private set; }
        public GameObject Model { get; private set; }
        public RenderTexture Image => disposed ? null : target;
        internal UnityEngine.Object[] OwnedObjects => new UnityEngine.Object[] { prefabRoot, renderRoot, Prefab, Mesh, Model, material, color, camera, target };
        internal TorusPreviewBody(Scene scene, TorusPreviewConfig config, CelestialBody parent)
        {
            Definition = config.Definition;
            try
            {
                prefabRoot = new GameObject(Definition.Name + " inactive prefab"); prefabRoot.SetActive(false); SceneManager.MoveGameObjectToScene(prefabRoot, scene);
                Prefab = prefabRoot.AddComponent<PSystemBody>(); var cb = prefabRoot.GetComponent<CelestialBody>() ?? prefabRoot.AddComponent<CelestialBody>();
                var driver = prefabRoot.GetComponent<OrbitDriver>() ?? prefabRoot.AddComponent<OrbitDriver>();
                Prefab.celestialBody = cb; Prefab.flightGlobalsIndex = Definition.NativeIndex; Prefab.children = new List<PSystemBody>(); Prefab.orbitDriver = driver;
                cb.bodyName = cb.bodyDisplayName = cb.bodyAdjectiveDisplayName = Definition.Name; cb.flightGlobalsIndex = Definition.NativeIndex;
                cb.Radius = Definition.MinorRadius; cb.gravParameter = Definition.Mu; cb.Mass = Definition.Mu / 6.67408e-11;
                cb.GeeASL = Definition.Mu / (Definition.MinorRadius * Definition.MinorRadius * 9.80665);
                cb.sphereOfInfluence = Definition.SphereOfInfluence; cb.atmosphere = cb.ocean = cb.rotates = cb.isHomeWorld = false; cb.rotationPeriod = 0;
                cb.rotation = Quaternion.identity; cb.initialRotation = cb.rotationAngle = 0; cb.orbitDriver = driver;
                driver.orbit = new Orbit(Definition.Inclination, 0, Definition.OrbitRadius, 0, 0, Definition.Phase, 0, parent); driver.referenceBody = parent;
                // Never activate this root: CelestialBody.Awake adds itself to its parent's orbitingBodies.
                renderRoot = new GameObject(Definition.Name + " scaled preview"); SceneManager.MoveGameObjectToScene(renderRoot, scene);
                Model = new GameObject("Detached scaled mesh"); Model.transform.SetParent(renderRoot.transform, false); Model.layer = 31;
                var data = TorusMeshBaker.BuildClosed(Definition.MajorRadius, Definition.MinorRadius, config.SegmentsU, config.SegmentsV);
                var vertices = new Vector3[data.Vertices.Length]; var normals = new Vector3[vertices.Length];
                var xy = new Vector2[vertices.Length]; var z = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    var p = data.Vertices[i]; var direction = p / p.Length;
                    vertices[i] = (Vector3)BodyQueries.Ksp(p / ScaledFactor); normals[i] = (Vector3)BodyQueries.Ksp(data.Normals[i]);
                    xy[i] = new Vector2((float)direction.X, (float)direction.Y); z[i] = new Vector2((float)direction.Z, 0);
                }
                Mesh = new Mesh { name = Definition.Name + " closed torus" }; Mesh.vertices = vertices; Mesh.normals = normals; Mesh.triangles = data.Triangles; Mesh.uv3 = xy; Mesh.uv4 = z; Mesh.RecalculateBounds();
                Model.AddComponent<MeshFilter>().sharedMesh = Mesh;
                color = new Texture2D(2, 2, TextureFormat.RGBA32, false); color.SetPixels(Enumerable.Repeat(new Color(.15f, .65f, .8f, 1), 4).ToArray()); color.Apply();
                material = new Material(SurfaceAssets.TerrainShader); material.SetTexture("_MainTex", color); material.SetFloat("_Opacity", 1); material.SetFloat("_PlanetOpacity", 0);
                var renderer = Model.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.enabled = false;
                Prefab.scaledVersion = Model; cb.scaledBody = Model;
                var lamp = new GameObject("Preview light"); lamp.transform.SetParent(renderRoot.transform, false); lamp.layer = 31; lamp.transform.rotation = Quaternion.Euler(35, -25, 0);
                var light = lamp.AddComponent<Light>(); light.type = LightType.Directional; light.color = Color.white; light.intensity = 1.2f; light.cullingMask = 1 << 31;
                var eye = new GameObject("Preview camera"); eye.transform.SetParent(renderRoot.transform, false); camera = eye.AddComponent<Camera>(); camera.enabled = false;
                camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.orthographic = true;
                camera.nearClipPlane = .01f; camera.farClipPlane = (float)(Definition.Geometry.BoundingRadius / ScaledFactor * 10 + 1);
                target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32); if (!target.Create()) throw new InvalidOperationException("Preview RenderTexture unavailable.");
                camera.targetTexture = target; Render(35, 45, 1.2f);
            }
            catch { Dispose(); throw; }
        }
        public void Render(float tilt, float azimuth, float zoom)
        {
            Check();
            if (!Jakobian.Core.Vector3d.Finite(tilt) || !Jakobian.Core.Vector3d.Finite(azimuth) || !Jakobian.Core.Vector3d.Finite(zoom)
                || tilt < 0 || tilt > 85 || Math.Abs(azimuth) > 100000 || zoom < 1.05f || zoom > 2) throw new ArgumentOutOfRangeException(nameof(tilt));
            float radius = (float)(Definition.Geometry.BoundingRadius / ScaledFactor);
            Vector3 direction = Quaternion.Euler(tilt, azimuth, 0) * Vector3.up;
            camera.transform.localPosition = direction * (radius * 4 + 1); camera.transform.LookAt(Model.transform.position, Vector3.forward);
            camera.orthographicSize = radius * zoom; camera.aspect = 1;
            RenderTexture previous = RenderTexture.active;
            var renderer = Model.GetComponent<MeshRenderer>(); renderer.enabled = true;
            try { camera.Render(); } finally { renderer.enabled = false; RenderTexture.active = previous; }
        }
        public Texture2D Capture()
        {
            Check();
            RenderTexture previous = RenderTexture.active; Texture2D result = null;
            try { RenderTexture.active = target; result = new Texture2D(512, 512, TextureFormat.RGBA32, false); result.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); result.Apply(); return result; }
            catch { if (result != null) UnityEngine.Object.Destroy(result); throw; }
            finally { RenderTexture.active = previous; }
        }
        public void Dispose()
        {
            if (disposed) return; Check(); disposed = true;
            if (prefabRoot != null) { prefabRoot.SetActive(false); UnityEngine.Object.Destroy(prefabRoot); }
            if (renderRoot != null) { renderRoot.SetActive(false); UnityEngine.Object.Destroy(renderRoot); }
            if (camera != null) camera.targetTexture = null;
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (Mesh != null) UnityEngine.Object.Destroy(Mesh);
            if (material != null) UnityEngine.Object.Destroy(material); if (color != null) UnityEngine.Object.Destroy(color);
        }
        private void Check()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("Preview body belongs to its Unity owner thread.");
            if (disposed) throw new ObjectDisposedException(nameof(TorusPreviewBody));
        }
    }
}
