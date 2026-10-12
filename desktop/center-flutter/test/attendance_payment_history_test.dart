import 'dart:io';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/domain/discount_calculation.dart';
import 'package:massar_center/features/attendance/student_history_panel.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';
import 'helpers/synthetic_installer_assets.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  for (final scenario in [
    'exempt',
    'exempt-with-credit',
    'zero-price',
    'paid',
    'partial',
    'unpaid',
    'month',
    'center-unpaid',
    'center-paid',
    'free-session',
    'unknown-import',
    'gift-month',
    'explicit-free-month',
  ]) {
    testWidgets(
      'attendance history $scenario shows actual payment and settles only true zero fees',
      (tester) async {
        installSyntheticRepairAssets();
        late Directory directory;
        late CenterStore store;
        late Student student;
        late LessonSession session;
        int? due, paid;
        late String status;
        await tester.runAsync(() async {
          await initializeDateFormatting('ar_EG');
          directory = await Directory.systemTemp.createTemp(
            'attendance-payment-history-',
          );
          store = await CenterStore.open(directory: directory.path);
          await store.setupAdmin('fixture-admin', 'fixture-password-2026');
          for (final kind in CatalogKind.values) {
            await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
          }
          await store.saveGroup(
            StudyGroup(
              name: 'مجموعة الاختبار',
              subjectId: store.catalogs[0].id,
              centerId: store.catalogs[1].id,
              gradeId: store.catalogs[2].id,
              sessionPrice: scenario == 'zero-price' ? 0 : 6000,
              packagePrice: 21000,
            ),
          );
          final group = store.groups.single;
          final centerOnly = scenario.startsWith('center-');
          await store.saveStudent(
            Student(
              name: 'طالب الاختبار',
              code: '12345',
              groupIds: [group.id],
              discountPercent:
                  scenario == 'exempt' ||
                      scenario == 'gift-month' ||
                      scenario == 'explicit-free-month' ||
                      centerOnly
                  ? 100
                  : 0,
              centerOnly: centerOnly,
              centerFeeAmount: 1500,
              createdAt: DateTime(2026, 9, 1),
            ),
          );
          student = store.students.single;
          await store.saveSession(
            LessonSession(
              groupId: group.id,
              number: 1,
              kind: scenario == 'free-session'
                  ? SessionKind.free
                  : SessionKind.counted,
              startsAt: DateTime.now().add(const Duration(hours: 1)),
              createdAt: DateTime.now(),
            ),
          );
          session = store.sessions.single;
          if (scenario == 'month' || scenario == 'exempt-with-credit') {
            await store.renewPackage(
              PackageRequest(studentId: student.id, groupId: group.id),
            );
          }
          if (scenario == 'exempt-with-credit') {
            await store.saveStudent(student.copyWith(discountPercent: 100));
            student = store.students.single;
          }
          if (scenario == 'month') {
            await store.collectAndAttend(
              EntryRequest(
                studentId: student.id,
                sessionId: session.id,
                mode: EntryMode.package,
              ),
            );
          } else {
            await store.recordAttendance(
              EntryRequest(
                studentId: student.id,
                sessionId: session.id,
                mode: scenario == 'explicit-free-month'
                    ? EntryMode.package
                    : EntryMode.single,
              ),
            );
            if (scenario == 'paid' ||
                scenario == 'partial' ||
                scenario == 'center-paid') {
              await store.collectAndAttend(
                EntryRequest(
                  studentId: student.id,
                  sessionId: session.id,
                  mode: EntryMode.single,
                  paidAmount: scenario == 'partial'
                      ? 2000
                      : scenario == 'center-paid'
                      ? 1500
                      : 6000,
                ),
              );
            }
          }
          final zero = [
            'exempt',
            'exempt-with-credit',
            'zero-price',
            'free-session',
            'explicit-free-month',
          ].contains(scenario);
          if (scenario == 'exempt-with-credit' ||
              scenario == 'explicit-free-month') {
            await store.collectAndAttend(
              EntryRequest(
                studentId: student.id,
                sessionId: session.id,
                mode: EntryMode.package,
              ),
            );
          }
          if (scenario == 'gift-month') {
            await store.saveStudent(
              student.copyWith(
                discountPercent: discountPercentForAmount(21000, 15000),
              ),
            );
            await store.renewPackage(
              PackageRequest(
                studentId: student.id,
                groupId: group.id,
                sessionId: session.id,
              ),
            );
            await store.saveStudent(student.copyWith(discountPercent: 0));
            expect(store.packages.single.remaining, 4);
            expect(
              store.sessionFinancialSummary(session.id).totalCollected,
              15000,
            );
            expect(store.sessionFinancialSummary(session.id).allFreeCount, 1);
          }
          if (scenario == 'unknown-import') {
            final path = await store.createBackup();
            final backup =
                jsonDecode(await File(path).readAsString())
                    as Map<String, dynamic>;
            backup['data']['installationSeedId'] = 'synthetic-import-history';
            backup['data']['attendances'].single['importSource'] =
                'synthetic sheet without payment evidence';
            backup['data']['attendances'].single['paymentPending'] = false;
            final imported = File('${directory.path}/import-fixture.json');
            await imported.writeAsString(jsonEncode(backup));
            await store.restoreBackup(imported.path);
            await store.signIn('fixture-admin', 'fixture-password-2026');
          }
          due = scenario == 'exempt-with-credit'
              ? 21000
              : scenario == 'gift-month'
              ? 15000
              : scenario == 'unknown-import'
              ? null
              : zero
              ? 0
              : scenario == 'month'
              ? 21000
              : centerOnly
              ? 1500
              : 6000;
          paid = scenario == 'exempt-with-credit'
              ? 21000
              : scenario == 'unknown-import'
              ? null
              : zero || scenario == 'unpaid' || scenario == 'center-unpaid'
              ? 0
              : scenario == 'partial'
              ? 2000
              : due;
          status = scenario == 'exempt-with-credit'
              ? 'مسدد'
              : scenario == 'unknown-import'
              ? 'غير موثق'
              : zero
              ? 'مجاني'
              : scenario == 'partial'
              ? 'دفع جزئي'
              : scenario == 'unpaid' || scenario == 'center-unpaid'
              ? 'غير مدفوع'
              : 'مسدد';
          expect(store.attendances, hasLength(1));
          if (zero) {
            expect(
              store.attendanceNeedsPayment(student.id, session.id),
              isFalse,
            );
            expect(store.sessionFinancialSummary(session.id).totalCollected, 0);
            expect(store.sessionFinancialSummary(session.id).allFreeCount, 1);
            final count = store.allPayments.length;
            await store.recordAttendance(
              EntryRequest(
                studentId: student.id,
                sessionId: session.id,
                mode: EntryMode.single,
              ),
            );
            expect(store.allPayments.length, count);
            if (scenario != 'free-session' &&
                scenario != 'exempt-with-credit') {
              expect(
                store.paymentCollectedFor(
                  store.payments
                      .where((p) => p.sessionId == session.id)
                      .single
                      .id,
                ),
                0,
              );
              expect(
                store.payments
                    .where((p) => p.sessionId == session.id)
                    .single
                    .netAmount,
                0,
              );
            }
            if (scenario == 'exempt-with-credit' ||
                scenario == 'explicit-free-month') {
              expect(store.packages.single.remaining, 3);
            }
            await store.close();
            store = await CenterStore.open(directory: directory.path);
            await store.signIn('fixture-admin', 'fixture-password-2026');
            expect(
              store.attendanceNeedsPayment(student.id, session.id),
              isFalse,
            );
          }
          if (scenario == 'center-unpaid' || scenario == 'unpaid') {
            expect(
              store.attendanceNeedsPayment(student.id, session.id),
              isTrue,
            );
          }
          if (scenario == 'month') {
            expect(store.packages.single.remaining, 3);
            expect(store.sessionFinancialSummary(session.id).totalCollected, 0);
          }
          if (scenario == 'partial') {
            await store.saveStudent(student.copyWith(discountPercent: 100));
            await store.saveGroup(group.copyWith(sessionPrice: 9999));
          }
        });
        await tester.binding.setSurfaceSize(const Size(1440, 1000));
        try {
          await tester.pumpWidget(
            MaterialApp(
              theme: MassarTheme.light,
              home: Directionality(
                textDirection: TextDirection.rtl,
                child: Scaffold(
                  body: SingleChildScrollView(
                    child: StudentHistoryPanel(
                      store: store,
                      student: student,
                      currentSessionId: session.id,
                    ),
                  ),
                ),
              ),
            ),
          );
          await tester.pumpAndSettle();
          final tables = tester.widgetList<DataTable>(find.byType(DataTable));
          final table = tables.singleWhere(
            (t) => t.columns.any((c) => (c.label as Text).data == 'الحضور'),
          );
          int column(String label) =>
              table.columns.indexWhere((c) => (c.label as Text).data == label);
          final cells = table.rows.single.cells;
          String cellText(int index) => (tester
              .widgetList<Text>(
                find.descendant(
                  of: find.byWidget(cells[index].child),
                  matching: find.byType(Text),
                ),
              )
              .map((t) => t.data ?? '')
              .join());
          expect(
            cellText(column('المطلوب')),
            due == null
                ? '—'
                : '${money(due!)}${scenario == 'unpaid' ? ' *' : ''}',
          );
          expect(
            cellText(column('المدفوع')),
            paid == null ? '—' : money(paid!),
          );
          expect(cellText(column('السداد')), status);
          final icon = tester.widget<Icon>(
            find.descendant(
              of: find.byWidget(cells[column('السداد')].child),
              matching: find.byType(Icon),
            ),
          );
          expect(
            icon.icon,
            status == 'مجاني' || status == 'مسدد'
                ? Icons.check_circle_outline
                : Icons.remove_circle_outline,
          );
          expect(tester.takeException(), isNull);
        } finally {
          await tester.pumpWidget(const SizedBox.shrink());
          await tester.runAsync(() async {
            await store.close();
            await directory.delete(recursive: true);
          });
          await tester.binding.setSurfaceSize(null);
        }
      },
    );
  }
}
