#!/usr/bin/env python3
"""Extract an owner-supplied export into a private first-install CenterState.

Requires Python 3.11+ and openpyxl. Reads source files without modifying them.
The mapping file is an explicit review decision, not inferred payment evidence.
Unresolved records are preserved separately; stdout contains counts only.
"""

from __future__ import annotations

import argparse
import collections
import csv
import datetime as dt
import hashlib
import json
import math
import os
from pathlib import Path
import re
import tempfile
import unicodedata
import zipfile
import xml.etree.ElementTree as ET

import openpyxl

UNKNOWN_DATE = "1970-01-01T00:00:00.000"
PROFILE_HEADERS = (
    "اسم الطالب", "رقم الطالب", "رقم ولي الأمر", "الرقم التعريفي",
    "المجموعة", "المستوى", "المركز", "الخصم", "تاريخ التسجيل",
)
EXAM_HEADERS = ("الامتحان الاول (10)", "الامتحان التاني (10)", "الامتحان الثالث (10)")
SENSITIVE_HEADERS = {"كلمة المرور", "password", "Password"}


class ImportFailure(Exception):
    """Safe diagnostic; never includes a student's source values."""


def text(value: object) -> str:
    if value is None:
        return ""
    if isinstance(value, bool):
        return "True" if value else "False"
    if isinstance(value, float):
        if not math.isfinite(value):
            raise ImportFailure("non_finite_source_number")
        if value.is_integer():
            return str(int(value))
    value = str(value).strip()
    literal = re.fullmatch(r'="((?:[^\"]|\"\")*)"', value)
    return literal.group(1).replace('""', '"') if literal else value


def normalized(value: object) -> str:
    return " ".join(unicodedata.normalize("NFC", text(value)).split())


def identifier(value: object) -> str:
    result = text(value)
    if isinstance(value, float) and abs(value) > 2**53:
        raise ImportFailure("imprecise_excel_identifier")
    if not re.fullmatch(r"[0-9]{5,}", result):
        raise ImportFailure("missing_or_invalid_full_id")
    return result


def stable_id(kind: str, value: str) -> str:
    return f"seed-{kind}-" + hashlib.sha256(value.encode()).hexdigest()[:24]


def source_date(value: object) -> str:
    if isinstance(value, dt.datetime):
        parsed = value
    else:
        try:
            parsed = dt.datetime.fromisoformat(text(value))
        except ValueError as error:
            raise ImportFailure("missing_or_invalid_registration_date") from error
    return parsed.isoformat(timespec="milliseconds")


def json_value(value: object) -> object:
    if isinstance(value, (dt.datetime, dt.date)):
        return value.isoformat()
    if isinstance(value, float) and not math.isfinite(value):
        return {"unreadableNumber": str(value)}
    return value


