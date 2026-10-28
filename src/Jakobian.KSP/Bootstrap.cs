using System;
using System.Linq;
using UnityEngine;

namespace Jakobian.KSP
{
    /// <summary>Runtime status and explicitly requested native startup checks.</summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class Bootstrap : MonoBehaviour
    {
        private void Start()
        {
            string[] relevant = { "Kopernicus", "ModularFlightIntegrator", "0Harmony", "KSPCommunityFixes", "ModuleManager" };
            string assemblies = string.Join(", ", AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => relevant.Contains(a.GetName().Name)).Select(a => a.GetName().Name + " " + a.GetName().Version).ToArray());
            Debug.Log("[Jakobian] Development 0.10.0; Unity=" + Application.unityVersion + "; peers=" + assemblies);
            Debug.Log("[Jakobian] Standalone loader: published=" + SystemLoader.Published + "; bound=" + SystemLoader.Bound + "; bodies=" + SystemLoader.Definitions.Count);
            if (SystemLoader.Error != null && !Environment.GetCommandLineArgs().Any(a => a.StartsWith("-jakobian")))
                PopupDialog.SpawnPopupDialog(new Vector2(.5f, .5f), new Vector2(.5f, .5f), "JakobianLoader", "Jakobian loader error", SystemLoader.Error, "Close", false, UISkinManager.defaultSkin);
            if (Environment.GetCommandLineArgs().Contains("-jakobianBootProbe"))
            {
                try { NativeChecks.Run(); Debug.Log("[Jakobian] BOOT_PROBE_PASS"); }
                catch (Exception error) { Debug.LogError("[Jakobian] BOOT_PROBE_FAIL " + error); }
                Application.Quit();
            }
        }
    }
}
