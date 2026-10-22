import 'dart:convert';
import 'dart:typed_data';

import 'package:avelri_gestao/features/students/data/student_import_parser.dart';
import 'package:excel/excel.dart';
import 'package:flutter_test/flutter_test.dart';

Uint8List csv(String value) => Uint8List.fromList(utf8.encode(value));

void main() {
  group('parseStudentImportFile CSV', () {
    test('handles BOM, semicolon, quoted delimiters and escaped quotes', () {
      final parsed = parseStudentImportFile(
        'alunos.csv',
        csv(
          '\uFEFFCliente;Telefones;E-mail\r\n"Ana; Maria";"11; 2222";ana@example.test\r\n"D""Ávila";;',
        ),
      );

      expect(parsed.headers, ['Cliente', 'Telefones', 'E-mail']);
      expect(parsed.rows, hasLength(2));
      expect(parsed.rows.first.rowNumber, 2);
      expect(parsed.rows.first.cells, [
        'Ana; Maria',
        '11; 2222',
        'ana@example.test',
      ]);
      expect(parsed.rows.last.cells.first, 'D"Ávila');
    });

    test('rejects malformed quotes, excess columns and non UTF-8 data', () {
      expect(
        () => parseStudentImportFile(
          'alunos.csv',
          csv('Nome,E-mail\n"Ana,ana@example.test'),
        ),
        throwsA(isA<StudentImportFileException>()),
      );
      expect(
        () => parseStudentImportFile('alunos.csv', csv('Nome\nAna,extra')),
        throwsA(isA<StudentImportFileException>()),
      );
      expect(
        () => parseStudentImportFile('alunos.csv', Uint8List.fromList([0xff])),
        throwsA(isA<StudentImportFileException>()),
      );
    });

    test('keeps missing bytes, size and extension validation', () {
      expect(
        () => parseStudentImportFile('alunos.csv', null),
        throwsA(isA<StudentImportFileException>()),
      );
      expect(
        () => parseStudentImportFile(
          'alunos.csv',
          Uint8List(maxStudentImportFileBytes + 1),
        ),
        throwsA(isA<StudentImportFileException>()),
      );
      expect(
        () => parseStudentImportFile('alunos.txt', csv('Nome\nAna')),
        throwsA(isA<StudentImportFileException>()),
      );
    });
  });

  test('rejects formulas in XLSX workbooks', () {
    final workbook = Excel.createExcel();
    final sheet = workbook['Sheet1'];
    sheet.cell(CellIndex.indexByString('A1')).value = TextCellValue('Nome');
    sheet.cell(CellIndex.indexByString('A2')).value = FormulaCellValue('1+1');
    final bytes = Uint8List.fromList(workbook.encode()!);
    expect(
      () => parseStudentImportFile('alunos.xlsx', bytes),
      throwsA(isA<StudentImportFileException>()),
    );
  });
}
