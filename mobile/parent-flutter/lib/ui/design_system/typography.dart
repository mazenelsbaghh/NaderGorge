import 'package:flutter/material.dart';

class PageHeading extends StatelessWidget {
  final String title;
  final String? subtitle;
  const PageHeading(this.title, {super.key, this.subtitle});
  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(title, style: Theme.of(context).textTheme.headlineLarge),
      if (subtitle != null && subtitle!.isNotEmpty)
        Text(subtitle!, style: Theme.of(context).textTheme.bodyLarge),
      const SizedBox(height: 20),
    ],
  );
}

class SectionHeading extends StatelessWidget {
  final String title;
  final VoidCallback onTap;
  const SectionHeading(this.title, {super.key, required this.onTap});
  @override
  Widget build(BuildContext context) => Row(
    children: [
      Expanded(
        child: Text(title, style: Theme.of(context).textTheme.titleLarge),
      ),
      TextButton(onPressed: onTap, child: const Text('عرض الكل')),
    ],
  );
}
