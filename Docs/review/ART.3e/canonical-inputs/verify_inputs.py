"""Read-only comparison of exact ART3E Git/carry-in rendering bytes; no Unity or file writes."""
import difflib
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
sys.dont_write_bytecode = True

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
SOURCE = '9654133029902fb2541ee833d6ef80710755a5bb'
PIPELINE = 'Assets/Settings/Mobile_RPAsset.asset'
MATERIALS = ['Assets/_Game/Art/Materials/' + n + '.mat' for n in
             ('GrayboxSelection', 'GrayboxTower', 'GrayboxTowerPreview')]
DEFAULTS = {'m_ReflectionProbeAtlas': 1, 'm_AllowPostProcessAlphaOutput': 0,
    'm_PrefilterAlphaOutput': 0, 'm_PrefilterScreenSpaceIrradiance': 0,
    'm_PrefilterBicubicLightmapSampling': 0, 'm_PrefilterReflectionProbeRotation': 0,
    'm_PrefilterReflectionProbeBlending': 0, 'm_PrefilterReflectionProbeBoxProjection': 0,
    'm_PrefilterReflectionProbeAtlas': 0}


def require(value, message):
    if not value:
        raise AssertionError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def scalar(text, name):
    matches = re.findall(r'^  ' + re.escape(name) + r': (.*)$', text, re.M)
    require(len(matches) == 1, 'unique scalar ' + name)
    return matches[0]


def compare_pipeline(canonical, carry):
    rows = []
    for name in ('k_AssetVersion', 'k_AssetPreviousVersion'):
        require(scalar(canonical, name) == '12' and scalar(carry, name) == '13', name)
        rows.append(dict(field=name, canonical='12', carry='13', rule='URP12to13 migration bookkeeping'))
        canonical = canonical.replace('  ' + name + ': 12\n', '  ' + name + ': 13\n')
    for name, value in DEFAULTS.items():
        require(not re.search(r'^  ' + name + ':', canonical, re.M), 'missing-field default ' + name)
        require(scalar(carry, name) == str(value), 'default changed ' + name)
        carry = carry.replace('  ' + name + ': ' + str(value) + '\n', '')
        rows.append(dict(field=name, canonical='absent; C# initializer=' + str(value), carry=str(value), rule='explicit current serialized default'))
    require(scalar(canonical, 'm_UseLegacyLightmaps') == '0', 'obsolete legacy value')
    require(not re.search(r'^  m_UseLegacyLightmaps:', carry, re.M), 'obsolete field absent')
    canonical = canonical.replace('  m_UseLegacyLightmaps: 0\n', '')
    rows.append(dict(field='m_UseLegacyLightmaps', canonical='0', carry='absent', rule='not a current class field or FormerlySerializedAs target'))
    require(canonical == carry, 'unaccounted pipeline bytes; no broad field ignore')
    return rows


def compare_material(canonical, carry):
    pattern = r'^    - _Color: (.*)$'
    a = re.findall(pattern, canonical, re.M); b = re.findall(pattern, carry, re.M)
    require(len(a) == len(b) == 1, 'single legacy Color entry')
    base = re.findall(r'^    - _BaseColor: (.*)$', canonical, re.M)
    require(len(base) == 1 and base == re.findall(r'^    - _BaseColor: (.*)$', carry, re.M), 'active BaseColor drift')
    require(a[0] == '{r: 1, g: 1, b: 1, a: 1}', 'canonical legacy Color')
    numbers = lambda s: [float(x) for x in re.findall(r'[rgba]: ([0-9.]+)', s)]
    require(len(numbers(b[0])) == 4 and max(abs(x-y) for x,y in zip(numbers(b[0]), numbers(base[0]))) < 1e-7,
            'legacy alias differs from active color')
    require(canonical.replace('    - _Color: ' + a[0], '    - _Color: ' + b[0]) == carry,
            'unaccounted material bytes (shader/texture/keywords/render state included)')
    require('guid: 933532a4fcc9baf4fa0491de14d08ed7' in canonical, 'URP Lit GUID')
    return dict(field='m_SavedProperties.m_Colors._Color', canonical=a[0], carry=b[0],
                activeBaseColor=base[0], rule='obsolete lightmapper alias; not consumed by Lit GPU passes or Battle Presentation')


def package_map():
    result = {}
    for name in ('com.unity.render-pipelines.universal', 'com.unity.render-pipelines.core',
                 'com.unity.render-pipelines.universal-config'):
        candidates = list((ROOT/'Library/PackageCache').glob(name+'@*'))
        require(len(candidates) == 1, 'one installed package ' + name)
        result[name] = candidates[0]
        expected = '17.0.3' if name.endswith('-config') else '17.3.0'
        require(json.loads((candidates[0]/'package.json').read_text())['version'] == expected, 'pinned package '+name)
    return result


