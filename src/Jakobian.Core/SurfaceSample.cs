namespace Jakobian.Core
{
    /// <summary>Prototype geometry result in body-local metres. Normal faces away from solid material.</summary>
    public readonly struct SurfaceSample
    {
        public readonly string SurfaceId, ChartId;
        public readonly Vector3d Point, Normal;
        public readonly double SignedDistance, U, V;
        public SurfaceSample(string surfaceId, string chartId, Vector3d point, Vector3d normal, double distance, double u, double v)
        { SurfaceId = surfaceId; ChartId = chartId; Point = point; Normal = normal; SignedDistance = distance; U = u; V = v; }
    }

    /// <summary>Small experimental subset of the proposed query API, not the final extension ABI.</summary>
    public interface IAnalyticSurface
    {
        /// <summary>False means invalid input or a non-unique nearest point. No arbitrary normal is invented.</summary>
        bool TryNearest(Vector3d bodyLocalPosition, out SurfaceSample sample);
    }
}
