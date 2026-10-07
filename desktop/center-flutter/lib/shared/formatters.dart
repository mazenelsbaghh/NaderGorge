import 'package:intl/intl.dart';
import 'package:intl/date_symbol_data_local.dart';
import '../domain/models.dart';

String money(int piastres) =>
    '${NumberFormat('#,##0.##', 'ar_EG').format(piastres / 100)} ج';
String shortDate(DateTime date) {
  initializeDateFormatting('ar_EG');
  return DateFormat('d/M/yyyy', 'ar_EG').format(date);
}

String sessionDateLabel(LessonSession session) =>
    session.startsAtKnown ? shortDate(session.startsAt) : 'التاريخ غير متوفر';

String sessionLabel(LessonSession session) =>
    'شهر ${session.monthNumber} · حصة ${session.number}${session.name.isEmpty ? '' : ' · ${session.name}'}';

int compareSessionsNewestFirst(LessonSession first, LessonSession second) {
  final dateOrder = second.startsAt.compareTo(first.startsAt);
  if (dateOrder != 0) {
    return dateOrder;
  }
  final monthOrder = second.monthNumber.compareTo(first.monthNumber);
  return monthOrder != 0 ? monthOrder : second.number.compareTo(first.number);
}

String sessionKindLabel(SessionKind kind) => switch (kind) {
  SessionKind.counted => 'ضمن الباقة',
  SessionKind.free => 'خارج الباقة • مجانية',
  SessionKind.extra => 'خارج الباقة • بسعر منفصل',
};
String homeworkLabel(HomeworkStatus status) => switch (status) {
  HomeworkStatus.notReviewed => 'لم يُراجع',
  HomeworkStatus.complete => 'كامل',
  HomeworkStatus.incomplete => 'ناقص',
  HomeworkStatus.missing => 'لم يعمل',
  HomeworkStatus.exempt => 'معفى',
};
String attendanceLabel(AttendanceStatus status) => switch (status) {
  AttendanceStatus.present => 'حاضر',
  AttendanceStatus.absent => 'غائب',
  AttendanceStatus.makeup => 'حاضر • تعويض',
};
String staffRoleLabel(StaffRole role) => switch (role) {
  StaffRole.admin => 'الإدارة',
  StaffRole.cashier => 'الاستقبال والتحصيل',
  StaffRole.assistant => 'المساعد والرصد',
};

/// Display only: exact percentages remain intact in storage and calculations.
String percentText(num percent) {
  if (percent == percent.round()) return '${percent.round()}';
  final rounded = percent
      .toStringAsFixed(6)
      .replaceFirst(RegExp(r'0+$'), '')
      .replaceFirst(RegExp(r'\.$'), '');
  return '${num.parse(rounded) == percent ? '' : '≈'}$rounded';
}

num? percentFromText(String text) {
  const arabic = '٠١٢٣٤٥٦٧٨٩', persian = '۰۱۲۳۴۵۶۷۸۹';
  var normalized = text.trim();
  for (var digit = 0; digit < 10; digit++) {
    normalized = normalized
        .replaceAll(arabic[digit], '$digit')
        .replaceAll(persian[digit], '$digit');
  }
  normalized = normalized.replaceAll('٫', '.').replaceAll(',', '.');
  if (!RegExp(r'^\d+(\.\d+)?$').hasMatch(normalized)) return null;
  final percent = num.tryParse(normalized);
  return percent != null && percent.isFinite && percent >= 0 && percent <= 100
      ? percent
      : null;
}
