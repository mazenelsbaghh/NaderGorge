import 'package:flutter/material.dart';
import 'tokens.dart';
import 'motion.dart';

class PrimaryAction extends StatefulWidget {
  final String label;
  final VoidCallback? onPressed;
  final bool busy;
  const PrimaryAction({
    super.key,
    required this.label,
    required this.onPressed,
    this.busy = false,
  });
  @override
  State<PrimaryAction> createState() => _PrimaryActionState();
}

class _PrimaryActionState extends State<PrimaryAction> {
  bool pressed = false;
  @override
  Widget build(BuildContext context) => Listener(
    onPointerDown: (_) => setState(() => pressed = true),
    onPointerUp: (_) => setState(() => pressed = false),
    onPointerCancel: (_) => setState(() => pressed = false),
    child: AnimatedScale(
      scale: pressed && !widget.busy ? .975 : 1,
      duration: MassarMotion.duration(
        context,
        const Duration(milliseconds: 120),
      ),
      child: DecoratedBox(
        decoration: const BoxDecoration(
          borderRadius: BorderRadius.all(Radius.circular(40)),
          gradient: LinearGradient(
            colors: [Color(0xFF153857), MassarTokens.navy],
          ),
        ),
        child: SizedBox(
          width: double.infinity,
          child: FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: Colors.transparent,
              disabledBackgroundColor: Colors.transparent,
              foregroundColor: Colors.white,
              disabledForegroundColor: Colors.white70,
            ),
            onPressed: widget.busy ? null : widget.onPressed,
            child: AnimatedSwitcher(
              duration: MassarMotion.duration(context, MassarMotion.change),
              child: widget.busy
                  ? const SizedBox.square(
                      key: ValueKey('busy'),
                      dimension: 22,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        color: Colors.white,
                      ),
                    )
                  : Text(
                      widget.label,
                      key: ValueKey(widget.label),
                      textAlign: TextAlign.center,
                    ),
            ),
          ),
        ),
      ),
    ),
  );
}
