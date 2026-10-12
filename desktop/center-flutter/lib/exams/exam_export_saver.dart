import 'dart:io';

import 'package:path/path.dart' as p;

import '../domain/models.dart';

/// Copies only a previously verified export to a fresh user-selected filename.
class ExamExportSaver {
  ExamExportSaver({
    required Iterable<Directory> protectedDirectories,
    required this.isAuthorized,
  }) : protectedDirectories = List.unmodifiable(protectedDirectories);

  final List<Directory> protectedDirectories;
  final bool Function() isAuthorized;
  static const _extensions = {'.xlsx', '.pdf', '.zip', '.csv', '.sqlite3'};

  Future<File> save(File verifiedExport, String destinationPath) async {
    File? created;
    try {
      _requireAuthorization();
      final destination = await _destination(verifiedExport, destinationPath);
      final expectedBytes = await verifiedExport.length();
      if (expectedBytes == 0) {
        throw const CenterException('ملف التصدير فارغ؛ أعد التنزيل.');
      }
      _requireAuthorization();
      await destination.create(exclusive: true);
      created = destination;
      await _copy(verifiedExport, destination, expectedBytes);
      _requireAuthorization();
      return destination;
    } catch (failure, stack) {
      if (created != null) await created.delete();
      if (failure is CenterException) rethrow;
      throw CenterException(
        'تعذر حفظ الملف؛ اختر اسمًا جديدًا وتأكد من مساحة المجلد وصلاحية الكتابة.',
        cause: failure,
        stackTrace: stack,
      );
    }
  }

  void _requireAuthorization() {
    if (!isAuthorized()) {
      throw const CenterException(
        'انتهت جلسة الموظف؛ سجل الدخول ثم أعد التصدير.',
      );
    }
  }

  Future<File> _destination(File source, String destinationPath) async {
    final extension = p.extension(source.path).toLowerCase();
    if (!_extensions.contains(extension) ||
        !p.isAbsolute(destinationPath) ||
        protectedDirectories.isEmpty ||
        await FileSystemEntity.type(source.path, followLinks: false) !=
            FileSystemEntityType.file) {
      throw const CenterException('مسار ملف التصدير غير صالح.');
    }
    final name = p.basename(destinationPath);
    _validateFilename(name);
    final normalizedName = p.setExtension(name, extension);
    final parent = await Directory(
      p.dirname(destinationPath),
    ).resolveSymbolicLinks();
    final destination = File(p.join(parent, normalizedName));
    await _rejectProtectedDestination(destination.path);
    if (await FileSystemEntity.type(destination.path, followLinks: false) !=
        FileSystemEntityType.notFound) {
      throw const CenterException(
        'الملف موجود بالفعل؛ اختر اسمًا جديدًا للحفاظ عليه.',
      );
    }
    return destination;
  }

  void _validateFilename(String name) {
    if (name.isEmpty ||
        name == '.' ||
        name == '..' ||
        name.endsWith(' ') ||
        name.endsWith('.') ||
        RegExp(r'[<>:"/\\|?*\x00-\x1f]').hasMatch(name) ||
        RegExp(
          r'^(con|prn|aux|nul|com[1-9]|lpt[1-9]|conin\$|conout\$)(?:\.|$)',
          caseSensitive: false,
        ).hasMatch(name)) {
      throw const CenterException('اختر اسم ملف صالحًا للحفظ على ويندوز.');
    }
  }

  Future<void> _rejectProtectedDestination(String destination) async {
    final target = _comparisonPath(destination);
    for (final directory in protectedDirectories) {
      final protected = _comparisonPath(await _resolvedDirectory(directory));
      if (target == protected || target.startsWith('$protected/')) {
        throw const CenterException(
          'احفظ الملف في المستندات أو سطح المكتب، بعيدًا عن ملفات البرنامج والنسخ التلقائية.',
        );
      }
    }
  }

  // Missing protected folders remain protected before their first creation;
  // existing ancestors are resolved so directory links cannot bypass the rule.
  Future<String> _resolvedDirectory(Directory directory) async {
    var ancestor = p.normalize(p.absolute(directory.path));
    final missing = <String>[];
    while (await FileSystemEntity.type(ancestor, followLinks: false) ==
        FileSystemEntityType.notFound) {
      missing.add(p.basename(ancestor));
      final parent = p.dirname(ancestor);
      if (ancestor == parent) {
        throw const CenterException('تعذر التحقق من مجلدات البرنامج المحمية.');
      }
      ancestor = parent;
    }
    return p.joinAll([
      await Directory(ancestor).resolveSymbolicLinks(),
      ...missing.reversed,
    ]);
  }

  String _comparisonPath(String path) => p
      .normalize(path)
      .replaceAll('\\', '/')
      .replaceFirst(RegExp(r'/+$'), '')
      .toLowerCase();

  Future<void> _copy(File source, File destination, int expectedBytes) async {
    if (await FileSystemEntity.type(destination.path, followLinks: false) !=
        FileSystemEntityType.file) {
      throw const CenterException('تغير مسار الحفظ؛ اختر اسمًا جديدًا.');
    }
    final output = await destination.open(mode: FileMode.writeOnly);
    var copied = 0;
    try {
      await for (final chunk in source.openRead()) {
        _requireAuthorization();
        copied += chunk.length;
        if (copied > expectedBytes) {
          throw const CenterException(
            'تغير ملف التصدير أثناء الحفظ؛ أعد تنزيله.',
          );
        }
        await output.writeFrom(chunk);
      }
      if (copied != expectedBytes) {
        throw const CenterException('لم يُنسخ الملف كاملًا؛ أعد المحاولة.');
      }
      await output.flush();
      _requireAuthorization();
    } finally {
      await output.close();
    }
  }
}
