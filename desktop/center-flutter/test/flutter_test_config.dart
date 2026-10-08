import 'dart:async';

import 'package:flutter_test/flutter_test.dart';

import 'helpers/synthetic_installer_assets.dart';

class _AssetFixtureTestBinding extends AutomatedTestWidgetsFlutterBinding {
  // LAN tests use controlled loopback servers; cloud tests inject HTTP fakes.
  @override
  bool get overrideHttpClient => false;
}

Future<void> testExecutable(FutureOr<void> Function() testMain) async {
  _AssetFixtureTestBinding();
  setUpAll(installSyntheticRepairAssets);
  setUp(installSyntheticRepairAssets);
  await testMain();
}
