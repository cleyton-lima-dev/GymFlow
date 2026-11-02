import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:archive/archive.dart';
import 'package:avelri_gestao/features/students/data/student_import_parser.dart';
import 'package:excel/excel.dart';
import 'package:flutter_test/flutter_test.dart';

Uint8List csv(String value) => Uint8List.fromList(utf8.encode(value));

Uint8List xlsxWithAbsoluteWorksheetTarget(Uint8List bytes) {
  final archive = ZipDecoder().decodeBytes(bytes);
  final relationships = archive.findFile('xl/_rels/workbook.xml.rels')!;
  relationships.decompress();
  final xml = utf8.decode(relationships.content as List<int>);
  final absoluteXml = xml.replaceAllMapped(
    RegExp(r'(Target=")(worksheets/[^\"]+)"'),
    (match) => '${match.group(1)}/xl/${match.group(2)}"',
  );
  final content = utf8.encode(absoluteXml);
  archive.addFile(ArchiveFile(relationships.name, content.length, content));
  return Uint8List.fromList(ZipEncoder().encode(archive)!);
}

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

    test('rejects characters after a closing quote with a safe location', () {
      expect(
        () => parseStudentImportFile(
          'alunos.csv',
          csv('Nome,E-mail\n"Ana"x,ana@example.test'),
        ),
        throwsA(
          isA<StudentImportFileException>().having(
            (error) => error.message,
            'message',
            allOf(contains('linha 2, coluna 6'), isNot(contains('Ana'))),
          ),
        ),
      );
    });

    test(
      'rejects quotes in unquoted fields and spaces after closing quotes',
      () {
        expect(
          () => parseStudentImportFile(
            'alunos.csv',
            csv('Nome,E-mail\nAn"a,ana@example.test'),
          ),
          throwsA(
            isA<StudentImportFileException>().having(
              (error) => error.message,
              'message',
              allOf(contains('linha 2, coluna 3'), isNot(contains('An"a'))),
            ),
          ),
        );
        expect(
          () => parseStudentImportFile(
            'alunos.csv',
            csv('Nome,E-mail\n"Ana" ,ana@example.test'),
          ),
          throwsA(
            isA<StudentImportFileException>().having(
              (error) => error.message,
              'message',
              allOf(
                contains('linha 2, coluna 6'),
                contains('Espaços após aspas fechadas não são aceitos'),
                isNot(contains('Ana')),
              ),
            ),
          ),
        );
      },
    );

    test(
      'preserves valid multiline quoted fields and existing edge trimming',
      () {
        final parsed = parseStudentImportFile(
          'alunos.csv',
          csv(
            ' Nome ,E-mail\r\n"Ana\r\nMaria",ana@example.test\r\n Ana ,outra@example.test',
          ),
        );

        expect(parsed.headers, ['Nome', 'E-mail']);
        expect(parsed.rows, hasLength(2));
        expect(parsed.rows.first.rowNumber, 2);
        expect(parsed.rows.first.cells, ['Ana\r\nMaria', 'ana@example.test']);
        expect(parsed.rows.last.cells.first, 'Ana');
      },
    );

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

  test('reads XLSX workbook relationships with absolute package paths', () {
    final workbook = Excel.createExcel();
    final sheet = workbook['Sheet1'];
    sheet.cell(CellIndex.indexByString('A1')).value = TextCellValue('Nome');
    sheet.cell(CellIndex.indexByString('B1')).value = TextCellValue('E-mail');
    sheet.cell(CellIndex.indexByString('A2')).value = TextCellValue(
      'Aluna sintética',
    );
    sheet.cell(CellIndex.indexByString('B2')).value = TextCellValue(
      'aluna@example.test',
    );
    final bytes = xlsxWithAbsoluteWorksheetTarget(
      Uint8List.fromList(workbook.encode()!),
    );

    final parsed = parseStudentImportFile('alunos.xlsx', bytes);

    expect(parsed.headers, ['Nome', 'E-mail']);
    expect(parsed.rows, hasLength(1));
    expect(parsed.rows.single.cells, ['Aluna sintética', 'aluna@example.test']);
  });

  test('reads Excel-generated XLSX with two synthetic student rows', () {
    final bytes = File(
      'test/features/students/fixtures/issue130_excel_two_synthetic_students.xlsx',
    ).readAsBytesSync();
    final parsed = parseStudentImportFile('alunos.xlsx', bytes);
    final headers = parsed.headers.map((value) => value.toLowerCase()).toList();
    final nameIndex = headers.indexOf('nome');
    final emailIndex = headers.indexOf('email');
    final phoneIndex = headers.indexOf('telefone');
    final birthDateIndex = headers.indexOf('nascimento');

    expect(headers, ['nome', 'email', 'telefone', 'nascimento']);
    expect(parsed.rows, hasLength(2));
    expect([
      nameIndex,
      emailIndex,
      phoneIndex,
      birthDateIndex,
    ], everyElement(greaterThanOrEqualTo(0)));
    for (final row in parsed.rows) {
      expect(row.cells.length, parsed.headers.length);
      expect(row.cells[nameIndex].trim().isNotEmpty, isTrue);
      expect(
        RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(row.cells[emailIndex]),
        isTrue,
      );
      final phoneDigits = row.cells[phoneIndex].replaceAll(RegExp(r'\D'), '');
      expect(phoneDigits.length >= 10 && phoneDigits.length <= 13, isTrue);
      expect(_isSupportedDate(row.cells[birthDateIndex]), isTrue);
    }
  });
}

bool _isSupportedDate(String value) {
  if (DateTime.tryParse(value) != null) return true;
  final match = RegExp(r'^(\d{2})/(\d{2})/(\d{4})$').firstMatch(value);
  if (match == null) return false;
  final day = int.parse(match.group(1)!);
  final month = int.parse(match.group(2)!);
  final year = int.parse(match.group(3)!);
  final date = DateTime(year, month, day);
  return date.year == year && date.month == month && date.day == day;
}
