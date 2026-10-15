import 'package:flutter/material.dart';

import 'appearance.dart';
import 'massar_logo.dart';
import 'theme.dart';

class WorkspaceNavigationSection {
  const WorkspaceNavigationSection({
    required this.id,
    required this.label,
    required this.icon,
  });
  final String id, label;
  final IconData icon;
}

/// The reception and exam workspaces share the same native navigation surface.
class MassarWorkspaceNavigation extends StatelessWidget {
  const MassarWorkspaceNavigation({
    super.key,
    required this.sections,
    required this.selectedId,
    required this.onSelected,
    required this.userName,
    required this.userSubtitle,
    required this.footerAction,
    this.actions = const [],
    this.listKey,
  });
  final List<WorkspaceNavigationSection> sections;
  final String selectedId, userName, userSubtitle;
  final ValueChanged<String> onSelected;
  final List<Widget> actions;
  final Widget footerAction;
  final Key? listKey;

  @override
  Widget build(BuildContext context) => Container(
    width: 220,
    color: MassarPalette.of(context).nav,
    child: SafeArea(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Padding(
            padding: EdgeInsets.fromLTRB(24, 28, 24, 8),
            child: Row(
              children: [
                Expanded(
                  child: Align(
                    alignment: AlignmentDirectional.centerStart,
                    child: MassarLogo(height: 42, onDarkSurface: true),
                  ),
                ),
                AppearanceToggle(onDarkSurface: true),
              ],
            ),
          ),
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: 24),
            child: Text(
              'نادر جورج · إدارة السناتر',
              style: TextStyle(color: Color(0xFFCDDBE5)),
            ),
          ),
          const SizedBox(height: 12),
          ...actions,
          if (actions.isNotEmpty) const SizedBox(height: 20),
          Expanded(
            child: ListView(
              key: listKey,
              children: sections
                  .map(
                    (section) => Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 10,
                        vertical: 2,
                      ),
                      child: ListTile(
                        key: ValueKey('workspace-navigation-${section.id}'),
                        selected: selectedId == section.id,
                        selectedTileColor: const Color(0xFF183A59),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(8),
                        ),
                        leading: Icon(
                          section.icon,
                          color: selectedId == section.id
                              ? const Color(0xFF71D4C7)
                              : const Color(0xFFCDDBE5),
                        ),
                        title: Text(
                          section.label,
                          style: TextStyle(
                            color: selectedId == section.id
                                ? Colors.white
                                : const Color(0xFFCDDBE5),
                            fontSize: 14,
                          ),
                        ),
                        onTap: () => onSelected(section.id),
                      ),
                    ),
                  )
                  .toList(),
            ),
          ),
          const Divider(color: Color(0xFF2A405C)),
          ListTile(
            leading: const Icon(Icons.person_outline, color: Color(0xFFCDDBE5)),
            title: Text(userName, style: const TextStyle(color: Colors.white)),
            subtitle: Text(
              userSubtitle,
              style: const TextStyle(color: Color(0xFFCDDBE5), fontSize: 12),
            ),
          ),
          footerAction,
          const SizedBox(height: 14),
        ],
      ),
    ),
  );
}
