using System;
using System.IO;
using System.Linq;
using Jakobian.Core;
using UnityEngine;

namespace Jakobian.KSP
{
    internal static class WorldPersistenceChecks
    {
        internal static void Run()
        {
            WorldCatalog current = SystemLoader.CurrentWorld;
            if (current == null || current.Bodies.Count != 5) throw new InvalidOperationException("Expected five compiled world descriptors.");
            var raw = new ConfigNode("SCENARIO"); raw.AddValue("name", nameof(WorldScenario)); raw.AddNode("Extension").AddValue("foreignMetadata", "preserve-me");
            var legacy = WorldSnapshot.Read(raw);
            Require(legacy.Reconcile(current).Status == WorldCompatibility.LegacyUnverified, "Legacy silently adopted.");
            Require(!legacy.WriteScenario(current).HasNode("World"), "Saving legacy silently adopted it.");
            var futureScenario = raw.CreateCopy(); futureScenario.AddNode("FutureWorld").AddValue("schemaVersion", "2");
            var unsupported = WorldSnapshot.Read(futureScenario);
            Require(unsupported.Reconcile(current).Status == WorldCompatibility.UnsupportedRecord && !unsupported.TryAdoptCurrent(current, out _)
                && unsupported.WriteScenario(current).ToString() == futureScenario.ToString(), "Unknown scenario data was adopted or lost.");
            Require(legacy.TryAdoptCurrent(current, out var adopted), "Explicit adoption failed.");
            ConfigNode adoptedRaw = adopted.WriteScenario(current);
            adoptedRaw.GetNode("World").AddNode("Extension").AddValue("futureText", "opaque");
            adoptedRaw.GetNode("World").GetNodes("Body")[0].AddNode("Extension").AddValue("tag", "retained");
            var baseline = WorldSnapshot.Read(adoptedRaw);
            Require(baseline.Reconcile(current).Status == WorldCompatibility.Compatible, "Adopted baseline mismatch.");
            ConfigNode copy = baseline.WriteScenario(current); copy.GetNode("World").SetValue("schemaVersion", "9", true);
            var future = WorldSnapshot.Read(copy);
            Require(future.Reconcile(current).Status == WorldCompatibility.UnsupportedSchema && !future.TryAdoptCurrent(current, out _), "Future schema adopted.");
            Require(future.WriteScenario(current).ToString() == copy.ToString(), "Future record lost data.");
            copy = baseline.WriteScenario(current); copy.GetNode("World").AddValue("futureBehavior", "unknown");
            var unknown = WorldSnapshot.Read(copy);
            Require(unknown.Reconcile(current).Status == WorldCompatibility.UnsupportedRecord && unknown.WriteScenario(current).ToString() == copy.ToString(), "Unknown field discarded or treated as compatible.");
            copy = baseline.WriteScenario(current); copy.AddNode(copy.GetNode("World").CreateCopy());
            Require(WorldSnapshot.Read(copy).Reconcile(current).Status == WorldCompatibility.InvalidRecord, "Duplicate world accepted.");
            copy = baseline.WriteScenario(current); copy.GetNode("World").GetNodes("Body")[0].SetValue("fingerprint", "tampered", true);
            var invalid = WorldSnapshot.Read(copy);
            Require(invalid.Reconcile(current).Status == WorldCompatibility.InvalidRecord && invalid.WriteScenario(current).ToString() == copy.ToString() && !invalid.TryAdoptCurrent(current, out _), "Invalid fingerprint adopted/discarded.");
            copy = baseline.WriteScenario(current); copy.GetNode("World").RemoveNode(copy.GetNode("World").GetNodes("Body")[0]);
            Require(WorldSnapshot.Read(copy).Reconcile(current).Status == WorldCompatibility.InvalidRecord, "Missing descriptor evaded catalog fingerprint.");
            copy = baseline.WriteScenario(current); var tokenBody = copy.GetNode("World").GetNodes("Body")[0]; tokenBody.SetValue("id", " " + tokenBody.GetValue("id"), true);
            Require(WorldSnapshot.Read(copy).Reconcile(current).Status == WorldCompatibility.InvalidRecord, "Malformed token was normalized as compatible.");
            var first = current.Bodies[0];
            foreach (string field in new[] { "shape.x", "map.height.sha256", "provider", "gravity.mu", "spin.period", "orbit.radius" })
            {
                var changedBody = new WorldBodyDescriptor(first.Id, first.NativeName, first.NativeIndex,
                    first.Properties.Select(p => p.Key == field ? new WorldProperty(p.Key, "changed") : p));
                var changed = new WorldCatalog(current.Bodies.Select(b => b.Id == first.Id ? changedBody : b));
                Require(baseline.Reconcile(changed).Status == WorldCompatibility.Incompatible && baseline.WriteScenario(changed).ToString() == adoptedRaw.ToString(), "Changed world overwritten: " + field);
            }
            var reindexed = new WorldCatalog(current.Bodies.Select(b => b.Id == first.Id ? new WorldBodyDescriptor(b.Id, b.NativeName, 100, b.Properties) : b));
            Require(baseline.Reconcile(reindexed).Status == WorldCompatibility.Incompatible, "Reindex accepted.");
            var removed = new WorldCatalog(current.Bodies.Skip(1));
            Require(baseline.Reconcile(removed).Status == WorldCompatibility.Incompatible, "Removed body accepted.");
            var addedBody = new WorldBodyDescriptor("probe/addition", "ProbeAddition", 100, first.Properties);
            var additions = new WorldCatalog(current.Bodies.Concat(new[] { addedBody }));
            Require(baseline.Reconcile(additions).Status == WorldCompatibility.Additive, "Addition not classified.");
            var appended = WorldSnapshot.Read(baseline.WriteScenario(additions));
            Require(appended.Reconcile(additions).Status == WorldCompatibility.Compatible && baseline.Reconcile(current).Status == WorldCompatibility.Compatible, "Addition mutated old baseline.");
            string path = Path.Combine(KSPUtil.ApplicationRootPath, ".dev", "WorldSnapshot.cfg");
            var container = new ConfigNode(); container.AddNode(baseline.WriteScenario(current)); container.Save(path);
            for (int i = 0; i < 3; i++)
            {
                ConfigNode read = ConfigNode.Load(path).GetNode("SCENARIO"); var roundtrip = WorldSnapshot.Read(read);
                Require(roundtrip.Reconcile(current).Status == WorldCompatibility.Compatible && read.GetNode("Extension").GetValue("foreignMetadata") == "preserve-me"
                    && read.GetNode("World").GetNode("Extension").GetValue("futureText") == "opaque", "Disk roundtrip lost identity/metadata.");
                container = new ConfigNode(); container.AddNode(roundtrip.WriteScenario(current)); container.Save(path);
            }
            Require(legacy.Reconcile(current).Status == WorldCompatibility.LegacyUnverified, "Independent snapshot inherited adoption.");
            var textBody = new WorldBodyDescriptor("probe/text", "Text", 100, new[] {
                new WorldProperty("provider", "probe/v1"), new WorldProperty("generator", "text/v1"),
                new WorldProperty("empty", ""), new WorldProperty("text", "床\n};=//é") });
            var textCatalog = new WorldCatalog(new[] { textBody });
            Require(legacy.TryAdoptCurrent(textCatalog, out var textSnapshot), "Text fixture adoption failed.");
            string textPath = Path.Combine(KSPUtil.ApplicationRootPath, ".dev", "WorldSnapshotText.cfg");
            var textContainer = new ConfigNode(); textContainer.AddNode(textSnapshot.WriteScenario(textCatalog)); textContainer.Save(textPath);
            Require(WorldSnapshot.Read(ConfigNode.Load(textPath).GetNode("SCENARIO")).Reconcile(textCatalog).Status == WorldCompatibility.Compatible, "UTF8/empty/ConfigNode-syntax property did not roundtrip.");
            Debug.Log("[Jakobian] WORLD_CODEC_PASS bodies=5 mutations=geometry-assets-provider-index-dynamics-missing addition=passed legacy=unverified future=preserved tamper=rejected diskRoundtrips=3");
        }
        internal static WorldScenario Scenario()
        {
            var scenarios = UnityEngine.Object.FindObjectsOfType<WorldScenario>();
            if (scenarios.Length != 1) throw new InvalidOperationException("Expected one live per-game WorldScenario; found " + scenarios.Length);
            return scenarios[0];
        }
        internal static void AssertSaved(string folder)
        {
            ConfigNode game = ConfigNode.Load(Path.Combine(KSPUtil.ApplicationRootPath, "saves", folder, "persistent.sfs")).GetNode("GAME");
            ConfigNode[] records = game.GetNodes("SCENARIO").Where(n => n.GetValue("name") == nameof(WorldScenario)).ToArray();
            Require(records.Length == 1 && records[0].GetNodes("World").Length == 1, "Native save lost/duplicated world scenario.");
            Require(WorldSnapshot.Read(records[0]).Reconcile(SystemLoader.CurrentWorld).Status == WorldCompatibility.Compatible, "Native saved world differs from adopted descriptors.");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
