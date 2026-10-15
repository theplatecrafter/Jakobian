"""Pin source corresponding to the development profile's release tags."""
import hashlib
import io
import json
from pathlib import Path
import zipfile
from fetch_sources import ROOT, get

VARIANTS = [
    ("Kopernicus/Kopernicus", "release-251", "Kopernicus-release251"),
    ("KSPModdingLibs/KSPCommunityFixes", "1.41.1", "KSPCommunityFixes-release1411"),
    ("Phantomical/KSPTextureLoader", "v1.0.38", "KSPTextureLoader-release1038"),
]
lock = ROOT / "docs/research/release-source-lock.json"
old = {r["label"]: r for r in json.loads(lock.read_text())} if lock.exists() else {}
records = []
for repo, tag, label in VARIANTS:
    ref = old[label]["commit"] if label in old else tag
    commit = json.loads(get(f"https://api.github.com/repos/{repo}/commits/{ref}"))
    sha = commit["sha"]
    url = f"https://codeload.github.com/{repo}/zip/{sha}"
    data = get(url)
    digest = hashlib.sha256(data).hexdigest()
    if label in old and digest != old[label]["archive_sha256"]:
        raise ValueError("Source hash changed: " + label)
    target = ROOT / ".research" / label
    target.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for entry in archive.infolist():
            relative = Path(*Path(entry.filename).parts[1:])
            if not relative.parts: continue
            destination = (target / relative).resolve()
            if not destination.is_relative_to(target.resolve()): raise ValueError("Unsafe archive path")
            if entry.is_dir(): destination.mkdir(parents=True, exist_ok=True)
            else:
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(archive.read(entry))
    records.append({"repository": repo, "tag": tag, "label": label, "commit": sha,
                    "archive_url": url, "archive_sha256": digest, "local_directory": f".research/{label}"})
    print(label + " " + sha, flush=True)
lock.write_text(json.dumps(records, indent=2) + "\n")
