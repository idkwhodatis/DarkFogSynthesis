#!/usr/bin/env python3
"""Temporary, bounded Patch A integration; removed after this task."""
from pathlib import Path
import os
import subprocess
os.chdir(Path(__file__).resolve().parents[1])
subprocess.run(['git', 'diff', '--exit-code', '8352245bd270012b0aa47ced3838b4dc4453e66c', 'HEAD', '--', '.', ':(exclude)scripts/dfs-adaptation-step.py', ':(exclude)scripts/dfs-adaptation.patch', ':(exclude).github/workflows/dfs-adaptation.yml'], check=True)
subprocess.run(['git', 'apply', '--check', '--index', 'scripts/dfs-adaptation.patch'], check=True)
subprocess.run(['git', 'apply', '--index', 'scripts/dfs-adaptation.patch'], check=True)
with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as stream:
    stream.write('message=Prepare all icons and recipe slots before queuing content\n')
