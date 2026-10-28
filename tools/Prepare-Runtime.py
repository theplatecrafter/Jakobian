"""Materialize the hash-locked community Harmony distribution, optionally preflight/install it.

No upstream binaries are committed. Different existing user dependencies are never replaced.
"""
import hashlib
import json
from pathlib import Path
import re
import sys
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
LOCK = json.loads((ROOT / 'docs/development/runtime-dependency-lock.json').read_text())
CACHE = ROOT / '.research/runtime/HarmonyKSP/GameData/000_Harmony'


def checked(data, expected):
    if hashlib.sha256(data).hexdigest() != expected:
        raise ValueError('Runtime dependency hash mismatch')
    return data


def main():
    archive = ROOT / LOCK['cache']
    if not archive.exists():
        archive.parent.mkdir(parents=True, exist_ok=True)
        archive.write_bytes(checked(urllib.request.urlopen(LOCK['url'], timeout=90).read(), LOCK['sha256']))
    checked(archive.read_bytes(), LOCK['sha256'])
    pending = {}
    with zipfile.ZipFile(archive) as package:
        for name, digest in LOCK['files'].items():
            pending[name] = checked(package.read('GameData/000_Harmony/' + name), digest)
    for notice in LOCK['notices']:
        source = ROOT / '.research/runtime-notices' / notice['name']
        if not source.exists():
            source.parent.mkdir(parents=True, exist_ok=True)
            source.write_bytes(checked(urllib.request.urlopen(notice['url'], timeout=90).read(), notice['sha256']))
        pending[notice['name']] = checked(source.read_bytes(), notice['sha256'])
    CACHE.mkdir(parents=True, exist_ok=True)
    for name, data in pending.items():
        destination = CACHE / name
        if destination.exists() and destination.read_bytes() != data:
            raise ValueError('Modified runtime cache; investigate: ' + str(destination))
    for name, data in pending.items():
        (CACHE / name).write_bytes(data)
    if len(sys.argv) > 1:
        if len(sys.argv) != 3 or sys.argv[1] not in ('--preflight', '--install') or not re.fullmatch(r'[a-z][a-z0-9-]{0,40}', sys.argv[2]):
            raise ValueError('Usage: Prepare-Runtime.py [--preflight|--install instance]')
        instance = (ROOT / 'instances' / sys.argv[2]).resolve()
        if not instance.is_relative_to((ROOT / 'instances').resolve()) or json.loads((instance / '.jakobian-instance.json').read_text(encoding='utf-8-sig'))['owner'] != 'Jakobian':
            raise ValueError('Not a managed Jakobian instance')
        target = instance / 'GameData/000_Harmony'
        if not target.resolve().is_relative_to(instance / 'GameData'):
            raise ValueError('Dependency path escapes GameData')
        for path in (instance / 'GameData', target):
            if path.is_symlink() or path.is_junction():
                raise ValueError('Runtime installation may not traverse links/junctions')
        for existing in (instance / 'GameData').rglob('0Harmony.dll'):
            if existing.resolve() != (target / '0Harmony.dll').resolve():
                raise ValueError('Another Harmony installation exists; not creating a duplicate: ' + str(existing))
        for name, data in pending.items():
            dest = target / name
            if dest.is_symlink() or (dest.exists() and dest.read_bytes() != data):
                raise ValueError('Existing runtime dependency differs; not replacing: ' + str(dest))
        if sys.argv[1] == '--install':
            target.mkdir(parents=True, exist_ok=True)
            for name, data in pending.items():
                (target / name).write_bytes(data)
    print('HarmonyKSP 2.2.1.0: hash-locked runtime prepared' + ('; ' + sys.argv[1] if len(sys.argv) > 1 else ''))


if __name__ == '__main__':
    main()
