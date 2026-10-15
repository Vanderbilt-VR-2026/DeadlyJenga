"""Package the verified hand module; run from the repository root."""
from pathlib import Path
import hashlib
import json
import tarfile
import zipfile

root = Path(__file__).resolve().parents[2]
out = Path(__file__).resolve().parent
package = out / 'CandyPhysicsHands.unitypackage'
integration = [
    'Assets/CastlePrototype/Scripts/CastlePlaySession.cs',
    'Assets/CandyPlayers/CandySmoothHandMesh.cs',
    'Assets/CandyPlayers/CandyTrackingSource.cs',
    'Assets/CandyPlayers/CandyPlayerRig.cs',
    'Assets/CandyPlayers/Editor/CandyPlayerBuilder.cs',
]
required = integration + ['Assets/CandyPhysicsHands/README.md',
    'Assets/CandyPhysicsHands/CandyPhysicalHand.cs',
    'Assets/CastlePrototype/Prefabs/CastleFlyingHand.prefab',
    'Assets/CandyPlayers/CandyHandSkeleton.cs']
with tarfile.open(package, 'r:gz') as tar:
    members = {m.name: m for m in tar.getmembers()}
    exported = {}
    for name in members:
        if name.endswith('/pathname'):
            path = tar.extractfile(members[name]).read().decode().strip()
            asset = name.rsplit('/', 1)[0] + '/asset'
            if asset in members:
                exported[path] = tar.extractfile(members[asset]).read()
    for path in required:
        assert path in exported, f'Missing export: {path}'
        assert exported[path] == (root / path).read_bytes(), f'Stale export: {path}'
report = (out / 'Validation.txt').read_text()
assert sum(line.startswith('PASS ') for line in report.splitlines()) == 33
assert not any(line.startswith('FAIL ') for line in report.splitlines())
files = [package, out / 'HANDOFF.md', out / 'Validation.txt', out / 'COMPLETE.txt',
         Path(__file__).resolve()]
files += [out / name for name in ['contact-pinch.png', 'contact-lift.png',
    'contact-release.png', 'castle-candy-pointer.png', 'giant-tracked-pose.png', 'GiantTracking.txt']]
files += sorted((root / 'Assets/CandyPhysicsHands').rglob('*'))
files += [root / 'Assets/CandyPhysicsHands.meta']
files += [root / path for path in integration]
files += [root / (path + '.meta') for path in integration]
files = sorted(set(p for p in files if p.is_file()))
manifest = {
    'module': 'Candy Physics Hands', 'checks_passed': 33, 'checks_failed': 0,
    'validation': 'Synthetic input and native Unity physics; hardware validation required.',
    'files': {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
              for p in files},
}
manifest_path = out / 'PACKAGE_MANIFEST.json'
manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
archive = out / 'CandyPhysicsHands-Reapply.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
    for p in files + [manifest_path]:
        z.write(p, p.relative_to(root).as_posix())
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
print(f'Verified {len(exported)} exported assets; {len(files)} hashed files; archive {archive.stat().st_size:,} bytes.')
