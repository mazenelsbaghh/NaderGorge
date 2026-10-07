import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:archive/archive.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/academic_excel_import.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/academic_excel_import_dialog.dart';
import 'package:massar_center/features/management/academics_page.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:xml/xml.dart';

List<int> _workbook(Map<String, List<List<String>>> sheets) {
  final archive = Archive();
  void add(String name, String contents) {
    final bytes = utf8.encode(contents);
    archive.addFile(ArchiveFile(name, bytes.length, bytes));
  }

  final names = sheets.keys.toList();
  add(
    'xl/workbook.xml',
    '<workbook xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>${[for (var i = 0; i < names.length; i++) '<sheet name="${names[i]}" sheetId="${i + 1}" r:id="r${i + 1}"/>'].join()}</sheets></workbook>',
  );
  add(
    'xl/_rels/workbook.xml.rels',
    '<Relationships>${[for (var i = 0; i < names.length; i++) '<Relationship Id="r${i + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet${i + 1}.xml"/>'].join()}</Relationships>',
  );
  for (var sheet = 0; sheet < names.length; sheet++) {
    final rows = [academicExcelColumns, ...sheets[names[sheet]]!];
    add(
      'xl/worksheets/sheet${sheet + 1}.xml',
      '<worksheet><sheetData>${[
        for (var row = 0; row < rows.length; row++) '<row r="${row + 1}">${[for (var column = 0; column < rows[row].length; column++) '<c r="${String.fromCharCode(65 + column)}${row + 1}" t="inlineStr"><is><t>${XmlText(rows[row][column]).toXmlString()}</t></is></c>'].join()}</row>',
      ].join()}</sheetData></worksheet>',
    );
  }
  return ZipEncoder().encode(archive);
}

