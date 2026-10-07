import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../application/center_store.dart';
import '../../domain/models.dart';
import '../../shared/notice_dialog.dart';
import '../../shared/problem_reporting.dart';
import '../../shared/scrollable_dialog.dart';
import '../../shared/theme.dart';
import 'entry_confirmation_dialog.dart';

/// Changes membership only; financial and attendance history retain their group.
class StudentTransferDialog extends StatefulWidget {
  const StudentTransferDialog({
    super.key,
    required this.store,
    required this.studentId,
    this.initialSourceGroupId,
    this.initialTargetGroupId,
  });

  final CenterStore store;
  final String studentId;
  final String? initialSourceGroupId, initialTargetGroupId;

  @override
  State<StudentTransferDialog> createState() => _StudentTransferDialogState();
}

class _StudentTransferDialogState extends State<StudentTransferDialog> {
  String? _from, _to;
  bool _reviewing = false, _busy = false, _scannerBlocked = false;
  bool _finished = false;
  LogicalKeyboardKey? _armedKey;
  final _confirmationFocus = FocusNode();

  Student? get _student => widget.store.students
      .where((student) => student.id == widget.studentId)
      .firstOrNull;
  List<StudyGroup> get _sources => widget.store.groups
      .where((group) => _student?.groupIds.contains(group.id) == true)
      .toList();
  bool get _valid =>
      widget.store.canCollect &&
      _student != null &&
      _sources.any((group) => group.id == _from) &&
      _to != _from &&
      _student?.groupIds.contains(_to) == false &&
      widget.store.groups.any((group) => group.id == _to);

  @override
  void initState() {
    super.initState();
    _from = _sources.any((group) => group.id == widget.initialSourceGroupId)
        ? widget.initialSourceGroupId
        : _sources.firstOrNull?.id;
    _to = widget.initialTargetGroupId == _from
        ? null
        : widget.initialTargetGroupId;
    widget.store.addListener(_refresh);
  }

  @override
  void dispose() {
    widget.store.removeListener(_refresh);
    _confirmationFocus.dispose();
    super.dispose();
  }

  void _refresh() {
    if (mounted && !_busy) setState(() {});
  }

