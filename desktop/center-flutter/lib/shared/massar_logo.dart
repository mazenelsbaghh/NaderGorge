import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

/// The original Massar artwork, using its approved light-surface or dark-surface variant.
class MassarLogo extends StatelessWidget {
  const MassarLogo({super.key, this.height = 48, this.onDarkSurface});

  final double height;
  final bool? onDarkSurface;

  @override
  Widget build(BuildContext context) {
    final darkSurface =
        onDarkSurface ?? Theme.of(context).brightness == Brightness.dark;
    return SvgPicture.asset(
      darkSurface ? 'assets/logo-light.svg' : 'assets/logo.svg',
      width: height * 1030 / 650,
      height: height,
      fit: BoxFit.contain,
      semanticsLabel: 'مسار',
      excludeFromSemantics: false,
    );
  }
}
