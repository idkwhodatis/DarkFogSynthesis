#!/usr/bin/env python3
"""Independent fixtures for the read-only snapshot comparator; no game data or game acceptance."""
from copy import deepcopy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).with_name('compare-runtime-snapshots.py')
spec = importlib.util.spec_from_file_location('snapshot_compare', SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def tech(id, pre=None, implicit=None):
    pre, implicit = pre or [], implicit or []
    return dict(id=id, page=0, nameKey=f'vanilla.{id}', IsHiddenTech=id in range(1901, 1905), Published=True,
                IsObsolete=False, PreItem=[5202] if id == 1901 else [], PreTechs=pre, PreTechsImplicit=implicit,
                UnlockRecipes=[], Items=[6001], ItemPoints=[10], HashNeeded=3600, position={'x': id, 'y': 0},
                preCache=pre.copy(), postCache=[], unlockCache=[], isLabTech=True)


def fixture():
    s = dict(schemaVersion=3, processId='isolated-process', sessionId=None, stage='pre-register',
             gameVersion='fixture-game', unityVersion='fixture-unity', clrVersion='fixture-clr', pluginVersion='0.1.0',
             assemblies=[{'name': 'fixture', 'version': '0', 'sha256': 'a' * 64}],
             policy=dict(peaceMode=None, extendToCombat=False, effectiveApply=None), recipes=[], items=[], technologies=[])
    ids = [1826, 1808, 1124, 1312, 1203, 1417, 1145, 1820, 1811, 1809, 1818, 1901, 1902, 1903, 1904]
    s['technologies'] = [tech(id) for id in ids]
    for id in range(5201, 5207):
        s['items'].append(dict(id=id, nameKey=f'item.{id}', UnlockKey=-2, type='Material', GridIndex=0,
                               recipes=[], handcrafts=[], maincraft=None, maincraftProductCount=0, handcraft=None, handcraftProductCount=0))
    return s


def registered(before):
    s = deepcopy(before)
    s['stage'] = 'post-bind'
    # Literal independent table rather than generating the observed side from the comparator's expectations.
    rows = [
        (48101, 'energy_shard', 5206, 2, 120, 1951, 'Smelt', [1128,1109,1110], [1,1,1]),
        (48102, 'dark_fog_matrix', 5201, 1, 240, 1952, 'Research', [1113,1404,1401,1119], [2,1,1,1]),
        (48103, 'silicon_neuron', 5202, 1, 240, 1312, 'Assemble', [1302,1107,1113], [2,2,2]),
        (48104, 'matter_recombinator', 5203, 1, 360, 1203, 'Assemble', [1304,1205,1120,1113], [1,2,2,2]),
        (48105, 'negentropy_singularity', 5204, 1, 480, 1417, 'Assemble', [1127,1126,1802,1113], [1,2,1,2]),
        (48106, 'core_element', 5205, 1, 600, 1145, 'Assemble', [1122,1125,1205,1113], [2,2,2,4]),
    ]
    for id,name,pre,implicit,items,points,cost,unlock,pos in [
        (1951,'energy_analysis',[1826],[],[6001],[10],36000,[48101],{'x':21,'y':-55}),
        (1952,'information_topology',[1808],[1124],[6001,6002,6003],[6,6,6],180000,[48102],{'x':29,'y':-43})]:
        t = tech(id, pre, implicit)
        t.update(nameKey=f'dark_fog_synthesis.tech.{name}.name', Items=items, ItemPoints=points, HashNeeded=cost,
                 UnlockRecipes=unlock, unlockCache=unlock.copy(), position=pos)
        s['technologies'].append(t)
        next(t for t in s['technologies'] if t['id']==pre[0])['postCache'].append(id)
    for index,(id,name,output,count,ticks,owner,kind,inputs,quantities) in enumerate(rows):
        s['recipes'].append(dict(id=id,nameKey=f'dark_fog_synthesis.recipe.{name}.name',type=kind,Handcraft=True,
            NonProductive=False,productive=True,TimeSpend=ticks,Items=inputs,ItemCounts=quantities,Results=[output],
            ResultCounts=[count],GridIndex=4101+index,unlockTech=owner))
        item=next(i for i in s['items'] if i['id']==output)
        item['recipes'].append(id); item['handcrafts'].append(id)
        if item['maincraft'] is None: item['maincraft'],item['maincraftProductCount']=id,count
        if item['handcraft'] is None: item['handcraft'],item['handcraftProductCount']=id,count
        t=next(t for t in s['technologies'] if t['id']==owner)
        if id not in t['UnlockRecipes']: t['UnlockRecipes'].append(id)
        if id not in t['unlockCache']: t['unlockCache'].append(id)
    return s


def mode_pair(peace=True, extend=False):
    before = registered(fixture()); before['stage']='pre-mode'; before['sessionId']='session-A'
    before['policy']=dict(peaceMode=peace,extendToCombat=extend,effectiveApply=peace or extend)
    after=deepcopy(before);after['stage']='post-mode'
    if peace or extend:
        for child,parent in ((1901,1820),(1902,1811),(1903,1809),(1904,1818)):
            next(t for t in after['technologies'] if t['id']==child)['PreTechs'].append(parent)
            next(t for t in after['technologies'] if t['id']==child)['preCache'].append(parent)
            next(t for t in after['technologies'] if t['id']==parent)['postCache'].append(child)
    return before,after


class Snapshots(unittest.TestCase):
    def test_registration(self):
        before=fixture();after=registered(before)
        self.assertEqual(module.compare(before,after,'registration')['differences'], [])

    def test_registration_research_classification_and_page(self):
        for id in (1951, 1952):
            for field, value in (('isLabTech', False), ('page', 999)):
                with self.subTest(id=id, field=field):
                    before = fixture(); after = registered(before)
                    next(t for t in after['technologies'] if t['id'] == id)[field] = value
                    result = module.compare(before, after, 'registration')
                    self.assertEqual(result['status'], 'drift')
                    self.assertIn(f'/technologies/{id}/{field}', [d['path'] for d in result['differences']])

    def test_registration_unavailable_classification_refused(self):
        for id in (1951, 1952):
            before = fixture(); after = registered(before)
            next(t for t in after['technologies'] if t['id'] == id)['isLabTech'] = None
            with self.assertRaisesRegex(ValueError, f'/technologies/{id}/isLabTech'):
                module.compare(before, after, 'registration')

    def test_registration_page_uses_native_baseline_anchors(self):
        before = fixture()
        for t in before['technologies']: t['page'] = 1
        after = registered(before)
        for t in after['technologies']: t['page'] = 1
        self.assertEqual(module.compare(before, after, 'registration')['status'], 'structural_match')
        next(t for t in before['technologies'] if t['id'] == 1808)['page'] = 2
        with self.assertRaisesRegex(ValueError, 'anchors disagree'):
            module.compare(before, after, 'registration')

    def test_registration_rejects_invalid_field_types(self):
        for field, value in (('page', True), ('page', '0'), ('page', -1), ('isLabTech', 1), ('isLabTech', 'true')):
            before = fixture(); after = registered(before)
            next(t for t in after['technologies'] if t['id'] == 1951)[field] = value
            with self.assertRaises(ValueError): module.compare(before, after, 'registration')

    def test_registration_classification_cli_exit_codes(self):
        with tempfile.TemporaryDirectory() as tmp:
            before_path = Path(tmp) / 'before.json'; after_path = Path(tmp) / 'after.json'
            before = fixture(); before_path.write_text(json.dumps(before), encoding='utf-8')
            for field, value, code in (('isLabTech', True, 0), ('isLabTech', False, 1), ('isLabTech', None, 2), ('page', 999, 1)):
                after = registered(before)
                next(t for t in after['technologies'] if t['id'] == 1951)[field] = value
                after_path.write_text(json.dumps(after), encoding='utf-8')
                result = subprocess.run([sys.executable, str(SCRIPT), str(before_path), str(after_path),
                                         '--phase', 'registration'], capture_output=True, text=True)
                self.assertEqual(result.returncode, code, result.stdout + result.stderr)
                if code == 2: self.assertNotIn('structural_match', result.stdout)

    def test_registration_cache_link(self):
        before=fixture();after=registered(before)
        next(t for t in after['technologies'] if t['id']==1312)['unlockCache']=[]
        paths=[d['path'] for d in module.compare(before,after,'registration')['differences']]
        self.assertIn('/technologies/1312/unlockCache',paths)

    def test_old_recipe_is_not_accepted(self):
        before=fixture();after=registered(before)
        after['recipes'][2]['Items'][1]=1402;after['recipes'][2]['ItemCounts'][1]=1
        self.assertEqual(module.compare(before,after,'registration')['status'],'drift')

    def test_preserve_foreign_fallback(self):
        before=fixture();before['items'][0].update(maincraft=100,maincraftProductCount=7,handcraft=101,handcraftProductCount=9)
        after=registered(before)
        self.assertEqual(module.compare(before,after,'registration')['differences'],[])
        after['items'][0]['maincraft']=48102
        self.assertEqual(module.compare(before,after,'registration')['status'],'drift')

    def test_unrelated_unlock(self):
        before=fixture();after=registered(before)
        next(t for t in after['technologies'] if t['id']==1818)['UnlockRecipes']=[999]
        self.assertEqual(module.compare(before,after,'registration')['status'],'drift')

    def test_modes_and_restoration(self):
        for peace in (False,True):
            for extend in (False,True):
                before,after=mode_pair(peace,extend)
                self.assertEqual(module.compare(before,after,'mode')['differences'],[])
                restored=deepcopy(before);restored['stage']='post-restore'
                self.assertEqual(module.compare(after,restored,'restore',before)['differences'],[])
                self.assertEqual(len(restored['recipes']),6)

    def test_restore_requires_correct_baseline(self):
        before,after=mode_pair();restored=deepcopy(before);restored['stage']='post-restore'
        with self.assertRaises(ValueError):module.compare(after,restored,'restore')
        baseline=deepcopy(before);baseline['stage']='pre-register'
        with self.assertRaises(ValueError):module.compare(after,restored,'restore',baseline)

    def test_mode_foreign_edges_and_cache_modes(self):
        before,after=mode_pair()
        child=next(t for t in before['technologies'] if t['id']==1901)
        child['PreTechs']=[1820];child['preCache']=[1820,1312];child['PreTechsImplicit']=[1312]
        next(t for t in before['technologies'] if t['id']==1820)['postCache']=[1901]
        child=next(t for t in after['technologies'] if t['id']==1901)
        child['PreTechsImplicit']=[1312];child['preCache']=[1820,1312]
        self.assertEqual(module.compare(before,after,'mode')['differences'],[])

    def test_removed_mode_edge_reported(self):
        before,after=mode_pair();next(t for t in after['technologies'] if t['id']==1901)['PreTechs']=[]
        self.assertTrue(module.compare(before,after,'mode')['differences'])

    def test_no_mutation(self):
        before,after=mode_pair();old=deepcopy((before,after));module.compare(before,after,'mode')
        self.assertEqual((before,after),old)

    def test_context_and_missing_fields(self):
        before,after=mode_pair()
        for field in ('processId','gameVersion','assemblies','sessionId'):
            bad=deepcopy(after);bad[field]='changed'
            with self.assertRaises(ValueError):module.compare(before,bad,'mode')
        bad=deepcopy(after);bad['technologies'][0].pop('preCache')
        with self.assertRaises(ValueError):module.compare(before,bad,'mode')

    def test_duplicate_record_and_null_cache(self):
        before,after=mode_pair();after['technologies'].append(after['technologies'][0])
        with self.assertRaises(ValueError):module.compare(before,after,'mode')
        before,after=mode_pair();next(t for t in before['technologies'] if t['id']==1901)['preCache']=[None]
        with self.assertRaises(ValueError):module.compare(before,after,'mode')

    def test_parser_rejects_duplicates_and_nan(self):
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'x.json'
            for raw in ('{"x":1,"x":2}','{"x":NaN}'):
                path.write_text(raw)
                with self.assertRaises(ValueError):module.load(path)

    def test_cli_reports_and_no_overwrite(self):
        with tempfile.TemporaryDirectory() as folder:
            p=Path(folder);before,after=mode_pair()
            (p/'a.json').write_text(json.dumps(before));(p/'b.json').write_text(json.dumps(after))
            args=[sys.executable,str(SCRIPT),str(p/'a.json'),str(p/'b.json'),'--phase','mode','--output',str(p/'out.json')]
            run=subprocess.run(args,capture_output=True,text=True)
            self.assertEqual(run.returncode,0,run.stderr)
            old=(p/'out.json').read_bytes();run=subprocess.run(args,capture_output=True,text=True)
            self.assertEqual(run.returncode,2);self.assertEqual((p/'out.json').read_bytes(),old)


if __name__=='__main__':unittest.main(verbosity=2)
