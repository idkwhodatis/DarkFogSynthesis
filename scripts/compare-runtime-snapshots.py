#!/usr/bin/env python3
"""Strict, read-only comparison of DFS schema-3 prototype observations.

Exit 0: observed structural changes match this bounded policy, not game acceptance.
Exit 1: structural drift. Exit 2: invalid/unsupported inputs. No save is read/written.
"""
from __future__ import annotations
import argparse
from copy import deepcopy
import json
from pathlib import Path
import sys

TABLES = ('recipes', 'items', 'technologies')
STAGES = {'pre-register', 'post-bind', 'pre-mode', 'post-mode', 'execution-cache-ready', 'session-ready', 'post-restore', 'manual'}
# Independent literal oracle, intentionally not populated from a submitted snapshot.
RECIPES = {
    48101: ('energy_shard', 5206, 2, 120, 1951, 'Smelt', [1128, 1109, 1110], [1, 1, 1]),
    48102: ('dark_fog_matrix', 5201, 1, 240, 1952, 'Research', [1113, 1404, 1401, 1119], [2, 1, 1, 1]),
    48103: ('silicon_neuron', 5202, 1, 240, 1312, 'Assemble', [1302, 1107, 1113], [2, 2, 2]),
    48104: ('matter_recombinator', 5203, 1, 360, 1203, 'Assemble', [1304, 1205, 1120, 1113], [1, 2, 2, 2]),
    48105: ('negentropy_singularity', 5204, 1, 480, 1417, 'Assemble', [1127, 1126, 1802, 1113], [1, 2, 1, 2]),
    48106: ('core_element', 5205, 1, 600, 1145, 'Assemble', [1122, 1125, 1205, 1113], [2, 2, 2, 4]),
}
TECHS = {
    1951: ('energy_analysis', [1826], [], [6001], [10], 36000, [48101], {'x': 21, 'y': -55}),
    1952: ('information_topology', [1808], [1124], [6001, 6002, 6003], [6, 6, 6], 180000, [48102], {'x': 29, 'y': -43}),
}
EDGES = {1901: 1820, 1902: 1811, 1903: 1809, 1904: 1818}
REQUIRED = {
    'recipes': {'id', 'nameKey', 'type', 'Handcraft', 'NonProductive', 'productive', 'TimeSpend', 'Items', 'ItemCounts', 'Results', 'ResultCounts', 'GridIndex', 'unlockTech'},
    'items': {'id', 'nameKey', 'UnlockKey', 'type', 'GridIndex', 'recipes', 'handcrafts', 'maincraft', 'maincraftProductCount', 'handcraft', 'handcraftProductCount'},
    'technologies': {'id', 'page', 'nameKey', 'IsHiddenTech', 'Published', 'IsObsolete', 'PreItem', 'PreTechs', 'PreTechsImplicit', 'UnlockRecipes', 'Items', 'ItemPoints', 'HashNeeded', 'position', 'preCache', 'postCache', 'unlockCache', 'isLabTech'},
}


def unique_object(pairs):
    out = {}
    for key, value in pairs:
        if key in out:
            raise ValueError(f'Duplicate JSON field: {key}')
        out[key] = value
    return out


def load(path: Path) -> dict:
    def reject(value):
        raise ValueError(f'Nonfinite JSON number: {value}')
    with path.open(encoding='utf-8-sig') as stream:
        return json.load(stream, object_pairs_hook=unique_object, parse_constant=reject)


def tables(snapshot: dict) -> dict:
    if not isinstance(snapshot, dict) or snapshot.get('schemaVersion') != 3 or snapshot.get('stage') not in STAGES:
        raise ValueError('Expected a phase-labelled schema-3 snapshot; older exports are not silently upgraded.')
    for name in ('processId', 'gameVersion', 'unityVersion', 'clrVersion', 'pluginVersion'):
        if not isinstance(snapshot.get(name), str) or not snapshot[name]:
            raise ValueError(f'Missing snapshot identity field: {name}')
    if not isinstance(snapshot.get('assemblies'), list) or not snapshot['assemblies']:
        raise ValueError('Missing assembly identities')
    policy = snapshot.get('policy')
    if not isinstance(policy, dict) or type(policy.get('extendToCombat')) is not bool:
        raise ValueError('Missing configuration policy')
    for key in ('peaceMode', 'effectiveApply'):
        if policy.get(key) is not None and type(policy[key]) is not bool:
            raise ValueError(f'Invalid policy.{key}')
    expected_apply = None if policy.get('peaceMode') is None else policy['peaceMode'] or policy['extendToCombat']
    if policy.get('effectiveApply') is not expected_apply:
        raise ValueError('Inconsistent effective mode policy')
    out = {}
    for table in TABLES:
        records = snapshot.get(table)
        if not isinstance(records, list):
            raise ValueError(f'Missing table: {table}')
        out[table] = {}
        for record in records:
            if not isinstance(record, dict) or not REQUIRED[table] <= record.keys():
                raise ValueError(f'Incomplete {table} record')
            key = record['id']
            if type(key) is not int or key <= 0 or key in out[table]:
                raise ValueError(f'Invalid or duplicate {table} id: {key}')
            out[table][key] = deepcopy(record)
    return out


