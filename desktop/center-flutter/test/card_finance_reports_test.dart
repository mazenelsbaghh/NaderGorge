import 'dart:convert';
import 'dart:io';

import 'package:cryptography/cryptography.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late LessonSession session;
  late Student mina, lara, legacy;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-card-finance-');
    const password = 'card-report-test-pass';
    final salt = List<int>.generate(24, (index) => index + 1);
    final key = await Pbkdf2(
      macAlgorithm: Hmac.sha256(),
      iterations: 120000,
      bits: 256,
    ).deriveKey(secretKey: SecretKey(utf8.encode(password)), nonce: salt);
    final admin = InstallationAdmin(
      id: 'card-report-owner',
      name: 'card-report-owner',
      credential: {
        'algorithm': 'pbkdf2-sha256-120000',
        'salt': base64Encode(salt),
        'hash': base64Encode(await key.extractBytes()),
      },
    );
    store = await CenterStore.open(directory: directory.path);
    await store.ensureInstallationAdmin(admin);
    await store.signIn(admin.name, password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة الكروت',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
        sessionPrice: 10000,
      ),
    );
    group = store.groups.single;
    for (final (code, name, discount) in [
      ('C-1', 'مينا', 25),
      ('C-2', 'لارا', 0),
      ('C-3', 'طالب قديم', 50),
    ]) {
      await store.saveStudent(
        Student(
          code: code,
          name: name,
          groupIds: [group.id],
          discountPercent: discount,
          createdAt: DateTime.now().subtract(const Duration(days: 10)),
        ),
      );
    }
    mina = store.students[0];
    lara = store.students[1];
    legacy = store.students[2];
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: 1,
        startsAt: DateTime.now().subtract(const Duration(hours: 1)),
        createdAt: DateTime.now(),
      ),
    );
    session = store.sessions.single;
    await store.saveCardSettings(
      const CenterCardSettings(price: 3000, requirePaymentBeforeReceipt: false),
    );
  });

  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  test(
    'card-only payment cannot settle the lesson and linked cash appears separately in saved closing',
    () async {
      await store.collectStudentCard(studentId: mina.id, sessionId: session.id);
      await store.collectStudentCard(
        studentId: lara.id,
        sessionId: session.id,
        method: 'إنستاباي',
      );
      await store.receiveStudentCard(mina.id);
      await store.receiveStudentCard(legacy.id);
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.packages, isEmpty);
      expect(
        store.paymentStatusFor(mina.id, session.id).status,
        StudentPaymentStatus.notPaid,
      );
      var summary = store.sessionFinancialSummary(session.id);
      expect(summary.cardPaymentCount, 2);
      expect(summary.cardCollectedAmount, 5250);
      expect(summary.expectedCash, 2250);
      expect(summary.totalCollected, 5250);
      expect(summary.singlePaymentCount, 0);
      expect(summary.packageSalesCount, 0);
      expect(summary.presentCount, 0);
      expect(summary.studentCategories, isEmpty);
      expect(
        summary.lines.where((line) => line.label.startsWith('دفع كارت')),
        hasLength(2),
      );
      await store.collectAndAttend(
        EntryRequest(
          studentId: mina.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 9750);
      summary = store.closings.single.summary;
      expect(summary.expectedCash, 9750);
      expect(summary.totalCollected, 12750);
      expect(summary.singlePaymentCount, 1);
      expect(summary.presentCount, 1);
      expect(summary.freeCount, 0);
      expect(store.closings.single.difference, 0);
      final original = summary.toJson();
      await store.saveCardSettings(const CenterCardSettings(price: 9900));
      await store.saveStudent(mina.copyWith(discountPercent: 0));
      final backup = await store.createBackup(
        destination: '${directory.path}/closing.json',
      );
      await store.restoreBackup(backup);
      expect(store.closings.single.summary.toJson(), original);
      expect(store.cardPayments.first.netAmount, 2250);
    },
  );

  test(
    'card status and financial CSV distinguish paid pending, legacy delivery and unassigned cash without duplicate income',
    () async {
      await store.collectStudentCard(studentId: mina.id, sessionId: session.id);
      await store.collectStudentCard(
        studentId: lara.id,
        sessionId: session.id,
        method: 'إنستاباي',
      );
      await store.receiveStudentCard(mina.id);
      await store.receiveStudentCard(legacy.id);
      var cards = CenterReports.build(
        store,
        CenterReportKind.cards,
        const CenterReportFilter(),
      );
      expect(cards.summary['لم يستلموا'], '1');
      expect(cards.summary['مسددون ولم يستلموا'], '1');
      final legacyRow = cards.rows.firstWhere(
        (row) => row.first == legacy.code,
      );
      expect(
        legacyRow[cards.columns.indexOf('المحصل حتى الآن (جنيه مصري)')],
        isNull,
      );
      expect(
        legacyRow[cards.columns.indexOf('حالة الاستلام')],
        'استلم بدون تحصيل مسجل',
      );
      await store.saveStudent(
        Student(
          code: 'UNGROUPED',
          name: 'دفع خارج الحصة',
          groupIds: [group.id],
          createdAt: DateTime.now(),
        ),
      );
      await store.collectStudentCard(studentId: store.students.last.id);
      final report = CenterReports.build(
        store,
        CenterReportKind.payments,
        const CenterReportFilter(),
      );
      expect(report.rows, hasLength(3));
      expect(report.summary['التحصيل'], '82.50 ج');
      expect(report.summary['صافي النقدي'], '52.50 ج');
      expect(report.summary['الخصومات'], '7.50 ج');
      expect(report.summary['تحصيل غير مرتبط بحصة'], '30.00 ج');
      final linked = CenterReports.build(
        store,
        CenterReportKind.payments,
        CenterReportFilter(groupId: group.id),
      );
      expect(linked.rows, hasLength(2));
      final unassigned = CenterReports.build(
        store,
        CenterReportKind.payments,
        const CenterReportFilter(unassignedPaymentsOnly: true),
      );
      expect(unassigned.rows, hasLength(1));
      final refunds = CenterReports.build(
        store,
        CenterReportKind.payments,
        const CenterReportFilter(paymentMode: PaymentReportMode.refunds),
      );
      expect(refunds.rows, isEmpty);
      expect(refunds.summary['التحصيل'], '0.00 ج');
      await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.payments,
        filter: const CenterReportFilter(),
        destination: '${directory.path}/cards.csv',
      );
      final csv = await File('${directory.path}/cards.csv').readAsString();
      expect(csv, contains('تحصيل كارت'));
      expect(csv, contains('"22.50"'));
      expect(csv, isNot(contains(legacy.code)));
      cards = CenterReports.build(
        store,
        CenterReportKind.cards,
        const CenterReportFilter(),
      );
      expect(cards.summary['لم يستلموا'], '2');
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
    },
  );
}