def import_versions(material, project, postprocessor):
    # Legacy UpgradeV1 умеет обратный Color→BaseColor перенос, но он не достижим при version10.
    require('guid: d0353a89b1f911e48b9e16bdc9f2e058' in material and scalar(material, 'version') == '10',
            'existing embedded Material AssetVersion10')
    require(scalar(project, 'm_LastMaterialVersion') == '10', 'project material version10 / no version-triggered reimport')
    array = re.findall(r'k_Upgraders = \{ ([^}]+) \};', postprocessor)
    require(len(array) == 1 and array[0].split(', ') == ['UpgradeV'+str(n) for n in range(1,11)], 'exact ten material upgraders')
    require('if (subAsset is AssetVersion sub)' in postprocessor and 'assetVersion = sub;' in postprocessor and
            'while (assetVersion.version >= 0 && assetVersion.version < k_Upgraders.Length)' in postprocessor,
            'existing subasset branch / guarded material migration')
    require('RenameColor("_Color", "_BaseColor");' in code_block(postprocessor, 'public LitUpdaterV1(string oldShaderName)'),
            'inverse legacy alias migration explicitly accounted')
    return dict(embeddedAssetVersion=10, projectVersion=10, upgraderCount=10,
                upgradeLoopEntered=False, inverseColorToBaseColorUpdaterReachable=False)


def code_block(text, anchor):
    start = text.index(anchor); begin = text.index('{', start); depth = 0
    for index in range(begin, len(text)):
        if text[index] == '{': depth += 1
        if text[index] == '}': depth -= 1
        if depth == 0: return text[begin+1:index]
    raise AssertionError('unclosed code block')