def context(left, right, *, session=False):
    for key in ('processId', 'gameVersion', 'unityVersion', 'clrVersion', 'pluginVersion', 'assemblies'):
        if left[key] != right[key]:
            raise ValueError(f'Cannot compare different runtime environments: {key}')
    if left['policy']['extendToCombat'] != right['policy']['extendToCombat']:
        raise ValueError('The startup configuration differs')
    if session and (not left.get('sessionId') or left.get('sessionId') != right.get('sessionId')):
        raise ValueError('Mode/restore comparisons require the same process-local session identity')


def array(record, key):
    value = record[key]
    if value is None:
        return []
    if not isinstance(value, list) or any(type(v) is not int for v in value):
        raise ValueError(f'Invalid/null-element native cache: {record["id"]}.{key}')
    return value.copy()


def append(record, key, value):
    values = array(record, key)
    if value not in values:
        values.append(value)
    record[key] = values


def diff(expected, actual, path=''):
    if isinstance(expected, dict) and isinstance(actual, dict):
        for key in sorted(expected.keys() | actual.keys(), key=str):
            child = f'{path}/{key}'
            if key not in expected:
                yield {'path': child, 'kind': 'unexpected', 'actual': actual[key]}
            elif key not in actual:
                yield {'path': child, 'kind': 'missing', 'expected': expected[key]}
            else:
                yield from diff(expected[key], actual[key], child)
    elif expected != actual or (isinstance(expected, bool) != isinstance(actual, bool)):
        yield {'path': path, 'kind': 'changed', 'expected': expected, 'actual': actual}


def registration(before, after):
    expected = deepcopy(before)
    if set(after['recipes']) - set(before['recipes']) != RECIPES.keys() or set(after['technologies']) - set(before['technologies']) != TECHS.keys():
        raise ValueError('Registration must add exactly recipe IDs 48101..48106 and technology IDs 1951/1952')
    checks = []
    grids = []
    for key, (name, output, count, ticks, owner, kind, inputs, quantities) in RECIPES.items():
        actual = after['recipes'][key]
        frozen = dict(id=key, nameKey=f'dark_fog_synthesis.recipe.{name}.name', type=kind, Handcraft=True,
                      NonProductive=False, productive=True, TimeSpend=ticks, Items=inputs, ItemCounts=quantities,
                      Results=[output], ResultCounts=[count], unlockTech=owner)
        checks.extend(diff(frozen, {k: actual[k] for k in frozen}, f'/recipes/{key}'))
        grids.append(actual['GridIndex'])
        expected['recipes'][key] = deepcopy(actual)
    if any(type(v) is not int for v in grids) or grids[0] < 1101 or grids[0] % 1000 != 101 or grids != list(range(grids[0], grids[0] + 6)):
        raise ValueError('Unexpected assigned recipe-grid batch')
    if any(r['GridIndex'] in grids for r in before['recipes'].values()):
        raise ValueError('Assigned recipe-grid collision')
    for key, (name, pre, implicit, items, points, cost, unlocks, position) in TECHS.items():
        actual = after['technologies'][key]
        frozen = dict(id=key, nameKey=f'dark_fog_synthesis.tech.{name}.name', IsHiddenTech=False, Published=True,
                      IsObsolete=False, PreItem=[], PreTechs=pre, PreTechsImplicit=implicit, Items=items,
                      ItemPoints=points, HashNeeded=cost, UnlockRecipes=unlocks, unlockCache=unlocks, position=position)
        checks.extend(diff(frozen, {k: actual[k] for k in frozen}, f'/technologies/{key}'))
        if actual['preCache'] not in (pre, list(dict.fromkeys(pre + implicit))):
            checks.append({'path': f'/technologies/{key}/preCache', 'kind': 'invalid-prerequisite-cache', 'actual': actual['preCache']})
        if actual['postCache'] not in (None, []):
            checks.append({'path': f'/technologies/{key}/postCache', 'kind': 'unexpected-child', 'actual': actual['postCache']})
        expected['technologies'][key] = deepcopy(actual)
        for parent in pre:
            append(expected['technologies'][parent], 'postCache', key)
    for key, (_, output, count, _, owner, *_rest) in RECIPES.items():
        tech = expected['technologies'][owner]
        append(tech, 'UnlockRecipes', key)
        tech['unlockCache'] = array(tech, 'UnlockRecipes')
        item = expected['items'][output]
        append(item, 'recipes', key)
        append(item, 'handcrafts', key)
        for fallback, quantity in (('maincraft', 'maincraftProductCount'), ('handcraft', 'handcraftProductCount')):
            if item[fallback] is None:
                item[fallback], item[quantity] = key, count
    return expected, checks


