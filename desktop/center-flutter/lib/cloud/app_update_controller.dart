import 'dart:async';
import 'dart:convert';
import 'dart:ffi';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:flutter/foundation.dart';
import 'package:path/path.dart' as path;

import '../domain/models.dart' show CenterException;
import '../shared/app_build_metadata.dart';
import '../shared/problem_reporting.dart';
import 'cloud_support_settings.dart';

enum AppUpdateStatus {
  unconfigured,
  unsupported,
  idle,
  checking,
  upToDate,
  downloading,
  downloaded,
  offline,
  unauthorized,
  error,
}

enum AppPackageRole { host, client }

/// Downloads verified release archives; never installs or opens an archive.
class AppUpdateController extends ChangeNotifier {
  AppUpdateController({
    required Directory directory,
    required Future<CloudSupportConfiguration?> Function() configuration,
    HttpClient Function()? httpClientFactory,
  }) : _directory = directory.absolute,
       _httpClientFactory = httpClientFactory ?? HttpClient.new,
       _configuration = configuration,
       _packageRole = null,
       _packagePlatform = null;

  AppUpdateController._package({
    required Directory directory,
    required Future<CloudSupportConfiguration?> Function() configuration,
    required HttpClient Function() httpClientFactory,
    required AppPackageRole role,
    required String? platform,
  }) : _directory = directory.absolute,
       _configuration = configuration,
       _httpClientFactory = httpClientFactory,
       _packageRole = role,
       _packagePlatform = platform;

  /// A transferable package is independent of the version installed here.
  AppUpdateController createPackageDownloader(AppPackageRole target) =>
      AppUpdateController._package(
        directory: Directory(
          path.join(_directory.path, 'packages', target.name),
        ),
        configuration: _configuration,
        httpClientFactory: _httpClientFactory,
        role: target,
        platform: platform,
      );

  static const _maximumManifestBytes = 256 * 1024;
  static const _maximumDownloadBytes = 2 * 1024 * 1024 * 1024;
  static const _networkTimeout = Duration(seconds: 45);
  final Directory _directory;
  final Future<CloudSupportConfiguration?> Function() _configuration;
  final HttpClient Function() _httpClientFactory;
  final AppPackageRole? _packageRole;
  final String? _packagePlatform;
  Timer? _timer;
  HttpClient? _client;
  Future<void>? _checkingFuture;
  bool _initialized = false;
  bool _closed = false;
  int _failures = 0;
  AppUpdateStatus _status = AppUpdateStatus.idle;
  _UpdateManifest? _manifest;
  String? _downloadedPath;
  String? _lastError;
  String? _restoredOrigin;
  double? _progress;

  AppUpdateStatus get status => _status;
  String? get availableVersion => _manifest?.version;
  String? get releaseNotes => _manifest?.notes;
  String? get downloadedPath => _downloadedPath;
  String? get lastError => _lastError;
  double? get progress => _progress;
  bool get checking => _checkingFuture != null;
  String get role => _packageRole?.name ?? AppBuildMetadata.role;
  String get installedVersion => AppBuildMetadata.version;
  String get installedBuild => AppBuildMetadata.buildIdentifier;
  String? get platform => _packageRole != null
      ? _packagePlatform
      : switch (Abi.current()) {
          Abi.windowsX64 when Platform.isWindows => 'windows-x64',
          Abi.macosArm64 when Platform.isMacOS => 'macos-arm64',
          _ => null,
        };

  String get statusLabel => switch (_status) {
    AppUpdateStatus.unconfigured => 'اربط الجهاز بمسار لمتابعة التحديثات',
    AppUpdateStatus.unsupported => 'لا توجد حزمة تحديث لهذا النظام',
    AppUpdateStatus.idle =>
      _packageRole != null ? 'جاهز لتنزيل النسخة' : 'متابعة التحديثات جاهزة',
    AppUpdateStatus.checking => 'جارٍ البحث عن تحديث',
    AppUpdateStatus.upToDate =>
      _packageRole != null
          ? 'لا توجد حزمة منشورة لهذه النسخة حاليًا'
          : 'لا يوجد إصدار أحدث متاح',
    AppUpdateStatus.downloading => 'جارٍ تنزيل التحديث',
    AppUpdateStatus.downloaded =>
      _packageRole != null
          ? 'النسخة جاهزة للحفظ والإرسال؛ لم يتم تثبيتها'
          : 'التحديث جاهز كملف؛ لم يتم تثبيته',
    AppUpdateStatus.offline =>
      _packageRole != null
          ? 'تعذر الاتصال؛ أعد المحاولة عند عودة الإنترنت'
          : 'سنحاول مجددًا عند عودة الاتصال',
    AppUpdateStatus.unauthorized => 'راجع ربط الجهاز بمسار',
    AppUpdateStatus.error => 'تعذرت متابعة التحديث',
  };

