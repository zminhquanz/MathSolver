"""Behavior checks for the translator workflow; no MAUI runtime required."""
import argparse
import copy
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import localization as loc


class TranslationWorkflowTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name)
        self.source = dict(schemaVersion=1, culture="vi-VN", languageName="Vietnamese",
                           nativeName="Tiếng Việt", strings={"Formula.Label": "= Giá trị {value}",
                                                            "Common.Multiline": "Dòng 1\nDòng 2, có dấu phẩy"},
                           templates={"dynamic.field": "Nhập {field|translate}."})
        self.save("vi-VN.json", self.source)
        self.save("catalog.json", {"sourceCulture": "vi-VN"})
        self.sheet = self.folder / "sheet.csv"
        self.output = self.folder / "target.json"
        self.export_args = argparse.Namespace(resources=self.folder, pack=None,
                                               previous=None, output=self.sheet)
        self.import_args = argparse.Namespace(resources=self.folder, input=self.sheet,
                                               output=self.output, culture="en-US",
                                               language_name="English", native_name="English",
                                               author="Test", allow_partial=False)

    def save(self, name, value):
        (self.folder / name).write_text(json.dumps(value, ensure_ascii=False), encoding="utf-8")

    def export(self):
        with patch.object(loc, "find_references", return_value={}):
            loc.export_sheet(self.export_args)

    def fill(self):
        rows = list(loc.read_sheet(self.sheet).values())
        for row in rows:
            row["translation"] = row["source"]
            row["status"] = "translated"
        loc.write_sheet(self.sheet, rows)
        return rows

    def test_round_trip_unicode_multiline_formula_and_row_order(self):
        self.export()
        rows = self.fill()
        rows.reverse()
        loc.write_sheet(self.sheet, rows)
        self.assertIn("'= Giá trị", self.sheet.read_text(encoding="utf-8-sig"))
        loc.import_sheet(self.import_args)
        actual = loc.read_json(self.output)
        self.assertEqual(actual["strings"], self.source["strings"])
        self.assertEqual(actual["templates"], self.source["templates"])

    def test_duplicate_and_unknown_keys_rejected_without_overwrite(self):
        self.export()
        rows = self.fill()
        self.output.write_text("existing", encoding="utf-8")
        loc.write_sheet(self.sheet, rows + [rows[0]])
        with self.assertRaisesRegex(ValueError, "Duplicate CSV"):
            loc.import_sheet(self.import_args)
        rows[0]["key"] = "typo"
        loc.write_sheet(self.sheet, rows)
        with self.assertRaisesRegex(ValueError, "Unknown keys"):
            loc.import_sheet(self.import_args)
        self.assertEqual(self.output.read_text(), "existing")

    def test_changed_source_requires_review_even_after_repeated_refresh(self):
        self.export()
        self.fill()
        self.source["strings"]["Formula.Label"] = "Giá trị mới {value}"
        self.save("vi-VN.json", self.source)
        with self.assertRaisesRegex(ValueError, "Source changed"):
            loc.import_sheet(self.import_args)
        self.export_args.previous = self.sheet
        self.export()
        self.export()
        rows = loc.read_sheet(self.sheet)
        row = rows[("strings", "Formula.Label")]
        self.assertEqual(row["status"], "needs-review")
        self.assertEqual(row["translation"], "= Giá trị {value}")
        self.assertEqual(row["previous_source"], "= Giá trị {value}")
        with self.assertRaisesRegex(ValueError, "Review required"):
            loc.import_sheet(self.import_args)
        row["status"] = "translated"
        loc.write_sheet(self.sheet, rows.values())
        loc.import_sheet(self.import_args)

    def test_placeholder_modifier_and_missing_variable_rejected(self):
        self.export()
        original = self.fill()
        for replacement in ("Enter {field}.", "Enter a value."):
            rows = copy.deepcopy(original)
            rows[-1]["translation"] = replacement
            loc.write_sheet(self.sheet, rows)
            with self.assertRaisesRegex(ValueError, "Placeholder mismatch"):
                loc.import_sheet(self.import_args)

    def test_partial_omits_blank_and_missing_values(self):
        self.export()
        rows = self.fill()[1:]
        rows[0]["translation"] = "  "
        loc.write_sheet(self.sheet, rows)
        with self.assertRaisesRegex(ValueError, "Missing"):
            loc.import_sheet(self.import_args)
        self.import_args.allow_partial = True
        loc.import_sheet(self.import_args)
        actual = loc.read_json(self.output)
        self.assertEqual(actual["strings"], {})
        self.assertEqual(actual["templates"], self.source["templates"])

    def test_spreadsheet_escape_is_reversible(self):
        for value in ("=1+1", "  @text", "-12", "+text", "'literal", "\tvalue", "Bình thường"):
            self.assertEqual(loc.spreadsheet_decode(loc.spreadsheet_encode(value)), value)

    def test_duplicate_json_key_rejected(self):
        path = self.folder / "duplicate.json"
        path.write_text('{"strings": {"x": "a", "x": "b"}}')
        with self.assertRaisesRegex(ValueError, "Duplicate JSON"):
            loc.read_json(path)


if __name__ == "__main__":
    unittest.main()
