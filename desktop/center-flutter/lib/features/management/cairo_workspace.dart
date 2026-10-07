import 'package:flutter/material.dart';
import '../../application/center_store.dart';
import '../../shared/workspace_draft_guard.dart';
import 'academics_page.dart';
import 'students_page.dart';
import 'groups_page.dart';
import 'catalogs_page.dart';

/// Cairo has its own academic workspace, separate from cash reception.
class CairoWorkspace extends StatefulWidget {
  const CairoWorkspace({super.key, required this.store});
  final CenterStore store;

  @override
  State<CairoWorkspace> createState() => _CairoWorkspaceState();
}

class _CairoWorkspaceState extends State<CairoWorkspace> {
  int _page = 0;
  bool _switching = false;
  static const _labels = [
    'حضور ورصد الامتحان',
    'طلاب القاهرة',
    'مجموعات القاهرة',
    'سناتر القاهرة',
  ];

  Future<void> _select(int page) async {
    if (_switching || page == _page) return;
    _switching = true;
    try {
      final drafts = WorkspaceDraftScope.maybeOf(context);
      if (drafts != null && !await drafts.requestLeave(context)) return;
      if (mounted) setState(() => _page = page);
    } finally {
      _switching = false;
    }
  }

  @override
  Widget build(BuildContext context) => Column(
    children: [
      Padding(
        padding: const EdgeInsets.all(12),
        child: Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            for (var index = 0; index < _labels.length; index++)
              ChoiceChip(
                label: Text(_labels[index]),
                selected: _page == index,
                onSelected: (_) => _select(index),
              ),
          ],
        ),
      ),
      Expanded(
        child: switch (_page) {
          1 => StudentsPage(store: widget.store, cairo: true),
          2 => GroupsPage(store: widget.store, cairo: true),
          3 => CatalogsPage(store: widget.store, cairo: true),
          _ => AcademicsPage(store: widget.store, cairo: true),
        },
      ),
    ],
  );
}
