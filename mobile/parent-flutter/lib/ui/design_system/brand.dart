import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

class BrandLogo extends StatelessWidget {
  final double width;
  const BrandLogo({super.key, this.width = 82});
  @override
  Widget build(BuildContext context) => Semantics(
    label: 'مسار أكاديمي',
    image: true,
    child: Container(
      decoration: BoxDecoration(
        color: Theme.of(context).brightness == Brightness.dark
            ? Colors.white
            : Colors.transparent,
        borderRadius: BorderRadius.circular(12),
      ),
      child: SvgPicture.asset(
        'assets/logo.svg',
        width: width,
        height: width * .62,
        fit: BoxFit.contain,
      ),
    ),
  );
}
