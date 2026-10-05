#!/usr/bin/env python3
"""Check one manually exported UI frame. Not a visual/clickability or multi-state certification."""
from __future__ import annotations
import argparse
import json
import math
from pathlib import Path
import sys


def object_pairs(pairs):
    out={}
    for key,value in pairs:
        if key in out:raise ValueError('Duplicate JSON key: '+key)
        out[key]=value
    return out


def load(path):
    def reject(value):raise ValueError('Nonfinite value: '+value)
    return json.loads(path.read_text(encoding='utf-8-sig'),object_pairs_hook=object_pairs,parse_constant=reject)


def rectangle(value):
    if not isinstance(value,dict) or set(value)!={'x','y','width','height'}:raise ValueError('Incomplete rectangle')
    if any(type(v) not in (int,float) or not math.isfinite(v) for v in value.values()):raise ValueError('Invalid rectangle coordinates')
    if value['width']<=0 or value['height']<=0:raise ValueError('Empty/inverted rectangle')
    return value['x'],value['y'],value['x']+value['width'],value['y']+value['height']


def inside(a,b):return a[0]>=b[0] and a[1]>=b[1] and a[2]<=b[2] and a[3]<=b[3]


def overlaps(a,b,padding):return a[0]<b[2]+padding and a[2]+padding>b[0] and a[1]<b[3]+padding and a[3]+padding>b[1]


def check(report,kind,tech_ids=None,state=None,padding=2):
    if not isinstance(report,dict) or report.get('schemaVersion')!=3 or report.get('stage')!='manual':raise ValueError('Use a manual schema-3 runtime export')
    if type(padding) not in (int,float) or not math.isfinite(padding) or padding<0:raise ValueError('Invalid padding')
    ui=report.get('uiLayout')
    if not isinstance(ui,dict) or ui.get('schemaVersion')!=3 or ui.get('status')!='captured' or ui.get('errors')!=[]:
        raise ValueError('UI capture is absent, partial or predates effective-mask selection; recapture with UI schema 3')
    if ui.get('clipSemantics')!='native-padded-rectmask2d':raise ValueError('Missing effective padded-mask semantics')
    if ui.get('maskSelection')!='native-graphic-sorting-boundaries':raise ValueError('Missing native graphic mask-selection semantics; recapture this frame')
    if ui.get('coordinateSystem')!='screen-pixels-bottom-left':raise ValueError('Unsupported coordinate system')
    viewport=rectangle(ui['viewport'])
    records=ui.get('records')
    if not isinstance(records,list):raise ValueError('Missing UI records')
    indexed={}
    for r in records:
        if not isinstance(r,dict) or not {'key','kind','prototypeId','group','state','bounds','container','clips'}<=r.keys():raise ValueError('Incomplete UI record')
        if not isinstance(r['key'],str) or not r['key'] or r['key'] in indexed:raise ValueError('Duplicate/invalid UI key')
        if not isinstance(r['group'],str) or not r['group'] or type(r['prototypeId']) is not int:raise ValueError('Invalid UI group/identity')
        if not isinstance(r['clips'],list):raise ValueError('Missing clip inventory')
        rectangle(r['bounds']);rectangle(r['container'])
        for clip in r['clips']:rectangle(clip)
        indexed[r['key']]=r
    if kind=='technology':
        required=set(tech_ids or [1951,1952])
        if not required<={1951,1952}:raise ValueError('Only the two owned technology IDs may be targeted')
    elif kind=='lab-choice':required={48102}
    else:raise ValueError('Unknown target kind')
    targets=[r for r in records if r['kind']==kind and r['prototypeId'] in required]
    if {r['prototypeId'] for r in targets}!=required or len(targets)!=len(required):
        raise ValueError('Required controls are missing or duplicated; open/focus them and export again')
    if state and any(r['state']!=state for r in targets):raise ValueError('The requested stable focus state was not observed')
    issues=[];pairs=set()
    for r in targets:
        bounds=rectangle(r['bounds'])
        for label,clip in [('viewport',viewport),('container',rectangle(r['container'])),*((f'clip-{i}',rectangle(c)) for i,c in enumerate(r['clips']))]:
            if not inside(bounds,clip):issues.append({'control':r['key'],'kind':'clipped','boundary':label})
        for other in records:
            pair=tuple(sorted((r['key'],other['key'])))
            if other['key']==r['key'] or other['group']!=r['group'] or pair in pairs:continue
            if overlaps(bounds,rectangle(other['bounds']),padding):
                pairs.add(pair);issues.append({'control':r['key'],'kind':'overlap-or-insufficient-gap','other':other['key']})
    return {'schemaVersion':1,'status':'issues' if issues else 'observed_geometry_match','kind':kind,
            'language':ui.get('language','unknown'),'states':{r['prototypeId']:r['state'] for r in targets},
            'scope':'single observed frame only; labels/connectors/pixels/clickability and other language/focus/scale states require manual evidence',
            'issues':issues}


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('snapshot',type=Path)
    p.add_argument('--kind',choices=('technology','lab-choice'),required=True)
    p.add_argument('--tech-id',type=int,action='append');p.add_argument('--state',choices=('normal','hover','expanded','locked','unlocked'))
    p.add_argument('--padding',type=float,default=2);p.add_argument('--output',type=Path)
    a=p.parse_args();result=check(load(a.snapshot),a.kind,a.tech_id,a.state,a.padding)
    text=json.dumps(result,indent=2,ensure_ascii=False,allow_nan=False)+'\n'
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:stream.write(text)
    print(text,end='');return 0 if not result['issues'] else 1


if __name__=='__main__':
    try:raise SystemExit(main())
    except (OSError,ValueError,KeyError,TypeError) as error:
        print('UI check refused: '+str(error),file=sys.stderr);raise SystemExit(2)