  Future<void> initialize() async {
    if (_initialized || _closed) return;
    _initialized = true;
    await checkNow();
  }

  Future<void> checkNow() {
    if (_closed) return Future<void>.value();
    _timer?.cancel();
    return _checkingFuture ??= _check().whenComplete(() {
      _checkingFuture = null;
      _schedule();
      _notify();
    });
  }

  Future<void> _check() async {
    _lastError = null;
    _progress = null;
    _setStatus(AppUpdateStatus.checking);
    try {
      final configuration = await _configuration();
      if (_closed) return;
      if (configuration == null) {
        _manifest = null;
        _downloadedPath = null;
        _restoredOrigin = null;
        _setStatus(AppUpdateStatus.unconfigured);
        return;
      }
      final releasePlatform = platform;
      if (releasePlatform == null) {
        _setStatus(AppUpdateStatus.unsupported);
        return;
      }
      _requireOrigin(configuration.origin);
      await _restoreDownloaded(configuration);
      if (_closed) return;
      _client = _httpClientFactory()
        ..connectionTimeout = _networkTimeout
        ..autoUncompress = true;
      final response = await _get(
        configuration,
        '/v1/updates/$releasePlatform/$role',
      );
      if (response.statusCode == HttpStatus.noContent) {
        _manifest = null;
        _downloadedPath = null;
        _failures = 0;
        _setStatus(AppUpdateStatus.upToDate);
        return;
      }
      _requireResponse(response);
      final manifest = _UpdateManifest.fromJson(
        jsonDecode(utf8.decode(await _manifestBytes(response))),
      );
      if (manifest.platform != releasePlatform || manifest.role != role) {
        throw const FormatException('Release target mismatch');
      }
      if (_manifest?.sha256 != manifest.sha256) _downloadedPath = null;
      _manifest = manifest;
      if (_packageRole == null && !_newerThanInstalled(manifest)) return;
      await _download(configuration, manifest);
      if (_closed) return;
      _failures = 0;
      _setStatus(AppUpdateStatus.downloaded);
    } on _UpdateAuthorizationException catch (error, stack) {
      _failed(
        error,
        stack,
        AppUpdateStatus.unauthorized,
        'تعذر التحقق من ربط الجهاز بمسار. أعد مراجعة الإعدادات.',
      );
    } on SocketException catch (error, stack) {
      _offline(error, stack);
    } on HandshakeException catch (error, stack) {
      _offline(error, stack);
    } on HttpException catch (error, stack) {
      _offline(error, stack);
    } on TimeoutException catch (error, stack) {
      _offline(error, stack);
    } on FileSystemException catch (error, stack) {
      _failed(
        error,
        stack,
        AppUpdateStatus.error,
        'تعذر حفظ التحديث. راجع مساحة القرص ومكان الحفظ.',
      );
    } on FormatException catch (error, stack) {
      _failed(
        error,
        stack,
        AppUpdateStatus.error,
        'بيانات التحديث أو بصمة الملف غير صحيحة. لم يتم تثبيت شيء.',
      );
    } on _UpdateResponseException catch (error, stack) {
      _failed(
        error,
        stack,
        AppUpdateStatus.error,
        'خدمة التحديث غير متاحة الآن.',
      );
    } on CenterException catch (error, stack) {
      _failed(
        error,
        stack,
        AppUpdateStatus.error,
        'تعذر قراءة ربط الجهاز بمسار. راجع الإعدادات.',
      );
    } on TypeError catch (error, stack) {
      _failed(
        error,
        stack,
        AppUpdateStatus.error,
        'بيانات ربط التحديث غير صالحة. راجع الإعدادات.',
      );
    } on ArgumentError catch (error, stack) {
      _failed(
        error,
        stack,
        AppUpdateStatus.error,
        'بيانات طلب التحديث غير صالحة. راجع الإعدادات.',
      );
    } on StateError catch (error, stack) {
      _failed(
        error,
        stack,
        AppUpdateStatus.error,
        'تعذرت متابعة التحديث الآن. سنحاول لاحقًا.',
      );
    } on _UpdateCanceledException {
      // Closing the application aborts download requests without an error notice.
    } finally {
      _client?.close(force: true);
      _client = null;
    }
  }

