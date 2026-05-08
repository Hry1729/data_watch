from __future__ import annotations

from dataclasses import dataclass
from datetime import UTC, datetime
from io import BytesIO
import re
from typing import Any
from xml.etree import ElementTree as ET
from zipfile import ZIP_DEFLATED, ZipFile


MAIN_NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
REL_NS = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
PACKAGE_REL_NS = "http://schemas.openxmlformats.org/package/2006/relationships"
CONTENT_TYPE_NS = "http://schemas.openxmlformats.org/package/2006/content-types"
DOC_PROPS_NS = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"
DOC_PROPS_VT_NS = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"
CORE_PROPS_NS = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
DCTERMS_NS = "http://purl.org/dc/terms/"
DC_NS = "http://purl.org/dc/elements/1.1/"
XSI_NS = "http://www.w3.org/2001/XMLSchema-instance"

NS = {
    "main": MAIN_NS,
    "rel": REL_NS,
    "pkg": PACKAGE_REL_NS,
}

CELL_REF_RE = re.compile(r"([A-Z]+)(\d+)")


@dataclass(frozen=True)
class WorkbookSheet:
    name: str
    rows: list[list[Any]]


def build_workbook_bytes(sheets: list[WorkbookSheet]) -> bytes:
    if not sheets:
        raise ValueError("at least one sheet is required")

    buffer = BytesIO()
    with ZipFile(buffer, "w", compression=ZIP_DEFLATED) as workbook:
        workbook.writestr("[Content_Types].xml", _build_content_types_xml(len(sheets)))
        workbook.writestr("_rels/.rels", _build_root_relationships_xml())
        workbook.writestr("docProps/app.xml", _build_app_properties_xml(sheets))
        workbook.writestr("docProps/core.xml", _build_core_properties_xml())
        workbook.writestr("xl/workbook.xml", _build_workbook_xml(sheets))
        workbook.writestr("xl/_rels/workbook.xml.rels", _build_workbook_relationships_xml(len(sheets)))
        workbook.writestr("xl/styles.xml", _build_styles_xml())

        for index, sheet in enumerate(sheets, start=1):
            workbook.writestr(
                f"xl/worksheets/sheet{index}.xml",
                _build_sheet_xml(sheet.rows),
            )

    return buffer.getvalue()


def read_workbook_bytes(content: bytes) -> dict[str, list[list[str]]]:
    buffer = BytesIO(content)
    with ZipFile(buffer, "r") as workbook:
        shared_strings = _read_shared_strings(workbook)
        workbook_xml = ET.fromstring(workbook.read("xl/workbook.xml"))
        rels_xml = ET.fromstring(workbook.read("xl/_rels/workbook.xml.rels"))

        rel_map: dict[str, str] = {}
        for relation in rels_xml.findall(f"{{{PACKAGE_REL_NS}}}Relationship"):
            rel_map[str(relation.attrib.get("Id", ""))] = str(relation.attrib.get("Target", ""))

        sheets: dict[str, list[list[str]]] = {}
        for sheet_node in workbook_xml.findall("main:sheets/main:sheet", NS):
            name = str(sheet_node.attrib.get("name", "")).strip() or "Sheet"
            rel_id = str(sheet_node.attrib.get(f"{{{REL_NS}}}id", "")).strip()
            target = rel_map.get(rel_id)
            if not target:
                continue

            target_path = target if target.startswith("xl/") else f"xl/{target.lstrip('/')}"
            sheets[name] = _read_sheet_rows(workbook.read(target_path), shared_strings)

        return sheets


def _build_content_types_xml(sheet_count: int) -> str:
    sheet_overrides = "".join(
        f'<Override PartName="/xl/worksheets/sheet{index}.xml" '
        f'ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>'
        for index in range(1, sheet_count + 1)
    )
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        f'<Types xmlns="{CONTENT_TYPE_NS}">'
        '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
        '<Default Extension="xml" ContentType="application/xml"/>'
        '<Override PartName="/xl/workbook.xml" '
        'ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
        f"{sheet_overrides}"
        '<Override PartName="/xl/styles.xml" '
        'ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>'
        '<Override PartName="/docProps/core.xml" '
        'ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>'
        '<Override PartName="/docProps/app.xml" '
        'ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>'
        "</Types>"
    )


