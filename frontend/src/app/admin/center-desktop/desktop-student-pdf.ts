import type { jsPDF } from 'jspdf';
import type { DesktopStudentSource, DesktopStudent, DesktopStudentLesson, DesktopStudentSearch } from '@/services/center-desktop-service';
import { desktopClock, desktopTimestamp } from './desktop-datetime';
import { attendanceLabel, homeworkLabel, paymentLines, examScore } from './student-report-format';

interface StudentReportInput {
  student: DesktopStudent;
  profile: NonNullable<DesktopStudentSearch['profile']>;
  receipt: DesktopStudentSource;
}

const widths = [44, 33, 43, 60, 52, 37];
const headings = ['الحصة والمجموعة', 'موعد الحصة', 'الحضور ووقت التسجيل', 'الدفع', 'درجة الامتحان', 'الواجب'];
const margin = 14;
const lineHeight = 4.6;
const bottom = 196;

class StudentReport {
  private y = 0;
  private rowNumber = 0;
  constructor(private pdf: jsPDF, private input: StudentReportInput) {}

  private right(text: string, x: number, y: number) {
    this.pdf.text(text, x, y, { align: 'right', isInputRtl: true, isInputVisual: false, isOutputVisual: true, isOutputRtl: false });
  }

  private lines(text: string, width: number): string[] {
    return text.split('\n').flatMap(paragraph => this.pdf.splitTextToSize(paragraph, width) as string[]);
  }

  private newPage() {
    if (this.y) this.pdf.addPage();
    this.pdf.setFont('MassarReport', 'normal');
    this.pdf.setFontSize(16);
    this.pdf.setTextColor('#102a43');
    this.right('مسار · سجل الطالب', 283, 14);
    this.pdf.setFontSize(10);
    let baseline = 21;
    for (const line of this.lines(`${this.input.student.name} · الكود: ${this.input.student.code}`, 269)) {
      this.right(line, 283, baseline);
      baseline += lineHeight;
    }
    this.pdf.setFontSize(9);
    this.y = baseline + 3;
  }

  private paragraph(text: string) {
    for (const line of this.lines(text, 269)) {
      if (this.y + 6 > bottom) this.newPage();
      this.right(line, 283, this.y);
      this.y += 5.5;
    }
  }

  private tableHeader() {
    if (this.y + 27 > bottom) this.newPage();
    this.pdf.setFillColor('#eaf0f4');
    this.pdf.rect(margin, this.y, 269, 10, 'F');
    let right = 283;
    headings.forEach((heading, index) => {
      this.right(heading, right - 2, this.y + 6);
      right -= widths[index];
    });
    this.y += 10;
  }

  private cells(lesson: DesktopStudentLesson) {
    const attendance = [attendanceLabel(lesson.attendance?.status)];
    if (lesson.attendance) attendance.push(lesson.attendance.recordedAt ? `وقت التسجيل: ${desktopClock(lesson.attendance.recordedAt)} (القاهرة)` : 'وقت التسجيل غير متوفر');
    if (lesson.attendance?.makeupSourceGroup) attendance.push(`تعويض عن ${lesson.attendance.makeupSourceGroup}`);
    const exams = lesson.exams.map(exam => `${exam.name || 'الامتحان'}: ${examScore(exam)}`);
    return [
      [lesson.monthName || `الشهر ${lesson.month}`, `الحصة ${lesson.number ?? 'غير محددة'}`, lesson.name, lesson.group, lesson.center].filter(Boolean).join('\n'),
      lesson.date ? desktopTimestamp(lesson.date) : 'التاريخ غير متوفر',
      attendance.join('\n'), paymentLines(lesson.payment).join('\n'),
      exams.length ? exams.join('\n') : 'لا يوجد امتحان مسجل', homeworkLabel(lesson.homework),
    ].map((text, index) => this.lines(text, widths[index] - 5));
  }

