import 'dart:convert';

import 'package:flutter/material.dart';

import '../../domain/models.dart';
import '../../shared/theme.dart';

Uri examAdminEmbeddedAddress(
  String address, {
  String? batchId,
  bool nativeShell = false,
  required Brightness brightness,
}) {
  final uri = Uri.tryParse(address);
  if (uri == null ||
      uri.scheme != 'http' ||
      uri.host != '127.0.0.1' ||
      uri.port != 8766 ||
      uri.userInfo.isNotEmpty ||
      (batchId != null &&
          !RegExp(r'^[A-Za-z0-9_-]{1,128}$').hasMatch(batchId))) {
    throw const CenterException('تعذر فتح الإدارة. أعد تجهيز الشاشة.');
  }
  return uri.replace(
    queryParameters: {
      ...uri.queryParameters,
      'embedded': 'center',
      'theme': brightness.name,
      if (nativeShell) 'native-shell': '1',
      if (batchId != null) ...{'view': 'whatsapp-center', 'batch': batchId},
    },
  );
}

String examAdminThemeScript(ThemeData theme) {
  final colors = theme.brightness == Brightness.dark
      ? MassarPalette.dark
      : MassarPalette.light;
  String hex(Color color) =>
      '#${(color.toARGB32() & 0xffffff).toRadixString(16).padLeft(6, '0')}';
  final palette = <String, Color>{
    'canvas': colors.canvas,
    'surface': colors.surface,
    'ink': colors.ink,
    'muted': colors.muted,
    'line': colors.line,
    'accent': colors.accent,
    'subtle': colors.subtle,
    'success': colors.success,
    'successSurface': colors.successSurface,
    'warning': colors.warning,
    'warningSurface': colors.warningSurface,
    'error': colors.error,
    'errorSurface': colors.errorSurface,
    'nav': colors.nav,
    'textOnNav': colors.textOnNav,
  };
  final payload = jsonEncode({
    'mode': theme.brightness.name,
    'colors': palette.map((role, color) => MapEntry(role, hex(color))),
    'onPrimary': hex(theme.colorScheme.onPrimary),
  });
  return '''(() => {
    if (location.protocol !== 'http:' || location.hostname !== '127.0.0.1' ||
        location.port !== '8766' ||
        new URLSearchParams(location.search).get('embedded') !== 'center') return;
    window.__massarCenterTheme = $payload;
    if (typeof window.applyMassarCenterTheme === 'function') {
      window.applyMassarCenterTheme(window.__massarCenterTheme);
    }
  })();''';
}