def _build_root_relationships_xml() -> str:
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        f'<Relationships xmlns="{PACKAGE_REL_NS}">'
        '<Relationship Id="rId1" '
        'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" '
        'Target="xl/workbook.xml"/>'
        '<Relationship Id="rId2" '
        'Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" '
        'Target="docProps/core.xml"/>'
        '<Relationship Id="rId3" '
        'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" '
        'Target="docProps/app.xml"/>'
        "</Relationships>"
    )


def _build_app_properties_xml(sheets: list[WorkbookSheet]) -> str:
    titles = "".join(f"<vt:lpstr>{_escape_xml(sheet.name)}</vt:lpstr>" for sheet in sheets)
    count = len(sheets)
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        f'<Properties xmlns="{DOC_PROPS_NS}" xmlns:vt="{DOC_PROPS_VT_NS}">'
        "<Application>Codex</Application>"
        "<DocSecurity>0</DocSecurity>"
        "<ScaleCrop>false</ScaleCrop>"
        "<HeadingPairs><vt:vector size=\"2\" baseType=\"variant\">"
        "<vt:variant><vt:lpstr>Worksheets</vt:lpstr></vt:variant>"
        f"<vt:variant><vt:i4>{count}</vt:i4></vt:variant>"
        "</vt:vector></HeadingPairs>"
        f"<TitlesOfParts><vt:vector size=\"{count}\" baseType=\"lpstr\">{titles}</vt:vector></TitlesOfParts>"
        "<Company>OpenAI Codex</Company>"
        "<LinksUpToDate>false</LinksUpToDate>"
        "<SharedDoc>false</SharedDoc>"
        "<HyperlinksChanged>false</HyperlinksChanged>"
        "<AppVersion>1.0</AppVersion>"
        "</Properties>"
    )


def _build_core_properties_xml() -> str:
    timestamp = datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        f'<cp:coreProperties xmlns:cp="{CORE_PROPS_NS}" xmlns:dc="{DC_NS}" '
        f'xmlns:dcterms="{DCTERMS_NS}" xmlns:xsi="{XSI_NS}">'
        "<dc:creator>OpenAI Codex</dc:creator>"
        "<cp:lastModifiedBy>OpenAI Codex</cp:lastModifiedBy>"
        f'<dcterms:created xsi:type="dcterms:W3CDTF">{timestamp}</dcterms:created>'
        f'<dcterms:modified xsi:type="dcterms:W3CDTF">{timestamp}</dcterms:modified>'
        "</cp:coreProperties>"
    )


def _build_workbook_xml(sheets: list[WorkbookSheet]) -> str:
    sheet_entries = "".join(
        f'<sheet name="{_escape_attr(sheet.name)}" sheetId="{index}" r:id="rId{index}"/>'
        for index, sheet in enumerate(sheets, start=1)
    )
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        f'<workbook xmlns="{MAIN_NS}" xmlns:r="{REL_NS}">'
        f"<sheets>{sheet_entries}</sheets>"
        "</workbook>"
    )


def _build_workbook_relationships_xml(sheet_count: int) -> str:
    sheet_relationships = "".join(
        f'<Relationship Id="rId{index}" '
        'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" '
        f'Target="worksheets/sheet{index}.xml"/>'
        for index in range(1, sheet_count + 1)
    )
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        f'<Relationships xmlns="{PACKAGE_REL_NS}">'
        f"{sheet_relationships}"
        f'<Relationship Id="rId{sheet_count + 1}" '
        'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" '
        'Target="styles.xml"/>'
        "</Relationships>"
    )


