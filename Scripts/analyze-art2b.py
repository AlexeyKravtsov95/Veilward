#!/usr/bin/env python3
"""Пересчёт только полных device runs. Unity frame intervals != display/CPU execution time."""
import argparse
import json
import math
from pathlib import Path
import re
import statistics


def require(condition, message):
    if not condition:
        raise ValueError(message)


def stats(values):
    ordered = sorted(values)
    return {'samples': len(ordered), 'median': statistics.median(ordered) if ordered else None,
            'p95': ordered[math.ceil(.95 * len(ordered)) - 1] if ordered else None,
            'max': max(ordered) if ordered else None}


def analyze(root):
    root = Path(root)
    require((root / 'complete.json').is_file(), 'incomplete run')
    complete = json.loads((root / 'complete.json').read_text())
    require(complete == {'complete': True, 'repeats': 3}, 'invalid completion')
    settings = json.loads((root / 'run.json').read_text())
    receipt = json.loads(settings['sourceReceipt'])
    require(re.fullmatch('[0-9a-f]{40}', receipt.get('sourceSHA', '')) is not None and
            receipt.get('profile') == 'art-reference', 'not an exact-source art device receipt')
    intervals, cpu, gpu, memory = [], [], [], []
    require(len(list(root.glob('window-*.json'))) == 3, 'expected exactly three windows')
    for i in range(1, 4):
        window = json.loads((root / f'window-{i}.json').read_text())
        require(window['scenario'] == settings['scenario'] and window['repeat'] == i, 'mixed scenario/repeat')
        n = window['samples']
        require(type(n) is int and n > 0, 'empty window')
        require(window['durationSeconds'] >= 30 and window['warmupSeconds'] >= 5, 'short window/warmup')
        for name in ('intervalsMs', 'cpuFrameMs', 'gpuFrameMs'):
            values = window[name]
            require(len(values) == n and all(type(v) in (int, float) and math.isfinite(v) and
                    (v > 0 if name == 'intervalsMs' else v >= 0) for v in values), 'invalid raw samples')
        require(abs(sum(window['intervalsMs']) / 1000 - window['durationSeconds']) < .001, 'duration mismatch')
        if settings['scenario'] == 'normal':
            require(window['advancedTicks'] > 0 and window['observedAttacks'] > 0, 'frozen normal combat')
        row = {k: window[k] for k in ('unityAllocatedBaseline', 'unityAllocatedPeak', 'unityAllocatedAfter', 'unityReservedPeak')}
        require(all(type(v) is int and v >= 0 for v in row.values()), 'invalid memory')
        require(row['unityAllocatedPeak'] >= max(row['unityAllocatedBaseline'], row['unityAllocatedAfter']), 'invalid memory peak')
        memory.append(row)
        intervals.extend(window['intervalsMs'])
        cpu.extend(v for v in window['cpuFrameMs'] if v > 0)
        gpu.extend(v for v in window['gpuFrameMs'] if v > 0)
    return {'scenario': settings['scenario'], 'sourceReceipt': receipt, 'intervalMs': stats(intervals),
            'cpuFrameMs': stats(cpu), 'gpuFrameMs': stats(gpu), 'memory': memory,
            'limitations': 'CPU frame includes pacing; zeros are unavailable. Unity allocated/reserved is not native resident footprint. No display-present or broad device guarantee.'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run', type=Path)
    args = parser.parse_args()
    try:
        print(json.dumps(analyze(args.run), indent=2, allow_nan=False))
    except (ValueError, KeyError, OSError, TypeError) as error:
        raise SystemExit('FAIL: ' + str(error))


if __name__ == '__main__': main()
