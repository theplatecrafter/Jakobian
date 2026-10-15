# Startup profile investigation — 14 October 2026

The diagnostic scaffold reached MainMenu in stock KSP and in the Kopernicus dependency profile without optional KSPCommunityFixes. This is bootstrap coexistence evidence, not arbitrary-body flight or graphics compatibility.

| Attempt | Profile / mode | Result |
| --- | --- | --- |
| `baseline-20261014-130933.log` | stock + scaffold, batch/headless | bootstrap marker, self-exit |
| `kopernicus-20261014-131515.log` | full locked profile including KSPCF, batch/headless | stalled before MainMenu; terminated by this task |
| `kopernicus-20261014-132203.log` | same full profile, batch with D3D11 | stalled before MainMenu; terminated by this task |
| `kopernicus-20261014-132525.log` | same full profile, normal D3D11 startup | stalled before MainMenu; terminated by this task |
| `kopernicus-20261014-133010.log` | same Kopernicus dependencies, KSPCF directory moved outside GameData, normal D3D11 | bootstrap marker, self-exit; one `SpaceCenter.Start` NullReferenceException |

Logs remain under `artifacts/boot/`; hashes and relevant markers are in [validation-record.json](validation-record.json), with compact [boot evidence](boot-evidence.txt).

ModuleManager completed its patches/cache processing in the stalled attempts. Logs include KSPCF's intentional stock-loader coroutine termination and stop progressing during the early loader stage. The same supplied game/dependency pins advance through native texture/model/part/system loading with KSPCF excluded. This narrows further investigation to the combined startup profile; it does **not** prove the exact failing method or establish that KSPCF alone is defective. The scaffold's code runs at MainMenu and installs no loader/physics patches.

KSPCF 1.41.1 release source's FastLoader injects its replacement loading path at startup; inspecting that path and its interaction with the exact texture-loader version/ARM64 host is a sensible next experiment. Do not claim that a ModuleManager `FastLoader = false` setting disables it: the inspected release implementation installs this loader separately. A safe follow-up compares stock+KSPCF, texture-loader options where supported, and alternate explicitly pinned dependency versions, each in its own profile with controlled settings.

Active development profile currently excludes KSPCF and records this in `.jakobian-profile.json`. Original KSPCF package files remain in `instances/kopernicus/.disabled/KSPCommunityFixes`; the locked dependency manifest retains the original package. Reinstalling the full dependency lock restores that profile. No upstream binaries were modified, no release minimums were chosen from this test, and no player saves were loaded.

The successful modded boot's exception stack names **`SpaceCenter.Start`** before the Jakobian MainMenu marker. Its cause was not established, and no KSC/flight validation was performed. Retain it as an explicit limitation rather than describing the profile as exception-free.
