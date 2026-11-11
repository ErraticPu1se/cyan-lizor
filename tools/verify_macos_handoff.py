"""Validate preparation metadata only; this does not certify a runnable Mac build."""
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
EXPECTED = {"9320": "3 droplets", "7205": "你长的很像我的一个故人", "3616": "big fluffy tail"}


def validate_catalog(catalog):
    if not isinstance(catalog, dict):
        raise ValueError("Catalog must be an object")
    if set(catalog) != {"schemaVersion", "status", "maxActivePets", "species", "presets"}:
        raise ValueError("Unexpected catalog fields")
    if type(catalog["schemaVersion"]) is not int or catalog["schemaVersion"] != 1:
        raise ValueError("Unsupported catalog version")
    if catalog["status"] != "planning-only" or catalog["species"] != "cyan":
        raise ValueError("This handoff only defines planned cyan presets")
    if type(catalog["maxActivePets"]) is not int or catalog["maxActivePets"] != 1:
        raise ValueError("First Mac scope allows exactly one active pet")
    presets = catalog["presets"]
    if not isinstance(presets, list) or len(presets) != 3:
        raise ValueError("Exactly three presets are approved")
    seen = set()
    for preset in presets:
        if not isinstance(preset, dict) or set(preset) != {"key", "appearanceId", "name", "assetStatus"}:
            raise ValueError("Preset must contain identity metadata only")
        identity = preset["appearanceId"]
        if not isinstance(identity, str) or identity not in EXPECTED or identity in seen:
            raise ValueError("Unknown or duplicate preset ID")
        seen.add(identity)
        if preset["key"] != "cyan-" + identity or preset["name"] != EXPECTED[identity]:
            raise ValueError("Preset identity/name does not match the approved catalog")
        if preset["assetStatus"] != "not-exported":
            raise ValueError("This preparation has no exported appearance payload")


def main():
    validate_catalog(json.loads((ROOT / "macos/presets.json").read_text(encoding="utf-8")))
    for relative in ("MAC_HANDOFF.md", "docs/macos-dependency-audit.md"):
        text = (ROOT / relative).read_text(encoding="utf-8")
        for source in re.findall(r"`(Assets/[^`]+\.cs)`", text):
            if not (ROOT / source).is_file():
                raise ValueError("Audit references a missing source: " + source)
    print("MAC_HANDOFF_CHECKS_PASSED: three cyan identities, one active pet; appearance assets and Mac runtime still pending.")


if __name__ == "__main__":
    main()
