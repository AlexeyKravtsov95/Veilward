#!/usr/bin/env python3
"""Извлечь только числовые Activity Monitor samples одного VEILWARD PID.

Input: private xctrace export of activity-monitor-process-live; stdout: public CSV.
Остальные процессы и локальные identifiers в output не попадают.
"""
import csv
import re
import sys
import xml.etree.ElementTree as ET


def extract(path, executable='VEILWARDSpiritDefense'):
    root = ET.parse(path).getroot()
    refs = {node.get('id'): node for node in root.iter() if node.get('id')}
    def resolve(node):
        return refs[node.get('ref')] if node.get('ref') else node
    columns = ['start', 'memory-physical-footprint', 'memory-real',
               'memory-real-private', 'cpu-percent', 'thread-count']
    rows, pids = [], set()
    for table in root.findall('.//node'):
        schema = table.find('schema')
        if schema is None or schema.get('name') != 'activity-monitor-process-live':
            continue
        names = [c.findtext('mnemonic') for c in schema.findall('col')]
        for row in table.findall('row'):
            if len(row) != len(names):
                raise ValueError('row/schema mismatch')
            cells = dict(zip(names, [resolve(c) for c in row]))
            process = cells['process']
            match = re.fullmatch(re.escape(executable) + r' \((\d+)\)', process.get('fmt', ''))
            if not match:
                continue
            pid = resolve(process.find('pid')).text
            if pid != match.group(1) or resolve(cells['pid']).text != pid:
                raise ValueError('process/PID mismatch')
            pids.add(pid)
            values = []
            for name in columns:
                cell = cells[name]
                value = '' if cell.tag == 'sentinel' else cell.text
                if value and not re.fullmatch(r'\d+(?:\.\d+)?', value):
                    raise ValueError('non-numeric field')
                if name != 'cpu-percent' and not value:
                    raise ValueError('missing memory/time/thread sample')
                values.append(value or '')
            rows.append(values)
    if len(pids) != 1 or len(rows) < 2:
        raise ValueError('expected samples from exactly one game PID')
    times = [int(row[0]) for row in rows]
    if any(b <= a for a, b in zip(times, times[1:])):
        raise ValueError('unordered sample time')
    return columns, rows


if __name__ == '__main__':
    try:
        columns, rows = extract(sys.argv[1])
        writer = csv.writer(sys.stdout, lineterminator="\n")
        writer.writerow(columns)
        writer.writerows(rows)
    except (ValueError, KeyError, IndexError, OSError) as error:
        raise SystemExit('FAIL: ' + str(error))
