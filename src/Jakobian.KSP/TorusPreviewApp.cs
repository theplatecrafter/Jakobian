using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Jakobian.KSP
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class TorusPreviewApp : MonoBehaviour
    {
        private TorusPreviewRegistry registry;
        private TorusPreviewBody preview;
        private PreviewMenuLock menuLock;
        private GUIStyle headingStyle, labelStyle, buttonStyle;
        private string error;
        private bool probe, failed, enabledPreview;
        private float tilt = 35, azimuth = 45;
        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs(); probe = args.Contains("-jakobianTorusPreviewProbe");
            enabledPreview = probe || args.Contains("-jakobianTorusPreview"); if (!enabledPreview) yield break;
            GameEvents.onGameSceneLoadRequested.Add(OnSceneRequested);
            if (probe) DontDestroyOnLoad(gameObject);
            else
            {
                menuLock = new PreviewMenuLock();
            }
            TorusPreviewConfig config = null;
            try
            {
                if (SystemLoader.Error != null || !SystemLoader.Bound) throw new InvalidOperationException(SystemLoader.Error ?? "Baseline not bound.");
                string path = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "Jakobian", "Preview", "Torus.cfg.disabled");
                ConfigNode root = ConfigNode.Load(path); if (root == null) throw new ArgumentException("Missing preview definition.");
                BodyDefinition.Check(root, "", "JakobianPreview"); config = TorusPreviewConfig.Parse(root.GetNode("JakobianPreview"));
                if (probe) NativeChecks.Run();
                else { registry = new TorusPreviewRegistry(); preview = registry.Publish(config); }
            }
            catch (Exception e) { Fail(e); }
            if (!probe || failed) yield break;
            IEnumerator checks = TorusPreviewChecks.Run(config);
            try
            {
                while (!failed)
                {
                    bool next = false; object current = null;
                    try { next = checks.MoveNext(); if (next) current = checks.Current; }
                    catch (Exception e) { Fail(e); }
                    if (!next || failed) break;
                    yield return current;
                }
            }
            finally { (checks as IDisposable)?.Dispose(); }
            if (failed) yield break;
            // No Game.Start: exercise a native non-game scene roundtrip after preview resources are gone.
            var identity = new NativeWorldIdentity();
            HighLogic.LoadScene(GameScenes.SETTINGS);
            float deadline = Time.realtimeSinceStartup + 90;
            while (HighLogic.LoadedScene != GameScenes.SETTINGS && Time.realtimeSinceStartup < deadline) yield return null;
            if (HighLogic.LoadedScene != GameScenes.SETTINGS) { Fail(new InvalidOperationException("Settings transition timed out.")); yield break; }
            yield return new WaitForSeconds(1);
            HighLogic.LoadScene(GameScenes.MAINMENU); deadline = Time.realtimeSinceStartup + 90;
            while (HighLogic.LoadedScene != GameScenes.MAINMENU && Time.realtimeSinceStartup < deadline) yield return null;
            if (HighLogic.LoadedScene != GameScenes.MAINMENU) { Fail(new InvalidOperationException("Preview return to MainMenu timed out.")); yield break; }
            yield return new WaitForSeconds(2);
            try { identity.Verify(); Debug.Log("[Jakobian] TORUS_PREVIEW_SCENE_PASS settings=returned globals=unchanged"); }
            catch (Exception e) { Fail(e); yield break; }
            Debug.Log("[Jakobian] TORUS_PREVIEW_SUITE_PASS detached=True liveTorus=False cycles=2"); Application.Quit();
        }
        private void Fail(Exception e)
        {
            failed = true; error = e.Message; Debug.LogError("[Jakobian] TORUS_PREVIEW_FAIL " + e);
            registry?.Dispose(); registry = null; preview = null;
            if (probe) Application.Quit();
        }
        private void OnSceneRequested(GameScenes target)
        {
            if (target == GameScenes.MAINMENU) return;
            RemoveMenuBlocker();
            registry?.Dispose(); registry = null; preview = null;
        }
        private void OnDestroy()
        {
            GameEvents.onGameSceneLoadRequested.Remove(OnSceneRequested); RemoveMenuBlocker(); registry?.Dispose();
        }
        private void RemoveMenuBlocker()
        {
            menuLock?.Dispose(); menuLock = null;
        }
        private void OnGUI()
        {
            if (!enabledPreview || probe || HighLogic.LoadedScene != GameScenes.MAINMENU) return;
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true }; labelStyle.normal.textColor = Color.white;
                headingStyle = new GUIStyle(labelStyle) { fontSize = 28, fontStyle = FontStyle.Bold };
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, fixedHeight = 44 };
            }
            Color original = GUI.color; GUI.color = new Color(.025f, .035f, .05f, 1);
            GUI.DrawTexture(new Rect(20, 20, Screen.width - 40, Screen.height - 40), Texture2D.whiteTexture); GUI.color = original;
            GUI.Label(new Rect(40, 30, Screen.width - 80, 40), "Jakobian torus preview — not a flight destination", headingStyle);
            if (error != null)
            {
                GUI.Label(new Rect(40, 85, Screen.width - 80, 100), error, labelStyle);
                if (GUI.Button(new Rect(40, 200, 240, 44), "Close preview", buttonStyle)) { RemoveMenuBlocker(); enabledPreview = false; }
                return;
            }
            if (preview == null) return;
            float size = Math.Min(512, Math.Max(160, Screen.height - 180));
            GUI.DrawTexture(new Rect(40, 100, size, size), preview.Image, ScaleMode.ScaleToFit, true);
            GUILayout.BeginArea(new Rect(60 + size, 100, Math.Max(150, Screen.width - size - 100), Screen.height - 160));
            GUILayout.Label(preview.Definition.Name, labelStyle);
            GUILayout.Label("Major radius: " + preview.Definition.MajorRadius + " m\nTube radius: " + preview.Definition.MinorRadius + " m", labelStyle);
            GUILayout.Label("Detached preview. The five playable moons are unchanged.", labelStyle);
            bool dirty = false;
            if (GUILayout.Button("Top view", buttonStyle)) { tilt = 0; dirty = true; }
            if (GUILayout.Button("Tilted view", buttonStyle)) { tilt = 40; dirty = true; }
            if (GUILayout.Button("Rotate left", buttonStyle)) { azimuth = (azimuth - 30) % 360; dirty = true; }
            if (GUILayout.Button("Rotate right", buttonStyle)) { azimuth = (azimuth + 30) % 360; dirty = true; }
            if (GUILayout.Button("Close preview", buttonStyle)) { RemoveMenuBlocker(); registry?.Dispose(); registry = null; preview = null; enabledPreview = false; }
            GUILayout.EndArea();
            if (dirty && preview != null) { try { preview.Render(tilt, azimuth, 1.2f); } catch (Exception e) { Fail(e); } }
        }
    }
}
