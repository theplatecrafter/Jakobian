using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Jakobian.Core;
using UnityEngine;

namespace Jakobian.KSP
{
    [DefaultExecutionOrder(-20000)]
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class AdmissionRuntime : MonoBehaviour
    {
        public const string Owner = "Jakobian.LoadAdmission.v1";
        public static bool Ready { get; private set; }
        public static string InstallError { get; private set; }
        public static AdmissionDecision? LastDenied { get; private set; }
        public static long Rejections { get; private set; }
        internal static readonly List<MethodBase> Targets = new List<MethodBase>();
        private static readonly Dictionary<MethodBase, MethodInfo> expectedPrefixes = new Dictionary<MethodBase, MethodInfo>();
        public static bool IsProtected => Ready && Targets.Count == 5 && Targets.All(t => {
            var info = Harmony.GetPatchInfo(t);
            return info != null && expectedPrefixes.TryGetValue(t, out var method) && info.Prefixes.Count(p => p.owner == Owner && p.PatchMethod == method) == 1
                && info.Prefixes.Count(p => p.owner == Owner) == 1 && !ForeignPatches(t);
        });
        private static Harmony harmony;
        private static bool noticePending;
        private static readonly bool probe = Environment.GetCommandLineArgs().Any(a => a.StartsWith("-jakobian", StringComparison.Ordinal) && a.IndexOf("Probe", StringComparison.Ordinal) >= 0);
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            try { Install(); }
            catch (Exception e)
            {
                InstallError = e.Message; Debug.LogError("[Jakobian] ADMISSION_INSTALL_FAILED " + e);
                Application.Quit(); // Required safety hook failure is not a runnable supported installation.
            }
        }
        public static void Install()
        {
            if (Ready) { if (!IsProtected) throw new InvalidOperationException("Installed admission protection has changed."); return; }
            if (typeof(Harmony).Assembly.GetName().Version != new Version(2, 2, 1, 0)) throw new InvalidOperationException("Admission requires the tested HarmonyKSP 2.2.1.0 runtime.");
            if (WorldBodyDescriptor.HashBytes(File.ReadAllBytes(typeof(Harmony).Assembly.Location)) != "19ce60ac3280f72ec1751d36a40cb7e2fece2934df8345969dc7feb83bd633e4"
                || AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "0Harmony") != 1
                || Directory.GetFiles(Path.Combine(KSPUtil.ApplicationRootPath, "GameData"), "0Harmony.dll", SearchOption.AllDirectories).Length != 1)
                throw new InvalidOperationException("Harmony runtime bytes/installation are not the single tested dependency.");
            if (WorldBodyDescriptor.HashBytes(File.ReadAllBytes(typeof(Game).Assembly.Location)) != "d9e42483f25ee80a9c11d6c1c0a0d29b4ec78c1e08d76c971b71580c9cce51e4")
                throw new InvalidOperationException("Admission native assembly profile is unverified; refusing startup.");
            Targets.Clear();
            expectedPrefixes.Clear();
            var pairs = new[] {
                Tuple.Create((MethodBase)AccessTools.Method(typeof(GamePersistence), "LoadGameCfg", new[]{typeof(ConfigNode),typeof(string),typeof(bool),typeof(bool)}), nameof(LoadConfig)),
                Tuple.Create((MethodBase)AccessTools.Method(typeof(Game), "Start", Type.EmptyTypes), nameof(StartGame)),
                Tuple.Create((MethodBase)AccessTools.Method(typeof(Game), "Load", Type.EmptyTypes), nameof(LoadGame)),
                Tuple.Create((MethodBase)AccessTools.Method(typeof(FlightDriver), "StartAndFocusVessel", new[]{typeof(Game),typeof(int)}), nameof(StartFlight)),
                Tuple.Create((MethodBase)AccessTools.Method(typeof(FlightDriver), "StartAndFocusVessel", new[]{typeof(string),typeof(int)}), nameof(StartFile)) };
            harmony = new Harmony(Owner);
            try
            {
                foreach (var pair in pairs)
                {
                    if (pair.Item1 == null) throw new MissingMethodException("Required admission target missing: " + pair.Item2);
                    Targets.Add(pair.Item1);
                    expectedPrefixes.Add(pair.Item1, AccessTools.Method(typeof(AdmissionRuntime), pair.Item2));
                    if (ForeignPatches(pair.Item1)) throw new InvalidOperationException("Competing load/start patches require an ownership adapter.");
                    harmony.Patch(pair.Item1, prefix: new HarmonyMethod(typeof(AdmissionRuntime), pair.Item2) { priority = Priority.First });
                }
                if (Targets.Any(t => Harmony.GetPatchInfo(t)?.Prefixes.Count(p => p.owner == Owner) != 1)) throw new InvalidOperationException("Admission patch installation incomplete.");
                Ready = true; Debug.Log("[Jakobian] ADMISSION_INSTALLED targets=5 owner=" + Owner + " phase=before-native-work");
            }
            catch { foreach (var target in Targets) harmony.Unpatch(target, HarmonyPatchType.All, Owner); Targets.Clear(); throw; }
        }
        private static bool ForeignPatches(MethodBase target)
        {
            var info = Harmony.GetPatchInfo(target);
            return info != null && info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers).Any(p => p.owner != Owner);
        }
        internal static bool Check(AdmissionDecision decision, string route)
        {
            if (!IsProtected) decision = AdmissionDecision.Denied(AdmissionReason.DeniedUnknown, decision.WorldStatus, "Admission protection changed or a competing load/start patch requires coordination.");
            if (decision.Allowed) return true;
            LastDenied = decision; Rejections++; noticePending = true;
            Debug.LogWarning("[Jakobian] LOAD_ADMISSION_DENIED route=" + route + " reason=" + decision.Reason + " world=" + decision.WorldStatus);
            return false;
        }
        private static bool LoadConfig(ConfigNode __0, ref Game __result)
        {
            if (Check(GameAdmission.Inspect(__0), "LoadGameCfg")) return true;
            __result = null; return false;
        }
        private static bool StartGame(Game __instance) => Check(GameAdmission.Inspect(__instance), "Game.Start");
        private static bool LoadGame(Game __instance) => Check(GameAdmission.Inspect(__instance), "Game.Load");
        private static bool StartFlight(Game __0) => Check(GameAdmission.Inspect(__0), "FlightDriver.Game");
        private static bool StartFile(string __0)
        {
            try { return Check(GameAdmission.Inspect(GamePersistence.LoadSFSFile(__0, HighLogic.SaveFolder)), "FlightDriver.File"); }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            { return Check(AdmissionDecision.Denied(AdmissionReason.InvalidReference, WorldCompatibility.InvalidRecord, "Unable to read the requested state file."), "FlightDriver.File"); }
        }
        private void Update()
        {
            if (Ready && !IsProtected && !probe)
            {
                InstallError = "Competing load/start patches detected; this runtime profile is unsupported."; Debug.LogError("[Jakobian] ADMISSION_OWNERSHIP_LOST"); Application.Quit(); return;
            }
            if (!noticePending || probe || !HighLogic.LoadedSceneHasPlanetarium && HighLogic.LoadedScene != GameScenes.MAINMENU) return;
            noticePending = false;
            PopupDialog.SpawnPopupDialog(new Vector2(.5f,.5f), new Vector2(.5f,.5f), "JakobianAdmission", "Jakobian load refused",
                LastDenied.Value.Detail + " Restore the matching mod/configuration files and retry. The saved baseline was not replaced.", "Close", false, UISkinManager.defaultSkin);
        }
    }
}
