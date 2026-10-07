import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:flutter/material.dart';
import '../../shared/massar_logo.dart';
import '../../application/center_store.dart';
import '../../shared/theme.dart';
import '../../shared/appearance.dart';

class AuthScreen extends StatefulWidget {
  const AuthScreen({super.key, required this.store, this.onOpenLan});
  final CenterStore store;
  final VoidCallback? onOpenLan;
  @override
  State<AuthScreen> createState() => _AuthScreenState();
}

class _AuthScreenState extends State<AuthScreen> {
  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  bool _busy = false;
  bool _showPassword = false;
  String? _error;
  bool _submitted = false;
  final _confirmFocus = FocusNode();

  @override
  void dispose() {
    _name.dispose();
    _password.dispose();
    _confirm.dispose();
    _confirmFocus.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_busy) return;
    setState(() => _submitted = true);
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      if (widget.store.hasStaff) {
        await widget.store.signIn(_name.text.trim(), _password.text);
      } else {
        await widget.store.setupAdmin(_name.text.trim(), _password.text);
      }
    } catch (failure, stackTrace) {
      reportProblem(failure, stackTrace, operation: 'ui.auth_screen');
      if (mounted) {
        setState(
          () => _error = failure is CenterException
              ? failure.message
              : 'تعذر إتمام الدخول. حاول مرة أخرى، ولو الجهاز مرتبط تأكد من اتصال الرئيسي.',
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final firstRun = !widget.store.hasStaff;
    return Scaffold(
      body: LayoutBuilder(
        builder: (context, constraints) {
          final form = _loginForm(firstRun);
          if (constraints.maxWidth < 1000) return form;
          return Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Expanded(flex: 6, child: form),
              Expanded(flex: 5, child: _brandPanel()),
            ],
          );
        },
      ),
    );
  }

  Widget _loginForm(bool firstRun) => Center(
    child: MassarScrollView(
      padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 40),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 440),
        child: Form(
          key: _formKey,
          autovalidateMode: _submitted
              ? AutovalidateMode.onUserInteraction
              : AutovalidateMode.disabled,
          child: AutofillGroup(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: const MassarLogo(height: 56),
                ),
                const Align(
                  alignment: AlignmentDirectional.centerEnd,
                  child: AppearanceToggle(),
                ),
                const SizedBox(height: 20),
                if (widget.onOpenLan != null)
                  TextButton.icon(
                    key: const Key('auth-lan-settings'),
                    onPressed: _busy ? null : widget.onOpenLan,
                    icon: const Icon(Icons.router_outlined),
                    label: const Text('ربط الأجهزة على نفس الراوتر'),
                  ),
                Text(
                  firstRun ? 'نبدأ بإعداد السنتر' : 'أهلًا بعودتك',
                  style: const TextStyle(
                    fontSize: 32,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  firstRun
                      ? 'إنشاء حساب الإدارة على هذا الجهاز'
                      : 'دخول الموظف',
                  style: TextStyle(
                    color: MassarPalette.of(context).muted,
                    fontSize: 16,
                  ),
                ),
                const SizedBox(height: 10),
                Text(
                  firstRun
                      ? 'أنشئ حسابك الأول لإدارة الطلاب والمجموعات والحسابات.'
                      : 'استخدم حسابك لمتابعة الطلاب والمراجعة وتقفيلة الحسابات.',
                  style: TextStyle(
                    color: MassarPalette.of(context).muted,
                    height: 1.6,
                  ),
                ),
                const SizedBox(height: 28),
                TextFormField(
                  key: const Key('auth-name'),
                  controller: _name,
                  autofocus: true,
                  enabled: !_busy,
                  textInputAction: TextInputAction.next,
                  autofillHints: const [AutofillHints.username],
                  decoration: const InputDecoration(labelText: 'اسم المستخدم'),
                  validator: (name) => name == null || name.trim().isEmpty
                      ? 'اكتب اسم المستخدم'
                      : null,
                ),
                const SizedBox(height: 16),
                TextFormField(
                  key: const Key('auth-password'),
                  controller: _password,
                  obscureText: !_showPassword,
                  enabled: !_busy,
                  autofillHints: [
                    firstRun
                        ? AutofillHints.newPassword
                        : AutofillHints.password,
                  ],
                  decoration: InputDecoration(
                    labelText: 'كلمة المرور',
                    suffixIcon: IconButton(
                      key: const Key('auth-password-visibility'),
                      tooltip: _showPassword
                          ? 'إخفاء كلمة المرور'
                          : 'إظهار كلمة المرور',
                      onPressed: _busy
                          ? null
                          : () =>
                                setState(() => _showPassword = !_showPassword),
                      icon: Icon(
                        _showPassword
                            ? Icons.visibility_off_outlined
                            : Icons.visibility_outlined,
                      ),
                    ),
                  ),
                  textInputAction: firstRun
                      ? TextInputAction.next
                      : TextInputAction.done,
                  onFieldSubmitted: (_) =>
                      firstRun ? _confirmFocus.requestFocus() : _submit(),
                  validator: (password) {
                    if (password == null || password.isEmpty) {
                      return 'اكتب كلمة المرور';
                    }
                    if (firstRun && password.length < 8) {
                      return 'استخدم ٨ أحرف على الأقل';
                    }
                    return null;
                  },
                ),
                if (firstRun) ...[
                  const SizedBox(height: 16),
                  TextFormField(
                    key: const Key('auth-confirm'),
                    focusNode: _confirmFocus,
                    textInputAction: TextInputAction.done,
                    controller: _confirm,
                    obscureText: !_showPassword,
                    enabled: !_busy,
                    decoration: const InputDecoration(
                      labelText: 'تأكيد كلمة المرور',
                    ),
                    onFieldSubmitted: (_) => _submit(),
                    validator: (confirmation) => confirmation != _password.text
                        ? 'كلمتا المرور غير متطابقتين'
                        : null,
                  ),
                ],
                if (_error != null) ...[
                  const SizedBox(height: 16),
                  Semantics(
                    liveRegion: true,
                    child: Text(
                      _error!,
                      style: TextStyle(color: MassarPalette.of(context).error),
                    ),
                  ),
                ],
                if (_busy) ...[
                  const SizedBox(height: 16),
                  const LinearProgressIndicator(minHeight: 3),
                ],
                const SizedBox(height: 24),
                FilledButton(
                  key: const Key('auth-submit'),
                  onPressed: _busy ? null : _submit,
                  child: Text(
                    _busy
                        ? 'جارٍ التحقق…'
                        : firstRun
                        ? 'إنشاء الحساب والبدء'
                        : 'تسجيل الدخول',
                  ),
                ),
                const SizedBox(height: 20),
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(
                      Icons.computer_outlined,
                      size: 18,
                      color: MassarPalette.of(context).accent,
                    ),
                    SizedBox(width: 8),
                    Flexible(
                      child: Text(
                        widget.store.isRemote
                            ? 'شبكة محلية • البيانات على الجهاز الرئيسي'
                            : 'أوفلاين • البيانات محفوظة على الجهاز',
                        textAlign: TextAlign.center,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    ),
  );

  Widget _brandPanel() => ColoredBox(
    color: MassarColors.navy,
    child: LayoutBuilder(
      builder: (context, constraints) => MassarScrollView(
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: constraints.maxHeight),
          child: Padding(
            padding: const EdgeInsets.all(48),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text(
                  'مسار / نادر جورج',
                  style: TextStyle(color: Color(0xFFB8D9DE), fontSize: 18),
                ),
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 64),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'يوم السنتر،\nبأرقام واضحة.',
                        style: TextStyle(
                          color: Colors.white,
                          fontSize: 42,
                          fontWeight: FontWeight.w700,
                          height: 1.4,
                        ),
                      ),
                      SizedBox(height: 24),
                      Text(
                        'من دخول الطالب لآخر جنيه في التقفيلة.\nالحضور والتحصيل والمراجعة في مكان واحد.',
                        style: TextStyle(
                          color: Color(0xFFD7E4ED),
                          fontSize: 18,
                          height: 1.8,
                        ),
                      ),
                    ],
                  ),
                ),
                const Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Divider(color: Color(0xFF30425E)),
                    SizedBox(height: 16),
                    Text(
                      'جهاز واحد. حساب مستقل لكل موظف.',
                      style: TextStyle(color: Colors.white, fontSize: 16),
                    ),
                    SizedBox(height: 8),
                    Text(
                      'شغلك محفوظ محليًا، وتقدر تكمّل من غير إنترنت.',
                      style: TextStyle(color: Color(0xFFD7E4ED), height: 1.6),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    ),
  );
}
