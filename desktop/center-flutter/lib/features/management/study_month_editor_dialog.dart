import 'package:flutter/material.dart';

import '../../application/center_store.dart';
import '../../domain/models.dart';
import '../../shared/formatters.dart';
import 'management_widgets.dart';

class StudyMonthEditorDialog extends StatefulWidget {
  const StudyMonthEditorDialog({super.key, required this.store, this.month});
  final CenterStore store;
  final StudyMonth? month;

  @override
  State<StudyMonthEditorDialog> createState() => _StudyMonthEditorDialogState();
}

class _StudyMonthEditorDialogState extends State<StudyMonthEditorDialog> {
  late final TextEditingController _name;
  late final TextEditingController _price;
  late final TextEditingController _count;
  final _lessons = <_PreparedLessonDraft>[];
  final _removed = <_PreparedLessonDraft>[];
  late final List<(_PreparedLessonDraft, SessionKind)> _initialLessons;
  String? _countError;

  bool get _lessonsChanged =>
      _lessons.length != _initialLessons.length ||
      _lessons.indexed.any((item) {
        final original = _initialLessons[item.$1];
        return !identical(item.$2, original.$1) || item.$2.kind != original.$2;
      });

  @override
  void initState() {
    super.initState();
    _name = TextEditingController(text: widget.month?.name ?? '');
    _price = TextEditingController(
      text: priceText(widget.month?.price ?? 21000),
    );
    final existing = [...?widget.month?.lessons]
      ..sort((a, b) => a.number.compareTo(b.number));
    _count = TextEditingController(
      text: '${existing.isEmpty ? 4 : existing.length}',
    );
    if (existing.isEmpty) {
      for (var number = 1; number <= 4; number++) {
        _lessons.add(_PreparedLessonDraft(number: number));
      }
    } else {
      _lessons.addAll(
        existing.map(
          (lesson) => _PreparedLessonDraft(
            lesson: lesson,
            linked:
                widget.store.sessions.any(
                  (session) => session.preparedLessonId == lesson.id,
                ) ||
                widget.store.academicActivities.any(
                  (activity) => activity.preparedLessonId == lesson.id,
                ),
          ),
        ),
      );
    }
    _initialLessons = [for (final lesson in _lessons) (lesson, lesson.kind)];
  }

  @override
  void dispose() {
    _name.dispose();
    _price.dispose();
    _count.dispose();
    for (final lesson in [..._lessons, ..._removed]) {
      lesson.dispose();
    }
    super.dispose();
  }

  void _prepareLessons() {
    final count = int.tryParse(_count.text);
    if (count == null || count < 1 || count > 500) {
      setState(() => _countError = 'اكتب عدد حصص من ١ إلى ٥٠٠');
      return;
    }
    if (_lessons.skip(count).any((lesson) => lesson.linked)) {
      setState(
        () => _countError = 'لا يمكن حذف حصة مرتبطة بمجموعة؛ السجل محفوظ',
      );
      return;
    }
    setState(() {
      _countError = null;
      while (_lessons.length > count) {
        _removed.add(_lessons.removeLast());
      }
      var number = _lessons.fold<int>(0, (max, lesson) {
        final value = int.tryParse(lesson.number.text) ?? 0;
        return value > max ? value : max;
      });
      while (_lessons.length < count) {
        final restored = _removed.isEmpty ? null : _removed.removeLast();
        if (restored == null) {
          _lessons.add(_PreparedLessonDraft(number: ++number));
        } else {
          _lessons.add(restored);
          final restoredNumber = int.tryParse(restored.number.text) ?? 0;
          if (restoredNumber > number) {
            number = restoredNumber;
          }
        }
      }
    });
  }

  Future<void> _save() async {
    if (_lessons.isEmpty || int.tryParse(_count.text) != _lessons.length) {
      throw const CenterException('اضغط «تجهيز الحصص» بعد تغيير عددها أولًا.');
    }
    await widget.store.saveStudyMonth(
      StudyMonth(
        id: widget.month?.id ?? '',
        name: _name.text.trim(),
        number: widget.month?.number ?? 0,
        price: piastresFromText(_price.text)!,
        lessons: _lessons
            .map(
              (draft) => PreparedLesson(
                id: draft.id,
                name: draft.name.text.trim(),
                number: int.parse(draft.number.text),
                kind: draft.kind,
                extraPrice: draft.kind == SessionKind.extra
                    ? piastresFromText(draft.extraPrice.text)!
                    : 0,
              ),
            )
            .toList(),
      ),
    );
  }

