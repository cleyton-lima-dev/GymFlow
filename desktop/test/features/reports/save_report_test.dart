import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:avelri_gestao/features/reports/presentation/save_report.dart';

void main() {
  test('CSV file preserves bytes and requires explicit overwrite', () async {
    final directory = await Directory.systemTemp.createTemp(
      'gymflow-reports-test-',
    );
    try {
      final path = '${directory.path}${Platform.pathSeparator}synthetic.csv';
      const bytes = [0xef, 0xbb, 0xbf, 65, 59, 66, 13, 10];
      await writeCsvFile(path, bytes);
      expect(await File(path).readAsBytes(), bytes);
      await expectLater(
        writeCsvFile(path, [0]),
        throwsA(isA<FileSystemException>()),
      );
      expect(await File(path).readAsBytes(), bytes);
      await writeCsvFile(path, [1], overwrite: true);
      expect(await File(path).readAsBytes(), [1]);
    } finally {
      await directory.delete(recursive: true);
    }
  });
  test('relative, remote and non-CSV destinations are rejected', () {
    for (final path in [
      'relative.csv',
      r'\\server\share\report.csv',
      '${Directory.systemTemp.path}${Platform.pathSeparator}report.exe',
    ]) {
      expect(() => csvFile(path), throwsA(isA<FileSystemException>()));
    }
  });
}
