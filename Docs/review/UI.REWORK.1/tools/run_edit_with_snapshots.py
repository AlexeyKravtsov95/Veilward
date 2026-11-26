import subprocess
import sys
from pathlib import Path

EDIT_RESULT = Path('TestResults/UIREWORK1/EditMode.json')
paths = list(Path('Assets/_Game/Art/Materials').glob('*.mat')) + [
    Path('Assets/Settings/Mobile_RPAsset.asset'),
    Path('Assets/_Game/Scenes/Battle.unity'),
    Path('Assets/_Game/Scenes/MainMenu.unity'),
    Path('Assets/_Game/Prefabs/Shade.prefab'),
    Path('Assets/_Game/Prefabs/TestTower.prefab'),
]
before = {p: p.read_bytes() for p in paths}
backup = EDIT_RESULT.parent / 'edit-snapshot'
for p, data in before.items():
    destination = backup / p
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(data)
try:
    cmd = ['unity', 'command', 'test', 'run', '--caller', 'plugin', '--skill', 'unity-cli',
           '--format', 'json', '--timeout', '600', '--', '--mode', 'editor']
    with EDIT_RESULT.open('w') as report:
        proc = subprocess.run(cmd, stdout=report, check=False)
    sys.exit(proc.returncode)
finally:
    # Не даём внешнему восстановлению открыть диалог поверх активной сцены.
    subprocess.run(['unity', 'command', 'scene', 'open', '--caller', 'plugin', '--skill', 'unity-cli',
                    '--format', 'json', '--', '--path', 'Assets/_Game/Scenes/Bootstrap.unity'], check=True)
    changed = []
    for p, data in before.items():
        if p.read_bytes() != data:
            p.write_bytes(data)
            changed.append(str(p))
    print('Restored test-produced files:', len(changed))
    (EDIT_RESULT.parent / 'edit-restored.tsv').write_text('\n'.join(changed) + '\n')
