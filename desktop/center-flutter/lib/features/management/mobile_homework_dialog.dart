import 'dart:io';
import 'package:barcode/barcode.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_svg/flutter_svg.dart';
import '../../application/center_store.dart';
import '../../domain/models.dart';
import '../../lan/lan_controller.dart';
import '../../shared/scrollable_dialog.dart';
import '../../shared/notice_dialog.dart';
import 'management_widgets.dart';

Future<void> showMobileHomeworkDialog(
  BuildContext context, {
  required CenterStore store,
  required LanController? lan,
  required LessonSession? session,
  String? activityId,
}) async {
  String? message;
  if (session == null || !store.sessionHasStarted(session.id)) {
    message = 'اختر مجموعة وحصة بدأت أولًا. لازم الطالب يكون مسجّل حضور فيها.';
  } else if (lan == null || !lan.isHost || store.isRemote) {
    message = 'افتح الميزة على الهوست بعد تشغيل الربط المحلي.';
  } else if (store.isCairoGroup(session.groupId)) {
    message = 'هذه الميزة لرصد واجبات إسكندرية.';
  }
  final activities = session == null
      ? <AcademicActivity>[]
      : store
            .academicActivitiesFor(session.id)
            .where((a) => a.kind == AcademicActivityKind.homework)
            .toList();
  if (activities.isEmpty) {
    message ??= 'أضف واجبًا لهذه الحصة من شاشة الامتحانات والواجبات أولًا.';
  }
  if (message != null) {
    await showMassarNotice(
      context,
      message,
      title: 'واجب الموبايل',
      kind: NoticeKind.warning,
    );
    return;
  }
  await showDialog<void>(
    context: context,
    builder: (_) => _MobileHomeworkDialog(
      store: store,
      lan: lan!,
      session: session!,
      activities: activities,
      initialActivityId: activityId,
    ),
  );
}

class _MobileHomeworkDialog extends StatefulWidget {
  const _MobileHomeworkDialog({
    required this.store,
    required this.lan,
    required this.session,
    required this.activities,
    this.initialActivityId,
  });
  final CenterStore store;
  final LanController lan;
  final LessonSession session;
  final List<AcademicActivity> activities;
  final String? initialActivityId;
  @override
  State<_MobileHomeworkDialog> createState() => _MobileHomeworkDialogState();
}

class _MobileHomeworkDialogState extends State<_MobileHomeworkDialog> {
  String? _activityId;
  List<String> _urls = [];
  String? _selectedUrl;
  bool _busy = false;
  String? _error;
  String? _hotspotMessage;
  @override
  void initState() {
    super.initState();
    _activityId = widget.activities
        .where((a) => a.id == widget.initialActivityId)
        .firstOrNull
        ?.id;
    if (_activityId == null && widget.activities.length == 1) {
      _activityId = widget.activities.single.id;
    }
  }

