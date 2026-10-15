import 'package:flutter/material.dart';

import '../../exams/omada_models.dart';
import '../../exams/omada_setup_links.dart';

class OmadaFirstSetupHelp extends StatelessWidget {
  const OmadaFirstSetupHelp({
    super.key,
    required this.disabled,
    required this.onOpen,
    required this.controllerAddress,
  });
  final bool disabled;
  final Future<void> Function(Uri) onOpen;
  final String controllerAddress;

  @override
  Widget build(BuildContext context) => ExpansionTile(
    tilePadding: EdgeInsets.zero,
    title: const Text('معاك الجهازين والأكسس بوينت فقط؟'),
    childrenPadding: const EdgeInsets.only(bottom: 12),
    expandedCrossAxisAlignment: CrossAxisAlignment.start,
    children: [
      const Text(
        'ثبّت Omada Software Controller على الساكند مرة واحدة. تنزيله يحتاج إنترنت، وبعد ربطه تعمل الإدارة المحلية بدون إنترنت.',
      ),
      const SizedBox(height: 10),
      const Text(
        'لو الأكسس بوينت مُدار بالفعل، استخدم إدارته الحالية. عند نقله لإدارة جديدة، احتفظ بإعداداته واسم شبكة الطلاب وكلمة سرها قبل تبنّيه؛ لا تعمل Reset.',
      ),
      const SizedBox(height: 10),
      const Text(
        'داخل إدارة Omada: Settings ← Platform Integration ← Open API ← Add New App. اختَر Client Credentials، وامنح الربط صلاحية تعديل الشبكات للموقع المقصود. انسخ عنوان الإدارة وOmada ID وClient ID وClient Secret في الحقول التالية.',
      ),
      const SizedBox(height: 12),
      Wrap(
        spacing: 10,
        runSpacing: 8,
        children: [
          OutlinedButton.icon(
            onPressed: disabled ? null : () => onOpen(OmadaSetupLinks.download),
            icon: const Icon(Icons.download_outlined),
            label: const Text('تنزيل الإدارة من TP-Link'),
          ),
          OutlinedButton.icon(
            onPressed: disabled
                ? null
                : () {
                    final uri = Uri.tryParse(controllerAddress);
                    if (uri != null) onOpen(uri);
                  },
            icon: const Icon(Icons.open_in_browser),
            label: const Text('فتح إدارة Omada'),
          ),
          TextButton(
            onPressed: disabled
                ? null
                : () => onOpen(OmadaSetupLinks.integrationGuide),
            child: const Text('شرح الربط الرسمي'),
          ),
        ],
      ),
    ],
  );
}

class OmadaConnectionForm extends StatelessWidget {
  const OmadaConnectionForm({
    super.key,
    required this.address,
    required this.omadaId,
    required this.clientId,
    required this.secret,
    required this.disabled,
    required this.onChanged,
  });
  final TextEditingController address, omadaId, clientId, secret;
  final bool disabled;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) => Column(
    children: [
      _field(
        address,
        'عنوان إدارة Omada المحلي',
        hint: 'https://127.0.0.1:8043',
      ),
      const SizedBox(height: 10),
      _field(omadaId, 'Omada ID من بيانات الإدارة'),
      const SizedBox(height: 10),
      _field(clientId, 'Client ID لربط مسار'),
      const SizedBox(height: 10),
      _field(secret, 'Client Secret لربط مسار', password: true),
    ],
  );

  Widget _field(
    TextEditingController controller,
    String label, {
    String? hint,
    bool password = false,
  }) => TextField(
    controller: controller,
    enabled: !disabled,
    obscureText: password,
    autocorrect: false,
    enableSuggestions: false,
    textDirection: TextDirection.ltr,
    onChanged: onChanged,
    decoration: InputDecoration(labelText: label, hintText: hint),
  );
}

