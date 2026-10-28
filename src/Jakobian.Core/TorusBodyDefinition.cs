using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Jakobian.Core
{
    /// <summary>Experimental immutable preview-only body. No live-system or force ownership is granted.</summary>
    public sealed class TorusBodyDefinition
    {
        public string Id => World.Id;
        public string Name => World.NativeName;
        public int NativeIndex => World.NativeIndex;
        public double MajorRadius { get; }
        public double MinorRadius { get; }
        public double Mu { get; }
        public double SphereOfInfluence { get; }
        public string Parent { get; }
        public int ParentIndex { get; }
        public double OrbitRadius { get; }
        public double Inclination { get; }
        public double Phase { get; }
        public AnalyticSolid Geometry { get; }
        public TorusChart Chart { get; }
        public WorldBodyDescriptor World { get; }
        public TorusBodyDefinition(string id, string name, int nativeIndex, double major, double minor, double mu,
            double soi, string parent, int parentIndex, double orbitRadius, double inclination, double phase)
        {
            Geometry = new AnalyticSolid(major, minor); Chart = new TorusChart(major, minor);
            if (minor < 100 || major + minor > 1e10 || minor / major < 1e-4 || nativeIndex < 17
                || !Vector3d.Finite(mu) || mu <= 0 || mu > 1e30 || !Vector3d.Finite(soi) || soi < 5 * (major + minor) || soi > 1e15
                || parent == null || !Regex.IsMatch(parent, @"\A[A-Za-z][A-Za-z0-9_]{0,63}\z") || parentIndex < 0
                || !Vector3d.Finite(orbitRadius) || orbitRadius <= 20 * (major + minor) || orbitRadius > 1e15
                || !Vector3d.Finite(inclination) || Math.Abs(inclination) > 180 || !Vector3d.Finite(phase) || Math.Abs(phase) > 1e9)
                throw new ArgumentException("Invalid preview torus dimensions, identity or dynamics bounds.");
            MajorRadius = major; MinorRadius = minor; Mu = mu; SphereOfInfluence = soi; Parent = parent; ParentIndex = parentIndex;
            OrbitRadius = orbitRadius; Inclination = inclination; Phase = phase;
            var p = new List<WorldProperty>();
            void Text(string key, string value) => p.Add(new WorldProperty(key, value));
            void Number(string key, double value) => Text(key, value == 0 ? "0" : value.ToString("G17", CultureInfo.InvariantCulture));
            Text("provider", "jakobian/detached-torus-preview/v1"); Text("generator", "analytic-torus/v1"); Text("capability", "preview-only");
            Text("shape", "Torus"); Text("surface", TorusAnchor.SurfaceId); Text("chart", TorusAnchor.ChartId);
            Number("shape.major", major); Number("shape.minor", minor); Number("referenceRadius", minor);
            Text("gravity", "central-point-metadata/v1"); Number("gravity.mu", mu); Number("influence.sphere", soi);
            Text("spin", "static/v1"); Text("environment", "airless-dry/v1"); Text("orbit", "native-circular-metadata/v1");
            Text("orbit.parent", parent); Number("orbit.parentIndex", parentIndex); Number("orbit.radius", orbitRadius);
            Number("orbit.inclination", inclination); Number("orbit.phase", phase); Number("orbit.epoch", 0);
            World = new WorldBodyDescriptor(id, name, nativeIndex, p);
        }
    }
}
