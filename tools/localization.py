"""Export, refresh and import translator CSVs. Python 3.9+, standard library only."""
import argparse
import csv
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RESOURCES = ROOT / "MathSolver/Resources/Raw/Localization"
SECTIONS = ("strings", "templates")
COLUMNS = ["section", "key", "context", "source", "description", "notes",
           "placeholders", "screenshot", "references", "translation", "status",
           "previous_source", "source_hash"]
# Same placeholder grammar as LanguagePackValidator; retain modifiers as well.
PLACEHOLDER = re.compile(r"(?<!\{)\{([A-Za-z0-9_.-]+)(\|translate)?(:[^{}]+)?\}(?!\})")


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"),
                      object_pairs_hook=unique_object)


def digest(source):
    return hashlib.sha256(source.encode("utf-8")).hexdigest()


def placeholders(value):
    return {match.group(0) for match in PLACEHOLDER.finditer(value)}


def spreadsheet_encode(value):
    # An apostrophe prevents Excel interpreting text such as =, + or @ as formulas.
    # Escape leading apostrophes too so this representation is reversible.
    if value.startswith("'") or value.lstrip().startswith(("=", "+", "-", "@")) or value.startswith(("\t", "\r")):
        return "'" + value
    return value


def spreadsheet_decode(value):
    return value[1:] if value.startswith("'") else value


def read_sheet(path):
    with Path(path).open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        if reader.fieldnames != COLUMNS:
            raise ValueError("CSV headers were changed. Use the exported column names/order.")
        result = {}
        for number, raw in enumerate(reader, 2):
            if None in raw or any(value is None for value in raw.values()):
                raise ValueError(f"Invalid CSV row {number}")
            row = {key: spreadsheet_decode(value) for key, value in raw.items()}
            identity = (row["section"], row["key"])
            if identity in result:
                raise ValueError(f"Duplicate CSV key at row {number}: {identity}")
            result[identity] = row
        return result


def write_sheet(path, rows):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=COLUMNS)
        writer.writeheader()
        writer.writerows({key: spreadsheet_encode(row.get(key, "")) for key in COLUMNS}
                         for row in rows)


def load_sources(resources):
    catalog = read_json(resources / "catalog.json")
    source = read_json(resources / (catalog["sourceCulture"] + ".json"))
    return catalog, source


def check_pack(pack, source):
    if pack.get("schemaVersion") != 1:
        raise ValueError("Unsupported schemaVersion")
    # Syntax check only. The app still validates against .NET CultureInfo on import.
    if not re.fullmatch(r"[a-zA-Z]{2,8}(?:-[a-zA-Z0-9]{1,8})*", pack.get("culture", "")):
        raise ValueError("Invalid culture code syntax")
    for field in ("languageName", "nativeName"):
        if not isinstance(pack.get(field), str) or not pack[field].strip():
            raise ValueError(f"Missing {field}")
    for section in SECTIONS:
        values = pack.get(section)
        if not isinstance(values, dict):
            raise ValueError(f"Missing dictionary: {section}")
        for key, value in values.items():
            if key not in source[section]:
                raise ValueError(f"Unknown {section} key: {key}")
            if not isinstance(value, str):
                raise ValueError(f"Translation must be text: {key}")
            if value.strip() and placeholders(value) != placeholders(source[section][key]):
                raise ValueError(f"Placeholder mismatch: {key}")


def find_references(source, project):
    result = {}
    # Scan once. Literal-source references also help locate legacy translations.
    sources = {}
    for section in SECTIONS:
        for key, value in source[section].items():
            sources.setdefault(value, []).append((section, key))
    keys = {key: (section, key) for section in SECTIONS for key in source[section]}
    for path in sorted(project.rglob("*")):
        if path.suffix not in (".cs", ".xaml") or any(part in ("obj", "bin") for part in path.parts):
            continue
        for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
            for literal in re.findall(r'"([^"\n]*)"', line):
                key = literal.removeprefix("{localization:Translate ").removesuffix("}")
                identities = ([keys[key]] if key in keys else []) + sources.get(literal, [])
                for identity in identities:
                    reference = f"{path.relative_to(project).as_posix()}:{number}"
                    refs = result.setdefault(identity, [])
                    if reference not in refs and len(refs) < 4:
                        refs.append(reference)
    return result