  bool _newerThanInstalled(_UpdateManifest manifest) {
    if (installedVersion == 'development') {
      _lastError = 'نسخة تطوير؛ لا يمكن مقارنة إصدارها وتنزيل تحديث تلقائيًا.';
      _setStatus(AppUpdateStatus.error);
      return false;
    }
    final comparison = _ReleaseVersion.parse(
      manifest.version,
    ).compareTo(_ReleaseVersion.parse(installedVersion));
    if (comparison == 0 && manifest.build != installedBuild) {
      _lastError = 'رقم إصدار التحديث مكرر؛ يحتاج نشره برقم أحدث.';
      _setStatus(AppUpdateStatus.error);
      return false;
    }
    if (comparison <= 0 || manifest.build == installedBuild) {
      _downloadedPath = null;
      _failures = 0;
      _setStatus(AppUpdateStatus.upToDate);
      return false;
    }
    return true;
  }

  Future<HttpClientResponse> _get(
    CloudSupportConfiguration configuration,
    String requestPath,
  ) async {
    if (_closed) throw const _UpdateCanceledException();
    final uri = configuration.origin.resolve(requestPath);
    if (uri.origin != configuration.origin.origin) {
      throw const FormatException('Update origin mismatch');
    }
    final request = await _client!.getUrl(uri).timeout(_networkTimeout);
    request.followRedirects = false;
    request.headers.set(HttpHeaders.acceptEncodingHeader, 'identity');
    request.headers.set(
      HttpHeaders.userAgentHeader,
      'Massar-Center/${AppBuildMetadata.version}',
    );
    request.headers.set(
      HttpHeaders.authorizationHeader,
      'Bearer ${configuration.deviceToken}',
    );
    return request.close().timeout(_networkTimeout);
  }

  static void _requireOrigin(Uri origin) {
    if (origin.scheme != 'https' ||
        origin.host.isEmpty ||
        origin.userInfo.isNotEmpty ||
        origin.hasQuery ||
        origin.hasFragment ||
        (origin.path.isNotEmpty && origin.path != '/')) {
      throw const FormatException('Invalid update origin');
    }
  }

  static void _requireResponse(HttpClientResponse response) {
    if (response.statusCode == HttpStatus.unauthorized ||
        response.statusCode == HttpStatus.forbidden) {
      throw const _UpdateAuthorizationException();
    }
    if (response.statusCode != HttpStatus.ok) {
      throw const _UpdateResponseException();
    }
  }

  static Future<List<int>> _manifestBytes(HttpClientResponse response) async {
    if (response.contentLength > _maximumManifestBytes) {
      throw const FormatException('Update manifest too large');
    }
    final bytes = <int>[];
    await for (final chunk in response.timeout(_networkTimeout)) {
      if (bytes.length + chunk.length > _maximumManifestBytes) {
        throw const FormatException('Update manifest too large');
      }
      bytes.addAll(chunk);
    }
    return bytes;
  }

  Future<void> _download(
    CloudSupportConfiguration configuration,
    _UpdateManifest manifest,
  ) async {
    await _requireDirectory();
    await _directory.create(recursive: true);
    final archive = File(
      path.join(
        _directory.path,
        '${manifest.releaseId}-${manifest.sha256}.zip',
      ),
    );
    if (await archive.exists()) {
      await _verifyArchive(archive, manifest);
      await _requireSameConfiguration(configuration);
      await _saveReceipt(configuration, manifest, archive);
      return;
    }
    _progress = 0;
    _setStatus(AppUpdateStatus.downloading);
    final staging = await _directory.createTemp('.massar-update-');
    try {
      final temporary = File(path.join(staging.path, 'release.zip'));
      final response = await _get(configuration, manifest.downloadPath);
      _requireResponse(response);
      if (response.compressionState !=
              HttpClientResponseCompressionState.decompressed &&
          response.contentLength >= 0 &&
          response.contentLength != manifest.size) {
        throw const FormatException('Update length mismatch');
      }
      await _writeDownload(temporary, response, manifest);
      if (_closed) return;
      await _requireSameConfiguration(configuration);
      if (await archive.exists()) {
        await _verifyArchive(archive, manifest);
      } else {
        await temporary.rename(archive.path);
      }
      await _saveReceipt(configuration, manifest, archive);
    } finally {
      try {
        await staging.delete(recursive: true);
      } on FileSystemException catch (error, stack) {
        reportProblem(error, stack, operation: 'cloud.updates');
      }
    }
  }

