using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jakobian.KSP
{
    [KSPAddon(KSPAddon.Startup.PSystemSpawn, false)]
    public sealed class SystemLoader : MonoBehaviour
    {
        internal static readonly Dictionary<string, BodyDefinition> Definitions = new Dictionary<string, BodyDefinition>();
        internal static readonly Dictionary<CelestialBody, BodyDefinition> OwnedBodies = new Dictionary<CelestialBody, BodyDefinition>();
        internal static string Error;
        internal static bool Published, Bound;
        private GameObject container;

        private void Awake()
        {
            if (Published) return;
            try
            {
                if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Kopernicus"))
                    throw new InvalidOperationException("Kopernicus coexistence ownership adapter is not implemented. Jakobian will not publish a competing system.");
                var definitions = new List<BodyDefinition>();
                foreach (var config in GameDatabase.Instance.GetConfigs("Jakobian"))
                {
                    BodyDefinition.Check(config.config, "schemaVersion", "Body");
                    if (config.config.GetValue("schemaVersion") != "1") throw new ArgumentException("Jakobian schemaVersion must be 1.");
                    foreach (ConfigNode body in config.config.GetNodes("Body")) definitions.Add(BodyDefinition.Parse(body));
                }
                if (definitions.Count == 0) { Debug.Log("[Jakobian] No body definitions; stock system retained."); return; }
                PSystem stock = PSystemManager.Instance.systemPrefab;
                var originals = Flatten(stock.rootBody).ToList();
                var names = new HashSet<string>(originals.Select(b => b.celestialBody.bodyName));
                var ids = new HashSet<string>(); var indices = new HashSet<int>(originals.Select(b => b.flightGlobalsIndex));
                foreach (BodyDefinition d in definitions)
                    if (!names.Add(d.Name) || !ids.Add(d.Id) || !indices.Add(d.Index)) throw new ArgumentException("Duplicate body name, id or native index: " + d.Name);
                foreach (BodyDefinition d in definitions)
                    if (!names.Contains(d.Parent)) throw new ArgumentException("Missing orbit parent: " + d.Parent);
                // Initial body hierarchy is append-only beneath stock parents; no ambiguous cycles.
                foreach (BodyDefinition d in definitions)
                    if (!originals.Any(b => b.celestialBody.bodyName == d.Parent)) throw new ArgumentException("Initial loader requires a stock orbit parent.");

                container = new GameObject("Jakobian Prefab"); container.SetActive(false);
                PSystem system = container.AddComponent<PSystem>();
                system.systemName = "Jakobian"; system.systemScale = stock.systemScale; system.systemTimeScale = stock.systemTimeScale; system.mainToolbarSelected = stock.mainToolbarSelected;
                var map = new Dictionary<PSystemBody, PSystemBody>();
                foreach (PSystemBody original in originals)
                {
                    PSystemBody clone = Instantiate(original.gameObject, container.transform, true).GetComponent<PSystemBody>();
                    clone.name = original.name;
                    if (clone.celestialBody == original.celestialBody) throw new InvalidOperationException("Stock body clone did not isolate native metadata.");
                    clone.children = new List<PSystemBody>(); map.Add(original, clone);
                }
                foreach (PSystemBody original in originals)
                {
                    PSystemBody clone = map[original];
                    clone.children.AddRange(original.children.Select(child => map[child]));
                    if (original.orbitDriver != null && original.orbitDriver.orbit != null)
                    {
                        Orbit old = original.orbitDriver.orbit;
                        var parent = originals.FirstOrDefault(b => b.celestialBody == old.referenceBody);
                        if (parent != null) clone.orbitDriver.orbit = new Orbit(old.inclination, old.eccentricity, old.semiMajorAxis, old.LAN, old.argumentOfPeriapsis, old.meanAnomalyAtEpoch, old.epoch, map[parent].celestialBody);
                    }
                }
                system.rootBody = map[stock.rootBody];
                foreach (BodyDefinition definition in definitions)
                {
                    PSystemBody template = originals.First(b => b.celestialBody.bodyName == definition.Template);
                    PSystemBody parent = map[originals.First(b => b.celestialBody.bodyName == definition.Parent)];
                    PSystemBody body = BuildBody(template, parent, definition);
                    parent.children.Add(body);
                }
                foreach (BodyDefinition d in definitions) Definitions.Add(d.Name, d);
                PSystemManager.Instance.OnPSystemReady.Add(OnReady);
                PSystemManager.Instance.systemPrefab = system;
                Published = true;
                Debug.Log("[Jakobian] SYSTEM_PUBLISHED bodies=" + definitions.Count + " stock=" + originals.Count + " loader=standalone");
            }
            catch (Exception error)
            {
                Error = error.Message; Definitions.Clear();
                if (container != null && !Published) Destroy(container);
                Debug.LogError("[Jakobian] LOAD_FAILED: " + error);
            }
        }

        private PSystemBody BuildBody(PSystemBody template, PSystemBody parent, BodyDefinition d)
        {
            PSystemBody body = Instantiate(template.gameObject, container.transform, true).GetComponent<PSystemBody>();
            body.name = d.Name; body.children = new List<PSystemBody>(); body.flightGlobalsIndex = d.Index;
            CelestialBody cb = body.celestialBody;
            cb.gameObject.name = cb.bodyName = cb.bodyDisplayName = cb.bodyAdjectiveDisplayName = d.Name;
            cb.bodyDescription = d.Description; cb.isHomeWorld = false; cb.progressTree = null;
            cb.Radius = d.Shape.MinimumRadius; cb.gravParameter = d.Mu; cb.Mass = d.Mu / 6.67408e-11;
            cb.GeeASL = d.Mu / (cb.Radius * cb.Radius * 9.80665); cb.rotates = d.RotationPeriod > 0; cb.rotationPeriod = d.RotationPeriod; cb.tidallyLocked = false;
            // Nonrotating bodies skip CBUpdate's spin-frame initialization. A zero
            // serialized quaternion collapses surface/packed-vessel coordinates to the centre.
            cb.rotation = Quaternion.identity; cb.initialRotation = cb.rotationAngle = 0;
            cb.angularVelocity = global::Vector3d.zero; cb.zUpAngularVelocity = global::Vector3d.zero; cb.angularV = 0;
            cb.inverseRotation = false;
            cb.atmosphere = false; cb.ocean = false; cb.sphereOfInfluence = d.Soi;
            body.orbitDriver.orbit = new Orbit(d.Inclination, 0, d.OrbitRadius, 0, 0, d.Phase, 0, parent.celestialBody);
            body.orbitDriver.referenceBody = parent.celestialBody; cb.orbitDriver = body.orbitDriver;
            body.orbitDriver.orbitColor = d.Color;
            body.scaledVersion = Instantiate(template.scaledVersion, container.transform, true);
            body.scaledVersion.name = d.Name; body.scaledVersion.transform.localScale = Vector3.one;
            MeshFilter filter = body.scaledVersion.GetComponent<MeshFilter>(); filter.sharedMesh = ScaledShapeMesh.Build(d.Shape);
            Renderer renderer = body.scaledVersion.GetComponent<Renderer>(); renderer.sharedMaterial = new Material(renderer.sharedMaterial);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { d.Color, d.Color, d.Color, d.Color }); texture.Apply();
            renderer.sharedMaterial.SetTexture("_MainTex", texture);
            SphereCollider pick = body.scaledVersion.GetComponent<SphereCollider>(); if (pick != null) pick.radius = (float)(d.Shape.BoundingRadius / 6000);
            body.pqsVersion = Instantiate(template.pqsVersion, container.transform, true);
            body.pqsVersion.name = d.Name; body.pqsVersion.radius = cb.Radius; cb.pqsController = body.pqsVersion;
            // Instantiate copies component references, not material assets. Fade writes must
            // never change another body's terrain or the retained stock template material.
            body.pqsVersion.surfaceMaterial = new Material(template.pqsVersion.surfaceMaterial);
            if (template.pqsVersion.fallbackMaterial != null)
                body.pqsVersion.fallbackMaterial = new Material(template.pqsVersion.fallbackMaterial);
            foreach (PQSMod mod in body.pqsVersion.GetComponentsInChildren<PQSMod>(true))
            {
                if (mod is PQSMod_CelestialBodyTransform)
                {
                    var transformMod = (PQSMod_CelestialBodyTransform)mod;
                    transformMod.body = cb; transformMod.forceRebuildOnTargetChange = true;
                    transformMod.deactivateAltitude = 40000;
                    transformMod.planetFade = new PQSMod_CelestialBodyTransform.AltitudeFade {
                        fadeFloatName = "_PlanetOpacity", fadeStart = 20000, fadeEnd = 30000,
                        valueStart = 0, valueEnd = 1, secondaryRenderers = new List<GameObject>()
                    };
                    transformMod.secondaryFades = new PQSMod_CelestialBodyTransform.AltitudeFade[0];
                }
                else if (!(mod is PQSMod_QuadMeshColliders) && !(mod is PQSMod_MaterialSetDirection) && mod.GetType().Name != "PQSMod_UVPlanetRelativePosition") { mod.modEnabled = false; mod.enabled = false; }
                mod.sphere = body.pqsVersion;
            }
            var modifierObject = new GameObject("JakobianRadialTerrain");
            modifierObject.transform.SetParent(body.pqsVersion.transform, false);
            var modifier = modifierObject.AddComponent<RadialTerrain>();
            modifier.sphere = body.pqsVersion; modifier.modEnabled = true; modifier.order = 100000;
            modifier.shapeKind = (int)d.Shape.Kind; modifier.axisX = d.Shape.X; modifier.axisY = d.Shape.Y; modifier.axisZ = d.Shape.Z;
            modifier.exponent = d.Shape.Exponent; modifier.lobes = d.Shape.Lobes; modifier.surfaceColor = d.Color;
            body.pqsVersion.minLevel = 2; body.pqsVersion.maxLevel = 9;
            Debug.Log("[Jakobian] BODY_COMPILED " + d.Name + " id=" + d.Id + " shape=" + d.Shape.Kind + " refRadius=" + cb.Radius);
            return body;
        }

        private static IEnumerable<PSystemBody> Flatten(PSystemBody body)
        {
            yield return body;
            foreach (PSystemBody child in body.children) foreach (PSystemBody descendant in Flatten(child)) yield return descendant;
        }

        private void OnReady()
        {
            PSystemManager.Instance.OnPSystemReady.Remove(OnReady);
            foreach (var pair in Definitions)
            {
                CelestialBody cb = PSystemManager.Instance.localBodies.Single(b => b.bodyName == pair.Key);
                OwnedBodies.Add(cb, pair.Value);
                cb.sphereOfInfluence = pair.Value.Soi;
                foreach (PQSMod_CelestialBodyTransform transformMod in cb.pqsController.GetComponentsInChildren<PQSMod_CelestialBodyTransform>(true))
                    transformMod.body = cb;
                cb.scaledBody.transform.localScale = Vector3.one;
                if (!cb.rotates) StaticBodyFrames.Register(cb); else cb.CBUpdate();
                foreach (ScaledSpaceFader fader in cb.scaledBody.GetComponentsInChildren<ScaledSpaceFader>(true))
                { fader.celestialBody = cb; fader.fadeStart = 20000; fader.fadeEnd = 30000; }
                Debug.Log("[Jakobian] BODY_BOUND " + cb.bodyName + " nativeIndex=" + cb.flightGlobalsIndex + " pqs=" + (cb.pqsController != null));
            }
            Bound = true; Debug.Log("[Jakobian] SYSTEM_READY");
        }
    }
}
