using System;
using System.Collections;
using System.IO;
using System.Linq;
using Jakobian.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jakobian.KSP
{
    internal sealed class NativeWorldIdentity
    {
        private readonly CelestialBody[] bodies = FlightGlobals.Bodies.ToArray();
        private readonly CelestialBody[][] children;
        private readonly int[] indices;
        private readonly double[] radii, mus;
        private readonly PSystem system = PSystemManager.Instance.systemPrefab;
        private readonly CelestialBody[] local = PSystemManager.Instance.localBodies.ToArray();
        private readonly WorldCatalog world = SystemLoader.CurrentWorld;
        internal NativeWorldIdentity()
        { children = bodies.Select(b => b.orbitingBodies.ToArray()).ToArray(); indices = bodies.Select(b => b.flightGlobalsIndex).ToArray(); radii = bodies.Select(b => b.Radius).ToArray(); mus = bodies.Select(b => b.gravParameter).ToArray(); }
        internal void Verify()
        {
            if (!bodies.SequenceEqual(FlightGlobals.Bodies) || !local.SequenceEqual(PSystemManager.Instance.localBodies)
                || system != PSystemManager.Instance.systemPrefab || !ReferenceEquals(world, SystemLoader.CurrentWorld)) throw new InvalidOperationException("Preview altered native system identity/catalog.");
            for (int i = 0; i < bodies.Length; i++)
                if (!children[i].SequenceEqual(bodies[i].orbitingBodies) || indices[i] != bodies[i].flightGlobalsIndex || radii[i] != bodies[i].Radius || mus[i] != bodies[i].gravParameter)
                    throw new InvalidOperationException("Preview altered native body bindings/properties.");
        }
    }

    internal static class TorusPreviewChecks
    {
        internal static IEnumerator Run(TorusPreviewConfig config)
        {
            var identity = new NativeWorldIdentity();
            ulong beforeLocks = InputLockManager.LockMask;
            string otherLock = "Jakobian.PreviewProbe.Foreign." + Guid.NewGuid().ToString("N");
            InputLockManager.SetControlLock(ControlTypes.QUICKSAVE, otherLock);
            try
            {
                var menuLock = new PreviewMenuLock();
                try { if (!InputLockManager.IsLocked(ControlTypes.MAIN_MENU)) throw new InvalidOperationException("Preview menu input was not locked."); }
                finally { menuLock.Dispose(); menuLock.Dispose(); }
                if (InputLockManager.GetControlLock(otherLock) != ControlTypes.QUICKSAVE) throw new InvalidOperationException("Preview removed a foreign input lock.");
            }
            finally { InputLockManager.RemoveControlLock(otherLock); }
            if (InputLockManager.LockMask != beforeLocks) throw new InvalidOperationException("Preview leaked an input lock.");
            Debug.Log("[Jakobian] TORUS_PREVIEW_MENU_LOCK_PASS own=removed foreign=preserved");
            string sourcePath = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "Jakobian", "Preview", "Torus.cfg.disabled");
            ConfigNode source = ConfigNode.Load(sourcePath).GetNode("JakobianPreview");
            void RejectConfig(Action<ConfigNode> edit)
            {
                ConfigNode node = source.CreateCopy(); edit(node); bool rejected = false;
                try { TorusPreviewConfig.Parse(node); } catch (ArgumentException) { rejected = true; }
                if (!rejected) throw new InvalidOperationException("Unsupported/invalid preview configuration accepted.");
            }
            RejectConfig(n => n.AddValue("densityFunction", "unsupported"));
            RejectConfig(n => n.GetNode("Shape").AddValue("type", "Torus"));
            RejectConfig(n => n.SetValue("capability", "flight", true));
            RejectConfig(n => n.GetNode("Preview").SetValue("segmentsU", "7", true));
            RejectConfig(n => n.GetNode("Orbit").SetValue("referenceBody", "MissingParent", true));
            var quality = source.CreateCopy(); quality.GetNode("Preview").SetValue("segmentsU", "64", true);
            if (TorusPreviewConfig.Parse(quality).Definition.World.Fingerprint != config.Definition.World.Fingerprint)
                throw new InvalidOperationException("Render quality changed preview world identity.");
            foreach (string field in new[] { "id", "name", "flightGlobalsIndex" })
            {
                ConfigNode collision = source.CreateCopy();
                collision.SetValue(field, field == "id" ? SystemLoader.CurrentWorld.Bodies[0].Id : field == "name" ? "Kerbin" : SystemLoader.CurrentWorld.Bodies[0].NativeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), true);
                var rejected = new TorusPreviewRegistry(); var scene = rejected.Scene; bool blocked = false;
                try { rejected.Publish(TorusPreviewConfig.Parse(collision)); } catch (ArgumentException) { blocked = true; }
                finally { rejected.Dispose(); }
                if (!blocked || rejected.Count != 0 || rejected.Publications != 0) throw new InvalidOperationException("Native preview identity collision accepted: " + field);
                float deadline = Time.realtimeSinceStartup + 30;
                while (scene.IsValid() && Time.realtimeSinceStartup < deadline) yield return null;
                if (scene.IsValid()) throw new InvalidOperationException("Rejected preview scene did not unload.");
                identity.Verify();
            }
            Debug.Log("[Jakobian] TORUS_PREVIEW_CONFIG_PASS unknown-duplicate-capability-quality-parent=rejected identityCollisions=3 qualityIdentity=stable");
            for (int cycle = 0; cycle < 2; cycle++)
            {
                TorusPreviewRegistry registry = null; Scene scene = default(Scene);
                UnityEngine.Object[] owned = null;
                try
                {
                    registry = new TorusPreviewRegistry(); scene = registry.Scene; var preview = registry.Publish(config); var d = config.Definition;
                    owned = preview.OwnedObjects;
                    if (preview.Model.GetComponent<MeshRenderer>().enabled) throw new InvalidOperationException("Preview renderer can leak into native cameras.");
                    if (!scene.GetPhysicsScene().IsValid() || scene.GetPhysicsScene().Equals(Physics.defaultPhysicsScene)) throw new InvalidOperationException("Preview physics scene was not isolated.");
                    bool workerRejected = false;
                    var worker = new System.Threading.Thread(() => { try { preview.Dispose(); } catch (InvalidOperationException) { workerRejected = true; } });
                    worker.Start(); if (!worker.Join(1000) || !workerRejected) throw new InvalidOperationException("Cross-thread preview disposal was not rejected safely.");
                    if (preview.Prefab.gameObject.activeInHierarchy || preview.Prefab.celestialBody.flightGlobalsIndex != d.NativeIndex
                        || preview.Prefab.celestialBody.pqsController != null || preview.Prefab.pqsVersion != null
                        || preview.Prefab.orbitDriver.orbit.referenceBody.bodyName != d.Parent || registry.Count != 1 || registry.Publications != 1)
                        throw new InvalidOperationException("Detached prefab was incorrectly bound or activated.");
                    bool duplicate = false; try { registry.Publish(config); } catch (InvalidOperationException) { duplicate = true; }
                    if (!duplicate || registry.Publications != 1) throw new InvalidOperationException("Duplicate preview publication.");
                    if (BodyQueries.Sample(preview.Prefab.celestialBody, new global::Vector3d(), out _) != BodyQueryStatus.UnsupportedBody)
                        throw new InvalidOperationException("Detached preview acquired radial gameplay ownership.");
                    var vertices = preview.Mesh.vertices;
                    foreach (var vertex in vertices.Where((_, i) => i % 37 == 0))
                        if (Math.Abs(d.Geometry.SignedDistance(BodyQueries.Core(vertex) * TorusPreviewBody.ScaledFactor)) > d.MinorRadius * 1e-5)
                            throw new InvalidOperationException("Scaled mesh lost analytic dimensions.");
                    var collider = preview.Model.AddComponent<MeshCollider>(); collider.sharedMesh = preview.Mesh; Physics.SyncTransforms();
                    float outer = (float)(d.Geometry.BoundingRadius / 6000);
                    CheckRay(new Vector3(outer * 2, 0, 0), Vector3.left, outer);
                    CheckRay(Vector3.zero, Vector3.right, (float)((d.MajorRadius - d.MinorRadius) / 6000));
                    CheckRay(new Vector3(0, outer * 2, 0), Vector3.down, -1);
                    void CheckRay(Vector3 origin, Vector3 direction, float expected)
                    {
                        bool hit = collider.Raycast(new Ray(origin, direction), out var contact, outer * 4 + 1);
                        if ((expected < 0 && hit) || (expected >= 0 && (!hit || Math.Abs(contact.distance - expected) > .002))) throw new InvalidOperationException("Native scaled triangle ray/hole mismatch.");
                    }
                    collider.enabled = false; UnityEngine.Object.Destroy(collider);
                    preview.Render(0, 0, 1.2f); Texture2D capture = preview.Capture();
                    try
                    {
                        Color32[] pixels = capture.GetPixels32(); int ring = 0, empty = 0;
                        double size = d.Geometry.BoundingRadius / 6000 * 1.2, margin = size * 6 / 512;
                        for (int y = 0; y < 512; y += 4) for (int x = 0; x < 512; x += 4)
                        {
                            double px = (x + .5 - 256) * size * 2 / 512, py = (y + .5 - 256) * size * 2 / 512, rho = Math.Sqrt(px * px + py * py);
                            byte alpha = pixels[y * 512 + x].a;
                            if (rho > (d.MajorRadius - d.MinorRadius) / 6000 + margin && rho < d.Geometry.BoundingRadius / 6000 - margin)
                            { if (alpha < 128) throw new InvalidOperationException("GPU ring contains unexpected gap."); ring++; }
                            if (rho < (d.MajorRadius - d.MinorRadius) / 6000 - margin)
                            { if (alpha > 20) throw new InvalidOperationException("GPU closes the torus hole."); empty++; }
                        }
                        if (ring < 100 || empty < 100) throw new InvalidOperationException("Insufficient GPU silhouette evidence.");
                        string directory = Path.Combine(KSPUtil.ApplicationRootPath, ".dev", "screenshots"); Directory.CreateDirectory(directory);
                        File.WriteAllBytes(Path.Combine(directory, "TorusPreview-top.png"), ImageConversion.EncodeToPNG(capture));
                        Debug.Log("[Jakobian] TORUS_PREVIEW_GPU_PASS ringSamples=" + ring + " holeSamples=" + empty);
                    }
                    finally { UnityEngine.Object.Destroy(capture); }
                    preview.Render(40, 35, 1.2f); capture = preview.Capture();
                    try { File.WriteAllBytes(Path.Combine(KSPUtil.ApplicationRootPath, ".dev", "screenshots", "TorusPreview-oblique.png"), ImageConversion.EncodeToPNG(capture)); }
                    finally { UnityEngine.Object.Destroy(capture); }
                    var baseline = SystemLoader.CurrentWorld;
                    var catalog = new WorldCatalog(baseline.Bodies.Concat(new[] { d.World }));
                    var legacy = WorldSnapshot.Read(new ConfigNode("SCENARIO")); legacy.TryAdoptCurrent(baseline, out var prior);
                    if (prior.Reconcile(catalog).Status != WorldCompatibility.Additive) throw new InvalidOperationException("Preview catalog addition mismatch.");
                    var combined = WorldSnapshot.Read(prior.WriteScenario(catalog));
                    string file = Path.Combine(KSPUtil.ApplicationRootPath, ".dev", "TorusPreviewWorld.cfg"); var container = new ConfigNode(); container.AddNode(combined.WriteScenario(catalog)); container.Save(file);
                    if (WorldSnapshot.Read(ConfigNode.Load(file).GetNode("SCENARIO")).Reconcile(catalog).Status != WorldCompatibility.Compatible) throw new InvalidOperationException("Preview catalog disk roundtrip failed.");
                    var changed = new WorldBodyDescriptor(d.Id, d.Name, d.NativeIndex, d.World.Properties.Select(p => p.Key == "shape.major" ? new WorldProperty(p.Key, "changed") : p));
                    if (combined.Reconcile(new WorldCatalog(baseline.Bodies.Concat(new[] { changed }))).Status != WorldCompatibility.Incompatible) throw new InvalidOperationException("Preview shape edit accepted.");
                    identity.Verify(); Debug.Log("[Jakobian] TORUS_PREVIEW_BIND_PASS cycle=" + cycle + " detached=True nativeBodies=" + FlightGlobals.Bodies.Count + " world=unchanged");
                }
                finally { registry?.Dispose(); registry?.Dispose(); }
                float deadline = Time.realtimeSinceStartup + 30;
                while (scene.IsValid() && Time.realtimeSinceStartup < deadline) yield return null;
                if (scene.IsValid() || registry.Count != 0) throw new InvalidOperationException("Preview scene/resources did not unload.");
                yield return null;
                if (owned.Any(o => o != null)) throw new InvalidOperationException("Preview retained native mesh/material/texture/camera resources after disposal.");
                identity.Verify();
                Debug.Log("[Jakobian] TORUS_PREVIEW_DISPOSE_PASS cycle=" + cycle);
            }
        }
    }
}
