using System;
using System.Linq;
using Jakobian.Core;

namespace Jakobian.KSP
{
    public sealed class TorusPreviewConfig
    {
        public TorusBodyDefinition Definition { get; }
        public int SegmentsU { get; }
        public int SegmentsV { get; }
        private TorusPreviewConfig(TorusBodyDefinition definition, int u, int v) { Definition = definition; SegmentsU = u; SegmentsV = v; }
        public static TorusPreviewConfig Parse(ConfigNode node)
        {
            if (node == null || node.name != "JakobianPreview") throw new ArgumentException("Expected JakobianPreview node.");
            BodyDefinition.Check(node, "schemaVersion,capability,name,id,flightGlobalsIndex", "Shape,Properties,Orbit,Preview");
            if (node.GetValue("schemaVersion") != "1" || node.GetValue("capability") != "preview-only") throw new ArgumentException("Only schema 1 preview-only capability is supported.");
            foreach (string child in new[] { "Shape", "Properties", "Orbit", "Preview" }) if (!node.HasNode(child)) throw new ArgumentException("Missing " + child);
            var shape = node.GetNode("Shape"); var properties = node.GetNode("Properties"); var orbit = node.GetNode("Orbit"); var preview = node.GetNode("Preview");
            BodyDefinition.Check(shape, "type,majorRadius,minorRadius", ""); BodyDefinition.Check(properties, "gravParameter,sphereOfInfluence", "");
            BodyDefinition.Check(orbit, "referenceBody,semiMajorAxis,inclination,meanAnomalyAtEpoch", ""); BodyDefinition.Check(preview, "segmentsU,segmentsV", "");
            if (shape.GetValue("type") != "Torus") throw new ArgumentException("Preview supports Torus only.");
            double RequiredNumber(ConfigNode n, string key) { BodyDefinition.Required(n, key); return BodyDefinition.Number(n, key, double.NaN); }
            int Integer(ConfigNode n, string key, int min, int max)
            {
                double value = RequiredNumber(n, key);
                if (value < min || value > max || value != Math.Floor(value)) throw new ArgumentException("Invalid integer " + key);
                return (int)value;
            }
            string parentName = BodyDefinition.Required(orbit, "referenceBody");
            CelestialBody parent = FlightGlobals.Bodies.SingleOrDefault(b => b.bodyName == parentName);
            if (parent == null) throw new ArgumentException("Missing native parent " + parentName);
            var definition = new TorusBodyDefinition(BodyDefinition.Required(node, "id"), BodyDefinition.Required(node, "name"), Integer(node, "flightGlobalsIndex", 17, int.MaxValue),
                RequiredNumber(shape, "majorRadius"), RequiredNumber(shape, "minorRadius"), RequiredNumber(properties, "gravParameter"), RequiredNumber(properties, "sphereOfInfluence"),
                parentName, parent.flightGlobalsIndex, RequiredNumber(orbit, "semiMajorAxis"), RequiredNumber(orbit, "inclination"), RequiredNumber(orbit, "meanAnomalyAtEpoch"));
            return new TorusPreviewConfig(definition, Integer(preview, "segmentsU", 8, 256), Integer(preview, "segmentsV", 8, 128));
        }
    }
}
