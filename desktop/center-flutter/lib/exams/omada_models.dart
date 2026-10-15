import 'dart:io';

import '../domain/models.dart';

/// Secrets stay in memory or the Windows user-protected profile, never logs.
class OmadaControllerConfig {
  const OmadaControllerConfig({
    required this.baseUri,
    required this.omadaId,
    required this.clientId,
    required this.clientSecret,
    this.certificateSha256,
  });

  final Uri baseUri;
  final String omadaId;
  final String clientId;
  final String clientSecret;
  final String? certificateSha256;
}

class OmadaException extends CenterException {
  const OmadaException(super.message, {required this.code});
  final String code;
}

class OmadaCertificateInfo {
  const OmadaCertificateInfo({
    required this.sha256,
    required this.subject,
    required this.issuer,
    required this.validFrom,
    required this.validUntil,
  });
  final String sha256;
  final String subject;
  final String issuer;
  final DateTime validFrom;
  final DateTime validUntil;
}

class OmadaSite {
  const OmadaSite({required this.id, required this.name});
  final String id;
  final String name;
}

class OmadaAccessPoint {
  const OmadaAccessPoint({
    required this.mac,
    required this.name,
    required this.model,
    required this.status,
    required this.detailStatus,
  });
  final String mac;
  final String name;
  final String model;
  final int status;
  final int detailStatus;
  bool get isConnected => status == 1;
}

class OmadaWlanGroup {
  const OmadaWlanGroup({required this.id, required this.name});
  final String id;
  final String name;
}

class OmadaSsid {
  const OmadaSsid({
    required this.id,
    required this.name,
    required this.band,
    required this.security,
    required this.broadcast,
    required this.guestNetwork,
    required this.vlanEnabled,
  });
  final String id;
  final String name;
  final int band;
  final int security;
  final bool broadcast;
  final bool guestNetwork;
  final bool vlanEnabled;
}

/// Display-only metadata. The implementation retains the secret and snapshot.
class OmadaStaffWifiPreview {
  const OmadaStaffWifiPreview({
    required this.siteId,
    required this.wlanId,
    required this.accessPointMac,
    required this.staffName,
    required this.preservedSsids,
  });
  final String siteId;
  final String wlanId;
  final String accessPointMac;
  final String staffName;
  final List<OmadaSsid> preservedSsids;
}

class OmadaStaffWifiResult {
  const OmadaStaffWifiResult({
    required this.ssid,
    required this.recoveredAfterUncertainCreate,
  });
  final OmadaSsid ssid;
  final bool recoveredAfterUncertainCreate;
  bool get existingSsidsUnchanged => true;
  // Controller readback cannot prove over-the-air broadcasting or host access.
  bool get physicalBroadcastVerified => false;
}

typedef OmadaControllerServiceFactory =
    OmadaControllerService Function(OmadaControllerConfig config);

abstract interface class OmadaControllerService {
  Future<OmadaCertificateInfo> probeCertificate(Uri baseUri);
  Future<List<OmadaSite>> listSites();
  Future<List<OmadaAccessPoint>> listAccessPoints(String siteId);
  Future<List<OmadaWlanGroup>> listWlanGroups(String siteId);
  Future<List<OmadaSsid>> listSsids(String siteId, String wlanId);
  Future<OmadaStaffWifiPreview> previewStaffWifi({
    required String siteId,
    required String wlanId,
    required String accessPointMac,
    required String studentSsidId,
    required String staffName,
    required String staffPassword,
  });
  Future<OmadaStaffWifiResult> applyStaffWifi(
    OmadaStaffWifiPreview preview, {
    Future<void> Function()? beforeWrite,
  });
  void close();
}

/// Only explicit local Controller origins are accepted, with no URL credentials.
/// Literal addresses avoid resolving a saved controller name to a public host.
void validateOmadaControllerUri(Uri uri) {
  final host = uri.host;
  final address = InternetAddress.tryParse(host);
  final bytes = address?.rawAddress;
  final local =
      host == 'localhost' ||
      (address?.isLoopback ?? false) ||
      (bytes != null &&
          bytes.length == 4 &&
          (bytes[0] == 10 ||
              (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
              (bytes[0] == 192 && bytes[1] == 168))) ||
      (bytes != null && bytes.length == 16 && (bytes[0] & 0xfe) == 0xfc);
  if (uri.scheme != 'https' ||
      !local ||
      uri.userInfo.isNotEmpty ||
      uri.hasQuery ||
      uri.hasFragment ||
      (uri.path.isNotEmpty && uri.path != '/') ||
      uri.port < 1 ||
      uri.port > 65535) {
    throw const OmadaException(
      'اكتب عنوان HTTPS المحلي للكنترولر فقط، مثل https://127.0.0.1:8043 أو عنوانه داخل الشبكة.',
      code: 'invalid_controller_address',
    );
  }
}
