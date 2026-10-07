import 'notice_dialog.dart';
import 'scrollable_dialog.dart';
import 'dart:io';
import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';
import '../application/center_store.dart';
import '../domain/models.dart';
import 'formatters.dart';

abstract final class DocumentService {
  static Future<pw.ThemeData> _theme() async => pw.ThemeData.withFont(
    base: pw.Font.ttf(
      await rootBundle.load('assets/fonts/Tajawal-Regular.ttf'),
    ),
    bold: pw.Font.ttf(await rootBundle.load('assets/fonts/Tajawal-Bold.ttf')),
  );

  static Future<Uint8List> studentCard(CenterStore store, Student student) =>
      studentCards(store, [student]);

  static Future<Uint8List> studentCards(
    CenterStore store,
    List<Student> students,
  ) async {
    if (students.isEmpty ||
        students.map((student) => student.id).toSet().length !=
            students.length) {
      throw const CenterException('اختار طلبة مختلفين لطباعة الكروت.');
    }
    final document = pw.Document(theme: await _theme());
    for (final student in students) {
      if (!store.students.any((current) => current.id == student.id)) {
        throw const CenterException(
          'طالب من القائمة لم يعد موجودًا. حدّث قائمة الكروت.',
        );
      }
      document.addPage(_studentCardPage(store, student));
    }
    return document.save();
  }

  static pw.Page _studentCardPage(CenterStore store, Student student) =>
      pw.Page(
        pageFormat: const PdfPageFormat(300, 190, marginAll: 16),
        textDirection: pw.TextDirection.rtl,
        build: (_) => pw.Column(
          crossAxisAlignment: pw.CrossAxisAlignment.stretch,
          children: [
            pw.Text(
              'مسار | نادر جورج',
              style: pw.TextStyle(fontWeight: pw.FontWeight.bold, fontSize: 16),
            ),
            pw.SizedBox(height: 12),
            pw.Row(
              mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
              children: [
                pw.Expanded(
                  child: pw.Column(
                    crossAxisAlignment: pw.CrossAxisAlignment.start,
                    children: [
                      pw.Text(
                        student.name,
                        style: pw.TextStyle(
                          fontSize: 18,
                          fontWeight: pw.FontWeight.bold,
                        ),
                      ),
                      pw.SizedBox(height: 6),
                      pw.Text('كود الطالب: ${student.code}'),
                      pw.SizedBox(height: 6),
                      pw.Text(
                        student.groupIds.map(store.groupLabel).join(' • '),
                        style: const pw.TextStyle(fontSize: 9),
                      ),
                    ],
                  ),
                ),
                pw.SizedBox(width: 16),
                pw.BarcodeWidget(
                  barcode: pw.Barcode.qrCode(),
                  data: student.barcode.isEmpty
                      ? student.code
                      : student.barcode,
                  drawText: false,
                  width: 72,
                  height: 72,
                ),
              ],
            ),
            pw.Spacer(),
            pw.Text(
              'احتفظ بالكارت لتسجيل دخولك للحصة',
              style: const pw.TextStyle(fontSize: 10),
            ),
          ],
        ),
      );

  static Future<Uint8List> receipt(
    CenterStore store,
    PaymentRecord payment,
  ) async {
    final student = store.students.firstWhere(
      (student) => student.id == payment.studentId,
    );
    final document = pw.Document(theme: await _theme());
    document.addPage(
      pw.Page(
        pageFormat: PdfPageFormat.a5,
        textDirection: pw.TextDirection.rtl,
        build: (_) => pw.Column(
          crossAxisAlignment: pw.CrossAxisAlignment.stretch,
          children: [
            pw.Text(
              'مسار | نادر جورج',
              style: pw.TextStyle(fontWeight: pw.FontWeight.bold, fontSize: 22),
            ),
            pw.SizedBox(height: 12),
            pw.Text('إيصال تحصيل', style: const pw.TextStyle(fontSize: 18)),
            pw.SizedBox(height: 12),
            pw.Text('رقم الإيصال: ${payment.id}'),
            pw.Text('التاريخ: ${shortDate(payment.createdAt)}'),
            pw.Text('الطالب: ${student.name} • ${student.code}'),
            pw.Text('المجموعة: ${store.groupLabel(payment.groupId)}'),
            pw.Text('البيان: ${payment.description}'),
            pw.SizedBox(height: 18),
            pw.Divider(),
            pw.Text('السعر الأصلي: ${money(payment.baseAmount)}'),
            pw.Text('الخصم: ${percentText(payment.discountPercent)}%'),
            pw.Text(
              'قيمة الخصم: ${money(payment.baseAmount - payment.netAmount)}',
            ),
            pw.SizedBox(height: 8),
            pw.Text(
              'المدفوع عند العملية: ${money(payment.collectedAmount)}',
              style: pw.TextStyle(fontSize: 20, fontWeight: pw.FontWeight.bold),
            ),
            pw.Text('المستحق بعد الخصم: ${money(payment.netAmount)}'),
            pw.Text(
              'المديونية المتبقية: ${money(store.paymentDebtFor(payment.id))}',
            ),
            pw.Text('طريقة الدفع: ${payment.method}'),
            pw.Divider(),
            pw.Text('تم تسجيل الدفعة محليًا على الجهاز.'),
          ],
        ),
      ),
    );
    return document.save();
  }

  static Future<void> showStudentCard(
    BuildContext context,
    CenterStore store,
    Student student,
  ) async {
    final bytes = await studentCard(store, student);
    if (!context.mounted) return;
    await _showDocument(context, bytes, 'card-${student.code}.pdf');
  }

  static Future<void> showStudentCards(
    BuildContext context,
    CenterStore store,
    List<Student> students,
  ) async {
    final bytes = await studentCards(store, students);
    if (!context.mounted) return;
    await _showDocument(context, bytes, 'student-cards-${students.length}.pdf');
  }

  static Future<void> showReceipt(
    BuildContext context,
    CenterStore store,
    PaymentRecord payment,
  ) async {
    final bytes = await receipt(store, payment);
    if (!context.mounted) return;
    await _showDocument(context, bytes, 'receipt-${payment.id}.pdf');
  }

  static Future<void> _showDocument(
    BuildContext context,
    Uint8List bytes,
    String filename,
  ) async {
    final action = await showDialog<String>(
      context: context,
      builder: (context) => ScrollableMassarDialog(
        title: const Text('الكارت أو الإيصال'),
        content: const Text('احفظ نسخة PDF أو أرسل المستند للطابعة.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('إلغاء'),
          ),
          OutlinedButton(
            onPressed: () => Navigator.pop(context, 'save'),
            child: const Text('حفظ PDF'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, 'print'),
            child: const Text('طباعة'),
          ),
        ],
      ),
    );
    if (action == 'print') {
      await Printing.layoutPdf(onLayout: (_) async => bytes, name: filename);
    } else if (action == 'save') {
      final location = await getSaveLocation(
        suggestedName: filename,
        acceptedTypeGroups: const [
          XTypeGroup(label: 'PDF', extensions: ['pdf']),
        ],
      );
      if (location == null) return;
      await File(location.path).writeAsBytes(bytes, flush: true);
      if (context.mounted) {
        await showMassarNotice(
          context,
          'تم حفظ المستند',
          kind: NoticeKind.success,
        );
      }
    }
  }
}
