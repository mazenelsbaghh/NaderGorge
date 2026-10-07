part of 'center_store.dart';

class InstallationAdmin {
  InstallationAdmin({
    required this.id,
    required this.name,
    required Map<String, String> credential,
  }) : credential = Map.unmodifiable(credential) {
    if (id.trim().isEmpty ||
        name.trim().isEmpty ||
        credential['algorithm'] != 'pbkdf2-sha256-120000') {
      throw const CenterException('إعداد حساب مدير التثبيت غير مكتمل.');
    }
    try {
      if (base64Decode(credential['salt'] ?? '').length != 24 ||
          base64Decode(credential['hash'] ?? '').length != 32) {
        throw const FormatException('Invalid credential shape');
      }
    } on FormatException catch (error) {
      throw CenterException('إعداد حساب مدير التثبيت غير صالح.', cause: error);
    }
  }

  final String id;
  final String name;
  final Map<String, String> credential;

  factory InstallationAdmin.fromJson(Map<String, dynamic> value) {
    if (value['version'] != 1 ||
        value['id'] is! String ||
        value['name'] is! String ||
        value['credential'] is! Map) {
      throw const CenterException('إصدار إعداد مدير التثبيت غير مدعوم.');
    }
    return InstallationAdmin(
      id: value['id'] as String,
      name: value['name'] as String,
      credential: Map<String, String>.from(value['credential'] as Map),
    );
  }
}

extension InstallationAdminProvisioning on CenterStore {
  String? get installationAdminName => _installationAdmin?.name;

  Future<void> ensureInstallationAdmin(InstallationAdmin admin) async {
    _installationAdmin = admin;
    if (_installationAdminMatches(_state)) return;
    final actor = _state.staff
        .where(
          (u) =>
              u.id == admin.id ||
              u.name.toLowerCase() == admin.name.toLowerCase(),
        )
        .firstOrNull;
    await _change('installation_admin', 'تجهيز حساب مدير التثبيت', () async {
      _applyInstallationAdmin(_state);
    }, actorId: actor?.id ?? admin.id);
  }

  bool _installationAdminMatches(CenterState state) {
    final config = _installationAdmin;
    if (config == null) return true;
    final matches = state.staff.where(
      (u) =>
          u.id == config.id ||
          u.name.toLowerCase() == config.name.toLowerCase(),
    );
    if (matches.length != 1) return false;
    final user = matches.single;
    return user.name == config.name &&
        user.role == StaffRole.admin &&
        mapEquals(state.credentials[user.id], config.credential);
  }

  bool _applyInstallationAdmin(CenterState state) {
    final config = _installationAdmin;
    if (config == null || _installationAdminMatches(state)) return false;
    final matches = state.staff
        .where(
          (u) =>
              u.id == config.id ||
              u.name.toLowerCase() == config.name.toLowerCase(),
        )
        .toList();
    if (matches.length > 1) {
      throw const CenterException(
        'حساب مدير التثبيت يتعارض مع حسابين في النسخة. لم تتغير البيانات.',
      );
    }
    final user = StaffUser(
      id: matches.isEmpty ? config.id : matches.single.id,
      name: config.name,
      role: StaffRole.admin,
    );
    final index = state.staff.indexWhere((u) => u.id == user.id);
    if (index < 0) {
      state.staff.add(user);
    } else {
      state.staff[index] = user;
    }
    state.credentials[user.id] = Map.of(config.credential);
    return true;
  }
}
