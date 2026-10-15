import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/exams/exam_admin_theme.dart';

void main() {
  test(
    'embedded WhatsApp navigation preserves local admin capability and theme',
    () {
      final address = examAdminEmbeddedAddress(
        'http://127.0.0.1:8766/?view=room#center-access=synthetic-capability',
        batchId: 'report-batch_1',
        brightness: Brightness.dark,
      );
      expect(address.fragment, 'center-access=synthetic-capability');
      expect(address.queryParameters, {
        'view': 'whatsapp-center',
        'batch': 'report-batch_1',
        'embedded': 'center',
        'theme': 'dark',
      });
      final settings = address.replace(
        queryParameters: {...address.queryParameters, 'view': 'whatsapp'},
      );
      expect(settings.host, '127.0.0.1');
      expect(settings.queryParameters['embedded'], 'center');
      final shell = examAdminEmbeddedAddress(
        'http://127.0.0.1:8766/#center-access=synthetic-capability',
        brightness: Brightness.light,
        nativeShell: true,
      );
      expect(shell.queryParameters['native-shell'], '1');
      expect(shell.fragment, 'center-access=synthetic-capability');
    },
  );

  test(
    'admin handoff refuses external origins and malformed batch identity',
    () {
      for (final address in [
        'https://127.0.0.1:8766/',
        'http://example.com:8766/',
        'http://127.0.0.1:8767/',
        'http://someone@127.0.0.1:8766/',
      ]) {
        expect(
          () => examAdminEmbeddedAddress(address, brightness: Brightness.light),
          throwsA(isA<CenterException>()),
          reason: address,
        );
      }
      expect(
        () => examAdminEmbeddedAddress(
          'http://127.0.0.1:8766/',
          batchId: '../other?view=room',
          brightness: Brightness.light,
        ),
        throwsA(isA<CenterException>()),
      );
    },
  );
}
