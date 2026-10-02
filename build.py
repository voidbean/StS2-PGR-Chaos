#!/usr/bin/env python3
"""Build and package locally. Never installs, launches the game, or writes to Steam."""
import os, shutil, struct, subprocess, tempfile
from pathlib import Path
ROOT = Path(__file__).resolve().parent
GODOT = Path(os.environ.get('MEGADOT_PATH', str(Path.home() / 'Applications/MegaDot.app/Contents/MacOS/Godot')))
if not GODOT.is_file():
    raise SystemExit('Set MEGADOT_PATH to the MegaDot executable.')
subprocess.run(['dotnet', 'build', str(ROOT/'ChaosPrototype.csproj'), '-c', 'Release', '--nologo'], check=True, timeout=180)
# Avoid presenting stale files as the result of a failed resource export.
dist = ROOT/'dist'/'ChaosPrototype'
dist.mkdir(parents=True, exist_ok=True)
pck = dist/'ChaosPrototype.pck'
if pck.exists(): pck.unlink()
for args in [ ['--headless', '--path', str(ROOT/'assets'), '--editor', '--import'],
              ['--headless', '--path', str(ROOT/'assets'), '--export-pack', 'ModAssets', str(pck)]]:
    subprocess.run([str(GODOT), *args], check=True, timeout=180)
with pck.open('rb') as f:
    magic, fmt, major, minor, patch = struct.unpack('<5I', f.read(20))
if magic != 0x43504447 or (major, minor, patch) != (4, 5, 1):
    raise SystemExit('Unexpected PCK format/engine version; package not accepted.')
with tempfile.TemporaryDirectory(prefix='chaos-pack-check-') as test_root:
    Path(test_root, 'project.godot').write_text('config_version=5\n')
    subprocess.run([str(GODOT), '--headless', '--path', test_root,
                    '--script', str(ROOT/'tests/check_pack.gd'), '--', str(pck)],
                   check=True, timeout=60)
shutil.copy2(ROOT/'bin/Release/net9.0/ChaosPrototype.dll', dist)
shutil.copy2(ROOT/'ChaosPrototype.json', dist)
print('Package ready:', dist)
print('Not installed. BaseLib 3.4.7 must be installed separately.')
