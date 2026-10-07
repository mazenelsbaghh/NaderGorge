import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/features/management/management_widgets.dart';

void main() {
  for (final pageSize in [20, 50]) {
    testWidgets(
      'reception table with $pageSize rows reaches its final page and recovers when results shrink',
      (tester) async {
        var count = pageSize + 1;
        late StateSetter update;
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: StatefulBuilder(
                builder: (context, setState) {
                  update = setState;
                  return ManagementTable.builder(
                    columns: const ['الكود'],
                    pageSize: pageSize,
                    rowCount: count,
                    rowBuilder: (index) => DataRow(
                      cells: [DataCell(Text('student-${index + 1}'))],
                    ),
                  );
                },
              ),
            ),
          ),
        );
        expect(find.text('student-1'), findsOneWidget);
        expect(find.text('student-${pageSize + 1}'), findsNothing);
        await tester.tap(find.byTooltip('الصفحة التالية'));
        await tester.pumpAndSettle();
        expect(find.text('student-${pageSize + 1}'), findsOneWidget);
        expect(find.text('student-1'), findsNothing);
        update(() => count = 1);
        await tester.pumpAndSettle();
        expect(find.text('student-1'), findsOneWidget);
        expect(find.byTooltip('الصفحة التالية'), findsNothing);
        update(() => count = 0);
        await tester.pumpAndSettle();
        expect(find.text('لا توجد سجلات'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  }
}
