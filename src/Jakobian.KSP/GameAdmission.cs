using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jakobian.Core;

namespace Jakobian.KSP
{
    /// <summary>Read-only admission for the supported load/start entrypoints, not active-vessel unpack or teleport.</summary>
    public static class GameAdmission
    {
        private static readonly System.Reflection.FieldInfo protoValues = typeof(ProtoScenarioModule).GetField("moduleValues", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        internal static bool OwnScenarioName(string value) => value == nameof(WorldScenario) || value == typeof(WorldScenario).FullName;
        private static AdmissionDecision Invalid(string detail) => AdmissionDecision.Denied(AdmissionReason.InvalidReference, WorldCompatibility.InvalidRecord, detail);
        private static bool Supported(WorldCatalog catalog) => catalog == null || catalog.Bodies.All(b =>
            b.Properties.Any(p => p.Key == "provider" && p.Value == "jakobian/radial-pqs/v1")
            && b.Properties.Any(p => p.Key == "generator" && p.Value == "radial-mapped/v1")
            && !b.Properties.Any(p => p.Key == "capability"));
        private static int[] NativeIndices() => FlightGlobals.Bodies == null ? new int[0] : FlightGlobals.Bodies.Select(b => b.flightGlobalsIndex).ToArray();
        private static AdmissionDecision Records(IEnumerable<ConfigNode> records, IReadOnlyList<int> references)
        {
            if (SystemLoader.Error != null) return AdmissionDecision.Denied(AdmissionReason.UnavailableCatalog, WorldCompatibility.Unavailable, "The native system loader failed; no game start is admitted.");
            ConfigNode[] owned = records.ToArray();
            if (owned.Length > 1) return Invalid("Duplicate world scenario records.");
            WorldCatalog current = SystemLoader.Error == null ? SystemLoader.CurrentWorld : null;
            WorldCompatibility state = WorldCompatibility.LegacyUnverified; WorldCatalog saved = null;
            if (owned.Length == 1)
            {
                if (owned[0].values.Cast<ConfigNode.Value>().Count(v => v.name == "name") > 1) return Invalid("Duplicate world module identity.");
                if (owned[0].HasValue("name") && !OwnScenarioName(owned[0].GetValue("name"))) return Invalid("Noncanonical world module identity.");
                WorldSnapshot snapshot = WorldSnapshot.Read(owned[0]); saved = snapshot.SavedCatalog;
                state = snapshot.ParsedStatus == WorldCompatibility.LegacyUnverified ? WorldCompatibility.LegacyUnverified : snapshot.Reconcile(current).Status;
            }
            // Nonempty states cannot be admitted during a failed/unready system build.
            if (current == null && (references.Count != 0 || owned.Length != 0)) state = WorldCompatibility.Unavailable;
            return AdmissionPolicy.Evaluate(state, Supported(current) && Supported(saved), references, NativeIndices());
        }
        public static AdmissionDecision Inspect(ConfigNode root)
        {
            if (root == null) return Invalid("Missing game configuration.");
            ConfigNode game;
            if (root.name == "GAME") game = root;
            else
            {
                ConfigNode[] games = root.GetNodes("GAME");
                if (games.Length != 1) return Invalid("Expected exactly one GAME record.");
                game = games[0];
            }
            var references = new List<int>(); var states = game.GetNodes("FLIGHTSTATE");
            if (states.Length > 1) return Invalid("Duplicate FLIGHTSTATE records.");
            if (states.Length == 1)
            {
                ConfigNode[] vessels = states[0].GetNodes("VESSEL");
                if (vessels.Length > AdmissionPolicy.MaximumVessels) return AdmissionDecision.Denied(AdmissionReason.ReferenceBudgetExceeded, WorldCompatibility.InvalidRecord, "Vessel count exceeds admission budget.");
                foreach (var vessel in vessels)
                {
                    ConfigNode[] orbits = vessel.GetNodes("ORBIT");
                    if (orbits.Length != 1 || orbits[0].values.Cast<ConfigNode.Value>().Count(v => v.name == "REF") != 1
                        || !int.TryParse(orbits[0].GetValue("REF"), NumberStyles.None, CultureInfo.InvariantCulture, out int index)) return Invalid("Missing/malformed native orbit reference.");
                    references.Add(index);
                }
            }
            var scenarios = game.GetNodes("SCENARIO");
            if (scenarios.Length > 10000) return Invalid("Excessive scenario count.");
            return Records(scenarios.Where(n => n.values.Cast<ConfigNode.Value>().Any(v => v.name == "name" && OwnScenarioName(v.value?.Trim()))), references);
        }
        public static AdmissionDecision Inspect(Game game)
        {
            if (game == null) return Invalid("Missing game instance.");
            AdmissionDecision? serialized = null;
            if (game.config != null)
            {
                AdmissionDecision raw = Inspect(game.config);
                if (!raw.Allowed) return raw;
                serialized = raw;
            }
            var references = new List<int>();
            if (game.flightState?.protoVessels != null)
            {
                if (game.flightState.protoVessels.Count > AdmissionPolicy.MaximumVessels) return AdmissionDecision.Denied(AdmissionReason.ReferenceBudgetExceeded, WorldCompatibility.InvalidRecord, "Vessel count exceeds admission budget.");
                foreach (var vessel in game.flightState.protoVessels)
                {
                    if (vessel?.orbitSnapShot == null) return Invalid("Missing compiled native orbit reference.");
                    references.Add(vessel.orbitSnapShot.ReferenceBodyIndex);
                }
            }
            var records = new List<ConfigNode>(); var liveRecords = new List<ConfigNode>();
            if (game.scenarios != null)
            {
                if (game.scenarios.Count > 10000) return Invalid("Excessive scenario count.");
                foreach (var proto in game.scenarios)
                {
                    if (proto == null) return Invalid("Null compiled scenario record.");
                    ConfigNode record = protoValues?.GetValue(proto) as ConfigNode;
                    bool named = OwnScenarioName(proto.moduleName?.Trim());
                    bool storedOwned = record != null && record.values.Cast<ConfigNode.Value>().Any(v => v.name == "name" && OwnScenarioName(v.value?.Trim()));
                    if (!named && !storedOwned && !(proto.moduleRef is WorldScenario)) continue;
                    if (!OwnScenarioName(proto.moduleName)) return Invalid("Inconsistent compiled world module identity.");
                    if (record == null) return Invalid("Missing stored world scenario data.");
                    if (record.HasValue("name") && !OwnScenarioName(record.GetValue("name"))) return Invalid("Mismatched stored world module identity.");
                    records.Add(record);
                    if (proto.moduleRef != null)
                    {
                        ConfigNode live = (proto.moduleRef as WorldScenario)?.AdmissionRecord;
                        if (live == null) return Invalid("Uninitialized or conflicting live world scenario.");
                        liveRecords.Add(live);
                    }
                }
            }
            if (records.Count == 0 && serialized.HasValue && serialized.Value.Reason == AdmissionReason.AllowedRecorded) return Invalid("Recorded world scenario is absent from compiled game state.");
            AdmissionDecision stored = Records(records, references);
            if (!stored.Allowed) return stored;
            if (serialized.HasValue && serialized.Value.Reason == AdmissionReason.AllowedRecorded && stored.Reason != AdmissionReason.AllowedRecorded)
                return Invalid("Recorded baseline is missing from compiled world data.");
            if (liveRecords.Count != 0)
            {
                AdmissionDecision live = Records(liveRecords, references);
                if (live.Allowed && live.Reason == AdmissionReason.AllowedLegacyUnverified && stored.Reason == AdmissionReason.AllowedRecorded)
                    return Invalid("Recorded baseline is missing from the live world scenario.");
                if (!live.Allowed || live.Reason == AdmissionReason.AllowedRecorded) return live;
            }
            return stored;
        }
    }
}
