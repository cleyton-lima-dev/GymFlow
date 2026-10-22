import 'dart:io';

import 'package:avelri_gestao/features/students/data/column_header_mapper.dart';
import 'package:avelri_gestao/features/students/data/migration_import_templates.dart';
import 'package:avelri_gestao/features/students/data/student_import_parser.dart';
import 'package:avelri_gestao/features/students/presentation/migration_wizard_page.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('all official CSV models parse and map every supported field', () async {
    expect(migrationImportTemplates, hasLength(4));

    for (final template in migrationImportTemplates) {
      final data = await rootBundle.load(template.assetPath);
      final bytes = Uint8List.fromList(
        data.buffer.asUint8List(data.offsetInBytes, data.lengthInBytes),
      );
      final parsed = parseStudentImportFile(template.fileName, bytes);

      expect(parsed.headers, template.fields, reason: template.fileName);
      expect(parsed.rows, hasLength(1), reason: template.fileName);

      final mapping = <String, int?>{};
      for (final field in template.fields) {
        final suggested = ColumnHeaderMapper.suggest(parsed.headers, field);
        expect(
          suggested.isAmbiguous,
          isFalse,
          reason: '${template.fileName}: $field',
        );
        expect(
          suggested.index,
          isNotNull,
          reason: '${template.fileName}: $field',
        );
        mapping[field] = suggested.index;
      }

      final row = buildMigrationRowPayload(
        parsed.rows.single.rowNumber,
        template.fields,
        mapping,
        parsed.rows.single.cells,
      );
      expect(row['rowNumber'], 2, reason: template.fileName);
      expect(row['externalId'], isNotEmpty, reason: template.fileName);
      expect(row.keys, containsAll(template.fields));
    }
  });

  test('writes the selected official model without changing its bytes', () async {
    final template = migrationImportTemplates[1];
    final file = File(
      '${Directory.systemTemp.path}${Platform.pathSeparator}avelri-modelo-planos-${DateTime.now().microsecondsSinceEpoch}.csv',
    );
    addTearDown(() async {
      if (await file.exists()) await file.delete();
    });

    await saveMigrationTemplateFile(template, file.path);
    final data = await rootBundle.load(template.assetPath);
    expect(
      await file.readAsBytes(),
      data.buffer.asUint8List(data.offsetInBytes, data.lengthInBytes),
    );
  });
}
