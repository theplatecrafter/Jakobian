using System;

namespace Jakobian.Core
{
    public static class ReferencePhysics
    {
        /// <summary>Monopole only. mu in m^3/s^2, result in m/s^2. Singular inputs fail.</summary>
        public static bool TryPointGravity(Vector3d p, double mu, out Vector3d acceleration)
        {
            acceleration = default(Vector3d);
            double radius = p.Length;
            if (!p.IsFinite || !Vector3d.Finite(mu) || mu < 0 || !Vector3d.Finite(radius) || radius < 1e-12) return false;
            acceleration = (p / radius) * (-mu / radius / radius);
            return acceleration.IsFinite;
        }

        /// <summary>All inputs expressed in the same rotating axes. Transform gravity into these axes first.</summary>
        public static Vector3d RotatingAcceleration(Vector3d gravity, Vector3d originAcceleration, Vector3d omega,
            Vector3d omegaDot, Vector3d position, Vector3d rotatingVelocity)
        {
            if (!gravity.IsFinite || !originAcceleration.IsFinite || !omega.IsFinite || !omegaDot.IsFinite || !position.IsFinite || !rotatingVelocity.IsFinite)
                throw new ArgumentException("Reference-frame inputs must be finite.");
            Vector3d result = gravity - originAcceleration - Vector3d.Cross(omega, rotatingVelocity) * 2
                - Vector3d.Cross(omega, Vector3d.Cross(omega, position)) - Vector3d.Cross(omegaDot, position);
            if (!result.IsFinite) throw new ArgumentException("Reference-frame acceleration exceeds numeric range.");
            return result;
        }
    }
}
