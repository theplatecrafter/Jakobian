using System;

namespace Jakobian.Core
{
    public readonly struct Vector3d
    {
        public readonly double X, Y, Z;
        public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);
        public static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3d operator -(Vector3d a) => a * -1;
        public static Vector3d operator *(Vector3d a, double b) => new Vector3d(a.X * b, a.Y * b, a.Z * b);
        public static Vector3d operator /(Vector3d a, double b) => a * (1 / b);
        public static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Vector3d Cross(Vector3d a, Vector3d b) => new Vector3d(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }
}
