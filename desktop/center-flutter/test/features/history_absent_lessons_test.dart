import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/student_history_panel.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/reception_ui_fixture.dart';

void main() {
  for (final expanded in [false, true]) {
    testWidgets(
      'history ${expanded ? 'expanded' : 'compact'} shows missing closed lessons and recorded absence beside the lesson number without changing accounts',
      (tester) async {
        late Directory directory;
        late CenterStore store;
        await tester.runAsync(() async {
          await initializeDateFormatting('ar_EG');
          directory = await Directory.systemTemp.createTemp(
            'massar-absent-history-',
          );
          store = await CenterStore.open(directory: directory.path);
          await seedReceptionUi(
            store,
            directory,
            studentCount: 1,
            sessionCount: 3,
          );
          addTearDown(() async {
            await store.close();
            await directory.delete(recursive: true);
          });
          // Old imported lessons can be closed without a persisted absence row.
          final backup =
              jsonDecode(await File(await store.createBackup()).readAsString())
                  as Map<String, dynamic>;
          final snapshot = backup['data'] as Map<String, dynamic>;
          snapshot['installationSeedId'] = 'synthetic-absence-history';
          final sessions = snapshot['sessions'] as List;
          sessions[0]['status'] = 'closed';
          sessions[1]['status'] = 'closed';
          final attendance = snapshot['attendances'] as List;
          attendance.removeWhere((row) => row['sessionId'] == 'session-0');
          attendance.firstWhere(
            (row) => row['sessionId'] == 'session-1',
          )['status'] = 'absent';
          (snapshot['payments'] as List).removeWhere(
            (row) => row['sessionId'] != 'session-2',
          );
          snapshot['paymentChecks'] = [];
          final group = store.groups.single;
          snapshot['groups'] = [
            ...snapshot['groups'] as List,
            group.copyWith(id: 'other-group', name: 'مجموعة أخرى').toJson(),
          ];
          final at = store.sessions.first.startsAt;
          for (final (id, number, status, groupId, lessonAt, known, roster) in [
            (
              'future',
              4,
              SessionStatus.open,
              group.id,
              at.add(const Duration(days: 4)),
              true,
              null,
            ),
            (
              'canceled',
              5,
              SessionStatus.canceled,
              group.id,
              at.add(const Duration(days: 5)),
              true,
              null,
            ),
            (
              'other',
              6,
              SessionStatus.closed,
              'other-group',
              at.add(const Duration(days: 6)),
              true,
              null,
            ),
            (
              'before-enrollment',
              7,
              SessionStatus.closed,
              group.id,
              at.subtract(const Duration(days: 10)),
              true,
              null,
            ),
            (
              'unknown',
              8,
              SessionStatus.closed,
              group.id,
              at.add(const Duration(days: 8)),
              false,
              null,
            ),
            (
              'imported-roster',
              9,
              SessionStatus.closed,
              group.id,
              at.add(const Duration(days: 9)),
              false,
              ['student-0'],
            ),
          ]) {
            sessions.add(
              LessonSession(
                id: id,
                groupId: groupId,
                number: number,
                startsAt: lessonAt,
                startsAtKnown: known,
                createdAt: at,
                status: status,
                importRoster: roster,
              ).toJson(),
            );
          }
          final fixture = File('${directory.path}/history-fixture.json');
          await fixture.writeAsString(jsonEncode(backup));
          await store.restoreBackup(fixture.path);
          await store.signIn('ui-admin', 'test-password-2026');
        });
        final before = jsonEncode([
          store.allAttendances,
          store.allPayments,
          store.allPackages,
          store.closings,
          store.audit,
        ]);
        await tester.binding.setSurfaceSize(const Size(1600, 1200));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        await tester.pumpWidget(
          MaterialApp(
            theme: MassarTheme.light,
            home: Directionality(
              textDirection: TextDirection.rtl,
              child: Scaffold(
                body: SingleChildScrollView(
                  child: StudentHistoryPanel(
                    store: store,
                    student: store.students.single,
                    expanded: expanded,
                  ),
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        for (final id in ['session-0', 'session-1', 'imported-roster']) {
          final cell = find.byKey(ValueKey('history-absence-lesson-$id'));
          expect(cell, findsOneWidget);
          expect(
            find.descendant(
              of: cell,
              matching: find.byIcon(Icons.cancel_outlined),
            ),
            findsOneWidget,
          );
        }
        expect(find.text('غائب'), findsNWidgets(3));
        expect(
          find.byKey(const ValueKey('cancel-attendance-attendance-0-2')),
          findsOneWidget,
        );
        expect(
          find.byKey(const ValueKey('cancel-attendance-attendance-0-1')),
          findsNothing,
        );
        for (final number in [4, 5, 6, 7, 8]) {
          expect(find.textContaining('شهر 1 · حصة $number'), findsNothing);
        }
        expect(find.textContaining('شهر 1 · حصة 1'), findsOneWidget);
        expect(find.textContaining('شهر 1 · حصة 2'), findsOneWidget);
        expect(
          jsonEncode([
            store.allAttendances,
            store.allPayments,
            store.allPackages,
            store.closings,
            store.audit,
          ]),
          before,
        );
        expect(tester.takeException(), isNull);
        // The panel keeps its original widget input while store membership changes.
        await tester.runAsync(
          () => store.saveStudent(
            store.students.single.copyWith(groupIds: ['other-group']),
          ),
        );
        await tester.pumpAndSettle();
        expect(
          find.byKey(const ValueKey('history-absence-lesson-session-0')),
          findsNothing,
        );
        expect(
          find.byKey(const ValueKey('history-absence-lesson-session-1')),
          findsOneWidget,
        );
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox.shrink());
      },
    );
  }
}