def compute():
    old = json.loads((HERE.parent/'reports/final-gates-restored.json').read_text())['carryInSha256']
    files = []; diff = []
    for name in [PIPELINE] + MATERIALS:
        filename = Path(name).name
        a = (HERE/'canonical'/filename).read_bytes(); b = (HERE/'carry-in'/filename).read_bytes()
        require(a == subprocess.check_output(['git', 'show', SOURCE+':'+name], cwd=ROOT), 'exact canonical blob '+name)
        require(sha(b) == old[name], 'exact historically used carry-in '+name)
        changes = compare_pipeline(a.decode(), b.decode()) if name == PIPELINE else [compare_material(a.decode(), b.decode())]
        files.append(dict(path=name, canonicalSha256=sha(a), carryInSha256=sha(b), changes=changes,
                          allOtherBytesIdentical=True))
        diff.extend(difflib.unified_diff(a.decode().splitlines(True), b.decode().splitlines(True),
                    fromfile='canonical/'+name, tofile='carry-in/'+name))

    packages = package_map()
    vendor = {'Packages/'+name+'/package.json':sha((path/'package.json').read_bytes()) for name,path in packages.items()}
    def read(package, relative):
        path = packages[package]/relative; data = path.read_bytes()
        vendor['Packages/'+package+'/'+relative] = sha(data)
        return data.decode()
    urp = 'com.unity.render-pipelines.universal'; core = 'com.unity.render-pipelines.core'
    asset = read(urp, 'Runtime/Data/UniversalRenderPipelineAsset.cs')
    prefilter = read(urp, 'Runtime/Data/UniversalRenderPipelineAssetPrefiltering.cs')
    deprecated = read(urp, 'Runtime/Deprecated.cs')
    resources = read(urp, 'Runtime/Data/UniversalRenderPipelineAsset.DefaultResources.cs')
    for name, value in DEFAULTS.items():
        require(re.search(r'\bbool '+name+r' = '+('true' if value else 'false')+r';', asset+'\n'+prefilter), 'C# default '+name)
    require(code_block(asset, 'if (k_AssetVersion < 13)').strip() ==
            'k_AssetPreviousVersion = k_AssetVersion;\n                k_AssetVersion = 13;', 'migration12to13')
    require(code_block(asset, 'if (asset.k_AssetPreviousVersion < 13)').strip() ==
            'asset.k_AssetPreviousVersion = 13;', 'delayed version update')
    require('m_UseLegacyLightmaps' not in asset+prefilter+deprecated+resources, 'obsolete field really absent')
    reloader = read(core, 'Runtime/Utilities/ResourceReloader.cs')
    # Проверка хвоста UpgradeAsset включает ReloadAllNullIn, а не скрывает его за version rule.
    require('ResourceReloader.ReloadAllNullIn(asset, packagePath);\n            EditorUtility.SetDirty(asset);' in asset,
            'full delayed upgrade tail')
    partial = code_block(deprecated, 'public partial class UniversalRenderPipelineAsset')
    require(partial.count('ReloadGroup') == 1 and partial.count('[Reload(') == 2 and
            'TextureResources m_Textures;' in partial and
            '[Reload' not in asset+prefilter+resources, 'complete URP asset reload field inventory')
    fix = code_block(reloader, 'static bool FixGroupIfNeeded(System.Object container, FieldInfo info)')
    setter = code_block(reloader, 'static bool SetAndLoadIfNull(System.Object container, FieldInfo info,')
    require('if (IsNull(container, info))' in fix and fix.strip().endswith('return false;') and
            'if (IsNull(container, info))' in setter and setter.strip().endswith('return false;'), 'reload only null groups/refs')
    texture_refs = []
    for field, relative in [('blueNoise64LTex', 'Textures/BlueNoise64/L/LDR_LLL1_0.png'),
                            ('bayerMatrixTex', 'Textures/BayerMatrix.png')]:
        raw = (HERE/'canonical/Mobile_RPAsset.asset').read_text()
        ref = re.findall(r'^    '+field+r': \{fileID: ([0-9]+), guid: ([a-f0-9]+), type: 3\}$', raw, re.M)
        require(len(ref) == 1 and ref[0][0] != '0', 'nonnull reload resource '+field)
        meta = read(urp, relative+'.meta')
        require('guid: '+ref[0][1] in meta and 'TextureImporter:' in meta, 'reload GUID/type target '+field)
        png = (packages[urp]/relative).read_bytes()
        require(png.startswith(b'\x89PNG\r\n\x1a\n'), 'actual texture bytes '+field)
        vendor['Packages/'+urp+'/'+relative] = sha(png)
        texture_refs.append(dict(field=field, fileID=ref[0][0], guid=ref[0][1], resolved='Packages/'+urp+'/'+relative))

    shader = read(urp, 'Shaders/Lit.shader')
    require('guid: 933532a4fcc9baf4fa0491de14d08ed7' in read(urp, 'Shaders/Lit.shader.meta'), 'shader resolution')
    require('[MainColor] _BaseColor' in shader and '// ObsoleteProperties' in shader and
            not re.search(r'\b_Color\b', shader[shader.index('    SubShader'):]), 'GPU passes do not reference legacy Color')
    gui = read(urp, 'Editor/ShaderGUI/BaseShaderGUI.cs')
    require('material.SetColor("_Color", material.GetColor("_BaseColor"));' in gui, 'lightmapper alias direction')
    postprocessor = read(urp, 'Editor/AssetPostProcessors/MaterialPostprocessor.cs')
    read(urp, 'Editor/DefaultScene/UniversalProjectSettings.cs')
    project_meta = read(urp, 'Editor/DefaultScene/UniversalProjectSettings.cs.meta')
    version_meta = read(urp, 'Editor/AssetVersion.cs.meta'); read(urp, 'Editor/AssetVersion.cs')
    read(urp, 'Editor/ShaderUtils.cs'); read(urp, 'Runtime/ShaderUtils.cs')
    require('guid: d0353a89b1f911e48b9e16bdc9f2e058' in version_meta, 'embedded AssetVersion script identity')
    project_version = (ROOT/'ProjectSettings/URPProjectSettings.asset').read_bytes().decode()
    require('guid: 247994e1f5a72c2419c26a37e9334c01' in project_meta and
            'guid: 247994e1f5a72c2419c26a37e9334c01' in project_version, 'project version script identity')
    material_import = {Path(path).name:import_versions((HERE/'canonical'/Path(path).name).read_text(), project_version, postprocessor)
                       for path in MATERIALS}
    visited = set(); missing = []
    def visit(package, relative):
        key = (package, str(Path(relative)))
        if key in visited: return
        visited.add(key); text = read(*key)
        if not relative.endswith('.shader'):
            require(not re.search(r'\b_Color\b', text), 'legacy Color HLSL read '+relative)
        for include in re.findall(r'^\s*#\s*include(?:_with_pragmas)?\s+"([^"]+)"', text, re.M):
            if include.startswith('Packages/'):
                _, target_package, target_relative = include.split('/', 2)
            else: target_package, target_relative = package, str(Path(relative).parent/include)
            if target_package not in packages:
                missing.append(include); continue
            require((packages[target_package]/target_relative).is_file(), 'resolve HLSL '+include)
            visit(target_package, target_relative)
    visit(urp, 'Shaders/Lit.shader')
    optional = sorted(['Packages/com.unity.render-pipelines.gamecore/ShaderLibrary/API/GameCore.hlsl',
        'Packages/com.unity.render-pipelines.xboxone/ShaderLibrary/API/XBoxOne.hlsl',
        'Packages/com.unity.render-pipelines.ps4/ShaderLibrary/API/PSSL.hlsl',
        'Packages/com.unity.render-pipelines.ps5/ShaderLibrary/API/PSSL.hlsl',
        'Packages/com.unity.visualeffectgraph/Shaders/VFXMatricesOverride.hlsl',
        'Packages/com.unity.render-pipelines.ps5/ShaderLibrary/API/FoveatedRendering_PSSL.hlsl'])
    require(sorted(missing) == optional, 'no unknown shader dependency exclusions')
    require(not any(re.search(r'^\s*#\s*define\s+HAVE_VFX_MODIFICATION\b',
                (packages[p]/rel).read_text(), re.M) for p,rel in visited), 'no VFX Graph modification in Lit graph')
    source_files = ['Assets/_Game/Scripts/Presentation/TowerDisplay.cs', 'Assets/_Game/Scripts/Presentation/ReactionEffectView.cs',
                    'Assets/_Game/Scripts/Editor/BattleSceneBuilder.cs']
    for path in source_files:
        raw = (ROOT/path).read_bytes(); require(raw == subprocess.check_output(['git','show',SOURCE+':'+path],cwd=ROOT), 'Presentation source pin')
        vendor[path] = sha(raw)
    for path in source_files[:2]:
        require('Shader.PropertyToID("_BaseColor")' in (ROOT/path).read_text() and '"_Color"' not in (ROOT/path).read_text(), 'Battle active color property')
    for path in sorted((ROOT/'Assets/_Game/Scripts/Presentation').rglob('*.cs')):
        relative = str(path.relative_to(ROOT)); raw = path.read_bytes()
        require(raw == subprocess.check_output(['git','show',SOURCE+':'+relative],cwd=ROOT), 'all Presentation source pin')
        require('"_Color"' not in raw.decode(), 'no Battle legacy property consumer '+relative)
        vendor[relative] = sha(raw)
    fixed = sorted(set(subprocess.check_output(['git','ls-tree','-r','--name-only',SOURCE,'Assets/Settings'],cwd=ROOT,text=True).splitlines()) |
        {'ProjectSettings/GraphicsSettings.asset','ProjectSettings/QualitySettings.asset','ProjectSettings/URPProjectSettings.asset',
         'Packages/manifest.json','Packages/packages-lock.json'})
    settings = {path:sha(subprocess.check_output(['git','show',SOURCE+':'+path],cwd=ROOT)) for path in fixed}
    for path in fixed:
        if path != PIPELINE: require(sha((ROOT/path).read_bytes()) == settings[path], 'unchanged settings '+path)
    report = dict(testedSource=SOURCE, files=files, exactDiffSha256=sha(''.join(diff).encode()),
        dependencySha256=dict(sorted(vendor.items())), unchangedSettingsSha256=settings,
        shaderIncludeFiles=len(visited), optionalUninstalledIncludes=optional,
        equivalentForBattleRendering=True,
        scope='Stabilized Battle rendering values / existing macOS Editor, not editor dirty/queue state, baking or console/VFXGraph platforms',
        reloadAnalysis=dict(onlyGroup='m_Textures / TextureResources', nonnullResolvedResources=texture_refs,
            changed=False, upgradeSetDirty='serialization dirty flag; not a value mutation',
            nativeDeserializationProbeNotRun=True),
        materialImport=material_import,
        unityRerun=False, fullSuitesRerun=False, deviceTest=False)
    return report, ''.join(diff)


