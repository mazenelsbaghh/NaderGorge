import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/features/exams/exam_admin_navigation.dart';

void main() {
  test(
    'native navigation accepts only the current document and known visual routes',
    () {
      const document = '0123456789abcdef0123456789abcdef';
      final message = <String, dynamic>{
        'kind': 'massar-center-navigation',
        'nonce': 'native-instance',
        'documentId': document,
        'action': 'state',
        'destination': 'exams',
      };
      expect(
        examAdminNavigationMessage(
          message,
          nonce: 'native-instance',
          documentId: document,
        )?['destination'],
        'exams',
      );
      for (final invalid in [
        {...message, 'kind': 'massar-export'},
        {...message, 'nonce': 'previous-instance'},
        {...message, 'documentId': 'abcdef0123456789abcdef0123456789'},
        {...message, 'destination': '/api/backup'},
        {...message, 'action': 'execute'},
        {
          ...message,
          'action': 'leave-result',
          'allowed': true,
          'requestId': '1',
        },
        {...message, 'action': 'leave-result', 'allowed': true, 'requestId': 0},
      ]) {
        expect(
          examAdminNavigationMessage(
            invalid,
            nonce: 'native-instance',
            documentId: document,
          ),
          isNull,
        );
      }
      expect(
        examAdminNavigationMessage({
          ...message,
          'action': 'ready',
        }, nonce: 'native-instance')?['documentId'],
        document,
      );
    },
  );
}
