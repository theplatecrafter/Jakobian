using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Jakobian.Core;

namespace Jakobian.KSP
{
    /// <summary>Owns a defensive copy. Invalid/future records are preserved, never adopted as current.</summary>
    public sealed class WorldSnapshot
    {
        public const string NodeName = "World";
        private readonly ConfigNode rawScenario;
        private readonly WorldCatalog saved;
        private readonly WorldCompatibility parsed;
        public string Diagnostic { get; }
        public bool HasRecordedWorld => rawScenario.HasNode(NodeName);
        internal WorldCatalog SavedCatalog => saved;
        internal WorldCompatibility ParsedStatus => parsed;
        private WorldSnapshot(ConfigNode raw, WorldCatalog catalog, WorldCompatibility status, string diagnostic)
        { rawScenario = raw; saved = catalog; parsed = status; Diagnostic = diagnostic; }
        public static WorldSnapshot Read(ConfigNode scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            ConfigNode raw = scenario.CreateCopy();
            if (Unknown(raw, "name,scene", "World,Extension")) return new WorldSnapshot(raw, null, WorldCompatibility.UnsupportedRecord, "Unknown scenario fields; original record retained. Supplemental metadata belongs in Extension.");
            if (raw.values.Cast<ConfigNode.Value>().GroupBy(v => v.name, StringComparer.Ordinal).Any(g => g.Count() > 1))
                return new WorldSnapshot(raw, null, WorldCompatibility.InvalidRecord, "Duplicate scenario values; original record retained.");
            ConfigNode[] worlds = raw.GetNodes(NodeName);
            if (worlds.Length == 0) return new WorldSnapshot(raw, null, WorldCompatibility.LegacyUnverified, "No recorded baseline; historical world identity is unverified.");
            try
            {
                if (worlds.Length != 1) throw new ArgumentException("Duplicate World records.");
                ConfigNode world = worlds[0];
                var versionValues = world.values.Cast<ConfigNode.Value>().Where(v => v.name == "schemaVersion").ToArray();
                if (versionValues.Length != 1) throw new ArgumentException("Missing or duplicate schema version.");
                if (versionValues[0].value != "1") return new WorldSnapshot(raw, null, WorldCompatibility.UnsupportedSchema, "Unsupported saved-world schema; original record retained.");
                if (Unknown(world, "schemaVersion,adoption,adoptedUtc,catalogFingerprint", "Body,Extension")) return new WorldSnapshot(raw, null, WorldCompatibility.UnsupportedRecord, "Unknown world fields; original record retained.");
                Check(world, "schemaVersion,adoption,adoptedUtc,catalogFingerprint", "Body,Extension");
                Check(raw, "name,scene", "World,Extension");
                if (Token(world, "adoption") != "explicit-current") throw new ArgumentException("Unknown adoption provenance.");
                var bodies = new List<WorldBodyDescriptor>();
                if (world.GetNodes("Body").Length > 1024) throw new ArgumentException("Excessive saved body count.");
                foreach (ConfigNode body in world.GetNodes("Body"))
                {
                    if (Unknown(body, "id,nativeName,nativeIndex,fingerprint", "Property,Extension")) return new WorldSnapshot(raw, null, WorldCompatibility.UnsupportedRecord, "Unknown body fields; original record retained.");
                    Check(body, "id,nativeName,nativeIndex,fingerprint", "Property,Extension");
                    int index = int.Parse(Token(body, "nativeIndex"), NumberStyles.None, CultureInfo.InvariantCulture);
                    var properties = new List<WorldProperty>();
                    if (body.GetNodes("Property").Length > 1024) throw new ArgumentException("Excessive saved property count.");
                    foreach (ConfigNode p in body.GetNodes("Property"))
                    {
                        if (Unknown(p, "key,valueBase64", "")) return new WorldSnapshot(raw, null, WorldCompatibility.UnsupportedRecord, "Unknown property fields; original record retained.");
                        Check(p, "key,valueBase64", "");
                        string encoded = p.GetValue("valueBase64");
                        if (encoded == null || encoded.Length > 24000) throw new ArgumentException("Invalid encoded property size.");
                        string value = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(encoded));
                        properties.Add(new WorldProperty(Token(p, "key"), value));
                    }
                    var descriptor = new WorldBodyDescriptor(Token(body, "id"), Token(body, "nativeName"), index, properties);
                    if (!string.Equals(descriptor.Fingerprint, Token(body, "fingerprint"), StringComparison.Ordinal)) throw new ArgumentException("Saved descriptor fingerprint mismatch.");
                    bodies.Add(descriptor);
                }
                var catalog = new WorldCatalog(bodies);
                if (catalog.Fingerprint != Token(world, "catalogFingerprint")) throw new ArgumentException("Saved catalog fingerprint mismatch.");
                return new WorldSnapshot(raw, catalog, WorldCompatibility.Compatible, null);
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is OverflowException)
            { return new WorldSnapshot(raw, null, WorldCompatibility.InvalidRecord, e.Message); }
        }
        public WorldReconciliation Reconcile(WorldCatalog current) => current == null ? WorldReconciliation.State(WorldCompatibility.Unavailable)
            : parsed == WorldCompatibility.Compatible ? WorldReconciliation.Compare(saved, current) : WorldReconciliation.State(parsed);
        public bool TryAdoptCurrent(WorldCatalog current, out WorldSnapshot adopted)
        {
            adopted = null;
            if (current == null || parsed != WorldCompatibility.LegacyUnverified) return false;
            ConfigNode raw = rawScenario.CreateCopy(); var world = raw.AddNode(NodeName);
            world.AddValue("schemaVersion", "1"); world.AddValue("adoption", "explicit-current"); world.AddValue("adoptedUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            world.AddValue("catalogFingerprint", current.Fingerprint);
            foreach (var body in current.Bodies) world.AddNode(WriteBody(body));
            adopted = Read(raw); return adopted.parsed == WorldCompatibility.Compatible;
        }
        public ConfigNode WriteScenario(WorldCatalog current)
        {
            ConfigNode raw = rawScenario.CreateCopy();
            // Additions are recorded only after every saved identity and property matches.
            if (Reconcile(current).Status == WorldCompatibility.Additive)
            {
                var ids = new HashSet<string>(saved.Bodies.Select(b => b.Id), StringComparer.Ordinal);
                foreach (var body in current.Bodies) if (!ids.Contains(body.Id)) raw.GetNode(NodeName).AddNode(WriteBody(body));
                raw.GetNode(NodeName).SetValue("catalogFingerprint", current.Fingerprint, true);
            }
            return raw;
        }
        internal static ConfigNode WriteBody(WorldBodyDescriptor body)
        {
            var node = new ConfigNode("Body"); node.AddValue("id", body.Id); node.AddValue("nativeName", body.NativeName);
            node.AddValue("nativeIndex", body.NativeIndex.ToString(CultureInfo.InvariantCulture)); node.AddValue("fingerprint", body.Fingerprint);
            foreach (var p in body.Properties)
            {
                var property = node.AddNode("Property"); property.AddValue("key", p.Key);
                property.AddValue("valueBase64", Convert.ToBase64String(new UTF8Encoding(false, true).GetBytes(p.Value)));
            }
            return node;
        }
        private static bool Unknown(ConfigNode node, string values, string children) => node.values.Cast<ConfigNode.Value>().Any(v => !values.Split(',').Contains(v.name))
            || node.nodes.Cast<ConfigNode>().Any(n => !children.Split(',').Contains(n.name));
        private static string Token(ConfigNode node, string key)
        {
            string text = node.GetValue(key);
            if (string.IsNullOrEmpty(text) || text != text.Trim()) throw new ArgumentException("Missing/noncanonical saved token: " + key);
            return text;
        }
        private static void Check(ConfigNode node, string values, string children)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ConfigNode.Value value in node.values)
                if (!values.Split(',').Contains(value.name) || !seen.Add(value.name)) throw new ArgumentException("Unknown/duplicate saved value: " + value.name);
            foreach (ConfigNode child in node.nodes)
                if (!children.Split(',').Contains(child.name)) throw new ArgumentException("Unknown saved node: " + child.name);
        }
    }
}
