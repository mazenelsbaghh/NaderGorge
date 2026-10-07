#!/usr/bin/env python3
"""Read the chosen local SQLite database without modifying it; export fresh-install defaults."""
import argparse
import hashlib
import json
import os
import sqlite3
import tempfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
HISTORY_COLLECTIONS = ('sessions', 'packages', 'attendances', 'payments', 'academics',
                       'academicActivities', 'audit', 'staff', 'reviews', 'closings',
                       'paymentChecks', 'corrections', 'refunds', 'cardPayments', 'debtSettlements', 'cardReceipts')


def write_json(path, document):
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile('w', encoding='utf-8', dir=path.parent, delete=False) as output:
        temporary = Path(output.name)
        json.dump(document, output, ensure_ascii=False, indent=2)
        output.write('\n')
        output.flush()
        os.fsync(output.fileno())
    try:
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--database', type=Path, required=True)
    options = parser.parse_args()
    source = options.database.resolve(strict=True)
    connection = sqlite3.connect(source.as_uri() + '?mode=ro', uri=True)
    try:
        connection.execute('PRAGMA query_only = ON')
        connection.execute('BEGIN')
        row = connection.execute('SELECT payload FROM state WHERE id=1').fetchone()
        if row is None:
            raise RuntimeError('The selected database has no application state.')
        state = json.loads(row[0])
    finally:
        connection.close()
    if state.get('schemaVersion') not in (1, 2, 3, 4, 5, 6, 7, 8):
        raise RuntimeError('Unsupported application state.')
    catalog_ids = {entry['id'] for entry in state['catalogs']}
    group_ids = {group['id'] for group in state['groups']}
    for group in state['groups']:
        if any(group[key] not in catalog_ids for key in ('subjectId', 'centerId', 'gradeId')):
            raise RuntimeError('A group refers to a missing catalog entry.')
    for student in state['students']:
        if any(group not in group_ids for group in student['groupIds']):
            raise RuntimeError('A student refers to a missing group.')
    kept = {'catalogs', 'groups', 'students'}
    removed_counts = {key: len(value) for key, value in state.items()
                      if isinstance(value, list) and key not in kept}
    defaults = {key: value if key in kept else [] for key, value in state.items()
                if isinstance(value, list)}
    defaults.update({key: [] for key in HISTORY_COLLECTIONS})
    membership_keys = {student['id'] + ':' + group for student in state['students']
                       for group in student['groupIds']}
    default_schema = 5 if any(group.get('monthPlans') for group in defaults['groups']) else (4 if any(student.get('twinStudentId') for student in defaults['students']) else 2)
    defaults.update(schemaVersion=default_schema, credentials={},
                    cardSettings=state.get('cardSettings', {}),
                    enrollments={key: value for key, value in state['enrollments'].items()
                                 if key in membership_keys})
    digest = hashlib.sha256(json.dumps(defaults, ensure_ascii=False, sort_keys=True).encode()).hexdigest()
    defaults['installationSeedId'] = 'device-defaults-' + digest[:20]
    target = ROOT / 'assets/installation_seed.json'
    stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    if target.exists():
        preservation = ROOT / 'private-import' / ('seed-before-device-defaults-' + stamp + '.json')
        preservation.parent.mkdir(parents=True, exist_ok=True)
        preservation.write_bytes(target.read_bytes())
    write_json(target, {'version': 1, 'state': defaults})
    summary = {'sourceDatabase': str(source), 'exportedAt': datetime.now(timezone.utc).isoformat(),
               'installationSeedId': defaults['installationSeedId'],
               'students': len(defaults['students']), 'groups': len(defaults['groups']),
               'catalogs': len(defaults['catalogs']), 'clearedInDefaultsOnly': removed_counts,
               'studentProfilesUnchanged': defaults['students'] == state['students'],
               'groupProfilesUnchanged': defaults['groups'] == state['groups'],
               'sourceOpenedReadOnly': True}
    write_json(ROOT / 'private-import/device-defaults-summary.json', summary)
    print(json.dumps({key: value for key, value in summary.items() if key != 'sourceDatabase'}, ensure_ascii=False))


if __name__ == '__main__':
    main()