class OmadaNetworkChoices extends StatelessWidget {
  const OmadaNetworkChoices({
    super.key,
    required this.disabled,
    required this.sites,
    required this.wlans,
    required this.networks,
    required this.accessPoints,
    required this.site,
    required this.wlan,
    required this.student,
    required this.accessPoint,
    required this.onSite,
    required this.onWlan,
    required this.onStudent,
    required this.onAccessPoint,
  });
  final bool disabled;
  final List<OmadaSite> sites;
  final List<OmadaWlanGroup> wlans;
  final List<OmadaSsid> networks;
  final List<OmadaAccessPoint> accessPoints;
  final String? site, wlan, student, accessPoint;
  final ValueChanged<String?> onSite, onWlan, onStudent, onAccessPoint;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      _choice(
        'الموقع الموجود فيه Omada',
        site,
        sites.map((row) => (row.id, row.name)).toList(),
        onSite,
      ),
      if (site != null) ...[
        const SizedBox(height: 12),
        _choice(
          'جهاز Omada المتصل بالكابل',
          accessPoint,
          accessPoints
              .where((row) => row.isConnected)
              .map(
                (row) => (row.mac, '${row.name} · ${row.model} · ${row.mac}'),
              )
              .toList(),
          onAccessPoint,
        ),
        if (!accessPoints.any((row) => row.isConnected))
          const Padding(
            padding: EdgeInsets.only(top: 8),
            child: Text(
              'لم يظهر أكسس بوينت متصل في هذه الإدارة. راجع الكابل وربط الجهاز بالموقع أولًا.',
            ),
          ),
        const SizedBox(height: 12),
        _choice(
          'مجموعة الشبكات الحالية في Omada',
          wlan,
          wlans.map((row) => (row.id, row.name)).toList(),
          onWlan,
        ),
      ],
      if (wlan != null && networks.isNotEmpty) ...[
        const SizedBox(height: 12),
        _choice(
          'شبكة الطلاب الحالية، سيبقى اسمها كما هو',
          student,
          networks.map((row) => (row.id, row.name)).toList(),
          onStudent,
        ),
        const SizedBox(height: 6),
        const Text(
          'اختَر الشبكة التي يتصل بها الطلاب الآن. لا يوجد تعديل لاسمها أو كلمة سرها.',
        ),
      ],
    ],
  );

  Widget _choice(
    String label,
    String? selected,
    List<(String, String)> rows,
    ValueChanged<String?> onChanged,
  ) => DropdownButtonFormField<String>(
    key: ValueKey('$label:$selected:${rows.length}'),
    initialValue: rows.any((row) => row.$1 == selected) ? selected : null,
    isExpanded: true,
    decoration: InputDecoration(labelText: label),
    items: rows
        .map(
          (row) => DropdownMenuItem(
            value: row.$1,
            child: Text(row.$2, overflow: TextOverflow.ellipsis),
          ),
        )
        .toList(),
    onChanged: disabled || rows.isEmpty ? null : onChanged,
  );
}

class OmadaCertificateDialog extends StatelessWidget {
  const OmadaCertificateDialog({
    super.key,
    required this.address,
    required this.certificate,
  });
  final String address;
  final OmadaCertificateInfo certificate;

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('التحقق من إدارة Omada المحلية'),
    content: SingleChildScrollView(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'قارن البصمة مع شهادة صفحة إدارة Omada في المتصفح قبل اعتمادها. هذه الخطوة لا ترسل بيانات الربط.',
          ),
          const SizedBox(height: 12),
          SelectableText(address, textDirection: TextDirection.ltr),
          const SizedBox(height: 10),
          const Text('بصمة الشهادة SHA-256'),
          SelectableText(certificate.sha256, textDirection: TextDirection.ltr),
          const SizedBox(height: 10),
          Text(
            'صالحة حتى: ${certificate.validUntil.toLocal().toString().split(' ').first}',
          ),
        ],
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context, false),
        child: const Text('رجوع'),
      ),
      FilledButton(
        onPressed: () => Navigator.pop(context, true),
        child: const Text('البصمة مطابقة، اعتماد الإدارة'),
      ),
    ],
  );
}
