import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

// Independent cents ledger: rounding uses integer arithmetic, not production's
// floating-point formula. Seeds are fixed so failures reproduce exactly.
int netCents(int base, int discount) => (base * (100 - discount) + 50) ~/ 100;

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late String groupId;
  final dates = DateTime.now().add(const Duration(days: 2));

  setUp(() async {
    directory = await Directory.systemTemp.createTemp(
      'massar-finance-sequences-',
    );
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير', 'sequence-test-password');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة الاختبار',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
        sessionPrice: 101,
        twoSessionPrice: 17777,
        threeSessionPrice: 23333,
        packagePrice: 41001,
      ),
    );
    groupId = store.groups.single.id;
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });
  Future<void> restart() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('مدير', 'sequence-test-password');
  }

  Future<LessonSession> lesson(int number, SessionKind kind, int extra) async {
    await store.saveSession(
      LessonSession(
        groupId: groupId,
        number: number,
        kind: kind,
        extraPrice: extra,
        startsAt: dates.add(Duration(hours: number)),
        createdAt: DateTime.now(),
      ),
    );
    return store.sessions.last;
  }

  Future<Student> student(String code, int discount) async {
    await store.saveStudent(
      Student(
        code: code,
        name: 'طالب $code',
        discountPercent: discount,
        groupIds: [groupId],
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    );
    return store.students.last;
  }

  test(
    'all 101 discount percentages reconcile single and independently priced 2/3/4 purchases in cents after restart',
    () async {
      final session = await lesson(1, SessionKind.counted, 0);
      var expectedNet = 0;
      var expectedCash = 0;
      var expectedBase = 0;
      for (var discount = 0; discount <= 100; discount++) {
        for (final count in [1, 2, 3, 4]) {
          final entryStudent = await student('D$discount-$count', discount);
          final base = {1: 101, 2: 17777, 3: 23333, 4: 41001}[count]!;
          final net = netCents(base, discount);
          final method = (discount + count).isEven ? 'نقدي' : 'تحويل';
          final request = EntryRequest(
            studentId: entryStudent.id,
            sessionId: session.id,
            mode: count == 1 ? EntryMode.single : EntryMode.package,
            packageSessions: count == 1 ? 4 : count,
            method: method,
          );
          final quote = store.entryConfirmationFor(request);
          expect(
            quote.netAmount,
            net,
            reason: 'discount=$discount size=$count',
          );
          await store.collectAndAttend(
            EntryRequest(
              studentId: request.studentId,
              sessionId: request.sessionId,
              mode: request.mode,
              packageSessions: request.packageSessions,
              method: method,
              confirmation: quote,
            ),
          );
          final payment = store.payments.last;
          expect(payment.baseAmount, base);
          expect(payment.netAmount, net);
          expect(payment.discountPercent, discount);
          if (count > 1) {
            expect(store.packages.last.totalSessions, count);
            expect(store.packages.last.remaining, count - 1);
          }
          expectedBase += base;
          expectedNet += net;
          if (method == 'نقدي') expectedCash += net;
        }
      }
      await store.closeSession(session.id);
      final summary = store.sessionFinancialSummary(session.id);
      expect(summary.totalCollected, expectedNet);
      expect(summary.expectedCash, expectedCash);
      expect(summary.grossAmount, expectedBase);
      expect(
        summary.lines.fold<int>(0, (sum, row) => sum + row.total),
        expectedNet,
      );
      expect(store.attendanceCount(session.id), 404);
      await store.finalizeSession(
        sessionId: session.id,
        actualCash: expectedCash - 1,
      );
      expect(store.closings.single.difference, -1);
      final saved = store.closings.single.summary.toJson();
      await restart();
      expect(store.payments, hasLength(404));
      expect(store.closings.single.summary.toJson(), saved);
      expect(
        store.sessionFinancialSummary(session.id).totalCollected,
        expectedNet,
      );
    },
    timeout: const Timeout(Duration(minutes: 3)),
  );

  test(
    'sixty simultaneous students in a legacy limited group all register and reconcile after restart',
    () async {
      final legacyBackup = await store.createBackup();
      final content =
          jsonDecode(await File(legacyBackup).readAsString()) as Map;
      (content['data']['groups'] as List).single['capacity'] = 3;
      await File(legacyBackup).writeAsString(jsonEncode(content));
      await store.restoreBackup(legacyBackup);
      await restart();
      final session = await lesson(1, SessionKind.counted, 0);
      final ids = <String>[];
      for (var index = 0; index < 60; index++) {
        ids.add((await student('C$index', 25)).id);
      }
      final auditBefore = store.audit.length;
      await Future.wait(
        ids.map(
          (id) => store.collectAndAttend(
            EntryRequest(
              studentId: id,
              sessionId: session.id,
              mode: EntryMode.package,
              packageSessions: 2,
            ),
          ),
        ),
      );
      expect(store.attendanceCount(session.id), 60);
      expect(store.payments, hasLength(60));
      expect(store.packages, hasLength(60));
      expect(store.audit.length, auditBefore + 60);
      for (final id in ids) {
        expect(store.remainingFor(id, groupId), 1);
      }
      expect(
        store.sessionFinancialSummary(session.id).totalCollected,
        60 * netCents(17777, 25),
      );
      await store.closeSession(session.id);
      await restart();
      expect(store.attendanceCount(session.id), 60);
      expect(store.attendances, hasLength(60));
      expect(store.payments, hasLength(60));
      expect(
        store.packages.map((package) => package.remaining),
        everyElement(1),
      );
    },
  );

  for (final seed in [7, 43, 20261002]) {
    test(
      'seed $seed mixed 18-session lifecycle conserves balances and historical cash through price changes, absences, refunds and restarts',
      () async {
        final random = Random(seed);
        final ids = <String>[];
        final balances = <String, int>{};
        for (var index = 0; index < 5; index++) {
          final entryStudent = await student('R$index', 0);
          ids.add(entryStudent.id);
          balances[entryStudent.id] = 0;
        }
        final closings = <String, Map<String, dynamic>>{};
        var totalPurchases = 0;
        var totalRefunds = 0;
        for (var number = 1; number <= 18; number++) {
          final kind = number % 5 == 0
              ? SessionKind.free
              : number % 4 == 0
              ? SessionKind.extra
              : SessionKind.counted;
          final singlePrice = 101 + random.nextInt(30000);
          final priceByCount = {
            2: 7001 + random.nextInt(30000),
            3: 16001 + random.nextInt(30000),
            4: 27001 + random.nextInt(30000),
          };
          await store.saveGroup(
            store.groups.single.copyWith(
              sessionPrice: singlePrice,
              twoSessionPrice: priceByCount[2],
              threeSessionPrice: priceByCount[3],
              packagePrice: priceByCount[4],
            ),
          );
          final extra = 1 + random.nextInt(15000);
          final session = await lesson(number, kind, extra);
          var expectedNet = 0;
          var expectedCash = 0;
          final present = <String>{};
          for (final id in ids) {
            final discount = [0, 1, 25, 33, 50, 99, 100][random.nextInt(7)];
            await store.saveStudent(
              store.students
                  .firstWhere((s) => s.id == id)
                  .copyWith(discountPercent: discount),
            );
            if (random.nextInt(4) == 0) continue;
            final count = 2 + random.nextInt(3);
            final hasBalance = balances[id]! > 0;
            final mode = hasBalance || random.nextBool()
                ? EntryMode.package
                : EntryMode.single;
            final method = random.nextBool() ? 'نقدي' : 'تحويل';
            final base = kind == SessionKind.free
                ? 0
                : kind == SessionKind.extra
                ? extra
                : hasBalance
                ? 0
                : mode == EntryMode.single
                ? singlePrice
                : priceByCount[count]!;
            final net = netCents(base, discount);
            final request = EntryRequest(
              studentId: id,
              sessionId: session.id,
              mode: mode,
              packageSessions: count,
              method: method,
            );
            final quote = store.entryConfirmationFor(request);
            expect(
              quote.netAmount,
              net,
              reason: 'seed=$seed session=$number student=$id',
            );
            await store.collectAndAttend(
              EntryRequest(
                studentId: id,
                sessionId: session.id,
                mode: mode,
                packageSessions: count,
                method: method,
                confirmation: quote,
              ),
            );
            if (kind == SessionKind.counted && mode == EntryMode.package) {
              if (!hasBalance) balances[id] = count;
              balances[id] = balances[id]! - 1;
            }
            expectedNet += net;
            if (method == 'نقدي') expectedCash += net;
            totalPurchases += net;
            present.add(id);
            final countsBefore = (
              store.allPayments.length,
              store.allAttendances.length,
              store.audit.length,
            );
            await expectLater(
              store.collectAndAttend(request),
              throwsA(isA<CenterException>()),
            );
            expect((
              store.allPayments.length,
              store.allAttendances.length,
              store.audit.length,
            ), countsBefore);
            // Reverse a direct payment, then let closure create the unpaid absence.
            if (base > 0 &&
                (kind == SessionKind.extra || mode == EntryMode.single) &&
                random.nextInt(4) == 0) {
              await store.reverseEntry(
                attendanceId: store.attendances.last.id,
                reason: 'تسجيل خاطئ',
                refundMethod: 'نقدي',
              );
              expectedNet -= net;
              expectedCash -= net;
              totalRefunds += net;
              present.remove(id);
            }
          }
          await store.closeSession(session.id);
          for (final id in ids) {
            if (!present.contains(id) &&
                kind == SessionKind.counted &&
                balances[id]! > 0) {
              balances[id] = balances[id]! - 1;
            }
            expect(
              store.remainingFor(id, groupId),
              balances[id],
              reason: 'seed=$seed session=$number balance',
            );
          }
          expect(
            store.attendances.where((a) => a.sessionId == session.id),
            hasLength(5),
          );
          expect(store.attendanceCount(session.id), present.length);
          final summary = store.sessionFinancialSummary(session.id);
          expect(summary.totalCollected, expectedNet);
          expect(summary.expectedCash, expectedCash);
          expect(
            summary.lines.fold<int>(0, (sum, row) => sum + row.total),
            expectedNet,
          );
          await store.finalizeSession(
            sessionId: session.id,
            actualCash: max(0, expectedCash),
          );
          closings[session.id] = store.closings.last.summary.toJson();
          if (number % 6 == 0) {
            await restart();
            for (final closing in store.closings) {
              expect(closing.summary.toJson(), closings[closing.sessionId]);
            }
            expect(
              store.allPayments.fold<int>(0, (sum, row) => sum + row.netAmount),
              totalPurchases,
            );
            expect(
              store.refunds.fold<int>(0, (sum, row) => sum + row.amount),
              totalRefunds,
            );
            for (final id in ids) {
              expect(store.remainingFor(id, groupId), balances[id]);
            }
          }
        }
      },
      timeout: const Timeout(Duration(minutes: 3)),
    );
  }
}
