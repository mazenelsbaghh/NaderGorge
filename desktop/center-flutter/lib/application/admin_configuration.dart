import 'dart:convert';
import 'package:flutter/services.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import '../data/center_state.dart';
import 'center_store.dart';

Future<InstallationAdmin> loadInstallationAdmin() async {
  final source = await rootBundle.loadString('assets/admin_account.json');
  try {
    return InstallationAdmin.fromJson(
      Map<String, dynamic>.from(jsonDecode(source) as Map),
    );
  } on FormatException catch (error) {
    throw CenterException('تعذر قراءة إعداد حساب المدير.', cause: error);
  }
}

Future<CenterStore> openInstalledCenter({
  String? directory,
  InstallationAdmin? admin,
  bool clientOnly = false,
}) async {
  if (clientOnly) {
    final base =
        directory ??
        p.join((await getApplicationSupportDirectory()).path, 'massar-center');
    return CenterStore.clientWorkspace(directory: base);
  }
  final configuration = admin ?? await loadInstallationAdmin();
  final store = await CenterStore.open(
    directory: directory,
    initialState: loadInstallationSeed,
  );
  try {
    await store.ensureInstallationAdmin(configuration);
    return store;
  } catch (_) {
    await store.close();
    rethrow;
  }
}

Future<CenterState> loadInstallationSeed() async {
  final source = await rootBundle.loadString('assets/installation_seed.json');
  try {
    final bundle = Map<String, dynamic>.from(jsonDecode(source) as Map);
    if (bundle['version'] != 1 || bundle['state'] is! Map) {
      throw const FormatException('Unsupported installation seed');
    }
    return CenterState.fromJson(
      Map<String, dynamic>.from(bundle['state'] as Map),
    );
  } on FormatException catch (error) {
    throw CenterException('تعذر قراءة بيانات التثبيت الأول.', cause: error);
  }
}
