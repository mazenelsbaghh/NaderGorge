part of 'center_store.dart';

extension CenterStoreStudentRelationships on CenterStore {
  void _requireActiveStudent(Student student) {
    if (student.isSuspended) {
      throw const CenterException(
        'الطالب موقوف؛ أعد تفعيله أولًا قبل تسجيل حضور أو دفع جديد.',
      );
    }
  }

  Student? twinFor(String studentId) {
    final partnerId = _student(studentId).twinStudentId;
    return partnerId == null ? null : _student(partnerId);
  }

  String _studentProfileAudit(Student student) =>
      'حفظ الطالب — كود ${student.code} — التوأم ${student.twinStudentId == null ? 'لا يوجد' : _student(student.twinStudentId!).code} — الخصم الثابت ${student.discountPercent}٪';

  void _syncTwinRelationship(Student? previous, Student saved) {
    final previousId = previous?.twinStudentId;
    final partnerId = saved.twinStudentId;
    if (partnerId == previousId) return;
    Student? partner;
    if (partnerId != null) {
      if (partnerId == saved.id) {
        throw const CenterException('لا يمكن اختيار الطالب نفسه كتوأم.');
      }
      partner = _student(partnerId);
      if (partner.twinStudentId != null && partner.twinStudentId != saved.id) {
        throw const CenterException(
          'الطالب المختار مرتبط بتوأم آخر؛ راجع العلاقة السابقة أولًا.',
        );
      }
    }
    if (previousId != null) {
      final oldPartner = _student(previousId);
      _replace(
        _state.students,
        oldPartner.id,
        oldPartner.copyWith(clearTwinStudentId: true),
        (e) => e.id,
      );
    }
    if (partner != null) {
      _replace(
        _state.students,
        partner.id,
        partner.copyWith(twinStudentId: saved.id),
        (e) => e.id,
      );
    }
  }
}
