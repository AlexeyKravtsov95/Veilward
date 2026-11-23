"""Read-only applicability check of accepted PR85 certificate; no Unity run or user-file rewrite."""
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3]
HISTORY=ROOT/'Docs/review/ART.3e/canonical-inputs'
BASE='d7895fe0927f6d5890a2439fcdae4168dd224e1a'
def sha(data):return hashlib.sha256(data).hexdigest()
def require(value,why):
    if not value:raise AssertionError(why)
def verify():
    certificate=HISTORY/'equivalence.json'
    require(sha(certificate.read_bytes())=='2eb7c644caf12c748652683764ef99fa2628d18773dd3927a6c0478efac86e3d','accepted certificate identity')
    proof=json.loads(certificate.read_text())
    spec=importlib.util.spec_from_file_location('historical_inputs',HISTORY/'verify_inputs.py')
    historical=importlib.util.module_from_spec(spec);spec.loader.exec_module(historical)
    packages=historical.package_map()
    for record in proof['files']:
        path=record['path']
        require(sha((ROOT/path).read_bytes())==record['carryInSha256'],'exact loaded carry-in bytes '+path)
        require(sha(subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT))==record['canonicalSha256'],'canonical baseline '+path)
    carry_in={r['path']:r for r in proof['files']}
    for path,digest in proof['unchangedSettingsSha256'].items():
        if path in carry_in:
            require(digest==carry_in[path]['canonicalSha256'],'pinned canonical settings '+path)
            continue
        require(sha((ROOT/path).read_bytes())==digest,'settings/package drift '+path)
    exceptions={
        'Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs':lambda s:s.replace('SteamModelBuilder.Bind(root);','StormModelBuilder.Bind(root);'),
        'Assets/_Game/Scripts/Presentation/FlameModelBinding.cs':lambda s:s.replace('[SerializeField] private Transform[] steamLevels;','[SerializeField] private Transform[] steamLevels;\n        [SerializeField] private Transform[] stormLevels;').replace('== "steam" ? steamLevels : null','== "steam" ? steamLevels : definitionId.Value == "storm" ? stormLevels : null')}
    checked=0
    for path,digest in proof['dependencySha256'].items():
        if path.startswith('Packages/'):
            _,package,relative=path.split('/',2);data=(packages[package]/relative).read_bytes()
        else:data=(ROOT/path).read_bytes()
        if path in exceptions:
            original=subprocess.check_output(['git','show',proof['testedSource']+':'+path],cwd=ROOT)
            require(sha(original)==digest,'old dependency identity '+path)
            require(data==exceptions[path](original.decode()).encode(),'narrow model wiring delta '+path)
        else:require(sha(data)==digest,'shader/consumer dependency drift '+path)
        checked+=1
    # Новая builder ветка только выбирает shared model references; material consumer не добавлен.
    old=(ROOT/'Assets/_Game/Scripts/Editor/SteamModelBuilder.cs').read_text()
    expected=old.replace('Steam','Storm').replace('STEAM','STORM').replace('steam','storm').replace('Fox','Carp').replace('ART.3e','ART.3f').replace('StoneModelBuilder.Bind(root);','SteamModelBuilder.Bind(root);')
    require((ROOT/'Assets/_Game/Scripts/Editor/StormModelBuilder.cs').read_text()==expected,'shared-palette builder delta')
    return dict(certificate='Docs/review/ART.3e/canonical-inputs/equivalence.json',certificateSha256=sha(certificate.read_bytes()),exactCarryInFiles=4,dependencies=checked,unchangedSettings=len(proof['unchangedSettingsSha256']),narrowConsumerDeltas=sorted(exceptions),shaderProofRerun=False,newUnityRun=False,equivalentForBattleRendering=True,scope=proof['scope'])
if __name__=='__main__':print(json.dumps(verify(),indent=2))
