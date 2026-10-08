import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:flutter/services.dart';
import '../application/center_store.dart';
import '../domain/models.dart';
import '../domain/student_lookup.dart';
import '../shared/problem_reporting.dart';

class MobileHomeworkGrant {
  MobileHomeworkGrant(this.token, this.staffId, this.sessionId, this.activityId)
    : expiresAt = DateTime.now().add(const Duration(hours: 2));
  final String token, staffId, sessionId, activityId;
  final DateTime expiresAt;
}

/// A temporary capability exposes one homework, never the general LAN API.
class MobileHomeworkAccess {
  MobileHomeworkAccess(this.store) {
    store.addListener(_staffChanged);
  }

  void _staffChanged() {
    if (_grant != null &&
        Zone.root.run(() => store.currentUser?.id) != _grant!.staffId) {
      revoke();
    }
  }

  void dispose() {
    revoke();
    store.removeListener(_staffChanged);
  }

  final CenterStore store;
  MobileHomeworkGrant? _grant;
  final _attempts = <DateTime>[];

  Future<MobileHomeworkGrant> open(String sessionId, String activityId) async {
    final staffId = store.currentUser?.id;
    if (staffId == null || !store.canAssess) {
      throw const CenterException('سجّل الدخول لرصد الواجب.');
    }
    await store.recordHomeworkExceptions(
      sessionId: sessionId,
      activityId: activityId,
    );
    if (store.currentUser?.id != staffId) {
      throw const CenterException('تغير حساب الموظف. افتح الصفحة من جديد.');
    }
    final random = Random.secure();
    final token = base64UrlEncode(
      List.generate(32, (_) => random.nextInt(256)),
    );
    return _grant = MobileHomeworkGrant(token, staffId, sessionId, activityId);
  }

  void revoke() => _grant = null;

  bool _authorized(MobileHomeworkGrant grant) =>
      identical(grant, _grant) &&
      DateTime.now().isBefore(grant.expiresAt) &&
      store.currentUser?.id == grant.staffId &&
      store.canAssess;

  MobileHomeworkGrant _requireGrant(HttpRequest request) {
    final grant = _grant;
    if (grant == null ||
        !_authorized(grant) ||
        request.headers.value('X-Massar-Mobile-Token') != grant.token) {
      throw const CenterException(
        'انتهى رابط الموبايل. افتحه من البرنامج من جديد.',
      );
    }
    final now = DateTime.now();
    _attempts.removeWhere(
      (time) => now.difference(time) > const Duration(minutes: 1),
    );
    if (_attempts.length >= 120) {
      throw const CenterException('طلبات كثيرة. انتظر دقيقة.');
    }
    _attempts.add(now);
    return grant;
  }

  Map<String, dynamic> _scope(MobileHomeworkGrant grant) {
    final session = store.sessionById(grant.sessionId);
    final activity = store.academicActivities
        .where((a) => a.id == grant.activityId)
        .firstOrNull;
    if (session == null ||
        session.status == SessionStatus.canceled ||
        !store.sessionHasStarted(session.id) ||
        activity == null ||
        activity.kind != AcademicActivityKind.homework ||
        !activity.appliesToSession(session)) {
      throw const CenterException(
        'الحصة أو الواجب غير متاح. افتح رابطًا جديدًا.',
      );
    }
    return {
      'group': store.groupById(session.groupId)?.name ?? '',
      'session': 'حصة ${session.number}',
      'homework': activity.name,
    };
  }

  Map<String, dynamic> _student(MobileHomeworkGrant grant, Student student) {
    final present = store
        .academicAttendanceForSession(grant.sessionId)
        .any(
          (a) =>
              a.studentId == student.id && a.status != AttendanceStatus.absent,
        );
    final saved = store
        .academicRecordsForSession(grant.sessionId)
        .where(
          (r) => r.activityId == grant.activityId && r.studentId == student.id,
        )
        .firstOrNull;
    final status = saved?.homework;
    return {
      'id': student.id,
      'name': student.name,
      'code': student.code,
      'group': student.groupIds
          .map((id) => store.groupById(id)?.name ?? '')
          .join('، '),
      'present': present,
      'missing': status == HomeworkStatus.missing,
      'status': switch (status) {
        HomeworkStatus.missing => 'ما اتعملش',
        HomeworkStatus.incomplete => 'ناقص',
        HomeworkStatus.exempt => 'معفى',
        HomeworkStatus.complete => 'اتعمل',
        _ => 'لم يُراجع',
      },
    };
  }

