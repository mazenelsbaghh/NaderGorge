import 'package:flutter/material.dart';
import 'tokens.dart';
import 'surfaces.dart';
import 'actions.dart';

class StatusPill extends StatelessWidget {
  final String label;
  final Color color;
  const StatusPill(this.label, {super.key, this.color = MassarTokens.teal});
  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
    decoration: BoxDecoration(
      color: color.withValues(alpha: .12),
      borderRadius: BorderRadius.circular(40),
    ),
    child: Text(
      label,
      style: TextStyle(
        color: Theme.of(context).brightness == Brightness.dark
            ? Theme.of(context).colorScheme.onSurface
            : color,
        fontWeight: FontWeight.w700,
      ),
    ),
  );
}

class MassarDataRow extends StatelessWidget {
  final String title;
  final String? subtitle, trailing;
  final IconData? icon;
  final VoidCallback? onTap;
  const MassarDataRow(
    this.title, {
    super.key,
    this.subtitle,
    this.trailing,
    this.icon,
    this.onTap,
  });
  @override
  Widget build(BuildContext context) => ListTile(
    contentPadding: EdgeInsets.zero,
    minVerticalPadding: 12,
    leading: icon == null
        ? null
        : CircleAvatar(
            backgroundColor: MassarTokens.teal.withValues(alpha: .08),
            child: Icon(icon, color: Theme.of(context).colorScheme.secondary),
          ),
    title: Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
    subtitle: subtitle == null ? null : Text(subtitle!),
    trailing: trailing != null
        ? Text(trailing!)
        : onTap == null
        ? null
        : const Icon(Icons.chevron_left_rounded),
    onTap: onTap,
  );
}

class EmptyPanel extends StatelessWidget {
  final String message;
  final IconData icon;
  const EmptyPanel(this.message, {super.key, this.icon = Icons.inbox_outlined});
  @override
  Widget build(BuildContext context) => SoftPanel(
    child: Padding(
      padding: const EdgeInsets.symmetric(vertical: 32),
      child: Column(
        children: [
          Icon(icon, size: 42, color: Theme.of(context).colorScheme.secondary),
          const SizedBox(height: 16),
          Text(message, textAlign: TextAlign.center),
        ],
      ),
    ),
  );
}

class ErrorPanel extends StatelessWidget {
  final String message;
  final VoidCallback retry;
  const ErrorPanel(this.message, {super.key, required this.retry});
  @override
  Widget build(BuildContext context) => SoftPanel(
    child: Column(
      children: [
        const Icon(
          Icons.cloud_off_rounded,
          size: 42,
          color: MassarTokens.warning,
        ),
        const SizedBox(height: 16),
        Text(message, textAlign: TextAlign.center),
        const SizedBox(height: 20),
        PrimaryAction(label: 'إعادة المحاولة', onPressed: retry),
      ],
    ),
  );
}
