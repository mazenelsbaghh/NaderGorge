import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/document_service.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  test(
    'local Arabic card and discounted receipt generate valid PDF without fetching fonts',
    () async {
      final directory = await Directory.systemTemp.createTemp(
        'massar-documents-',
      );
      final store = await CenterStore.open(directory: directory.path);
      addTearDown(() async {
        await store.close();
        await directory.delete(recursive: true);
      });
      await store.setupAdmin('مدير', 'document-test-pass');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'المجموعة ١',
          subjectId: store.catalogs[0].id,
          centerId: store.catalogs[1].id,
          gradeId: store.catalogs[2].id,
          sessionPrice: 10000,
          packagePrice: 40000,
        ),
      );
      await store.saveStudent(
        Student(
          name: 'أحمد محمد',
          code: '1001',
          groupIds: [store.groups.single.id],
          discountPercent: 20,
          createdAt: DateTime.now(),
        ),
      );
      await store.saveSession(
        LessonSession(
          groupId: store.groups.single.id,
          number: 1,
          startsAt: DateTime.now(),
          createdAt: DateTime.now(),
        ),
      );
      await store.collectAndAttend(
        EntryRequest(
          studentId: store.students.single.id,
          sessionId: store.sessions.single.id,
          mode: EntryMode.package,
        ),
      );
      final documents = [
        await DocumentService.studentCard(store, store.students.single),
        await DocumentService.receipt(store, store.payments.single),
      ];
      expect(store.payments.single.netAmount, 32000);
      for (final bytes in documents) {
        expect(String.fromCharCodes(bytes.take(5)), '%PDF-');
        expect(bytes.length, greaterThan(3000));
      }
    },
  );
  test(
    'bulk cards produce one PDF page per exact student without receiving or charging',
    () async {
      final directory = await Directory.systemTemp.createTemp(
        'massar-bulk-cards-',
      );
      final store = await CenterStore.open(directory: directory.path);
      addTearDown(() async {
        await store.close();
        await directory.delete(recursive: true);
      });
      await store.setupAdmin('مدير', 'document-test-pass');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'طباعة الكروت',
          subjectId: store.catalogs[0].id,
          centerId: store.catalogs[1].id,
          gradeId: store.catalogs[2].id,
        ),
      );
      for (var i = 1; i <= 3; i++) {
        await store.saveStudent(
          Student(
            name: 'طالب $i',
            code: 'CARD-$i',
            groupIds: [store.groups.single.id],
            createdAt: DateTime.now(),
          ),
        );
      }
      final beforeAudit = store.audit.length;
      final bytes = await DocumentService.studentCards(store, store.students);
      expect(String.fromCharCodes(bytes.take(5)), '%PDF-');
      expect(
        RegExp(
          r'/Type\s*/Page\b',
        ).allMatches(String.fromCharCodes(bytes)).length,
        3,
      );
      expect(store.cardPayments, isEmpty);
      expect(store.cardReceipts, isEmpty);
      expect(store.payments, isEmpty);
      expect(store.audit.length, beforeAudit);
      await expectLater(
        DocumentService.studentCards(store, []),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        DocumentService.studentCards(store, [
          store.students.first,
          store.students.first,
        ]),
        throwsA(isA<CenterException>()),
      );
    },
  );
}
