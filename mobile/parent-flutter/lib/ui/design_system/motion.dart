import 'package:flutter/material.dart';

abstract final class MassarMotion {
  static const enter = Duration(milliseconds: 560);
  static const change = Duration(milliseconds: 280);
  static const curve = Curves.easeOutQuart;
  static Duration duration(BuildContext context, Duration value) =>
      MediaQuery.disableAnimationsOf(context) ? Duration.zero : value;
}

class Entrance extends StatelessWidget {
  final Widget child;
  final int order;
  const Entrance({super.key, required this.child, this.order = 0});
  @override
  Widget build(BuildContext context) {
    if (MediaQuery.disableAnimationsOf(context)) return child;
    final delay = (order.clamp(0, 5) * .07);
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: MassarMotion.enter,
      child: child,
      builder: (context, value, child) {
        final t = MassarMotion.curve.transform(
          ((value - delay) / (1 - delay)).clamp(0, 1),
        );
        return Opacity(
          opacity: t,
          child: Transform.translate(
            offset: Offset(0, 18 * (1 - t)),
            child: child,
          ),
        );
      },
    );
  }
}

class MassarRouteTransition extends PageTransitionsBuilder {
  const MassarRouteTransition();
  @override
  Widget buildTransitions<T>(
    PageRoute<T> route,
    BuildContext context,
    Animation<double> animation,
    Animation<double> secondaryAnimation,
    Widget child,
  ) {
    if (MediaQuery.disableAnimationsOf(context)) return child;
    final eased = animation.drive(CurveTween(curve: MassarMotion.curve));
    return FadeTransition(
      opacity: eased,
      child: SlideTransition(
        position: eased.drive(
          Tween(begin: const Offset(.04, 0), end: Offset.zero),
        ),
        child: child,
      ),
    );
  }
}
