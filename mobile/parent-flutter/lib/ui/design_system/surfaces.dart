import 'package:flutter/material.dart';
import 'tokens.dart';

class SoftPanel extends StatelessWidget {
  final Widget child;
  final Color? tint;
  const SoftPanel({super.key, required this.child, this.tint});
  @override
  Widget build(BuildContext context) => Container(
    width: double.infinity,
    padding: const EdgeInsets.all(MassarTokens.inset),
    decoration: BoxDecoration(
      color: tint ?? Theme.of(context).colorScheme.surface,
      borderRadius: BorderRadius.circular(MassarTokens.radius),
    ),
    child: child,
  );
}

class WaveCanvas extends StatelessWidget {
  final Widget child;
  const WaveCanvas({super.key, required this.child});
  @override
  Widget build(BuildContext context) => Stack(
    fit: StackFit.expand,
    children: [
      Positioned.fill(child: CustomPaint(painter: _WavePainter())),
      child,
    ],
  );
}

class _WavePainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final rect = Offset.zero & size;
    canvas.drawRect(
      rect,
      Paint()
        ..shader = const LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Color(0x00C8F1E8), Color(0x99C8F1E8)],
        ).createShader(rect),
    );
    for (var i = 0; i < 3; i++) {
      final y = size.height * (.65 + i * .12);
      final path = Path()
        ..moveTo(0, y)
        ..cubicTo(
          size.width * .4,
          y + 150,
          size.width * .8,
          y - 100,
          size.width,
          y - 20,
        )
        ..lineTo(size.width, size.height)
        ..lineTo(0, size.height)
        ..close();
      canvas.drawPath(
        path,
        Paint()
          ..color = (i.isEven ? Colors.white : MassarTokens.mint).withValues(
            alpha: .45,
          ),
      );
    }
  }

  @override
  bool shouldRepaint(_WavePainter oldDelegate) => false;
}

class ScreenBody extends StatelessWidget {
  final List<Widget> children;
  const ScreenBody({super.key, required this.children});
  @override
  Widget build(BuildContext context) => ListView.separated(
    padding: const EdgeInsets.all(MassarTokens.inset),
    itemCount: children.length,
    separatorBuilder: (_, _) => const SizedBox(height: MassarTokens.gap),
    itemBuilder: (_, i) => children[i],
  );
}
