import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/session_finance.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  final recordedAt = DateTime.utc(2026, 10, 8, 10);
  final session = LessonSession(
    id: 'lesson',
    groupId: 'group',
    number: 1,
    startsAt: recordedAt,
    createdAt: recordedAt,
  );

  test(
    'center-only payer categories use actual historical attendance and positive receipt amounts',
    () {
      final attendances = [
        for (final student in ['partial', 'full', 'unpaid'])
          AttendanceRecord(
            id: 'attendance-$student',
            studentId: student,
            sessionId: session.id,
            status: AttendanceStatus.present,
            centerFeeOnly: true,
            recordedAt: recordedAt,
          ),
        AttendanceRecord(
          id: 'makeup',
          studentId: 'makeup',
          sessionId: session.id,
          status: AttendanceStatus.makeup,
          centerFeeOnly: true,
          recordedAt: recordedAt,
        ),
        AttendanceRecord(
          id: 'regular',
          studentId: 'regular',
          sessionId: session.id,
          status: AttendanceStatus.present,
          recordedAt: recordedAt,
        ),
        AttendanceRecord(
          id: 'absent',
          studentId: 'absent',
          sessionId: session.id,
          status: AttendanceStatus.absent,
          centerFeeOnly: true,
          recordedAt: recordedAt,
        ),
        AttendanceRecord(
          id: 'another-lesson',
          studentId: 'other-session-attendee',
          sessionId: 'another-lesson',
          status: AttendanceStatus.present,
          centerFeeOnly: true,
          recordedAt: recordedAt,
        ),
      ];
      final receipts = [
        CenterFeeRecord(
          id: 'partial-original',
          studentId: 'partial',
          sessionId: session.id,
          amount: 2500,
          paidAmount: 500,
          recordedAt: recordedAt,
        ),
        for (final topUp in [(1, 500), (2, 500), (3, 1000)])
          CenterFeeRecord(
            id: 'top-up-${topUp.$1}',
            studentId: 'partial',
            sessionId: session.id,
            amount: topUp.$2,
            paidAmount: topUp.$2,
            originalFeeId: 'partial-original',
            recordedAt: recordedAt.add(const Duration(minutes: 1)),
          ),
        for (final student in [
          'full',
          'regular',
          'absent',
          'other-session-attendee',
        ])
          CenterFeeRecord(
            id: 'fee-$student',
            studentId: student,
            sessionId: session.id,
            amount: 500,
            paidAmount: 500,
            method: student == 'full' ? 'تحويل' : 'نقدي',
            recordedAt: recordedAt,
          ),
        CenterFeeRecord(
          id: 'makeup-fee',
          studentId: 'makeup',
          sessionId: session.id,
          amount: 1000,
          paidAmount: 1000,
          recordedAt: recordedAt,
        ),
        CenterFeeRecord(
          id: 'unpaid-fee',
          studentId: 'unpaid',
          sessionId: session.id,
          amount: 1500,
          paidAmount: 0,
          recordedAt: recordedAt,
        ),
        CenterFeeRecord(
          id: 'another-lesson-fee',
          studentId: 'partial',
          sessionId: 'another-lesson',
          amount: 9000,
          paidAmount: 9000,
          recordedAt: recordedAt,
        ),
      ];
      final summary = buildSessionFinancialSummary(
        session: session,
        attendances: attendances,
        payments: [
          PaymentRecord(
            id: 'teacher-receipt',
            studentId: 'regular',
            groupId: session.groupId,
            sessionId: session.id,
            description: 'رسوم المدرس',
            baseAmount: 6000,
            discountPercent: 0,
            netAmount: 6000,
            createdAt: recordedAt,
            staffId: 'cashier',
          ),
        ],
        centerFees: receipts,
      );
      expect(
        summary.centerFeePaymentCategories!.map(
          (category) => category.toJson(),
        ),
        [
          {
            'centerOnly': true,
            'unitAmount': 500,
            'studentCount': 2,
            'operationCount': 4,
          },
          {
            'centerOnly': true,
            'unitAmount': 1000,
            'studentCount': 2,
            'operationCount': 2,
          },
          {
            'centerOnly': false,
            'unitAmount': 500,
            'studentCount': 3,
            'operationCount': 3,
          },
        ],
      );
      expect(summary.centerFeeCollected, 5500);
      expect(summary.grossAmount, 11500);
      expect(summary.totalCollected, 11500);
      expect(summary.expectedCash, 11000);
      expect(
        summary.lines.fold<int>(0, (sum, line) => sum + line.total),
        11500,
      );
      expect(summary.paymentAmountCategories!.single.toJson(), {
        'kind': 'single',
        'unitAmount': 6000,
        'studentCount': 1,
        'operationCount': 1,
      });
      final decoded = SessionFinancialSummary.fromJson(
        jsonDecode(jsonEncode(summary.toJson())),
      );
      expect(decoded.toJson(), summary.toJson());
      expect(
        () => decoded.centerFeePaymentCategories!.clear(),
        throwsUnsupportedError,
      );
    },
  );

  test(
    'legacy closing remains unknown while an empty new closing records no center payers',
    () {
      final summary = buildSessionFinancialSummary(
        session: session,
        attendances: const [],
        payments: const [],
      );
      expect(summary.centerFeePaymentCategories, isEmpty);
      final legacyJson = summary.toJson()..remove('centerFeePaymentCategories');
      final legacy = SessionFinancialSummary.fromJson(
        jsonDecode(jsonEncode(legacyJson)),
      );
      expect(legacy.centerFeePaymentCategories, isNull);
      expect(legacy.toJson(), legacyJson);
      expect(
        legacy.toJson().containsKey('centerFeePaymentCategories'),
        isFalse,
      );
    },
  );
}
