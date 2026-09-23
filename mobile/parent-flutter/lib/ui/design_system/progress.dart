import 'package:flutter/material.dart';
import 'tokens.dart';
import 'dart:math' as math;

class ProgressArc extends StatelessWidget {
  final double progress;
  final String caption, detail;
  const ProgressArc({
    super.key,
    required this.progress,
    required this.caption,
    required this.detail,
  });
  @override
  Widget build(BuildContext context) => Semantics(
    label: '$caption، ${(progress * 100).round()}%، $detail',
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
                  child: CustomPaint(painter: _ArcPainter(progress)),
                ),
                Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        '${(progress * 100).round()}%',
                        style: const TextStyle(
                          fontSize: 48,
                          color: MassarTokens.teal,
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
        paint..color = MassarTokens.teal,
      );
    }
  }

  @override
  bool shouldRepaint(_ArcPainter oldDelegate) =>
      oldDelegate.progress != progress;
}
