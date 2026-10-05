#!/usr/bin/env python3
"""Geometry and localized-lock feedback fixtures only; never labelled target-game evidence."""
from copy import deepcopy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT=Path(__file__).with_name('check-ui-layout.py')
spec=importlib.util.spec_from_file_location('ui_layout',SCRIPT);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

def rect(x,y,w=100,h=40):return dict(x=x,y=y,width=w,height=h)

def record(key,id,x,group='canvas-page-0',kind='technology',state='normal'):
    return dict(key=key,kind=kind,prototypeId=id,group=group,state=state,bounds=rect(x,20),container=rect(0,0,1000,600),clips=[])

def fixture():
    return dict(schemaVersion=3,stage='manual',uiLayout=dict(schemaVersion=2,clipSemantics='native-padded-rectmask2d',status='captured',errors=[],language='fixture',
        coordinateSystem='screen-pixels-bottom-left',viewport=rect(0,0,1000,600),
        records=[record('a',1951,20),record('b',1952,180),record('neighbor',1000,350)]))

class Layout(unittest.TestCase):
    def test_legacy_or_unqualified_mask_capture_refused(self):
        s=fixture();s['uiLayout']['schemaVersion']=1
        with self.assertRaisesRegex(ValueError,'schema 2'):m.check(s,'technology')
        s=fixture();s['uiLayout'].pop('clipSemantics')
        with self.assertRaisesRegex(ValueError,'mask semantics'):m.check(s,'technology')

    def test_padded_mask_excludes_control_inside_raw_rectangle(self):
        s=fixture();a=s['uiLayout']['records'][0];a['bounds']=rect(5,30,10,10)
        a['clips']=[rect(20,0,160,100)] # Native [0,200] mask, left/right padding 20.
        result=m.check(s,'technology',[1951])
        self.assertIn({'control':'a','kind':'clipped','boundary':'clip-0'},result['issues'])

    def test_nested_effective_masks(self):
        s=fixture();a=s['uiLayout']['records'][0];a['bounds']=rect(30,30,10,10)
        a['clips']=[rect(20,0,160,100),rect(35,0,120,100)]
        result=m.check(s,'technology',[1951])
        self.assertIn({'control':'a','kind':'clipped','boundary':'clip-1'},result['issues'])
        self.assertNotIn({'control':'a','kind':'clipped','boundary':'clip-0'},result['issues'])

    def test_effective_padding_at_nonunit_screen_scale(self):
        # These are projected fixture rectangles, not a claim to execute Unity transforms.
        # The companion EditMode tests exercise the actual production projection helper.
        for scale in (1,1.5,2):
            s=fixture();a=s['uiLayout']['records'][0];a['bounds']=rect(5*scale,30*scale,10*scale,10*scale)
            a['clips']=[rect(20*scale,0,160*scale,100*scale)]
            self.assertTrue(m.check(s,'technology',[1951])['issues'])

    def test_valid_frame(self):self.assertEqual(m.check(fixture(),'technology')['issues'],[])
    def test_overlap(self):
        s=fixture();s['uiLayout']['records'][2]['bounds']=rect(50,20)
        self.assertEqual(m.check(s,'technology')['status'],'issues')
    def test_other_page_is_not_a_collision(self):
        s=fixture();s['uiLayout']['records'][2].update(bounds=rect(20,20),group='canvas-page-1')
        self.assertEqual(m.check(s,'technology')['issues'],[])
    def test_clip_viewport_and_panel(self):
        for target in ('container','clip','viewport'):
            s=fixture();a=s['uiLayout']['records'][0]
            if target=='clip':a['clips']=[rect(0,0,30,30)]
            elif target=='viewport':s['uiLayout']['viewport']=rect(0,0,30,30)
            else:a['container']=rect(0,0,30,30)
            self.assertTrue(m.check(s,'technology')['issues'])
    def test_missing_and_duplicate_controls_refused(self):
        s=fixture();s['uiLayout']['records'].pop(0)
        with self.assertRaises(ValueError):m.check(s,'technology')
        s=fixture();s['uiLayout']['records'].append(deepcopy(s['uiLayout']['records'][0]))
        with self.assertRaises(ValueError):m.check(s,'technology')
    def test_partial_and_absent_capture_refused(self):
        for status in ('partial',None):
            s=fixture();s['uiLayout']['status']=status
            with self.assertRaises(ValueError):m.check(s,'technology')
    def test_single_technology_focus(self):
        s=fixture();s['uiLayout']['records'][0]['state']='hover'
        self.assertEqual(m.check(s,'technology',[1951],'hover')['issues'],[])
        with self.assertRaises(ValueError):m.check(s,'technology',[1951],'expanded')
    def test_lab_locked_and_unlocked(self):
        for state in ('locked','unlocked'):
            s=fixture();s['uiLayout']['records']=[record('custom',48102,20,'lab-1','lab-choice',state),record('native',0,180,'lab-1','lab-native')]
            self.assertEqual(m.check(s,'lab-choice',state=state)['issues'],[])
    def test_bad_geometry_and_padding(self):
        for value in (float('nan'),float('inf'),-1,'100',True):
            s=fixture();s['uiLayout']['records'][0]['bounds']['width']=value
            with self.assertRaises(ValueError):m.check(s,'technology')
        with self.assertRaises(ValueError):m.check(fixture(),'technology',padding=float('nan'))
    def test_unchanged_inputs(self):
        s=fixture();before=deepcopy(s);m.check(s,'technology');self.assertEqual(s,before)
    def test_localized_requirement_template(self):
        root=Path(__file__).resolve().parents[1]
        for lang in ('en-US','zh-CN'):
            data=json.loads((root/f'src/DarkFogSynthesis/Localization/Strings.{lang}.json').read_text(encoding='utf-8'))
            template=data['dark_fog_synthesis.lab.requires_technology']
            self.assertEqual(template.count('{0}'),1)
            self.assertIn('Test Technology',template.format('Test Technology'))
    def test_silicon_neuron_text_matches_approved_revision(self):
        root=Path(__file__).resolve().parents[1]
        for lang,expected,obsolete in [('en-US','2 titanium alloy','particle broadband'),('zh-CN','钛合金 ×2','粒子宽带')]:
            data=json.loads((root/f'src/DarkFogSynthesis/Localization/Strings.{lang}.json').read_text(encoding='utf-8'))
            text=data['dark_fog_synthesis.recipe.silicon_neuron.description']
            self.assertIn(expected,text);self.assertNotIn(obsolete,text)
    def test_cli_create_only(self):
        with tempfile.TemporaryDirectory() as folder:
            p=Path(folder);(p/'in.json').write_text(json.dumps(fixture()))
            args=[sys.executable,str(SCRIPT),str(p/'in.json'),'--kind','technology','--output',str(p/'out.json')]
            result=subprocess.run(args,capture_output=True,text=True);self.assertEqual(result.returncode,0,result.stderr)
            before=(p/'out.json').read_bytes();result=subprocess.run(args,capture_output=True,text=True)
            self.assertEqual(result.returncode,2);self.assertEqual((p/'out.json').read_bytes(),before)

if __name__=='__main__':unittest.main(verbosity=2)
