import '../../cloud/cloud_support_controller.dart';
import '../../cloud/app_update_controller.dart';
import 'cloud_settings_page.dart';
import 'package:flutter/material.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/shared/appearance.dart';
import 'package:massar_center/shared/formatters.dart';
import '../../lan/lan_controller.dart';
import '../../shared/massar_logo.dart';
import '../../shared/workspace_draft_guard.dart';

import 'academics_page.dart';
import 'cairo_workspace.dart';
import 'backup_page.dart';
import 'catalogs_page.dart';
import 'cards_page.dart';
import 'card_settings_page.dart';
import 'groups_page.dart';
import 'reports_page.dart';
import 'review_page.dart';
import 'closings_page.dart';
import 'corrections_page.dart';
import 'sessions_page.dart';
import 'staff_page.dart';
import 'students_page.dart';
import 'lan_settings_page.dart';

class ManagementWorkspace extends StatefulWidget {
  const ManagementWorkspace({
    super.key,
    required this.store,
    required this.onOpenAttendance,
    this.onOpenSession,
    this.lanController,
    this.cloudController,
    this.updateController,
  });
  final CenterStore store;
  final VoidCallback onOpenAttendance;
  final ValueChanged<String>? onOpenSession;
  final LanController? lanController;
  final CloudSupportController? cloudController;
  final AppUpdateController? updateController;

  @override
  State<ManagementWorkspace> createState() => _ManagementWorkspaceState();
}

class _ManagementWorkspaceState extends State<ManagementWorkspace> {
  String _page = 'sessions';
  final _drafts = WorkspaceDraftController();
  bool _navigating = false;

  Future<void> _leaveFor(VoidCallback action) async {
    if (_navigating) return;
    _navigating = true;
    try {
      if (await _drafts.requestLeave(context) && mounted) action();
    } finally {
      _navigating = false;
    }
  }

  void _selectPage(String page) {
    if (_page == page) return;
    _leaveFor(() => setState(() => _page = page));
  }

  @override
  void initState() {
    super.initState();
    if (widget.store.canManage && widget.store.groups.isEmpty) {
      _page = 'catalogs';
    }
  }

  List<({String id, String label, IconData icon})> get _sections => [
    (id: 'sessions', label: 'الحصص', icon: Icons.calendar_month_outlined),
    (id: 'students', label: 'الطلبة', icon: Icons.people_outline),
    (id: 'groups', label: 'المجموعات', icon: Icons.groups_outlined),
    if (widget.store.canAssess)
      (
        id: 'academics',
        label: 'رصد الامتحانات والواجبات',
        icon: Icons.fact_check_outlined,
      ),
    (id: 'reports', label: 'التقارير', icon: Icons.bar_chart_outlined),
    if (widget.lanController != null)
      (id: 'lan', label: 'ربط الأجهزة', icon: Icons.lan_outlined),
    if (widget.cloudController != null && widget.updateController != null)
      (
        id: 'cloud',
        label: 'المزامنة والتحديثات',
        icon: Icons.cloud_sync_outlined,
      ),
    if (widget.store.canCollect) ...[
      (id: 'cards', label: 'كروت الطلبة', icon: Icons.badge_outlined),
      (id: 'review', label: 'مراجعة', icon: Icons.fact_check_outlined),
      (
        id: 'closings',
        label: 'تقفيلة الحسابات',
        icon: Icons.account_balance_wallet_outlined,
      ),
      (
        id: 'corrections',
        label: 'التصحيح والاسترداد',
        icon: Icons.edit_note_outlined,
      ),
    ],
    if (widget.store.canConfigureCards)
      (
        id: 'card-settings',
        label: 'إعدادات مازن',
        icon: Icons.settings_outlined,
      ),
    if (widget.store.canManage) ...[
      (id: 'catalogs', label: 'أساس النظام', icon: Icons.account_tree_outlined),
      (id: 'staff', label: 'الموظفون', icon: Icons.badge_outlined),
      (id: 'backup', label: 'النسخ والسجل', icon: Icons.backup_outlined),
    ],
  ];