  Future<Map<String, dynamic>> _api(HttpRequest request) async {
    final grant = _requireGrant(request);
    final scope = _scope(grant);
    if (request.method == 'GET' && request.uri.path == '/mobile/context') {
      return scope;
    }
    if (request.method != 'POST' ||
        request.headers.contentType?.mimeType != 'application/json') {
      throw const FormatException('JSON required');
    }
    final bytes = <int>[];
    await for (final chunk in request.timeout(const Duration(seconds: 15))) {
      bytes.addAll(chunk);
      if (bytes.length > 4096) throw const FormatException('Oversized request');
    }
    final body = jsonDecode(utf8.decode(bytes)) as Map<String, dynamic>;
    if (!_authorized(grant)) {
      throw const CenterException('انتهى رابط الموبايل.');
    }
    if (request.uri.path == '/mobile/lookup') {
      final code = body['code'] as String;
      if (code.length > 128) throw const FormatException('Oversized code');
      final matches = studentsWithIdentifier(store.students, code);
      if (matches.isEmpty) {
        throw const CenterException('الكود غير موجود. راجع كارت الطالب.');
      }
      if (matches.length > 1) {
        throw const CenterException(
          'الكود يطابق أكثر من طالب. راجع الموظف على الهوست.',
        );
      }
      return _student(grant, matches.single);
    }
    if (request.uri.path != '/mobile/confirm') {
      throw const FormatException('Unknown request');
    }
    final student = store.studentById(body['studentId'] as String);
    if (student == null) throw const CenterException('الطالب غير موجود.');
    await store.commandLan(
      deviceId: 'mobile-homework',
      responseKind: LanCommandResponse.receipt,
      staffId: grant.staffId,
      authorize: () => _authorized(grant),
      request: {
        'requestId': body['requestId'] as String,
        'operation': 'recordHomeworkExceptions',
        'arguments': {
          'sessionId': grant.sessionId,
          'activityId': grant.activityId,
          'missingStudentId': student.id,
        },
      },
    );
    return {'saved': true, 'student': _student(grant, student)};
  }

  Future<void> respond(HttpRequest request) async {
    request.response.headers.set('Cache-Control', 'no-store');
    request.response.headers.set('Referrer-Policy', 'no-referrer');
    request.response.headers.set('X-Content-Type-Options', 'nosniff');
    request.response.headers.set(
      'Content-Security-Policy',
      "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' blob:; media-src 'self' blob:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'",
    );
    try {
      final file = switch (request.uri.path) {
        '/mobile/' => ('index.html', 'text/html; charset=utf-8'),
        '/mobile/app.js' => ('app.js', 'text/javascript; charset=utf-8'),
        '/mobile/scanner.js' => (
          'scanner.js',
          'text/javascript; charset=utf-8',
        ),
        '/mobile/style.css' => ('style.css', 'text/css; charset=utf-8'),
        '/mobile/logo.svg' => ('../logo.svg', 'image/svg+xml'),
        '/mobile/font.ttf' => ('../fonts/Tajawal-Regular.ttf', 'font/ttf'),
        _ => null,
      };
      if (file != null && request.method == 'GET') {
        final asset = file.$1.startsWith('../')
            ? 'assets/${file.$1.substring(3)}'
            : 'assets/mobile-homework/${file.$1}';
        final bytes = await rootBundle.load(asset);
        request.response.headers.set('Content-Type', file.$2);
        request.response.add(
          bytes.buffer.asUint8List(bytes.offsetInBytes, bytes.lengthInBytes),
        );
      } else {
        request.response.headers.contentType = ContentType.json;
        request.response.write(jsonEncode(await _api(request)));
      }
    } on CenterException catch (error) {
      request.response.statusCode = HttpStatus.badRequest;
      request.response.write(jsonEncode({'message': error.message}));
    } on FormatException {
      request.response.statusCode = HttpStatus.badRequest;
      request.response.write(jsonEncode({'message': 'طلب غير صالح.'}));
    } on TypeError {
      request.response.statusCode = HttpStatus.badRequest;
      request.response.write(
        jsonEncode({'message': 'بيانات الطلب غير مكتملة.'}),
      );
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'mobile.homework');
      request.response.statusCode = HttpStatus.internalServerError;
      request.response.write(
        jsonEncode({'message': 'تعذر تأكيد الحفظ. أعد المحاولة بنفس الطالب.'}),
      );
    } finally {
      await request.response.close();
    }
  }
}
