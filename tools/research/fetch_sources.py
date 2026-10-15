"""Fetch pinned public source archives for inspection; never executes upstream code."""
import hashlib
import io
import json
from pathlib import Path
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
REPOSITORIES = [
    "Kopernicus/Kopernicus",
    "Kopernicus/KopernicusExamples",
    "sarbian/ModularFlightIntegrator",
    "KSPModdingLibs/KSPCommunityFixes",
    "sarbian/ModuleManager",
]


def get(url):
    request = urllib.request.Request(url, headers={"User-Agent": "Jakobian-research"})
    with urllib.request.urlopen(request, timeout=90) as response:
        return response.read()


def main():
    lock = ROOT / "docs" / "research" / "upstream-lock.json"
    pinned = {record["repository"]: record for record in json.loads(lock.read_text())} if lock.exists() else {}
    records = []
    for repo in REPOSITORIES:
        metadata = json.loads(get("https://api.github.com/repos/" + repo))
        branch = metadata["default_branch"]
        ref = pinned[repo]["commit"] if repo in pinned else branch
        commit = json.loads(get(f"https://api.github.com/repos/{repo}/commits/{ref}"))
        sha = commit["sha"]
        url = f"https://codeload.github.com/{repo}/zip/{sha}"
        archive = get(url)
        if repo in pinned and hashlib.sha256(archive).hexdigest() != pinned[repo]["archive_sha256"]:
            raise ValueError("Pinned source archive hash changed: " + repo)
        target = ROOT / ".research" / metadata["name"]
        target.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(io.BytesIO(archive)) as source:
            for member in source.infolist():
                relative = Path(*Path(member.filename).parts[1:])
                if not relative.parts:
                    continue
                destination = (target / relative).resolve()
                if not destination.is_relative_to(target.resolve()):
                    raise ValueError("Unsafe source archive path")
                if member.is_dir():
                    destination.mkdir(parents=True, exist_ok=True)
                else:
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    destination.write_bytes(source.read(member))
        record = {
            "repository": repo, "branch": branch, "commit": sha,
            "commit_date": commit["commit"]["committer"]["date"],
            "archive_url": url, "archive_sha256": hashlib.sha256(archive).hexdigest(),
            "license_metadata": metadata.get("license"),
            "local_directory": f".research/{metadata['name']}",
        }
        records.append(record)
        print(f"{repo} {sha} {len(archive)} bytes", flush=True)
    lock.parent.mkdir(parents=True, exist_ok=True)
    lock.write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