def apply_mode(before, enabled):
    expected = deepcopy(before)
    if not enabled:
        return expected
    for child, parent in EDGES.items():
        tech = expected['technologies'][child]
        pre, implicit, cache = array(tech, 'PreTechs'), array(tech, 'PreTechsImplicit'), array(tech, 'preCache')
        if cache not in (pre, list(dict.fromkeys(pre + implicit))):
            raise ValueError(f'Unknown prerequisite cache semantics on {child}')
        combined = cache != pre
        append(tech, 'PreTechs', parent)
        tech['preCache'] = list(dict.fromkeys(tech['PreTechs'] + implicit)) if combined else tech['PreTechs'].copy()
        append(expected['technologies'][parent], 'postCache', child)
    return expected


def compare(left, right, phase, baseline=None):
    before, after = tables(left), tables(right)
    context(left, right, session=phase in ('mode', 'restore'))
    checks = []
    if phase == 'registration':
        if (left['stage'], right['stage']) != ('pre-register', 'post-bind'):
            raise ValueError('Registration requires pre-register -> post-bind snapshots')
        expected, checks = registration(before, after)
    elif phase == 'mode':
        if (left['stage'], right['stage']) != ('pre-mode', 'post-mode') or left['policy'] != right['policy'] or right['policy']['effectiveApply'] is None:
            raise ValueError('Mode requires matching pre-mode -> post-mode policy snapshots')
        expected = apply_mode(before, right['policy']['effectiveApply'])
    elif phase == 'restore':
        if baseline is None:
            raise ValueError('Restore requires --baseline from this session before mode application')
        base = tables(baseline)
        context(baseline, left, session=True)
        if baseline['stage'] != 'pre-mode' or left['stage'] != 'post-mode' or right['stage'] != 'post-restore':
            raise ValueError('Restore requires pre-mode baseline, post-mode before, and post-restore after')
        if baseline['policy'] != left['policy'] or baseline['policy']['effectiveApply'] is None:
            raise ValueError('Baseline mode policy differs')
        checks.extend(diff(apply_mode(base, baseline['policy']['effectiveApply']), before, '/before-restore'))
        expected = base
    elif phase == 'equal':
        expected = before
    else:
        raise ValueError('Unknown comparison phase')
    checks.extend(diff(expected, after))
    return {'schemaVersion': 1, 'phase': phase, 'status': 'structural_match' if not checks else 'drift',
            'scope': 'exported prototype fields only; no game acceptance, save safety or fault attribution',
            'differences': checks}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('before', type=Path)
    parser.add_argument('after', type=Path)
    parser.add_argument('--phase', choices=('registration', 'mode', 'restore', 'equal'), required=True)
    parser.add_argument('--baseline', type=Path)
    parser.add_argument('--output', type=Path, help='Create a new report; existing files are never overwritten')
    args = parser.parse_args()
    result = compare(load(args.before), load(args.after), args.phase, load(args.baseline) if args.baseline else None)
    text = json.dumps(result, indent=2, ensure_ascii=False, allow_nan=False) + '\n'
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            stream.write(text)
    print(text, end='')
    return 0 if result['status'] == 'structural_match' else 1


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (OSError, ValueError, TypeError, KeyError) as error:
        print(f'Comparison refused: {error}', file=sys.stderr)
        raise SystemExit(2)