  void _review() {
    if (!_valid || _busy || _finished || hasPendingMassarNotice(context)) {
      return;
    }
    setState(() {
      _reviewing = true;
      _scannerBlocked = false;
      _armedKey = null;
    });
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _confirmationFocus.requestFocus();
    });
  }

  KeyEventResult _key(FocusNode node, KeyEvent event) {
    if (_busy || _finished || hasPendingMassarNotice(context)) {
      return KeyEventResult.handled;
    }
    final key = event.logicalKey;
    final enter =
        key == LogicalKeyboardKey.enter ||
        key == LogicalKeyboardKey.numpadEnter;
    if (enter || key == LogicalKeyboardKey.escape) {
      final keyboard = HardwareKeyboard.instance;
      if (event.synthesized ||
          keyboard.isControlPressed ||
          keyboard.isAltPressed ||
          keyboard.isMetaPressed ||
          keyboard.isShiftPressed ||
          event is KeyRepeatEvent) {
        _armedKey = null;
      } else if (event is KeyDownEvent) {
        _armedKey = key;
      } else if (event is KeyUpEvent && _armedKey == key) {
        _armedKey = null;
        if (!enter) {
          _finish(false);
        } else if (!_scannerBlocked) {
          _save();
        }
      }
      return KeyEventResult.handled;
    }
    if (EntryConfirmationDialog.isScannerText(event)) {
      _armedKey = null;
      setState(() => _scannerBlocked = true);
      return KeyEventResult.handled;
    }
    return KeyEventResult.ignored;
  }

  Future<void> _save() async {
    if (_finished ||
        !_reviewing ||
        !_valid ||
        _busy ||
        _scannerBlocked ||
        hasPendingMassarNotice(context)) {
      return;
    }
    final from = _from!, to = _to!;
    setState(() => _busy = true);
    try {
      await widget.store.transferStudent(widget.studentId, from, to);
      if (mounted) {
        setState(() => _busy = false);
        _finish(true);
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.student_transfer_dialog');
      if (mounted) {
        await showMassarNotice(
          context,
          error is CenterException
              ? error.message
              : 'تعذر نقل الطالب. حاول مرة أخرى.',
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _finish(bool transferred) {
    if (_finished || _busy || hasPendingMassarNotice(context)) return;
    _finished = true;
    Navigator.pop(context, transferred);
  }

  Widget _summary(MassarPalette palette) => Container(
    padding: const EdgeInsets.all(16),
    decoration: BoxDecoration(
      color: palette.warningSurface,
      borderRadius: BorderRadius.circular(8),
    ),
    child: const Text(
      'النقل يغيّر عضوية المجموعة فقط. الكود والباركود والخصم لا يتغيرون، والمجموعات الأخرى تبقى مسجلة. الحضور والمدفوعات ورصيد الباقة القديمة يظلون في مجموعتهم الأصلية. لا يسجّل النقل حضورًا أو دفعًا جديدًا.',
    ),
  );

  @override
  Widget build(BuildContext context) {
    final palette = MassarPalette.of(context);
    final dialog = ScrollableMassarDialog(
      key: const Key('student-transfer-dialog'),
      title: Text(_reviewing ? 'تأكيد نقل الطالب' : 'نقل الطالب إلى مجموعة'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            _student?.name ?? 'الطالب غير موجود',
            style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
          ),
          Text('كود الطالب: ${_student?.code ?? '—'}'),
          const SizedBox(height: 20),
          if (_reviewing) ...[
            Text('من: ${widget.store.groupLabel(_from!)}'),
            const SizedBox(height: 12),
            Text(
              'إلى: ${widget.store.groupLabel(_to!)}',
              style: TextStyle(
                color: palette.accent,
                fontWeight: FontWeight.bold,
              ),
            ),
          ] else ...[
            DropdownButtonFormField<String>(
              key: ValueKey('transfer-source-$_from'),
              initialValue: _sources.any((group) => group.id == _from)
                  ? _from
                  : null,
              isExpanded: true,
              decoration: const InputDecoration(
                labelText: 'المجموعة الأصلية التي ستُزال',
              ),
              items: _sources
                  .map(
                    (group) => DropdownMenuItem(
                      value: group.id,
                      child: Text(
                        widget.store.groupLabel(group.id),
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  )
                  .toList(),
              onChanged: !widget.store.canCollect
                  ? null
                  : (value) => setState(() {
                      _from = value;
                      if (_to == value ||
                          _student?.groupIds.contains(_to) == true) {
                        _to = null;
                      }
                    }),
            ),
            const SizedBox(height: 16),
            DropdownButtonFormField<String>(
              key: ValueKey('transfer-target-$_from-$_to'),
              initialValue:
                  widget.store.groups.any(
                    (group) =>
                        group.id == _to &&
                        group.id != _from &&
                        _student?.groupIds.contains(group.id) == false,
                  )
                  ? _to
                  : null,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'المجموعة الجديدة'),
              items: widget.store.groups
                  .where(
                    (group) =>
                        group.id != _from &&
                        _student?.groupIds.contains(group.id) == false,
                  )
                  .map(
                    (group) => DropdownMenuItem(
                      value: group.id,
                      child: Text(
                        widget.store.groupLabel(group.id),
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  )
                  .toList(),
              onChanged: !widget.store.canCollect
                  ? null
                  : (value) => setState(() => _to = value),
            ),
          ],
          const SizedBox(height: 20),
          _summary(palette),
          if (_reviewing && !_valid) ...[
            const SizedBox(height: 12),
            Text(
              'تغيّرت عضوية الطالب أو صلاحيتك. ألغِ النقل وراجع بياناته من جديد.',
              style: TextStyle(color: palette.warning),
            ),
          ],
          if (_scannerBlocked) ...[
            const SizedBox(height: 12),
            Text(
              'دخل كود أثناء التأكيد. اضغط Esc وراجع النقل من جديد.',
              style: TextStyle(color: palette.warning),
            ),
          ],
          if (_busy)
            const Padding(
              padding: EdgeInsets.only(top: 16),
              child: LinearProgressIndicator(),
            ),
        ],
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => _finish(false),
          child: const Text('إلغاء · Esc'),
        ),
        if (_reviewing)
          TextButton(
            onPressed: _busy
                ? null
                : () => setState(() {
                    _reviewing = false;
                    _scannerBlocked = false;
                    _armedKey = null;
                  }),
            child: const Text('تغيير المجموعة'),
          ),
        FilledButton.icon(
          key: const Key('student-transfer-confirm'),
          onPressed: !_valid || _busy || _scannerBlocked
              ? null
              : _reviewing
              ? _save
              : _review,
          icon: const Icon(Icons.drive_file_move_outline),
          label: Text(_reviewing ? 'تأكيد النقل · Enter' : 'مراجعة النقل'),
        ),
      ],
    );
    return PopScope(
      canPop: !_busy,
      child: Directionality(
        textDirection: TextDirection.rtl,
        child: _reviewing
            ? Focus(
                focusNode: _confirmationFocus,
                descendantsAreFocusable: false,
                onKeyEvent: _key,
                child: dialog,
              )
            : dialog,
      ),
    );
  }
}
