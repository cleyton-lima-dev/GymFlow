import 'dart:io';

import 'package:flutter/material.dart';

File csvFile(String path) {
  if (!path.toLowerCase().endsWith('.csv') ||
      !File(path).isAbsolute ||
      path.startsWith(r'\\')) {
    throw const FileSystemException(
      'Informe um caminho local absoluto com extensão .csv.',
    );
  }
  return File(path);
}

Future<void> writeCsvFile(
  String path,
  List<int> bytes, {
  bool overwrite = false,
}) async {
  final file = csvFile(path);
  if (!await file.parent.exists()) {
    throw const FileSystemException('A pasta selecionada não existe.');
  }
  if (!overwrite && await file.exists()) {
    throw const FileSystemException('O arquivo já existe.');
  }
  await file.writeAsBytes(bytes, flush: true);
}

// No shell invocation or new plugin: an explicit local path, with overwrite confirmation.
Future<String?> saveReport(
  BuildContext context,
  List<int> bytes,
  String filename,
) async {
  final home = Platform.environment['USERPROFILE'] ?? Directory.current.path;
  final controller = TextEditingController(
    text:
        '$home${Platform.pathSeparator}Downloads${Platform.pathSeparator}$filename',
  );
  final path = await showDialog<String>(
    context: context,
    builder: (dialog) => AlertDialog(
      title: const Text('Salvar CSV'),
      content: SizedBox(
        width: 600,
        child: TextField(
          controller: controller,
          autofocus: true,
          decoration: const InputDecoration(
            labelText: 'Caminho completo do arquivo',
            helperText:
                'Escolha uma pasta local existente. Extensão .csv obrigatória.',
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(dialog),
          child: const Text('Cancelar'),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(dialog, controller.text.trim()),
          child: const Text('Salvar'),
        ),
      ],
    ),
  );
  // Wait for the dialog's route transition before disposing its text controller.
  await Future<void>.delayed(const Duration(milliseconds: 300));
  controller.dispose();
  if (path == null || !context.mounted) return null;
  final file = csvFile(path);
  var approvedOverwrite = false;
  if (await file.exists()) {
    if (!context.mounted) return null;
    final overwrite = await showDialog<bool>(
      context: context,
      builder: (dialog) => AlertDialog(
        title: const Text('Substituir arquivo existente?'),
        content: Text(path),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialog, false),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialog, true),
            child: const Text('Substituir'),
          ),
        ],
      ),
    );
    if (overwrite != true) return null;
    approvedOverwrite = true;
  }
  await writeCsvFile(path, bytes, overwrite: approvedOverwrite);
  return path;
}