def main():
    report, diff = compute()
    require(report == json.loads((HERE/'equivalence.json').read_text()), 'current code/bytes match recorded semantic proof')
    require(diff == (HERE/'git-to-carry-in.diff').read_text(), 'complete exact diff')
    receipt = json.loads((HERE/'actual-inputs.json').read_text())
    require(receipt['testedSource'] == SOURCE and receipt['equivalenceCertificateSha256'] == sha((HERE/'equivalence.json').read_bytes()),
            'actual-input certificate/source pin')
    require(receipt['historicallyLoadedRenderingInputs'] == {r['path']:r['carryInSha256'] for r in report['files']} and
            receipt['canonicalCounterparts'] == {r['path']:r['canonicalSha256'] for r in report['files']}, 'actual used/canonical input inventories')
    for path, digest in receipt['historicalRuns'].items():
        require(sha((HERE.parent/path).read_bytes()) == digest, 'historical Unity/media result pin '+path)
    require(receipt['equivalentForBattleRendering'] is True and receipt['equivalenceScope'] == report['scope'] and
            all(receipt[k] is False for k in ('newUnityRun','newCompileRun','newCaptures','newDeviceRun','analysisChangedProjectFiles')),
            'explicit equivalence scope / no fabricated new Unity run')
    print('PASS rendering inputs: exact4 canonical/carry bytes; explicit12 pipeline fields +3 legacy aliases; all other bytes preserved; Lit include graph and settings pinned')


if __name__ == '__main__': main()
