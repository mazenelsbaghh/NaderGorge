import csv
import html
import io


def csv_bytes(headers, rows):
    output = io.StringIO(newline="")
    writer = csv.writer(output)
    writer.writerow(headers)
    for row in rows:
        writer.writerow(["'" + str(cell) if str(cell).startswith(("=", "+", "-", "@", "\t", "\r"))
                         else cell for cell in row])
    return output.getvalue().encode("utf-8-sig")


def students_csv(dashboard):
    return csv_bytes(["الكود", "الاسم", "الموبايل"],
                     ((attempt["code"], attempt["name"] or "", attempt["phone"] or "")
                      for attempt in dashboard["attempts"]))


def results_csv(dashboard):
    return csv_bytes(["الاسم", "الموبايل", "الكود", "الدرجة المصححة", "المجموع", "متبقي للتصحيح", "الحالة"],
                     ((attempt["name"] or "", attempt["phone"] or "", attempt["code"],
                       attempt["score"], attempt["maximum"], attempt["pending"],
                       "تم التسليم" if attempt["submitted_at"] else "لم يسلم")
                      for attempt in dashboard["attempts"] if attempt["joined_at"]))


def render_report(report):
    escape = html.escape
    attempt = report["attempt"]
    sections = []
    for index, question in enumerate(report["questions"], 1):
        answer = attempt["answers"].get(question["id"])
        model = question.get("modelAnswer", "")
        if question["kind"] == "mcq":
            answer = question["options"][answer] if answer is not None else "لم تُكتب إجابة"
            model = question["options"][question["correct"]]
        grade = attempt["grades"][question["id"]]
        sections.append(f"""<section class="question-report"><h2>{index}. {escape(question['text'])}</h2>
          <p class="score">{grade['score']} / {question['points']}</p>
          <h3>إجابة الطالب</h3><p class="answer">{escape(answer or 'لم تُكتب إجابة')}</p>
          <h3>الإجابة النموذجية</h3><p class="answer">{escape(model)}</p>
          <p>{escape(grade['feedback'])}</p></section>""")
    return f"""<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8">
      <meta name="viewport" content="width=device-width, initial-scale=1">
      <title>تقرير {escape(attempt['name'])}</title><link rel="stylesheet" href="/assets/report.css">
      <script type="module" src="/assets/report.js"></script></head><body>
      <div class="print-tools"><a href="/">الرجوع للإدارة ←</a><button id="print-report">طباعة / حفظ PDF</button>
      <span>اختر «حفظ كملف PDF» من نافذة الطباعة.</span></div>
      <header><img src="/assets/logo-mark.svg" alt="مسار"><div><h1>تقرير تصحيح الامتحان</h1>
      <p>{escape(report['exam']['config']['title'])}</p></div></header>
      <div class="identity"><strong>{escape(attempt['name'])}</strong>
      <span dir="ltr">{escape(attempt['phone'])}</span>
      <span>الكود: {escape(attempt['code'])}</span><strong>النتيجة: {attempt['score']} / {attempt['maximum']}</strong></div>
      {''.join(sections)}<footer>مسار · امتحانات السنتر · نسخة الإدارة</footer></body></html>""".encode("utf-8")
