import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/lan_firewall_repair.dart';
import 'package:path/path.dart' as p;

Future<String> runPowerShell(String script) async {
  final encoded = base64Encode([
    for (final unit in script.codeUnits) ...[unit & 255, unit >> 8],
  ]);
  final completed = await Process.run(
    p.join(
      Platform.environment['SystemRoot'] ?? r'C:\Windows',
      'System32',
      'WindowsPowerShell',
      'v1.0',
      'powershell.exe',
    ),
    ['-NoProfile', '-NonInteractive', '-EncodedCommand', encoded],
  );
  expect(
    completed.exitCode,
    0,
    reason: 'Windows fixture inspection must succeed.',
  );
  return completed.stdout as String;
}

void main() {
  test(
    'Windows in-app launcher repairs an Arabic folder and returns elevated failures',
    () async {
      final directory = await Directory.systemTemp.createTemp(
        'massar-button-test-',
      );
      final folder = await Directory(
        p.join(await directory.resolveSymbolicLinks(), "نسخة ' \$() الهوست"),
      ).create();
      final executable = File(p.join(folder.path, 'massar-lan-host.exe'));
      final native = Platform.environment['MASSAR_LAN_TEST_EXECUTABLE'];
      expect(
        native,
        isNotNull,
        reason: 'Test the exact native gateway selected for packaging.',
      );
      await File(native!).copy(executable.path);
      final helper = await File(
        'scripts/repair-lan-firewall.ps1',
      ).copy(p.join(folder.path, 'repair-lan-firewall.ps1'));
      final encodedPath = base64Encode(
        utf8.encode(p.normalize(executable.absolute.path)),
      );
      final filters =
          r'''$env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules')
$ErrorActionPreference = 'Stop'
'''
          "\$program = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('$encodedPath'))\n"
          r'''
$rules = @(Get-NetFirewallApplicationFilter -PolicyStore PersistentStore | Where-Object { $_.Program -eq $program } | Get-NetFirewallRule)
''';
      addTearDown(() async {
        await runPowerShell('$filters\n\$rules | Remove-NetFirewallRule');
        await directory.delete(recursive: true);
      });
      final manifest = File(p.join(folder.path, 'build-manifest.json'));
      await manifest.writeAsString('{"role":"client"}');
      await expectLater(
        LanFirewallRepair.repair(applicationDirectory: folder.path),
        throwsA(
          isA<CenterException>().having(
            (e) => e.diagnosticCode,
            'secondary rejected',
            11,
          ),
        ),
      );
      expect(
        int.parse(
          (await runPowerShell('$filters\nWrite-Output \$rules.Count')).trim(),
        ),
        0,
      );
      await manifest.writeAsString('{"role":"host"}');
      await LanFirewallRepair.repair(applicationDirectory: folder.path);
      await LanFirewallRepair.repair(applicationDirectory: folder.path);
      expect(
        int.parse(
          (await runPowerShell('$filters\nWrite-Output \$rules.Count')).trim(),
        ),
        2,
      );
      await helper.writeAsString('exit 12');
      await expectLater(
        LanFirewallRepair.repair(applicationDirectory: folder.path),
        throwsA(
          isA<CenterException>().having(
            (e) => e.diagnosticCode,
            'elevated failure retained',
            12,
          ),
        ),
      );
      await helper.writeAsString('throw "synthetic worker failure"');
      await expectLater(
        LanFirewallRepair.repair(applicationDirectory: folder.path),
        throwsA(
          isA<CenterException>().having(
            (e) => e.diagnosticCode,
            'worker exception cannot report success',
            21,
          ),
        ),
      );
    },
    skip: !Platform.isWindows,
    timeout: const Timeout(Duration(minutes: 2)),
  );
}
