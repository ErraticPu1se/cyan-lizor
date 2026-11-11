import copy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("verify_macos_handoff", ROOT / "tools/verify_macos_handoff.py")
checks = importlib.util.module_from_spec(spec)
spec.loader.exec_module(checks)


class MacHandoffTests(unittest.TestCase):
    def setUp(self):
        self.catalog = json.loads((ROOT / "macos/presets.json").read_text(encoding="utf-8"))

    def test_real_catalog_and_source_links(self):
        checks.main()

    def test_rejects_duplicate_and_unknown_ids(self):
        for value in ("9320", "12345", 7205):
            with self.subTest(value=value):
                data = copy.deepcopy(self.catalog)
                data["presets"][1]["appearanceId"] = value
                with self.assertRaises(ValueError):
                    checks.validate_catalog(data)

    def test_rejects_multiple_active_pets_and_wrong_types(self):
        for value in (0, 2, True, "1", None):
            with self.subTest(value=value):
                data = copy.deepcopy(self.catalog)
                data["maxActivePets"] = value
                with self.assertRaises(ValueError):
                    checks.validate_catalog(data)

    def test_rejects_missing_or_extra_presets(self):
        for presets in ([], None, self.catalog["presets"][:2], self.catalog["presets"] * 2):
            data = copy.deepcopy(self.catalog)
            data["presets"] = presets
            with self.assertRaises(ValueError):
                checks.validate_catalog(data)

    def test_rejects_fake_ready_assets_and_machine_paths(self):
        data = copy.deepcopy(self.catalog)
        data["presets"][0]["assetStatus"] = "ready"
        with self.assertRaises(ValueError):
            checks.validate_catalog(data)
        data = copy.deepcopy(self.catalog)
        data["gamePath"] = "machine-specific-path"
        with self.assertRaises(ValueError):
            checks.validate_catalog(data)

    def test_rejects_schema_species_and_name_changes(self):
        for field, value in (("schemaVersion", 2), ("schemaVersion", True), ("species", "white")):
            data = copy.deepcopy(self.catalog)
            data[field] = value
            with self.assertRaises(ValueError):
                checks.validate_catalog(data)
        self.catalog["presets"][1]["name"] = "changed"
        with self.assertRaises(ValueError):
            checks.validate_catalog(self.catalog)


if __name__ == "__main__":
    unittest.main()
