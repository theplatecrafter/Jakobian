"""Install the locked research profile only into a marked local development copy."""
import hashlib
import json
from pathlib import Path
import re
import sys
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    if len(sys.argv) != 2 or not re.fullmatch(r"[a-z][a-z0-9-]{0,40}", sys.argv[1]):
        raise SystemExit("Usage: python tools/Install-Dependencies.py <instance-name>")
    instances = (ROOT / "instances").resolve()
    target = (instances / sys.argv[1]).resolve()
    if not target.is_relative_to(instances) or target == instances:
        raise ValueError("Instance escapes workspace")
    marker = target / ".jakobian-instance.json"
    if not marker.exists() or json.loads(marker.read_text(encoding="utf-8-sig"))["owner"] != "Jakobian":
        raise ValueError("Not a Jakobian development instance")
    records = json.loads((ROOT / "docs/development/dependency-lock.json").read_text())
    pending = []
    for record in records:
        cache = ROOT / record["cache"]
        if not cache.exists():
            cache.parent.mkdir(parents=True, exist_ok=True)
            cache.write_bytes(urllib.request.urlopen(record["url"], timeout=90).read())
        data = cache.read_bytes()
        if hashlib.sha256(data).hexdigest() != record["sha256"]:
            raise ValueError("Hash mismatch: " + record["name"])
        if record["kind"] == "dll":
            pending.append((record["destination"], data))
        else:
            with zipfile.ZipFile(cache) as archive:
                for entry in archive.infolist():
                    if entry.is_dir():
                        continue
                    name = entry.filename.replace("\\", "/")
                    if name.startswith("GameData/"):
                        pending.append((name, archive.read(entry)))
                    elif re.fullmatch(r"ModuleManager\.[0-9.]+\.dll", name):
                        pending.append(("GameData/" + name, archive.read(entry)))
    # Validate every destination before writing. Never overwrite a different local install.
    seen = {}
    for relative, data in pending:
        path = (target / relative).resolve()
        if not path.is_relative_to(target / "GameData"):
            raise ValueError("Unsafe archive path")
        key = str(path).casefold()
        if key in seen and seen[key] != data:
            raise ValueError("Conflicting package entries")
        seen[key] = data
        if path.exists() and path.read_bytes() != data:
            raise ValueError("Existing file differs: " + relative)
    for relative, data in pending:
        path = target / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    (target / ".jakobian-dependencies.json").write_text(json.dumps(records, indent=2) + "\n")
    print("Installed locked research dependencies into " + str(target))


if __name__ == "__main__":
    main()
