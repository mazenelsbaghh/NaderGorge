import 'package:flutter/material.dart';
import 'tokens.dart';
import 'motion.dart';
import 'dart:math' as math;

class SoftPanel extends StatelessWidget {
  final Widget child;
  final Color? tint;
  const SoftPanel({super.key, required this.child, this.tint});
  @override
  Widget build(BuildContext context) => Container(
    width: double.infinity,
    padding: const EdgeInsets.all(MassarTokens.inset),
    decoration: BoxDecoration(
      color: tint == null
          ? Theme.of(context).colorScheme.surface
          : Theme.of(context).brightness == Brightness.dark
          ? Color.alphaBlend(
              MassarTokens.teal.withValues(alpha: .12),
              Theme.of(context).colorScheme.surface,
            )
          : tint,
      gradient: tint != null && Theme.of(context).brightness == Brightness.light
          ? const LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [Color(0xFFD7F0E9), Color(0xFFF0FAF7)],
            )
          : null,
      border: Border.all(
        color: Theme.of(context).colorScheme.onSurface.withValues(alpha: .025),
      ),
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
    physics: const AlwaysScrollableScrollPhysics(),
    itemCount: children.length,
    separatorBuilder: (_, _) => const SizedBox(height: MassarTokens.gap),
    itemBuilder: (_, i) => Entrance(order: i, child: children[i]),
  );
}

/// Native vector waves and a deterministic fine paper texture, shared by all routes.
class MassarBackdrop extends StatelessWidget {
  final Widget child;
  final bool panel;
  const MassarBackdrop({super.key, required this.child, this.panel = false});
  @override
  Widget build(BuildContext context) => CustomPaint(
    painter: _TexturePainter(
      dark: Theme.of(context).brightness == Brightness.dark,
      panel: panel,
    ),
    child: child,
  );
}

class _TexturePainter extends CustomPainter {
  final bool dark, panel;
  _TexturePainter({required this.dark, required this.panel});
  @override
  void paint(Canvas canvas, Size size) {
    final rect = Offset.zero & size;
    canvas.save();
    canvas.clipRect(rect);
    canvas.drawRect(
      rect,
      Paint()
        ..shader = RadialGradient(
          center: const Alignment(-1, -.85),
          radius: panel ? 1.8 : 1.2,
          colors: dark
              ? const [Color(0xFF142B36), Color(0xFF091525)]
              : const [Color(0xFFE0F4EE), MassarTokens.canvas],
          stops: const [0, .85],
        ).createShader(rect),
    );
    final w = size.width;
    final h = panel ? size.height : math.min(size.height, size.width * 1.7);
    final wave = Path()
      ..moveTo(0, 0)
      ..lineTo(w * .60, 0)
      ..cubicTo(w * .57, h * .17, w * .27, h * .16, 0, h * .22)
      ..close();
    final second = Path()
      ..moveTo(0, h * .21)
      ..cubicTo(w * .26, h * .12, w * .58, h * .26, w * .77, h * .14)
      ..cubicTo(w * .60, h * .39, w * .17, h * .36, 0, h * .46)
      ..close();
    final shader = LinearGradient(
      begin: Alignment.topLeft,
      end: Alignment.bottomRight,
      colors: [
        const Color(0xFF92D6C8).withValues(alpha: dark ? .10 : .19),
        MassarTokens.mint.withValues(alpha: dark ? .02 : .04),
      ],
    ).createShader(rect);
    canvas.drawPath(wave, Paint()..shader = shader);
    canvas.drawPath(second, Paint()..shader = shader);
    // Fixed seed keeps the grain stable across frames and avoids flicker.
    final random = math.Random(17);
    final grain = Paint()
      ..color = (dark ? Colors.white : MassarTokens.navy).withValues(
        alpha: .018,
      );
    for (var i = 0; i < (size.width * size.height / 35).round(); i++) {
      canvas.drawCircle(
        Offset(
          random.nextDouble() * size.width,
          random.nextDouble() * size.height,
        ),
        .32,
        grain,
      );
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(_TexturePainter oldDelegate) =>
      oldDelegate.dark != dark || oldDelegate.panel != panel;
}
