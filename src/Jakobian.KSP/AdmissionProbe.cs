using System;
using System.Collections;
using System.IO;
using System.Linq;
using HarmonyLib;
using Jakobian.Core;
using UnityEngine;

namespace Jakobian.KSP
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class AdmissionProbe : MonoBehaviour
    {
        private const string ObserveOwner = "Jakobian.Admission.ProbeObservers";
        private static int constructors, clears, loads, scenes;
        private static void Constructor() { constructors++; }
        private static void Clear() { clears++; }
        private static void Load() { loads++; }
        private static void Scene() { scenes++; }
        private static void ForeignPostfix() { }
        private IEnumerator Start()
        {
            if (!Environment.GetCommandLineArgs().Contains("-jakobianAdmissionProbe")) yield break;
            try { Run(); }
            catch (Exception e) { Debug.LogError("[Jakobian] ADMISSION_SUITE_FAIL " + e); Application.Quit(); yield break; }
            yield return new WaitForSeconds(1);
            Debug.Log("[Jakobian] ADMISSION_SUITE_PASS routes=5 beforeNative=True liveTorus=False"); Application.Quit();
        }
        private static void Require(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }
        private static void Reset() { constructors = clears = loads = scenes = 0; }
        private static void Denied(Action action, string label)
        {
            Reset(); long rejections = AdmissionRuntime.Rejections;
            var scene = HighLogic.LoadedScene; var game = HighLogic.CurrentGame; var vessel = FlightGlobals.ActiveVessel; string folder = HighLogic.SaveFolder;
            action();
            Require(AdmissionRuntime.Rejections > rejections, "Missing actual rejection: " + label);
            Require(constructors == 0 && clears == 0 && loads == 0 && scenes == 0, "Denied route reached native work: " + label);
            Require(HighLogic.LoadedScene == scene && ReferenceEquals(game, HighLogic.CurrentGame) && ReferenceEquals(vessel, FlightGlobals.ActiveVessel) && HighLogic.SaveFolder == folder,
                "Denied route changed live game/scene: " + label);
            Debug.Log("[Jakobian] ADMISSION_REJECT_PASS case=" + label + " constructor=0 idClear=0 flightLoad=0 sceneCall=0");
        }
        private static ConfigNode Baseline(ConfigNode seed, WorldCatalog catalog)
        {
            ConfigNode result = seed.CreateCopy(), game = result.GetNode("GAME");
            foreach (var n in game.GetNodes("SCENARIO")) if (GameAdmission.OwnScenarioName(n.GetValue("name"))) game.RemoveNode(n);
            var module = new ConfigNode("SCENARIO"); module.AddValue("name", nameof(WorldScenario)); module.AddValue("scene", "7, 5, 8");
            Require(WorldSnapshot.Read(module).TryAdoptCurrent(catalog, out var adopted), "Fixture adoption failed.");
            game.AddNode(adopted.WriteScenario(catalog)); return result;
        }
        private static ConfigNode WorldNode(ConfigNode root) => root.GetNode("GAME").GetNodes("SCENARIO").Single(n => GameAdmission.OwnScenarioName(n.GetValue("name")));
        private static void Run()
        {
            Require(AdmissionRuntime.IsProtected, "Admission hooks unavailable."); AdmissionRuntime.Install(); Require(AdmissionRuntime.IsProtected, "Second install damaged hooks.");
            NativeChecks.Run();
            var observe = new Harmony(ObserveOwner);
            var targets = new[] {
                Tuple.Create((System.Reflection.MethodBase)AccessTools.Constructor(typeof(Game), new[]{typeof(ConfigNode)}), nameof(Constructor)),
                Tuple.Create((System.Reflection.MethodBase)AccessTools.Method(typeof(FlightGlobals), "ClearpersistentIdDictionaries", Type.EmptyTypes), nameof(Clear)),
                Tuple.Create((System.Reflection.MethodBase)AccessTools.Method(typeof(FlightState), "Load", Type.EmptyTypes), nameof(Load)),
                Tuple.Create((System.Reflection.MethodBase)AccessTools.Method(typeof(HighLogic), "LoadScene", new[]{typeof(GameScenes)}), nameof(Scene)) };
            string savedFolder = HighLogic.SaveFolder;
            GameObject liveObject = null;
            try
            {
                foreach (var pair in targets) { Require(pair.Item1 != null, "Missing native observer."); observe.Patch(pair.Item1, prefix:new HarmonyMethod(typeof(AdmissionProbe), pair.Item2)); }
                var empty = new Game(); Require(GameAdmission.Inspect(empty).Allowed, "Healthy fresh empty game refused.");
                string originalError = SystemLoader.Error;
                try { SystemLoader.Error = "Private broken-loader fixture"; Denied(() => empty.Start(), "failed-loader-empty-game"); }
                finally { SystemLoader.Error = originalError; }
                ConfigNode seed = ConfigNode.Load(Path.Combine(KSPUtil.ApplicationRootPath, ".dev", "FlightSeed.sfs"));
                seed.GetNode("GAME").SetValue("Mode", "0", true);
                foreach (var n in seed.GetNode("GAME").GetNodes("SCENARIO")) if ((n.GetValue("name") ?? "").StartsWith("Tutorial", StringComparison.Ordinal)) seed.GetNode("GAME").RemoveNode(n);
                var current = SystemLoader.CurrentWorld; ConfigNode valid = Baseline(seed, current);
                Require(GameAdmission.Inspect(seed).Allowed && GameAdmission.Inspect(seed).Reason == AdmissionReason.AllowedLegacyUnverified, "Legacy fixture refused/relabelled.");
                Require(GameAdmission.Inspect(valid).Allowed, "Current baseline refused.");
                ConfigNode bad = null;
                foreach (string field in new[] { "shape.x", "map.height.sha256", "provider" })
                {
                    var first = current.Bodies[0];
                    var changed = new WorldBodyDescriptor(first.Id, first.NativeName, first.NativeIndex, first.Properties.Select(p => p.Key == field ? new WorldProperty(p.Key, "changed") : p));
                    bad = Baseline(seed, new WorldCatalog(current.Bodies.Select(b => b.Id == first.Id ? changed : b)));
                    string frozen = bad.ToString();
                    Denied(() => Require(GamePersistence.LoadGameCfg(bad, "JakobianAdmission", true, true) == null, "Denied config returned a Game."), field);
                    Require(bad.ToString() == frozen, "Rejection mutated source configuration.");
                }
                var indexBody = current.Bodies[0]; var changedIndex = new WorldBodyDescriptor(indexBody.Id, indexBody.NativeName, 10000, indexBody.Properties);
                bad = Baseline(seed, new WorldCatalog(current.Bodies.Select(b => b.Id == indexBody.Id ? changedIndex : b)));
                Denied(() => Require(GamePersistence.LoadGameCfg(bad,"JakobianAdmission",true,true) == null,"Reindex accepted."), "native-index");
                var missing = new WorldBodyDescriptor("probe/missing", "Missing", 10000, indexBody.Properties);
                bad = Baseline(seed, new WorldCatalog(current.Bodies.Concat(new[]{missing})));
                Denied(() => Require(GamePersistence.LoadGameCfg(bad,"JakobianAdmission",true,true) == null,"Missing accepted."), "missing-body");
                var preview = new TorusBodyDefinition("probe/torus", "ProbeTorus", 22, 9000, 1500, 5e7, 500000, "Kerbin", 1, 60000000, 0, 0);
                bad = Baseline(seed, new WorldCatalog(current.Bodies.Concat(new[]{preview.World})));
                Denied(() => Require(GamePersistence.LoadGameCfg(bad,"JakobianAdmission",true,true) == null,"Preview accepted."), "detached-preview");
                foreach (string kind in new[]{"future", "invalid", "duplicate", "bad-reference", "padded-name"})
                {
                    bad = valid.CreateCopy();
                    if(kind == "future") WorldNode(bad).GetNode("World").SetValue("schemaVersion","99",true);
                    if(kind == "invalid") WorldNode(bad).GetNode("World").SetValue("catalogFingerprint","tampered",true);
                    if(kind == "duplicate") bad.GetNode("GAME").AddNode(WorldNode(bad).CreateCopy());
                    if(kind == "bad-reference") bad.GetNode("GAME").GetNode("FLIGHTSTATE").GetNodes("VESSEL")[0].GetNode("ORBIT").SetValue("REF","999999",true);
                    if(kind == "padded-name") WorldNode(bad).SetValue("name"," WorldScenario ",true);
                    Denied(() => Require(GamePersistence.LoadGameCfg(bad,"JakobianAdmission",true,true) == null,"Unsafe config admitted."), kind);
                }
                bad = valid.CreateCopy(); WorldNode(bad).GetNode("World").SetValue("schemaVersion","99",true);
                var direct = new Game(); direct.config = bad;
                Denied(() => direct.Start(), "direct-Game.Start"); Denied(() => direct.Load(), "direct-Game.Load");
                Denied(() => FlightDriver.StartAndFocusVessel(direct,0), "direct-FlightDriver.Game");
                string folder = "JakobianAdmission-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                string directory = Path.Combine(KSPUtil.ApplicationRootPath,"saves",folder); Directory.CreateDirectory(directory); bad.Save(Path.Combine(directory,"blocked.sfs"));
                HighLogic.SaveFolder = folder;
                Denied(() => FlightDriver.StartAndFocusVessel("blocked",0), "direct-FlightDriver.File");
                HighLogic.SaveFolder = savedFolder;
                Reset(); Game compiled = GamePersistence.LoadGameCfg(valid.CreateCopy(),"JakobianAdmission",true,true);
                Require(compiled != null && constructors > 0 && clears > 0, "Allowed fixture did not exercise original constructor/ID clear.");
                Require(GameAdmission.Inspect(compiled).Allowed, "Compiled current game refused.");
                var ship = compiled.flightState.protoVessels[0]; int original = ship.orbitSnapShot.ReferenceBodyIndex; ship.orbitSnapShot.ReferenceBodyIndex = 999999;
                Denied(() => FlightDriver.StartAndFocusVessel(compiled,0), "mutated-proto-reference"); ship.orbitSnapShot.ReferenceBodyIndex = original;
                var proto = compiled.scenarios.Single(p => GameAdmission.OwnScenarioName(p.moduleName));
                var values = AccessTools.Field(typeof(ProtoScenarioModule),"moduleValues"); object originalValues = values.GetValue(proto);
                liveObject = new GameObject("Jakobian private admission module fixture"); var live = liveObject.AddComponent<WorldScenario>(); live.OnLoad(WorldNode(valid)); proto.moduleRef = live;
                values.SetValue(proto, WorldNode(bad).CreateCopy());
                Denied(() => compiled.Load(), "stored-future-live-compatible"); values.SetValue(proto, originalValues);
                live.OnLoad(WorldNode(bad)); Denied(() => compiled.Load(), "live-future-stored-compatible"); live.OnLoad(WorldNode(valid));
                string moduleName = proto.moduleName; proto.moduleName = "OtherModule";
                Denied(() => compiled.Start(), "renamed-owned-proto"); proto.moduleName = moduleName;
                values.SetValue(proto,null); Denied(() => compiled.Load(), "missing-proto-data"); values.SetValue(proto,originalValues);
                live.OnLoad(new ConfigNode("SCENARIO")); Denied(() => compiled.Load(), "recorded-reset-to-legacy"); live.OnLoad(WorldNode(valid));
                proto.moduleRef = null;
                var foreign = new Harmony("Jakobian.Admission.ForeignProbe"); var target = AdmissionRuntime.Targets[0];
                try
                {
                    foreign.Patch(target, postfix:new HarmonyMethod(typeof(AdmissionProbe),nameof(ForeignPostfix)));
                    Denied(() => Require(GamePersistence.LoadGameCfg(valid,"JakobianAdmission",true,true) == null,"Foreign patch profile admitted."), "foreign-owner");
                    Require(Harmony.GetPatchInfo(target).Postfixes.Any(p=>p.owner==foreign.Id), "Foreign patch was removed by admission.");
                }
                finally { foreign.Unpatch(target,HarmonyPatchType.All,foreign.Id); }
                var removedTarget = AdmissionRuntime.Targets[1]; var expected = Harmony.GetPatchInfo(removedTarget).Prefixes.Single(p=>p.owner==AdmissionRuntime.Owner).PatchMethod;
                var sameOwner = new Harmony(AdmissionRuntime.Owner); sameOwner.Unpatch(removedTarget,expected);
                Require(!AdmissionRuntime.IsProtected, "Missing owned hook was not detected.");
                sameOwner.Patch(removedTarget,prefix:new HarmonyMethod(expected){priority=Priority.First}); Require(AdmissionRuntime.IsProtected,"Probe hook restoration failed.");
                AdmissionRuntime.Install();
                Debug.Log("[Jakobian] ADMISSION_ORDER_PASS guardedRoutes=5 denyBeforeConstructor=True denyBeforeIDClear=True mutableProtoLive=checked owners=preserved missingHook=detected");
            }
            finally
            {
                HighLogic.SaveFolder = savedFolder;
                if(liveObject != null) { liveObject.SetActive(false); UnityEngine.Object.Destroy(liveObject); }
                foreach(var target in targets) if(target.Item1 != null) observe.Unpatch(target.Item1,HarmonyPatchType.All,ObserveOwner);
            }
        }
    }
}
