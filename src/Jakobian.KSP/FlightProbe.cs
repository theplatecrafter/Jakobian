using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Jakobian.KSP
{
    // Enabled only by an explicit development launch argument. Uses a private stock seed copy.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class FlightProbe : MonoBehaviour
    {
        private bool running;
        private IEnumerator Start()
        {
            string argument = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("-jakobianFlightProbe="));
            if (argument == null) yield break;
            DontDestroyOnLoad(gameObject); running = true;
            foreach (WhatsNewDialog dialog in UnityEngine.Object.FindObjectsOfType<WhatsNewDialog>())
                typeof(WhatsNewDialog).GetMethod("Dismiss", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.Invoke(dialog, null);
            try { NativeChecks.Run(); }
            catch (Exception error) { Fail(error.ToString()); yield break; }
            string selection = argument.Substring("-jakobianFlightProbe=".Length);
            string[] targets = selection == "all" ? new[] { "JakobianEllipsoid", "JakobianCube", "JakobianPeanut", "JakobianAtlas" } : new[] { selection };
            if (Environment.GetCommandLineArgs().Contains("-jakobianWorldIsolationProbe")) targets = new[] { "JakobianAtlas", "JakobianAtlas" };
            int visits = 0;
            foreach (string target in targets)
            {
                if (visits > 0)
                {
                    HighLogic.LoadScene(GameScenes.MAINMENU);
                    float deadline = Time.realtimeSinceStartup + 90;
                    while (HighLogic.LoadedScene != GameScenes.MAINMENU && Time.realtimeSinceStartup < deadline) yield return null;
                    if (HighLogic.LoadedScene != GameScenes.MAINMENU) { Fail("Inter-game MainMenu teardown timed out."); yield break; }
                    yield return new WaitForSeconds(2);
                }
                yield return RunBody(target);
                if (!running) yield break;
                visits++;
            }
            // Quit through normal scene teardown instead of destroying the flight
            // map/UI directly during Application.Quit callback ordering.
            yield return new WaitForSeconds(1);
            HighLogic.LoadScene(GameScenes.MAINMENU);
            float menuDeadline = Time.realtimeSinceStartup + 90;
            while (HighLogic.LoadedScene != GameScenes.MAINMENU && Time.realtimeSinceStartup < menuDeadline) yield return null;
            if (HighLogic.LoadedScene != GameScenes.MAINMENU) { Fail("Return to MainMenu timed out."); yield break; }
            yield return new WaitForSeconds(2);
            Debug.Log("[Jakobian] FLIGHT_SUITE_PASS bodies=" + targets.Length);
            running = false; Application.Quit();
        }

        private IEnumerator RunBody(string target)
        {
            CelestialBody body = FlightGlobals.Bodies.FirstOrDefault(b => b.bodyName == target);
            if (body == null || !SystemLoader.Definitions.ContainsKey(target)) { Fail("Unknown development body " + target); yield break; }
            yield return new WaitForSeconds(2);
            Game game;
            try
            {
                string folder = "JakobianProbe-" + target + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                Directory.CreateDirectory(Path.Combine(KSPUtil.ApplicationRootPath, "saves", folder));
                ConfigNode root = ConfigNode.Load(Path.Combine(KSPUtil.ApplicationRootPath, ".dev", "FlightSeed.sfs"));
                ConfigNode node = root.GetNode("GAME");
                node.SetValue("Mode", "0", true); node.SetValue("Title", folder, true); node.SetValue("scene", "7", true);
                foreach (ConfigNode scenario in node.GetNodes("SCENARIO"))
                    if ((scenario.GetValue("name") ?? "").StartsWith("Tutorial")) node.RemoveNode(scenario);
                ConfigNode state = node.GetNode("FLIGHTSTATE");
                foreach (ConfigNode vessel in state.GetNodes("VESSEL")) if (vessel.GetValue("type") == "SpaceObject") state.RemoveNode(vessel);
                ConfigNode ship = state.GetNodes("VESSEL")[0];
                var parts = ship.GetNodes("PART");
                for (int i = 1; i < parts.Length; i++) ship.RemoveNode(parts[i]);
                parts[0].RemoveValues("attN");
                ship.SetValue("name", "Jakobian landing probe", true); ship.SetValue("sit", "ORBITING", true);
                ship.SetValue("landed", "False", true); ship.SetValue("splashed", "False", true);
                ConfigNode orbit = ship.GetNode("ORBIT");
                double ut = double.Parse(state.GetValue("UT"), System.Globalization.CultureInfo.InvariantCulture);
                var initialOrbit = new Orbit(0, 0, SystemLoader.Definitions[target].Surface.BoundingRadius + 100, 0, 0, 0, ut, body);
                global::Vector3d orbitPoint = initialOrbit.getRelativePositionAtUT(ut);
                var futureFrame = new Planetarium.CelestialFrame();
                double angle = body.initialRotation + (body.rotates ? ut * 360 / body.rotationPeriod : 0) - Planetarium.InverseRotAngle;
                Planetarium.CelestialFrame.PlanetaryFrame(0, 90, angle, ref futureFrame);
                global::Vector3d local = futureFrame.WorldToLocal(orbitPoint).xzy;
                double floorRadius = SystemLoader.Definitions[target].Surface.RadiusAt(new Jakobian.Core.Vector3d(local.x, local.y, local.z));
                orbit.SetValue("SMA", (floorRadius + 100).ToString(System.Globalization.CultureInfo.InvariantCulture), true);
                orbit.SetValue("ECC", "0", true); orbit.SetValue("INC", "0", true); orbit.SetValue("LPE", "0", true); orbit.SetValue("LAN", "0", true); orbit.SetValue("MNA", "0", true);
                orbit.SetValue("EPH", state.GetValue("UT"), true); orbit.SetValue("REF", body.flightGlobalsIndex.ToString(), true);
                state.SetValue("activeVessel", "0", true);
                game = GamePersistence.LoadGameCfg(root, folder, true, false);
                game.Mode = Game.Modes.SANDBOX; game.startScene = GameScenes.FLIGHT;
                Type alarm = typeof(Game).Assembly.GetTypes().FirstOrDefault(t => t.Name == "AlarmClockScenario");
                if (alarm != null && !game.scenarios.Exists(s => s.moduleName == alarm.Name)) game.AddProtoScenarioModule(alarm, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION);
                if (!game.scenarios.Exists(s => s.moduleName == nameof(WorldScenario))) game.AddProtoScenarioModule(typeof(WorldScenario), GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION);
                HighLogic.SaveFolder = folder; HighLogic.CurrentGame = game;
                game.Start();
            }
            catch (Exception error) { Fail(error.ToString()); yield break; }
            float deadline = Time.realtimeSinceStartup + 240;
            while ((!HighLogic.LoadedSceneIsFlight || FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.packed || FlightGlobals.ActiveVessel.mainBody != body) && Time.realtimeSinceStartup < deadline) yield return null;
            Vessel active = FlightGlobals.ActiveVessel;
            if (active == null || active.packed || active.mainBody != body) { Fail("Flight state not ready on requested body."); yield break; }
            Debug.Log("[Jakobian] FLIGHT_PROBE_READY " + target);
            yield return new WaitForSeconds(2);
            try
            {
                if (WorldPersistenceChecks.Scenario().Assessment.Status != Jakobian.Core.WorldCompatibility.LegacyUnverified)
                    throw new InvalidOperationException("Fresh private game inherited a world baseline from another game.");
                Debug.Log("[Jakobian] WORLD_GAME_ISOLATION_PASS freshGame=unverified save=" + HighLogic.SaveFolder);
            }
            catch (Exception error) { Fail(error.ToString()); yield break; }
            yield return new WaitForEndOfFrame();
            try { NativeFlightChecks.Run(active, "approach"); }
            catch (Exception error) { Fail(error.ToString()); yield break; }
            yield return CheckAltimeter(active);
            if (!running) yield break;
            Directory.CreateDirectory(Path.Combine(KSPUtil.ApplicationRootPath, ".dev", "screenshots"));
            ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath, ".dev", "screenshots", target + "-flight.png"));
            global::Vector3d relative = active.GetWorldPos3D() - body.position;
            active.SetRotation(Quaternion.FromToRotation(Vector3.up, (Vector3)relative.normalized));
            Vector3 tangent = Vector3.Cross((Vector3)relative.normalized, Vector3.up);
            if (tangent.sqrMagnitude < .01f) tangent = Vector3.Cross((Vector3)relative.normalized, Vector3.right);
            active.SetWorldVelocity(body.getRFrmVel(active.GetWorldPos3D()) + (global::Vector3d)tangent.normalized * .1 - Krakensbane.GetFrameVelocity());
            FlightCamera.SetModeImmediate(FlightCamera.Modes.FREE);
            FlightCamera.fetch.SetCamCoordsFromPosition((Vector3)active.GetWorldPos3D() + (Vector3)relative.normalized * 15 + tangent.normalized * 30);
            try
            {
                BodyPointSample context;
                if (BodyQueries.Sample(body, active.GetWorldPos3D(), out context) != BodyQueryStatus.Available || FlightSurfaceContext.Instance == null)
                    throw new InvalidOperationException("Camera/context controller unavailable.");
                Transform cameraTransform = FlightCamera.fetch.GetCameraTransform();
                global::Vector3d underground = context.GroundWorld - context.GravityUpWorld * 10;
                FlightCamera.fetch.SetCamCoordsFromPosition((Vector3)underground);
                // SetCamCoords rotates the parent pivot, which moves its camera child.
                // Inject the actual below-ground endpoint after that parent change.
                cameraTransform.position = (Vector3)underground;
                BodyPointSample beforeGuard;
                if (BodyQueries.Sample(body, cameraTransform.position, out beforeGuard) != BodyQueryStatus.Available
                    || beforeGuard.LocalSurface.Clearance > -9)
                    throw new InvalidOperationException("Camera fixture did not start underground.");
                int corrections = FlightSurfaceContext.Instance.CameraCorrections;
                float pitch = FlightCamera.CamPitch, heading = FlightCamera.CamHdg, distance = FlightCamera.fetch.Distance;
                FlightSurfaceContext.Instance.ConstrainCamera(body, FlightCamera.fetch);
                BodyPointSample after;
                if (BodyQueries.Sample(body, cameraTransform.position, out after) != BodyQueryStatus.Available
                    || after.LocalSurface.Clearance < 1.8 || FlightSurfaceContext.Instance.CameraCorrections != corrections + 1)
                    throw new InvalidOperationException("Underground camera not constrained.");
                if (Math.Abs(FlightCamera.CamPitch - pitch) > .0001 || Math.Abs(FlightCamera.CamHdg - heading) > .0001
                    || Math.Abs(FlightCamera.fetch.Distance - distance) > .0001)
                    throw new InvalidOperationException("Camera correction changed native pitch/heading/zoom state.");
                Debug.Log("[Jakobian] CAMERA_GUARD_PASS " + target + " underground=-10 corrected=" + after.LocalSurface.Clearance);
                Vector3 beforeStock = cameraTransform.position;
                CelestialBody kerbin = FlightGlobals.Bodies.First(b => b.bodyName == "Kerbin");
                FlightSurfaceContext.Instance.ConstrainCamera(kerbin, FlightCamera.fetch);
                if (cameraTransform.position != beforeStock) throw new InvalidOperationException("Camera guard altered stock body state.");
            }
            catch (Exception error) { Fail(error.ToString()); yield break; }
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                BodyPointSample cameraSample;
                if (BodyQueries.Sample(body, FlightCamera.fetch.GetCameraTransform().position, out cameraSample) != BodyQueryStatus.Available
                    || cameraSample.LocalSurface.Clearance < 1.8)
                { Fail("Camera endpoint constraint did not survive native camera updates."); yield break; }
            }
            Debug.Log("[Jakobian] CAMERA_SEQUENCE_PASS " + target + " frames=10 nativePitchHeadingZoom=preserved");
            bool colliderSeen = false;
            float nextTrace = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                global::Vector3d position = active.GetWorldPos3D();
                Vector3 up = (Vector3)(position - body.position).normalized;
                if (Time.realtimeSinceStartup >= nextTrace)
                {
                    nextTrace = Time.realtimeSinceStartup + 10;
                    Debug.Log("[Jakobian] DROP_TRACE " + target + " alt=" + active.altitude + " terrain=" + active.terrainAltitude + " speed=" + active.srfSpeed + " packed=" + active.packed + " kinematic=" + active.rootPart?.rb?.isKinematic + " landed=" + active.Landed);
                }
                foreach (RaycastHit hit in Physics.RaycastAll((Vector3)position + up * 10, -up, 20000))
                    if (hit.collider.transform.IsChildOf(body.pqsController.transform)) colliderSeen = true;
                if (active.Landed)
                {
                    if (!colliderSeen) { Fail("Landed flag without observed terrain collider."); yield break; }
                    if (SystemLoader.Definitions[target].Assets.AlbedoTexture != null)
                    {
                        try { MappedSurfaceChecks.ContactMeshes(body); MappedSurfaceChecks.RenderAlbedo(body); }
                        catch (Exception error) { Fail(error.ToString()); yield break; }
                    }
                    try
                    {
                        if (!WorldPersistenceChecks.Scenario().TryAdoptCurrent()) throw new InvalidOperationException("Private game baseline adoption failed.");
                        GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
                        WorldPersistenceChecks.AssertSaved(HighLogic.SaveFolder);
                    }
                    catch (Exception error) { Fail(error.ToString()); yield break; }
                    // Re-seat the diagnostic camera after descent: its local frame can change
                    // during native flight setup. A pose chosen at orbit entry is not evidence
                    // that the final camera remains outside an irregular surface.
                    FlightCamera.fetch.SetCamCoordsFromPosition((Vector3)position + up * 60 + tangent.normalized * 30);
                    yield return new WaitForSeconds(1);
                    global::Vector3d cameraPosition = FlightCamera.fetch.transform.position;
                    // CelestialFrame uses orbit Z-up on both sides of its rotation;
                    // Unity/PQS use Y-up, as in native GetWorldSurfacePosition.
                    global::Vector3d cameraLocal = body.BodyFrame.WorldToLocal((cameraPosition - body.position).xzy).xzy;
                    double cameraFloor = SystemLoader.Definitions[target].Surface.RadiusAt(new Jakobian.Core.Vector3d(cameraLocal.x, cameraLocal.y, cameraLocal.z));
                    ScreenCapture.CaptureScreenshot(Path.Combine(KSPUtil.ApplicationRootPath, ".dev", "screenshots", target + "-landed.png"));
                    Debug.Log("[Jakobian] FLIGHT_RENDER_TRACE " + target + " activeRenderers="
                        + body.pqsController.GetComponentsInChildren<Renderer>().Count(r => r.enabled && r.gameObject.activeInHierarchy)
                        + " opacity=" + body.pqsController.surfaceMaterial.GetFloat("_PlanetOpacity")
                        + " cameraClearance=" + ((cameraPosition - body.position).magnitude - cameraFloor));
                    Debug.Log("[Jakobian] FLIGHT_LANDING_PASS " + target + " terrainCollider=" + colliderSeen + " landed=" + active.Landed + " save=" + HighLogic.SaveFolder);
                    yield return new WaitForSeconds(1);
                    double latitude = active.latitude, longitude = active.longitude, altitude = active.altitude;
                    string folder = HighLogic.SaveFolder;
                    Game reload = GamePersistence.LoadGame("persistent", folder, true, false);
                    FlightDriver.StartAndFocusVessel(reload, 0);
                    float reloadDeadline = Time.realtimeSinceStartup + 60;
                    while ((ReferenceEquals(FlightGlobals.ActiveVessel, active) || FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.packed) && Time.realtimeSinceStartup < reloadDeadline) yield return null;
                    Vessel restored = FlightGlobals.ActiveVessel;
                    if (restored == null || restored.packed || restored.mainBody != body || !restored.Landed
                        || Math.Abs(restored.latitude - latitude) > .01 || Math.Abs(restored.longitude - longitude) > .01 || Math.Abs(restored.altitude - altitude) > 3)
                    { Fail("Landed save/reload mismatch for " + target + " packed=" + restored?.packed + " landed=" + restored?.Landed + " alt=" + restored?.altitude + " expected=" + altitude + " lat=" + restored?.latitude + " lon=" + restored?.longitude); yield break; }
                    Debug.Log("[Jakobian] FLIGHT_RELOAD_PASS " + target);
                    yield return new WaitForSeconds(2);
                    try
                    {
                        if (WorldPersistenceChecks.Scenario().Assessment.Status != Jakobian.Core.WorldCompatibility.Compatible)
                            throw new InvalidOperationException("Reload lost world descriptors.");
                        GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
                        WorldPersistenceChecks.AssertSaved(HighLogic.SaveFolder);
                        Debug.Log("[Jakobian] WORLD_SCENARIO_ROUNDTRIP_PASS bodies=" + SystemLoader.CurrentWorld.Bodies.Count + " mode=diagnostic-only save=" + HighLogic.SaveFolder);
                    }
                    catch (Exception error) { Fail(error.ToString()); yield break; }
                    yield return new WaitForEndOfFrame();
                    try { NativeFlightChecks.Run(restored, "landed-reload"); }
                    catch (Exception error) { Fail(error.ToString()); yield break; }
                    yield return CheckAltimeter(restored);
                    yield break;
                }
                if (active.parts == null || active.parts.Count == 0) { Fail("Probe destroyed before landing."); yield break; }
                // Test-only braking substitutes for a lander's engine. Terrain contact and the
                // LANDED state still come from native colliders/physics; never assign either.
                // SetWorldVelocity takes the floating physics frame, not inertial velocity.
                active.SetWorldVelocity(body.getRFrmVel(position) - (global::Vector3d)up * 3
                    + (global::Vector3d)tangent.normalized * .1 - Krakensbane.GetFrameVelocity());
                yield return new WaitForFixedUpdate();
            }
            Fail("Landing deadline exceeded; colliderSeen=" + colliderSeen + " altitude=" + active.altitude);
        }

        private IEnumerator CheckAltimeter(Vessel vessel)
        {
            var altimeter = UnityEngine.Object.FindObjectOfType<global::KSP.UI.Screens.Flight.AltitudeTumbler>();
            if (altimeter == null) { Fail("Native altimeter not available."); yield break; }
            AltimeterDisplayState original = altimeter.CurrentMode;
            foreach (AltimeterDisplayState mode in new[] { AltimeterDisplayState.ASL, AltimeterDisplayState.AGL })
            {
                altimeter.SetModeTumbler(mode);
                yield return null;
                yield return new WaitForEndOfFrame();
                double expected = mode == AltimeterDisplayState.AGL ? vessel.radarAltitude : vessel.altitude * global::KSP.UI.Screens.Flight.AltitudeTumbler.altitudeMultiplier;
                double actual = altimeter.tumbler.Value;
                if (Math.Abs(actual - expected) > .01)
                {
                    altimeter.SetModeTumbler(original);
                    Fail("Native altimeter UI mismatch: mode=" + mode + " actual=" + actual + " expected=" + expected);
                    yield break;
                }
                Debug.Log("[Jakobian] ALTIMETER_MODE_PASS " + vessel.mainBody.bodyName + " mode=" + mode + " value=" + actual);
            }
            altimeter.SetModeTumbler(original);
        }
        private void Fail(string message) { Debug.LogError("[Jakobian] FLIGHT_PROBE_FAIL " + message); running = false; Application.Quit(); }
        private void Update() { if (running && Input.GetKeyDown(KeyCode.Escape)) { running = false; Application.Quit(); } }
    }
}
