/// Public compile metadata only; no device identity or application records.
abstract final class AppBuildMetadata {
  static const _configuredVersion = String.fromEnvironment(
    'MASSAR_APP_VERSION',
    defaultValue: 'development',
  );
  static const _configuredBuild = String.fromEnvironment(
    'MASSAR_BUILD_ID',
    defaultValue: 'development',
  );
  static const _clientOnly = bool.fromEnvironment('MASSAR_CLIENT_ONLY');
  static final _versionPattern = RegExp(
    r'^\d{1,6}\.\d{1,6}\.\d{1,6}(?:-[0-9A-Za-z][0-9A-Za-z.-]{0,31})?\+\d{1,10}$',
  );
  static final _buildPattern = RegExp(r'^[0-9a-f]{16}$');

  static final String version = isValidVersion(_configuredVersion)
      ? _configuredVersion
      : 'development';
  static final String buildIdentifier = isValidBuild(_configuredBuild)
      ? _configuredBuild
      : 'development';
  static String get role => _clientOnly ? 'client' : 'host';

  static bool isValidVersion(Object? version) =>
      version == 'development' ||
      version is String &&
          version.length <= 64 &&
          _versionPattern.firstMatch(version)?.end == version.length;

  static bool isValidBuild(Object? build) =>
      build == 'development' ||
      build is String && _buildPattern.firstMatch(build)?.end == build.length;
}
