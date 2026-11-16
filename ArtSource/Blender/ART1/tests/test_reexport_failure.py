"""Настоящий Blender exception не должен превращаться в PASS на старых FBX."""
import contextlib
import hashlib
import io
from pathlib import Path
import runpy
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[4]

class ReexportFailureTests(unittest.TestCase):
    def test_python_exception_aborts_before_comparison_or_positive_evidence(self):
        models = sorted((ROOT / 'Assets/_Game/Art/Models/ART1').glob('*.fbx'))
        before = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in models}
        real_run = subprocess.run
        unity_calls = []
        blender_results = []
        output = io.StringIO()
        with tempfile.TemporaryDirectory(prefix='art1-export-negative-') as temp:
            failure = Path(temp) / 'fail_export.py'
            failure.write_text('raise RuntimeError("ART1 intentional export failure")\n')
            def run(args, **kwargs):
                if args[0] == 'unity':
                    # Только Unity snapshot фиксирован: моделируем неизменные старые FBX.
                    unity_calls.append(args)
                    return subprocess.CompletedProcess(args, 0, '{"success":true,"result":{"unchanged":true}}', '')
                args = list(args)
                args[args.index('--python') + 1] = str(failure)
                # Blender/exception/exit code настоящие; не заменяем результат subprocess.
                check = kwargs.pop('check', False)
                result = real_run(args, **kwargs)
                blender_results.append(result.returncode)
                if check: result.check_returncode()
                return result
            # Не позволяем broken verifier перезаписать реальные golden reports при RED.
            with patch('subprocess.run', side_effect=run), patch.object(Path, 'write_text') as writes:
                with contextlib.redirect_stdout(output):
                    with self.assertRaises(subprocess.CalledProcessError) as error:
                        runpy.run_path(str(ROOT / 'ArtSource/Blender/ART1/verify_reexport.py'), run_name='__main__')
                self.assertEqual(error.exception.returncode, 1)
                self.assertEqual(blender_results, [1])
                self.assertEqual(len(unity_calls), 1, 'Не сравнивать старый FBX после failed export')
                self.assertNotIn('PASS:', output.getvalue())
                writes.assert_not_called()
        self.assertEqual(before, {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in models})

if __name__ == '__main__':
    unittest.main()
