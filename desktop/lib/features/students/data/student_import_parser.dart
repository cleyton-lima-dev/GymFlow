import 'dart:convert';

import 'package:archive/archive.dart';
import 'package:excel/excel.dart';
import 'package:flutter/foundation.dart';

const maxStudentImportFileBytes = 5 * 1024 * 1024;
const maxStudentImportRows = 5000;
const maxStudentImportColumns = 100;

class ParsedStudentImportFile {
  const ParsedStudentImportFile({
    required this.fileName,
    required this.headers,
    required this.rows,
  });

  final String fileName;
  final List<String> headers;
  final List<ParsedStudentImportRow> rows;
}

class ParsedStudentImportRow {
  const ParsedStudentImportRow({required this.rowNumber, required this.cells});

  final int rowNumber;
  final List<String> cells;
}

class StudentImportFileException implements Exception {
  const StudentImportFileException(this.message);

  final String message;

  @override
  String toString() => message;
}

ParsedStudentImportFile parseStudentImportFile(
  String fileName,
  Uint8List? bytes,
) {
  if (bytes == null || bytes.isEmpty) {
    throw const StudentImportFileException(
      'Não foi possível ler o arquivo selecionado.',
    );
  }
  if (bytes.length > maxStudentImportFileBytes) {
    throw const StudentImportFileException(
      'O arquivo deve ter no máximo 5 MB.',
    );
  }

  final extension = fileName.contains('.')
      ? fileName.split('.').last.toLowerCase()
      : null;
  if (extension == 'csv') return _parseCsv(fileName, bytes);
  if (extension == 'xlsx') return _parseXlsx(fileName, bytes);
  throw const StudentImportFileException('Selecione um arquivo CSV ou XLSX.');
}

ParsedStudentImportFile _parseCsv(String fileName, Uint8List bytes) {
  String text;
  try {
    text = utf8.decode(bytes, allowMalformed: false);
  } on FormatException {
    throw const StudentImportFileException(
      'O CSV deve usar codificação UTF-8.',
    );
  }
  if (text.startsWith('\uFEFF')) text = text.substring(1);
  final delimiter = _detectDelimiter(text);
  final records = _readCsv(text, delimiter);
  return _toParsedFile(fileName, records);
}

ParsedStudentImportFile _parseXlsx(String fileName, Uint8List bytes) {
  if (bytes.length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4b) {
    throw const StudentImportFileException(
      'O conteúdo do arquivo não corresponde a um XLSX válido.',
    );
  }
  var stage = 'ZIP directory validation';
  try {
    _validateZipDirectory(bytes);
    stage = 'ZIP extraction';
    final archive = ZipDecoder().decodeBytes(bytes);
    final files = archive.files.where((entry) => entry.isFile).toList();
    final expandedBytes = files.fold<int>(
      0,
      (total, entry) => total + entry.size,
    );
    if (files.length > 200 ||
        expandedBytes > 25 * 1024 * 1024 ||
        files.any((entry) => entry.size > 10 * 1024 * 1024)) {
      throw const StudentImportFileException(
        'A planilha excede os limites seguros de conteúdo descompactado.',
      );
    }

    stage = 'workbook relationship normalization';
    final workbookBytes = _normalizeWorkbookPackageXml(archive, bytes);
    stage = 'Excel workbook decoding';
    final workbook = Excel.decodeBytes(workbookBytes);
    if (workbook.tables.isEmpty) {
      throw const StudentImportFileException(
        'A planilha não contém uma aba com dados.',
      );
    }
    stage = 'first worksheet selection';
    final sheet = workbook.tables[workbook.tables.keys.first]!;
    if (sheet.maxRows > maxStudentImportRows + 1 ||
        sheet.maxColumns > maxStudentImportColumns) {
      throw const StudentImportFileException(
        'A primeira aba deve ter no máximo 5.000 registros e 100 colunas.',
      );
    }
    final records = <List<String>>[];
    stage = 'worksheet cell conversion';
    for (final row in sheet.rows) {
      final values = <String>[];
      for (final cell in row) {
        if (cell?.value is FormulaCellValue) {
          throw const StudentImportFileException(
            'A planilha contém fórmulas. Converta os campos mapeados em valores antes de importar.',
          );
        }
        values.add(_xlsxCellToText(cell));
      }
      records.add(values);
    }
    stage = 'header and row validation';
    return _toParsedFile(fileName, records);
  } on StudentImportFileException {
    rethrow;
  } catch (error, stackTrace) {
    // Keep diagnostics useful without logging workbook content or exception
    // messages that could contain values from a student's cells.
    if (kDebugMode) {
      final origin = stackTrace.toString().split('\n').take(4).join(' <- ');
      debugPrint(
        'Student XLSX parsing failed at $stage (${error.runtimeType}); $origin',
      );
    }
    throw const StudentImportFileException(
      'Não foi possível ler a primeira aba do XLSX.',
    );
  }
}

