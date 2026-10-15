"""Create a reviewable parser-target inventory from the pinned source tree."""
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
source = ROOT / ".research/Kopernicus/src"
targets = []
pattern = re.compile(r'\[ParserTarget(Collection)?\("([^"\n]+)"')
for file in sorted(source.rglob("*.cs")):
    for line, text in enumerate(file.read_text(encoding="utf-8-sig").splitlines(), 1):
        for match in pattern.finditer(text):
            targets.append({"path": file.relative_to(source.parent).as_posix(), "line": line,
                            "key": match.group(2), "collection": bool(match.group(1))})
output = ROOT / "docs/research/parser-target-inventory.json"
output.write_text(json.dumps({"scope": "Static parser attribute inventory; not runtime feature coverage", "targets": targets}, indent=2) + "\n")
print(f"Inventoried {len(targets)} parser attributes -> {output}")
