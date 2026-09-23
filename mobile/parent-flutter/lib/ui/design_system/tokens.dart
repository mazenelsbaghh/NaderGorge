import 'package:flutter/material.dart';
import 'motion.dart';

abstract final class MassarTokens {
  static const navy = Color(0xFF0A1D3D), teal = Color(0xFF0E8F8F);
  static const mint = Color(0xFFE3F4F0), canvas = Color(0xFFF6F7F8);
  static const ink = Color(0xFF2E3A47),
      warning = Color(0xFF956009),
      danger = Color(0xFFB42336);
  static const gap = 16.0, inset = 20.0, radius = 24.0, controlHeight = 52.0;
  static ThemeData theme(Brightness brightness) {
    final dark = brightness == Brightness.dark;
    final scheme = ColorScheme.fromSeed(seedColor: teal, brightness: brightness)
        .copyWith(
          primary: dark ? const Color(0xFF83D6CD) : navy,
          secondary: dark ? const Color(0xFF83D6CD) : teal,
          surface: dark ? const Color(0xFF14243A) : Colors.white,
          onSurface: dark ? const Color(0xFFF2F5F7) : navy,
        );
    return ThemeData(
      useMaterial3: true,
      pageTransitionsTheme: const PageTransitionsTheme(
        builders: {
          TargetPlatform.android: MassarRouteTransition(),
          TargetPlatform.iOS: MassarRouteTransition(),
        },
      ),
      colorScheme: scheme,
      brightness: brightness,
      fontFamily: 'Tajawal',
      appBarTheme: const AppBarTheme(
        backgroundColor: Colors.transparent,
        surfaceTintColor: Colors.transparent,
        toolbarHeight: 78,
      ),
      scaffoldBackgroundColor: Colors.transparent,
      textTheme: TextTheme(
        headlineLarge: TextStyle(
          fontSize: 30,
          fontWeight: FontWeight.w700,
          color: scheme.onSurface,
        ),
        headlineMedium: TextStyle(
          fontSize: 24,
          fontWeight: FontWeight.w700,
          color: scheme.onSurface,
        ),
        titleLarge: TextStyle(
          fontSize: 20,
          fontWeight: FontWeight.w700,
          color: scheme.onSurface,
        ),
        bodyLarge: TextStyle(
          fontSize: 16,
          height: 1.5,
          color: scheme.onSurface,
        ),
        bodyMedium: TextStyle(
          fontSize: 14,
          height: 1.5,
          color: dark ? Colors.white70 : ink,
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(48, controlHeight),
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
          shape: const StadiumBorder(),
          textStyle: const TextStyle(
            fontFamily: 'Tajawal',
            fontSize: 17,
            fontWeight: FontWeight.w700,
          ),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: scheme.surface,
        contentPadding: const EdgeInsets.all(18),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(radius),
          borderSide: BorderSide.none,
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(radius),
          borderSide: const BorderSide(color: teal, width: 2),
        ),
      ),
      dividerTheme: DividerThemeData(
        color: scheme.onSurface.withValues(alpha: .08),
        space: 28,
      ),
    );
  }
}
