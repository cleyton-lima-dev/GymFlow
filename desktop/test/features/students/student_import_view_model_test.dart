import 'dart:convert';
import 'dart:typed_data';

import 'package:avelri_gestao/core/network/api_client.dart';
import 'package:avelri_gestao/features/students/data/student_import_api_service.dart';
import 'package:avelri_gestao/features/students/presentation/student_import_view_model.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

const _batchId = '00000000-0000-0000-0000-000000000130';

Map<String, dynamic> _duplicatePreview() => {
  'batchId': _batchId,
  'alreadyCompleted': false,
  'rows': [
    {
      'rowNumber': 2,
      'isValid': true,
      'errors': <String>[],
      'candidates': [
        {
          'name': 'Aluna existente sintética',
          'matchBy': 'Email',
          'studentId': 'synthetic-student-id',
          'email': 'existing@example.invalid',
          'phone': null,
          'isArchived': false,
        },
      ],
    },
    {
      'rowNumber': 3,
      'isValid': true,
      'errors': <String>[],
      'candidates': <Object>[],
    },
  ],
  'results': <Object>[],
};

void main() {
  test('possible duplicate requires an explicit valid decision', () async {
    var previewRequests = 0;
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid/',
      client: MockClient((request) async {
        if (request.url.path == '/api/students/imports/preview') {
          previewRequests++;
          return http.Response(jsonEncode(_duplicatePreview()), 200);
        }
        return http.Response('Not found', 404);
      }),
    );
    final viewModel = await _readyViewModel(api);
    addTearDown(() {
      viewModel.dispose();
      api.close();
    });

    expect(viewModel.decisions[2], isNull);
    expect(viewModel.decisions[3], 'Import');
    viewModel.chooseNewStudentsStatus(true);
    expect(viewModel.canConfirm, isFalse);

    // A stale UI value by itself is not proof of an Admin decision.
    viewModel.decisions[2] = 'ImportAsNew';
    expect(viewModel.canConfirm, isFalse);
    viewModel.decisions.remove(2);
    viewModel.chooseDecision(2, 'Import');
    expect(viewModel.decisions[2], isNull);
    expect(viewModel.canConfirm, isFalse);

    viewModel.chooseDecision(2, 'Ignore');
    expect(viewModel.canConfirm, isTrue);

    await viewModel.createPreview();
    expect(previewRequests, 2);
    expect(viewModel.decisions[2], isNull);
    expect(viewModel.decisions[3], 'Import');
    expect(viewModel.canConfirm, isFalse);
  });

  test('explicit create-separately choice permits confirmation', () async {
    final api = ApiClient(
      baseUrl: 'https://synthetic.invalid/',
      client: MockClient((request) async {
        if (request.url.path == '/api/students/imports/preview') {
          return http.Response(jsonEncode(_duplicatePreview()), 200);
        }
        return http.Response('Not found', 404);
      }),
    );
    final viewModel = await _readyViewModel(api);
    addTearDown(() {
      viewModel.dispose();
      api.close();
    });

    viewModel.chooseNewStudentsStatus(false);
    expect(viewModel.canConfirm, isFalse);
    viewModel.chooseDecision(2, 'ImportAsNew');
    expect(viewModel.decisions[2], 'ImportAsNew');
    expect(viewModel.canConfirm, isTrue);
  });
}

Future<StudentImportViewModel> _readyViewModel(ApiClient api) async {
  final viewModel = StudentImportViewModel(StudentImportApiService(api));
  viewModel.loadFile(
    'synthetic-duplicates.csv',
    Uint8List.fromList(utf8.encode('Nome\nAluna nova\nPossível repetida\n')),
  );
  await viewModel.createPreview();
  return viewModel;
}