def export_sheet(args):
    catalog, source = load_sources(args.resources)
    pack = read_json(args.pack) if args.pack else None
    if pack:
        check_pack(pack, source)
    previous = read_sheet(args.previous) if args.previous else {}
    references = find_references(source, ROOT / "MathSolver")
    rows = []
    identities = set()
    for section in SECTIONS:
        for key, value in source[section].items():
            identity = (section, key)
            identities.add(identity)
            note = catalog.get("translationNotes", {}).get(key, {})
            group = catalog.get("translationGroups", {}).get(key.split(".")[0], "")
            old = previous.get(identity)
            translation = old["translation"] if old else (pack or {}).get(section, {}).get(key, "")
            status = "translated" if translation.strip() else "untranslated"
            previous_source = ""
            if old and (old["source"] != value or old["source_hash"] != digest(value)
                        or old["status"] == "needs-review"):
                status = "needs-review"
                previous_source = old["previous_source"] if old["source"] == value else old["source"]
            hints = []
            for match in PLACEHOLDER.finditer(value):
                name = match.group(1)
                hint = note.get("placeholders", {}).get(name, catalog.get("placeholderNotes", {}).get(name, ""))
                hints.append(match.group(0) + ": " + (hint or "Cần bổ sung ý nghĩa và ví dụ cho biến này."))
            rows.append(dict(section=section, key=key, source=value,
                             context=note.get("context") or group or "Cần bổ sung ngữ cảnh trước khi dịch.",
                             description=note.get("description", ""),
                             notes=note.get("translatorNote", ""),
                             placeholders="\n".join(dict.fromkeys(hints)),
                             screenshot=note.get("screenshot", ""),
                             references="\n".join(references.get(identity, [])),
                             translation=translation, status=status,
                             previous_source=previous_source, source_hash=digest(value)))
    removed = set(previous) - identities
    if removed:
        raise ValueError(f"Previous sheet contains {len(removed)} removed/unknown keys. Archive and remove those rows before refreshing: {sorted(removed)[:5]}")
    write_sheet(args.output, rows)
    print(f"Exported {len(rows)} rows to {args.output}")


def import_sheet(args):
    _, source = load_sources(args.resources)
    rows = read_sheet(args.input)
    expected = {(section, key) for section in SECTIONS for key in source[section]}
    unknown = set(rows) - expected
    if unknown:
        raise ValueError(f"Unknown keys: {sorted(unknown)[:5]}")
    errors = []
    missing = expected - set(rows)
    if missing and not args.allow_partial:
        errors.append(f"Missing {len(missing)} rows")
    pack = dict(schemaVersion=1, culture=args.culture, languageName=args.language_name,
                nativeName=args.native_name, author=args.author,
                appVersion=source.get("appVersion", ""), strings={}, templates={})
    skipped = len(missing)
    for (section, key), row in rows.items():
        value = source[section][key]
        if row["source"] != value or row["source_hash"] != digest(value):
            errors.append(f"Source changed: {key}. Refresh the sheet with --previous.")
        if row["status"] not in ("translated", "untranslated", "needs-review"):
            errors.append(f"Invalid status: {key}")
        if row["status"] == "needs-review":
            errors.append(f"Review required: {key}")
        translated = row["translation"]
        if not translated.strip():
            skipped += 1
            if not args.allow_partial:
                errors.append(f"Empty translation: {key}")
            continue
        pack[section][key] = translated
    if errors:
        raise ValueError("\n".join(errors[:20]) + (f"\n({len(errors)} errors total)" if len(errors) > 20 else ""))
    check_pack(pack, source)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    # All validation finishes before opening the destination.
    output.write_text(json.dumps(pack, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Created {output}; {skipped} missing/empty translations omitted (app uses Vietnamese fallback).")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--resources", type=Path, default=RESOURCES)
    commands = parser.add_subparsers(dest="command", required=True)
    export = commands.add_parser("export", help="Create or refresh a UTF-8 CSV")
    export.add_argument("--pack", type=Path, help="Existing target language JSON")
    export.add_argument("--previous", type=Path, help="Earlier CSV; preserves translations and flags source changes")
    export.add_argument("--output", type=Path, required=True)
    export.set_defaults(run=export_sheet)
    imp = commands.add_parser("import", help="Validate CSV and create a language JSON")
    imp.add_argument("--input", type=Path, required=True)
    imp.add_argument("--output", type=Path, required=True)
    imp.add_argument("--culture", required=True)
    imp.add_argument("--language-name", required=True)
    imp.add_argument("--native-name", required=True)
    imp.add_argument("--author", default="")
    imp.add_argument("--allow-partial", action="store_true", help="Omit missing/empty values for app fallback")
    imp.set_defaults(run=import_sheet)
    args = parser.parse_args()
    if getattr(args, "pack", None) and getattr(args, "previous", None):
        parser.error("Choose --pack or --previous, not both")
    try:
        args.run(args)
    except (ValueError, OSError, KeyError, TypeError, csv.Error) as error:
        print(f"Error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
