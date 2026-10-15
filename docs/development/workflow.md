# Development workflow

The owner manages commits, repository metadata, remotes and publication. Development may create or merge branches when useful. Start on **main**, use descriptive branch names without a `codex/` prefix, and do not commit, push, open PRs or publish without an explicit request. The existing unborn branch was renamed to `main`; no work was discarded or committed.

Develop the standalone Jakobian loader/runtime directly. Use pinned Kopernicus/Ringworld source as research inputs and preserve provenance for any future adapted code. Keep authored Markdown under `docs/`.

`instances/baseline` is the owner's current human-testing installation. Every successful Release build through `tools/Build.ps1` deploys the two current DLLs and the test-body pack there. Deployment rejects a running baseline game. Close it before rebuilding; do not replace loaded DLLs or player saves. `-SkipDeploy` permits a deliberate build-only check. The source `template_instance` remains untouched.

Keep fixture body names, IDs and indices stable after their first usable build. Add new test bodies with new IDs/indices; do not move occupied geometry without a validated migration. Automatic game probes use uniquely named private saves and never overwrite a player's save. Licensed stock seed data is copied only into ignored local instances.

Each development handoff includes the current build, enabled test bodies, completed checks, known limitations and the next concrete implementation task. Broad framework capabilities are tracked separately from the subset implemented in a particular development build.