Uint8List _normalizeWorkbookPackageXml(
  Archive archive,
  Uint8List originalBytes,
) {
  var changed = false;
  final packageXml = archive.files.where((entry) {
    if (!entry.isFile) return false;
    if (entry.name == 'xl/_rels/workbook.xml.rels' ||
        entry.name == 'xl/workbook.xml' ||
        entry.name == 'xl/styles.xml' ||
        entry.name == 'xl/sharedStrings.xml') {
      return true;
    }
    return entry.name.startsWith('xl/worksheets/') &&
        entry.name.endsWith('.xml');
  }).toList();

  for (final entry in packageXml) {
    entry.decompress();
    final content = entry.content;
    if (content is! List<int>) {
      throw const StudentImportFileException(
        'A estrutura XML do XLSX não pôde ser lida.',
      );
    }

    final xml = utf8.decode(content, allowMalformed: false);
    var normalizedXml = xml;
    if (entry.name == 'xl/_rels/workbook.xml.rels') {
      normalizedXml = normalizedXml.replaceAllMapped(
        RegExp(r'''(Target\s*=\s*["'])/xl/'''),
        (match) => match.group(1)!,
      );
    }

    final spreadsheetPrefixes = RegExp(
      r'''xmlns:([A-Za-z_][A-Za-z0-9_.-]*)\s*=\s*["']http://schemas\.openxmlformats\.org/spreadsheetml/2006/main["']''',
    ).allMatches(normalizedXml).map((match) => match.group(1)!).toSet();
    for (final prefix in spreadsheetPrefixes) {
      final elementPrefix = RegExp('<(/?)${RegExp.escape(prefix)}:');
      normalizedXml = normalizedXml.replaceAllMapped(
        elementPrefix,
        (match) => '<${match.group(1)}',
      );
    }

    if (entry.name.startsWith('xl/worksheets/')) {
      normalizedXml = _normalizeStringCellsWithoutFormulas(normalizedXml);
    }
    if (normalizedXml == xml) continue;
    final normalizedBytes = utf8.encode(normalizedXml);
    archive.addFile(
      ArchiveFile(entry.name, normalizedBytes.length, normalizedBytes),
    );
    changed = true;
  }

  if (!changed) return originalBytes;
  final encoded = ZipEncoder().encode(archive);
  if (encoded == null) {
    throw const StudentImportFileException(
      'A estrutura do XLSX não pôde ser normalizada.',
    );
  }
  return Uint8List.fromList(encoded);
}

String _normalizeStringCellsWithoutFormulas(String xml) {
  final cellPattern = RegExp(r'<c\b([^>]*)>(.*?)</c>', dotAll: true);
  final formulaPattern = RegExp(r'<(?:[A-Za-z_][A-Za-z0-9_.-]*:)?f(?:\s|/|>)');
  final valuePattern = RegExp(r'<v(?:\s[^>]*)?>(.*?)</v>', dotAll: true);
  final stringTypePattern = RegExp(r'''(\bt\s*=\s*)(["'])str\2''');

  return xml.replaceAllMapped(cellPattern, (cellMatch) {
    final attributes = cellMatch.group(1)!;
    final body = cellMatch.group(2)!;
    final typeMatch = stringTypePattern.firstMatch(attributes);
    if (typeMatch == null || formulaPattern.hasMatch(body)) {
      return cellMatch.group(0)!;
    }

    final valueMatch = valuePattern.firstMatch(body);
    final normalizedBody = valueMatch == null
        ? (body.trim().isEmpty ? '<is><t></t></is>' : null)
        : body.replaceRange(
            valueMatch.start,
            valueMatch.end,
            '<is><t>${valueMatch.group(1)!}</t></is>',
          );
    if (normalizedBody == null) return cellMatch.group(0)!;

    final normalizedAttributes = attributes.replaceFirstMapped(
      stringTypePattern,
      (match) =>
          '${match.group(1)!}${match.group(2)!}inlineStr${match.group(2)!}',
    );
    return '<c$normalizedAttributes>$normalizedBody</c>';
  });
}

