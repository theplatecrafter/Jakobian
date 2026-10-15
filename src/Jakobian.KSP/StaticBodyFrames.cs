using System.Collections.Generic;
using UnityEngine;

namespace Jakobian.KSP
{
    [DefaultExecutionOrder(-10000)]
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class StaticBodyFrames : MonoBehaviour
    {
        private static readonly List<CelestialBody> bodies = new List<CelestialBody>();
        private void Awake() { DontDestroyOnLoad(gameObject); }
        internal static void Register(CelestialBody body) { if (!bodies.Contains(body)) bodies.Add(body); Publish(body); }
        private void Update() { Refresh(); }
        private void FixedUpdate() { Refresh(); }
        private static void Refresh() { foreach (CelestialBody body in bodies) if (body != null) Publish(body); }

        private static void Publish(CelestialBody body)
        {
            // CBUpdate initializes this native matrix only in its rotating-body
            // branch. Static bodies still need a valid frame and must follow the
            // universe's inverse rotation without acquiring physical spin.
            body.directRotAngle = -Planetarium.InverseRotAngle % 360;
            Planetarium.CelestialFrame.PlanetaryFrame(0, 90, body.directRotAngle, ref body.BodyFrame);
            body.rotation = body.BodyFrame.Rotation.swizzle;
            if (body.bodyTransform != null) body.bodyTransform.rotation = body.rotation;
            body.transformRight = body.transform.right; body.transformUp = body.transform.up;
        }
    }
}
