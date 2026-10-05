"""Offline Arabic student PDF renderer, bundled with the desktop application."""
import argparse
import json
from pathlib import Path
import re
import sys
import tempfile
import zipfile

import arabic_reshaper
from bidi.algorithm import get_display
from reportlab.lib.colors import HexColor, white
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen.canvas import Canvas

NAVY = HexColor('#102a43')
TEAL = HexColor('#087c77')
MUTED = HexColor('#607082')
WIDTH, HEIGHT = A4
MARGIN = 40
RESHAPER = arabic_reshaper.ArabicReshaper(configuration={'use_unshaped_instead_of_isolated': True})


def visual(text):
    return get_display(RESHAPER.reshape(str(text)))


def lines(text, width, size=11, font='Tajawal'):
    result = []
    for paragraph in str(text).split('\n'):
        current = ''
        for word in paragraph.split():
            candidate = f'{current} {word}'.strip()
            if pdfmetrics.stringWidth(visual(candidate), font, size) <= width:
                current = candidate
                continue
            if current:
                result.append(current)
                current = ''
            for char in word:
                if current and pdfmetrics.stringWidth(visual(current + char), font, size) > width:
                    result.append(current)
                    current = ''
                current += char
        result.append(current)
    return result or ['']


class Report:
    def __init__(self, data, output):
        self.data = data
        self.attempt = data['attempt']
        self.title = data['exam']['config']['title']
        self.canvas = Canvas(str(output), pagesize=A4, pageCompression=1)
        self.canvas.setTitle(f"تقرير {self.attempt['name']} - {self.title}")
        self.canvas.setAuthor('مسار · امتحانات السنتر')
        self.page = 0
        self.new_page()

    def right(self, text, x, y, size=11, bold=False, color=NAVY):
        self.canvas.setFillColor(color)
        self.canvas.setFont('TajawalBold' if bold else 'Tajawal', size)
        self.canvas.drawRightString(x, y, visual(text))

    def footer(self):
        self.canvas.setStrokeColor(HexColor('#dbe5eb'))
        self.canvas.line(MARGIN, 36, WIDTH-MARGIN, 36)
        self.right('مسار · امتحانات السنتر · نسخة الطالب', WIDTH-MARGIN, 23, 9, color=MUTED)
        self.right(f'صفحة {self.page}', 100, 23, 9, color=MUTED)

    def new_page(self):
        if self.page:
            self.footer()
            self.canvas.showPage()
        self.page += 1
        self.right('مسار', WIDTH-MARGIN, HEIGHT-43, 24, True, TEAL)
        self.right('نتيجة وتصحيح الامتحان', WIDTH-MARGIN-90, HEIGHT-40, 15, True)
        self.y = HEIGHT-65
        for line in lines(self.title, WIDTH-2*MARGIN, 11):
            self.right(line, WIDTH-MARGIN, self.y, 11, color=MUTED)
            self.y -= 16
        self.canvas.setStrokeColor(TEAL)
        self.canvas.line(MARGIN, self.y, WIDTH-MARGIN, self.y)
        self.y -= 23
        if self.page > 1:
            self.right(self.attempt['name'], WIDTH-MARGIN, self.y, 10, True)
            self.y -= 22

    def ensure(self, height):
        if self.y-height < 55:
            self.new_page()

    def text_block(self, label, text, model=False):
        wrapped = lines(text or 'لم يجب الطالب', WIDTH-2*MARGIN-28)
        while wrapped:
            self.ensure(70)
            count = max(1, int((self.y-55-34)/17))
            chunk, wrapped = wrapped[:count], wrapped[count:]
            box_height = 35+len(chunk)*17
            self.canvas.setFillColor(HexColor('#eaf5f3' if model else '#f5f7f9'))
            self.canvas.roundRect(MARGIN, self.y-box_height, WIDTH-2*MARGIN, box_height, 6, fill=1, stroke=0)
            self.right(label, WIDTH-MARGIN-14, self.y-17, 10, True, TEAL if model else MUTED)
            cursor = self.y-35
            for line in chunk:
                self.right(line, WIDTH-MARGIN-14, cursor)
                cursor -= 17
            self.y -= box_height+9
            label = label + ' (تابع)' if not label.endswith('(تابع)') else label

    def build(self):
        a = self.attempt
        for label, value in [('اسم الطالب', a['name']), ('رقم الموبايل', a['phone']),
                             ('كود الطالب', a['code']), ('الدرجة النهائية', f"{a['score']:g} من {a['maximum']:g}"),
                             ('النسبة المئوية', f"{100*a['score']/a['maximum']:.1f}%" if a['maximum'] else '—')]:
            for line in lines(f'{label}: {value}', WIDTH-2*MARGIN, 12, 'TajawalBold'):
                self.right(line, WIDTH-MARGIN, self.y, 12, True)
                self.y -= 21
        self.y -= 12
        for index, question in enumerate(self.data['questions'], 1):
            qid = question['id']
            grade = a['grades'].get(qid, {})
            score, maximum = float(grade.get('score', 0)), float(question['points'])
            status = 'إجابة صحيحة' if score >= maximum else 'إجابة خاطئة' if score == 0 else 'إجابة جزئية'
            color = HexColor('#146440' if score >= maximum else '#a33232' if score == 0 else '#94600b')
            answer = a['answers'].get(qid)
            model = question.get('modelAnswer', '')
            if question['kind'] == 'mcq':
                answer = question['options'][int(answer)] if isinstance(answer, (int, float)) and 0 <= answer < len(question['options']) else None
                model = question['options'][int(question['correct'])]
            question_lines = lines(question['text'], WIDTH-2*MARGIN, 12, 'TajawalBold')
            feedback = re.sub(r'^Gemini:\s*', '', str(grade.get('feedback', '')), flags=re.I).strip()
            block_texts = [answer or 'لم يجب الطالب', model or 'لم يجب الطالب'] + ([feedback] if feedback else [])
            question_height = 36 + len(question_lines)*19 + sum(44+17*len(lines(text, WIDTH-2*MARGIN-28)) for text in block_texts)
            self.ensure(question_height if question_height < 600 else 160)
            self.right(f'السؤال {index} · {status}', WIDTH-MARGIN, self.y, 11, True, color)
            self.canvas.setFillColor(color)
            self.canvas.setFont('TajawalBold', 11)
            self.canvas.drawString(MARGIN, self.y, f'{score:g} / {maximum:g}')
            self.y -= 24
            for line in question_lines:
                self.ensure(23)
                self.right(line, WIDTH-MARGIN, self.y, 12, True)
                self.y -= 19
            self.text_block('إجابة الطالب', answer or 'لم يجب الطالب')
            self.text_block('الإجابة الصحيحة / النموذجية', model, model=True)
            if feedback:
                self.text_block('ملاحظة التصحيح', feedback)
            self.y -= 12
        self.footer()
        self.canvas.save()