  Future<void> _writeDownload(
    File temporary,
    HttpClientResponse response,
    _UpdateManifest manifest,
  ) async {
    final file = await temporary.open(mode: FileMode.writeOnly);
    final digest = _DigestReceiver();
    final hash = sha256.startChunkedConversion(digest);
    var written = 0;
    try {
      await for (final chunk in response.timeout(_networkTimeout)) {
        written += chunk.length;
        if (_closed ||
            written > manifest.size ||
            written > _maximumDownloadBytes) {
          throw const FormatException(
            'Update download interrupted or oversized',
          );
        }
        await file.writeFrom(chunk);
        hash.add(chunk);
        final fraction = written / manifest.size;
        if (fraction - (_progress ?? 0) >= 0.01) {
          _progress = fraction;
          _notify();
        }
      }
      hash.close();
      if (written != manifest.size ||
          digest.value.toString() != manifest.sha256) {
        throw const FormatException('Update digest mismatch');
      }
      await file.flush();
    } finally {
      await file.close();
    }
  }

  static Future<void> _verifyArchive(
    File archive,
    _UpdateManifest manifest,
  ) async {
    if (await FileSystemEntity.type(archive.path, followLinks: false) !=
            FileSystemEntityType.file ||
        await archive.length() != manifest.size ||
        (await sha256.bind(archive.openRead()).single).toString() !=
            manifest.sha256) {
      throw const FormatException('Saved update digest mismatch');
    }
  }

  Future<void> _requireSameConfiguration(
    CloudSupportConfiguration expected,
  ) async {
    if (_closed) throw const _UpdateCanceledException();
    final current = await _configuration();
    if (current?.origin != expected.origin ||
        current?.deviceToken != expected.deviceToken ||
        current?.centerId != expected.centerId) {
      throw const FormatException('Update configuration changed');
    }
  }

  Future<void> _restoreDownloaded(
    CloudSupportConfiguration configuration,
  ) async {
    if (_restoredOrigin == configuration.origin.origin) return;
    _restoredOrigin = configuration.origin.origin;
    _manifest = null;
    _downloadedPath = null;
    final receipt = File(path.join(_directory.path, 'downloaded-update.json'));
    try {
      await _requireDirectory();
      final type = await FileSystemEntity.type(
        receipt.path,
        followLinks: false,
      );
      if (type == FileSystemEntityType.notFound) return;
      if (type != FileSystemEntityType.file ||
          await receipt.length() > 16 * 1024) {
        throw const FormatException('Invalid downloaded update receipt');
      }
      final saved = jsonDecode(await receipt.readAsString());
      if (saved is! Map ||
          saved['schema'] != 1 ||
          saved['origin'] != configuration.origin.origin) {
        return;
      }
      final manifest = _UpdateManifest.fromJson(saved);
      if (manifest.platform != platform ||
          manifest.role != role ||
          (_packageRole == null &&
              (installedVersion == 'development' ||
                  _ReleaseVersion.parse(
                        manifest.version,
                      ).compareTo(_ReleaseVersion.parse(installedVersion)) <=
                      0))) {
        return;
      }
      final archive = File(
        path.join(
          _directory.path,
          '${manifest.releaseId}-${manifest.sha256}.zip',
        ),
      );
      await _verifyArchive(archive, manifest);
      if (_closed) return;
      _manifest = manifest;
      _downloadedPath = archive.path;
    } on FileSystemException catch (error, stack) {
      reportProblem(error, stack, operation: 'cloud.updates');
    } on FormatException catch (error, stack) {
      reportProblem(error, stack, operation: 'cloud.updates');
    }
  }

