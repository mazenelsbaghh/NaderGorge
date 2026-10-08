import 'dart:convert';

import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/data/center_state.dart';

// Every local open checks these legacy bundles. Tests use unrelated identities
// so fixture databases never read private repair/import records from a checkout.
final syntheticRepairAssets = <String, Object>{
  for (final name in [
    'data_repair_20261002',
    'data_repair_20261004',
    'cairo_codes_20261004',
    'gec_codes_20261004',
    'gec_duplicate_codes_20261004',
    'academic_import_20261004',
    'cairo_academic_import_20261004',
  ])
    'assets/$name.json': {
      'id': 'synthetic-$name',
      'installationSeedId': 'unrelated-synthetic-installation',
      'requiredSessionId': 'unrelated-synthetic-session',
      'updates': <Object>[],
    },
};

void installSyntheticRepairAssets() => _installAssets(syntheticRepairAssets);

/// Only the installer asset boundary is synthetic; provisioning uses real SQLite.
void installSyntheticInstallerAssets(InstallationAdmin admin) {
  final assets = <String, Object>{
    ...syntheticRepairAssets,
    'assets/admin_account.json': {
      'version': 1,
      'id': admin.id,
      'name': admin.name,
      'credential': admin.credential,
    },
    'assets/installation_seed.json': {
      'version': 1,
      'state': CenterState().toJson(),
    },
  };
  _installAssets(assets);
}

void _installAssets(Map<String, Object> assets) {
  final messenger =
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger;
  for (final path in assets.keys) {
    rootBundle.evict(path);
  }
  messenger.setMockMessageHandler('flutter/assets', (message) async {
    final path = const StringCodec().decodeMessage(message);
    final fixture = assets[path];
    if (fixture != null) {
      return ByteData.sublistView(
        Uint8List.fromList(utf8.encode(jsonEncode(fixture))),
      );
    }
    if (path != null && path.startsWith('assets/') && path.endsWith('.json')) {
      throw StateError(
        'Installer test requested an unconfigured private asset: $path',
      );
    }
    return messenger.delegate.send('flutter/assets', message);
  });
  addTearDown(() {
    messenger.setMockMessageHandler('flutter/assets', null);
    for (final path in assets.keys) {
      rootBundle.evict(path);
    }
  });
}