  Future<void> _open() async {
    setState(() {
      _busy = true;
      _error = null;
      _hotspotMessage = null;
    });
    try {
      final urls = await widget.lan.openMobileHomework(
        widget.session.id,
        _activityId!,
      );
      if (mounted) {
        setState(() {
          _urls = urls;
          _selectedUrl = widget.lan.preferredMobileHomeworkUrl(urls);
        });
      }
    } catch (error) {
      if (mounted) setState(() => _error = managementError(error));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _stop() async {
    setState(() => _busy = true);
    try {
      await widget.lan.closeMobileHomework();
      if (mounted) {
        setState(() {
          _urls = [];
          _selectedUrl = null;
          _error = null;
        });
      }
    } catch (error) {
      if (mounted) setState(() => _error = managementError(error));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _startHotspot() async {
    setState(() {
      _busy = true;
      _error = null;
      _hotspotMessage = null;
    });
    try {
      final hotspot = await widget.lan.startHomeworkHotspot();
      if (mounted) {
        setState(
          () => _hotspotMessage =
              'شبكة الواجب: ${hotspot['ssid'] ?? ''}. كلمة السر هي الموجودة في إعدادات نقطة اتصال ويندوز.',
        );
      }
    } catch (failure) {
      if (mounted) setState(() => _error = managementError(failure));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => ScrollableMassarDialog(
    title: const Text('واجب الموبايل'),
    width: 560,
    content: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          '${widget.store.groupById(widget.session.groupId)?.name ?? ''} · حصة ${widget.session.number}',
        ),
        const SizedBox(height: 12),
        const Text(
          'لازم الطالب يكون موجودًا ومسجّل حضور في هذه الحصة. باقي الحاضرين يتسجل لهم «اتعمل»، والموبايل يسجل الاستثناءات بعد التأكيد.',
        ),
        const SizedBox(height: 16),
        DropdownButtonFormField<String>(
          key: const Key('mobile-homework-activity'),
          initialValue: _activityId,
          isExpanded: true,
          decoration: const InputDecoration(
            labelText: 'اختر الواجب من قاعدة البيانات',
          ),
          items: widget.activities
              .map((a) => DropdownMenuItem(value: a.id, child: Text(a.name)))
              .toList(),
          onChanged: _busy || _urls.isNotEmpty
              ? null
              : (id) => setState(() => _activityId = id),
        ),
        const SizedBox(height: 16),
        if (Platform.isWindows) ...[
          OutlinedButton.icon(
            onPressed: _busy ? null : _startHotspot,
            icon: const Icon(Icons.wifi),
            label: const Text('تشغيل هوتسبوت الواجب من الهوست'),
          ),
          const Text(
            'اضغط تشغيل الهوتسبوت من الهوست؛ يمكنه استخدام اتصال الواي فاي أو الكابل حسب دعم ويندوز. وصّل موبايل الموظف بهذه الشبكة وافتح رابط الواجب. التسجيل على الساكند يستمر ما دام متصلًا بالهوست.',
          ),
          if (_hotspotMessage != null) Text(_hotspotMessage!),
          const SizedBox(height: 12),
        ],
        if (_urls.isEmpty)
          FilledButton.icon(
            key: const Key('mobile-homework-open'),
            onPressed: _busy || _activityId == null ? null : _open,
            icon: const Icon(Icons.qr_code),
            label: Text(_busy ? 'جارٍ التجهيز…' : 'تجهيز رابط الموبايل'),
          ),
        if (_error != null)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 12),
            child: Text(
              _error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ),
        if (_urls.isNotEmpty) ...[
          const Text(
            'تم اختيار اتصال هوتسبوت الهوست تلقائيًا إذا كان متاحًا. افتح الرابط أو امسح QR بالموبايل المتصل بنفس الشبكة.',
          ),
          DropdownButtonFormField<String>(
            key: const Key('mobile-homework-network'),
            initialValue: _selectedUrl,
            isExpanded: true,
            decoration: const InputDecoration(
              labelText: 'اتصال الموبايل بالهوست',
            ),
            items: _urls
                .map(
                  (url) => DropdownMenuItem(
                    value: url,
                    child: Text(
                      widget.lan.mobileHomeworkNetwork(url),
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                )
                .toList(),
            onChanged: (url) => setState(() => _selectedUrl = url),
          ),
          if (_selectedUrl case final String url) ...[
            const SizedBox(height: 16),
            Center(
              child: ColoredBox(
                color: Colors.white,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: SvgPicture.string(
                    Barcode.qrCode().toSvg(
                      url,
                      width: 220,
                      height: 220,
                      drawText: false,
                    ),
                    width: 220,
                    height: 220,
                  ),
                ),
              ),
            ),
            SelectableText(url, textDirection: TextDirection.ltr),
            TextButton.icon(
              onPressed: () => Clipboard.setData(ClipboardData(text: url)),
              icon: const Icon(Icons.copy),
              label: const Text('نسخ الرابط'),
            ),
          ],
          const SizedBox(height: 12),
          const Text(
            'الرابط خاص بالموظف والحصة والواجب، صالح لساعتين وينتهي عند الخروج من الحساب. فتح رابط جديد يلغي السابق.',
          ),
          const SizedBox(height: 12),
          const Text(
            'الكاميرا تحتاج إعداد الشهادة مرة واحدة: افتح الرابط وتابع إلى صفحة مسار، ونزّل شهادة الاتصال منها. على Android ثبّتها كشهادة CA من إعدادات الأمان. على iPhone ثبّت الملف ثم فعّل الثقة من الإعدادات ← عام ← حول ← إعدادات الثقة بالشهادات. أعد فتح الصفحة بعدها.',
          ),
          const Text(
            'لو الهاتف لا يصل للصفحة، راجع الشبكة والسماح لخدمة مسار على الشبكة الخاصة في جدار حماية ويندوز. يمكن كتابة الكود أو قراءة صورة الكارت أثناء إعداد الكاميرا.',
          ),
          OutlinedButton.icon(
            onPressed: _busy ? null : _stop,
            icon: const Icon(Icons.link_off),
            label: const Text('إيقاف رابط الموبايل'),
          ),
        ],
      ],
    ),
    actions: [
      TextButton(
        onPressed: _busy ? null : () => Navigator.pop(context),
        child: const Text('إغلاق النافذة'),
      ),
    ],
  );
}