void _validateZipDirectory(Uint8List bytes) {
  // Inspect declared expansion sizes before the decoder inflates workbook entries.
  final searchStart = (bytes.length - 22 - 0xffff).clamp(0, bytes.length);
  var endRecord = -1;
  for (var offset = bytes.length - 22; offset >= searchStart; offset--) {
    if (_readU32(bytes, offset) == 0x06054b50) {
      endRecord = offset;
      break;
    }
  }
  if (endRecord < 0) {
    throw const StudentImportFileException(
      'O XLSX não contém um diretório ZIP válido.',
    );
  }
  final entryCount = _readU16(bytes, endRecord + 10);
  final directorySize = _readU32(bytes, endRecord + 12);
  final directoryOffset = _readU32(bytes, endRecord + 16);
  if (entryCount == 0xffff ||
      directorySize == 0xffffffff ||
      directoryOffset == 0xffffffff ||
      directoryOffset + directorySize > endRecord ||
      entryCount > 200) {
    throw const StudentImportFileException(
      'A planilha excede os limites seguros de conteúdo.',
    );
  }

  var offset = directoryOffset;
  var totalExpanded = 0;
  for (var index = 0; index < entryCount; index++) {
    if (offset + 46 > bytes.length || _readU32(bytes, offset) != 0x02014b50) {
      throw const StudentImportFileException(
        'O diretório do XLSX está inválido.',
      );
    }
    final compressed = _readU32(bytes, offset + 20);
    final expanded = _readU32(bytes, offset + 24);
    final nameLength = _readU16(bytes, offset + 28);
    final extraLength = _readU16(bytes, offset + 30);
    final commentLength = _readU16(bytes, offset + 32);
    if (compressed == 0xffffffff ||
        expanded == 0xffffffff ||
        expanded > 10 * 1024 * 1024) {
      throw const StudentImportFileException(
        'A planilha excede os limites seguros de conteúdo.',
      );
    }
    totalExpanded += expanded;
    if (totalExpanded > 25 * 1024 * 1024) {
      throw const StudentImportFileException(
        'A planilha excede os limites seguros de conteúdo.',
      );
    }
    offset += 46 + nameLength + extraLength + commentLength;
    if (offset > directoryOffset + directorySize) {
      throw const StudentImportFileException(
        'O diretório do XLSX está inválido.',
      );
    }
  }
}

int _readU16(Uint8List bytes, int offset) =>
    bytes[offset] | (bytes[offset + 1] << 8);

int _readU32(Uint8List bytes, int offset) =>
    bytes[offset] |
    (bytes[offset + 1] << 8) |
    (bytes[offset + 2] << 16) |
    (bytes[offset + 3] << 24);

String _xlsxCellToText(Data? cell) {
  final value = cell?.value;
  if (value == null) return '';
  if (value is TextCellValue) return value.value.text ?? '';
  if (value is IntCellValue) return value.value.toString();
  if (value is DoubleCellValue) return value.value.toString();
  if (value is BoolCellValue) return value.value.toString();
  if (value is DateCellValue) {
    final date = value.asDateTimeLocal();
    return '${date.year.toString().padLeft(4, '0')}-'
        '${date.month.toString().padLeft(2, '0')}-'
        '${date.day.toString().padLeft(2, '0')}';
  }
  if (value is FormulaCellValue) {
    throw const StudentImportFileException(
      'Fórmulas não são aceitas no arquivo.',
    );
  }
  return value.toString();
}

ParsedStudentImportFile _toParsedFile(
  String fileName,
  List<List<String>> records,
) {
  if (records.isEmpty) {
    throw const StudentImportFileException('O arquivo está vazio.');
  }
  final headers = records.first.map((value) => value.trim()).toList();
  if (headers.length > maxStudentImportColumns) {
    throw const StudentImportFileException(
      'O arquivo deve ter no máximo 100 colunas.',
    );
  }
  if (headers.every((header) => header.isEmpty)) {
    throw const StudentImportFileException(
      'A primeira linha deve conter os nomes das colunas.',
    );
  }
  if (records.skip(1).any((row) => row.length > headers.length)) {
    throw const StudentImportFileException(
      'Há linhas com mais colunas do que o cabeçalho. Corrija o arquivo antes de importar.',
    );
  }

  final rows = <ParsedStudentImportRow>[];
  for (var index = 1; index < records.length; index++) {
    final values = List<String>.filled(headers.length, '');
    final source = records[index];
    for (
      var column = 0;
      column < headers.length && column < source.length;
      column++
    ) {
      values[column] = source[column].trim();
    }
    if (values.every((value) => value.isEmpty)) continue;
    rows.add(ParsedStudentImportRow(rowNumber: index + 1, cells: values));
    if (rows.length > maxStudentImportRows) {
      throw const StudentImportFileException(
        'O arquivo deve ter no máximo 5.000 registros.',
      );
    }
  }
  if (rows.isEmpty) {
    throw const StudentImportFileException(
      'O arquivo não contém registros após o cabeçalho.',
    );
  }
  return ParsedStudentImportFile(
    fileName: fileName,
    headers: headers,
    rows: rows,
  );
}

