using System.Collections.Generic;
using System.Globalization;
using Jakobian.Core;

namespace Jakobian.KSP
{
    internal static class WorldCatalogBuilder
    {
        internal static WorldCatalog Build()
        {
            var bodies = new List<WorldBodyDescriptor>();
            foreach (var pair in SystemLoader.OwnedBodies)
            {
                var d = pair.Value; var body = pair.Key; var p = new List<WorldProperty>();
                void Text(string key, string value) => p.Add(new WorldProperty(key, value));
                void Number(string key, double value) => Text(key, value == 0 ? "0" : value.ToString("G17", CultureInfo.InvariantCulture));
                Text("provider", "jakobian/radial-pqs/v1"); Text("generator", "radial-mapped/v1"); Text("game", "KSP-1.12.5-03190");
                Text("template", d.Template); Text("chart", "spherical-y/v1"); Text("surface", "exterior"); Text("shape", d.Shape.Kind.ToString());
                Number("shape.x", d.Shape.X); Number("shape.y", d.Shape.Y); Number("shape.z", d.Shape.Z);
                Number("shape.exponent", d.Shape.Exponent); Number("shape.lobes", d.Shape.Lobes); Number("referenceRadius", d.Shape.MinimumRadius);
                Number("terrain.offset", d.Surface.Offset); Number("terrain.deformity", d.Surface.Deformity); Text("terrain.normal", "symmetric-1e-5/v1");
                foreach (string role in new[] { "height", "color", "biome" })
                {
                    Text("map." + role + ".sha256", d.Assets.MapFingerprints.TryGetValue(role, out var hash) ? hash : "none");
                    Text("map." + role + ".path", d.Assets.MapSources.TryGetValue(role, out var path) ? path : "none");
                }
                Text("material.shader", d.Assets.AlbedoTexture != null ? "Jakobian/MappedTerrain/v1" : "stock-Minmus");
                Text("material.bundle", d.Assets.AlbedoTexture != null ? SurfaceAssets.ShaderFingerprint : "none");
                Number("material.red", d.Color.r); Number("material.green", d.Color.g); Number("material.blue", d.Color.b);
                foreach (var biome in d.Assets.Biomes)
                {
                    string prefix = "biome." + biome.Key.ToString("x8", CultureInfo.InvariantCulture);
                    Text(prefix + ".id", biome.Value.name); Number(prefix + ".value", biome.Value.value);
                }
                Text("gravity", "central-point/v1"); Number("gravity.mu", d.Mu); Number("influence.sphere", d.Soi);
                Text("environment", "airless-dry/v1"); Text("spin", "uniform-y/v1"); Number("spin.period", d.RotationPeriod); Number("spin.epoch", 0); Number("spin.initialAngle", 0);
                Text("orbit", "native-circular/v1"); Text("orbit.parent", d.Parent); Number("orbit.parentIndex", body.orbit.referenceBody.flightGlobalsIndex);
                Number("orbit.radius", d.OrbitRadius); Number("orbit.inclination", d.Inclination); Number("orbit.phase", d.Phase); Number("orbit.epoch", 0);
                bodies.Add(new WorldBodyDescriptor(d.Id, d.Name, d.Index, p));
            }
            return new WorldCatalog(bodies);
        }
    }
}
