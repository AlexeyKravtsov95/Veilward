"""Semantic negative controls; изменяют только строки в памяти, не Unity files."""
import importlib.util
from pathlib import Path
import sys
import unittest
from unittest.mock import patch
sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('render_input_proof', HERE/'verify_inputs.py')
v = importlib.util.module_from_spec(spec); spec.loader.exec_module(v)


class RenderingInputsTests(unittest.TestCase):
    def setUp(self):
        self.pipeline = (HERE/'canonical/Mobile_RPAsset.asset').read_text()
        self.pipeline_carry = (HERE/'carry-in/Mobile_RPAsset.asset').read_text()
        self.material = (HERE/'canonical/GrayboxSelection.mat').read_text()
        self.material_carry = (HERE/'carry-in/GrayboxSelection.mat').read_text()

    def test_exact_positive_pipeline(self):
        self.assertEqual(12, len(v.compare_pipeline(self.pipeline, self.pipeline_carry)))

    def test_exact_positive_three_materials(self):
        for path in v.MATERIALS:
            v.compare_material((HERE/'canonical'/Path(path).name).read_text(), (HERE/'carry-in'/Path(path).name).read_text())

    def test_active_pipeline_render_scale_rejected(self):
        before = '  m_RenderScale: ' + v.scalar(self.pipeline_carry, 'm_RenderScale')
        changed = self.pipeline_carry.replace(before, '  m_RenderScale: 0.5')
        self.assertNotEqual(self.pipeline_carry, changed)
        with self.assertRaises(AssertionError):
            v.compare_pipeline(self.pipeline, changed)

    def test_all_nine_explicit_defaults_rejected_if_changed(self):
        for field, value in v.DEFAULTS.items():
            with self.subTest(field=field), self.assertRaises(AssertionError):
                v.compare_pipeline(self.pipeline, self.pipeline_carry.replace('  '+field+': '+str(value), '  '+field+': '+str(1-value)))

    def test_renderer_reference_rejected(self):
        with self.assertRaises(AssertionError):
            v.compare_pipeline(self.pipeline, self.pipeline_carry.replace('65bc7dbf4170f435aa868c779acfb082', '00000000000000000000000000000001'))

    def test_null_reload_reference_rejected(self):
        with self.assertRaises(AssertionError):
            v.compare_pipeline(self.pipeline, self.pipeline_carry.replace('blueNoise64LTex: {fileID: 2800000', 'blueNoise64LTex: {fileID: 0'))

    def test_unknown_pipeline_field_rejected(self):
        with self.assertRaises(AssertionError):
            v.compare_pipeline(self.pipeline, self.pipeline_carry+'  m_UnknownRenderFeature: 1\n')

    def test_version_change_rejected(self):
        with self.assertRaises(AssertionError):
            v.compare_pipeline(self.pipeline, self.pipeline_carry.replace('k_AssetVersion: 13', 'k_AssetVersion: 14'))

    def test_active_material_color_rejected(self):
        with self.assertRaises(AssertionError):
            v.compare_material(self.material, self.material_carry.replace('_BaseColor: {r: 1', '_BaseColor: {r: 0.4'))

    def test_shader_reference_rejected(self):
        with self.assertRaises(AssertionError):
            v.compare_material(self.material, self.material_carry.replace('933532a4fcc9baf4fa0491de14d08ed7', '00000000000000000000000000000001'))

    def test_texture_reference_rejected(self):
        with self.assertRaises(AssertionError):
            v.compare_material(self.material, self.material_carry.replace('m_Texture: {fileID: 0}', 'm_Texture: {fileID: 1}', 1))

    def test_keyword_rejected(self):
        with self.assertRaises(AssertionError):
            v.compare_material(self.material, self.material_carry.replace('m_ValidKeywords: []', 'm_ValidKeywords: ["_ALPHATEST_ON"]'))

    def test_render_queue_rejected(self):
        with self.assertRaises(AssertionError):
            v.compare_material(self.material, self.material_carry.replace('m_CustomRenderQueue: -1', 'm_CustomRenderQueue: 3000'))

    def test_unknown_material_field_rejected(self):
        with self.assertRaises(AssertionError):
            v.compare_material(self.material, self.material_carry+'  m_RenderParameter: 1\n')

    def test_legacy_alias_arbitrary_color_rejected(self):
        with self.assertRaises(AssertionError):
            v.compare_material(self.material, self.material_carry.replace('_Color: {r: 1', '_Color: {r: 0.1'))

    def test_real_complete_proof_matches_record(self):
        v.main()

    def modified_read(self, target, transform):
        original = Path.read_bytes
        def read(path):
            data = original(path)
            return transform(data) if str(path).endswith(target) else data
        return patch.object(Path, 'read_bytes', read)

    def test_effective_default_code_drift_rejected(self):
        with self.modified_read('Runtime/Data/UniversalRenderPipelineAsset.cs',
                lambda b:b.replace(b'bool m_ReflectionProbeAtlas = true;', b'bool m_ReflectionProbeAtlas = false;')):
            with self.assertRaisesRegex(AssertionError, 'C# default'): v.compute()

    def test_shader_active_legacy_color_consumer_rejected(self):
        with self.modified_read('Shaders/LitInput.hlsl', lambda b:b+b'\nhalf4 active = _Color;\n'):
            with self.assertRaisesRegex(AssertionError, 'HLSL read'): v.compute()

    def test_quality_setting_drift_rejected(self):
        with self.modified_read('ProjectSettings/QualitySettings.asset', lambda b:b+b'  unknown: 1\n'):
            with self.assertRaisesRegex(AssertionError, 'unchanged settings'): v.compute()

    def test_embedded_old_material_version_rejected(self):
        post = (v.package_map()['com.unity.render-pipelines.universal']/'Editor/AssetPostProcessors/MaterialPostprocessor.cs').read_text()
        project = (v.ROOT/'ProjectSettings/URPProjectSettings.asset').read_text()
        changed = self.material.replace('  version: 10', '  version: 0')
        self.assertNotEqual(changed, self.material)
        with self.assertRaisesRegex(AssertionError, 'embedded Material AssetVersion10'):
            v.import_versions(changed, project, post)

    def test_project_version_triggering_reimport_rejected(self):
        with self.modified_read('ProjectSettings/URPProjectSettings.asset',
                lambda b:b.replace(b'm_LastMaterialVersion: 10', b'm_LastMaterialVersion: 0')):
            with self.assertRaisesRegex(AssertionError, 'project material version10'): v.compute()


if __name__ == '__main__': unittest.main(verbosity=2)