void main() {
  final binding = TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late LessonSession session;
  late AcademicActivity exam;
  late Student first, second, outsider;
  final captureKey = GlobalKey();

  setUp(
    () => binding.runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp('massar-excel-dialog-');
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('مسؤول الاختبار', 'test-password-2026');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(
          CatalogEntry(
            name: kind == CatalogKind.center ? 'سنتر القاهرة' : kind.name,
            kind: kind,
          ),
        );
      }
      for (final name in ['مجموعة السبت', 'مجموعة أخرى']) {
        await store.saveGroup(
          StudyGroup(
            name: name,
            subjectId: store.catalogs
                .firstWhere((entry) => entry.kind == CatalogKind.subject)
                .id,
            centerId: store.catalogs
                .firstWhere((entry) => entry.kind == CatalogKind.center)
                .id,
            gradeId: store.catalogs
                .firstWhere((entry) => entry.kind == CatalogKind.grade)
                .id,
            sessionPrice: 6000,
          ),
        );
      }
      group = store.groups.first;
      for (final (name, phone, groupId) in [
        ('أحمد علي', '01000000001', group.id),
        ('مينا هاني', '01000000002', group.id),
        ('زياد خارج المجموعة', '01000000003', store.groups.last.id),
      ]) {
        await store.saveStudent(
          Student(
            name: name,
            phone: phone,
            groupIds: [groupId],
            createdAt: DateTime(2026),
          ),
        );
      }
      first = store.students[0];
      second = store.students[1];
      outsider = store.students[2];
      final month = await store.saveStudyMonth(
        StudyMonth(
          name: 'الشهر الأول',
          lessons: [const PreparedLesson(number: 1)],
        ),
      );
      session = await store.startPreparedLesson(
        groupId: group.id,
        preparedLessonId: month.lessons.single.id,
      );
      exam = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: month.lessons.single.id,
          kind: AcademicActivityKind.exam,
          name: 'الامتحان الأول',
          maxScore: 20,
          createdAt: DateTime.now(),
        ),
      );
    }),
  );
  void importWidgetTest(String name, WidgetTesterCallback body) {
    testWidgets(name, (tester) async {
      try {
        await body(tester);
      } finally {
        await tester.pumpWidget(const SizedBox.shrink());
        var closed = false;
        final closing = store.close().whenComplete(() => closed = true);
        // UI commands own fake-zone futures; drain that queue while SQLite closes.
        for (var attempt = 0; attempt < 100 && !closed; attempt++) {
          await tester.runAsync(
            () => Future<void>.delayed(const Duration(milliseconds: 20)),
          );
          await tester.pump();
        }
        expect(
          closed,
          isTrue,
          reason:
              'The SQLite command queue must finish before leaving the widget test.',
        );
        await closing;
        await tester.runAsync(() => directory.delete(recursive: true));
      }
    });
  }

  List<String> source(
    Student student, {
    String score = '18',
    String maximum = '20',
    String status = 'completed',
    String? sourceGroup,
    String? sourceExam,
  }) => [
    student.name,
    student.phone,
    student.code,
    'الثالث',
    'القاهرة',
    sourceGroup ?? group.name,
    'الحصة الأولى',
    sourceExam ?? exam.name,
    '2026-10-08 12:00',
    score,
    maximum,
    '90%',
    '0',
    status,
    '$score من $maximum',
    '',
    'source-session',
    'source-attempt-${student.id}',
    'v1',
  ];

  Future<void> tap(WidgetTester tester, Finder finder) async {
    await tester.ensureVisible(finder);
    await tester.tap(finder);
    await tester.pumpAndSettle();
  }

  Future<void> waitForIo(WidgetTester tester) async {
    await tester.pump();
    for (var attempt = 0; attempt < 100; attempt++) {
      await tester.runAsync(
        () => Future<void>.delayed(const Duration(milliseconds: 20)),
      );
      await tester.pump();
      if (find.byType(LinearProgressIndicator).evaluate().isEmpty) break;
    }
    expect(find.byType(LinearProgressIndicator), findsNothing);
    await tester.pumpAndSettle();
  }

  Future<void> open(
    WidgetTester tester,
    Map<String, List<List<String>>> sheets, {
    Size size = const Size(1500, 1000),
  }) async {
    await tester.binding.setSurfaceSize(size);
    addTearDown(() => tester.binding.setSurfaceSize(null));
    tester.view.physicalSize = size;
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final bytes = _workbook(sheets);
    if (const bool.fromEnvironment('CAPTURE_UI')) {
      await tester.runAsync(() async {
        await (FontLoader('Tajawal')
              ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
              ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
            .load();
      });
    }
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        builder: (context, child) => RepaintBoundary(
          key: captureKey,
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: child!,
          ),
        ),
        home: RepaintBoundary(
          child: Scaffold(
            body: Builder(
              builder: (context) => TextButton(
                onPressed: () => showDialog<int>(
                  context: context,
                  barrierDismissible: false,
                  builder: (context) => AcademicExcelImportDialog(
                    store: store,
                    groupId: group.id,
                    sessionId: session.id,
                    activityId: exam.id,
                    fileLoader: () async =>
                        AcademicExcelFile('results.xlsx', bytes),
                  ),
                ),
                child: const Text('فتح الاستيراد'),
              ),
            ),
          ),
        ),
      ),
    );
    await tap(tester, find.text('فتح الاستيراد'));
    await tester.tap(find.byKey(const Key('import-choose-file')));
    await waitForIo(tester);
    expect(find.byKey(const Key('import-error')), findsNothing);
  }

  bool saveEnabled(WidgetTester tester) =>
      tester
          .widget<FilledButton>(find.byKey(const Key('import-save')))
          .onPressed !=
      null;
  Future<void> save(WidgetTester tester) async {
    await tester.runAsync(
      () => tester.tap(find.byKey(const Key('import-save'))),
    );
    await waitForIo(tester);
  }

  Future<void> review(WidgetTester tester, int row) =>
      tap(tester, find.byKey(ValueKey('import-review-$row')));
  Future<void> applyReview(WidgetTester tester) =>
      tap(tester, find.byKey(const Key('import-apply-review')));
  Future<void> refresh(WidgetTester tester) async {
    await tester.ensureVisible(find.byKey(const Key('import-refresh')));
    await tester.tap(find.byKey(const Key('import-refresh')));
    await waitForIo(tester);
  }

  Future<void> existing(Student student, num score) => store.saveAcademic(
    AcademicRecord(
      studentId: student.id,
      sessionId: session.id,
      activityId: exam.id,
      score: score,
      maxScore: 20,
      notes: 'ملاحظة محفوظة',
      updatedAt: DateTime.now(),
    ),
  );

  importWidgetTest(
    'preview names the destination, shows source metadata and saves Cairo grades without money',
    (tester) async {
      await open(tester, {
        'النتائج': [source(first)],
        'ورقة إضافية': [source(second)],
      });
      expect(
        find.textContaining('J: درجة الطالب · K: المجموع'),
        findsOneWidget,
      );
      expect(find.textContaining('جاهز: 1'), findsOneWidget);
      expect(store.academics, isEmpty);
      await review(tester, 2);
      expect(
        find.textContaining('Q · معرّف الحصة بالمصدر: source-session'),
        findsOneWidget,
      );
      expect(
        find.textContaining('R · معرّف المحاولة بالمصدر: source-attempt'),
        findsOneWidget,
      );
      expect(find.textContaining('S · نسخة الورقة: v1'), findsOneWidget);
      await applyReview(tester);
      await tester.ensureVisible(find.byKey(const Key('import-destination')));
      await tester.pumpAndSettle();
      final boundary = tester.renderObject<RenderRepaintBoundary>(
        find.byKey(captureKey),
      );
      if (const bool.fromEnvironment('CAPTURE_UI')) {
        await tester.runAsync(() async {
          final image = await boundary.toImage(pixelRatio: 1);
          final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
          final file = File('build/verification/academic-excel-preview.png');
          await file.parent.create(recursive: true);
          await file.writeAsBytes(bytes!.buffer.asUint8List());
          image.dispose();
        });
      }
      expect(boundary, isNotNull);
      expect(saveEnabled(tester), isTrue);
      await save(tester);
      expect(store.academics.single.score, 18);
      expect(store.attendances.single.studentId, first.id);
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      expect(
        store.audit.where((record) => record.action == 'academic_import'),
        hasLength(1),
      );
    },
  );

  importWidgetTest(
    'unchanged grades skip and changed grades need explicit replacement',
    (tester) async {
      await tester.runAsync(() async {
        await existing(first, 18);
        await existing(second, 5);
      });
      await open(tester, {
        'النتائج': [source(first), source(second, score: '7')],
      });
      expect(find.textContaining('محفوظ كما هو: 1'), findsOneWidget);
      expect(saveEnabled(tester), isFalse);
      final unchanged = store.academics.firstWhere(
        (record) => record.studentId == first.id,
      );
      await review(tester, 3);
      await tap(tester, find.byKey(const Key('import-replace-existing')));
      await applyReview(tester);
      expect(saveEnabled(tester), isTrue);
      await save(tester);
      expect(
        store.academics
            .firstWhere((record) => record.studentId == first.id)
            .toJson(),
        unchanged.toJson(),
      );
      expect(
        store.academics
            .firstWhere((record) => record.studentId == second.id)
            .score,
        7,
      );
      expect(
        store.academics
            .firstWhere((record) => record.studentId == second.id)
            .notes,
        contains('ملاحظة محفوظة'),
      );
    },
  );

  importWidgetTest(
    'unknown status and duplicate manual choices require review before any grade is saved',
    (tester) async {
      final row = source(first, status: 'unknown-status');
      row[17] = 'manual-attempt';
      row[1] = '';
      row[2] = '';
      await open(tester, {
        'النتائج': [row, source(first)],
      });
      expect(saveEnabled(tester), isTrue);
      await review(tester, 2);
      await tap(tester, find.byKey(const Key('import-manual-student')));
      expect(
        find.byKey(ValueKey('import-student-${outsider.id}')),
        findsNothing,
      );
      await tap(tester, find.byKey(ValueKey('import-student-${first.id}')));
      await tap(tester, find.byKey(const Key('import-source-reviewed')));
      await applyReview(tester);
      // The exact second row is already linked to the same student.
      expect(saveEnabled(tester), isFalse);
      expect(store.academics, isEmpty);
      await tap(tester, find.byKey(const ValueKey('import-include-3')));
      expect(saveEnabled(tester), isTrue);
      await save(tester);
      expect(store.academics, hasLength(1));
      expect(store.academics.single.studentId, first.id);
    },
  );

  importWidgetTest(
    'relevant changes stale the preview, support notifications do not, and logout clears choices',
    (tester) async {
      await open(tester, {
        'النتائج': [source(first)],
      });
      store.supportStatusReader = () => {'configured': true, 'queued': true};
      store.notifyListeners();
      await tester.pump();
      expect(saveEnabled(tester), isTrue);
      await tester.runAsync(() => existing(first, 9));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('import-stale')), findsOneWidget);
      expect(saveEnabled(tester), isFalse);
      await refresh(tester);
      expect(find.byKey(const Key('import-stale')), findsNothing);
      expect(saveEnabled(tester), isFalse);
      await review(tester, 2);
      await tap(tester, find.byKey(const Key('import-replace-existing')));
      await applyReview(tester);
      expect(saveEnabled(tester), isTrue);
      await review(tester, 2);
      store.signOut();
      await tester.pumpAndSettle();
      expect(saveEnabled(tester), isFalse);
      expect(find.text('مراجعة صف 2'), findsNothing);
      expect(find.byKey(const ValueKey('import-review-2')), findsNothing);
      expect(store.academics.single.score, 9);
    },
  );

  importWidgetTest(
    'uniform source mismatch needs one explicit sheet confirmation at a small window',
    (tester) async {
      await open(tester, {
        'النتائج': [
          source(first, sourceExam: 'اختبار خارجي'),
          source(second, sourceExam: 'اختبار خارجي'),
        ],
      }, size: const Size(720, 600));
      expect(saveEnabled(tester), isFalse);
      await tap(tester, find.byKey(const Key('import-context-reviewed')));
      expect(saveEnabled(tester), isTrue);
      expect(tester.takeException(), isNull);
      await save(tester);
      expect(store.academics, hasLength(2));
    },
  );

  importWidgetTest(
    'page import stays disabled until a group session and named exam are selected',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1300, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await tester.pumpWidget(
        MaterialApp(
          theme: MassarTheme.light,
          home: Scaffold(body: AcademicsPage(store: store, cairo: true)),
        ),
      );
      await tester.pumpAndSettle();
      final button = tester.widget<OutlinedButton>(
        find.byKey(const Key('academic-import-excel')),
      );
      expect(button.onPressed, isNull);
    },
  );
}
