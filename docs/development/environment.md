# Development environment

## Prepared environment

Main local repository: `C:\Users\Hans\Documents\programming\Jakobian`, now on **main**, with commits/remotes/publication managed by the owner. The supplied `template_instance` remains the source of licensed game references; scripts deploy only into independent marked copies under `instances/`. Source/game/save/dependency caches are excluded by `.gitignore`.

The required architecture is standalone: Jakobian's core and loader do not require Kopernicus. Development 0.10.0 retains five enabled radial fixtures, general-solid/torus contact foundations and a detached preview, and adds five-entrypoint load/start admission. **HarmonyKSP 2.2.1.0 is now a required runtime dependency**, pinned separately by [runtime-dependency-lock.json](runtime-dependency-lock.json). Build prepares it; deployment preflights user files before installing the verified package and notices. The broader Kopernicus dependency profile below remains research/coexistence context, not a requirement to install Kopernicus. Baseline runs with Kopernicus absent; competing loader coordination remains unimplemented.

Installed and inspected:

- .NET SDK **10.0.203**, pinned by `global.json`; .NET 10 hosts only offline tools/tests.
- KSP **1.12.5**, build **03190**, Unity **2019.4.18f1**, Windows x64 player running on this Windows ARM64 host's x64 compatibility environment.
- Unity editor **2019.4.18f1** at `C:\Program Files\Unity\Editor\Unity.exe`; exact asset-project version is pinned in `unity/Jakobian.Assets/ProjectSettings/ProjectVersion.txt`.
- Python **3.13** available for pinned research/dependency scripts.
- KSP host/core runtime target **net472**, C# **7.3**; reference assemblies **1.0.3** from NuGet and game assemblies referenced with `Private=false` so they are not copied to distribution output.
- Offline inspector uses Mono.Cecil **0.11.6** to read metadata/IL call references without executing KSP code.

All NuGet lockfiles are retained. The installed Kopernicus release uses a newer net4.8/C#14 upstream build; it is **not** built from this repository's conservative plugin target. Jakobian's standalone loader and any reused source must validate actual runtime APIs. Optional Kopernicus adapters need separate ABI tests; the bootstrap does not establish those.

See [environment-report.json](environment-report.json), [dependency-lock.json](dependency-lock.json), [upstream source lock](../research/upstream-lock.json) and [validation record](validation-record.json).

## Daily commands

Run PowerShell from the main repository root:

```powershell
./tools/Build.ps1
dotnet run --project tools/Jakobian.Inspect -c Release --no-build -- template_instance docs/research/ksp-assembly-report.json
python tools/research/fetch_sources.py
python tools/research/fetch_release_sources.py
python tools/research/inventory.py
python tools/Verify-Docs.py
```

Build performs locked restore, Release build and the analytic checks, then deploys DLLs and fixtures into baseline. Close baseline first; use `-SkipDeploy` for a deliberate build-only check. `Jakobian.sln` opens all four projects in Visual Studio/Rider/VS Code; `.vscode/tasks.json` provides build, checks and inspect tasks. `Directory.Build.local.props` can set `KspRoot` for a different licensed installation without checking private paths into source. Use forward-compatible tooling but do not reference .NET 10 runtime APIs from game projects.

The tests are a fail-fast console harness, run via `dotnet run`; **`dotnet test` is not the test command for this scaffold**. They target the netstandard core in a modern host. KSP bootstrap separately verifies that the net472 assemblies can load in the game's Mono runtime. An explicit development-only native flight harness checks contact and landed reload; see [human testing](human-testing.md). Debugger attachment is not configured.

## Isolated game profiles

Prepared copies are `instances/baseline` (stock + current Jakobian development build/test pack) and `instances/kopernicus` (historical locked Kopernicus research profile). KSPCommunityFixes is retained under the latter's `.disabled/` directory after the combined startup profile stalled; `.jakobian-profile.json` records that isolation. The active profile currently excludes KSPCF. They contain independent stock files rather than shared writable hardlinks. Source template saves are not copied, and no existing player's save is loaded.

For another fresh copy:

```powershell
./tools/New-Instance.ps1 -Name my-probe
./tools/Deploy.ps1 -Instance my-probe
./tools/Boot-Probe.ps1 -Instance my-probe
```

Names are bounded lowercase identifiers. Creation rejects existing destinations; deployment requires the ownership marker and copies Jakobian's two Release DLLs, enabled body fixtures and a private local stock seed for development probes. There is no automatic deletion, Git operation, mod publication or release packaging in these scripts.

