import 'package:flutter/material.dart';
import 'dart:math' as math;
import 'tokens.dart';
import 'surfaces.dart';
import 'brand.dart';

class MassarPage extends StatelessWidget {
  final String title;
  final Widget body;
  const MassarPage({super.key, required this.title, required this.body});
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      toolbarHeight: 82,
      automaticallyImplyLeading: false,
      title: Row(
        children: [
          Semantics(label: title, child: const BrandLogo(width: 120)),
          const Spacer(),
          IconButton.filledTonal(
            tooltip: 'رجوع',
            onPressed: () => Navigator.maybePop(context),
            icon: const Icon(
              Icons.arrow_forward_rounded,
              textDirection: TextDirection.ltr,
            ),
          ),
        ],
      ),
      backgroundColor: Colors.transparent,
      surfaceTintColor: Colors.transparent,
    ),
    body: body,
  );
}

class IconBadge extends StatelessWidget {
  final IconData icon;
  final Color color;
  final double size;
  const IconBadge(
    this.icon, {
    super.key,
    this.color = MassarTokens.teal,
    this.size = 46,
  });
  @override
  Widget build(BuildContext context) => Container(
    width: size,
    height: size,
    decoration: BoxDecoration(
      color: color.withValues(alpha: .09),
      borderRadius: BorderRadius.circular(size * .35),
    ),
    child: icon == Icons.science_outlined
        ? CustomPaint(
            painter: _AtomPainter(color),
            size: Size.square(size * .65),
          )
        : Icon(
            icon,
            color: Theme.of(context).brightness == Brightness.dark
                ? Theme.of(context).colorScheme.secondary
                : color,
            size: size * .52,
          ),
  );
}

class StudentBanner extends StatelessWidget {
  final String name, grade;
  final VoidCallback? onTap;
  const StudentBanner({
    super.key,
    required this.name,
    this.grade = '',
    this.onTap,
  });
  @override
  Widget build(BuildContext context) => SoftPanel(
    tint: MassarTokens.mint,
    child: InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(MassarTokens.radius),
      child: Row(
        children: [
          const IconBadge(Icons.person_rounded, size: 58),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(name, style: Theme.of(context).textTheme.titleLarge),
                if (grade.isNotEmpty) Text(grade),
              ],
            ),
          ),
          if (onTap != null) const Icon(Icons.keyboard_arrow_down_rounded),
        ],
      ),
    ),
  );
}

class PillTabs<T> extends StatelessWidget {
  final Map<T, String> options;
  final T selected;
  final ValueChanged<T> onChanged;
  const PillTabs({
    super.key,
    required this.options,
    required this.selected,
    required this.onChanged,
  });
  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(5),
    decoration: BoxDecoration(
      color: Theme.of(context).colorScheme.surface,
      borderRadius: BorderRadius.circular(32),
    ),
    child: LayoutBuilder(
      builder: (context, constraints) {
        final large = MediaQuery.textScalerOf(context).scale(14) > 20;
        final children = options.entries
            .map(
              (e) => Semantics(
                selected: e.key == selected,
                child: TextButton(
                  style: TextButton.styleFrom(
                    foregroundColor: e.key == selected
                        ? Colors.white
                        : Theme.of(context).colorScheme.onSurface,
                    backgroundColor: e.key == selected
                        ? MassarTokens.teal
                        : Colors.transparent,
                    minimumSize: const Size(48, 44),
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 12,
                    ),
                    shape: const StadiumBorder(),
                  ),
                  onPressed: () => onChanged(e.key),
                  child: Text(e.value, textAlign: TextAlign.center),
                ),
              ),
            )
            .toList();
        return large
            ? Wrap(spacing: 4, runSpacing: 4, children: children)
            : Row(children: children.map((c) => Expanded(child: c)).toList());
      },
    ),
  );
}

