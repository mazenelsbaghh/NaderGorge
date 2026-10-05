import math
import re
import uuid


class RoomError(Exception):
    def __init__(self, message, status=400):
        super().__init__(message)
        self.status = status


def text_field(payload, key, maximum, minimum=1):
    value = payload.get(key)
    if not isinstance(value, str) or not minimum <= len(value.strip()) <= maximum:
        raise RoomError(f"تحقق من الحقل: {key}")
    return value.strip()


def integer_field(payload, key, minimum, maximum):
    value = payload.get(key)
    if type(value) is not int or not minimum <= value <= maximum:
        raise RoomError(f"قيمة غير صالحة: {key}")
    return value


def boolean_field(payload, key):
    value = payload.get(key)
    if type(value) is not bool:
        raise RoomError(f"اختيار غير صالح: {key}")
    return value


def validate_question(question):
    if not isinstance(question, dict):
        raise RoomError("صيغة السؤال غير صالحة")
    kind = question.get("kind")
    if kind not in ("mcq", "essay"):
        raise RoomError("نوع السؤال غير صالح")
    clean = {"id": uuid.uuid4().hex, "kind": kind,
             "text": text_field(question, "text", 5000),
             "points": integer_field(question, "points", 1, 100)}
    if kind == "essay":
        clean["modelAnswer"] = text_field(question, "modelAnswer", 10000)
        return clean
    options = question.get("options")
    if not isinstance(options, list) or not 2 <= len(options) <= 6:
        raise RoomError("أضف من اختيارين إلى ستة اختيارات")
    clean["options"] = [text_field({"option": option}, "option", 1000) for option in options]
    clean["correct"] = integer_field(question, "correct", 0, len(options) - 1)
    return clean


def validate_exam(payload):
    questions = payload.get("questions")
    if not isinstance(questions, list) or not 1 <= len(questions) <= 100:
        raise RoomError("الامتحان يحتاج من سؤال واحد إلى ١٠٠ سؤال")
    clean_questions = [validate_question(question) for question in questions]
    count = integer_field(payload, "questionCount", 1, len(questions))
    if count < len(questions) and len({q["points"] for q in clean_questions}) != 1:
        raise RoomError("للسحب العشوائي، اجعل درجة كل سؤال متساوية لضمان تساوي المجموع")
    if payload.get("timerMode") not in ("shared", "individual"):
        raise RoomError("اختر نظام الوقت")
    return {"title": text_field(payload, "title", 150),
            "instructions": text_field(payload, "instructions", 2000, 0),
            "minutes": integer_field(payload, "minutes", 1, 240),
            "timerMode": payload["timerMode"],
            "allowLate": boolean_field(payload, "allowLate"),
            "shuffle": boolean_field(payload, "shuffle"),
            "questionCount": count, "questions": clean_questions}


def normalize_code(code):
    if not isinstance(code, str):
        raise RoomError("اكتب كود الدخول")
    code = code.strip().translate(str.maketrans("٠١٢٣٤٥٦٧٨٩", "0123456789")).upper()
    if not 1 <= len(code) <= 40 or not all(character.isalnum() or character in "-_." for character in code):
        raise RoomError("كود الطالب من ١ إلى ٤٠ حرفًا أو رقمًا، بدون مسافات")
    return code


def normalize_phone(phone):
    phone = text_field({"phone": phone}, "phone", 25)
    phone = phone.translate(str.maketrans("٠١٢٣٤٥٦٧٨٩", "0123456789"))
    phone = re.sub(r"[\s()\-]", "", phone)
    if not re.fullmatch(r"\+?[0-9]{10,15}", phone):
        raise RoomError("اكتب رقم موبايل صحيحًا من ١٠ إلى ١٥ رقمًا")
    return phone


def validate_answers(answers, questions):
    if not isinstance(answers, dict) or set(answers) - {q["id"] for q in questions}:
        raise RoomError("الإجابات تحتوي على سؤال غير مخصص لك")
    for question in questions:
        answer = answers.get(question["id"])
        if answer is None:
            continue
        if question["kind"] == "mcq":
            if type(answer) is not int or not 0 <= answer < len(question["options"]):
                raise RoomError("اختيار غير صالح")
        elif not isinstance(answer, str) or len(answer) > 10000:
            raise RoomError("الإجابة المقالية يجب ألا تتجاوز ١٠٠٠٠ حرف")
    return answers


def validate_grade(payload, maximum):
    score = payload.get("score")
    if type(score) not in (int, float) or not math.isfinite(score) or not 0 <= score <= maximum:
        raise RoomError("الدرجة يجب أن تقع بين صفر ودرجة السؤال")
    return {"score": score, "feedback": text_field(payload, "feedback", 2000, 0)}
