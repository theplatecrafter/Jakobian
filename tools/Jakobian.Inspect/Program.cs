using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Mono.Cecil;

if (args.Length < 2 || args.Length > 3) throw new ArgumentException("Usage: Jakobian.Inspect <KSP root> <output.json> [--api | --types=fragment | --il=Type::Method]");
string typeFilter = args.Length == 3 && args[2].StartsWith("--types=") ? args[2].Substring(8) : null;
bool fullApi = args.Length == 3 && (args[2] == "--api" || typeFilter != null);
string ilTarget = args.Length == 3 && args[2].StartsWith("--il=") ? args[2].Substring(5) : null;
string root = Path.GetFullPath(args[0]);
string[] wanted = { "CelestialBody", "PQS", "PQS_GameBindings", "PQSMod", "PQSMod_CelestialBodyTransform", "PQSMod_QuadMeshColliders", "PSystem", "PSystemBody", "PSystemManager", "ScaledSpaceFader", "FlightCamera", "NavBall", "Altimeter", "AltimeterSliderButtons", "FlightUIController", "ResourceMap", "ModuleResourceHarvester", "ModuleResourceIntake", "Vessel", "VesselPrecalculate", "FlightIntegrator", "PartBuoyancy", "KerbalEVA", "Orbit", "OrbitDriver", "FlightGlobals", "Krakensbane", "FloatingOrigin", "ConfigNode" };
string[] methods = { "GetAltitude", "GetSurfaceHeight", "GetWorldSurfacePosition", "GetSurfaceNVector", "GetPressure", "GetDensity", "CalculateGravity", "CalculatePhysicsStats", "CalculatePressure", "CalculateConstantsAtmosphere", "UpdateCaches", "UpdatePosVel", "GoOnRails", "GoOffRails", "checkLanded", "GetHeightFromSurface", "FixedUpdate", "UpdateOrbit", "getGeeForceAtPosition", "SafeToEngage" };
var assemblies = new List<object>();
foreach (string name in new[] { "Assembly-CSharp.dll", "Assembly-CSharp-firstpass.dll", "UnityEngine.CoreModule.dll", "UnityEngine.PhysicsModule.dll" })
{
    string file = Path.Combine(root, "KSP_x64_Data", "Managed", name);
    using var assembly = AssemblyDefinition.ReadAssembly(file);
    assemblies.Add(new {
        file = name, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant(),
        identity = assembly.Name.FullName, runtime = assembly.MainModule.RuntimeVersion,
        references = assembly.MainModule.AssemblyReferences.Select(a => a.FullName).ToArray(),
        types = assembly.MainModule.Types.Where(t => typeFilter != null ? t.FullName.IndexOf(typeFilter, StringComparison.OrdinalIgnoreCase) >= 0
            : wanted.Contains(t.Name) || t.Name == "CameraManager" || t.Name == "AbundanceRequest" || (ilTarget != null && ilTarget.StartsWith(t.Name + "::"))).Select(t => new {
            name = t.FullName,
            fields = t.Fields.Where(f => fullApi || new[] { "Radius", "gravParameter", "altitude", "terrainAltitude", "heightFromTerrain", "upAxis", "north", "east", "geeForce", "staticPressurekPa", "atmDensity", "srf_velocity", "Landed", "Splashed", "packed", "pqsController" }.Contains(f.Name)).Select(f => f.FullName).ToArray(),
            properties = fullApi ? t.Properties.Select(p => p.FullName).ToArray() : Array.Empty<string>(),
            nestedFields = fullApi ? t.NestedTypes.Select(n => new { name = n.Name, fields = n.Fields.Select(f => f.FullName).ToArray() }).ToArray() : null,
            methods = t.Methods.Where(m => fullApi || methods.Contains(m.Name) || (ilTarget != null && (t.Name + "::" + m.Name) == ilTarget)).Select(m => new {
                signature = m.FullName, visibility = m.IsPublic ? "public" : m.IsFamily ? "protected" : "nonpublic", virtualMethod = m.IsVirtual, staticMethod = m.IsStatic,
                il = ilTarget != null && (t.Name + "::" + m.Name) == ilTarget && m.HasBody ? m.Body.Instructions.Select(i => i.ToString()).ToArray() : null,
                calls = m.HasBody ? m.Body.Instructions.Where(i => i.Operand is MethodReference).Select(i => ((MethodReference)i.Operand).FullName).Distinct().ToArray() : Array.Empty<string>()
            }).ToArray()
        }).ToArray()
    });
}
var report = new {
    inspection = "Metadata and IL call references only; game code was not executed.",
    build = File.ReadAllText(Path.Combine(root, "buildID64.txt")),
    unityPlayerFileVersion = FileVersionInfo.GetVersionInfo(Path.Combine(root, "UnityPlayer.dll")).FileVersion,
    assemblies
};
string output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine("Inspected " + assemblies.Count + " assemblies -> " + output);
