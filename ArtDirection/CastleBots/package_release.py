"""Run from the repository root after a successful Unity bot verification."""
from pathlib import Path
import hashlib
import json
import tarfile
import zipfile

out = Path(__file__).resolve().parent
root = out.parents[1]
report = (out / 'Validation.txt').read_text()
assert sum(line.startswith('PASS ') for line in report.splitlines()) == 46
assert not any(line.startswith('FAIL ') for line in report.splitlines())
integration = [
    'Assets/Scripts/CastleSurvivor.cs',
    'Assets/Scripts/CastlePlaySession.cs',
    'Assets/Scripts/CastleWorldMenu.cs',
    'Assets/CandyPlayers/CandyPlayerAvatar.cs',
    'Assets/HandDemo/Scripts/FlyingHandController.cs',
    'Assets/HandDemo/Scripts/FlyingHandInputSource.cs',
]
package = out / 'CastleBots.unitypackage'
with tarfile.open(package, 'r:gz') as tar:
    members = {m.name: m for m in tar.getmembers()}
    exported = {}
    for name in members:
        if name.endswith('/pathname'):
            path = tar.extractfile(members[name]).read().decode().strip()
            asset = name.rsplit('/', 1)[0] + '/asset'
            if asset in members:
                exported[path] = tar.extractfile(members[asset]).read()
    required = integration + ['Assets/CastleBots/CastleBotBrain.cs',
        'Assets/CastleBots/CastleBotPopulation.cs', 'Assets/CastleBots/README.md',
        'Assets/CastleBots/CastleHandBotBrain.cs', 'Assets/CastleBots/CastleHandBotPopulation.cs',
        'Assets/CandyPhysicsHands/CandyFlyingHandBridge.cs', 'Assets/CandyPhysicsHands/ICandyHandPoseSource.cs']
    for path in required:
        assert exported.get(path) == (root / path).read_bytes(), f'Missing or stale export: {path}'
files = [package, Path(__file__).resolve()]
files += [out / name for name in ['Validation.txt', 'COMPLETE.txt', 'Traversal.txt', 'HandContact.txt', 'npc-castle.png', 'ai-hands-castle.png']]
files += [p for p in (root / 'Assets/CandyPhysicsHands').rglob('*') if p.is_file()]
files += [p for p in (root / 'Assets/CastleBots').rglob('*') if p.is_file()]
files += [root / 'Assets/CastleBots.meta', root / 'Assets/CandyPhysicsHands.meta']
files += [root / path for path in integration]
files += [root / (path + '.meta') for path in integration]
manifest = out / 'PACKAGE_MANIFEST.json'
manifest.write_text(json.dumps({
    'module': 'Local Castle Bots and AI Hands', 'passed': 46, 'failed': 0,
    'limits': 'Local editor simulation; real Quest performance and network authority not verified.',
    'sha256': {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
               for p in files},
}, indent=2) + '\n')
archive = out / 'CastleBots-Reapply.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
    for p in files + [manifest]:
        z.write(p, p.relative_to(root).as_posix())
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
print(f'Verified {len(exported)} exported assets; {len(files)} hashed files; {archive.stat().st_size:,} bytes.')
