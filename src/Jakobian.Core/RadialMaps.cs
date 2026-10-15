using System;

namespace Jakobian.Core
{
    public readonly struct MapColor
    {
        public readonly double R, G, B, A;
        public MapColor(double r, double g, double b, double a) { R = r; G = g; B = b; A = a; }
    }

    /// <summary>Immutable RGBA8, south-to-north rows. Columns are periodic samples at x/width;
    /// rows include both poles at y/(height-1). No sRGB conversion is performed here.</summary>
    public sealed class RadialMap
    {
        private readonly byte[] pixels;
        public int Width { get; }
        public int Height { get; }
        public RadialMap(int width, int height, byte[] rgba)
        {
            if (width < 4 || width > 2048 || height < 3 || height > 1025 || rgba == null || rgba.Length != width * height * 4)
                throw new ArgumentException("Map must be 4..2048 by 3..1025 RGBA8 pixels.");
            Width = width; Height = height; pixels = (byte[])rgba.Clone();
            // A pole is a single physical point, independent of longitude.
            for (int y = 0; y < height; y += height - 1) for (int x = 1; x < width; x++)
                if (Pixel(x, y) != Pixel(0, y)) throw new ArgumentException("Every pixel in each pole row must have the same RGBA value.");
        }
        public static void Coordinates(Vector3d direction, out double u, out double v)
        {
            double length = direction.Length;
            if (!direction.IsFinite || !Vector3d.Finite(length) || length < 1e-12) throw new ArgumentException("Invalid map direction.");
            u = Math.Atan2(direction.Z, direction.X) / (2 * Math.PI); u -= Math.Floor(u);
            // A tiny negative longitude can round to exactly 1 after wrapping.
            if (u >= 1) u = 0;
            v = Math.Asin(Math.Max(-1, Math.Min(1, direction.Y / length))) / Math.PI + .5;
        }
        public uint Pixel(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) throw new ArgumentOutOfRangeException();
            int i = (y * Width + x) * 4;
            return ((uint)pixels[i] << 24) | ((uint)pixels[i + 1] << 16) | ((uint)pixels[i + 2] << 8) | pixels[i + 3];
        }
        public uint Nearest(Vector3d direction)
        {
            double u, v; Coordinates(direction, out u, out v);
            return Pixel((int)Math.Floor(u * Width + .5) % Width, Math.Min(Height - 1, (int)Math.Floor(v * (Height - 1) + .5)));
        }
        public MapColor Bilinear(Vector3d direction)
        {
            double u, v; Coordinates(direction, out u, out v);
            double x = u * Width, y = Math.Max(0, Math.Min(Height - 1, v * (Height - 1)));
            if (x >= Width) x = 0;
            int x0 = (int)x, y0 = (int)y, x1 = (x0 + 1) % Width, y1 = Math.Min(y0 + 1, Height - 1);
            double tx = x - x0, ty = y - y0;
            return new MapColor(Channel(x0,y0,x1,y1,tx,ty,0), Channel(x0,y0,x1,y1,tx,ty,1),
                Channel(x0,y0,x1,y1,tx,ty,2), Channel(x0,y0,x1,y1,tx,ty,3));
        }
        private double Channel(int x, int y, int x1, int y1, double tx, double ty, int c)
        {
            double a = pixels[(y * Width + x) * 4 + c] * (1-tx) + pixels[(y * Width + x1) * 4 + c] * tx;
            double b = pixels[(y1 * Width + x) * 4 + c] * (1-tx) + pixels[(y1 * Width + x1) * 4 + c] * tx;
            return (a * (1-ty) + b * ty) / 255;
        }
    }

    /// <summary>Single-valued radial terrain, not an arbitrary solid or an overhang model.</summary>
    public sealed class MappedRadialSurface
    {
        public RadialShape Shape { get; }
        public RadialMap HeightMap { get; }
        public double Offset { get; }
        public double Deformity { get; }
        public double MinimumRadius => Shape.MinimumRadius + Offset;
        public double BoundingRadius => Shape.BoundingRadius + Offset + Deformity;
        public MappedRadialSurface(RadialShape shape, RadialMap heightMap = null, double offset = 0, double deformity = 0)
        {
            if (shape == null || !Vector3d.Finite(offset) || !Vector3d.Finite(deformity) || deformity < 0
                || Math.Abs(offset) + deformity > shape.MinimumRadius * .25 || shape.MinimumRadius + offset < 100
                || (heightMap == null && deformity != 0)) throw new ArgumentException("Invalid radial terrain bounds or missing height map.");
            Shape = shape; HeightMap = heightMap; Offset = offset; Deformity = deformity;
        }
        public double RadiusAt(Vector3d direction) => Shape.RadiusAt(direction) + Offset + (HeightMap == null ? 0 : Deformity * HeightMap.Bilinear(direction).R);
        public Vector3d SurfacePoint(Vector3d direction) { var up = direction / direction.Length; return up * RadiusAt(up); }
        public bool TrySample(Vector3d position, double referenceRadius, out RadialSurfaceSample sample)
        {
            if (!RadialSurfaceQuery.TrySample(Shape, position, referenceRadius, out sample)) return false;
            if (HeightMap == null && Offset == 0) return true;
            Vector3d up = sample.RadialUp;
            // Tangent derivatives of the SAME radius field used by PQS. Bilinear maps are
            // only piecewise differentiable; this symmetric stencil defines boundary normals.
            Vector3d t = Vector3d.Cross(Math.Abs(up.Y) < .9 ? new Vector3d(0,1,0) : new Vector3d(1,0,0), up);
            t /= t.Length;
            Vector3d b = Vector3d.Cross(up,t);
            const double epsilon = 1e-5;
            Vector3d dt = SurfacePoint(up + t * epsilon) - SurfacePoint(up - t * epsilon);
            Vector3d db = SurfacePoint(up + b * epsilon) - SurfacePoint(up - b * epsilon);
            Vector3d normal = Vector3d.Cross(dt,db); normal /= normal.Length;
            double floor = RadiusAt(up);
            sample = new RadialSurfaceSample(up * floor, normal, up, sample.North, sample.East,
                position.Length - floor, sample.ReferenceAltitude, sample.LatitudeDegrees, sample.LongitudeDegrees, sample.HasHeading);
            return normal.IsFinite;
        }
    }
}
