using System;
using System.Linq;
using Jakobian.Core;
using UnityEngine;

namespace Jakobian.KSP
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION)]
    public sealed class WorldScenario : ScenarioModule
    {
        private WorldSnapshot worldSnapshot;
        private WorldCompatibility? lastLogged;
        private WorldSnapshot assessedSnapshot;
        private WorldCatalog assessedWorld;
        private WorldReconciliation cached;
        private bool automated;
        public override void OnAwake()
        {
            base.OnAwake(); automated = Environment.GetCommandLineArgs().Any(a => a.StartsWith("-jakobian", StringComparison.Ordinal) && a.IndexOf("Probe", StringComparison.Ordinal) >= 0);
        }
        private static WorldCatalog LiveWorld => SystemLoader.Error == null ? SystemLoader.CurrentWorld : null;
        internal ConfigNode AdmissionRecord => worldSnapshot?.WriteScenario(null);
        public WorldReconciliation Assessment
        {
            get
            {
                WorldCatalog live = LiveWorld;
                if (cached == null || !ReferenceEquals(worldSnapshot, assessedSnapshot) || !ReferenceEquals(live, assessedWorld))
                {
                    cached = worldSnapshot == null ? WorldReconciliation.State(WorldCompatibility.Unavailable) : worldSnapshot.Reconcile(live);
                    assessedSnapshot = worldSnapshot; assessedWorld = live;
                }
                return cached;
            }
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node); worldSnapshot = WorldSnapshot.Read(node); lastLogged = null; LogAssessment();
        }
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            if (worldSnapshot == null) worldSnapshot = WorldSnapshot.Read(new ConfigNode("SCENARIO"));
            ConfigNode preserved = worldSnapshot.WriteScenario(LiveWorld);
            // KSP owns the module name/scenes. Preserve all supplemental values/nodes, including unknown data.
            foreach (ConfigNode.Value value in preserved.values)
                if (value.name != "name" && value.name != "scene") node.AddValue(value.name, value.value);
            foreach (ConfigNode child in preserved.nodes) node.AddNode(child.CreateCopy());
            worldSnapshot = WorldSnapshot.Read(preserved); LogAssessment();
        }
        /// <summary>Caller must have explicit first-adoption intent. This does not validate historical geometry.</summary>
        public bool TryAdoptCurrent()
        {
            if (worldSnapshot == null || !worldSnapshot.TryAdoptCurrent(LiveWorld, out var adopted)) return false;
            worldSnapshot = adopted; lastLogged = null; LogAssessment(); return true;
        }
        private void Update() { LogAssessment(); }
        private void LogAssessment()
        {
            WorldReconciliation result = Assessment;
            if (lastLogged == result.Status) return;
            lastLogged = result.Status;
            Debug.Log("[Jakobian] WORLD_RECONCILIATION status=" + result.Status + " mode=diagnostic-only changes=" + result.Differences.Count);
        }
        private void OnGUI()
        {
            if (automated) return;
            WorldReconciliation result = Assessment;
            if (result.CanRecordAdditions || (SystemLoader.CurrentWorld == null && worldSnapshot == null)
                || (SystemLoader.CurrentWorld != null && SystemLoader.CurrentWorld.Bodies.Count == 0 && result.Status == WorldCompatibility.LegacyUnverified)) return;
            GUILayout.BeginArea(new Rect(20, 80, Math.Min(500, Screen.width - 40), 250), GUI.skin.box);
            GUILayout.Label("Jakobian world: " + result.Status);
            GUILayout.Label("Compatibility diagnostic only. Flight is not blocked. Restore matching mod files before using an incompatible world.");
            if (result.Status == WorldCompatibility.LegacyUnverified)
            {
                GUILayout.Label("No saved baseline. Recording today's world cannot verify geometry from earlier mod versions.");
                if (GUILayout.Button("Record current world baseline")) TryAdoptCurrent();
            }
            else
            {
                GUILayout.Label(worldSnapshot?.Diagnostic ?? "Saved descriptors are retained; current definitions will not replace them.");
                foreach (var change in result.Differences.Take(4)) GUILayout.Label(change.BodyId + ": " + change.Field + " (" + change.Kind + ")");
            }
            GUILayout.EndArea();
        }
    }
}
