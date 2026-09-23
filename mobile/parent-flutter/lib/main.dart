import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:timezone/data/latest.dart' as tz;
import 'package:url_launcher/url_launcher.dart';
import 'data/device_bridge.dart';
import 'data/parent_api.dart';
import 'data/profile_store.dart';
import 'state/parent_controller.dart';
import 'ui/design_system.dart';
import 'ui/link_flow.dart';
import 'ui/dashboard.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  tz.initializeTimeZones();
  final bridge = DeviceBridge();
  runApp(
    ParentApp(
      controller: ParentController(
        api: ParentApi(),
        store: ProfileStore(bridge: bridge),
        bridge: bridge,
      ),
    ),
  );
}

class ParentApp extends StatefulWidget {
  final ParentController controller;
  const ParentApp({super.key, required this.controller});
  @override
  State<ParentApp> createState() => _ParentAppState();
}

class _ParentAppState extends State<ParentApp> with WidgetsBindingObserver {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    widget.controller.initialize();
    widget.controller.bridge.listen(() async {
      await widget.controller.refresh();
      await widget.controller.registerPush();
    });
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed && widget.controller.initialized) {
      widget.controller.refresh();
      widget.controller.registerPush();
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    DeviceBridge.channel.setMethodCallHandler(null);
    widget.controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: widget.controller,
    builder: (context, _) => MaterialApp(
      title: 'مسار • ولي الأمر',
      debugShowCheckedModeBanner: false,
      locale: const Locale('ar'),
      supportedLocales: const [Locale('ar')],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      theme: MassarTokens.theme(Brightness.light),
      darkTheme: MassarTokens.theme(Brightness.dark),
      themeMode: widget.controller.themeMode,
      builder: (context, child) =>
          Directionality(textDirection: TextDirection.rtl, child: child!),
      home: root(),
    ),
  );
  Widget root() {
    final controller = widget.controller;
    if (!controller.initialized) return const SplashScreen();
    if (controller.config?.flag('updateRequired') == true) {
      return UpdateScreen(controller: controller);
    }
    if (controller.failure != null && controller.config == null) {
      return Scaffold(
        body: SafeArea(
          child: ScreenBody(
            children: [
              const BrandLogo(width: 180),
              ErrorPanel(
                controller.failure!.message,
                retry: controller.initialize,
              ),
            ],
          ),
        ),
      );
    }
    if (controller.active == null) return LinkFlow(controller: controller);
    return Dashboard(controller: controller);
  }
}

class UpdateScreen extends StatelessWidget {
  final ParentController controller;
  const UpdateScreen({super.key, required this.controller});
  @override
  Widget build(BuildContext context) => Scaffold(
    body: SafeArea(
      child: ScreenBody(
        children: [
          const BrandLogo(width: 190),
          const PageHeading('تحديث مطلوب'),
          Text(controller.config!.text('updateMessage')),
          PrimaryAction(
            label: 'تحديث التطبيق',
            onPressed: () async {
              final uri = Uri.tryParse(controller.config!.text('updateUrl'));
              if (uri == null ||
                  !['https', 'market', 'itms-apps'].contains(uri.scheme) ||
                  !await launchUrl(uri, mode: LaunchMode.externalApplication)) {
                if (context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(
                      content: Text(
                        'تعذر فتح المتجر. افتح صفحة التطبيق في المتجر لتحديثه.',
                      ),
                    ),
                  );
                }
              }
            },
          ),
          TextButton(
            onPressed: controller.refresh,
            child: const Text('التحقق من التحديث'),
          ),
        ],
      ),
    ),
  );
}
