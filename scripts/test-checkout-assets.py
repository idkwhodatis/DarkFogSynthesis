#!/usr/bin/env python3
"""Check raw asset hashes in fresh Git checkouts with different newline settings.

Requires Python 3.9+, Pillow and Git on PATH, but not an existing .git directory.
Only temporary repositories are created; no caller Git configuration is changed.
This exercises Git's checkout conversion, not a native Windows runtime/build.
"""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
NAMES = ("energy-analysis", "information-topology", "package-icon")
FIXTURE_FILES = (
    ".gitattributes", "scripts/generate-assets.py", "icon.png",
    "assets/generated/assets-manifest.json",
    *(f"assets/source/{name}.svg" for name in NAMES),
    *(f"assets/generated/{name}.png" for name in NAMES),
)
CONTROL = "checkout-newline-control.txt"


class AssetCheckoutTests(unittest.TestCase):
    @classmethod
    def command(cls, args, *, cwd, check=True):
        result = subprocess.run(
            [str(arg) for arg in args], cwd=cwd, env=cls.environment,
            capture_output=True, text=True, timeout=30,
        )
        if check and result.returncode:
            raise AssertionError(
                f"Command failed ({result.returncode}): {args}\n"
                f"{result.stdout}{result.stderr}"
            )
        return result

    @classmethod
    def git(cls, *args, cwd):
        return cls.command([cls.git_executable, *args], cwd=cwd)

    @classmethod
    def setUpClass(cls):
        cls.git_executable = shutil.which("git")
        if not cls.git_executable:
            raise RuntimeError("Install Git to run the isolated asset checkout regression.")
        temporary = tempfile.TemporaryDirectory(prefix="darkfog-checkout-assets-")
        cls.addClassCleanup(temporary.cleanup)
        cls.root = Path(temporary.name)
        cls.source = cls.root / "source"
        cls.source.mkdir()
        cls.template = cls.root / "empty-template"
        cls.template.mkdir()
        # Ignore user/system attributes, templates, hooks and inherited Git repo
        # variables. All settings below belong only to these temporary repos.
        cls.environment = {key: value for key, value in os.environ.items()
                           if not key.startswith("GIT_")}
        cls.environment.update({
            "GIT_CONFIG_NOSYSTEM": "1", "GIT_CONFIG_GLOBAL": os.devnull,
            "GIT_ATTR_NOSYSTEM": "1", "GIT_TERMINAL_PROMPT": "0",
        })
        for relative in FIXTURE_FILES:
            target = cls.source / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((ROOT / relative).read_bytes())
        (cls.source / CONTROL).write_bytes(b"first line\nsecond line\n")
        cls.original_svgs = {
            f"assets/source/{name}.svg": (cls.source / f"assets/source/{name}.svg").read_bytes()
            for name in NAMES
        }
        manifest = json.loads((cls.source / "assets/generated/assets-manifest.json").read_bytes())
        cls.expected_hashes = {asset["source"]: asset["sourceSha256"] for asset in manifest["assets"]}
        # Catch already-damaged fixtures before Git could normalize their bytes.
        cls.command([sys.executable, "scripts/generate-assets.py", "--check"], cwd=cls.source)
        cls.git("init", "--quiet", f"--template={cls.template}", cwd=cls.source)
        cls.git("config", "core.autocrlf", "false", cwd=cls.source)
        cls.git("config", "core.eol", "lf", cwd=cls.source)
        cls.git("config", "user.name", "Asset checkout regression", cwd=cls.source)
        cls.git("config", "user.email", "asset-checkout@example.invalid", cwd=cls.source)
        cls.git("add", "--all", cwd=cls.source)
        cls.git("commit", "--quiet", "-m", "Isolated committed asset fixtures", cwd=cls.source)

    def check_checkout(self, mode):
        checkout = self.root / f"checkout-{mode}"
        self.git("clone", "--quiet", "--no-checkout", "--no-hardlinks",
                 f"--template={self.template}", str(self.source), str(checkout), cwd=self.root)
        self.git("config", "core.autocrlf", mode, cwd=checkout)
        # Make the false/input controls deterministic on every host; autocrlf
        # true overrides core.eol and must still convert the unprotected file.
        self.git("config", "core.eol", "lf", cwd=checkout)
        self.git("checkout", "--quiet", "--force", "HEAD", cwd=checkout)
        expected_control = b"first line\r\nsecond line\r\n" if mode == "true" else b"first line\nsecond line\n"
        self.assertEqual((checkout / CONTROL).read_bytes(), expected_control)
        self.assertEqual(self.git("status", "--porcelain", cwd=checkout).stdout, "")
        for relative, expected in self.original_svgs.items():
            actual = (checkout / relative).read_bytes()
            self.assertNotIn(b"\r", actual, relative)
            self.assertEqual(actual, expected, relative)
            self.assertEqual(hashlib.sha256(actual).hexdigest(), self.expected_hashes[relative], relative)
        self.command([sys.executable, "scripts/generate-assets.py", "--check"], cwd=checkout)

        # A real content change must still fail; never normalize/rewrite hashes
        # to hide the original checkout bug or weaken the raw-byte validator.
        source = checkout / "assets/source/energy-analysis.svg"
        original = source.read_bytes()
        altered = original.replace(b'viewBox="0 0 256 256"', b'viewBox="0 0 255 256"', 1)
        self.assertNotEqual(original, altered, "SVG mutation fixture must change content")
        manifest_path = checkout / "assets/generated/assets-manifest.json"
        manifest_before = manifest_path.read_bytes()
        source.write_bytes(altered)
        rejected = self.command([sys.executable, "scripts/generate-assets.py", "--check"],
                                cwd=checkout, check=False)
        self.assertNotEqual(rejected.returncode, 0)
        self.assertIn("Asset manifest does not match current SVG or PNG files", rejected.stderr)
        self.assertEqual(manifest_path.read_bytes(), manifest_before)

    def test_autocrlf_false(self):
        self.check_checkout("false")

    def test_autocrlf_input(self):
        self.check_checkout("input")

    def test_autocrlf_true(self):
        self.check_checkout("true")


if __name__ == "__main__":
    unittest.main(verbosity=2)
