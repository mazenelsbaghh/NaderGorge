import 'package:flutter/material.dart';
import 'tokens.dart';

class MassarBottomNavigation extends StatelessWidget {
  final int selectedIndex;
  final ValueChanged<int> onChanged;
  const MassarBottomNavigation({
    super.key,
    required this.selectedIndex,
    required this.onChanged,
  });
  static const destinations = [
    (Icons.home_rounded, 'الرئيسية'),
    (Icons.menu_book_rounded, 'الحصص'),
    (Icons.bar_chart_rounded, 'النتائج'),
    (Icons.more_horiz_rounded, 'المزيد'),
  ];
  @override
  Widget build(BuildContext context) => SafeArea(
    top: false,
    child: Padding(
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 10),
      child: Container(
        padding: const EdgeInsets.all(8),
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surface,
          borderRadius: BorderRadius.circular(32),
        ),
        child: Row(
          children: List.generate(destinations.length, (index) {
            final selected = selectedIndex == index;
            final color = selected
                ? Colors.white
                : Theme.of(context).colorScheme.onSurface;
            return Expanded(
              child: Semantics(
                selected: selected,
                button: true,
                label: destinations[index].$2,
                child: Material(
                  color: selected ? MassarTokens.navy : Colors.transparent,
                  borderRadius: BorderRadius.circular(26),
                  child: InkWell(
                    borderRadius: BorderRadius.circular(26),
                    onTap: () => onChanged(index),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        vertical: 12,
                        horizontal: 2,
                      ),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(destinations[index].$1, color: color),
                          const SizedBox(height: 5),
                          Text(
                            destinations[index].$2,
                            style: TextStyle(
                              color: color,
                              fontSize: 12,
                              fontWeight: selected
                                  ? FontWeight.w700
                                  : FontWeight.w500,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            );
          }),
        ),
      ),
    ),
  );
}