`Boot-Probe` returns a process ID/log path immediately and starts a hidden bounded watchdog (600 seconds by default, configurable with `-TimeoutSeconds`). The watchdog verifies executable/start-time identity before stopping a timed-out process and writes a `.result.json` next to the log. The diagnostic MainMenu plugin logs `BOOT_PROBE_PASS` and quits when `-jakobianBootProbe` is present. This exercises the ordinary loader's configured system and native queries without entering flight or creating a save. Keep graphics-dependent upstream profiles out of headless assumptions:

```powershell
python tools/Install-Dependencies.py my-probe
./tools/Boot-Probe.ps1 -Instance my-probe -Graphics
```

Watch the returned log for the marker. A process that remains alive or a log with assemblies listed is not a pass. For stalled probes, validate the returned PID's executable path matches this development instance before stopping it. The normal graphics probe is launched hidden and may still create a taskbar window; do not use a visible interactive UI as a necessary acceptance step in an unattended test. Re-running the dependency installer on the isolated profile will restore KSPCF into GameData; that intentionally returns to the full locked profile and is a new test, not equivalent to the active working/isolation profile.

## Locked dependency profile

| Dependency | Pinned research version | Provenance |
| --- | --- | --- |
| Kopernicus | release-251 / 1.12.1-251 | GitHub release ZIP |
| ModuleManager | 4.2.3 | GitHub release ZIP |
| HarmonyKSP | 2.2.1.0 | GitHub release ZIP |
| KSPTextureLoader | 1.0.38 | GitHub release ZIP, published 14 October 2026 |
| ModularFlightIntegrator | file/product 1.2.10.0; assembly 1.0.0.0 | binary + MIT notice from pinned Kopernicus dependency tree |
| KSPCommunityFixes | 1.41.1 | GitHub release ZIP |

The installer verifies cached/downloaded SHA-256 values against the lock and preflights every GameData path. It refuses to replace differing local files. Upstream README/license files remain with their packages; source archives remain separately available for inspection. This private profile is not a redistributable modpack and does not choose Jakobian's future release dependency minimums.

If a newly pinned upstream set cannot boot, retain its failure evidence and create a separate tested profile; do not silently downgrade it while reporting the original pin passed. The latest inspected master source and the actual release binary can be different commits; source links identify the inspected revision explicitly.

## Unity assets project

`unity/Jakobian.Assets` is a Unity 2019.4.18f1 source project with the original mapped-terrain shader and an explicit editor bundle build. Run `tools/Build-Assets.ps1` after changing shader sources; it waits for the hidden editor, stages a successful Windows x64 bundle into `GameData/Jakobian/Assets`, and writes source/bundle hashes. Intermediate output remains under ignored `artifacts/unity`. `tools/Build.ps1` deploys the staged bundle and rejects mismatched source/bundle hashes. The player consumes the reviewed staged bundle; the Unity editor is needed only for shader rebuilds.

Unity installation/version was verified; no claim that Unity license activation, batch asset compilation or an arbitrary-body shader was validated in this pass. Game C# compilation does not require opening Unity. Preserve upstream shader license/source notices if any are ported.

## What the current code does

`SphereSurface` and `TorusSurface` provide experimental nearest oriented boundary samples. `RadialShape` supplies the restricted native PQS-compatible geometry. `RadialSurfaceQuery` / `BodyQueries` publish explicit geometry/navigation/environment context. `SystemLoader` independently parses/builds/publishes/binds the new system while retaining stock bodies. `RadialTerrain` supplies native terrain heights/colors and `ScaledShapeMesh` builds map geometry. `FlightSurfaceContext` supplies the readout/scoped camera guard. `Bootstrap` / `NativeChecks` / `NativeFlightChecks` and the explicitly enabled `FlightProbe` provide native validation. The inspector records selected actual KSP members/IL calls; `--types=fragment` discovers additional native signatures and `--il=Type::Method` writes inspection-only IL into ignored artifacts.

The first restricted body parser and native radial terrain/collision path exist. Development 0.8.0 adds immutable world-snapshot persistence/reconciliation; 0.10.0 enforces the supported load/start admission routes. General-topology gameplay, local environment/flight solvers, third-party adapters and full spherical parity remain future work. Disabled examples describe that future language; the active test pack is `GameData/JakobianTestBodies`.

Development 0.6.0 adds `tools/Torus-Probe.ps1`, using a separate Unity physics scene and bounded torus contact publisher. The original Unity shader bundle is unchanged; the core/runtime DLLs are rebuilt and deployed normally. The torus provider does not yet register celestial bodies or modify player vessel/save behavior.
