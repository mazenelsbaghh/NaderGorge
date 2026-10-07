import 'package:flutter/material.dart';
import '../../application/center_store.dart';
import '../../domain/models.dart';
import '../../shared/formatters.dart';
import '../attendance/student_editor_dialog.dart';
import '../attendance/student_history_panel.dart';

class StudentProfilePage extends StatefulWidget {
  const StudentProfilePage({
    super.key,
    required this.store,
    required this.studentId,
    this.embedded = false,
    this.readOnly = false,
  });
  final CenterStore store;
  final String studentId;
  final bool embedded, readOnly;

  @override
  State<StudentProfilePage> createState() => _StudentProfilePageState();
}

class _StudentProfilePageState extends State<StudentProfilePage> {
  bool _modalOpen = false;

  Future<void> _openModal(Future<void> Function() action) async {
    if (_modalOpen) return;
    setState(() => _modalOpen = true);
    try {
      await action();
    } finally {
      if (mounted) setState(() => _modalOpen = false);
    }
  }

  Future<void> _edit(Student student) => _openModal(() async {
    final route = DialogRoute<String>(
      context: context,
      builder: (_) =>
          StudentEditorDialog(store: widget.store, student: student),
    );
    await Navigator.of(context).push(route);
    await route.completed;
  });

  @override
  Widget build(BuildContext context) => Directionality(
    textDirection: TextDirection.rtl,
    child: DefaultTabController(
      length: 2,
      child: ListenableBuilder(
        listenable: widget.store,
        builder: (context, _) {
          final student = widget.store.students
              .where((s) => s.id == widget.studentId)
              .firstOrNull;
          return Scaffold(
            appBar: AppBar(
              automaticallyImplyLeading: false,
              title: Text(student?.name ?? 'ملف الطالب'),
              leading: widget.embedded
                  ? null
                  : BackButton(
                      onPressed: _modalOpen
                          ? null
                          : () => Navigator.pop(context),
                    ),
              actions: [
                if (!widget.readOnly &&
                    student != null &&
                    (widget.store.canManage || widget.store.canCollect))
                  TextButton.icon(
                    onPressed: _modalOpen ? null : () => _edit(student),
                    icon: const Icon(Icons.edit_outlined),
                    label: const Text('تعديل بيانات الطالب'),
                  ),
              ],
              bottom: student == null
                  ? null
                  : const TabBar(
                      tabs: [
                        Tab(
                          text: 'بيانات الطالب والمجموعات',
                          icon: Icon(Icons.person_outline),
                        ),
                        Tab(text: 'السجل الكامل', icon: Icon(Icons.history)),
                      ],
                    ),
            ),
            body: student == null
                ? const Center(
                    child: Text('الطالب غير متاح في البيانات الحالية.'),
                  )
                : TabBarView(
                    children: [
                      _details(student),
                      StudentHistoryPanel(
                        store: widget.store,
                        student: student,
                        expanded: true,
                        actionsEnabled: !widget.readOnly && !_modalOpen,
                        onOpenModal: _openModal,
                      ),
                    ],
                  ),
          );
        },
      ),
    ),
  );

  Widget _section(String title, List<Widget> children) => Card(
    margin: const EdgeInsets.only(bottom: 16),
    child: Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(title, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 16),
          ...children,
        ],
      ),
    ),
  );

  Widget _field(String label, String value) => Padding(
    padding: const EdgeInsets.only(bottom: 10),
    child: SelectableText('$label: ${value.isEmpty ? '—' : value}'),
  );

  Widget _details(Student student) {
    final store = widget.store;
    final twin = store.twinFor(student.id);
    final receipt = store.cardReceiptFor(student.id);
    return ListView(
      key: const PageStorageKey('student-profile-details'),
      padding: const EdgeInsets.all(24),
      children: [
        _section('بيانات الطالب', [
          _field('الاسم', student.name),
          _field('كود الطالب', student.code),
          _field('باركود الكارت', student.barcode),
          _field('رقم الطالب', student.phone),
          _field('رقم ولي الأمر', student.guardianPhone),
          _field(
            'تاريخ التسجيل',
            student.createdAtKnown ? shortDate(student.createdAt) : 'غير متوفر',
          ),
          _field(
            'الحالة',
            student.isSuspended
                ? 'مُصفّى — ${student.suspensionReason}'
                : 'فعال',
          ),
          _field(
            'التوأم',
            twin == null ? 'لا يوجد' : '${twin.name} · ${twin.code}',
          ),
          _field('استلام الكارت', receipt == null ? 'لم يستلم' : 'تم الاستلام'),
        ]),
        _section('الملاحظات', [
          SelectableText(
            student.notes.isEmpty ? 'لا توجد ملاحظات مسجلة.' : student.notes,
          ),
        ]),
        if (store.canCollect)
          _section('الحساب والخصم', [
            _field('الخصم الثابت', '${percentText(student.discountPercent)}٪'),
            _field(
              'المديونية المتبقية',
              money(store.studentDebtFor(student.id)),
            ),
            _field('باكدج', student.packageMember ? 'نعم' : 'لا'),
            _field(
              'رسوم السنتر',
              student.centerOnly || student.centerFeeEnabled
                  ? money(student.centerFeeAmount)
                  : 'غير مفعّلة',
            ),
            if (student.centerOnly)
              const Text('معفى من رسوم المدرس — رسوم السنتر فقط'),
          ]),
        _section('المجموعات ورصيد الحصص', [
          if (student.groupIds.isEmpty)
            const Text('الطالب غير مسجل في مجموعات.'),
          for (final id in student.groupIds)
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.groups_outlined),
              title: Text(store.groupLabel(id)),
              subtitle: store.canCollect
                  ? Text(
                      'الرصيد المتبقي: ${store.remainingFor(student.id, id)} حصص',
                    )
                  : null,
            ),
        ]),
        if (store.canCollect)
          _section('الشهور المشتراة', [
            if (!store.allPackages.any((p) => p.studentId == student.id))
              const Text('لا توجد شهور مشتراة.'),
            for (final package in store.allPackages.where(
              (p) => p.studentId == student.id,
            ))
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.calendar_month_outlined),
                title: Text(
                  package.monthPlanName ?? 'شهر ${package.totalSessions} حصص',
                ),
                subtitle: Text(
                  '${store.groupLabel(package.groupId)} · المتبقي ${package.remaining} من ${package.totalSessions} حصص',
                ),
                trailing: Text(
                  store.packages.any((p) => p.id == package.id)
                      ? package.remaining > 0
                            ? 'ساري'
                            : 'مستهلك'
                      : 'ملغى',
                ),
              ),
          ]),
      ],
    );
  }
}