def render(data, output):
    base = Path(getattr(sys, '_MEIPASS', Path(__file__).resolve().parent))
    font_dir = base / 'fonts' if hasattr(sys, '_MEIPASS') else base / 'web' / 'assets'
    if 'Tajawal' not in pdfmetrics.getRegisteredFontNames():
        pdfmetrics.registerFont(TTFont('Tajawal', str(font_dir/'Tajawal-Regular.ttf')))
        pdfmetrics.registerFont(TTFont('TajawalBold', str(font_dir/'Tajawal-Bold.ttf')))
    Report(data, output).build()


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--zip', action='store_true')
    parser.add_argument('input', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    reports = json.loads(args.input.read_text())
    if not args.zip:
        render(reports, args.output)
    else:
        used = set()
        with tempfile.TemporaryDirectory(prefix='massar-pdfs-') as directory, zipfile.ZipFile(args.output, 'w', zipfile.ZIP_DEFLATED) as archive:
            for index, report in enumerate(reports, 1):
                attempt = report['attempt']
                name = re.sub(r'[\\/:*?"<>|\x00-\x1f]', '-', attempt['name']).strip(' .')[:100] or 'طالب'
                stem = f"{name} - {attempt['score']:g} من {attempt['maximum']:g}"
                filename = stem + '.pdf'
                if filename.casefold() in used:
                    filename = f"{stem} ({index}).pdf"
                used.add(filename.casefold())
                path = Path(directory) / 'report.pdf'
                render(report, path)
                archive.write(path, filename)

