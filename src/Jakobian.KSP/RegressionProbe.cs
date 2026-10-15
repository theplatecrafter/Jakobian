using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Jakobian.KSP
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class RegressionProbe : MonoBehaviour
    {
        private bool failed;
        private IEnumerator Start()
        {
            if (!Environment.GetCommandLineArgs().Contains("-jakobianRegressionProbe")) yield break;
            DontDestroyOnLoad(gameObject);
            foreach (WhatsNewDialog dialog in UnityEngine.Object.FindObjectsOfType<WhatsNewDialog>())
                typeof(WhatsNewDialog).GetMethod("Dismiss", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.Invoke(dialog, null);
            try { NativeChecks.Run(); } catch (Exception e) { Fail(e.ToString()); yield break; }
            string input = Path.Combine(KSPUtil.ApplicationRootPath, ".dev", "regression-inputs");
            foreach (string file in Directory.GetFiles(input, "*.sfs").OrderBy(p => p))
            {
                yield return MainMenu();
                if (failed) yield break;
                yield return LoadHistorical(file);
                if (failed) yield break;
            }
            yield return MainMenu();
            if (failed) yield break;
            yield return StockOrbit();
            if (failed) yield break;
            yield return MainMenu();
            if (failed) yield break;
            Debug.Log("[Jakobian] REGRESSION_SUITE_PASS");
            Application.Quit();
        }

        private IEnumerator LoadHistorical(string path)
        {
            ConfigNode root = ConfigNode.Load(path), gameNode = root.GetNode("GAME");
            ConfigNode ship = gameNode.GetNode("FLIGHTSTATE").GetNodes("VESSEL")[0];
            int index = int.Parse(ship.GetNode("ORBIT").GetValue("REF"), CultureInfo.InvariantCulture);
            CelestialBody body = FlightGlobals.Bodies.Single(b => b.flightGlobalsIndex == index);
            double latitude = Number(ship, "lat"), longitude = Number(ship, "lon"), altitude = Number(ship, "alt");
            string folder = "JakobianUpgrade-" + Path.GetFileNameWithoutExtension(path) + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Vessel previous = FlightGlobals.ActiveVessel;
            try { StartCopy(root, folder); } catch (Exception e) { Fail(e.ToString()); yield break; }
            yield return Ready(previous, body);
            if (failed) yield break;
            Vessel vessel = FlightGlobals.ActiveVessel;
            try
            {
                if (!vessel.Landed || Math.Abs(vessel.latitude - latitude) > .01 || Math.Abs(vessel.longitude - longitude) > .01 || Math.Abs(vessel.altitude - altitude) > 3)
                    throw new InvalidOperationException("Historical landed pose changed: " + path + " alt=" + vessel.altitude + " expected=" + altitude);
                NativeFlightChecks.Run(vessel, "historical-upgrade");
                GamePersistence.SaveGame("persistent", folder, SaveMode.OVERWRITE);
            }
            catch (Exception e) { Fail(e.ToString()); yield break; }
            yield return new WaitForSeconds(1);
            Game reload = GamePersistence.LoadGame("persistent", folder, true, false);
            FlightDriver.StartAndFocusVessel(reload, 0);
            yield return Ready(vessel, body);
            if (failed) yield break;
            Vessel restored = FlightGlobals.ActiveVessel;
            if (!restored.Landed || Math.Abs(restored.altitude - altitude) > 3) { Fail("Historical upgraded reload changed landing."); yield break; }
            Debug.Log("[Jakobian] HISTORICAL_UPGRADE_PASS fixture=" + Path.GetFileName(path) + " body=" + body.bodyName + " landed=True pose=preserved reload=passed copy=" + folder);
        }

        private IEnumerator StockOrbit()
        {
            ConfigNode root = ConfigNode.Load(Path.Combine(KSPUtil.ApplicationRootPath, ".dev", "FlightSeed.sfs"));
            ConfigNode node = root.GetNode("GAME"), state = node.GetNode("FLIGHTSTATE");
            foreach (ConfigNode scenario in node.GetNodes("SCENARIO")) if ((scenario.GetValue("name") ?? "").StartsWith("Tutorial")) node.RemoveNode(scenario);
            foreach (ConfigNode vessel in state.GetNodes("VESSEL")) if (vessel.GetValue("type") == "SpaceObject") state.RemoveNode(vessel);
            ConfigNode ship = state.GetNodes("VESSEL")[0];
            var parts = ship.GetNodes("PART"); for (int i = 1; i < parts.Length; i++) ship.RemoveNode(parts[i]); parts[0].RemoveValues("attN");
            ConfigNode orbit = ship.GetNode("ORBIT"); orbit.SetValue("REF", "1", true); orbit.SetValue("SMA", "700000", true);
            foreach (string field in new[] { "ECC", "INC", "LPE", "LAN", "MNA" }) orbit.SetValue(field, "0", true);
            orbit.SetValue("EPH", state.GetValue("UT"), true); ship.SetValue("sit", "ORBITING", true); ship.SetValue("landed", "False", true);
            state.SetValue("activeVessel", "0", true);
            CelestialBody body = FlightGlobals.Bodies.Single(b => b.bodyName == "Kerbin");
            Vessel previous = FlightGlobals.ActiveVessel;
            string folder = "JakobianStock-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            try { StartCopy(root, folder); } catch (Exception e) { Fail(e.ToString()); yield break; }
            yield return Ready(previous, body); if (failed) yield break;
            Vessel active = FlightGlobals.ActiveVessel;
            double initialSma = active.orbit.semiMajorAxis;
            yield return new WaitForSeconds(10);
            BodyPointSample context;
            if (SystemLoader.OwnedBodies.ContainsKey(body) || BodyQueries.Sample(body, active.CoMD, out context) != BodyQueryStatus.UnsupportedBody
                || FlightSurfaceContext.Instance.CameraCorrections != 0 || Math.Abs(active.orbit.semiMajorAxis - initialSma) > 50
                || Math.Abs(active.altitude - 100000) > 100 || active.Landed)
            { Fail("Stock orbit scope/dynamics mismatch."); yield break; }
            GamePersistence.SaveGame("persistent", folder, SaveMode.OVERWRITE);
            yield return new WaitForSeconds(1);
            FlightDriver.StartAndFocusVessel(GamePersistence.LoadGame("persistent", folder, true, false), 0);
            yield return Ready(active, body); if (failed) yield break;
            if (Math.Abs(FlightGlobals.ActiveVessel.orbit.semiMajorAxis - initialSma) > 50) { Fail("Stock orbit reload mismatch."); yield break; }
            Debug.Log("[Jakobian] STOCK_ORBIT_PASS body=Kerbin scope=excluded coastSeconds=10 orbit=stable reload=passed copy=" + folder);
        }

        private static void StartCopy(ConfigNode root, string folder)
        {
            Directory.CreateDirectory(Path.Combine(KSPUtil.ApplicationRootPath, "saves", folder));
            ConfigNode node = root.GetNode("GAME"); node.SetValue("Title", folder, true); node.SetValue("Mode", "0", true); node.SetValue("scene", "7", true);
            Game game = GamePersistence.LoadGameCfg(root, folder, true, false); game.Mode = Game.Modes.SANDBOX; game.startScene = GameScenes.FLIGHT;
            Type alarm = typeof(Game).Assembly.GetTypes().FirstOrDefault(t => t.Name == "AlarmClockScenario");
            if (alarm != null && !game.scenarios.Exists(s => s.moduleName == alarm.Name)) game.AddProtoScenarioModule(alarm, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION);
            HighLogic.SaveFolder = folder; HighLogic.CurrentGame = game; game.Start();
        }
        private IEnumerator MainMenu()
        {
            // New games start at MainMenu, after old scene/UI teardown; do not call
            // Game.Start repeatedly from the previous fixture's end-of-frame callback.
            yield return new WaitForSeconds(1);
            if (HighLogic.LoadedScene != GameScenes.MAINMENU) HighLogic.LoadScene(GameScenes.MAINMENU);
            float deadline = Time.realtimeSinceStartup + 90;
            while (HighLogic.LoadedScene != GameScenes.MAINMENU && Time.realtimeSinceStartup < deadline) yield return null;
            if (HighLogic.LoadedScene != GameScenes.MAINMENU) { Fail("Return to MainMenu timed out."); yield break; }
            yield return new WaitForSeconds(2);
        }
        private IEnumerator Ready(Vessel previous, CelestialBody body)
        {
            float deadline = Time.realtimeSinceStartup + 90;
            while ((!HighLogic.LoadedSceneIsFlight || FlightGlobals.ActiveVessel == null || ReferenceEquals(previous, FlightGlobals.ActiveVessel)
                || FlightGlobals.ActiveVessel.packed || FlightGlobals.ActiveVessel.mainBody != body) && Time.realtimeSinceStartup < deadline) yield return null;
            if (FlightGlobals.ActiveVessel == null || ReferenceEquals(previous, FlightGlobals.ActiveVessel) || FlightGlobals.ActiveVessel.packed || FlightGlobals.ActiveVessel.mainBody != body)
            { Fail("Historical/stock flight readiness timed out."); yield break; }
            yield return new WaitForSeconds(2); yield return new WaitForEndOfFrame();
        }
        private static double Number(ConfigNode node, string field) => double.Parse(node.GetValue(field), CultureInfo.InvariantCulture);
        private void Fail(string message) { Debug.LogError("[Jakobian] REGRESSION_SUITE_FAIL " + message); failed = true; Application.Quit(); }
    }
}
