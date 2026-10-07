part of 'center_store.dart';

/// Unpaired client placeholder. Its path locates metadata, never a SQLite file.
class _ClientWorkspace extends CenterStore {
  _ClientWorkspace(String directory)
    : super._network(p.join(directory, 'center.sqlite'));

  @override
  bool get isClientWorkspace => true;
  @override
  bool get hasStaff => true;

  @override
  Future<void> _exclusive(
    Future<void> Function() work, {
    String operation = 'database.operation',
  }) => Future.error(
    const CenterException(
      'هذا جهاز فرعي؛ اربطه بالجهاز الرئيسي ثم سجّل دخول الموظف. لا توجد قاعدة بيانات محلية للعمل دون اتصال.',
    ),
  );

  @override
  Future<void> prepareLanSwitch() async {
    if (await File(
      p.join(File(databasePath).parent.path, 'lan-pending-command.json'),
    ).exists()) {
      throw const CenterException(
        'توجد عملية غير مؤكدة؛ ارجع إلى الربط المحفوظ وسجّل دخول الموظف لمراجعتها قبل تغيير الجهاز الرئيسي.',
      );
    }
  }

  @override
  Future<void> close() async {
    _closed = true;
  }
}