  Future<void> _requireDirectory() async {
    final type = await FileSystemEntity.type(
      _directory.path,
      followLinks: false,
    );
    if (type != FileSystemEntityType.notFound &&
        type != FileSystemEntityType.directory) {
      throw const FormatException('Invalid update directory');
    }
  }

  String? get suggestedFilename => _manifest == null
      ? null
      : 'massar-$role-${_manifest!.platform}-${_manifest!.version}.zip';

  Future<void> saveDownloadedTo(String destination) async {
    final downloaded = _downloadedPath;
    final manifest = _manifest;
    if (downloaded == null || manifest == null) {
      throw const CenterException('نزّل النسخة أولًا ثم احفظها لإرسالها.');
    }
    if (path.extension(destination).toLowerCase() != '.zip') {
      throw const CenterException('احفظ نسخة البرنامج بامتداد ZIP.');
    }
    await _verifyArchive(File(downloaded), manifest);
    if (path.equals(path.absolute(destination), path.absolute(downloaded))) {
      return;
    }
    final type = await FileSystemEntity.type(destination, followLinks: false);
    if (type != FileSystemEntityType.notFound &&
        type != FileSystemEntityType.file) {
      throw const CenterException('اختر ملف ZIP عاديًا لحفظ النسخة.');
    }
    await File(downloaded).copy(destination);
  }

  Future<void> _saveReceipt(
    CloudSupportConfiguration configuration,
    _UpdateManifest manifest,
    File archive,
  ) async {
    if (_closed) return;
    final staging = await _directory.createTemp('.massar-update-receipt-');
    try {
      final temporary = File(path.join(staging.path, 'receipt.json'));
      await temporary.writeAsString(
        jsonEncode({
          'schema': 1,
          'origin': configuration.origin.origin,
          ...manifest.toJson(),
          'filename': path.basename(archive.path),
          'downloadedAt': DateTime.now().toUtc().toIso8601String(),
        }),
        flush: true,
      );
      await temporary.rename(
        path.join(_directory.path, 'downloaded-update.json'),
      );
      _downloadedPath = archive.path;
      _progress = 1;
    } finally {
      try {
        await staging.delete(recursive: true);
      } on FileSystemException catch (error, stack) {
        reportProblem(error, stack, operation: 'cloud.updates');
      }
    }
  }

  void _offline(Object error, StackTrace stack) => _failed(
    error,
    stack,
    _downloadedPath == null
        ? AppUpdateStatus.offline
        : AppUpdateStatus.downloaded,
    _packageRole != null
        ? 'تعذر الاتصال. اضغط تنزيل للمحاولة مجددًا؛ الملف المكتمل يظل قابلًا للحفظ.'
        : 'تعذر الاتصال. سنحاول مجددًا دون تغيير الداتا.',
  );

  void _failed(
    Object error,
    StackTrace stack,
    AppUpdateStatus status,
    String message,
  ) {
    if (_closed) return;
    _failures = (_failures + 1).clamp(0, 5);
    _lastError = message;
    final reason = switch (error) {
      _UpdateAuthorizationException() => 2101,
      _UpdateResponseException() => 2102,
      FormatException() => 2103,
      SocketException() || TimeoutException() => 2104,
      _ => 2199,
    };
    reportProblem(
      CenterException('Update failure', cause: error, diagnosticCode: reason),
      stack,
      operation: 'cloud.updates',
    );
    _setStatus(status);
  }

  void _schedule() {
    if (_closed || _packageRole != null) return;
    final seconds = _failures == 0
        ? 300
        : (30 * (1 << (_failures - 1))).clamp(30, 60);
    _timer = Timer(Duration(seconds: seconds), () => unawaited(checkNow()));
  }

  void _setStatus(AppUpdateStatus status) {
    _status = status;
    _notify();
  }

  void _notify() {
    if (!_closed) notifyListeners();
  }

  Future<void> close() async {
    _closed = true;
    _timer?.cancel();
    _client?.close(force: true);
    await _checkingFuture;
  }

  @override
  void dispose() {
    _closed = true;
    _timer?.cancel();
    _client?.close(force: true);
    super.dispose();
  }
}

class _UpdateManifest {
  const _UpdateManifest(
    this.releaseId,
    this.version,
    this.build,
    this.platform,
    this.role,
    this.size,
    this.sha256,
    this.downloadPath,
    this.notes,
  );