  Widget _content() => switch (_page) {
    'cairo' => CairoWorkspace(store: widget.store),
    'catalogs' => CatalogsPage(store: widget.store),
    'groups' => GroupsPage(store: widget.store),
    'students' => StudentsPage(store: widget.store),
    'cards' => CardsPage(store: widget.store),
    'card-settings' => CardSettingsPage(store: widget.store),
    'academics' => AcademicsPage(store: widget.store),
    'reports' => ReportsPage(store: widget.store),
    'review' => ReviewPage(store: widget.store),
    'closings' => ClosingsPage(store: widget.store),
    'corrections' => CorrectionsPage(store: widget.store),
    'backup' => BackupPage(store: widget.store),
    'staff' => StaffPage(store: widget.store),
    'lan' => LanSettingsPage(controller: widget.lanController!),
    'cloud' => CloudSettingsPage(
      store: widget.store,
      cloud: widget.cloudController!,
      updates: widget.updateController!,
    ),
    _ => SessionsPage(
      store: widget.store,
      onOpenAttendance: () => _leaveFor(widget.onOpenAttendance),
      onOpenSession: widget.onOpenSession,
    ),
  };

  Widget _navigation() => Container(
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
          if (widget.store.canCollect)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: FilledButton.icon(
                onPressed: () => _leaveFor(widget.onOpenAttendance),
                icon: const Icon(Icons.qr_code_scanner),
                label: const Text('التحضير والتحصيل'),
              ),
            ),
          if (widget.store.canAssess)
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
              child: FilledButton.icon(
                key: const Key('open-cairo-attendance'),
                onPressed: () => _selectPage('cairo'),
                icon: const Icon(Icons.school_outlined),
                label: const Text(
                  'حضور مجموعات القاهرة',
                  textAlign: TextAlign.center,
                ),
                style: FilledButton.styleFrom(
                  backgroundColor: _page == 'cairo'
                      ? const Color(0xFF087F8C)
                      : const Color(0xFF183A59),
                  foregroundColor: Colors.white,
                ),
              ),
            ),
          if (widget.store.canCollect || widget.store.canAssess)
            const SizedBox(height: 20),
          Expanded(
            child: ListView(
              key: const Key('management-navigation'),
              children: _sections
                  .map(
                    (section) => Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 10,
                        vertical: 2,
                      ),
                      child: ListTile(
                        selected: _page == section.id,
                        selectedTileColor: const Color(0xFF183A59),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(8),
                        ),
                        leading: Icon(
                          section.icon,
                          color: _page == section.id
                              ? const Color(0xFF71D4C7)
                              : const Color(0xFFCDDBE5),
                        ),
                        title: Text(
                          section.label,
                          style: TextStyle(
                            color: _page == section.id
                                ? Colors.white
                                : const Color(0xFFCDDBE5),
                            fontSize: 14,
                          ),
                        ),
                        onTap: () => _selectPage(section.id),
                      ),
                    ),
                  )
                  .toList(),
            ),
          ),
          const Divider(color: Color(0xFF2A405C)),
          ListTile(
            leading: const Icon(Icons.person_outline, color: Color(0xFFCDDBE5)),
            title: Text(
              widget.store.currentUser?.name ?? '',
              style: const TextStyle(color: Colors.white),
            ),
            subtitle: Text(
              widget.store.currentUser == null
                  ? ''
                  : staffRoleLabel(widget.store.currentUser!.role),
              style: const TextStyle(color: Color(0xFFCDDBE5), fontSize: 12),
            ),
          ),
          TextButton.icon(
            onPressed: () => _leaveFor(widget.store.signOut),
            icon: const Icon(Icons.logout, color: Color(0xFFCDDBE5)),
            label: const Text(
              'تبديل الموظف',
              style: TextStyle(color: Color(0xFFCDDBE5)),
            ),
          ),
          const SizedBox(height: 14),
        ],
      ),
    ),
  );

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: widget.store,
    builder: (context, _) => WorkspaceDraftScope(
      controller: _drafts,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _navigation(),
          Expanded(
            child: FocusTraversalGroup(
              child: KeyedSubtree(key: ValueKey(_page), child: _content()),
            ),
          ),
        ],
      ),
    ),
  );
}
