using System;
using System.Linq;
using UnityEngine;

namespace Jakobian.KSP
{
    internal static class NativeChecks
    {
        internal static void Run()
        {
            TorusMeshChecks.Run();
            if (SystemLoader.Error != null) throw new InvalidOperationException(SystemLoader.Error);
            if (SystemLoader.Definitions.Count > 0 && !SystemLoader.Bound) throw new InvalidOperationException("System binding incomplete.");
            CelestialBody kerbin = FlightGlobals.Bodies.Single(b => b.bodyName == "Kerbin");
            if (kerbin.flightGlobalsIndex != 1 || kerbin.Radius != 600000) throw new InvalidOperationException("Stock homeworld changed.");
            foreach (var pair in SystemLoader.Definitions)
            {
                CelestialBody body = FlightGlobals.Bodies.Single(b => b.bodyName == pair.Key);
                if (body.pqsController == null || body.scaledBody == null) throw new InvalidOperationException("Missing representation: " + pair.Key);
                if (FlightGlobals.Bodies.Any(other => other != body && other.pqsController != null
                    && other.pqsController.surfaceMaterial == body.pqsController.surfaceMaterial))
                    throw new InvalidOperationException("Shared mutable terrain material: " + pair.Key);
                foreach (PQSMod_CelestialBodyTransform transformMod in body.pqsController.GetComponentsInChildren<PQSMod_CelestialBodyTransform>(true))
                    if (transformMod.body != body) throw new InvalidOperationException("Terrain bound to prefab rather than live body: " + pair.Key);
                double surfaceDistance = (body.GetWorldSurfacePosition(0, 0, 100) - body.position).magnitude;
                if (Math.Abs(surfaceDistance - body.Radius - 100) > .01) throw new InvalidOperationException("Invalid static body frame: " + pair.Key + " distance=" + surfaceDistance);
                body.pqsController.StartUpSphere();
                RadialTerrain modifier = body.pqsController.GetComponentInChildren<RadialTerrain>(true);
                Debug.Log("[Jakobian] TERRAIN_BIND " + pair.Key + " kind=" + modifier.shapeKind + " axisX=" + modifier.axisX + " enabled=" + modifier.modEnabled);
                foreach (var direction in new[] { new global::Vector3d(1, 0, 0), new global::Vector3d(0, 1, 0), new global::Vector3d(0, 0, 1), new global::Vector3d(1, 1, 1) })
                {
                    double expected = pair.Value.Surface.RadiusAt(new Jakobian.Core.Vector3d(direction.x, direction.y, direction.z));
                    double actual = body.pqsController.GetSurfaceHeight(direction.normalized);
                    if (Math.Abs(expected - actual) > .01) throw new InvalidOperationException(pair.Key + " PQS height mismatch: " + actual + " expected " + expected);
                }
                Mesh mesh = body.scaledBody.GetComponent<MeshFilter>().sharedMesh;
                if (mesh.vertexCount < 100 || Math.Abs(body.scaledBody.transform.localScale.x - 1) > 1e-5) throw new InvalidOperationException("Scaled representation invalid.");
                foreach (var coordinates in new[] { new Vector2(25, 70), new Vector2(-32, -110), new Vector2(89, 179) })
                {
                    global::Vector3d position = body.GetWorldSurfacePosition(coordinates.x, coordinates.y, 30000);
                    BodyPointSample context;
                    if (BodyQueries.Sample(body, position, out context) != BodyQueryStatus.Available) throw new InvalidOperationException("Context unavailable: " + pair.Key);
                    if (Math.Abs(context.LocalSurface.LatitudeDegrees - coordinates.x) > .0001
                        || Math.Abs(context.LocalSurface.LongitudeDegrees - coordinates.y) > .0001)
                        throw new InvalidOperationException("Context chart/frame mismatch: " + pair.Key);
                    global::Vector3d local = BodyQueries.WorldToLocalVector(body, position - body.position);
                    if ((BodyQueries.LocalToWorldVector(body, BodyQueries.Core(local)) - (position - body.position)).magnitude > .001)
                        throw new InvalidOperationException("Context frame roundtrip: " + pair.Key);
                    double nativeFloor = body.pqsController.GetSurfaceHeight(local.normalized);
                    if (Math.Abs(context.LocalSurface.Clearance - (local.magnitude - nativeFloor)) > .01)
                        throw new InvalidOperationException("Context ground mismatch: " + pair.Key);
                    if (context.BiomeName != ScienceUtil.GetExperimentBiome(body,coordinates.x,coordinates.y))
                        throw new InvalidOperationException("Native science biome mismatch: " + pair.Key);
                }
                if (pair.Value.Assets.BiomePixels != null) MappedSurfaceChecks.Run(body,pair.Value);
                Debug.Log("[Jakobian] NATIVE_CONTEXT_PASS " + pair.Key + " frame=Y/Z chart=stock down=gravity ground=radial");
                Debug.Log("[Jakobian] NATIVE_BODY_PASS " + pair.Key + " PQS=analytic shape=" + pair.Value.Shape.Kind + " vertices=" + mesh.vertexCount);
            }
            BodyPointSample unsupported;
            if (BodyQueries.Sample(kerbin, kerbin.position + new global::Vector3d(700000, 0, 0), out unsupported) != BodyQueryStatus.UnsupportedBody)
                throw new InvalidOperationException("Context must not claim stock body ownership.");
        }
    }
}
