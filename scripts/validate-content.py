#!/usr/bin/env python3
"""Check checked-in localization, IDs and acceptance boundaries without game DLLs."""
from __future__ import annotations

import json
from pathlib import Path
import re
import sys

from package import CHECK_IDS, validate_manifest

ROOT = Path(__file__).resolve().parents[1]
TECHS = ("energy_analysis", "information_topology")
RECIPES = ("energy_shard", "dark_fog_matrix", "silicon_neuron", "matter_recombinator", "negentropy_singularity", "core_element")


def unique_object(pairs: list[tuple]) -> dict:
    value = {}
    for key, item in pairs:
        if key in value:
            raise ValueError(f"Duplicate JSON key: {key}")
        value[key] = item
    return value


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_object)


def main() -> int:
    plan = (ROOT / "DarkFogSynthesis_Implementation_Plan_ZH.md").read_text(encoding="utf-8")
    required = {f"dark_fog_synthesis.tech.{key}.{field}" for key in TECHS for field in ("name", "description", "conclusion")}
    required.update(f"dark_fog_synthesis.recipe.{key}.{field}" for key in RECIPES for field in ("name", "description"))
    locales = [load(ROOT / "src/DarkFogSynthesis/Localization" / f"Strings.{language}.json") for language in ("en-US", "zh-CN")]
    if set(locales[0]) != set(locales[1]):
        raise ValueError("English and Chinese localization key sets differ")
    for locale in locales:
        if not required <= set(locale):
            raise ValueError("Missing technology or recipe localization key")
        if any(not key.startswith("dark_fog_synthesis.") or not isinstance(value, str) or not value.strip() for key, value in locale.items()):
            raise ValueError("Localization must have namespaced keys and nonempty string values")
        for key in TECHS:
            for field in ("name", "description", "conclusion"):
                value = locale[f"dark_fog_synthesis.tech.{key}.{field}"]
                if value not in plan:
                    raise ValueError(f"Technology {key}.{field} differs from approved plan text")
    ids = load(ROOT / "docs/compatibility/proto-ids.json")
    if ids["techs"] != dict(zip(TECHS, (1951, 1952))) or ids["recipes"] != dict(zip(RECIPES, range(48101, 48107))):
        raise ValueError("Fixed prototype ID manifest changed")
    constants = (ROOT / "src/DarkFogSynthesis.Core/Definitions/ProtoIds.cs").read_text(encoding="utf-8")
    actual_ids = [int(value) for value in re.findall(r"new (?:TechId|RecipeId)\((\d+)\)", constants)]
    if actual_ids != [1951, 1952, *range(48101, 48107)]:
        raise ValueError("Core constants differ from ID manifest")
    acceptance = load(ROOT / "docs/compatibility/acceptance-status.json")
    checks = acceptance["checks"]
    if len(checks) != len(CHECK_IDS) or {c["id"] for c in checks} != set(CHECK_IDS):
        raise ValueError("Acceptance matrix is missing or duplicating required scenarios")
    statuses = {"passed", "failed", "not_executed", "environment_limited"}
    if any(c["status"] not in statuses for c in checks):
        raise ValueError("Unknown acceptance status")
    if acceptance.get("releaseEligible") and any(c["status"] != "passed" for c in checks):
        raise ValueError("Release eligibility cannot coexist with incomplete acceptance")
    manifest = validate_manifest()
    print(f"PASS: {len(locales[0])} bilingual keys; exact approved tech prose; fixed IDs; {len(checks)} acceptance IDs; Thunderstore manifest {manifest['version_number']}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as error:
        print(f"Content validation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