String _detectDelimiter(String text) {
  final sample = text.split(RegExp(r'\r?\n')).first;
  final counts = <String, int>{};
  for (final delimiter in [',', ';', '\t']) {
    var quoted = false;
    var count = 0;
    for (var index = 0; index < sample.length; index++) {
      if (sample[index] == '"') {
        if (quoted && index + 1 < sample.length && sample[index + 1] == '"') {
          index++;
        } else {
          quoted = !quoted;
        }
      } else if (!quoted && sample[index] == delimiter) {
        count++;
      }
    }
    counts[delimiter] = count;
  }
  return counts.entries
      .reduce((left, right) => left.value >= right.value ? left : right)
      .key;
}

enum _CsvFieldState { unquoted, quoted, afterQuote }

List<List<String>> _readCsv(String text, String delimiter) {
  final records = <List<String>>[];
  var row = <String>[];
  final cell = StringBuffer();
  // Strict quoting: a quote starts only an empty field, and no whitespace is
  // permitted between a closing quote and its delimiter or record ending.
  var state = _CsvFieldState.unquoted;
  var lineNumber = 1;
  var columnNumber = 0;
  var openingQuoteLine = 0;
  var openingQuoteColumn = 0;

  void finishField() {
    row.add(cell.toString());
    cell.clear();
    state = _CsvFieldState.unquoted;
  }

  void finishRecord() {
    finishField();
    records.add(row);
    row = <String>[];
  }

  int lineEndingLength(int index) =>
      text[index] == '\r' && index + 1 < text.length && text[index + 1] == '\n'
      ? 2
      : 1;

  for (var index = 0; index < text.length;) {
    final character = text[index];

    if (state == _CsvFieldState.quoted) {
      if (character == '"') {
        if (index + 1 < text.length && text[index + 1] == '"') {
          cell.write('"');
          index += 2;
          columnNumber += 2;
        } else {
          state = _CsvFieldState.afterQuote;
          index++;
          columnNumber++;
        }
        continue;
      }

      if (character == '\r' || character == '\n') {
        final endingLength = lineEndingLength(index);
        cell.write(text.substring(index, index + endingLength));
        index += endingLength;
        lineNumber++;
        columnNumber = 0;
        continue;
      }

      cell.write(character);
      index++;
      columnNumber++;
      continue;
    }

    if (state == _CsvFieldState.afterQuote) {
      if (character == delimiter) {
        finishField();
        index++;
        columnNumber++;
        continue;
      }

      if (character == '\r' || character == '\n') {
        finishRecord();
        index += lineEndingLength(index);
        lineNumber++;
        columnNumber = 0;
        continue;
      }

      final message = character.trim().isEmpty
          ? 'Espaços após aspas fechadas não são aceitos; use somente o '
                'delimitador ou o fim da linha'
          : 'Caractere inválido após aspas fechadas; use somente o '
                'delimitador ou o fim da linha';
      throw StudentImportFileException(
        '$message (linha $lineNumber, coluna ${columnNumber + 1}).',
      );
    }

    if (character == '"') {
      if (cell.isNotEmpty) {
        throw StudentImportFileException(
          'Aspas só podem iniciar um campo. Use aspas duplas escapadas dentro '
          'de campos entre aspas (linha $lineNumber, coluna ${columnNumber + 1}).',
        );
      }
      state = _CsvFieldState.quoted;
      openingQuoteLine = lineNumber;
      openingQuoteColumn = columnNumber + 1;
      index++;
      columnNumber++;
      continue;
    }

    if (character == delimiter) {
      finishField();
      index++;
      columnNumber++;
      continue;
    }

    if (character == '\r' || character == '\n') {
      finishRecord();
      index += lineEndingLength(index);
      lineNumber++;
      columnNumber = 0;
      continue;
    }

    cell.write(character);
    index++;
    columnNumber++;
  }

  if (state == _CsvFieldState.quoted) {
    throw StudentImportFileException(
      'Aspas sem fechamento iniciadas na linha $openingQuoteLine, '
      'coluna $openingQuoteColumn.',
    );
  }

  if (cell.isNotEmpty || row.isNotEmpty || state == _CsvFieldState.afterQuote) {
    finishField();
    records.add(row);
  }
  return records;
}