  final String releaseId, version, build, platform, role, sha256, downloadPath;
  final String? notes;
  final int size;

  factory _UpdateManifest.fromJson(Object? json) {
    if (json is! Map ||
        !_matches(json['releaseId'], r'[A-Za-z0-9_-]{1,64}') ||
        !AppBuildMetadata.isValidVersion(json['version']) ||
        json['version'] == 'development' ||
        !_matches(json['build'], r'[0-9a-f]{16}') ||
        !['windows-x64', 'macos-arm64'].contains(json['platform']) ||
        !['host', 'client'].contains(json['role']) ||
        json['size'] is! int ||
        (json['size'] as int) < 1 ||
        (json['size'] as int) > AppUpdateController._maximumDownloadBytes ||
        !_matches(json['sha256'], r'[0-9a-f]{64}') ||
        !_matches(
          json['downloadPath'],
          r'/v1/releases/[A-Za-z0-9][A-Za-z0-9._-]{0,199}\.zip',
        ) ||
        (json['notes'] != null &&
            (json['notes'] is! String ||
                (json['notes'] as String).length > 2048))) {
      throw const FormatException('Invalid update manifest');
    }
    _ReleaseVersion.parse(json['version'] as String);
    return _UpdateManifest(
      json['releaseId'] as String,
      json['version'] as String,
      json['build'] as String,
      json['platform'] as String,
      json['role'] as String,
      json['size'] as int,
      json['sha256'] as String,
      json['downloadPath'] as String,
      json['notes'] as String?,
    );
  }

  static bool _matches(Object? text, String pattern) =>
      text is String &&
      text.length <= 256 &&
      RegExp('^$pattern\$').firstMatch(text)?.end == text.length;

  Map<String, Object?> toJson() => {
    'releaseId': releaseId,
    'version': version,
    'build': build,
    'platform': platform,
    'role': role,
    'size': size,
    'sha256': sha256,
    'downloadPath': downloadPath,
    if (notes != null) 'notes': notes,
  };
}

class _ReleaseVersion implements Comparable<_ReleaseVersion> {
  _ReleaseVersion(this.parts, this.prerelease, this.build);
  final List<int> parts;
  final List<String> prerelease;
  final int build;

  factory _ReleaseVersion.parse(String version) {
    final buildSplit = version.split('+');
    final nameSplit = buildSplit.first.split('-');
    final prerelease = nameSplit.length > 1
        ? nameSplit.skip(1).join('-').split('.')
        : <String>[];
    if (prerelease.any((part) => part.isEmpty)) {
      throw const FormatException('Invalid release prerelease');
    }
    return _ReleaseVersion(
      nameSplit.first.split('.').map(int.parse).toList(),
      prerelease,
      int.parse(buildSplit.last),
    );
  }

  @override
  int compareTo(_ReleaseVersion other) {
    for (var index = 0; index < 3; index++) {
      final comparison = parts[index].compareTo(other.parts[index]);
      if (comparison != 0) return comparison;
    }
    if (prerelease.isEmpty != other.prerelease.isEmpty) {
      return prerelease.isEmpty ? 1 : -1;
    }
    final count = prerelease.length < other.prerelease.length
        ? prerelease.length
        : other.prerelease.length;
    for (var index = 0; index < count; index++) {
      final left = prerelease[index], right = other.prerelease[index];
      final leftNumber = int.tryParse(left), rightNumber = int.tryParse(right);
      final comparison = leftNumber != null && rightNumber != null
          ? leftNumber.compareTo(rightNumber)
          : leftNumber != null
          ? -1
          : rightNumber != null
          ? 1
          : left.compareTo(right);
      if (comparison != 0) return comparison;
    }
    final comparison = prerelease.length.compareTo(other.prerelease.length);
    return comparison != 0 ? comparison : build.compareTo(other.build);
  }
}

class _DigestReceiver implements Sink<Digest> {
  Digest? value;
  @override
  void add(Digest digest) => value = digest;
  @override
  void close() {}
}

class _UpdateAuthorizationException implements Exception {
  const _UpdateAuthorizationException();
}

class _UpdateResponseException implements Exception {
  const _UpdateResponseException();
}

class _UpdateCanceledException implements Exception {
  const _UpdateCanceledException();
}
