import 'package:flutter/material.dart';

abstract final class MassarColors {
  static const navy = Color(0xFF0A1D3D);
  static const teal = Color(0xFF0E8F8F);
  static const canvas = Color(0xFFF6F7F8);
  static const ink = Color(0xFF2E3A47);
  static const line = Color(0xFFDCE1E6);
}

/// Semantic colors for surfaces and text in both appearance modes.
class MassarPalette {
  const MassarPalette({
    required this.canvas,
    required this.surface,
    required this.ink,
    required this.muted,
    required this.line,
    required this.accent,
    required this.subtle,
    required this.success,
    required this.successSurface,
    required this.warning,
    required this.warningSurface,
    required this.error,
    required this.errorSurface,
    required this.nav,
    required this.textOnNav,
  });
  final Color canvas, surface, ink, muted, line, accent, subtle;
  final Color success, successSurface, warning, warningSurface;
  final Color error, errorSurface, nav, textOnNav;

  Color get tableHeader =>
      Color.alphaBlend(accent.withValues(alpha: .18), surface);
  Color get tableRow =>
      Color.alphaBlend(accent.withValues(alpha: .035), surface);

  static MassarPalette of(BuildContext context) =>
      Theme.of(context).brightness == Brightness.dark ? dark : light;

  static const light = MassarPalette(
    canvas: Color(0xFFF6F7F8),
    surface: Color(0xFFFFFFFF),
    ink: Color(0xFF0A1D3D),
    muted: Color(0xFF4C596A),
    line: Color(0xFFDCE1E6),
    accent: Color(0xFF087B7B),
    subtle: Color(0xFFEEF1F4),
    success: Color(0xFF0B695F),
    successSurface: Color(0xFFE8F4F2),
    warning: Color(0xFF75500B),
    warningSurface: Color(0xFFFFF5DD),
    error: Color(0xFF9F2121),
    errorSurface: Color(0xFFFFEEEE),
    nav: Color(0xFF0A1D3D),
    textOnNav: Color(0xFFE6EDF5),
  );
  static const dark = MassarPalette(
    canvas: Color(0xFF0C1220),
    surface: Color(0xFF141E2F),
    ink: Color(0xFFE6EDF5),
    muted: Color(0xFFADBCCD),
    line: Color(0xFF344257),
    accent: Color(0xFF64D9CC),
    subtle: Color(0xFF1E2B40),
    success: Color(0xFF77DFC4),
    successSurface: Color(0xFF15352D),
    warning: Color(0xFFFFD58A),
    warningSurface: Color(0xFF3C2C17),
    error: Color(0xFFFFADAA),
    errorSurface: Color(0xFF3B222B),
    nav: Color(0xFF0A1020),
    textOnNav: Color(0xFFE6EDF5),
  );
}

abstract final class MassarTheme {
  static ThemeData get light => _build(Brightness.light, MassarPalette.light);
  static ThemeData get dark => _build(Brightness.dark, MassarPalette.dark);

  static ThemeData _build(Brightness brightness, MassarPalette colors) {
    final scheme =
        ColorScheme.fromSeed(
          seedColor: MassarColors.teal,
          brightness: brightness,
        ).copyWith(
          primary: colors.accent,
          onPrimary: brightness == Brightness.dark
              ? const Color(0xFF092625)
              : Colors.white,
          secondary: colors.accent,
          surface: colors.surface,
          onSurface: colors.ink,
          onSurfaceVariant: colors.muted,
          outline: colors.line,
          outlineVariant: colors.line,
          error: colors.error,
          errorContainer: colors.errorSurface,
          onErrorContainer: colors.error,
        );
    final shape = RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(8),
    );
    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      fontFamily: 'Tajawal',
      scaffoldBackgroundColor: colors.canvas,
      dividerColor: colors.line,
      appBarTheme: AppBarTheme(
        backgroundColor: colors.surface,
        foregroundColor: colors.ink,
        scrolledUnderElevation: 0,
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: colors.surface,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
        enabledBorder: OutlineInputBorder(
          borderSide: BorderSide(color: colors.line),
        ),
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 14,
          vertical: 14,
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
          shape: shape,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
          shape: shape,
        ),
      ),
      dataTableTheme: DataTableThemeData(
        headingRowColor: WidgetStatePropertyAll(colors.tableHeader),
        dataRowColor: WidgetStateProperty.resolveWith((states) {
          if (states.contains(WidgetState.selected)) {
            return colors.successSurface;
          }
          if (states.contains(WidgetState.hovered)) return colors.subtle;
          return colors.tableRow;
        }),
        headingRowHeight: 42,
        dataRowMinHeight: 40,
        dataRowMaxHeight: 64,
        dataTextStyle: TextStyle(
          fontFamily: 'Tajawal',
          fontSize: 15,
          color: colors.ink,
        ),
        headingTextStyle: TextStyle(
          fontFamily: 'Tajawal',
          fontSize: 15,
          fontWeight: FontWeight.w700,
          color: colors.ink,
        ),
        dividerThickness: 0.6,
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: colors.surface,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.all(Radius.circular(12)),
        ),
      ),
    );
  }
}