def atomic_private_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(prefix=".seed-", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as stream:
            json.dump(value, stream, ensure_ascii=False, allow_nan=False, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(temporary, 0o600)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def workbook_tables(path: Path):
    workbook = openpyxl.load_workbook(path, read_only=True, data_only=True)
    try:
        for sheet in workbook:
            # Some supplied workbooks incorrectly declare A1:A1 in XML.
            sheet.reset_dimensions()
            iterator = iter(sheet.values)
            headers = tuple(next(iterator, ()))
            rows = [(number, tuple(row)) for number, row in enumerate(iterator, 2)
                    if any(value not in (None, "") for value in row)]
            yield sheet.title, headers, rows
    finally:
        workbook.close()


def row_dict(headers, row) -> dict:
    return {text(header) if header is not None else f"column_{index + 1}":
            json_value(row[index]) if index < len(row) else None
            for index, header in enumerate(headers) if header not in SENSITIVE_HEADERS}


def boolean(value: object) -> bool | None:
    return {"true": True, "false": False}.get(text(value).lower())


def explicit_discount_percent(value: object) -> int | float | None:
    value = normalized(value)
    if value in ("ربع", "ربع الاشتراك", "¼"):
        return 25
    fraction = re.fullmatch(r"(\d+)\s*/\s*(\d+)", value)
    if fraction and int(fraction[2]) > 0 and 0 <= int(fraction[1]) <= int(fraction[2]):
        percent = 100 * int(fraction[1]) / int(fraction[2])
        return int(percent) if percent.is_integer() else percent
    return None


def attendance_number(filename: str) -> int | None:
    name = normalized(filename)
    if "الشهر الثاني" in name:
        return 5
    for number, words in (
        (1, ("اول", "أول", "حصه1", "حصه 1")),
        (2, ("تاني", "ثاني", "التانيه", "حصه 2")),
        (3, ("تالت", "ثالث", "الثالثه", "حصه 3", "حضور 3")),
        (4, ("رابع", "الرابعه", "حصه 4")),
    ):
        if any(word in name for word in words):
            return number
    return None


def empty_state() -> dict:
    lists = ("catalogs", "groups", "students", "sessions", "packages", "attendances",
             "payments", "academics", "academicActivities", "audit", "staff", "reviews",
             "closings", "paymentChecks", "corrections", "refunds", "cardPayments", "cardReceipts")
    return {"schemaVersion": 3, **{key: [] for key in lists},
            "cardSettings": {"price": None, "requirePaymentBeforeReceipt": True},
            "credentials": {}, "enrollments": {}}


class SeedImporter:
    def __init__(self, source: Path, mapping: dict):
        self.source, self.mapping = source, mapping
        self.state = empty_state()
        self.inventory, self.unresolved, self.provenance, self.cairo_candidates = [], [], [], []
        self.counts = collections.Counter()
        self.students_by_barcode, self.groups_by_folder = {}, {}
        self.identity_by_profile = collections.defaultdict(list)
        self.catalog_by_key, self.sessions_by_pair = {}, {}
        self.master_rosters, self.monthly_rosters = {}, {}
        self.discount_corroborations = collections.defaultdict(list)
        self.attendance_pairs, self.academic_keys = set(), set()
        self.fatal = []
        self.generated_at = dt.datetime.now().astimezone().isoformat(timespec="milliseconds")

    def relative(self, path: Path) -> str:
        return path.relative_to(self.source).as_posix()

    def issue(self, kind: str, path: Path, row: int | None = None,
              values: dict | None = None, **details) -> None:
        self.counts[kind] += 1
        self.unresolved.append({"kind": kind, "file": self.relative(path),
                                "row": row, "values": values, **details})

    def catalog(self, kind: str, name: str) -> str:
        key = (kind, normalized(name))
        if not key[1]:
            raise ImportFailure("empty_catalog")
        if key not in self.catalog_by_key:
            value = {"id": stable_id(kind, key[1]), "name": key[1], "kind": kind}
            self.state["catalogs"].append(value)
            self.catalog_by_key[key] = value["id"]
        return self.catalog_by_key[key]

    def group_for(self, path: Path, row: dict) -> dict:
        folder = self.relative(path.parent)
        name, grade, center = (normalized(row[key]) for key in ("المجموعة", "المستوى", "المركز"))
        if not all((name, grade, center)):
            raise ImportFailure("missing_group_catalog_mapping")
        existing = self.groups_by_folder.get(folder)
        if existing:
            if (existing["name"], existing["sourceGrade"], existing["sourceCenter"]) != (name, grade, center):
                raise ImportFailure("conflicting_group_catalog_mapping")
            return existing
        group = {"id": stable_id("group", name), "name": name,
                 "subjectId": self.catalog("subject", self.mapping["subject"]),
                 "centerId": self.catalog("center", center), "gradeId": self.catalog("grade", grade),
                 "schedule": name, "sessionPrice": 0, "packagePrice": 0,
                 "priceConfigured": False}
        prices = self.mapping.get("prices", {}).get(name, self.mapping.get("defaultPrices"))
        if prices is not None:
            for key in ("sessionPrice", "packagePrice", "twoSessionPrice", "threeSessionPrice"):
                if key in prices:
                    if type(prices[key]) is not int or prices[key] < 0:
                        raise ImportFailure("invalid_explicit_group_price")
                    group[key] = prices[key]
            if "sessionPrice" not in prices or "packagePrice" not in prices:
                raise ImportFailure("incomplete_explicit_group_prices")
            group["priceConfigured"] = True
        self.state["groups"].append(group)
        result = {**group, "sourceGrade": grade, "sourceCenter": center}
        self.groups_by_folder[folder] = result
        return result

    def add_profile(self, path: Path, sheet: str, row_number: int, row: dict) -> None:
        self.counts["profile_rows"] += 1
        try:
            barcode = identifier(row["الرقم التعريفي"])
            name = normalized(row["اسم الطالب"])
            if not name:
                raise ImportFailure("missing_student_name")
            registered = source_date(row["تاريخ التسجيل"])
            group = self.group_for(path, row)
            student = {"id": stable_id("student", barcode), "barcode": barcode,
                       "code": barcode[-5:], "name": name,
                       "phone": text(row["رقم الطالب"]), "guardianPhone": text(row["رقم ولي الأمر"]),
                       "groupIds": [group["id"]], "discountPercent": 0, "notes": "",
                       "createdAt": registered}
            discount = text(row["الخصم"])
            if discount not in ("", "0"):
                percent = explicit_discount_percent(discount)
                corroboration = self.discount_corroborations.get(barcode, [])
                if percent is not None:
                    student["discountPercent"] = percent
                    self.counts["explicit_fraction_discount_resolved"] += 1
                elif discount == "4" and corroboration:
                    student["discountPercent"] = 25
                    self.counts["quarter_discount_corroborated"] += 1
                    self.provenance.append({"entity": "discount", "id": student["id"],
                                            "percent": 25, "basis": corroboration})
                else:
                    student["notes"] = "خصم المصدر غير مُفسَّر، لم يُطبق: " + discount
                    student["discountNeedsReview"] = True
                    self.issue("uninterpreted_discount", path, row_number, {"barcode": barcode, "discount": discount})
            old = self.students_by_barcode.get(barcode)
            if old:
                if any(old[key] != student[key] for key in ("name", "phone", "guardianPhone", "discountPercent")):
                    raise ImportFailure("conflicting_duplicate_full_id")
                if group["id"] not in old["groupIds"]:
                    old["groupIds"].append(group["id"])
                old["createdAt"] = min(old["createdAt"], registered)
                student = old
            else:
                self.students_by_barcode[barcode] = student
                self.state["students"].append(student)
            self.state["enrollments"][f'{student["id"]}:{group["id"]}'] = registered
            key = (self.relative(path.parent), normalized(student["name"]),
                   normalized(student["phone"]), normalized(student["guardianPhone"]))
            self.identity_by_profile[key].append(barcode)
            self.provenance.append({"entity": "student", "id": student["id"],
                                    "file": self.relative(path), "sheet": sheet, "row": row_number})
        except ImportFailure as error:
            self.issue(str(error), path, row_number, row)
            self.fatal.append(str(error))

    def session(self, path: Path, number: int) -> dict:
        folder = self.relative(path.parent)
        group = self.groups_by_folder.get(folder)
        if group is None:
            raise ImportFailure("missing_session_group")
        if number == 5 and not self.mapping.get("monthlyImportEnabled", False):
            raise ImportFailure("monthly_history_held")
        key = (group["id"], number)
        if key not in self.sessions_by_pair:
            dates = self.mapping.get("lessonDates", {}).get(folder, {})
            actual = dates.get(str(number))
            starts_at = source_date(actual) if actual else UNKNOWN_DATE
            if not actual and not self.mapping.get("allowUnknownLessonDates", False):
                raise ImportFailure("missing_lesson_date")
            value = {"id": stable_id("session", f'{group["id"]}:{number}'),
                     "groupId": group["id"], "number": number, "startsAt": starts_at,
                     "startsAtKnown": actual is not None, "kind": "counted", "extraPrice": 0,
                     "status": "closed", "createdAt": starts_at,
                     "importRoster": sorted(self.monthly_rosters.get(folder, set()) if number == 5
                                            else self.master_rosters.get(group["id"], set()))}
            self.state["sessions"].append(value)
            self.sessions_by_pair[key] = value
        return self.sessions_by_pair[key]

    def import_monthly_profiles(self, path: Path, sheet: str, headers, rows) -> None:
        """Only new full IDs become profiles; current master data is never replaced."""
        group = self.groups_by_folder.get(self.relative(path.parent))
        if group is None:
            raise ImportFailure("monthly_group_missing")
        id_header = "الرقم التعريفي" if "الرقم التعريفي" in headers else "كود الطالب"
        for row_number, values in rows:
            row = row_dict(headers, values)
            raw_id = text(row.get(id_header))
            if not re.fullmatch(r"[0-9]{9}", raw_id):
                self.issue("monthly_profile_missing_or_invalid_id", path, row_number, row)
                continue  # Kept in the separate unresolved source-row report.
            if raw_id in self.students_by_barcode:
                source_note = text(row.get("درجات"))
                if source_note:
                    label = ("ملاحظة المصدر — الحصة الأولى للشهر الثاني: "
                             if self.mapping.get("monthlyImportEnabled", False)
                             else "ملاحظة مصدر الشهر الثاني — الحصة لم تُعتمد بعد: ")
                    self.students_by_barcode[raw_id]["notes"] += "\n" + label + source_note
                    self.counts["monthly_source_notes_in_app"] += 1
                continue
            name = normalized(row.get("اسم الطالب"))
            if not name:
                self.issue("monthly_new_identity_without_name", path, row_number, row)
                continue
            student = {"id": stable_id("student", raw_id), "barcode": raw_id,
                       "code": raw_id[-5:], "name": name,
                       "phone": text(row.get("رقم الطالب")),
                       "guardianPhone": text(row.get("رقم ولي الأمر", row.get("رقم ولي الامر"))),
                       "groupIds": [group["id"]], "discountPercent": 0,
                       "notes": "بيانات مستوردة من قائمة الشهر الثاني؛ تاريخ التسجيل الأصلي غير معلوم.",
                       "createdAt": self.generated_at, "createdAtKnown": False}
            self.students_by_barcode[raw_id] = student
            source_note = text(row.get("درجات"))
            if source_note:
                label = ("ملاحظة المصدر — الحصة الأولى للشهر الثاني: "
                         if self.mapping.get("monthlyImportEnabled", False)
                         else "ملاحظة مصدر الشهر الثاني — الحصة لم تُعتمد بعد: ")
                student["notes"] += "\n" + label + source_note
                self.counts["monthly_source_notes_in_app"] += 1
            self.state["students"].append(student)
            self.state["enrollments"][f'{student["id"]}:{group["id"]}'] = self.generated_at
            self.counts["new_monthly_profiles"] += 1
            self.provenance.append({"entity": "student", "id": student["id"],
                                    "file": self.relative(path), "sheet": sheet, "row": row_number,
                                    "originalRegistrationDateKnown": False})

    def prepare_cairo(self, path: Path, sheet: str, headers, rows) -> None:
        """Prepare source-row identities without substituting serials for card codes."""
        if sheet != "الطلاب" or "اسم الطالب" not in headers:
            return
        seen = set()
        for row_number, values in rows:
            row = row_dict(headers, values)
            name = normalized(row.get("اسم الطالب"))
            center = normalized(row.get("السنتر")) if "السنتر" in row else "ألفا"
            identity = (name, normalized(row.get("رقم الطالب")), normalized(row.get("رقم ولي الأمر")))
            if not name:
                self.issue("cairo_missing_name", path, row_number, row)
                continue
            if identity in seen:
                self.issue("cairo_ambiguous_duplicate_identity", path, row_number, row)
                continue
            seen.add(identity)
            if center not in ("ألفا", "Gec", "الراعي", "جابر الأنصاري"):
                self.issue("cairo_missing_or_ambiguous_center", path, row_number, row)
                original_center = center
                center = (f"سنتر متعدد — {center} — يحتاج مراجعة" if center
                          else "سنتر غير محدد — يحتاج مراجعة")
            else:
                original_center = center
            candidate = {"sourceIdentity": stable_id("cairo-student", f'{self.relative(path)}:{sheet}:{row_number}'),
                         "file": self.relative(path), "sheet": sheet, "row": row_number,
                         "name": name, "phone": text(row.get("رقم الطالب")),
                         "guardianPhone": text(row.get("رقم ولي الأمر")), "center": center,
                         "originalCenter": original_center,
                         "code": None, "barcode": None, "raw": row}
            self.cairo_candidates.append(candidate)
            self.counts["cairo_source_profiles"] += 1

    def emit_cairo(self) -> None:
        """Only run after an explicit starting code and grade have been approved."""
        start = self.mapping.get("cairoCodeStart")
        if start is None:
            return
        if type(start) is not int or start < 0:
            raise ImportFailure("invalid_cairo_start_code")
        grade = text(self.mapping.get("cairoGrade"))
        if not grade:
            raise ImportFailure("explicit_cairo_grade_required")
        existing_codes = {student["code"] for student in self.state["students"]}
        existing_codes.update(student["barcode"] for student in self.state["students"] if student.get("barcode"))
        # The larger, corrected workbook keeps its serials before Alpha's repeats.
        candidates = sorted(self.cairo_candidates,
                            key=lambda candidate: ("باقي" not in candidate["file"], candidate["file"], candidate["row"]))
        source_codes = {text(candidate["raw"].get(self.mapping.get("cairoCodeHeader", "م")))
                        for candidate in candidates}
        next_code = start
        for candidate in candidates:
            source_code = text(candidate["raw"].get(self.mapping.get("cairoCodeHeader", "م")))
            preserve = bool(re.fullmatch(r"[0-9]+", source_code)) and source_code not in existing_codes
            if preserve:
                code = source_code
            else:
                while str(next_code) in existing_codes or str(next_code) in source_codes:
                    next_code += 1
                code = str(next_code)
                next_code += 1
            existing_codes.add(code)
            path = self.source / candidate["file"]
            # Different centers in a shared workbook remain different groups.
            virtual = path.parent / candidate["center"] / path.name
            group = self.group_for(virtual, {"المجموعة": f'القاهرة — {candidate["center"]}',
                "المستوى": grade, "المركز": f'القاهرة — {candidate["center"]}'})
            self.master_rosters[group["id"]] = {
                item["sourceIdentity"] for item in candidates if item["center"] == candidate["center"]}
            review = text(candidate["raw"].get("المراجعة الحالية"))
            student = {"id": candidate["sourceIdentity"], "code": code, "barcode": "",
                       "name": candidate["name"], "phone": candidate["phone"],
                       "guardianPhone": candidate["guardianPhone"], "groupIds": [group["id"]],
                       "discountPercent": 0, "createdAt": self.generated_at, "createdAtKnown": False,
                       "notes": ("تم اعتماد المسلسل الأصلي ككود؛ لا يوجد باركود أصلي في الملف." if preserve
                                 else "كود جديد مُخصص عند الاستيراد؛ المسلسل الأصلي: " + (source_code or "غير موجود") +
                                      ". لا يوجد باركود أصلي في الملف.") +
                                ("\nالسنتر الأصلي يحتاج مراجعة: " + (candidate["originalCenter"] or "غير محدد")
                                 if candidate["center"] != candidate["originalCenter"] else "") +
                                ("\nمراجعة المصدر: " + review if review else "")}
            self.state["students"].append(student)
            self.state["enrollments"][f'{student["id"]}:{group["id"]}'] = self.generated_at
            candidate["code"] = code
            self.provenance.append({"entity": "student", "id": student["id"],
                                    "file": candidate["file"], "sheet": candidate["sheet"],
                                    "row": candidate["row"], "originalSerial": source_code or None,
                                    "codeAllocated": not preserve, "originalCodePreserved": preserve,
                                    "originalRegistrationDateKnown": False})
            is_alpha = candidate["center"] == "ألفا"
            attendance_headers = {
                "حضور الحصة الأولى": 1, "حضور الحصة الثانية": 2,
                "الحصه التالته" if is_alpha else "حضور الحصة الثالثة": 3,
            }
            if not is_alpha:
                attendance_headers["الحصه الرابعه"] = 4
            # Define all known lesson numbers without fabricating a date or roster absence.
            for lesson_number in range(1, 4 if is_alpha else 5):
                self.session(virtual, lesson_number)
            for header, lesson_number in attendance_headers.items():
                value = text(candidate["raw"].get(header))
                if not value:
                    continue
                status = {"حاضر": "present", "أول حصة": "present", "غائب": "absent"}.get(value)
                if status is None:
                    self.issue("cairo_attendance_annotation", path, candidate["row"],
                               {"column": header, "value": value})
                    student["notes"] += f"\nملاحظة المصدر — الحصة {lesson_number} — {header}: {value}. يحتاج مراجعة؛ لم يُسجل حضور أو غياب."
                    continue
                session = self.session(virtual, lesson_number)
                pair = (student["id"], session["id"])
                self.attendance_pairs.add(pair)
                record = {"id": stable_id("attendance", ":".join(pair)), "studentId": student["id"],
                          "sessionId": session["id"], "status": status, "recordedAt": session["startsAt"],
                          "packageId": None, "originalAttendanceId": None, "importSource": candidate["file"]}
                self.state["attendances"].append(record)
                self.provenance.append({"entity": "attendance", "id": record["id"],
                                        "file": candidate["file"], "sheet": candidate["sheet"],
                                        "row": candidate["row"], "column": header})
            grade_headers = {"الامتحان الأول / 10": (2, "الامتحان الأول / 10"),
                             "الامتحان التاني" if is_alpha else "الامتحان الثاني / 10":
                                 (3, "الامتحان الثاني / 10")}
            if not is_alpha:
                grade_headers["الامتحان الثالث"] = (4, "الامتحان الثالث / 10")
            for header, (lesson_number, exam_name) in grade_headers.items():
                value = text(candidate["raw"].get(header))
                if not value:
                    continue
                exam_absent = value == "لم يمتحن"
                annotation = ""
                try:
                    score = None if exam_absent else float(value)
                    if score is not None:
                        if not math.isfinite(score) or not 0 <= score <= 10:
                            raise ValueError()
                        score = int(score) if score.is_integer() else score
                except ValueError:
                    self.issue("cairo_exam_annotation", path, candidate["row"],
                               {"column": header, "value": value})
                    score = None
                    annotation = "ملاحظة المصدر: " + value + ". تحتاج مراجعة؛ لم تُستنتج درجة أو غياب امتحان."
                session = self.session(virtual, lesson_number)
                activity_id = stable_id("exam", session["id"] + ":" + exam_name)
                if not any(activity["id"] == activity_id for activity in self.state["academicActivities"]):
                    self.state["academicActivities"].append({"id": activity_id, "sessionId": session["id"],
                        "kind": "exam", "name": exam_name, "maxScore": 10, "createdAt": session["startsAt"]})
                pair = (student["id"], activity_id)
                self.academic_keys.add(pair)
                record = {"id": stable_id("academic", ":".join(pair)), "studentId": student["id"],
                          "sessionId": session["id"], "activityId": activity_id, "homework": "notReviewed",
                          "score": score, "maxScore": 10, "examAbsent": exam_absent,
                          "notes": annotation, "updatedAt": session["startsAt"]}
                self.state["academics"].append(record)
                self.provenance.append({"entity": "academic", "id": record["id"],
                                        "file": candidate["file"], "sheet": candidate["sheet"],
                                        "row": candidate["row"], "column": header})
            self.counts["cairo_allocated_profiles"] += 1
            self.counts["cairo_original_codes_preserved" if preserve else "cairo_new_codes_allocated"] += 1

    def import_monthly_grades(self, path: Path, sheet: str, headers, rows) -> None:
        if "درجه الامتحان" not in headers or "كود الطالب" not in headers:
            return
        exam_name = "امتحان الحصة الأولى — الشهر الثاني"
        for row_number, values in rows:
            row = row_dict(headers, values)
            barcode = text(row.get("كود الطالب"))
            if not re.fullmatch(r"[0-9]{9}", barcode):
                continue
            student = self.students_by_barcode.get(barcode)
            if student is None:
                self.issue("monthly_exam_unknown_identity", path, row_number, row)
                continue
            raw = text(row.get("درجه الامتحان"))
            if not raw:
                continue
            try:
                score = float(raw)
                if not math.isfinite(score) or not 0 <= score <= 10:
                    raise ValueError()
                score = int(score) if score.is_integer() else score
            except ValueError:
                self.issue("monthly_exam_non_numeric_or_out_of_range", path, row_number, row)
                continue
            session = self.session(path, 5)
            activity_id = stable_id("exam", session["id"] + ":" + exam_name)
            if not any(activity["id"] == activity_id for activity in self.state["academicActivities"]):
                self.state["academicActivities"].append({"id": activity_id, "sessionId": session["id"],
                    "kind": "exam", "name": exam_name, "maxScore": 10,
                    "maxScoreKnown": False, "createdAt": session["startsAt"]})
            pair = (student["id"], activity_id)
            if pair in self.academic_keys:
                self.issue("duplicate_monthly_exam_record", path, row_number, row)
                continue
            self.academic_keys.add(pair)
            record = {"id": stable_id("academic", ":".join(pair)), "studentId": student["id"],
                      "sessionId": session["id"], "activityId": activity_id, "homework": "notReviewed",
                      "score": score, "maxScore": 10, "maxScoreKnown": False,
                      "examAbsent": False, "notes": "",
                      "updatedAt": session["startsAt"]}
            self.state["academics"].append(record)
            self.counts["monthly_exam_values"] += 1
            self.provenance.append({"entity": "academic", "id": record["id"],
                                    "file": self.relative(path), "sheet": sheet, "row": row_number,
                                    "column": "درجه الامتحان", "maxScoreBasis": "unknown in source; maxScoreKnown false, internal maxScore 10 is a placeholder"})

    def import_monthly_attendance(self, path: Path, sheet: str, headers, rows) -> None:
        if not self.mapping.get("monthlyNonemptyMarksPresent", False):
            return
        id_header = "الرقم التعريفي" if "الرقم التعريفي" in headers else "كود الطالب"
        attendance_header = "حضور الحصه" if "حضور الحصه" in headers else "الدفع بتاعه"
        if attendance_header not in headers:
            return
        for row_number, values in rows:
            row = row_dict(headers, values)
            stamp = text(row.get(attendance_header))
            barcode = text(row.get(id_header))
            student = self.students_by_barcode.get(barcode) if re.fullmatch(r"[0-9]{9}", barcode) else None
            if not stamp:
                continue  # A blank field does not prove absence.
            if student is None:
                self.issue("monthly_attendance_invalid_or_unknown_identity", path, row_number, row)
                continue
            session = self.session(path, 5)
            pair = (student["id"], session["id"])
            if pair in self.attendance_pairs:
                self.issue("duplicate_monthly_attendance_pair", path, row_number, row)
                continue
            self.attendance_pairs.add(pair)
            record = {"id": stable_id("attendance", ":".join(pair)), "studentId": student["id"],
                      "sessionId": session["id"], "status": "present", "recordedAt": session["startsAt"],
                      "packageId": None, "originalAttendanceId": None, "importSource": self.relative(path)}
            self.state["attendances"].append(record)
            self.counts["monthly_present_attendance"] += 1
            self.provenance.append({"entity": "attendance", "id": record["id"],
                                    "file": self.relative(path), "sheet": sheet, "row": row_number,
                                    "column": attendance_header, "sourceStamp": stamp,
                                    "financialInterpretation": "none"})

    def import_csv(self, path: Path) -> None:
        with path.open(encoding="utf-8-sig", newline="") as stream:
            reader = csv.DictReader(stream)
            required = {"Id", "الاسم", "حضور", "تعويض"}
            if not required.issubset(reader.fieldnames or []):
                raise ImportFailure("unrecognized_attendance_csv_schema")
            number = attendance_number(path.name)
            rows = list(reader)
            if number == 5 and not self.mapping.get("monthlyImportEnabled", False):
                for row_number, row in enumerate(rows, 2):
                    self.issue("monthly_held_attendance_row", path, row_number, row)
                return
            if number == 5:
                self.monthly_rosters[self.relative(path.parent)] = {
                    self.students_by_barcode[text(row["Id"])]["id"] for row in rows
                    if text(row["Id"]) in self.students_by_barcode}
            for row_number, row in enumerate(rows, 2):
                self.counts["attendance_source_rows"] += 1
                try:
                    barcode = identifier(row["Id"])
                    student = self.students_by_barcode.get(barcode)
                    if student is None:
                        raise ImportFailure("attendance_unknown_identity")
                    if number is None or (number == 5 and not self.mapping.get("fourLessonsPerMonth", False)):
                        raise ImportFailure("unconfirmed_lesson_number")
                    present, makeup = boolean(row["حضور"]), boolean(row["تعويض"])
                    if present is None or makeup is None:
                        raise ImportFailure("unknown_attendance_status")
                    if makeup:
                        raise ImportFailure("makeup_without_original_paid_absence")
                    session = self.session(path, number)
                    key = (student["id"], session["id"])
                    if key in self.attendance_pairs:
                        raise ImportFailure("duplicate_attendance_pair")
                    self.attendance_pairs.add(key)
                    record = {"id": stable_id("attendance", ":".join(key)),
                              "studentId": student["id"], "sessionId": session["id"],
                              "status": "present" if present else "absent", "recordedAt": session["startsAt"],
                              "packageId": None, "originalAttendanceId": None,
                              "importSource": self.relative(path)}
                    self.state["attendances"].append(record)
                    self.provenance.append({"entity": "attendance", "id": record["id"],
                                            "file": self.relative(path), "row": row_number})
                except ImportFailure as error:
                    self.issue(str(error), path, row_number, row)

    def import_grades(self, path: Path, sheet: str, headers, rows) -> None:
        mapping = self.mapping.get("examSessionNumbers", {})
        for row_number, source_row in rows:
            row = row_dict(headers, source_row)
            self.counts["grade_source_rows"] += 1
            key = (self.relative(path.parent), normalized(row.get("اسم الطالب")),
                   normalized(row.get("رقم الطالب")), normalized(row.get("رقم ولي الأمر")))
            identities = list(dict.fromkeys(self.identity_by_profile.get(key, [])))
            for header in EXAM_HEADERS:
                raw = text(row.get(header))
                if not raw:
                    continue
                self.counts["exam_source_values"] += 1
                try:
                    if len(identities) != 1:
                        for barcode in identities:
                            student = self.students_by_barcode[barcode]
                            student["notes"] += "\nتوجد درجة قديمة معلقة تحتاج مطابقة الكارت؛ لم تُنسب لهذا الطالب."
                            self.counts["ambiguous_grade_students_noted"] += 1
                        raise ImportFailure("ambiguous_exam_identity" if identities else "unmatched_exam_identity")
                    number = mapping.get(header)
                    if type(number) is not int or number <= 0:
                        raise ImportFailure("unconfirmed_exam_session_mapping")
                    score = float(raw)
                    if not math.isfinite(score) or not 0 <= score <= 10:
                        raise ImportFailure("invalid_exam_score")
                    score = int(score) if score.is_integer() else score
                    session = self.session(path, number)
                    activity_id = stable_id("exam", session["id"] + ":" + header)
                    if not any(activity["id"] == activity_id for activity in self.state["academicActivities"]):
                        self.state["academicActivities"].append({"id": activity_id,
                            "sessionId": session["id"], "kind": "exam", "name": header,
                            "maxScore": 10, "createdAt": session["startsAt"]})
                    student_id = self.students_by_barcode[identities[0]]["id"]
                    pair = (student_id, activity_id)
                    if pair in self.academic_keys:
                        raise ImportFailure("duplicate_exam_record")
                    self.academic_keys.add(pair)
                    value = {"id": stable_id("academic", ":".join(pair)), "studentId": student_id,
                             "sessionId": session["id"], "activityId": activity_id, "homework": "notReviewed",
                             "score": score, "maxScore": 10, "examAbsent": False, "notes": "",
                             "updatedAt": session["startsAt"]}
                    self.state["academics"].append(value)
                    self.provenance.append({"entity": "academic", "id": value["id"],
                                            "file": self.relative(path), "sheet": sheet,
                                            "row": row_number, "column": header})
                except (ImportFailure, ValueError) as error:
                    kind = str(error) if isinstance(error, ImportFailure) else "non_numeric_exam_score"
                    self.issue(kind, path, row_number, row, column=header)

    def prepare_display_labels(self) -> None:
        labels = self.mapping.get("catalogLabels", {})
        canonical_ids, remapped = {}, {}
        for entry in self.state["catalogs"]:
            original = entry["name"]
            entry["name"] = normalized(labels.get(entry["kind"], {}).get(original, original))
            if not entry["name"]:
                raise ImportFailure("empty_catalog_display_label")
            key = (entry["kind"], entry["name"])
            remapped[entry["id"]] = canonical_ids.setdefault(key, entry["id"])
        self.state["catalogs"] = [entry for entry in self.state["catalogs"]
                                  if remapped[entry["id"]] == entry["id"]]
        for group in self.state["groups"]:
            for field in ("subjectId", "centerId", "gradeId"):
                group[field] = remapped[group[field]]
            display = self.mapping.get("groupLabels", {}).get(group["name"], {})
            for field in ("name", "schedule"):
                if field in display:
                    group[field] = normalized(display[field])
                    if not group[field]:
                        raise ImportFailure("empty_group_display_label")

    def prepare_initial_history(self) -> dict:
        mode = self.mapping.get("initialHistory", "import")
        if mode not in ("import", "empty"):
            raise ImportFailure("invalid_initial_history_mode")
        if mode == "import":
            return {}
        # The owner requested a fresh first lesson while retaining every identity.
        removed = {key: len(value) for key, value in self.state.items()
                   if isinstance(value, list) and key not in ("catalogs", "groups", "students")}
        for key in removed:
            self.state[key] = []
        for student in self.state["students"]:
            student["notes"] = self.profile_only_notes(student["notes"])
        return removed

    @staticmethod
    def profile_only_notes(notes: str) -> str:
        history_prefixes = ("ملاحظة المصدر — الحصة", "ملاحظة مصدر الشهر الثاني",
                            "توجد درجة قديمة معلقة")
        return "\n".join(line for line in notes.splitlines()
                         if not line.startswith(history_prefixes)).strip().replace(
            "بيانات مستوردة من قائمة الشهر الثاني؛", "بيانات مستوردة من قائمة الطلاب؛")

    def run(self) -> tuple[dict, dict]:
        files = sorted(path for path in self.source.rglob("*") if path.is_file() and path.name != ".DS_Store")
        tables = []
        for path in files:
            if path.suffix.lower() != ".csv":
                continue
            with path.open(encoding="utf-8-sig", newline="") as stream:
                for row_number, row in enumerate(csv.DictReader(stream), 2):
                    if explicit_discount_percent(row.get("Discount Money")) == 25:
                        barcode = text(row.get("Id"))
                        if re.fullmatch(r"[0-9]{5,}", barcode):
                            self.discount_corroborations[barcode].append({"file": self.relative(path),
                                "row": row_number, "value": text(row["Discount Money"])})
        for path in files:
            self.inventory.append({"file": self.relative(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                                   "bytes": path.stat().st_size})
            if path.suffix.lower() == ".xlsx":
                for sheet, headers, rows in workbook_tables(path):
                    tables.append((path, sheet, headers, rows))
                    if set(PROFILE_HEADERS).issubset(headers):
                        for row_number, source_row in rows:
                            self.add_profile(path, sheet, row_number, row_dict(headers, source_row))
        for student in self.state["students"]:
            for group_id in student["groupIds"]:
                self.master_rosters.setdefault(group_id, set()).add(student["id"])
        for path, sheet, headers, rows in tables:
            if "الشهر" in path.name and ("الرقم التعريفي" in headers or "كود الطالب" in headers):
                self.import_monthly_profiles(path, sheet, headers, rows)
                id_header = "الرقم التعريفي" if "الرقم التعريفي" in headers else "كود الطالب"
                self.monthly_rosters[self.relative(path.parent)] = {
                    self.students_by_barcode[text(row_dict(headers, values).get(id_header))]["id"]
                    for _, values in rows
                    if text(row_dict(headers, values).get(id_header)) in self.students_by_barcode}
                if self.mapping.get("monthlyImportEnabled", False):
                    self.import_monthly_grades(path, sheet, headers, rows)
                    self.import_monthly_attendance(path, sheet, headers, rows)
                else:
                    self.counts["monthly_held_workbook_rows"] += len(rows)
            if "مجاميع القاهره" in self.relative(path):
                self.prepare_cairo(path, sheet, headers, rows)
        codes = collections.defaultdict(list)
        for student in self.state["students"]:
            codes[student["code"]].append(student["id"])
        for values in codes.values():
            if len(values) > 1:
                self.fatal.append("last_five_code_collision")
                self.counts["last_five_code_collision"] += 1
        for path in files:
            if path.suffix.lower() == ".csv":
                self.import_csv(path)
            elif path.suffix.lower() == ".docx":
                with zipfile.ZipFile(path) as archive:
                    tree = ET.fromstring(archive.read("word/document.xml"))
                    paragraphs = ["".join(element.itertext()) for element in tree.iter()
                                  if element.tag.endswith("}p")]
                self.issue("source_instructions_retained", path, values={"paragraphs": paragraphs})
        self.emit_cairo()
        for path, sheet, headers, rows in tables:
            if set(PROFILE_HEADERS).issubset(headers):
                continue
            if set(EXAM_HEADERS).issubset(headers):
                self.import_grades(path, sheet, headers, rows)
            else:
                for row_number, row in rows:
                    if "الشهر" in path.name:
                        kind = "monthly_source_snapshot_not_overwritten"
                    elif "مجاميع القاهره" in self.relative(path) and sheet == "الطلاب":
                        kind = "cairo_source_row_without_code" if self.mapping.get("cairoCodeStart") is None else "cairo_source_snapshot"
                    else:
                        kind = "unmapped_workbook_row"
                    self.issue(kind, path, row_number, row_dict(headers, row), sheet=sheet)
        self.prepare_display_labels()
        excluded_history = self.prepare_initial_history()
        source_fingerprint = hashlib.sha256(json.dumps(self.inventory, ensure_ascii=False, sort_keys=True).encode()).hexdigest()
        seed_id = "owner-export-" + hashlib.sha256((source_fingerprint + json.dumps(self.mapping, ensure_ascii=False, sort_keys=True)).encode()).hexdigest()[:24]
        self.state["installationSeedId"] = seed_id
        manifest = {"seedId": seed_id, "generatedAt": self.generated_at,
                    "sourceFingerprint": source_fingerprint, "sources": self.inventory,
                    "sourceCounts": dict(self.counts), "importedCounts": {
                        key: len(value) for key, value in self.state.items() if isinstance(value, list)},
                    "unknowns": ["lesson dates explicitly unknown unless supplied in mapping",
                                 "unconfigured group prices are not free lesson prices",
                                 "no payments, packages, card receipt, homework or exam absence inferred",
                                 "unmapped identities and ambiguous grades kept in private report"],
                    "fatalIssueCounts": dict(collections.Counter(self.fatal)), "mapping": self.mapping,
                    "discountResolution": {"explicitFractionStudents": self.counts["explicit_fraction_discount_resolved"],
                        "corroboratedQuarterStudents": self.counts["quarter_discount_corroborated"],
                        "needsReviewStudents": sum(bool(student.get("discountNeedsReview")) for student in self.state["students"])},
                    "rosterBasis": "master home-group profiles for lessons 1–4; exact monthly worksheet/CSV IDs for lesson 5; Cairo source-center rows",
                    "cairoGrade": ("owner confirmed: " + self.mapping["cairoGrade"]
                                   if self.mapping.get("cairoGradeConfirmed", False)
                                   else "not provided; explicit unknown-grade catalog"),
                    "monthlyExamMaxScoreBasis": "unknown in source; maxScoreKnown false, internal maxScore 10 is a placeholder; no percentage inferred"}
        manifest["monthlyHistory"] = {
            "enabled": self.mapping.get("monthlyImportEnabled", False) and not excluded_history,
            "profilesRetained": True,
            "basis": ("explicitly enabled in reviewed mapping" if self.mapping.get("monthlyImportEnabled", False)
                      else "withheld pending source confirmation; attendance, exams and fifth sessions excluded; source snapshots retained privately")}
        manifest["initialHistory"] = {
            "mode": self.mapping.get("initialHistory", "import"),
            "excludedCounts": excluded_history,
            "basis": ("owner requested profiles only; no previous sessions, attendance, payments or academic records; next lesson number is 1"
                      if excluded_history else "source historical records included")}
        if excluded_history:
            manifest["monthlyHistory"]["basis"] = "profiles retained; operational history cleared at owner's request"
            manifest["rosterBasis"] = "student group membership retained; no historical sessions or rosters installed"
        manifest["academicBreakdown"] = {
            "graded": sum(record["score"] is not None for record in self.state["academics"]),
            "explicitNotTaken": sum(record["examAbsent"] for record in self.state["academics"]),
            "notesOnly": sum(record["score"] is None and not record["examAbsent"] and bool(record["notes"])
                             for record in self.state["academics"]),
            "fractionalScores": sum(isinstance(record["score"], float) for record in self.state["academics"])}
        manifest["attendanceBreakdown"] = dict(collections.Counter(
            record["status"] for record in self.state["attendances"]))
        private = {"manifest": manifest, "provenance": self.provenance,
                   "cairoCandidates": self.cairo_candidates, "unresolved": self.unresolved}
        return {"version": 1, "state": self.state, "manifest": manifest}, private


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--mapping", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=Path("assets/installation_seed.json"))
    parser.add_argument("--private-report", type=Path, default=Path("private-import/import-summary.json"))
    args = parser.parse_args()
    try:
        source = args.source.resolve(strict=True)
        if not source.is_dir():
            raise ImportFailure("source_not_directory")
        for destination in (args.output, args.private_report):
            if destination.suffix.lower() != ".json":
                raise ImportFailure("json_output_required")
            if destination.resolve().is_relative_to(source):
                raise ImportFailure("output_would_modify_source_tree")
            if destination.exists() and destination.is_symlink():
                raise ImportFailure("output_symlink_rejected")
        if args.output.resolve() == args.private_report.resolve():
            raise ImportFailure("output_and_report_must_differ")
        mapping = json.loads(args.mapping.read_text(encoding="utf-8"))
        if not isinstance(mapping, dict) or not text(mapping.get("subject")):
            raise ImportFailure("explicit_subject_required")
        importer = SeedImporter(source, mapping)
        seed, private = importer.run()
        atomic_private_json(args.private_report, private)
        if importer.fatal:
            print(json.dumps({"generated": False, "fatalIssueCounts": dict(collections.Counter(importer.fatal))}))
            return 2
        atomic_private_json(args.output, seed)
        print(json.dumps({"generated": True, "importedCounts": seed["manifest"]["importedCounts"],
                          "issueCounts": dict(importer.counts)}, ensure_ascii=False))
        return 0
    except (ImportFailure, OSError, ValueError, KeyError, zipfile.BadZipFile) as error:
        # Source values / XML errors are not suitable console diagnostics.
        print(json.dumps({"generated": False, "error": str(error) if isinstance(error, ImportFailure) else type(error).__name__}))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
