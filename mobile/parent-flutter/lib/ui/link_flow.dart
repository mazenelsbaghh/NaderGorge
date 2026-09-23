import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../data/models.dart';
import '../data/parent_api.dart';
import '../state/parent_controller.dart';
import 'design_system.dart';

class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});
  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: Colors.white,
    body: WaveCanvas(
      child: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) => SingleChildScrollView(
            child: ConstrainedBox(
              constraints: BoxConstraints(
                minHeight: constraints.maxHeight,
                minWidth: constraints.maxWidth,
              ),
              child: Padding(
                padding: const EdgeInsets.all(32),
                child: Column(
                  children: [
                    SizedBox(height: constraints.maxHeight * .24),
                    const Entrance(child: BrandLogo(width: 310)),
                    const SizedBox(height: 30),
                    Text(
                      'ولي الأمر',
                      style: Theme.of(context).textTheme.headlineMedium,
                    ),
                    SizedBox(height: constraints.maxHeight * .18),
                    const CircularProgressIndicator(color: MassarTokens.teal),
                    const SizedBox(height: 20),
                    const Text('جاري فتح التطبيق'),
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

class LinkFlow extends StatefulWidget {
  final ParentController controller;
  final bool welcome;
  const LinkFlow({super.key, required this.controller, this.welcome = true});
  @override
  State<LinkFlow> createState() => _LinkFlowState();
}

class _LinkFlowState extends State<LinkFlow> {
  final code = TextEditingController();
  final form = GlobalKey<FormState>();
  late bool welcome = widget.welcome;
  bool busy = false;
  String? error;
  LinkedStudent? candidate;
  StudentDetails? details;
  @override
  void dispose() {
    code.dispose();
    super.dispose();
  }

  Future<void> verify() async {
    if (!form.currentState!.validate()) return;
    setState(() {
      busy = true;
      error = null;
    });
    try {
      final student = await widget.controller.api.verify(
        code.text.trim().toUpperCase(),
      );
      final profile = await widget.controller.api.details(student);
      if (mounted) {
        setState(() {
          candidate = student;
          details = profile;
        });
      }
    } on ParentFailure catch (failure) {
      if (mounted) setState(() => error = failure.message);
    } on FormatException {
      if (mounted) {
        setState(
          () => error = 'استجابة رمز المتابعة غير صالحة. حاول مرة أخرى.',
        );
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> confirm() async {
    setState(() {
      busy = true;
      error = null;
    });
    try {
      await widget.controller.confirm(candidate!);
      if (mounted && Navigator.of(context).canPop()) {
        Navigator.of(context).pop();
      }
    } on PlatformException {
      if (mounted) {
        setState(() => error = 'تعذر حفظ الربط بأمان. أعد المحاولة.');
      }
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(toolbarHeight: 48, backgroundColor: Colors.transparent),
    body: AnimatedSwitcher(
      duration: MassarMotion.duration(
        context,
        const Duration(milliseconds: 360),
      ),
      child: ScreenBody(
        key: ValueKey(
          welcome
              ? 'welcome'
              : candidate == null
              ? 'code'
              : 'confirm',
        ),
        children: [
          const Center(child: BrandLogo(width: 190)),
          const SizedBox(height: 12),
          ...welcome
              ? welcomeChildren(context)
              : candidate == null
              ? codeChildren(context)
              : confirmationChildren(context),
        ],
      ),
    ),
  );

  List<Widget> welcomeChildren(BuildContext context) => [
    SoftPanel(
      tint: MassarTokens.mint,
      child: LayoutBuilder(
        builder: (context, constraints) {
          final arc = const ProgressArc(
            progress: .75,
            caption: 'تقدّم الحصص',
            detail: 'مثال توضيحي',
          );
          final illustration = Image.asset(
            'assets/onboarding.png',
            height: 230,
            fit: BoxFit.contain,
            excludeFromSemantics: true,
          );
          return Column(
            children: [
              if (MediaQuery.textScalerOf(context).scale(14) > 20) ...[
                arc,
                illustration,
              ] else
                Row(
                  textDirection: TextDirection.ltr,
                  children: [
                    Expanded(child: arc),
                    Expanded(child: Entrance(order: 2, child: illustration)),
                  ],
                ),
              const SizedBox(height: 16),
              const Text(
                'مستقبل أفضل يبدأ بالمتابعة اليوم',
                textAlign: TextAlign.center,
                style: TextStyle(fontWeight: FontWeight.w700),
              ),
            ],
          );
        },
      ),
    ),
    const PageHeading(
      'تابع تقدّم ابنك بوضوح',
      subtitle: 'الحصص والنتائج والواجبات في مكان واحد',
    ),
    const Row(
      mainAxisAlignment: MainAxisAlignment.spaceEvenly,
      children: [
        Column(
          children: [
            IconBadge(Icons.play_arrow_rounded),
            SizedBox(height: 8),
            Text('الحصص'),
          ],
        ),
        Column(
          children: [
            IconBadge(Icons.bar_chart_rounded),
            SizedBox(height: 8),
            Text('النتائج'),
          ],
        ),
        Column(
          children: [
            IconBadge(Icons.notifications_rounded),
            SizedBox(height: 8),
            Text('التنبيهات'),
          ],
        ),
      ],
    ),
    PrimaryAction(
      label: 'ربط طالب',
      onPressed: () => setState(() => welcome = false),
    ),
    const Text(
      'استخدم رمز المتابعة من حساب الطالب',
      textAlign: TextAlign.center,
    ),
  ];

  List<Widget> codeChildren(BuildContext context) => [
    const PageHeading('أدخل رمز المتابعة', subtitle: 'رمز من 6 أحرف أو أرقام'),
    SoftPanel(
      child: Form(
        key: form,
        child: Column(
          children: [
            TextFormField(
              controller: code,
              textDirection: TextDirection.ltr,
              textAlign: TextAlign.center,
              maxLength: 6,
              enabled: !busy,
              textCapitalization: TextCapitalization.characters,
              inputFormatters: [
                FilteringTextInputFormatter.allow(RegExp('[a-zA-Z0-9]')),
              ],
              style: const TextStyle(
                fontSize: 28,
                letterSpacing: 5,
                fontWeight: FontWeight.w700,
              ),
              decoration: InputDecoration(
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(24),
                  borderSide: const BorderSide(color: MassarTokens.mint),
                ),
                labelText: 'رمز المتابعة',
                hintText: 'A1B2C3',
                counterText: '',
              ),
              validator: (value) =>
                  RegExp(r'^[A-Za-z0-9]{6}$').hasMatch(value?.trim() ?? '')
                  ? null
                  : 'أدخل رمزًا مكوّنًا من 6 أحرف أو أرقام',
              onFieldSubmitted: (_) {
                if (!busy) verify();
              },
            ),
            TextButton.icon(
              onPressed: busy
                  ? null
                  : () async {
                      final clipboard = await Clipboard.getData(
                        Clipboard.kTextPlain,
                      );
                      if (mounted) {
                        code.text = (clipboard?.text ?? '')
                            .trim()
                            .toUpperCase();
                      }
                    },
              icon: const Icon(Icons.content_paste_rounded),
              label: const Text('لصق الرمز'),
            ),
            const Text('ستجد الرمز في حساب الطالب'),
            const ExpansionTile(
              title: Text('أين أجد الرمز؟'),
              children: [
                Padding(
                  padding: EdgeInsets.all(16),
                  child: Text(
                    'اطلب من الطالب فتح حسابه في مسار ونسخ رمز متابعة ولي الأمر، ثم أدخله هنا.',
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    ),
    if (error != null)
      Text(error!, style: const TextStyle(color: MassarTokens.danger)),
    PrimaryAction(label: 'التحقق من الرمز', onPressed: verify, busy: busy),
  ];

  List<Widget> confirmationChildren(BuildContext context) => [
    const PageHeading(
      'تأكد من بيانات الطالب',
      subtitle: 'هل هذه بيانات الطالب الذي تريد متابعته؟',
    ),
    SoftPanel(
      child: Column(
        children: [
          CircleAvatar(
            radius: 40,
            backgroundColor: MassarTokens.mint,
            child: Text(
              candidate!.name.characters.first,
              style: const TextStyle(
                fontSize: 38,
                color: MassarTokens.teal,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          const SizedBox(height: 20),
          Text(
            candidate!.name,
            style: Theme.of(context).textTheme.headlineMedium,
          ),
          const Divider(),
          MassarDataRow(
            'الصف الدراسي',
            subtitle: details!.text('grade'),
            icon: Icons.school_outlined,
          ),
          if (details!.text('school').isNotEmpty)
            MassarDataRow(
              'المدرسة',
              subtitle: details!.text('school'),
              icon: Icons.apartment,
            ),
        ],
      ),
    ),
    if (error != null)
      Text(error!, style: const TextStyle(color: MassarTokens.danger)),
    PrimaryAction(label: 'تأكيد الربط', onPressed: confirm, busy: busy),
    TextButton(
      onPressed: busy
          ? null
          : () => setState(() {
              candidate = null;
              details = null;
            }),
      child: const Text('تعديل الرمز'),
    ),
  ];
}
