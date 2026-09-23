import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:http/http.dart' as http;
import 'models.dart';

class ParentFailure implements Exception {
  final String message;
  final bool unauthorized;
  const ParentFailure(this.message, {this.unauthorized = false});
}

class ParentApi {
  final http.Client client;
  final Uri baseUrl;
  ParentApi({http.Client? client, Uri? baseUrl})
    : client = client ?? http.Client(),
      baseUrl =
          baseUrl ??
          Uri.parse(
            const String.fromEnvironment(
              'PARENT_API_URL',
              defaultValue: 'https://api.massar-academy.net/',
            ),
          );

  Future<dynamic> request(String path, {String? token, Json? body}) async {
    final uri = baseUrl.resolve('api/parent/$path');
    final headers = {
      'Accept': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
    };
    try {
      final response =
          await (body == null
                  ? client.get(uri, headers: headers)
                  : client.post(
                      uri,
                      headers: {...headers, 'Content-Type': 'application/json'},
                      body: jsonEncode(body),
                    ))
              .timeout(const Duration(seconds: 20));
      if (response.statusCode == 401 || response.statusCode == 403) {
        throw const ParentFailure(
          'انتهت صلاحية الربط. أعد إدخال رمز الطالب.',
          unauthorized: true,
        );
      }
      final envelope = jsonDecode(utf8.decode(response.bodyBytes));
      if (envelope is! Json) throw const FormatException('Invalid envelope');
      if (response.statusCode >= 400 || envelope['success'] != true) {
        throw ParentFailure(
          envelope['message'] as String? ?? 'تعذر إتمام الطلب. حاول مرة أخرى.',
        );
      }
      return envelope['data'];
    } on TimeoutException {
      throw const ParentFailure('انتهت مهلة الاتصال. حاول مرة أخرى.');
    } on SocketException {
      throw const ParentFailure(
        'تعذر الاتصال. تحقق من الإنترنت وحاول مرة أخرى.',
      );
    } on http.ClientException {
      throw const ParentFailure('تعذر الاتصال بالخادم. حاول مرة أخرى.');
    } on FormatException {
      throw const ParentFailure('تعذر قراءة استجابة الخادم. حاول مرة أخرى.');
    }
  }

  Future<LinkedStudent> verify(String code) async => LinkedStudent.verified(
    await request(
          'verify-code',
          body: {
            'trackingCode': code,
            'platform': Platform.isIOS ? 'ios' : 'android',
          },
        )
        as Json,
  );
  Future<StudentDetails> details(LinkedStudent student) async => StudentDetails(
    await request('student-details', token: student.token) as Json,
  );
  Future<List<AcademicRow>> notifications(LinkedStudent student) async =>
      (await request('notifications', token: student.token) as List)
          .map((row) => AcademicRow(row as Json))
          .toList();
  Future<void> markRead(LinkedStudent student, String id) async {
    await request(
      'notifications/${Uri.encodeComponent(id)}/read',
      token: student.token,
      body: {},
    );
  }

  Future<void> register(LinkedStudent student, String deviceToken) async {
    await request(
      'device-token',
      token: student.token,
      body: {
        'deviceToken': deviceToken,
        'platform': Platform.isIOS ? 'ios' : 'android',
      },
    );
  }

  void dispose() => client.close();
}
