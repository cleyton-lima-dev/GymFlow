import 'package:avelri_gestao/features/students/data/column_header_mapper.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('ColumnHeaderMapper', () {
    test('recognizes technical names independent of case and separators', () {
      final headers = ['EXTERNAL_ID', 'Duration Months', 'billingCycle'];

      expect(ColumnHeaderMapper.suggest(headers, 'externalId').index, 0);
      expect(ColumnHeaderMapper.suggest(headers, 'durationMonths').index, 1);
      expect(ColumnHeaderMapper.suggest(headers, 'billingCycle').index, 2);
    });

    test('recognizes Portuguese accents and common English aliases', () {
      final headers = [
        'Código externo',
        'Duração em meses',
        'Ciclo de cobrança',
      ];

      expect(ColumnHeaderMapper.suggest(headers, 'externalId').index, 0);
      expect(ColumnHeaderMapper.suggest(headers, 'durationMonths').index, 1);
      expect(ColumnHeaderMapper.suggest(headers, 'billingCycle').index, 2);
    });

    test('recognizes external enrollment and charge identifiers', () {
      final headers = ['ID Externo da Matrícula', 'Charge ID'];

      expect(
        ColumnHeaderMapper.suggest(headers, 'enrollmentExternalId').index,
        0,
      );
      expect(ColumnHeaderMapper.suggest(headers, 'chargeExternalId').index, 1);
    });

    test('does not choose when multiple headers match the same field', () {
      final suggestion = ColumnHeaderMapper.suggest([
        'externalId',
        'ID externo',
      ], 'externalId');

      expect(suggestion.index, isNull);
      expect(suggestion.isAmbiguous, isTrue);
    });
  });
}
