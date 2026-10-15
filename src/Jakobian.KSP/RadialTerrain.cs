using Jakobian.Core;
using UnityEngine;

namespace Jakobian.KSP
{
    // Public fields survive Unity's prefab -> live PQS component instantiation.
    public sealed class RadialTerrain : PQSMod
    {
        public int shapeKind;
        public double axisX, axisY, axisZ, exponent, lobes;
        public Color surfaceColor;
        private RadialShape shape;
        private RadialShape Shape => shape ?? (shape = new RadialShape((RadialShapeKind)shapeKind, axisX, axisY, axisZ, exponent, lobes));
        public override void OnSetup() { shape = null; requirements = PQS.ModiferRequirements.MeshColorChannel; }
        public override void OnVertexBuildHeight(PQS.VertexBuildData data)
        {
            global::Vector3d d = data.directionFromCenter;
            data.vertHeight = Shape.RadiusAt(new Jakobian.Core.Vector3d(d.x, d.y, d.z));
        }
        public override void OnVertexBuild(PQS.VertexBuildData data) { data.vertColor = surfaceColor; data.allowScatter = false; }
        public override double GetVertexMinHeight() => Shape.MinimumRadius - sphere.radius;
        public override double GetVertexMaxHeight() => Shape.BoundingRadius - sphere.radius;
    }
}