  private row(lesson: DesktopStudentLesson) {
    const cells = this.cells(lesson);
    const count = Math.max(...cells.map(lines => lines.length));
    const fullHeight = Math.max(16, count * lineHeight + 6);
    if (fullHeight <= bottom - 45 && this.y + fullHeight > bottom) { this.newPage(); this.tableHeader(); }
    let offset = 0;
    while (offset < count) {
      if (this.y + 16 > bottom) { this.newPage(); this.tableHeader(); }
      const available = Math.floor((bottom - this.y - 6) / lineHeight);
      const take = Math.min(available, count - offset);
      const height = Math.max(16, take * lineHeight + 6);
      const fragment = cells.map(lines => lines.slice(offset, offset + take));
      if (offset > 0 && !fragment[0].length) fragment[0] = [`تابع الحصة ${lesson.number ?? 'غير محددة'}`, lesson.monthName || `الشهر ${lesson.month}`];
      this.drawCells(fragment, height);
      offset += take;
    }
    this.rowNumber++;
  }

  private drawCells(cells: string[][], height: number) {
    this.pdf.setFillColor(this.rowNumber % 2 === 0 ? '#ffffff' : '#f6f8fa');
    this.pdf.rect(margin, this.y, 269, height, 'F');
    this.pdf.setDrawColor('#dbe3e9');
    this.pdf.line(margin, this.y + height, 283, this.y + height);
    let right = 283;
    cells.forEach((lines, index) => {
      lines.forEach((line, lineIndex) => this.right(line, right - 2, this.y + 5 + lineIndex * lineHeight));
      right -= widths[index];
    });
    this.y += height;
  }

  build() {
    this.newPage();
    const { student, profile, receipt } = this.input;
    this.paragraph(`موبايل الطالب: ${student.phone || 'غير مسجل'} · رقم ولي الأمر: ${student.guardianPhone || 'غير مسجل'}`);
    this.paragraph(`السنتر: ${student.centers?.join('، ') || 'غير متوفر'} · المجموعة: ${student.groups.join('، ') || 'غير متوفرة'}`);
    this.paragraph(`آخر تحديث للبيانات: ${desktopTimestamp(receipt.createdAt)} · التقرير يشمل كل الشهور والمجموعات في هذه النسخة.`);
    this.paragraph(`حضور وتعويض: ${profile.present} · غياب مسجل: ${profile.absent} · الخصم الثابت: ${student.discountPercent ?? 0}٪`);
    if (student.notes) this.paragraph(`ملاحظات الطالب: ${student.notes}`);
    this.y += 3;
    this.pdf.setFontSize(9);
    this.tableHeader();
    const lessons = [...profile.lessons].sort((a, b) => a.month - b.month || (a.number ?? Infinity) - (b.number ?? Infinity) || a.group.localeCompare(b.group));
    if (!lessons.length) this.paragraph('لا توجد حصص مسجلة في هذه النسخة.');
    lessons.forEach(lesson => this.row(lesson));
    for (let page = 1; page <= this.pdf.getNumberOfPages(); page++) {
      this.pdf.setPage(page);
      this.pdf.setFontSize(8);
      this.right(`صفحة ${page} من ${this.pdf.getNumberOfPages()} · بيانات النسخة المرفوعة، عدم وجود سجل لا يعني الغياب.`, 283, 204);
    }
    return this.pdf.output('blob');
  }
}

export async function createStudentPdf(input: StudentReportInput) {
  const [{ jsPDF }, fontResponse] = await Promise.all([import('jspdf'), fetch('/fonts/MassarReportArabic.ttf', { cache: 'force-cache' })]);
  if (!fontResponse.ok) throw new Error('تعذر تحميل خط التقرير العربي.');
  const font = new Uint8Array(await fontResponse.arrayBuffer());
  if (font.length < 100 || new DataView(font.buffer).getUint32(0) !== 0x00010000) throw new Error('ملف خط التقرير غير صالح.');
  const binary = Array.from(font, byte => String.fromCharCode(byte)).join('');
  const pdf = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4', compress: true, putOnlyUsedFonts: true });
  // Reuse the finance reports' font: its cmap includes the Arabic presentation
  // forms emitted by jsPDF, including isolated letters that Tajawal omits.
  pdf.addFileToVFS('MassarReportArabic.ttf', btoa(binary));
  pdf.addFont('MassarReportArabic.ttf', 'MassarReport', 'normal');
  pdf.setProperties({ title: `سجل الطالب ${input.student.name}`, author: 'مسار' });
  return new StudentReport(pdf, input).build();
}
