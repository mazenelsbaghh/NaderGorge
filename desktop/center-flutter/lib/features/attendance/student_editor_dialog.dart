import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/notice_dialog.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/domain/student_lookup.dart';
import 'package:massar_center/shared/theme.dart';
import '../management/management_widgets.dart' show priceText, piastresFromText;

/// Local student enrollment and editing, also usable from management.
/// Returns the saved student ID to the caller.
class StudentEditorDialog extends StatefulWidget {
  const StudentEditorDialog({
    super.key,
    required this.store,
    this.student,
    this.initialGroupId,
    this.focusTwin = false,
    this.cairo,
  });
  final CenterStore store;
  final Student? student;
  final String? initialGroupId;
  final bool focusTwin;
  final bool? cairo;

  @override
  State<StudentEditorDialog> createState() => _StudentEditorDialogState();
}

class _StudentEditorDialogState extends State<StudentEditorDialog> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _name,
      _code,
      _phone,
      _guardian,
      _discount,
      _notes,
      _centerFee;
  late final Set<String> _groups;
  bool _busy = false;
  bool _confirmingDiscard = false;
  bool _allowClose = false;
  bool _finished = false;
  bool _saveShortcutArmed = false;
  bool _escapeArmed = false;
  late final List<String> _initialFields;
  late final Set<String> _initialGroups;
  late final String? _initialTwin;
  late final bool _initialCenterOnly;
  bool _discountEdited = false;
  bool _centerOnly = false, _centerConfigEdited = false;
  final _discountFocus = FocusNode();
  LogicalKeyboardKey? _discountEnter;
  String? _twinStudentId;
  String? _discountBeforeTwinGrant;
  final _twinSearch = TextEditingController();
  bool get _twinGrant =>
      _twinStudentId != null && _twinStudentId != widget.student?.twinStudentId;

  @override
  void initState() {
    super.initState();
    final student = widget.student;
    _name = TextEditingController(text: student?.name ?? '');
    _code = TextEditingController(
      text: student?.code ?? widget.store.nextStudentCode,
    );
    _phone = TextEditingController(text: student?.phone ?? '');
    _guardian = TextEditingController(text: student?.guardianPhone ?? '');
    _phone.addListener(_refreshPhoneOwners);
    _guardian.addListener(_refreshPhoneOwners);
    _twinStudentId = student?.twinStudentId;
    _discount = TextEditingController(text: '${student?.discountPercent ?? 0}');
    _notes = TextEditingController(text: student?.notes ?? '');
    _centerOnly = student?.centerOnly ?? false;
    _centerFee = TextEditingController(
      text: priceText(student?.centerFeeAmount ?? 1500),
    );
    _groups = {
      ...?student?.groupIds,
      if (student == null && widget.initialGroupId != null)
        widget.initialGroupId!,
    };
    _initialFields = _draftFields;
    _initialGroups = Set.of(_groups);
    _initialTwin = _twinStudentId;
    _initialCenterOnly = _centerOnly;
  }

  List<String> get _draftFields => [
    _name.text,
    _phone.text,
    _guardian.text,
    _discount.text,
    _notes.text,
    _centerFee.text,
  ];

  bool get _hasChanges {
    final fields = _draftFields;
    for (var index = 0; index < fields.length; index++) {
      if (fields[index] != _initialFields[index]) return true;
    }
    return _discountEdited ||
        _centerConfigEdited ||
        _initialTwin != _twinStudentId ||
        _initialCenterOnly != _centerOnly ||
        _initialGroups.length != _groups.length ||
        !_initialGroups.containsAll(_groups);
  }

  Future<void> _finish([String? savedId]) async {
    if (_finished || !mounted) return;
    setState(() {
      _finished = true;
      _allowClose = true;
    });
    await WidgetsBinding.instance.endOfFrame;
    if (mounted && ModalRoute.of(context)?.isCurrent == true) {
      Navigator.of(context).pop(savedId);
    }
  }

  Future<void> _requestClose() async {
    if (_busy ||
        _finished ||
        _confirmingDiscard ||
        hasPendingMassarNotice(context)) {
      return;
    }
    if (!_hasChanges) {
      await _finish();
      return;
    }
    setState(() => _confirmingDiscard = true);
    try {
      final discard = await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (dialogContext) => Directionality(
          textDirection: TextDirection.rtl,
          child: ScrollableMassarDialog(
            key: const Key('student-editor-discard'),
            width: 460,
            title: const Text('تغييرات لم تُحفظ'),
            content: const Text(
              'كتبت تغييرات في بيانات الطالب. هل تريد تركها وإغلاق النموذج؟',
            ),
            actions: [
              TextButton(
                key: const Key('student-editor-keep-editing'),
                autofocus: true,
                onPressed: () => Navigator.of(dialogContext).pop(false),
                child: const Text('متابعة التعديل'),
              ),
              FilledButton(
                key: const Key('student-editor-discard-changes'),
                style: FilledButton.styleFrom(
                  backgroundColor: Theme.of(context).colorScheme.error,
                  foregroundColor: Theme.of(context).colorScheme.onError,
                ),
                onPressed: () => Navigator.of(dialogContext).pop(true),
                child: const Text('ترك التغييرات'),
              ),
            ],
          ),
        ),
      );
      if (discard == true && mounted) await _finish();
    } finally {
      if (mounted) setState(() => _confirmingDiscard = false);
    }
  }

  KeyEventResult _editorKey(FocusNode node, KeyEvent event) {
    final key = event.logicalKey;
    final keyboard = HardwareKeyboard.instance;
    if (key == LogicalKeyboardKey.escape) {
      if (event is KeyDownEvent) {
        _escapeArmed =
            !event.synthesized &&
            !keyboard.isControlPressed &&
            !keyboard.isMetaPressed &&
            !keyboard.isAltPressed &&
            !keyboard.isShiftPressed &&
            !_busy &&
            !_finished &&
            !_confirmingDiscard &&
            !hasPendingMassarNotice(context) &&
            ModalRoute.of(context)?.isCurrent == true;
      } else if (event is KeyUpEvent) {
        final armed = _escapeArmed;
        _escapeArmed = false;
        if (armed) _requestClose();
      }
      return KeyEventResult.handled;
    }
    if (key != LogicalKeyboardKey.keyS) return KeyEventResult.ignored;
    if (event is KeyUpEvent && _saveShortcutArmed) {
      _saveShortcutArmed = false;
      if (!event.synthesized &&
          !keyboard.isShiftPressed &&
          !keyboard.isAltPressed) {
        _save();
      }
      return KeyEventResult.handled;
    }
    if (!(keyboard.isControlPressed || keyboard.isMetaPressed)) {
      return KeyEventResult.ignored;
    }
    if (event is KeyDownEvent) {
      _saveShortcutArmed =
          !event.synthesized &&
          !keyboard.isShiftPressed &&
          !keyboard.isAltPressed &&
          !_busy &&
          !_finished &&
          !_confirmingDiscard &&
          !hasPendingMassarNotice(context) &&
          ModalRoute.of(context)?.isCurrent == true;
    }
    return KeyEventResult.handled;
  }

  @override
  void dispose() {
    _phone.removeListener(_refreshPhoneOwners);
    _guardian.removeListener(_refreshPhoneOwners);
    _twinSearch.dispose();
    _discountFocus.dispose();
    for (final controller in [
      _name,
      _code,
      _phone,
      _guardian,
      _discount,
      _notes,
      _centerFee,
    ]) {
      controller.dispose();
    }
    super.dispose();
  }

  void _refreshPhoneOwners() {
    if (mounted) setState(() {});
  }

  String _phoneDigits(String phone) =>
      normalizeStudentIdentifier(phone).replaceAll(RegExp(r'\D'), '');

  Widget _phoneNotice(TextEditingController field) {
    final digits = _phoneDigits(field.text);
    if (digits.isEmpty) return const SizedBox.shrink();
    final owners = widget.store.students
        .where(
          (student) =>
              student.id != widget.student?.id &&
              (_phoneDigits(student.phone) == digits ||
                  _phoneDigits(student.guardianPhone) == digits),
        )
        .toList();
    if (owners.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Text(
        'الرقم مستخدم بالفعل لدى: ${owners.map((student) => '${student.name} · ${student.code}').join('، ')}. يمكنك متابعة التسجيل.',
        style: TextStyle(color: MassarPalette.of(context).warning),
      ),
    );
  }

  void _chooseTwin(String? id) {
    setState(() {
      final wasGrant = _twinGrant;
      if (!wasGrant) _discountBeforeTwinGrant = _discount.text;
      _twinStudentId = id;
      if (_twinGrant) {
        _discount.text = '100';
      } else if (wasGrant && widget.store.canEditDiscount) {
        _discount.text =
            _discountBeforeTwinGrant ??
            '${widget.student?.discountPercent ?? 0}';
      } else if (!widget.store.canEditDiscount) {
        _discount.text = '${widget.student?.discountPercent ?? 0}';
      }
    });
  }

  Widget _twinPicker() {
    final candidates = widget.store.students
        .where(
          (student) =>
              student.id != widget.student?.id &&
              (student.id == _twinStudentId ||
                  (student.twinStudentId == null ||
                          student.twinStudentId == widget.student?.id) &&
                      studentMatchesSearch(student, _twinSearch.text)),
        )
        .toList();
    final selected = candidates
        .where((student) => student.id == _twinStudentId)
        .firstOrNull;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TextField(
          key: const Key('student-twin-search'),
          controller: _twinSearch,
          enabled: !_busy,
          decoration: const InputDecoration(
            labelText: 'بحث عن التوأم بالكود أو الاسم',
          ),
          onChanged: (_) => setState(() {}),
        ),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          key: ValueKey('student-twin-$_twinStudentId'),
          initialValue: _twinStudentId,
          isExpanded: true,
          decoration: const InputDecoration(labelText: 'التوأم'),
          items: [
            const DropdownMenuItem(value: null, child: Text('لا يوجد')),
            ...candidates.map(
              (student) => DropdownMenuItem(
                value: student.id,
                child: Tooltip(
                  message: '${student.name} · ${student.code}',
                  child: Text(
                    '${student.name} · ${student.code}',
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ),
            ),
          ],
          onChanged: _busy ? null : _chooseTwin,
        ),
        const SizedBox(height: 6),
        Text(
          selected == null
              ? 'التوأم: لا يوجد'
              : 'التوأم: ${selected.name} · ${selected.code}',
        ),
        if (selected != null)
          Text(
            'خصم التوأم المسجل: ${percentText(selected.discountPercent)}٪${selected.discountPercent == 100 ? ' — معفى بالكامل' : ''}. اختيار التوأم لا يغير خصمه.',
          ),
        Text(
          _twinGrant
              ? 'الطالب الحالي معفى بنسبة ١٠٠٪ عند حفظ العلاقة. الحسابات السابقة لا تتغير.'
              : 'فك علاقة التوأم لا يزيل الخصم الحالي تلقائيًا؛ راجع نسبة الخصم أدناه.',
        ),
      ],
    );
  }

  KeyEventResult _discountKey(FocusNode node, KeyEvent event) {
    final key = event.logicalKey;
    if (!_discountFocus.hasFocus ||
        (key != LogicalKeyboardKey.enter &&
            key != LogicalKeyboardKey.numpadEnter)) {
      return KeyEventResult.ignored;
    }
    final keyboard = HardwareKeyboard.instance;
    if (_busy ||
        _finished ||
        _confirmingDiscard ||
        ModalRoute.of(context)?.isCurrent != true ||
        !widget.store.canEditDiscount ||
        _twinGrant ||
        event.synthesized ||
        keyboard.isControlPressed ||
        keyboard.isShiftPressed ||
        keyboard.isAltPressed ||
        keyboard.isMetaPressed ||
        hasPendingMassarNotice(context)) {
      _discountEnter = null;
      return KeyEventResult.handled;
    }
    if (event is KeyDownEvent) {
      _discountEnter = key;
    } else if (event is KeyUpEvent) {
      final armed = _discountEnter == key;
      _discountEnter = null;
      if (armed) {
        // Enter explicitly reviews even an unchanged imported percentage.
        _discountEdited = true;
        _save();
      }
    }
    return KeyEventResult.handled;
  }

  Future<void> _save() async {
    if (_busy ||
        _finished ||
        _confirmingDiscard ||
        ModalRoute.of(context)?.isCurrent != true ||
        hasPendingMassarNotice(context) ||
        !_form.currentState!.validate()) {
      return;
    }
    if (_groups.isEmpty) {
      await showMassarNotice(
        context,
        'اختار مجموعة واحدة على الأقل لتسجيل الطالب.',
        kind: NoticeKind.warning,
      );
      return;
    }
    setState(() {
      _busy = true;
    });
    try {
      final latest = widget.store.students
          .where((student) => student.id == widget.student?.id)
          .firstOrNull;
      final changesDiscount = _discountEdited && widget.store.canEditDiscount;
      final request = Student(
        id: widget.student?.id ?? '',
        code: _code.text.trim(),
        name: _name.text.trim(),
        phone: _phone.text.trim(),
        guardianPhone: _guardian.text.trim(),
        groupIds: _groups.toList(),
        discountPercent: _twinGrant
            ? 100
            : changesDiscount
            ? percentFromText(_discount.text)!
            : latest?.discountPercent ?? widget.student?.discountPercent ?? 0,
        discountNeedsReview: _twinGrant || changesDiscount
            ? false
            : latest?.discountNeedsReview ??
                  widget.student?.discountNeedsReview ??
                  false,
        centerOnly: _centerOnly,
        centerFeeAmount: piastresFromText(_centerFee.text) ?? 1500,
        notes: _notes.text.trim(),
        createdAt: widget.student?.createdAt ?? DateTime.now(),
        twinStudentId: _twinStudentId,
      );
      final String savedId;
      if (widget.student == null) {
        final saved = await widget.store.registerStudent(request);
        savedId = saved.id;
      } else {
        await widget.store.saveStudent(
          request,
          preserveDiscount: !_discountEdited,
          preserveCenterOnly: !_centerConfigEdited,
        );
        savedId = widget.student!.id;
      }
      if (!mounted) return;
      await showMassarNotice(
        context,
        'تم حفظ بيانات الطالب.',
        kind: NoticeKind.success,
      );
      if (mounted) await _finish(savedId);
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.student_editor_dialog');
      if (mounted) {
        await showMassarNotice(
          context,
          error is CenterException
              ? error.message
              : 'تعذر حفظ الطالب. حاول مرة أخرى.',
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Widget _field(
    TextEditingController controller,
    String label, {
    bool required = false,
    bool numeric = false,
  }) => TextFormField(
    controller: controller,
    enabled: !_busy,
    keyboardType: numeric ? TextInputType.number : TextInputType.text,
    decoration: InputDecoration(labelText: label),
    validator: required
        ? (value) =>
              value == null || value.trim().isEmpty ? 'هذا الحقل مطلوب' : null
        : null,
  );

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: widget.store,
    builder: (context, _) => PopScope<String>(
      canPop: _allowClose,
      onPopInvokedWithResult: (didPop, result) {
        if (!didPop) _requestClose();
      },
      child: Focus(
        onKeyEvent: _editorKey,
        onFocusChange: (focused) {
          if (!focused) {
            _saveShortcutArmed = false;
            _escapeArmed = false;
          }
        },
        child: ScrollableMassarDialog(
          title: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                widget.student == null
                    ? 'إضافة طالب وتسجيله'
                    : 'تعديل بيانات الطالب',
              ),
              if (widget.student != null)
                Text(
                  '${widget.student!.name} · كود ${widget.student!.code}',
                  style: Theme.of(context).textTheme.bodyMedium,
                ),
              Text(
                'Ctrl / ⌘ + S للحفظ. التعديل لا يسجل حضورًا أو دفعًا.',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ],
          ),
          content: SizedBox(
            width: 620,
            child: Form(
              key: _form,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (widget.focusTwin) _twinPicker(),
                  _field(_name, 'اسم الطالب', required: true),
                  const SizedBox(height: 16),
                  TextFormField(
                    key: const Key('student-editor-code'),
                    controller: _code,
                    readOnly: true,
                    enabled: !_busy,
                    textDirection: TextDirection.ltr,
                    decoration: InputDecoration(
                      labelText: 'كود الطالب',
                      suffixIcon: const Icon(Icons.lock_outline),
                      helperText: widget.student == null
                          ? 'يتحدد تلقائيًا عند الحفظ، ولا يمكن تغييره.'
                          : 'كود الطالب ثابت بعد التسجيل ولا يمكن تعديله.',
                    ),
                  ),
                  const SizedBox(height: 16),
                  if (widget.student != null &&
                      widget.student!.barcode.isNotEmpty) ...[
                    TextFormField(
                      key: const Key('student-editor-barcode'),
                      initialValue: widget.student!.barcode,
                      readOnly: true,
                      enabled: !_busy,
                      textDirection: TextDirection.ltr,
                      decoration: const InputDecoration(
                        labelText: 'الباركود الكامل للكارت',
                        suffixIcon: Icon(Icons.lock_outline),
                        helperText: 'باركود الكارت ثابت ولا يمكن تعديله.',
                      ),
                    ),
                    const SizedBox(height: 16),
                  ],
                  Row(
                    children: [
                      Expanded(child: _field(_phone, 'رقم الطالب')),
                      const SizedBox(width: 12),
                      Expanded(child: _field(_guardian, 'رقم ولي الأمر')),
                    ],
                  ),
                  _phoneNotice(_phone),
                  _phoneNotice(_guardian),
                  const SizedBox(height: 16),
                  if (!widget.focusTwin) _twinPicker(),
                  const SizedBox(height: 20),
                  const Text(
                    'المجموعات المسجل فيها',
                    style: TextStyle(fontWeight: FontWeight.bold),
                  ),
                  if (widget.store.groups.isEmpty)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 12),
                      child: Text('أنشئ مجموعة من الإدارة أولًا.'),
                    ),
                  ...(widget.cairo == null
                          ? widget.store.groups
                          : widget.store.groupsForRegion(cairo: widget.cairo!))
                      .map(
                        (group) => CheckboxListTile(
                          contentPadding: EdgeInsets.zero,
                          title: Text(widget.store.groupLabel(group.id)),
                          value: _groups.contains(group.id),
                          onChanged: _busy
                              ? null
                              : (value) => setState(() {
                                  if (value == true) {
                                    _groups.add(group.id);
                                  } else {
                                    _groups.remove(group.id);
                                  }
                                }),
                        ),
                      ),
                  const SizedBox(height: 12),
                  if (widget.cairo != true)
                    SwitchListTile(
                      contentPadding: EdgeInsets.zero,
                      title: const Text('السنتر فقط — إعفاء المدرس ١٠٠٪'),
                      value: _centerOnly,
                      onChanged: _busy || !widget.store.canEditDiscount
                          ? null
                          : (enabled) => setState(() {
                              _centerOnly = enabled;
                              _centerConfigEdited = true;
                              if (enabled) {
                                _discount.text = '100';
                                _discountEdited = true;
                              }
                            }),
                    ),
                  if (_centerOnly && widget.cairo != true)
                    TextFormField(
                      controller: _centerFee,
                      enabled: !_busy,
                      decoration: const InputDecoration(
                        labelText: 'رسوم السنتر لكل حصة بالجنيه',
                      ),
                      onChanged: (_) => _centerConfigEdited = true,
                      validator: (text) =>
                          (piastresFromText(text ?? '') ?? 0) <= 0
                          ? 'أدخل رسومًا أكبر من صفر'
                          : null,
                    ),
                  if (widget.cairo != true)
                    Focus(
                      onKeyEvent: _discountKey,
                      onFocusChange: (focused) {
                        if (!focused) _discountEnter = null;
                      },
                      child: TextFormField(
                        key: const Key('student-editor-discount'),
                        controller: _discount,
                        focusNode: _discountFocus,
                        onChanged: (_) => _discountEdited = true,
                        enabled:
                            !_busy &&
                            widget.store.canEditDiscount &&
                            !_twinGrant &&
                            !_centerOnly,
                        decoration: const InputDecoration(
                          labelText: 'نسبة الخصم الثابتة',
                          suffixText: '٪',
                          helperText:
                              'Enter يحفظ الخصم وبيانات الطالب. يسري الخصم على الدفع الجديد فقط.',
                        ),
                        keyboardType: TextInputType.number,
                        validator: (value) {
                          final percent = percentFromText(value ?? '');
                          return percent == null || percent < 0 || percent > 100
                              ? 'أدخل نسبة من ٠ إلى ١٠٠'
                              : null;
                        },
                      ),
                    ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: _notes,
                    enabled: !_busy,
                    maxLines: 2,
                    decoration: const InputDecoration(
                      labelText: 'ملاحظات الطالب',
                    ),
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: _busy || _finished || _confirmingDiscard
                  ? null
                  : _requestClose,
              child: const Text('إلغاء'),
            ),
            FilledButton(
              key: const Key('student-editor-save'),
              onPressed: _busy || _finished || _confirmingDiscard
                  ? null
                  : _save,
              child: _busy
                  ? Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Theme.of(context).colorScheme.onPrimary,
                          ),
                        ),
                        const SizedBox(width: 8),
                        const Text('جارٍ الحفظ…'),
                      ],
                    )
                  : Text(
                      widget.student == null ? 'حفظ الطالب' : 'حفظ التعديلات',
                    ),
            ),
          ],
        ),
      ),
    ),
  );
}
