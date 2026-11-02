import 'dart:convert';
import 'dart:typed_data';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/core/network/api_exception.dart';
import 'package:avelri_gestao/features/students/data/student_import_api_service.dart';
import 'package:avelri_gestao/features/students/data/student_import_parser.dart';
import 'package:avelri_gestao/features/students/presentation/student_import_view_model.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

Map<String, dynamic> _previewRow(int rowNumber, String name) => {
  'rowNumber': rowNumber,
  'name': name,
  'email': null,
  'phone': null,
  'birthDate': null,
};

int _serializedPreviewBytes(List<Map<String, dynamic>> rows) =>
    utf8.encode(jsonEncode({'rows': rows})).length;

String _repeat(String value, int count) {
  final buffer = StringBuffer();
  for (var index = 0; index < count; index++) {
    buffer.write(value);
  }
  return buffer.toString();
}

List<Map<String, dynamic>> _rowsAtPreviewBodyLimit() {
  final rows = List.generate(
    500,
    (index) => _previewRow(index + 2, _repeat('x', 10_000)),
  );
  var size = _serializedPreviewBytes(rows);
  var rowIndex = rows.length - 1;

  while (size > StudentImportApiService.previewRequestBodyLimitBytes) {
    while (rowIndex >= 0 && (rows[rowIndex]['name'] as String).isEmpty) {
      rowIndex--;
    }
    if (rowIndex < 0) throw StateError('Could not size the synthetic payload.');

    final name = rows[rowIndex]['name'] as String;
    final excess = size - StudentImportApiService.previewRequestBodyLimitBytes;
    final removeCount = name.length < excess ? name.length : excess;
    rows[rowIndex]['name'] = name.substring(0, name.length - removeCount);
    size = _serializedPreviewBytes(rows);
  }

  if (size != StudentImportApiService.previewRequestBodyLimitBytes) {
    throw StateError('The synthetic payload did not reach the byte boundary.');
  }
  return rows;
}

void main() {
  group('StudentImportApiService preview request size', () {
    test(
      'counts UTF-8 bytes after JSON escaping and Unicode encoding',
      () async {
        final payload = {'value': 'ação 🚀 \\ " \n \u0001'};
        final expectedBytes = utf8.encode(jsonEncode(payload)).length;
        var sentBytes = 0;
        final client = ApiClient(
          baseUrl: 'http://127.0.0.1:1/',
          client: MockClient((request) async {
            sentBytes = request.bodyBytes.length;
            return http.Response('{}', 200);
          }),
        );
        addTearDown(client.close);

        await client.post(
          'synthetic',
          body: payload,
          maxBodyBytes: expectedBytes,
        );

        expect(sentBytes, expectedBytes);
        await expectLater(
          client.post(
            'synthetic',
            body: payload,
            maxBodyBytes: expectedBytes - 1,
          ),
          throwsA(
            isA<ApiException>().having(
              (exception) => exception.statusCode,
              'statusCode',
              413,
            ),
          ),
        );
        expect(sentBytes, expectedBytes);
      },
    );

    test('sends a payload exactly at the API byte limit', () async {
      var sentBytes = 0;
      final client = ApiClient(
        baseUrl: 'http://127.0.0.1:1/',
        client: MockClient((request) async {
          sentBytes = request.bodyBytes.length;
          return http.Response(
            '{"batchId":"synthetic","alreadyCompleted":false,"rows":[],"results":[]}',
            200,
            headers: {'content-type': 'application/json'},
          );
        }),
      );
      addTearDown(client.close);

      final rows = _rowsAtPreviewBodyLimit();
      expect(
        _serializedPreviewBytes(rows),
        StudentImportApiService.previewRequestBodyLimitBytes,
      );

      await StudentImportApiService(client).preview(rows);

      expect(sentBytes, StudentImportApiService.previewRequestBodyLimitBytes);
    });

    test(
      'blocks a payload one byte over the API limit before sending',
      () async {
        var requestCount = 0;
        final client = ApiClient(
          baseUrl: 'http://127.0.0.1:1/',
          client: MockClient((_) async {
            requestCount++;
            return http.Response('{}', 200);
          }),
        );
        addTearDown(client.close);

        final rows = _rowsAtPreviewBodyLimit();
        rows.last['name'] = '${rows.last['name']}x';

        expect(
          _serializedPreviewBytes(rows),
          StudentImportApiService.previewRequestBodyLimitBytes + 1,
        );
        await expectLater(
          StudentImportApiService(client).preview(rows),
          throwsA(
            isA<ApiException>().having(
              (exception) => exception.statusCode,
              'statusCode',
              413,
            ),
          ),
        );
        expect(requestCount, 0);
      },
    );

    test(
      'blocks a near-limit CSV whose control characters expand in JSON',
      () async {
        const name = '\u0001';
        final sourceBuilder = StringBuffer('Nome;Observacao\n')
          ..write(_repeat(name, 850_000))
          ..write(';')
          ..write(_repeat('x', 4_000_000))
          ..write('\n');
        final source = sourceBuilder.toString();
        final sourceBytes = Uint8List.fromList(utf8.encode(source));
        expect(
          sourceBytes.length,
          lessThanOrEqualTo(maxStudentImportFileBytes),
        );
        expect(sourceBytes.length, greaterThan(4_800_000));

        var requestCount = 0;
        final client = ApiClient(
          baseUrl: 'http://127.0.0.1:1/',
          client: MockClient((_) async {
            requestCount++;
            return http.Response('{}', 200);
          }),
        );
        addTearDown(client.close);

        final viewModel = StudentImportViewModel(
          StudentImportApiService(client),
        );
        viewModel.loadFile('synthetic.csv', sourceBytes);
        final row = viewModel.source!.rows.single;
        expect(viewModel.mapping[StudentImportField.name], 0);
        final rows = [_previewRow(row.rowNumber, row.cells.first.trim())];
        final sourceFieldBytes = utf8.encode(row.cells.first).length;
        final serializedBytes = _serializedPreviewBytes(rows);
        expect(
          sourceFieldBytes,
          lessThan(StudentImportApiService.previewRequestBodyLimitBytes),
        );
        expect(
          serializedBytes,
          greaterThan(StudentImportApiService.previewRequestBodyLimitBytes),
        );

        await viewModel.createPreview();
        expect(viewModel.errorMessage, contains('5.000.000 bytes'));
        expect(requestCount, 0);
      },
    );
  });
}
