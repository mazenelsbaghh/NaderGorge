import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../shared/workspace_draft_guard.dart';

import '../../application/center_store.dart';
import '../../domain/models.dart';
import 'management_widgets.dart';

class CardSettingsPage extends StatefulWidget {
  const CardSettingsPage({super.key, required this.store});
  final CenterStore store;

  @override
  State<CardSettingsPage> createState() => _CardSettingsPageState();
}

class _CardSettingsPageState extends State<CardSettingsPage> {
  final _form = GlobalKey<FormState>();
  late final _price = TextEditingController(
    text: widget.store.cardSettings.price == null
        ? ''
        : priceText(widget.store.cardSettings.price!),
  );
  late bool _requirePayment =
      widget.store.cardSettings.requirePaymentBeforeReceipt;
  bool _busy = false;
  bool _submitted = false, _discarding = false;
  String? _saveError;
  late CenterCardSettings _savedSettings = widget.store.cardSettings;
  bool get _dirty =>
      _price.text !=
          (_savedSettings.price == null
              ? ''
              : priceText(_savedSettings.price!)) ||
      _requirePayment != _savedSettings.requirePaymentBeforeReceipt;

  Future<void> _reset() async {
    if (_busy || _discarding || !_dirty) return;
    setState(() => _discarding = true);
    try {
      final accepted = await confirmManagement(
        context,
        title: 'تجاهل تعديلات إعدادات الكروت؟',
        description:
            'ستعود للسعر وشرط الاستلام المحفوظين، دون تغيير أي دفعة أو استلام سابق.',
        confirmLabel: 'تجاهل التعديلات',
        destructive: true,
      );
      if (accepted && mounted) {
        setState(() {
          _savedSettings = widget.store.cardSettings;
          _price.text = _savedSettings.price == null
              ? ''
              : priceText(_savedSettings.price!);
          _requirePayment = _savedSettings.requirePaymentBeforeReceipt;
          _saveError = null;
          _submitted = false;
        });
      }
    } finally {
      if (mounted) setState(() => _discarding = false);
    }
  }

  @override
  void dispose() {
    _price.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_busy ||
        _discarding ||
        ModalRoute.of(context)?.isCurrent != true ||
        !widget.store.canConfigureCards) {
      return;
    }
    setState(() {
      _submitted = true;
      _saveError = null;
    });
    if (!_form.currentState!.validate()) return;
    final settings = CenterCardSettings(
      price: _price.text.trim().isEmpty ? null : piastresFromText(_price.text)!,
      requirePaymentBeforeReceipt: _requirePayment,
    );
    setState(() => _busy = true);
    try {
      await widget.store.saveCardSettings(settings);
      if (mounted) {
        setState(() {
          _savedSettings = settings;
          _price.text = settings.price == null
              ? ''
              : priceText(settings.price!);
        });
        await showManagementMessage(context, 'حُفظت إعدادات الكروت.');
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.card_settings_page');
      if (mounted) {
        setState(() => _saveError = managementError(error));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => WorkspaceDraftRegistration(
    dirty: _dirty,
    busy: _busy || _discarding,
    child: CallbackShortcuts(
      bindings: {
        const SingleActivator(
          LogicalKeyboardKey.keyS,
          control: true,
          includeRepeats: false,
        ): _save,
        const SingleActivator(
          LogicalKeyboardKey.keyS,
          meta: true,
          includeRepeats: false,
        ): _save,
      },
      child: ManagementPanel(
        title: 'إعدادات مازن',
        subtitle: 'سعر كارت الطالب وشرط الدفع قبل الاستلام.',
        child: !widget.store.canConfigureCards
            ? const EmptySection(
                message: 'هذه الإعدادات متاحة لحساب مازن المثبت فقط.',
              )
            : MassarScrollView(
                child: Align(
                  alignment: Alignment.topRight,
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 620),
                    child: Form(
                      key: _form,
                      autovalidateMode: _submitted
                          ? AutovalidateMode.onUserInteraction
                          : AutovalidateMode.disabled,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          if (_saveError != null)
                            Padding(
                              padding: const EdgeInsets.only(bottom: 16),
                              child: Semantics(
                                liveRegion: true,
                                child: Text(
                                  'تعذر إتمام الحفظ أو تأكيده. $_saveError\nالتعديلات موجودة؛ صحح السبب ثم حاول مجددًا.',
                                  style: TextStyle(
                                    color: Theme.of(context).colorScheme.error,
                                  ),
                                ),
                              ),
                            ),
                          TextFormField(
                            key: const Key('card-setting-price'),
                            controller: _price,
                            enabled: !_busy && !_discarding,
                            onChanged: (_) => setState(() => _saveError = null),
                            keyboardType: const TextInputType.numberWithOptions(
                              decimal: true,
                            ),
                            decoration: const InputDecoration(
                              labelText: 'سعر كارت الطالب بالجنيه',
                              helperText:
                                  'الفارغ يعني أن السعر لم يُحدد؛ الصفر سعر مجاني.',
                            ),
                            validator: (text) =>
                                text == null || text.trim().isEmpty
                                ? null
                                : validPrice(text),
                          ),
                          const SizedBox(height: 20),
                          SwitchListTile(
                            key: const Key('card-setting-require-payment'),
                            contentPadding: EdgeInsets.zero,
                            title: const Text(
                              'يشترط تسجيل دفع الكارت قبل استلامه',
                            ),
                            subtitle: const Text(
                              'إيقاف الشرط يسمح للاستقبال ومازن بتسجيل استلام كروت الطلبة القدامى دون دفع مسجل في البرنامج.',
                            ),
                            value: _requirePayment,
                            onChanged: _busy || _discarding
                                ? null
                                : (value) =>
                                      setState(() => _requirePayment = value),
                          ),
                          const SizedBox(height: 12),
                          const Text(
                            'دفع الكارت مستقل عن الحصص والباقات، ويطبق خصم الطالب وقت الدفع. تغيير السعر لا يعدّل المدفوعات السابقة. الطباعة وحدها لا تسجل استلامًا.',
                          ),
                          const SizedBox(height: 24),
                          FilledButton.icon(
                            key: const Key('save-card-settings'),
                            onPressed: _busy || _discarding ? null : _save,
                            icon: const Icon(Icons.save_outlined),
                            label: Text(
                              _busy ? 'جارٍ الحفظ…' : 'حفظ إعدادات الكروت',
                            ),
                          ),
                          TextButton(
                            onPressed: _busy || _discarding || !_dirty
                                ? null
                                : _reset,
                            child: const Text('تجاهل التعديلات'),
                          ),
                          const Text('Ctrl / ⌘ + S لحفظ الإعدادات'),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
      ),
    ),
  );
}
