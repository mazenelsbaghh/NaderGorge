import 'package:flutter/material.dart';
import 'tokens.dart';
import 'motion.dart';
import 'dart:math' as math;

class ProgressArc extends StatelessWidget {
  final double progress;
  final String caption, detail;
  final String? valueLabel;
  const ProgressArc({
    super.key,
    required this.progress,
    required this.caption,
    required this.detail,
    this.valueLabel,
  });
  @override
  Widget build(BuildContext context) => Semantics(
    label: '$caption، ${valueLabel ?? '${(progress * 100).round()}%'}، $detail',
    child: ExcludeSemantics(
      child: Column(
        children: [
          SizedBox(
            height:
                184 + (MediaQuery.textScalerOf(context).scale(48) - 48) * 2.5,
            width: 290,
            child: Stack(
              alignment: Alignment.bottomCenter,
              children: [
                Positioned.fill(
                  child: TweenAnimationBuilder<double>(
                    tween: Tween(begin: 0, end: progress),
                    duration: MassarMotion.duration(
                      context,
                      const Duration(milliseconds: 850),
                    ),
                    curve: MassarMotion.curve,
                    builder: (context, value, _) =>
                        CustomPaint(painter: _ArcPainter(value)),
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        valueLabel ?? '${(progress * 100).round()}%',
                        textDirection: TextDirection.ltr,
                        style: TextStyle(
                          fontSize: valueLabel == null ? 48 : 38,
                          color: Theme.of(context).colorScheme.onSurface,
                          fontWeight: FontWeight.w900,
                        ),
                      ),
                      Text(
                        caption,
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 8),
          Text(detail, textAlign: TextAlign.center),
        ],
      ),
    ),
  );
}

class _ArcPainter extends CustomPainter {
  final double progress;
  _ArcPainter(this.progress);
  @override
  void paint(Canvas canvas, Size size) {
    final rect = Rect.fromLTWH(16, 20, size.width - 32, (size.width - 32));
    final paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round
      ..strokeWidth = 18
      ..color = MassarTokens.teal.withValues(alpha: .1);
    canvas.drawArc(rect, math.pi, math.pi, false, paint);
    if (progress > 0) {
      canvas.drawArc(
        rect,
        math.pi,
        math.pi * progress.clamp(0, 1),
        false,
        paint
          ..color = Colors.white
          ..shader = SweepGradient(
            startAngle: math.pi,
            endAngle: math.pi + math.pi * progress.clamp(.001, 1),
            colors: const [
              Color(0xFF62D0C5),
              Color(0xFF00959F),
              Color(0xFF35B9B4),
            ],
            stops: const [0, .55, 1],
          ).createShader(rect),
      );
      final angle = math.pi + math.pi * progress.clamp(0, 1);
      final end = Offset(
        rect.center.dx + rect.width / 2 * math.cos(angle),
        rect.center.dy + rect.height / 2 * math.sin(angle),
      );
      canvas.drawCircle(end, 10, Paint()..color = Colors.white);
      canvas.drawCircle(end, 7, Paint()..color = MassarTokens.teal);
    }
  }

  @override
  bool shouldRepaint(_ArcPainter oldDelegate) =>
      oldDelegate.progress != progress;
}

class ProgressRing extends StatelessWidget {
  final double? progress;
  final String label;
  final double size;
  const ProgressRing({
    super.key,
    required this.progress,
    required this.label,
    this.size = 60,
  });
  @override
  Widget build(BuildContext context) => SizedBox(
    width: size,
    height: size,
    child: Stack(
      alignment: Alignment.center,
      children: [
        SizedBox.expand(
          child: TweenAnimationBuilder<double>(
            tween: Tween(begin: 0, end: progress?.clamp(0, 1) ?? 0),
            duration: MassarMotion.duration(
              context,
              const Duration(milliseconds: 700),
            ),
            curve: MassarMotion.curve,
            builder: (context, value, _) =>
                CustomPaint(painter: _RingPainter(value)),
          ),
        ),
        Padding(
          padding: const EdgeInsets.all(7),
          child: Text(
            label,
            textAlign: TextAlign.center,
            style: const TextStyle(fontWeight: FontWeight.w900),
          ),
        ),
      ],
    ),
  );
}

class _RingPainter extends CustomPainter {
  final double progress;
  _RingPainter(this.progress);
  @override
  void paint(Canvas canvas, Size size) {
    final rect = (Offset.zero & size).deflate(3);
    final paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 5
      ..strokeCap = StrokeCap.round
      ..color = MassarTokens.teal.withValues(alpha: .1);
    canvas.drawOval(rect, paint);
    if (progress <= 0) return;
    canvas.drawArc(
      rect,
      -math.pi / 2,
      math.pi * 2 * progress,
      false,
      paint
        ..color = Colors.white
        ..shader = SweepGradient(
          transform: const GradientRotation(-math.pi / 2),
          startAngle: 0,
          endAngle: math.pi * 2 * progress,
          colors: const [
            Color(0xFF62D0C5),
            Color(0xFF00959F),
            Color(0xFF35B9B4),
          ],
          stops: const [0, .55, 1],
        ).createShader(rect),
    );
  }

  @override
  bool shouldRepaint(_RingPainter oldDelegate) =>
      oldDelegate.progress != progress;
}
