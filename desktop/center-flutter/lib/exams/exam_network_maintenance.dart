import '../domain/models.dart';
import 'exam_engine.dart';

/// Durable engine ownership prevents room opening during a network change.
class ExamNetworkMaintenance {
  ExamNetworkMaintenance(this.engine);

  final ExamEngine engine;
  String? releaseWarning;

  Future<T> run<T>(
    Future<T> Function(Future<void> Function() beforeWrite) change,
  ) async {
    releaseWarning = null;
    final lease = await engine.request('/api/network/maintenance/acquire', {});
    final token = lease['leaseToken'];
    if (token is! String || token.isEmpty) {
      throw const CenterException('تعذر تأمين وقت تجهيز الشبكة. أعد الفحص.');
    }
    try {
      final expiresAt = lease['expiresAt'];
      if (expiresAt is! num || !expiresAt.isFinite) {
        throw const CenterException('تعذر تأمين وقت تجهيز الشبكة. أعد الفحص.');
      }
      return await change(() async {
        if (DateTime.now().millisecondsSinceEpoch / 1000 >= expiresAt - 30) {
          throw const CenterException(
            'انتهت مهلة التجهيز قبل الإرسال. أعد الفحص.',
          );
        }
      });
    } finally {
      await _release(token);
    }
  }

  Future<void> _release(String token) async {
    try {
      final released = await engine.request(
        '/api/network/maintenance/release',
        {'leaseToken': token},
      );
      if (released['released'] != true) throw const FormatException();
    } on Object {
      releaseWarning =
          'لم يتأكد انتهاء حجز التهيئة. افحص الشبكة قبل فتح القاعة؛ '
          'الحجز المؤقت ينتهي خلال عشر دقائق.';
    }
  }
}