class StatPair extends StatelessWidget {
  final String firstLabel, firstValue, secondLabel, secondValue;
  const StatPair({
    super.key,
    required this.firstLabel,
    required this.firstValue,
    required this.secondLabel,
    required this.secondValue,
  });
  @override
  Widget build(BuildContext context) => Row(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      for (final item in [(firstLabel, firstValue), (secondLabel, secondValue)])
        Expanded(
          child: Padding(
            padding: const EdgeInsets.all(8),
            child: Column(
              children: [
                Text(item.$1, textAlign: TextAlign.center),
                const SizedBox(height: 6),
                Text(
                  item.$2,
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.titleLarge,
                ),
              ],
            ),
          ),
        ),
    ],
  );
}

class ContextBanner extends StatelessWidget {
  final String title, subtitle;
  final IconData icon;
  const ContextBanner({
    super.key,
    required this.title,
    required this.subtitle,
    required this.icon,
  });
  @override
  Widget build(BuildContext context) => Container(
    decoration: BoxDecoration(
      border: Border.all(
        color: Theme.of(context).colorScheme.surface,
        width: 1.5,
      ),
      borderRadius: BorderRadius.circular(28),
    ),
    clipBehavior: Clip.antiAlias,
    child: MassarBackdrop(
      panel: true,
      child: Padding(
        padding: const EdgeInsets.all(22),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: Theme.of(context).textTheme.headlineMedium,
                  ),
                  if (subtitle.isNotEmpty)
                    Text(
                      subtitle,
                      style: Theme.of(context).textTheme.bodyLarge,
                    ),
                ],
              ),
            ),
            const SizedBox(width: 16),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                border: Border.all(
                  color: Theme.of(context).colorScheme.surface,
                  width: 1.5,
                ),
                borderRadius: BorderRadius.circular(24),
              ),
              child: IconBadge(icon, size: 60),
            ),
          ],
        ),
      ),
    ),
  );
}

class AnswerPanel extends StatelessWidget {
  final String label, answer;
  final IconData icon;
  final Color color;
  const AnswerPanel({
    super.key,
    required this.label,
    required this.answer,
    required this.icon,
    required this.color,
  });
  @override
  Widget build(BuildContext context) => Container(
    width: double.infinity,
    margin: const EdgeInsets.only(top: 10),
    padding: const EdgeInsets.all(14),
    decoration: BoxDecoration(
      color: color.withValues(alpha: .07),
      borderRadius: BorderRadius.circular(20),
    ),
    child: Row(
      children: [
        IconBadge(icon, color: color),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: TextStyle(
                  color: Theme.of(context).brightness == Brightness.dark
                      ? Theme.of(context).colorScheme.secondary
                      : color,
                  fontWeight: FontWeight.w700,
                ),
              ),
              Text(answer, style: Theme.of(context).textTheme.bodyLarge),
            ],
          ),
        ),
      ],
    ),
  );
}

class MetricRow extends StatelessWidget {
  final String title, subtitle;
  final IconData icon;
  const MetricRow(
    this.title, {
    super.key,
    required this.subtitle,
    required this.icon,
  });
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 8),
    child: Row(
      children: [
        IconBadge(icon, size: 52),
        const SizedBox(width: 16),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: Theme.of(context).textTheme.bodyLarge),
              const SizedBox(height: 5),
              Text(subtitle, style: Theme.of(context).textTheme.titleLarge),
            ],
          ),
        ),
      ],
    ),
  );
}

class _AtomPainter extends CustomPainter {
  final Color color;
  _AtomPainter(this.color);
  @override
  void paint(Canvas canvas, Size size) {
    canvas.translate(size.width / 2, size.height / 2);
    final paint = Paint()
      ..color = color
      ..strokeWidth = 2
      ..style = PaintingStyle.stroke;
    for (var i = 0; i < 3; i++) {
      canvas.save();
      canvas.rotate(i * math.pi / 3);
      canvas.drawOval(
        Rect.fromCenter(
          center: Offset.zero,
          width: size.width * .82,
          height: size.height * .32,
        ),
        paint,
      );
      canvas.restore();
    }
    canvas.drawCircle(Offset.zero, size.width * .06, Paint()..color = color);
  }

  @override
  bool shouldRepaint(_AtomPainter oldDelegate) => oldDelegate.color != color;
}
