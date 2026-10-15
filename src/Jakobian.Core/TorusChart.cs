using System;

namespace Jakobian.Core
{
    public readonly struct TorusAnchor
    {
        public readonly double U, V, NormalOffset;
        public const string ChartId = "torus-uv/v1";
        public const string SurfaceId = "exterior";
        public TorusAnchor(double u, double v, double normalOffset)
        {
            if (!Vector3d.Finite(u) || !Vector3d.Finite(v) || !Vector3d.Finite(normalOffset)
                || Math.Abs(u) > 1e9 || Math.Abs(v) > 1e9) throw new ArgumentOutOfRangeException(nameof(u));
            U = Wrap(u); V = Wrap(v); NormalOffset = normalOffset;
        }
        internal static double Wrap(double value) { double result = value - Math.Floor(value); return result >= 1 ? 0 : result; }
    }

    public readonly struct TorusMaterialFrame
    {
        public readonly Vector3d Position, Normal, UDirection, VDirection;
        internal TorusMaterialFrame(Vector3d point, Vector3d normal, Vector3d u, Vector3d v)
        { Position = point; Normal = normal; UDirection = u; VDirection = v; }
    }

    /// <summary>Periodic material coordinates, independent of render/collider tessellation.
    /// VDirection cross UDirection is the outward normal, matching TorusMeshBaker winding.</summary>
    public sealed class TorusChart
    {
        public double MajorRadius { get; }
        public double MinorRadius { get; }
        public TorusChart(double major, double minor)
        {
            var solid = new AnalyticSolid(major, minor);
            MajorRadius = solid.MajorRadius; MinorRadius = solid.MinorRadius;
        }
        public bool TryResolve(TorusAnchor anchor, out TorusMaterialFrame frame)
        {
            frame = default(TorusMaterialFrame);
            double tube = MinorRadius + anchor.NormalOffset;
            // Beyond these limits offset charts fold or reach nonunique medial sets.
            if (!Vector3d.Finite(tube) || tube <= 0 || tube >= MajorRadius) return false;
            double u = anchor.U * 2 * Math.PI, v = anchor.V * 2 * Math.PI;
            double cu = Math.Cos(u), su = Math.Sin(u), cv = Math.Cos(v), sv = Math.Sin(v);
            Vector3d normal = new Vector3d(cu * cv, sv, su * cv);
            frame = new TorusMaterialFrame(new Vector3d((MajorRadius + tube * cv) * cu, tube * sv, (MajorRadius + tube * cv) * su),
                normal, new Vector3d(-su,0,cu), new Vector3d(-cu*sv,cv,-su*sv));
            return true;
        }
        public bool TryProject(Vector3d point, out TorusAnchor anchor)
        {
            anchor = default(TorusAnchor);
            double rho = Math.Sqrt(point.X * point.X + point.Z * point.Z);
            if (!point.IsFinite || !Vector3d.Finite(rho) || rho <= (MajorRadius + MinorRadius) * 1e-12) return false;
            double x = rho - MajorRadius, tube = Math.Sqrt(x*x + point.Y*point.Y);
            if (!Vector3d.Finite(tube) || tube <= MinorRadius * 1e-12 || tube >= MajorRadius) return false;
            anchor = new TorusAnchor(Math.Atan2(point.Z,point.X)/(2*Math.PI),Math.Atan2(point.Y,x)/(2*Math.PI),tube-MinorRadius);
            return true;
        }
    }
}
