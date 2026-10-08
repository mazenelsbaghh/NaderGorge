import 'dart:convert';
import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/center_store_host_bridge.dart';
import 'package:massar_center/lan/mobile_homework_access.dart';
import 'package:uuid/uuid.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late CenterStoreHostBridge bridge;
  late HttpClient client;
  late LessonSession session;
  late AcademicActivity homework;
  late Student present, absent;
  late MobileHomeworkGrant grant;
  const password = 'mobile-test-password';

  Future<(int, Map<String, dynamic>)> request(
    String path, {
    Map<String, dynamic>? body,
    String? token,
  }) async {
    final call = await client.openUrl(
      body == null ? 'GET' : 'POST',
      bridge.uri.resolve(path),
    );
    call.headers.set('X-Massar-Bridge-Secret', bridge.secret);
    call.headers.set('X-Massar-Mobile-Token', token ?? grant.token);
    if (body != null) {
      call.headers.contentType = ContentType.json;
      call.write(jsonEncode(body));
    }
    final response = await call.close();
    return (
      response.statusCode,
      jsonDecode(await utf8.decoder.bind(response).join())
          as Map<String, dynamic>,
    );
  }

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-mobile-test-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير', password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة الموبايل',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
      ),
    );
    final group = store.groups.single;
    final month = await store.saveStudyMonth(
      StudyMonth(
        name: 'شهر الواجب',
        lessons: [const PreparedLesson(number: 1)],
      ),
    );
    session = await store.startPreparedLesson(
      groupId: group.id,
      preparedLessonId: month.lessons.single.id,
    );
    homework = await store.saveAcademicActivity(
      AcademicActivity(
        preparedLessonId: month.lessons.single.id,
        name: 'واجب الدرس الأول',
        kind: AcademicActivityKind.homework,
        createdAt: DateTime.now(),
      ),
    );
    for (final code in ['00123', '00456']) {
      await store.saveStudent(
        Student(
          name: 'طالب $code',
          code: code,
          barcode: 'CARD-$code',
          groupIds: [group.id],
          createdAt: DateTime.now(),
        ),
      );
    }
    present = store.students.firstWhere((s) => s.code == '00123');
    absent = store.students.firstWhere((s) => s.code == '00456');
    await store.recordAttendance(
      EntryRequest(
        studentId: present.id,
        sessionId: session.id,
        mode: EntryMode.single,
      ),
    );
    bridge = await CenterStoreHostBridge.start(store);
    grant = await bridge.mobileHomework.open(session.id, homework.id);
    client = HttpClient();
  });
  tearDown(() async {
    client.close(force: true);
    await bridge.close();
    await store.close();
    await directory.delete(recursive: true);
  });

  test(
    'barcode and Arabic code preview saved student without recording an exception',
    () async {
      for (final code in ['CARD-00123', '٠٠١٢٣', '0123']) {
        final preview = await request('/mobile/lookup', body: {'code': code});
        expect(preview.$1, 200);
        expect(preview.$2['id'], present.id);
        expect(preview.$2['present'], true);
        expect(preview.$2.containsKey('phone'), false);
      }
      expect(store.academics.single.homework, HomeworkStatus.complete);
      expect(
        (await request('/mobile/lookup', body: {'code': 'unknown'})).$1,
        400,
      );
      expect(store.students.length, 2);
    },
  );

  test(
    'unregistered and non-attending students cannot be confirmed; scope stays on selected homework',
    () async {
      final preview = await request(
        '/mobile/lookup',
        body: {'code': absent.code},
      );
      expect(preview.$2['present'], false);
      for (final id in [absent.id, const Uuid().v4()]) {
        expect(
          (await request(
            '/mobile/confirm',
            body: {'studentId': id, 'requestId': const Uuid().v4()},
          )).$1,
          400,
        );
      }
      final second = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: homework.preparedLessonId,
          name: 'واجب آخر',
          kind: AcademicActivityKind.homework,
          createdAt: DateTime.now(),
        ),
      );
      final confirmation = {
        'studentId': present.id,
        'requestId': const Uuid().v4(),
        'activityId': second.id,
      };
      expect((await request('/mobile/confirm', body: confirmation)).$1, 200);
      final saved = store.academics.single;
      expect(saved.activityId, homework.id);
      expect(saved.homework, HomeworkStatus.missing);
      expect((await request('/mobile/confirm', body: confirmation)).$1, 200);
      expect(store.academics.single.updatedAt, saved.updatedAt);
    },
  );

  test(
    'revoked and logged-out links cannot read student data even after same staff signs in',
    () async {
      expect((await request('/mobile/context', token: 'wrong-token')).$1, 400);
      final oldToken = grant.token;
      grant = await bridge.mobileHomework.open(session.id, homework.id);
      expect((await request('/mobile/context', token: oldToken)).$1, 400);
      store.signOut();
      await store.signIn('مدير', password);
      expect(
        (await request('/mobile/lookup', body: {'code': present.code})).$1,
        400,
      );
      expect(store.academics.single.homework, HomeworkStatus.complete);
    },
  );

  test('an exam cannot be selected as mobile homework', () async {
    final exam = await store.saveAcademicActivity(
      AcademicActivity(
        preparedLessonId: homework.preparedLessonId,
        name: 'امتحان',
        kind: AcademicActivityKind.exam,
        maxScore: 10,
        createdAt: DateTime.now(),
      ),
    );
    await expectLater(
      bridge.mobileHomework.open(session.id, exam.id),
      throwsA(isA<CenterException>()),
    );
    expect((await request('/mobile/context')).$2['homework'], homework.name);
  });
}