  Widget _lessonFields(_PreparedLessonDraft draft) => Card(
    key: ObjectKey(draft),
    child: Padding(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'الحصة ${draft.number.text}',
            style: const TextStyle(fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 8),
          TextFormField(
            controller: draft.number,
            readOnly: draft.linked,
            decoration: const InputDecoration(
              labelText: 'رقم الحصة داخل الشهر',
            ),
            keyboardType: TextInputType.number,
            validator: positiveNumber,
          ),
          const SizedBox(height: 8),
          TextFormField(
            controller: draft.name,
            maxLength: 120,
            decoration: const InputDecoration(
              labelText: 'اسم الحصة (اختياري)',
              helperText: 'يمكن استعمال رقم الحصة فقط، أو اسم مع الرقم',
            ),
          ),
          DropdownButtonFormField<SessionKind>(
            initialValue: draft.kind,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'حساب الحصة'),
            items: SessionKind.values
                .map(
                  (kind) => DropdownMenuItem(
                    value: kind,
                    child: Text(sessionKindLabel(kind)),
                  ),
                )
                .toList(),
            onChanged: draft.linked
                ? null
                : (kind) {
                    if (kind != null) {
                      setState(() => draft.kind = kind);
                    }
                  },
          ),
          if (draft.linked)
            const Text('هذه الحصة مرتبطة بمجموعة؛ رقمها وحسابها محفوظان.'),
          if (draft.kind == SessionKind.extra) ...[
            const SizedBox(height: 8),
            TextFormField(
              controller: draft.extraPrice,
              readOnly: draft.linked,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              decoration: const InputDecoration(
                labelText: 'سعر الحصة الإضافية بالجنيه',
              ),
              validator: validPrice,
            ),
          ],
        ],
      ),
    ),
  );

  @override
  Widget build(BuildContext context) => Directionality(
    textDirection: TextDirection.rtl,
    child: ManagementEditor(
      title: widget.month == null
          ? 'إضافة شهر وحصصه'
          : 'تعديل ${widget.month!.name}',
      saveLabel: widget.month == null ? 'إضافة شهر وحصصه' : 'حفظ التعديلات',
      hasUnsavedChanges: _lessonsChanged,
      controllers: const [],
      fields: [
        const Text(
          'هذه الحصص مشتركة. لا ترتبط بأي مجموعة إلا عند الضغط على «ابدأ الحصة» في التحضير.',
        ),
        const SizedBox(height: 12),
        TextFormField(
          autofocus: true,
          controller: _name,
          maxLength: 120,
          decoration: const InputDecoration(labelText: 'اسم الشهر'),
          validator: requiredText,
        ),
        TextFormField(
          controller: _price,
          decoration: const InputDecoration(
            labelText: 'سعر الشهر بالجنيه',
            helperText:
                'تغيير السعر يطبّقه على الشراء الجديد في كل المجموعات؛ المدفوعات السابقة محفوظة',
          ),
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          validator: validPrice,
        ),
        const SizedBox(height: 12),
        TextFormField(
          controller: _count,
          decoration: InputDecoration(
            labelText: 'عدد الحصص المعدّة',
            errorText: _countError,
          ),
          keyboardType: TextInputType.number,
          validator: (text) {
            final count = int.tryParse(text ?? '');
            return count == null || count < 1 || count > 500
                ? 'اكتب عدد حصص من ١ إلى ٥٠٠'
                : null;
          },
        ),
        OutlinedButton.icon(
          onPressed: _prepareLessons,
          icon: const Icon(Icons.format_list_numbered),
          label: const Text('تجهيز الحصص'),
        ),
        if (widget.month != null)
          const Text(
            'الحصة المرتبطة بمجموعة تحتفظ برقمها وحسابها وسجلها؛ لا يمكن حذف حصة مستخدمة.',
          ),
        ..._lessons.map(_lessonFields),
      ],
      onSave: _save,
    ),
  );
}

class _PreparedLessonDraft {
  _PreparedLessonDraft({
    PreparedLesson? lesson,
    int number = 1,
    this.linked = false,
  }) : id = lesson?.id ?? '',
       name = TextEditingController(text: lesson?.name ?? ''),
       number = TextEditingController(text: '${lesson?.number ?? number}'),
       extraPrice = TextEditingController(
         text: priceText(lesson?.extraPrice ?? 0),
       ),
       kind = lesson?.kind ?? SessionKind.counted;
  final String id;
  final bool linked;
  final TextEditingController name, number, extraPrice;
  SessionKind kind;

  void dispose() {
    name.dispose();
    number.dispose();
    extraPrice.dispose();
  }
}
