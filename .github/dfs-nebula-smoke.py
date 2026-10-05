#!/usr/bin/env python3
"""Bounded reference-only build/audit for the three approved fixes; removed by this workflow.
No DSP or Unity engine is executed. Downloads remain outside the checkout.
"""
from pathlib import Path
import hashlib
from io import BytesIO
import json
import shutil
import subprocess
import tempfile
import urllib.request
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]

def run(args):
    subprocess.run(args, cwd=ROOT, check=True, timeout=600)

def digest(data):
    return hashlib.sha256(data).hexdigest()

with tempfile.TemporaryDirectory(prefix='dfs-review-reference-') as tmp:
    temp = Path(tmp)
    project = temp / 'References.csproj'
    project.write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net472</TargetFramework></PropertyGroup><ItemGroup>
<PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" />
<PackageReference Include="DysonSphereProgram.GameLibs" Version="0.10.35.29088-r.0" />
<PackageReference Include="UnityEngine.Modules" Version="2022.3.53" />
<PackageReference Include="BepInEx.BaseLib" Version="5.4.20" />
<PackageReference Include="HarmonyX" Version="2.7.0" />
</ItemGroup></Project>''')
    packages = temp / 'packages'
    run(['dotnet','restore',str(project),'--packages',str(packages),
         '--source','https://nuget.bepinex.dev/v3/index.json','--source','https://api.nuget.org/v3/index.json'])
    baseline = json.loads((ROOT/'docs/compatibility/compile-baseline.json').read_text())
    pins = {r['name']: r['sha256'] for r in baseline['references']}
    refs = temp/'refs'; game=refs/'game'; bep=refs/'bep'; common=refs/'common'; ldb=refs/'ldb'
    for p in (game,bep,common,ldb):p.mkdir(parents=True)
    def select(name, directory, parent=None):
        matches = sorted((parent or packages).rglob(name))
        if name in pins:
            matches=[p for p in matches if digest(p.read_bytes())==pins[name]]
        if not matches: raise ValueError('Missing exact pinned reference: '+name)
        source=matches[0];shutil.copyfile(source,directory/name);return source
    select('Assembly-CSharp.dll',game)
    unity=select('UnityEngine.CoreModule.dll',game).parent
    for name in ('UnityEngine.dll','UnityEngine.IMGUIModule.dll','UnityEngine.ImageConversionModule.dll','UnityEngine.UIModule.dll','UnityEngine.UI.dll'):
        select(name,game,unity if (unity/name).exists() else packages)
    select('BepInEx.dll',bep); select('0Harmony.dll',bep)
    for name,url,folder in (
        ('CommonAPI.dll','https://thunderstore.io/package/download/CommonAPI/CommonAPI/1.6.7/',common),
        ('LDBTool.dll','https://thunderstore.io/package/download/xiaoye97/LDBTool/3.0.3/',ldb)):
        request=urllib.request.Request(url,headers={'User-Agent':'DarkFogSynthesis-reference-verification'})
        with urllib.request.urlopen(request,timeout=90) as response:content=response.read(64*1024*1024)
        with ZipFile(BytesIO(content)) as archive:
            choices=[archive.read(n) for n in archive.namelist() if Path(n).name==name]
        choices=[data for data in choices if digest(data)==pins[name]]
        if len(choices)!=1:raise ValueError('Dependency checksum mismatch: '+name)
        (folder/name).write_bytes(choices[0])
    run(['dotnet','build','src/DarkFogSynthesis/DarkFogSynthesis.csproj','-c','Release',
        '-p:DarkFogReferenceMode=reference-assembly-smoke',f'-p:DSPManagedDir={game}',f'-p:BepInExDir={bep}',
        f'-p:CommonApiDir={common}',f'-p:LdbToolDir={ldb}'])
    dll=ROOT/'src/DarkFogSynthesis/bin/Release/net472/DarkFogSynthesis.dll'
    capture=dll.with_name('DarkFogSynthesis.build-inputs.txt').read_text(encoding='utf-8-sig')
    search=sorted({str(Path(line.split('|')[1]).parent) for line in capture.splitlines() if line.startswith('reference|')})
    run(['dotnet','run','--project','scripts/ResourceAudit','-c','Release','--',str(dll),
        'src/DarkFogSynthesis/Localization','--json-report','artifacts/resource-audit.json'])
    run(['dotnet','run','--project','scripts/PublicApiAudit','-c','Release','--',str(dll),str(game/'Assembly-CSharp.dll'),
        *search,'--json-report','artifacts/public-api-audit.json'])
    run(['python','scripts/package.py','--record-build','--configuration','Release','--reference-mode','reference-assembly-smoke'])
    # Compile the exact optional Unity fixture for API/type errors, but never execute
    # Unity's native calls in a managed/reference-only host.
    fixture=temp/'fixture';fixture.mkdir()
    for rel in ('src/DarkFogSynthesis/Diagnostics/RectMaskClipCapture.cs',
                'tests/DarkFogSynthesis.Unity.Tests/RectMaskClipCaptureTests.cs'):
        shutil.copyfile(ROOT/rel,fixture/Path(rel).name)
    references=''.join(f'<Reference Include="{p.stem}" HintPath="{p}" Private="false" />' for p in game.glob('UnityEngine*.dll'))
    (fixture/'Fixture.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net472</TargetFramework><LangVersion>latest</LangVersion><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3"/><PackageReference Include="NUnit" Version="3.13.3"/>'+references+'</ItemGroup></Project>')
    run(['dotnet','build',str(fixture/'Fixture.csproj'),'-c','Release'])
    print('PASS: plugin/Core and optional Unity fixture reference compilation, resource/API audits and provenance. Unity/DSP runtime tests NOT EXECUTED.')