def _build_styles_xml() -> str:
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        f'<styleSheet xmlns="{MAIN_NS}">'
        '<fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>'
        '<fills count="1"><fill><patternFill patternType="none"/></fill></fills>'
        '<borders count="1"><border/></borders>'
        '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>'
        '<cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs>'
        '<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>'
        "</styleSheet>"
    )


def _build_sheet_xml(rows: list[list[Any]]) -> str:
    max_row = max(len(rows), 1)
    max_col = max((len(row) for row in rows), default=1)
    dimension = f"A1:{_column_name(max_col)}{max_row}"
    row_xml = "".join(
        _build_row_xml(row_index=index, values=row)
        for index, row in enumerate(rows, start=1)
    )
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        f'<worksheet xmlns="{MAIN_NS}">'
        f'<dimension ref="{dimension}"/>'
        f"<sheetData>{row_xml}</sheetData>"
        "</worksheet>"
    )


def _build_row_xml(*, row_index: int, values: list[Any]) -> str:
    cells = []
    for col_index, value in enumerate(values, start=1):
        if value in (None, ""):
            continue
        ref = f"{_column_name(col_index)}{row_index}"
        if isinstance(value, bool):
            cells.append(f'<c r="{ref}" t="b"><v>{"1" if value else "0"}</v></c>')
            continue
        if isinstance(value, (int, float)) and not isinstance(value, bool):
            cells.append(f'<c r="{ref}"><v>{value}</v></c>')
            continue
        text = _escape_xml(str(value))
        cells.append(
            f'<c r="{ref}" t="inlineStr"><is><t xml:space="preserve">{text}</t></is></c>'
        )
    return f'<row r="{row_index}">{"".join(cells)}</row>'


def _read_shared_strings(workbook: ZipFile) -> list[str]:
    try:
        raw = workbook.read("xl/sharedStrings.xml")
    except KeyError:
        return []

    xml = ET.fromstring(raw)
    values: list[str] = []
    for item in xml.findall("main:si", NS):
        parts = [node.text or "" for node in item.findall(".//main:t", NS)]
        values.append("".join(parts))
    return values


def _read_sheet_rows(raw_sheet: bytes, shared_strings: list[str]) -> list[list[str]]:
    sheet_xml = ET.fromstring(raw_sheet)
    rows: list[list[str]] = []

    for row_node in sheet_xml.findall("main:sheetData/main:row", NS):
        row_values: dict[int, str] = {}
        max_col = 0
        for cell in row_node.findall("main:c", NS):
            ref = str(cell.attrib.get("r", "")).upper()
            match = CELL_REF_RE.fullmatch(ref)
            if not match:
                continue
            col_idx = _column_index(match.group(1))
            row_values[col_idx] = _read_cell_value(cell, shared_strings)
            max_col = max(max_col, col_idx)
        rows.append([row_values.get(col, "") for col in range(1, max_col + 1)])

    return rows


def _read_cell_value(cell: ET.Element, shared_strings: list[str]) -> str:
    cell_type = str(cell.attrib.get("t", ""))
    if cell_type == "inlineStr":
        parts = [node.text or "" for node in cell.findall(".//main:t", NS)]
        return "".join(parts).strip()

    value_node = cell.find("main:v", NS)
    raw_value = value_node.text if value_node is not None and value_node.text is not None else ""
    raw_value = raw_value.strip()
    if cell_type == "s":
        try:
            return shared_strings[int(raw_value)].strip()
        except (ValueError, IndexError):
            return ""
    if cell_type == "b":
        return "true" if raw_value == "1" else "false"
    return raw_value


def _column_name(index: int) -> str:
    if index <= 0:
        raise ValueError("column index must be positive")

    result = []
    current = index
    while current > 0:
        current, remainder = divmod(current - 1, 26)
        result.append(chr(65 + remainder))
    return "".join(reversed(result))


def _column_index(name: str) -> int:
    value = 0
    for char in name:
        value = value * 26 + (ord(char) - 64)
    return value


def _escape_xml(value: str) -> str:
    return (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )


def _escape_attr(value: str) -> str:
    return _escape_xml(value).replace('"', "&quot;")
