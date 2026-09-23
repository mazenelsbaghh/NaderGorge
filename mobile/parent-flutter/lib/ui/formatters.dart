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

String duration(int seconds) => '${seconds ~/ 60} دقيقة';
