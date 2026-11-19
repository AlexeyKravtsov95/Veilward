import importlib.util
import json
import pathlib
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('art2b', pathlib.Path(__file__).parents[1] / 'analyze-art2b.py')
art = importlib.util.module_from_spec(spec)
spec.loader.exec_module(art)


class ArtMeasurementsTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = pathlib.Path(self.tmp.name)
        (self.root / 'run.json').write_text(json.dumps({'scenario': 'dense', 'sourceReceipt': json.dumps({'sourceSHA': 'a' * 40, 'profile': 'art-reference', 'buildNumber': '43'})}))
        (self.root / 'complete.json').write_text('{"complete":true,"repeats":3}')
        self.window = dict(scenario='dense', samples=300, warmupSeconds=5, durationSeconds=30,
                           observedAttacks=0, advancedTicks=0, intervalsMs=[100] * 300,
                           cpuFrameMs=[0] * 300, gpuFrameMs=[0] * 300,
                           unityAllocatedBaseline=100, unityAllocatedPeak=110,
                           unityAllocatedAfter=101, unityReservedPeak=200)
        for i in range(1, 4):
            (self.root / f'window-{i}.json').write_text(json.dumps(dict(self.window, repeat=i)))

    def test_recomputes_positive_totals_and_unavailable_gpu(self):
        report = art.analyze(self.root)
        self.assertEqual(900, report['intervalMs']['samples'])
        self.assertEqual(100, report['intervalMs']['p95'])
        self.assertIsNone(report['gpuFrameMs']['median'])
        self.assertEqual(0, report['gpuFrameMs']['samples'])

    def test_normal_window_requires_activity_after_warmup(self):
        settings = json.loads((self.root / 'run.json').read_text())
        settings['scenario'] = 'normal'
        (self.root / 'run.json').write_text(json.dumps(settings))
        for i in range(1, 4):
            (self.root / f'window-{i}.json').write_text(json.dumps(dict(self.window, repeat=i, scenario='normal')))
        with self.assertRaisesRegex(ValueError, 'frozen normal combat'): art.analyze(self.root)

    def test_missing_completion_rejects_interrupted_run(self):
        (self.root / 'complete.json').unlink()
        with self.assertRaises(ValueError): art.analyze(self.root)

    def test_bad_samples_short_windows_and_frozen_normal_are_rejected(self):
        for change in ({'samples': 0}, {'durationSeconds': 1}, {'intervalsMs': [float('nan')] * 300},
                       {'repeat': 2}, {'gpuFrameMs': [-1] * 300}, {'scenario': 'normal'}):
            with self.subTest(change=change):
                (self.root / 'window-1.json').write_text(json.dumps({**self.window, "repeat": 1, **change}))
                with self.assertRaises(ValueError): art.analyze(self.root)


if __name__ == '__main__': unittest.main()
