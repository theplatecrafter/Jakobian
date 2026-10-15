using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Jakobian.Core;
using UnityEngine;

namespace Jakobian.KSP
{
    internal sealed class BodyDefinition
    {
        internal string Name, Id, Template, Parent, Description;
        internal RadialShape Shape;
        internal SurfaceAssets Assets;
        internal MappedRadialSurface Surface => Assets.Surface;
        internal double Mu, OrbitRadius, Inclination, Phase, Soi, RotationPeriod;
        internal int Index;
        internal Color Color;

        internal static BodyDefinition Parse(ConfigNode node)
        {
            Check(node, "name,id,flightGlobalsIndex,red,green,blue", "Template,Properties,Orbit,Shape,Terrain");
            Check(node.GetNode("Template"), "name", "");
            Check(node.GetNode("Properties"), "radius,gravParameter,sphereOfInfluence,description,rotates,rotationPeriod", "");
            Check(node.GetNode("Orbit"), "referenceBody,semiMajorAxis,inclination,meanAnomalyAtEpoch,eccentricity", "");
            Check(node.GetNode("Shape"), "type,axisX,axisY,axisZ,exponent,lobes", "");
            string name = Required(node, "name"), id = Required(node, "id");
            if (!Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_]{0,63}$") || !Regex.IsMatch(id, "^[A-Za-z0-9][A-Za-z0-9_./-]{0,127}$"))
                throw new ArgumentException("Body name or stable id contains invalid characters.");
            ConfigNode shape = node.GetNode("Shape"), properties = node.GetNode("Properties"), orbit = node.GetNode("Orbit");
            RadialShapeKind kind;
            if (!Enum.TryParse(shape == null ? "Sphere" : Required(shape, "type"), false, out kind))
                throw new ArgumentException(name + ": this build supports Sphere, Ellipsoid, Superellipsoid and Peanut only.");
            double radius = Number(properties, "radius", 5000), x = Number(shape, "axisX", radius), y = Number(shape, "axisY", radius), z = Number(shape, "axisZ", radius);
            var definition = new BodyDefinition {
                Name = name, Id = id, Template = node.GetNode("Template")?.GetValue("name") ?? "Minmus",
                Parent = orbit?.GetValue("referenceBody") ?? "Kerbin", Description = properties?.GetValue("description") ?? "Jakobian development test body.",
                Shape = new RadialShape(kind, x, y, z, Number(shape, "exponent", 2), Number(shape, "lobes", .4)),
                Mu = Number(properties, "gravParameter", 50000000), OrbitRadius = Number(orbit, "semiMajorAxis", 20000000),
                Inclination = Number(orbit, "inclination", 0), Phase = Number(orbit, "meanAnomalyAtEpoch", 0),
                Soi = Number(properties, "sphereOfInfluence", 250000), Index = (int)Number(node, "flightGlobalsIndex", -1),
                RotationPeriod = Number(properties, "rotationPeriod", 21600),
                Color = new Color((float)Number(node, "red", .5), (float)Number(node, "green", .6), (float)Number(node, "blue", .8), 1)
            };
            if (definition.Template != "Minmus") throw new ArgumentException(name + ": initial airless backend requires Template Minmus.");
            if (definition.Index < 17 || Number(node, "flightGlobalsIndex", -1) != definition.Index || definition.Mu <= 0 || definition.OrbitRadius <= definition.Shape.BoundingRadius * 20 || definition.Soi < definition.Shape.BoundingRadius * 5
                || definition.Color.r < 0 || definition.Color.r > 1 || definition.Color.g < 0 || definition.Color.g > 1 || definition.Color.b < 0 || definition.Color.b > 1)
                throw new ArgumentException(name + ": invalid index, gravity, orbit or SOI bounds.");
            // Reject unsupported dynamics/environment rather than silently giving them different meanings.
            if (Number(orbit, "eccentricity", 0) != 0
                || node.HasNode("Environment") || node.HasNode("Atmosphere") || node.HasNode("Ocean") || node.HasNode("PQS") || node.HasNode("Gravity") || node.HasNode("Rotation"))
                throw new ArgumentException(name + ": current radial backend is circular-orbit, airless and dry.");
            bool rotates = true;
            if (properties != null && properties.HasValue("rotates") && !bool.TryParse(properties.GetValue("rotates"), out rotates)) throw new ArgumentException(name + ": invalid rotates boolean.");
            if (!rotates) definition.RotationPeriod = 0;
            else if (definition.RotationPeriod < 60) throw new ArgumentException(name + ": rotating bodies require rotationPeriod >= 60 seconds.");
            definition.Assets = SurfaceAssets.Load(definition,node.GetNode("Terrain"));
            if (definition.OrbitRadius <= definition.Surface.BoundingRadius * 20 || definition.Soi < definition.Surface.BoundingRadius * 5)
                throw new ArgumentException(name + ": terrain exceeds orbit/SOI bounds.");
            return definition;
        }

        internal static void Check(ConfigNode node, string values, string children)
        {
            if (node == null) return;
            string[] allowedValues = values.Split(','), allowedChildren = children.Split(',');
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (ConfigNode.Value value in node.values)
                if (Array.IndexOf(allowedValues, value.name) < 0 || !seen.Add(value.name)) throw new ArgumentException("Unsupported or duplicate value " + node.name + "/" + value.name);
            seen.Clear();
            foreach (ConfigNode child in node.nodes)
                if (Array.IndexOf(allowedChildren, child.name) < 0 || ((child.name != "Body" && child.name != "Biome") && !seen.Add(child.name))) throw new ArgumentException("Unsupported or duplicate node " + node.name + "/" + child.name);
        }

        internal static string Required(ConfigNode node, string key)
        {
            string value = node?.GetValue(key);
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Missing " + key + " in " + node?.name);
            return value.Trim();
        }
        internal static double Number(ConfigNode node, string key, double fallback)
        {
            string text = node?.GetValue(key);
            if (text == null) return fallback;
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !Jakobian.Core.Vector3d.Finite(value))
                throw new ArgumentException("Invalid finite number: " + node.name + "/" + key);
            return value;
        }
    }
}
