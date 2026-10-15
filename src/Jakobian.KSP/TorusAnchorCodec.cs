using System;
using System.Globalization;
using Jakobian.Core;

namespace Jakobian.KSP
{
    /// <summary>Experimental analytic-torus material record. Not a KSP vessel/scenario serializer.</summary>
    public static class TorusAnchorCodec
    {
        public static ConfigNode Save(string bodyId,TorusChart chart,TorusAnchor anchor)
        {
            if(!ValidId(bodyId) || chart == null || !chart.TryResolve(anchor,out _)) throw new ArgumentException("Invalid torus anchor context.");
            var node=new ConfigNode("JakobianTorusAnchor"); node.AddValue("schemaVersion","1"); node.AddValue("bodyId",bodyId);
            node.AddValue("surfaceId",TorusAnchor.SurfaceId); node.AddValue("chartId",TorusAnchor.ChartId);
            node.AddValue("majorRadius",Text(chart.MajorRadius)); node.AddValue("minorRadius",Text(chart.MinorRadius));
            node.AddValue("u",Text(anchor.U)); node.AddValue("v",Text(anchor.V)); node.AddValue("normalOffset",Text(anchor.NormalOffset)); return node;
        }
        public static bool TryLoad(ConfigNode node,string bodyId,TorusChart chart,out TorusAnchor anchor,out string error)
        {
            anchor=default(TorusAnchor); error=null;
            try
            {
                if(node == null || node.name != "JakobianTorusAnchor" || chart == null || !ValidId(bodyId)) throw new ArgumentException("Missing anchor context.");
                BodyDefinition.Check(node,"schemaVersion,bodyId,surfaceId,chartId,majorRadius,minorRadius,u,v,normalOffset","");
                if(node.GetValue("schemaVersion") != "1" || node.GetValue("bodyId") != bodyId || node.GetValue("surfaceId") != TorusAnchor.SurfaceId || node.GetValue("chartId") != TorusAnchor.ChartId)
                    throw new ArgumentException("Anchor identity or schema mismatch.");
                double Number(string key)
                {
                    double value=double.Parse(BodyDefinition.Required(node,key),NumberStyles.Float,CultureInfo.InvariantCulture);
                    if(!Jakobian.Core.Vector3d.Finite(value)) throw new ArgumentException("Nonfinite anchor value."); return value;
                }
                if(Number("majorRadius") != chart.MajorRadius || Number("minorRadius") != chart.MinorRadius) throw new ArgumentException("Anchor shape changed; migration required.");
                var parsed=new TorusAnchor(Number("u"),Number("v"),Number("normalOffset"));
                if(!chart.TryResolve(parsed,out _)) throw new ArgumentException("Anchor offset has folded or is ambiguous.");
                anchor=parsed; return true;
            }
            catch(Exception e) when(e is ArgumentException || e is FormatException || e is OverflowException) { error=e.Message; return false; }
        }
        private static string Text(double value) => value.ToString("G17",CultureInfo.InvariantCulture);
        private static bool ValidId(string value) => value != null && System.Text.RegularExpressions.Regex.IsMatch(value,"^[A-Za-z0-9][A-Za-z0-9_./-]{0,127}$");
    }
}
