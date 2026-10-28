using System;
using System.Collections.Generic;
using Jakobian.Core;
using UnityEngine;
using CoreVector = Jakobian.Core.Vector3d;
using WorldVector = global::Vector3d;

namespace Jakobian.KSP
{
    internal static class GeneralContextNativeChecks
    {
        internal static void Run(Transform root, IList<MeshCollider> colliders)
        {
            var geometry = new AnalyticSolid(1000, 200);
            var settings = new GeneralQuerySettings(1000, 5000, 1e-5, 4096);
            // Oracle vectors/distances are independent of the context's frame conversion.
            WorldVector Vector(Vector3 v) => (WorldVector)root.TransformDirection(v);
            WorldVector Point(Vector3 v) => (WorldVector)root.position + Vector(v);
            GeneralBodyPointSample Sample(Vector3 p, Vector3 g)
            {
                if (GeneralBodyQueries.Sample(root, geometry, Point(p), Vector(g), Vector(Vector3.up), settings, out var sample) != GeneralBodyQueryStatus.Available)
                    throw new InvalidOperationException("General context bridge unavailable.");
                return sample;
            }
            void Check(Vector3 p, Vector3 g, double expected, Vector3 localNormal)
            {
                var sample = Sample(p, g);
                if (sample.Local.GroundStatus != GroundQueryStatus.Contact || !sample.GroundPointWorld.HasValue
                    || Math.Abs(sample.Local.GroundClearance.Value - expected) > .002
                    || (sample.UpWorld.Value - Vector(-g.normalized)).magnitude > 1e-5
                    || (sample.GroundNormalWorld.Value - Vector(localNormal)).magnitude > 1e-4)
                    throw new InvalidOperationException("General context transformed point/vertical/normal mismatch.");
                var ray = new Ray((Vector3)Point(p), (Vector3)Vector(g.normalized));
                double closest = double.PositiveInfinity;
                Vector3 normal = Vector3.zero;
                foreach (MeshCollider collider in colliders)
                    if (collider.Raycast(ray, out var hit, 5000) && hit.distance < closest) { closest = hit.distance; normal = hit.normal; }
                if (Math.Abs(closest - expected) > 2 || Vector3.Angle(normal, (Vector3)sample.GroundNormalWorld.Value) > 4
                    || (sample.GroundPointWorld.Value - (Point(p) + Vector(g.normalized) * expected)).magnitude > .002)
                    throw new InvalidOperationException("General context disagrees with native collider distance/orientation.");
            }
            Check(new Vector3(1250, 0, 0), Vector3.left, 50, Vector3.right);
            Check(new Vector3(750, 0, 0), Vector3.left, 1550, Vector3.right);
            Check(new Vector3(1000, 250, 0), Vector3.down, 50, Vector3.up);
            Vector3 savedPosition = root.position; Quaternion savedRotation = root.rotation;
            try
            {
                root.position += new Vector3(-3000, 250, 6000); root.rotation = Quaternion.Euler(-70, 140, 85); Physics.SyncTransforms();
                Check(new Vector3(1250, 0, 0), Vector3.left, 50, Vector3.right);
                Check(new Vector3(750, 0, 0), Vector3.left, 1550, Vector3.right);
                Check(new Vector3(1000, 250, 0), Vector3.down, 50, Vector3.up);
            }
            finally { root.position = savedPosition; root.rotation = savedRotation; Physics.SyncTransforms(); }
            var sphereObject = new GameObject("Jakobian query sphere comparison");
            try
            {
                sphereObject.transform.SetParent(root, false);
                SphereCollider sphereCollider = sphereObject.AddComponent<SphereCollider>(); sphereCollider.radius = 200;
                Physics.SyncTransforms();
                WorldVector sphereOrigin = Point(new Vector3(250, 0, 0));
                if (GeneralBodyQueries.Sample(root, new AnalyticSolid(200), sphereOrigin, Vector(Vector3.left), Vector(Vector3.up), settings, out var sphere) != GeneralBodyQueryStatus.Available
                    || !sphere.Local.Ground.HasValue || Math.Abs(sphere.Local.GroundClearance.Value - 50) > .002
                    || !sphereCollider.Raycast(new Ray((Vector3)sphereOrigin, (Vector3)Vector(Vector3.left)), out var sphereHit, 1000)
                    || Math.Abs(sphereHit.distance - 50) > .01 || Vector3.Angle(sphereHit.normal, (Vector3)sphere.GroundNormalWorld.Value) > .01)
                    throw new InvalidOperationException("General sphere context disagrees with native sphere collider.");
            }
            finally { sphereObject.SetActive(false); UnityEngine.Object.Destroy(sphereObject); }
            var inner = Sample(new Vector3(750, 0, 0), Vector3.left);
            if (Math.Abs(inner.Local.SignedDistance - 50) > .002 || Math.Abs(inner.Local.ReferenceAltitude + 250) > .002
                || WorldVector.Dot(inner.UpWorld.Value, inner.NearestNormalWorld.Value) > -.9999)
                throw new InvalidOperationException("General context conflates datum, nearest and vertical.");
            var hole = Sample(Vector3.zero, Vector3.down);
            if (hole.Local.GroundStatus != GroundQueryStatus.Miss || hole.Local.GroundClearance.HasValue
                || hole.GroundPointWorld.HasValue || hole.NearestPointWorld.HasValue)
                throw new InvalidOperationException("General context invents axial ground or nearest point.");
            var zero = Sample(new Vector3(1250, 0, 0), Vector3.zero);
            if (zero.Local.GroundStatus != GroundQueryStatus.NoGravity || zero.UpWorld.HasValue || !zero.NearestPointWorld.HasValue)
                throw new InvalidOperationException("General context zero-gravity availability mismatch.");
            WorldVector origin = Point(new Vector3(1250, 0, 0)), gravity = Vector(Vector3.left), north = Vector(Vector3.up);
            if (GeneralBodyQueries.Sample(null, geometry, origin, gravity, north, settings, out _) != GeneralBodyQueryStatus.InvalidFrame
                || GeneralBodyQueries.Sample(root, geometry, new WorldVector(double.NaN, 0, 0), gravity, north, settings, out _) != GeneralBodyQueryStatus.InvalidRequest)
                throw new InvalidOperationException("General context invalid inputs accepted.");
            Vector3 scale = root.localScale;
            try
            {
                foreach (Vector3 invalid in new[] { new Vector3(2, 1, 1), new Vector3(-1, 1, 1), new Vector3(0, 1, 1) })
                {
                    root.localScale = invalid;
                    if (GeneralBodyQueries.Sample(root, geometry, origin, gravity, north, settings, out _) != GeneralBodyQueryStatus.InvalidFrame)
                        throw new InvalidOperationException("General context scaled/reflected/singular frame accepted.");
                }
            }
            finally { root.localScale = scale; Physics.SyncTransforms(); }
            var parent = new GameObject("Jakobian query frame rejection test");
            try
            {
                var child = new GameObject("unit columns with parent shear"); child.transform.SetParent(parent.transform, false);
                parent.transform.localScale = new Vector3(2, 1, 1);
                child.transform.localRotation = Quaternion.Euler(0, 0, 45);
                child.transform.localScale = new Vector3((float)(1 / Math.Sqrt(2.5)), (float)(1 / Math.Sqrt(2.5)), 1);
                if (GeneralBodyQueries.Sample(child.transform, geometry, origin, gravity, north, settings, out _) != GeneralBodyQueryStatus.InvalidFrame)
                    throw new InvalidOperationException("General context accepted sheared parent frame with unit-length columns.");
            }
            finally { parent.SetActive(false); UnityEngine.Object.Destroy(parent); }
            Debug.Log("[Jakobian] GENERAL_CONTEXT_NATIVE_PASS torusContacts=6 sphere=passed hole=miss innerNearest=50 innerGround=1550 datum=-250 frames=2 invalidFrames=scale-reflection-singular-parentShear zeroGravity=explicit");
        }
    }
}
