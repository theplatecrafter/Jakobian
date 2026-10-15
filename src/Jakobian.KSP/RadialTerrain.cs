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
        public string bodyName;
        private RadialShape shape;
        private RadialShape Shape => shape ?? (shape = new RadialShape((RadialShapeKind)shapeKind, axisX, axisY, axisZ, exponent, lobes));
        private MappedRadialSurface surface;
        private bool mapped;
        private MappedRadialSurface Surface
        {
            get
            {
                if (surface == null)
                {
                    BodyDefinition definition;
                    surface = bodyName != null && SystemLoader.Definitions.TryGetValue(bodyName,out definition) ? definition.Surface : new MappedRadialSurface(Shape);
                }
                return surface;
            }
        }
        public override void OnSetup()
        {
            shape = null; surface = null;
            BodyDefinition definition;
            mapped = bodyName != null && SystemLoader.Definitions.TryGetValue(bodyName,out definition) && definition.Assets.AlbedoTexture != null;
            requirements = PQS.ModiferRequirements.MeshColorChannel;
            if (mapped) requirements |= PQS.ModiferRequirements.MeshUV3 | PQS.ModiferRequirements.MeshUV4;
        }
        public override void OnVertexBuildHeight(PQS.VertexBuildData data)
        {
            global::Vector3d d = data.directionFromCenter;
            data.vertHeight = Surface.RadiusAt(new Jakobian.Core.Vector3d(d.x, d.y, d.z));
        }
        public override void OnVertexBuild(PQS.VertexBuildData data)
        {
            data.vertColor = surfaceColor; data.allowScatter = false;
            // Native UVPlanetRelativePosition rewrites UV/UV2 after vertex modifiers.
            // Reserve UV3/UV4 for our direction; leave native shader channels intact.
            if (mapped) { var d = data.directionFromCenter.normalized; data.u3 = d.x; data.v3 = d.y; data.u4 = d.z; data.v4 = 0; }
        }
        public override double GetVertexMinHeight() => Surface.MinimumRadius - sphere.radius;
        public override double GetVertexMaxHeight() => Surface.BoundingRadius - sphere.radius;
    }
}
