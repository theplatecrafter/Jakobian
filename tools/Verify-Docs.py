"""Check local Markdown links and proposal example braces, not game schema semantics."""
from pathlib import Path
import re
import urllib.parse

ROOT = Path(__file__).resolve().parents[1]
errors = []
links = 0
for file in (ROOT / "docs").rglob("*.md"):
    text = file.read_text(encoding="utf-8")
    for target in re.findall(r'(?<!!)\[[^\]]*\]\(([^)]+)\)', text):
        target = target.strip("<>").split("#", 1)[0]
        if not target or "://" in target or target.startswith("mailto:"):
            continue
        links += 1
        if not (file.parent / urllib.parse.unquote(target)).exists():
            errors.append(f"{file.relative_to(ROOT)}: missing {target}")
for file in (ROOT / "examples").glob("*.cfg.disabled"):
    content = "\n".join(line.split("//", 1)[0] for line in file.read_text().splitlines())
    level = 0
    for char in content:
        if char == "{": level += 1
        if char == "}": level -= 1
        if level < 0: errors.append(str(file) + ": unexpected close brace")
    if level: errors.append(str(file) + ": unbalanced braces")
if errors:
    raise SystemExit("\n".join(errors))
print(f"PASS: {links} local documentation links and proposal example braces. No loader/schema validation implied.")
