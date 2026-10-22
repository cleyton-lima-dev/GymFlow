import 'dart:convert';
import 'dart:typed_data';

import 'package:archive/archive.dart';
import 'package:excel/excel.dart';

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
  try {
    _validateZipDirectory(bytes);
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

    final workbook = Excel.decodeBytes(bytes);
    if (workbook.tables.isEmpty) {
      throw const StudentImportFileException(
        'A planilha não contém uma aba com dados.',
      );
    }
    final sheet = workbook.tables[workbook.tables.keys.first]!;
    if (sheet.maxRows > maxStudentImportRows + 1 ||
        sheet.maxColumns > maxStudentImportColumns) {
      throw const StudentImportFileException(
        'A primeira aba deve ter no máximo 5.000 registros e 100 colunas.',
      );
    }
    final records = <List<String>>[];
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
    return _toParsedFile(fileName, records);
  } on StudentImportFileException {
    rethrow;
  } catch (_) {
    throw const StudentImportFileException(
      'Não foi possível ler a primeira aba do XLSX.',
    );
  }
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

List<List<String>> _readCsv(String text, String delimiter) {
  final records = <List<String>>[];
  var row = <String>[];
  final cell = StringBuffer();
  var quoted = false;

  for (var index = 0; index < text.length; index++) {
    final character = text[index];
    if (quoted) {
      if (character == '"') {
        if (index + 1 < text.length && text[index + 1] == '"') {
          cell.write('"');
          index++;
        } else {
          quoted = false;
        }
      } else {
        cell.write(character);
      }
      continue;
    }

    if (character == '"' && cell.isEmpty) {
      quoted = true;
    } else if (character == delimiter) {
      row.add(cell.toString());
      cell.clear();
    } else if (character == '\n' || character == '\r') {
      if (character == '\r' &&
          index + 1 < text.length &&
          text[index + 1] == '\n') {
        index++;
      }
      row.add(cell.toString());
      cell.clear();
      records.add(row);
      row = <String>[];
    } else {
      cell.write(character);
    }
  }
  if (quoted) {
    throw const StudentImportFileException(
      'O CSV contém aspas sem fechamento.',
    );
  }
  if (cell.isNotEmpty || row.isNotEmpty) {
    row.add(cell.toString());
    records.add(row);
  }
  return records;
}
