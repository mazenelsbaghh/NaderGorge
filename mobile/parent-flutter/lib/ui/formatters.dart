import 'package:intl/intl.dart';
import 'package:timezone/timezone.dart' as tz;

String number(num value) => NumberFormat('0.##', 'en').format(value);
String displayDate(String raw, {bool compact = false}) {
  if (raw.isEmpty) return '—';
  final normalized = RegExp(r'(Z|[+-]\d{2}:\d{2})$').hasMatch(raw)
      ? raw
      : '${raw}Z';
  final parsed = DateTime.tryParse(normalized);
  if (parsed == null) return '—';
  return DateFormat(
    compact ? 'd MMMM' : 'd MMMM y، h:mm a',
    'ar',
  ).format(tz.TZDateTime.from(parsed, tz.getLocation('Africa/Cairo')));
}

String duration(int seconds) {
  final safeSeconds = seconds < 0 ? 0 : seconds;
  final hours = safeSeconds ~/ 3600;
  final minutes = (safeSeconds % 3600) ~/ 60;
  final remainder = safeSeconds % 60;
  return [
    if (hours > 0) '$hours ساعة',
    if (minutes > 0) '$minutes دقيقة',
    if (remainder > 0 || safeSeconds == 0) '$remainder ثانية',
  ].join(' و');
}
